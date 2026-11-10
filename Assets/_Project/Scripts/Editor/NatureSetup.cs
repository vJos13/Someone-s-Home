using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Puebla el exterior del Relato 1 con la vegetación y rocas de Tools/Blender/build_nature.py.
    ///  - Bosque al otro lado del río (dejando libre el fondo de la criatura), pinos en las lomas, álamos en las orillas.
    ///  - Artemisa, rocas, troncos, tocones, cerca y tendido eléctrico.
    ///  - Pasto y juncos como detalles del terreno (instanciados → baratos en WebGL).
    ///  - Las piedras del sendero pasan a ser rocas reales (los cubos quedan como colliders invisibles).
    /// Respeta las zonas de juego: casa, camino casa → sendero, sendero de rocas, columpio y la línea de visión a la criatura.
    /// Determinista (semilla fija). Lo ejecuta el asistente: SomeonesHome.EditorTools.NatureSetup.ApplyAll()
    /// </summary>
    public static class NatureSetup
    {
        const string ModelDir = "Assets/_Project/Art/Models/Nature";
        const string MatDir = "Assets/_Project/Art/Materials/Nature";
        const string RootName = "Naturaleza";

        // Mismos valores que Relato1Builder.
        const float W = 200f, SlopeTop = 108f, SlopeBottom = 120f, RiverNear = 133f, RiverFar = 147f, Bank = 6f;
        static readonly Vector3 HouseC = new(90f, 20.35f, 58f);
        static readonly Vector3 Swing = new(92f, 6f, 127f);
        static readonly Vector2 PathA = new(68f, 107.5f), PathB = new(100f, 114f), PathC = new(80f, 120.8f);
        static readonly Vector2 DoorOut = new(92f, 64f);

        struct Spec
        {
            public string set, tex; public Color color; public float smooth, metal; public bool twoSided;
        }

        static readonly Dictionary<string, Spec> Specs = new()
        {
            { "M_Nat_CortezaPino",  new Spec { set = "Corteza",      color = new Color(1f, 0.7f, 0.52f), smooth = 0.2f } },
            { "M_Nat_Corteza",      new Spec { set = "Corteza",      color = new Color(0.8f, 0.78f, 0.74f), smooth = 0.2f } },
            { "M_Nat_FollajePino",  new Spec { set = "Follaje",      color = new Color(0.55f, 0.7f, 0.62f) } },
            { "M_Nat_FollajeAlamo", new Spec { set = "Follaje",      color = new Color(1f, 0.98f, 0.78f) } },
            { "M_Nat_Artemisa",     new Spec { set = "Follaje",      color = new Color(0.95f, 1f, 0.95f) } },
            { "M_Nat_Roca",         new Spec { set = "Roca",         color = Color.white } },
            { "M_Nat_MaderaCerca",  new Spec { set = "MaderaVieja",  color = new Color(0.85f, 0.85f, 0.82f) } },
            { "M_Nat_Poste",        new Spec { set = "MaderaVieja",  color = new Color(0.6f, 0.5f, 0.42f) } },
            { "M_Nat_Pasto",        new Spec { tex = "Assets/_Project/Art/Textures/Nature/T_PastoHoja.png", color = Color.white, smooth = 0.1f, twoSided = true } },
            { "M_Nat_Juncos",       new Spec { tex = "Assets/_Project/Art/Textures/Nature/T_PastoHoja.png", color = new Color(0.62f, 0.85f, 0.55f), smooth = 0.15f, twoSided = true } },
            { "M_Nat_Metal",        new Spec { color = new Color(0.25f, 0.25f, 0.25f), smooth = 0.5f, metal = 0.7f } },
            { "M_Nat_Vidrio",       new Spec { color = new Color(0.4f, 0.55f, 0.5f), smooth = 0.9f } },
        };

        public static void ApplyAll()
        {
            ConfigureModels();
            Populate();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ════════════════════════════════════════════════════════════════
        //  ASSETS
        // ════════════════════════════════════════════════════════════════
        public static void ConfigureModels()
        {
            PBRMaterialSetup.ConfigureImporters();
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Nature");
            string grassTex = "Assets/_Project/Art/Textures/Nature/T_PastoHoja.png";
            if (AssetImporter.GetAtPath(grassTex) is TextureImporter ti && (ti.wrapMode != TextureWrapMode.Clamp || !ti.sRGBTexture))
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }

            var remap = new Dictionary<string, Material>();
            foreach (var (name, s) in Specs)
            {
                string path = $"{MatDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                if (s.set != null) PBRMaterialSetup.ApplyToMaterial(m, s.set, Vector2.one, s.color);
                else
                {
                    m.SetColor("_BaseColor", s.color);
                    if (s.tex != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(s.tex));
                }
                if (s.set == null) { m.SetFloat("_Smoothness", s.smooth); m.SetFloat("_Metallic", s.metal); }
                if (s.twoSided) m.SetFloat("_Cull", (float)CullMode.Off);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                remap[name] = m;
            }
            AssetDatabase.SaveAssets();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter imp) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                imp.globalScale = 1f;
                imp.bakeAxisConversion = false;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importCameras = false;
                imp.importLights = false;
                // UV2 solo para lo que entra en el lightmap (rocas, madera). Árboles y pasto usan light probes.
                imp.generateSecondaryUV = file.StartsWith("Roca") || file.StartsWith("Cerca") || file.StartsWith("Poste") ||
                                          file.StartsWith("Tronco") || file.StartsWith("Tocon");
                imp.meshCompression = ModelImporterMeshCompression.Medium;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var (name, mat) in remap)
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                imp.SaveAndReimport();
            }
            Debug.Log($"[Naturaleza] Modelos configurados ({remap.Count} materiales).");
        }

        static GameObject Model(string file) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{file}.fbx");

        // ════════════════════════════════════════════════════════════════
        //  ZONAS
        // ════════════════════════════════════════════════════════════════
        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>true si en (x,z) no debe ir nada con volumen (r = radio del objeto).</summary>
        static bool Blocked(float x, float z, float r, bool tall)
        {
            var p = new Vector2(x, z);
            if (x > HouseC.x - 9f - r && x < HouseC.x + 9f + r && z > HouseC.z - 8f - r && z < HouseC.z + 10f + r) return true;
            if (SegDist(p, DoorOut, PathA) < 6f + r) return true;                        // casa → inicio del sendero
            if (SegDist(p, PathA, PathB) < 4f + r || SegDist(p, PathB, PathC) < 4f + r) return true;
            if (Vector2.Distance(p, new Vector2(Swing.x, Swing.z)) < 9f + r) return true;
            if (SegDist(p, new Vector2(Swing.x, Swing.z), PathC) < 4f + r) return true;  // carrera de Sam
            if (z > RiverNear - 1.5f && z < RiverFar + 1.5f) return true;                 // agua
            if (tall && x > 80f && x < 116f && z > SlopeBottom && z < 185f) return true;   // fondo de la criatura
            if (x > 84f - r && x < 110f + r && z > RiverFar && z < 172f) return true;       // nada delante de la criatura
            if (x > 97.5f - r && x < 105.5f + r && z > 50f - r && z < 60f + r) return true;   // camioneta de Tammy (llegada)
            if (x > 94.5f - r && x < 101f + r && z > 41f - r && z < 50f + r) return true;     // camioneta de papá (mañana)
            if (Vector2.Distance(p, new Vector2(78.5f, 66f)) < 4f + r) return true;          // cobertizo
            if (SegDist(p, new Vector2(80f, 66f), new Vector2(82f, 85f)) < 2.5f + r) return true; // huellas al campo
            return false;
        }

        // ════════════════════════════════════════════════════════════════
        //  POBLAR
        // ════════════════════════════════════════════════════════════════
        public static void Populate()
        {
            var terrain = Terrain.activeTerrain;
            if (!terrain) { Debug.LogError("[Naturaleza] No hay terreno activo."); return; }
            var env = terrain.transform.parent;

            var oldTrees = GameObject.Find("Trees");
            if (oldTrees) Object.DestroyImmediate(oldTrees);
            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            root.SetParent(env, false);

            var rng = new System.Random(42);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root, false); return g; }

            GameObject Place(string model, Transform parent, float x, float z, float scale, float sink = 0.15f, float? yaw = null)
            {
                var fbx = Model(model);
                if (!fbx) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var pos = new Vector3(x, 0, z);
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y - sink * scale;
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw ?? R(0, 360), 0));
                go.transform.localScale = Vector3.one * scale;
                return go;
            }

            void Static(GameObject go, bool gi)
            {
                var f = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | (gi ? StaticEditorFlags.ContributeGI : 0);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, f);
            }

            // ── Árboles ──
            var trees = Group("Arboles");
            int treeCount = 0;
            void Tree(string model, float x, float z, float scale)
            {
                var go = Place(model, trees, x, z, scale, 0.3f);
                if (!go) return;
                if (go.TryGetComponent<LODGroup>(out var lod))
                {
                    var lods = lod.GetLODs();
                    float[] t = { 0.22f, 0.07f, 0.012f };
                    for (int i = 0; i < lods.Length; i++) lods[i].screenRelativeTransitionHeight = t[Mathf.Min(i, t.Length - 1)];
                    lod.SetLODs(lods);
                    lod.fadeMode = LODFadeMode.CrossFade;
                    lod.animateCrossFading = true;
                }
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = 0.4f; col.height = 6f; col.center = new Vector3(0, 3f, 0);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
                Static(go, false);
                treeCount++;
            }
            string Pine() => rng.NextDouble() switch { < 0.4 => "Pino_A", < 0.75 => "Pino_B", _ => "Pino_C" };
            string Alamo() => rng.NextDouble() < 0.5 ? "Alamo_A" : "Alamo_B";

            bool TryTree(string model, float x0, float x1, float z0, float z1, float s0, float s1, int tries = 30)
            {
                for (int i = 0; i < tries; i++)
                {
                    float x = R(x0, x1), z = R(z0, z1);
                    if (Blocked(x, z, 3f, true)) continue;
                    Tree(model, x, z, R(s0, s1));
                    return true;
                }
                return false;
            }

            // Árbol grande y solitario donde empieza el sendero (punto de referencia del relato).
            Tree("Alamo_A", 61f, 104.5f, 1.25f);
            // Bosque al otro lado del río (el fondo de la criatura queda despejado: se recorta contra el cielo).
            for (int i = 0; i < 70; i++)
                TryTree(rng.NextDouble() < 0.7 ? Pine() : Alamo(), 0f, W, 158f, 199f, 0.8f, 1.25f);
            for (int i = 0; i < 12; i++) TryTree(Alamo(), 5f, 195f, RiverFar + 3f, 158f, 0.85f, 1.15f);
            // Orilla de este lado, lejos del columpio.
            for (int i = 0; i < 10; i++) TryTree(Alamo(), 15f, 185f, SlopeBottom + 1f, RiverNear - 2.5f, 0.8f, 1.1f);
            // Lomas detrás de la casa y bordes del campo.
            for (int i = 0; i < 40; i++) TryTree(Pine(), 0f, W, 2f, 38f, 0.8f, 1.3f);
            for (int i = 0; i < 16; i++) TryTree(Pine(), 2f, 34f, 38f, SlopeTop, 0.8f, 1.2f);
            for (int i = 0; i < 16; i++) TryTree(Pine(), 166f, 198f, 38f, SlopeTop, 0.8f, 1.2f);
            // Árboles secos: silueta inquietante.
            Tree("ArbolSeco_A", 72f, 153f, 1.1f);
            Tree("ArbolSeco_A", 124f, 151f, 0.9f);
            for (int i = 0; i < 4; i++) TryTree("ArbolSeco_A", 20f, 180f, 40f, 104f, 0.8f, 1.1f);

            // ── Artemisa ──
            var bushes = Group("Arbustos");
            int bushCount = 0;
            void Bushes(int n, float x0, float x1, float z0, float z1)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = R(x0, x1), z = R(z0, z1);
                    if (Blocked(x, z, 0.8f, false)) continue;
                    if (x > 86f && x < 108f && z > SlopeBottom && z < 175f) continue;   // ni arbustos delante de la criatura
                    var go = Place(rng.NextDouble() < 0.5 ? "Artemisa_A" : "Artemisa_B", bushes, x, z, R(0.7f, 1.5f), 0.1f);
                    if (!go) continue;
                    if (go.TryGetComponent<LODGroup>(out var lod))
                    {
                        var l = lod.GetLODs();
                        if (l.Length >= 2) { l[0].screenRelativeTransitionHeight = 0.08f; l[1].screenRelativeTransitionHeight = 0.015f; lod.SetLODs(l); }
                    }
                    Static(go, false);
                    bushCount++;
                }
            }
            Bushes(110, 20f, 180f, 38f, SlopeTop - 1f);
            Bushes(30, 20f, 180f, SlopeTop, SlopeBottom);
            Bushes(20, 15f, 185f, SlopeBottom, RiverNear - 1f);
            Bushes(70, 0f, W, RiverFar + 2f, 198f);

            // ── Rocas ──
            var rocks = Group("Rocas");
            int rockCount = 0;
            void Rocks(int n, string[] models, float x0, float x1, float z0, float z1, float s0, float s1, bool collider)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = R(x0, x1), z = R(z0, z1), s = R(s0, s1);
                    if (Blocked(x, z, 1.5f * s, false)) continue;
                    var go = Place(models[rng.Next(models.Length)], rocks, x, z, s, 0.25f);
                    if (!go) continue;
                    if (collider)
                        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                            mf.gameObject.AddComponent<MeshCollider>().convex = true;
                    Static(go, true);
                    rockCount++;
                }
            }
            Rocks(18, new[] { "Roca_A", "Roca_C", "Roca_D" }, 20f, 180f, 38f, SlopeTop - 2f, 0.6f, 1.2f, true);
            Rocks(35, new[] { "Roca_A", "Roca_B", "Roca_E", "Roca_D" }, 15f, 185f, SlopeTop, SlopeBottom, 0.7f, 1.4f, true);
            Rocks(20, new[] { "Roca_A", "Roca_C", "Roca_D" }, 15f, 185f, SlopeBottom, RiverNear - 0.5f, 0.5f, 1.1f, true);
            Rocks(25, new[] { "Roca_A", "Roca_B", "Roca_C", "Roca_E" }, 0f, W, RiverFar + 0.5f, 198f, 0.6f, 1.5f, false);
            Rocks(20, new[] { "Roca_B", "Roca_E" }, 0f, W, 0f, 36f, 1f, 2f, false);

            // ── Troncos caídos y tocones ──
            var deadwood = Group("Troncos");
            for (int i = 0; i < 14; i++)
            {
                float x = R(15f, 185f), z = rng.NextDouble() < 0.5 ? R(40f, 104f) : R(RiverFar + 3f, 190f);
                if (Blocked(x, z, 2.5f, false)) continue;
                var go = Place(i % 2 == 0 ? "Tronco_Caido" : "Tocon", deadwood, x, z, R(0.8f, 1.2f), 0.05f);
                if (!go) continue;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().convex = true;
                Static(go, true);
            }

            // ── Cerca de troncos: frente de la casa (con portón) y borde oeste del campo ──
            var fence = Group("Cerca");
            void FenceLine(Vector2 a, Vector2 b, float gapFrom = -1, float gapTo = -1)
            {
                Vector2 d = (b - a).normalized;
                float len = Vector2.Distance(a, b);
                for (float s = 0; s + 2.5f <= len + 0.01f; s += 2.5f)
                {
                    Vector2 m = a + d * (s + 1.25f);
                    float along = Vector2.Dot(m, d);
                    if (gapFrom >= 0 && along > gapFrom && along < gapTo) continue;
                    var go = Place("Cerca_Tramo", fence, m.x, m.y, 1f, 0f,
                        Quaternion.LookRotation(new Vector3(d.x, 0, d.y)).eulerAngles.y + 90f);
                    if (!go) continue;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(2.6f, 1.3f, 0.25f); bc.center = new Vector3(0, 0.65f, 0);
                    Static(go, true);
                }
            }
            FenceLine(new Vector2(32f, 44f), new Vector2(150f, 44f), 86f, 94f);
            FenceLine(new Vector2(32f, 44f), new Vector2(32f, 104f));

            // ── Tendido eléctrico (detrás de la casa) con cables que cuelgan ──
            var power = Group("TendidoElectrico");
            var wireMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Nat_Cable.mat");
            if (!wireMat)
            {
                wireMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                wireMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
                AssetDatabase.CreateAsset(wireMat, $"{MatDir}/M_Nat_Cable.mat");
            }
            var poles = new List<GameObject>();
            for (float x = 6f; x <= 196f; x += 31.5f)
            {
                var go = Place("Poste_Luz", power, x, 38.5f, 1f, 0f, 90f + R(-3f, 3f));
                if (!go) continue;
                go.AddComponent<CapsuleCollider>().radius = 0.2f;
                Static(go, true);
                poles.Add(go);
            }
            void Wire(Vector3 a, Vector3 b, float sag)
            {
                var g = new GameObject("Cable");
                g.transform.SetParent(power, false);
                var lr = g.AddComponent<LineRenderer>();
                lr.sharedMaterial = wireMat;
                lr.widthMultiplier = 0.025f;
                lr.positionCount = 12;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11f;
                    lr.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t));
                }
            }
            for (int i = 0; i + 1 < poles.Count; i++)
                foreach (float off in new[] { -0.95f, 0f, 0.95f })
                {
                    Vector3 Ins(GameObject p) => p.transform.TransformPoint(new Vector3(-off, 8.72f, 0f));
                    Wire(Ins(poles[i]), Ins(poles[i + 1]), 0.9f);
                }
            // Acometida a la casa desde el poste más cercano.
            if (poles.Count > 0)
            {
                GameObject near = poles[0];
                foreach (var p in poles) if (Mathf.Abs(p.transform.position.x - HouseC.x) < Mathf.Abs(near.transform.position.x - HouseC.x)) near = p;
                Wire(near.transform.TransformPoint(new Vector3(0, 7.9f, 0)), HouseC + new Vector3(5.8f, 2.9f, -4.7f), 0.5f);
            }

            // ── Piedras del sendero ──
            int steps = 0;
            var path = GameObject.Find("RockPath (sendero de rocas)");
            if (path)
            {
                var old2 = path.transform.Find("Piedras (modelo)");
                if (old2) Object.DestroyImmediate(old2.gameObject);
                var stones = new GameObject("Piedras (modelo)").transform;
                stones.SetParent(path.transform, false);
                var fbx = Model("Roca_Plana");
                foreach (Transform cube in path.transform)
                {
                    if (!cube.name.StartsWith("Roca_") || !fbx) continue;
                    var mr = cube.GetComponent<MeshRenderer>();
                    if (mr) mr.enabled = false;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, stones);
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Vector3 top = cube.position + Vector3.up * cube.lossyScale.y * 0.5f;
                    go.transform.SetPositionAndRotation(top, cube.rotation * Quaternion.Euler(0, rng.Next(2) * 180f, 0));
                    go.transform.localScale = new Vector3(cube.lossyScale.x / 2.3f, 1f, cube.lossyScale.z / 1.6f);
                    Static(go, true);
                    steps++;
                }
            }

            PaintDetails(terrain);
            Debug.Log($"[Naturaleza] {treeCount} árboles, {bushCount} arbustos, {rockCount} rocas, {steps} piedras de sendero, {poles.Count} postes.");
        }

        // ════════════════════════════════════════════════════════════════
        //  PASTO Y JUNCOS (detalles del terreno)
        // ════════════════════════════════════════════════════════════════
        static void PaintDetails(Terrain terrain)
        {
            var data = terrain.terrainData;
            var protos = new List<DetailPrototype>();
            void Proto(string model, float minW, float maxW, float minH, float maxH)
            {
                var fbx = Model(model);
                if (!fbx) return;
                protos.Add(new DetailPrototype
                {
                    prototype = fbx, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = minW, maxWidth = maxW, minHeight = minH, maxHeight = maxH, noiseSpread = 0.4f, alignToGround = 0.6f,
                    healthyColor = Color.white, dryColor = Color.white,
                });
            }
            Proto("Pasto_A", 0.8f, 1.4f, 0.7f, 1.3f);
            Proto("Pasto_B", 0.8f, 1.3f, 0.8f, 1.3f);
            Proto("Juncos", 0.8f, 1.2f, 0.8f, 1.2f);
            if (protos.Count < 3) { Debug.LogWarning("[Naturaleza] Faltan modelos de pasto."); return; }
            data.detailPrototypes = protos.ToArray();
            const int res = 256;
            data.SetDetailResolution(res, 32);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            var a = new int[res, res];
            var b = new int[res, res];
            var c = new int[res, res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float nx = (ix + 0.5f) / res, nz = (iz + 0.5f) / res;
                float x = nx * W, z = nz * W;
                float h = data.GetInterpolatedHeight(nx, nz);
                float steep = data.GetSteepness(nx, nz);
                var p = new Vector2(x, z);
                bool house = x > HouseC.x - 7.5f && x < HouseC.x + 7.5f && z > HouseC.z - 6f && z < HouseC.z + 8.5f;
                bool trail = SegDist(p, PathA, PathB) < 1.8f || SegDist(p, PathB, PathC) < 1.8f;
                bool shed = Vector2.Distance(p, new Vector2(78.5f, 66f)) < 2.2f;
                bool trucks = (x > 99.5f && x < 103.5f && z > 51.5f && z < 57.5f) || (x > 96.3f && x < 98.7f && z > 42.8f && z < 48.2f);
                if (house || trail || shed || trucks) continue;
                float patch = Mathf.PerlinNoise(x * 0.08f + 3f, z * 0.08f + 7f);
                float fine = Mathf.PerlinNoise(x * 0.5f, z * 0.5f);
                bool water = h < Bank - 0.25f;
                bool shore = (z > RiverNear - 2.2f && z < RiverNear + 0.2f) || (z > RiverFar - 0.2f && z < RiverFar + 2.5f);
                if (shore && !(x > 84f && x < 110f && z < RiverNear + 1f) && fine > 0.35f) c[iz, ix] = 1;
                if (water) continue;
                float density = steep > 32f ? 0.25f : 1f;
                if (patch * density > 0.38f && fine > 0.25f) a[iz, ix] = patch > 0.6f ? 2 : 1;
                if (patch > 0.62f && fine > 0.55f && steep < 30f) b[iz, ix] = 1;
            }
            data.SetDetailLayer(0, 0, 0, a);
            data.SetDetailLayer(0, 0, 1, b);
            data.SetDetailLayer(0, 0, 2, c);
            terrain.detailObjectDistance = 55f;
            terrain.detailObjectDensity = 1f;
            EditorUtility.SetDirty(data);
        }
    }
}
