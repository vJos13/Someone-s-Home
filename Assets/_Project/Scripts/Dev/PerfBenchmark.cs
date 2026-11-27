using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Medición de rendimiento reproducible (Semana 14: "mismas condiciones antes y después").
    /// Recorre puntos de vista fijos del Relato 1 con el clima y el sol congelados, y en cada uno
    /// calienta unos segundos y luego registra con ProfilerRecorder: frame time de CPU (hilo principal
    /// y de render), GPU, GC Alloc, draw calls, SetPass, batches, triángulos, sombras y memoria.
    ///
    /// Cómo se lanza:
    ///  · Build de desarrollo:  ./SomeonesHome.x86_64 -benchmark -benchmarkLabel ANTES -benchmarkOut /ruta/res.json
    ///    (al terminar escribe el JSON y cierra el juego).
    ///  · Editor (Play): añadir el componente a cualquier objeto; deja el JSON en <see cref="outPath"/>.
    ///
    /// No está en la escena y no cambia nada del juego: solo existe mientras mide.
    /// </summary>
    public class PerfBenchmark : MonoBehaviour
    {
        public string label = "editor";
        public string outPath;
        public float warmup = 2.5f;
        public float sample = 6f;
        public int repetitions = 3;
        public bool screenshots = true;
        /// <summary>Si es ≥ 0, solo congela y se queda en esa vista (para grabar con el Profiler), sin medir.</summary>
        public int holdView = -1;
        /// <summary>Preset de GRÁFICOS a medir (0 Baja … 3 Ultra); -1 = no tocar.</summary>
        public int preset = -1;
        /// <summary>Hay una medición en curso: GraphicsQuality no toca VSync ni el límite de FPS.</summary>
        public static bool Active { get; private set; }
        /// <summary>
        /// Estado a medir dentro de la misma build: 6 = todas las optimizaciones (lo que trae el juego).
        /// Un número menor deshace en memoria las optimizaciones posteriores a ese número (ver OPTIMIZATION_LOG.md),
        /// así cada cambio se mide por separado con la misma build, la misma escena y las mismas vistas.
        /// </summary>
        public int config = 6;
        /// <summary>Al final compara AudioSource.PlayClipAtPoint con el pool de OneShotAudio (OPT-06).</summary>
        public bool poolTest;
        /// <summary>Efectos nuevos de la Semana 15 a apagar para medir su coste (shafts, water, vhs, haze).</summary>
        public string off = "";

        public bool Done { get; private set; }
        public string Status { get; private set; } = "inicio";

        struct View
        {
            public string name;
            public Vector3 feet;      // x,z del suelo; y = altura desde la que se busca el piso (interior) o NaN (terreno)
            public float yaw, pitch;
            public bool night;
        }

        // Puntos fijos: los mismos en todas las mediciones. Cubren las zonas más cargadas del nivel.
        static readonly View[] Views =
        {
            new() { name = "01_exterior_llegada",  feet = new Vector3(101.5f, float.NaN, 58.6f), yaw = 262f, pitch = 4f },
            new() { name = "02_interior_casa",     feet = new Vector3(91.5f, 22.5f, 56.5f),      yaw = 0f,   pitch = 4f },
            new() { name = "03_campo_bosque",      feet = new Vector3(86f, float.NaN, 80f),      yaw = 0f,   pitch = 2f },
            new() { name = "04_borde_colina",      feet = new Vector3(100f, float.NaN, 104f),    yaw = 350f, pitch = 8f },
            new() { name = "05_rio_columpio",      feet = new Vector3(90f, float.NaN, 121f),     yaw = 20f,  pitch = 2f },
            new() { name = "06_noche_linterna",    feet = new Vector3(92.3f, float.NaN, 64.4f),  yaw = 315f, pitch = 6f, night = true },
        };

        // Contadores del Profiler (los mismos que muestran el Profiler y el panel Stats).
        static readonly (string cat, string name)[] Counters =
        {
            ("Internal", "Main Thread"),
            ("Render", "CPU Main Thread Frame Time"),
            ("Render", "CPU Render Thread Frame Time"),
            ("Render", "CPU Total Frame Time"),
            ("Render", "GPU Frame Time"),
            // Unity 6.6 separa las draw calls por ruta; el total es la suma de estas.
            ("Render", "Standard Draw Calls Count"),
            ("Render", "Standard Instanced Draw Calls Count"),
            ("Render", "SRP Batcher Draw Calls Count"),
            ("Render", "BRG Draw Calls Count"),
            ("Render", "Instanced Batches Count"),
            ("Render", "Static Batches Count"),
            ("Render", "SetPass Calls Count"),
            ("Render", "Triangles Count"),
            ("Render", "Vertices Count"),
            ("Render", "Shadow Casters Count"),
            ("Memory", "GC Allocated In Frame"),
            ("Memory", "Total Used Memory"),
            ("Memory", "Gfx Used Memory"),
            ("Memory", "GC Used Memory"),
        };

        // ── Arranque desde la línea de comandos (build) ─────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-benchmark") < 0) return;
            Active = true;
            var b = new GameObject("PerfBenchmark").AddComponent<PerfBenchmark>();
            b.label = Arg(args, "-benchmarkLabel") ?? "build";
            b.outPath = Arg(args, "-benchmarkOut");
            if (int.TryParse(Arg(args, "-benchmarkReps"), out int reps)) b.repetitions = reps;
            if (int.TryParse(Arg(args, "-benchmarkConfig"), out int cfg)) b.config = cfg;
            if (int.TryParse(Arg(args, "-benchmarkPreset"), out int preset)) b.preset = preset;
            b.poolTest = System.Array.IndexOf(args, "-benchmarkPool") >= 0;
            if (System.Array.IndexOf(args, "-benchmarkNoViews") >= 0) b.repetitions = 0;
            b.off = Arg(args, "-benchmarkOff") ?? "";
        }

        static string Arg(string[] a, string key)
        {
            int i = System.Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(outPath))
                outPath = Path.Combine(Application.persistentDataPath, $"benchmark_{label}.json");
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            Debug.developerConsoleEnabled = false;   // ver QA-05: la consola de desarrollo entra en bucle si se abre

            yield return null;
            yield return null;
            Freeze();
            RevertAfter(config);
            ApplyOff(off);
            if (preset >= 0) { GraphicsQuality.LoadPreset(preset); GraphicsQuality.Apply(); }
            if (holdView >= 0)
            {
                var hv = Views[Mathf.Clamp(holdView, 0, Views.Length - 1)];
                if (hv.night) GoNight();
                Place(hv);
                Status = "hold " + hv.name;
                yield break;
            }

            var results = new List<string>();
            // Primero las vistas de día (repetidas e intercaladas), después la noche (no se vuelve al día).
            for (int pass = 0; pass < 2; pass++)
            {
                bool nightPass = pass == 1;
                if (nightPass) GoNight();
                for (int rep = 1; rep <= repetitions; rep++)
                    foreach (var v in Views)
                    {
                        if (v.night != nightPass) continue;
                        Status = $"{v.name} rep {rep}";
                        Place(v);
                        yield return new WaitForSecondsRealtime(warmup);
                        string shot = screenshots && rep == 1
                            ? Path.Combine(Path.GetDirectoryName(outPath) ?? ".", $"{label}_{v.name}.png") : null;
                        yield return Measure(v.name, rep, shot, results);
                    }
            }

            string pool = poolTest ? null : "null";
            if (poolTest) yield return PoolTest(r => pool = r);

            var sb = new StringBuilder();
            sb.Append("{\"label\":\"").Append(label).Append("\",\"config\":").Append(config).Append(",\"preset\":").Append(preset).Append(",\"pool\":").Append(pool).Append(",\"unity\":\"").Append(Application.unityVersion)
              .Append("\",\"editor\":").Append(Application.isEditor ? "true" : "false")
              .Append(",\"development\":").Append(Debug.isDebugBuild ? "true" : "false")
              .Append(",\"gfx\":\"").Append(SystemInfo.graphicsDeviceType).Append("\",\"gpu\":\"").Append(Esc(SystemInfo.graphicsDeviceName))
              .Append("\",\"cpu\":\"").Append(Esc(SystemInfo.processorType)).Append("\",\"screen\":\"").Append(Screen.width).Append('x').Append(Screen.height)
              .Append("\",\"quality\":\"").Append(QualitySettings.names[QualitySettings.GetQualityLevel()])
              .Append("\",\"runs\":[").Append(string.Join(",", results)).Append("]}");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[PerfBenchmark] {label}: {results.Count} corridas → {outPath}");
            Status = "fin";
            Done = true;
            if (!Application.isEditor) Application.Quit();
        }

        // ── Apagar efectos nuevos (Semana 15), uno por corrida, para atribuir el coste de GPU ──
        static void ApplyOff(string list)
        {
            if (string.IsNullOrEmpty(list)) return;
            foreach (var k in list.Split(','))
                switch (k.Trim())
                {
                    case "shafts": GameSettings.LightShafts = false; break;          // rayos de luz (pasada a pantalla completa)
                    case "water": GameSettings.WaterReflections = false; break;      // reflection probe en tiempo real del río
                    case "vhs": GameSettings.VhsFilter = 0; break;                   // filtro VHS (pasada a pantalla completa)
                    case "haze":                                                     // cono de luz de la linterna
                        foreach (var h in FindObjectsByType<FlashlightHaze>(FindObjectsInactive.Include)) h.enabled = false;
                        FlashlightHaze.Disabled = true; break;
                }
            GraphicsQuality.Apply();
        }

        // ── Deshacer optimizaciones (solo en memoria, para medir cada una) ──
        void RevertAfter(int keep)
        {
            if (keep >= 6) return;
            var rp = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset
                     ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (rp && keep < 5)
            {
                // Se modifica una COPIA del asset de URP: en el Editor, cambiar el asset original en Play lo deja
                // cambiado en disco (es un asset del proyecto, no un objeto de la escena).
                rp = Instantiate(rp);
                QualitySettings.renderPipeline = rp;
            }
            var terrain = Terrain.activeTerrain;
            if (keep < 5)   // OPT-05: terreno instanciado y sin Opaque Texture
            {
                if (terrain) terrain.drawInstanced = false;
                if (rp) rp.supportsCameraOpaqueTexture = true;
            }
            if (keep < 4)   // OPT-04: LOD bias, plano lejano y distancia del pasto
            {
                QualitySettings.lodBias = 2f;
                foreach (var v in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include))
                { var lens = v.Lens; lens.FarClipPlane = 400f; v.Lens = lens; }
                if (terrain) terrain.detailObjectDistance = 55f;
            }
            if (keep < 3)   // OPT-03: Occlusion Culling
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include)) c.useOcclusionCulling = false;
            if (keep < 2)   // OPT-02: 4 cascadas y sombras en todo
            {
                if (rp) rp.shadowCascadeCount = 4;
                var reg = PerfRegistry.Instance;
                if (reg && reg.shadowsOff != null)
                    foreach (var r in reg.shadowsOff) if (r) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            // OPT-01 (SSAO) no se puede deshacer en una build: cambia las variantes de shader. Se midió con dos builds.
        }

        // ── OPT-06: PlayClipAtPoint frente al pool, un sonido por frame ─────
        IEnumerator PoolTest(System.Action<string> done)
        {
            Status = "pool";
            var clip = AudioClip.Create("click", 4410, 1, 44100, false);   // 0,1 s, como un clic de interacción
            Vector3 pos = player ? player.transform.position + player.transform.forward * 2f : Vector3.zero;
            const int N = 100;
            var parts = new List<string>();
            for (int mode = 0; mode < 3; mode++)        // 0 = control sin sonidos, 1 = PlayClipAtPoint, 2 = pool
            {
                for (int w = 0; w < 40; w++) { Play(mode, clip, pos); yield return null; }   // el pool llega a su tamaño de trabajo
                yield return new WaitForSecondsRealtime(0.5f);
                var gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", N + 8);
                var gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count", N + 8);
                var times = new List<float>(N);
                int objs0 = FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
                int peak = 0;
                for (int i = 0; i < N; i++)
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    Play(mode, clip, pos);
                    times.Add((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1e6f / System.Diagnostics.Stopwatch.Frequency);
                    if (i == N / 2) peak = FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length - objs0;
                    yield return null;
                }
                long bytes = 0, count = 0;
                // La muestra de la mitad (FindObjectsByType) asigna un array: se descarta ese frame.
                for (int k = 0; k < gcBytes.Count; k++) bytes += gcBytes.GetSample(k).Value;
                for (int k = 0; k < gcCount.Count; k++) count += gcCount.GetSample(k).Value;
                int frames = gcBytes.Count;
                gcBytes.Dispose(); gcCount.Dispose();
                times.Sort();
                string name = mode == 0 ? "control" : mode == 1 ? "PlayClipAtPoint" : "pool";
                parts.Add($"{{\"mode\":\"{name}\",\"frames\":{frames},\"gc_bytes_total\":{bytes},\"gc_allocs_total\":{count}," +
                          $"\"cpu_us_med\":{F(times[N / 2])},\"cpu_us_p95\":{F(times[(int)(N * 0.95f)])},\"audiosources_alive_mid\":{peak}}}");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            done("[" + string.Join(",", parts) + "]");
        }

        static void Play(int mode, AudioClip clip, Vector3 pos)
        {
            if (mode == 1) AudioSource.PlayClipAtPoint(clip, pos, 0f);
            else if (mode == 2) OneShotAudio.Play(clip, pos, 0f);
        }

        // ── Condiciones fijas ───────────────────────────────────────────────
        FirstPersonController player;

        void Freeze()
        {
            // La historia no corre durante la medición: nada aparece, nada se mueve solo.
            var director = FindAnyObjectByType<Level1Director>();
            if (director) { director.StopAllCoroutines(); director.enabled = false; }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) c.enabled = false;

            player = FindAnyObjectByType<FirstPersonController>();
            if (player) { player.InputLocked = true; player.LookLocked = true; }

            // Lluvia moderada constante y el sol quieto: todas las corridas ven lo mismo.
            if (Weather.Instance) Weather.Instance.Freeze(0.55f, 0.6f, 0.35f);
            if (Atmosphere.Instance) Atmosphere.Instance.frozen = true;
        }

        void GoNight()
        {
            var night = FindAnyObjectByType<NightMode>();
            if (night) night.Apply();
            var flashlight = FindAnyObjectByType<Flashlight>(FindObjectsInactive.Include);
            if (flashlight) flashlight.Give();
        }

        void Place(View v)
        {
            if (!player) return;
            Vector3 p = v.feet;
            var terrain = Terrain.activeTerrain;
            if (float.IsNaN(p.y))
                p.y = terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;
            else if (Physics.Raycast(p, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            player.Teleport(p + Vector3.up * 0.05f, Quaternion.Euler(0f, v.yaw, 0f));
            player.Pitch = v.pitch;   // + mira hacia abajo
        }

        // ── Medición ────────────────────────────────────────────────────────
        IEnumerator Measure(string view, int rep, string shotPath, List<string> results)
        {
            var recs = new ProfilerRecorder[Counters.Length];
            for (int i = 0; i < Counters.Length; i++)
                recs[i] = ProfilerRecorder.StartNew(new ProfilerCategory(Counters[i].cat), Counters[i].name, 8192);
            var dts = new List<float>(4096);
            int f0 = Time.frameCount;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < sample)
            {
                yield return null;
                dts.Add(Time.unscaledDeltaTime * 1000f);
            }
            int frames = Time.frameCount - f0;
            float secs = Time.realtimeSinceStartup - t0;

            var sb = new StringBuilder();
            sb.Append("{\"view\":\"").Append(view).Append("\",\"rep\":").Append(rep)
              .Append(",\"frames\":").Append(frames).Append(",\"secs\":").Append(F(secs))
              .Append(",\"fps\":").Append(F(frames / secs));
            AppendStats(sb, "frame_ms", dts);
            for (int i = 0; i < Counters.Length; i++)
            {
                var r = recs[i];
                var vals = new List<float>(r.Count);
                // Los tiempos vienen en nanosegundos y la memoria en bytes: se pasan a ms y KB/MB.
                double scale = Counters[i].name.Contains("Time") || Counters[i].name == "Main Thread" ? 1e-6
                             : Counters[i].name == "GC Allocated In Frame" ? 1.0 / 1024.0
                             : Counters[i].name.Contains("Memory") ? 1.0 / (1024.0 * 1024.0) : 1.0;
                for (int k = 0; k < r.Count; k++) vals.Add((float)(r.GetSample(k).Value * scale));
                AppendStats(sb, Key(Counters[i].name), vals);
                r.Dispose();
            }
            sb.Append('}');
            results.Add(sb.ToString());

            if (shotPath != null)
            {
                ScreenCapture.CaptureScreenshot(shotPath);
                yield return null;
                yield return null;
            }
        }

        static string Key(string n) => n.ToLowerInvariant().Replace(' ', '_');

        static void AppendStats(StringBuilder sb, string key, List<float> v)
        {
            if (v.Count == 0) { sb.Append(",\"").Append(key).Append("\":null"); return; }
            v.Sort();
            double sum = 0; foreach (var x in v) sum += x;
            sb.Append(",\"").Append(key).Append("\":{\"n\":").Append(v.Count)
              .Append(",\"avg\":").Append(F(sum / v.Count))
              .Append(",\"med\":").Append(F(v[v.Count / 2]))
              .Append(",\"p95\":").Append(F(v[Mathf.Min(v.Count - 1, (int)(v.Count * 0.95f))]))
              .Append(",\"p99\":").Append(F(v[Mathf.Min(v.Count - 1, (int)(v.Count * 0.99f))]))
              .Append(",\"max\":").Append(F(v[v.Count - 1])).Append('}');
        }

        static string F(double x) => x.ToString("0.####", CultureInfo.InvariantCulture);
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "'");
    }
}
