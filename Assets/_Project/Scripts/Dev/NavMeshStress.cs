using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace SomeonesHome
{
    /// <summary>
    /// Prueba de estrés de navegación (Semana 15 · QA y Profiling). El juego no usa NavMesh (la criatura corre por el
    /// terreno con un script), así que esto mide qué costaría: hornea un NavMesh sobre el nivel real (terreno, rocas,
    /// casa, cercas) y lanza N agentes que persiguen a un objetivo que se mueve, como haría un EnemyAI.
    ///
    /// Dos versiones del mismo comportamiento, medidas con la misma build, la misma escena y las mismas cantidades:
    ///  · ingenua    → cada agente llama SetDestination cada frame y usa la evitación de calidad alta (lo típico).
    ///  · optimizada → recálculo escalonado cada 0,5 s (round-robin: 1/30 de los agentes por frame), solo si el objetivo
    ///                 se movió más de 1 m, y evitación de calidad baja; los lejanos (&gt; 35 m) no evitan entre sí.
    ///
    /// Por cada cantidad registra con ProfilerRecorder: frame time, hilo principal, los marcadores de navegación
    /// (NavMeshManager = actualización del crowd, Crowd.Pathfinding, Crowd.Avoidance…), el coste de las llamadas del
    /// script (SetDestination) y el GC Alloc. Además mide el horneado y el coste de NavMesh.CalculatePath.
    ///
    /// Build de desarrollo:  ./SomeonesHome.x86_64 -navStress -navOut /ruta/nav.json [-navCounts 0,25,50,100,200,400]
    ///                       ./SomeonesHome.x86_64 -navStress -navHold 400 -navMode 0   (se queda así para el Profiler)
    /// No está en la escena: solo existe mientras mide.
    /// </summary>
    public class NavMeshStress : MonoBehaviour
    {
        public string outPath;
        public int[] counts = { 0, 25, 50, 100, 200, 400 };
        public float warmup = 3f, sample = 6f;
        public int holdCount = -1, holdMode = 0;
        public static bool Active { get; private set; }
        public string Status { get; private set; } = "inicio";

        // Zona de juego: alrededor de la casa y el campo (donde la criatura persigue al jugador).
        static readonly Vector3 Center = new(92f, 0f, 78f);
        const float AreaRadius = 38f;

        readonly List<NavMeshAgent> agents = new();
        readonly List<Vector3> lastGoal = new();
        Transform target;
        NavMeshSurface surface;
        NavMeshDataInstance loaded;
        int mode;                     // 0 ingenua · 1 optimizada
        int cursor;                   // round-robin de la versión optimizada
        long scriptTicks;             // tiempo de las llamadas del script en este frame
        Mesh capsule;
        Material agentMat;
        FirstPersonController player;

        static readonly string[] Markers =
        {
            "NavMeshManager", "Crowd.Pathfinding", "Crowd.Avoidance", "Crowd.Steering", "Crowd.Proximity.Collect",
            "Crowd.Proximity.Insert", "Crowd.Corners", "Crowd.PathOptimization", "Crowd.RepathTrigger", "Crowd.Integrate",
            "Crowd.Collision", "Crowd.Movement", "Components.NavMeshAgent.Transform",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            Active = false;
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-navStress") < 0) return;
            Active = true;
            var s = new GameObject("NavMeshStress").AddComponent<NavMeshStress>();
            s.outPath = Arg(args, "-navOut");
            var c = Arg(args, "-navCounts");
            if (!string.IsNullOrEmpty(c)) s.counts = System.Array.ConvertAll(c.Split(','), int.Parse);
            if (int.TryParse(Arg(args, "-navHold"), out int h)) s.holdCount = h;
            if (int.TryParse(Arg(args, "-navMode"), out int m)) s.holdMode = m;
        }

        static string Arg(string[] a, string key)
        {
            int i = System.Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        IEnumerator Start()
        {
            Active = true;
            // En una Development Build cualquier error abre la consola de desarrollo (IMGUI), cuya fuente no está en la
            // build: avisa en cada frame y se redibuja (bug QA-05, +3 ms y 300 KB de GC por frame). No debe medirse eso.
            Debug.developerConsoleEnabled = false;
            if (string.IsNullOrEmpty(outPath)) outPath = Path.Combine(Application.persistentDataPath, "navmesh_stress.json");
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            yield return null;
            yield return null;
            Freeze();

            // ── NavMesh ──
            // En la build se carga el horneado del Editor (Resources/Nav/NavMesh_Estres): las mallas de rocas, tocones y
            // troncos no tienen Read/Write, y el horneado en tiempo de ejecución las salta (bug QA-06). En el Editor
            // (o si falta el asset) se hornea aquí y se mide el tiempo.
            Status = "horneando";
            var bake = new List<float>();
            string navInfo = "";
            var baked = Resources.Load<NavMeshData>("Nav/NavMesh_Estres");
            string source = "runtime";
            if (baked && !Application.isEditor)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                loaded = NavMesh.AddNavMeshData(baked);
                bake.Add(Ms(System.Diagnostics.Stopwatch.GetTimestamp() - t0));
                source = "asset";
                yield return null;
            }
            else
                for (int i = 0; i < 3; i++)
                {
                    if (surface) { surface.RemoveData(); Destroy(surface.gameObject); yield return null; }
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    Bake();
                    bake.Add(Ms(System.Diagnostics.Stopwatch.GetTimestamp() - t0));
                    yield return null;
                }
            var tri = NavMesh.CalculateTriangulation();
            navInfo = $"\"navmesh_source\":\"{source}\",\"navmesh_vertices\":{tri.vertices.Length},\"navmesh_triangles\":{tri.indices.Length / 3}";

            capsule = BuildCapsuleMesh();
            agentMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.08f, 0.06f), enableInstancing = true };
            target = new GameObject("Objetivo (jugador simulado)").transform;
            Random.InitState(1234);                                   // mismas posiciones en todas las corridas

            if (holdCount >= 0)
            {
                mode = holdMode;
                Spawn(holdCount);
                Status = $"hold {holdCount} modo {mode}";
                while (true) { Tick(); yield return null; }
            }

            // ── Coste de pathfinding puro (CalculatePath, 500 pares fijos) ──
            Status = "calculatepath";
            string pathStats = PathfindingCost(500);

            // ── Escalado con N agentes, versión ingenua y optimizada intercaladas ──
            var runs = new List<string>();
            foreach (int n in counts)
                for (int m = 0; m < 2; m++)
                {
                    mode = m;
                    Clear();
                    Spawn(n);
                    Status = $"{n} agentes · {(m == 0 ? "ingenua" : "optimizada")}";
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < warmup) { Tick(); yield return null; }
                    if (n == 200 && m == 0)
                        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(outPath) ?? ".", "navmesh_200_agentes.png"));
                    yield return Measure(n, m, runs);
                }
            Clear();

            var sb = new StringBuilder();
            sb.Append("{\"unity\":\"").Append(Application.unityVersion).Append("\",\"development\":").Append(Debug.isDebugBuild ? "true" : "false")
              .Append(",\"gfx\":\"").Append(SystemInfo.graphicsDeviceType).Append("\",\"cpu\":\"").Append(SystemInfo.processorType.Replace("\"", "'"))
              .Append("\",\"cpu_cores\":").Append(SystemInfo.processorCount)
              .Append(",\"screen\":\"").Append(Screen.width).Append('x').Append(Screen.height).Append("\",")
              .Append("\"bake_ms\":[").Append(string.Join(",", bake.ConvertAll(F))).Append("],").Append(navInfo)
              .Append(",\"calculate_path\":").Append(pathStats)
              .Append(",\"runs\":[").Append(string.Join(",", runs)).Append("]}");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[NavMeshStress] {runs.Count} corridas → {outPath}");
            Status = "fin";
            if (!Application.isEditor) Application.Quit();
        }

        // ════════════════════════════════════════════════════════════════
        void Freeze()
        {
            var director = FindAnyObjectByType<Level1Director>();
            if (director) { director.StopAllCoroutines(); director.enabled = false; }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) c.enabled = false;
            player = FindAnyObjectByType<FirstPersonController>();
            if (player) { player.InputLocked = true; player.LookLocked = true; }
            if (Weather.Instance) Weather.Instance.Freeze(0f, 0.3f, 0f);
            if (Atmosphere.Instance) Atmosphere.Instance.frozen = true;
            // Cámara fija sobre la zona: se ve a los agentes y el render cambia poco entre cantidades.
            if (player)
            {
                var p = new Vector3(92f, 0f, 46f);
                var t = Terrain.activeTerrain;
                p.y = t ? t.SampleHeight(p) + t.transform.position.y + 0.1f : 20f;
                player.Teleport(p, Quaternion.Euler(0f, 0f, 0f));
                player.Pitch = 12f;
            }
        }

        void Bake()
        {
            var go = new GameObject("NavMesh (prueba de estrés)");
            surface = go.AddComponent<NavMeshSurface>();
            Configure(surface);     // terreno, rocas, casa y cercas; sin lo que está en Ignore Raycast (personajes, triggers)
            surface.BuildNavMesh();
        }

        Vector3 RandomPoint()
        {
            for (int i = 0; i < 30; i++)
            {
                var c = Random.insideUnitCircle * AreaRadius;
                var p = Center + new Vector3(c.x, 0f, c.y);
                var t = Terrain.activeTerrain;
                p.y = t ? t.SampleHeight(p) + t.transform.position.y : 20f;
                if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) return hit.position;
            }
            return Center;
        }

        void Spawn(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Agente " + i);
                go.transform.position = RandomPoint();
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = capsule;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = agentMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var a = go.AddComponent<NavMeshAgent>();
                a.speed = 3.5f; a.acceleration = 10f; a.angularSpeed = 360f; a.radius = 0.35f; a.height = 1.7f;
                a.stoppingDistance = 1.2f; a.baseOffset = 1f;
                a.obstacleAvoidanceType = mode == 0 ? ObstacleAvoidanceType.HighQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                a.avoidancePriority = 50;
                agents.Add(a);
                lastGoal.Add(new Vector3(9999f, 0f, 9999f));
            }
            cursor = 0;
        }

        void Clear()
        {
            foreach (var a in agents) if (a) Destroy(a.gameObject);
            agents.Clear();
            lastGoal.Clear();
        }

        /// <summary>El "jugador" da vueltas por el campo: todos lo persiguen, cada uno a un punto a su alrededor.</summary>
        void Tick()
        {
            float t = Time.time * 0.12f;
            var goal = Center + new Vector3(Mathf.Cos(t) * 18f, 0f, Mathf.Sin(t * 1.3f) * 14f);
            if (NavMesh.SamplePosition(goal, out var hit, 6f, NavMesh.AllAreas)) target.position = hit.position;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int n = agents.Count;
            if (mode == 0)
            {
                // Ingenua: SetDestination de todos, cada frame.
                for (int i = 0; i < n; i++) agents[i].SetDestination(Goal(i));
            }
            else if (n > 0)
            {
                // Optimizada: cada agente se actualiza cada ~0,5 s (a 60 FPS: n/30 por frame) y solo si su meta se movió.
                int per = Mathf.Max(1, Mathf.CeilToInt(n / 30f));
                for (int k = 0; k < per; k++)
                {
                    int i = cursor; cursor = (cursor + 1) % n;
                    var a = agents[i];
                    var g = Goal(i);
                    if ((g - lastGoal[i]).sqrMagnitude > 1f) { a.SetDestination(g); lastGoal[i] = g; }
                    // Lejos del objetivo no hace falta esquivarse con cuidado.
                    bool far = (a.transform.position - target.position).sqrMagnitude > 35f * 35f;
                    var want = far ? ObstacleAvoidanceType.NoObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                    if (a.obstacleAvoidanceType != want) a.obstacleAvoidanceType = want;
                }
            }
            scriptTicks = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        }

        Vector3 Goal(int i)
        {
            // Rodean al objetivo (anillo de 2–5 m), así no se amontonan todos en el mismo punto.
            float ang = i * 2.399963f;   // ángulo áureo
            float r = 2f + (i % 7) * 0.5f;
            return target.position + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }

        string PathfindingCost(int pairs)
        {
            var path = new NavMeshPath();
            var times = new List<float>(pairs);
            int complete = 0, corners = 0;
            Random.InitState(99);
            var a = new Vector3[pairs]; var b = new Vector3[pairs];
            for (int i = 0; i < pairs; i++) { a[i] = RandomPoint(); b[i] = RandomPoint(); }
            for (int i = 0; i < pairs; i++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                NavMesh.CalculatePath(a[i], b[i], NavMesh.AllAreas, path);
                times.Add(Ms(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000f);   // µs
                if (path.status == NavMeshPathStatus.PathComplete) complete++;
                corners += path.corners.Length;
            }
            times.Sort();
            float sum = 0f; foreach (var x in times) sum += x;
            return $"{{\"pairs\":{pairs},\"complete\":{complete},\"avg_corners\":{F((float)corners / pairs)}," +
                   $"\"us_avg\":{F(sum / pairs)},\"us_med\":{F(times[pairs / 2])},\"us_p95\":{F(times[(int)(pairs * 0.95f)])},\"us_max\":{F(times[pairs - 1])}}}";
        }

        IEnumerator Measure(int n, int m, List<string> runs)
        {
            var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 4096);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 4096);
            var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 4096);
            var dc = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SRP Batcher Draw Calls Count", 4096);
            var marks = new ProfilerRecorder[Markers.Length];
            for (int i = 0; i < Markers.Length; i++) marks[i] = ProfilerRecorder.StartNew(ProfilerCategory.Ai, Markers[i], 4096);
            var dts = new List<float>(4096);
            var script = new List<float>(4096);
            int f0 = Time.frameCount;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < sample)
            {
                Tick();
                script.Add(Ms(scriptTicks));
                yield return null;
                dts.Add(Time.unscaledDeltaTime * 1000f);
            }
            int frames = Time.frameCount - f0;
            float secs = Time.realtimeSinceStartup - t0;
            int stuck = 0;
            foreach (var a in agents) if (a.pathStatus != NavMeshPathStatus.PathComplete) stuck++;

            var sb = new StringBuilder();
            sb.Append("{\"agents\":").Append(n).Append(",\"mode\":\"").Append(m == 0 ? "ingenua" : "optimizada")
              .Append("\",\"frames\":").Append(frames).Append(",\"fps\":").Append(F(frames / secs))
              .Append(",\"paths_incomplete\":").Append(stuck);
            Stats(sb, "frame_ms", dts);
            Stats(sb, "script_ms", script);
            Stats(sb, "main_ms", Values(main, 1e-6));
            Stats(sb, "gpu_ms", Values(gpu, 1e-6));
            Stats(sb, "gc_kb", Values(gc, 1.0 / 1024.0));
            Stats(sb, "draw_calls", Values(dc, 1.0));
            for (int i = 0; i < Markers.Length; i++) Stats(sb, "m_" + Markers[i], Values(marks[i], 1e-6));
            sb.Append('}');
            runs.Add(sb.ToString());
            main.Dispose(); gc.Dispose(); gpu.Dispose(); dc.Dispose();
            foreach (var r in marks) r.Dispose();
        }

        static List<float> Values(ProfilerRecorder r, double scale)
        {
            var v = new List<float>(r.Count);
            for (int k = 0; k < r.Count; k++) v.Add((float)(r.GetSample(k).Value * scale));
            return v;
        }

        static void Stats(StringBuilder sb, string key, List<float> v)
        {
            if (v.Count == 0) { sb.Append(",\"").Append(key).Append("\":null"); return; }
            v.Sort();
            float sum = 0f; foreach (var x in v) sum += x;
            sb.Append(",\"").Append(key).Append("\":{\"avg\":").Append(F(sum / v.Count))
              .Append(",\"med\":").Append(F(v[v.Count / 2]))
              .Append(",\"p95\":").Append(F(v[Mathf.Min(v.Count - 1, (int)(v.Count * 0.95f))]))
              .Append(",\"max\":").Append(F(v[v.Count - 1])).Append('}');
        }

        static float Ms(long ticks) => ticks * 1000f / System.Diagnostics.Stopwatch.Frequency;
        static string F(float x) => x.ToString("0.####", CultureInfo.InvariantCulture);

        static Mesh BuildCapsuleMesh()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            return m;
        }

        void OnDestroy()
        {
            Active = false;
            if (surface) surface.RemoveData();
            if (loaded.valid) loaded.Remove();
        }

        /// <summary>Mismos ajustes que el horneado del Editor (NavMeshStressBake).</summary>
        public static void Configure(NavMeshSurface s)
        {
            s.collectObjects = CollectObjects.Volume;
            s.center = Center + Vector3.up * 20f;
            s.size = new Vector3(AreaRadius * 2f + 20f, 80f, AreaRadius * 2f + 20f);
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = ~(1 << 2);
        }
    }
}
