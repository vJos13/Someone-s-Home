using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Integra la casa y el columpio modelados en Blender (Tools/Blender/build_environment.py).
    ///  - Los colliders siguen siendo las cajas del greybox (se ocultan sus MeshRenderer, no se borran):
    ///    la jugabilidad (paredes, escalón, puerta, sillas) no cambia.
    ///  - La puerta del modelo se cuelga de la bisagra del greybox (Door) y el asiento del pivote del columpio.
    ///  - Materiales URP en Art/Materials/Environment con las texturas PBR de Blender (UV en metros → tiling 1).
    /// Lo ejecuta el asistente vía Unity Pipeline: SomeonesHome.EditorTools.EnvironmentSetup.ApplyAll()
    /// </summary>
    public static class EnvironmentSetup
    {
        const string ModelDir = "Assets/_Project/Art/Models/Environment";
        const string MatDir = "Assets/_Project/Art/Materials/Environment";
        const string GreyboxMatDir = "Assets/_Project/Art/Materials/Greybox";

        // Mismo giro que CHAIR_YAW en build_environment.py.
        static readonly float[] ChairYaw = { -9f, 6f, 11f, -5f };

        struct Spec
        {
            public string set; public Color color; public float smooth; public float metal;
            public Spec(string set, Color color, float smooth = 0.5f, float metal = 0f)
            { this.set = set; this.color = color; this.smooth = smooth; this.metal = metal; }
        }

        static readonly Dictionary<string, Spec> Specs = new()
        {
            { "M_Casa_Revestimiento", new("Revestimiento", new Color(0.86f, 0.9f, 0.9f)) },
            { "M_Casa_Moldura",       new("Yeso",          new Color(0.95f, 0.94f, 0.9f)) },
            { "M_Casa_Yeso",          new("Yeso",          new Color(1f, 0.96f, 0.88f)) },
            { "M_Casa_Piso",          new("MaderaVieja",   new Color(1f, 0.86f, 0.72f)) },
            { "M_Casa_TechoInt",      new("MaderaVieja",   new Color(1f, 0.97f, 0.9f)) },
            { "M_Casa_Tejas",         new("Tejas",         Color.white) },
            { "M_Casa_Concreto",      new("Concreto",      Color.white) },
            { "M_Casa_Ladrillo",      new("Concreto",      new Color(0.78f, 0.42f, 0.34f)) },
            { "M_Casa_Azulejo",       new("Azulejo",       Color.white) },
            { "M_Casa_Gabinete",      new("MaderaVieja",   new Color(1f, 0.9f, 0.75f)) },
            { "M_Casa_Encimera",      new("Concreto",      new Color(0.95f, 0.92f, 0.84f)) },
            { "M_Casa_Mueble",        new("MaderaVieja",   new Color(0.85f, 0.65f, 0.5f)) },
            { "M_Casa_PuertaPintada", new("MaderaVieja",   new Color(0.55f, 0.66f, 0.62f)) },
            { "M_Casa_Alfombra",      new("Tela",          new Color(0.5f, 0.24f, 0.2f)) },
            { "M_Casa_Tela",          new("Tela",          new Color(0.9f, 0.84f, 0.7f)) },
            { "M_Casa_Lena",          new("Corteza",       Color.white) },
            { "M_Columpio_Metal",     new("MetalOxidado",  Color.white) },
            { "M_Columpio_Madera",    new("MaderaVieja",   Color.white) },
            { "M_Casa_Metal",         new(null, new Color(0.62f, 0.62f, 0.6f), 0.65f, 0.9f) },
            { "M_Casa_Esmalte",       new(null, new Color(0.86f, 0.85f, 0.8f), 0.75f) },
            { "M_Casa_Negro",         new(null, new Color(0.04f, 0.04f, 0.04f), 0.4f) },
            { "M_Casa_Lampara",       new(null, new Color(0.22f, 0.36f, 0.28f), 0.6f) },
            { "M_Casa_Cuadro",        new(null, new Color(0.3f, 0.33f, 0.28f), 0.3f) },
            { "M_Casa_Ceramica",      new(null, new Color(0.93f, 0.91f, 0.86f), 0.85f) },
            { "M_Casa_Vidrio",        new(null, new Color(0.7f, 0.78f, 0.8f, 0.22f), 0.95f) },
        };

        public static void ApplyAll()
        {
            ConfigureModels();
            ReplaceInOpenScene();
        }

        // ════════════════════════════════════════════════════════════════
        //  ASSETS
        // ════════════════════════════════════════════════════════════════
        public static void ConfigureModels()
        {
            PBRMaterialSetup.ConfigureImporters();
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Environment");

            var remap = new Dictionary<string, Material>();
            foreach (var (name, spec) in Specs)
            {
                string path = $"{MatDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                if (spec.set != null) PBRMaterialSetup.ApplyToMaterial(m, spec.set, Vector2.one, spec.color);
                else
                {
                    m.SetColor("_BaseColor", spec.color);
                    m.SetFloat("_Smoothness", spec.smooth);
                    m.SetFloat("_Metallic", spec.metal);
                }
                if (name == "M_Casa_Vidrio") MakeTransparent(m);
                EditorUtility.SetDirty(m);
                remap[name] = m;
            }
            foreach (var name in new[] { "M_Mantel", "M_Foco" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{GreyboxMatDir}/{name}.mat");
                if (m) remap[name] = m;
            }
            AssetDatabase.SaveAssets();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter imp) continue;
                imp.globalScale = 1f;
                imp.bakeAxisConversion = false;           // con true Unity gira 180°: build_environment.py ya modela en ejes Unity
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importCameras = false;
                imp.importLights = false;
                imp.generateSecondaryUV = true;          // UV2 para luces horneadas (rúbrica: baked lights)
                imp.meshCompression = ModelImporterMeshCompression.Off;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var (name, mat) in remap)
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                imp.SaveAndReimport();
            }
            Debug.Log($"[Entorno] Modelos configurados ({remap.Count} materiales).");
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        static GameObject Model(string file) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{file}.fbx");

        static GameObject Place(GameObject fbx, Transform parent, Vector3 worldPos, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go;
        }

        static void HideRenderers(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
        }

        static void SetStatic(GameObject go, bool isStatic)
        {
            var flags = isStatic
                ? StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                  StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic
                : 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        // ════════════════════════════════════════════════════════════════
        //  SPAWN (usado por el builder y por ReplaceInOpenScene)
        // ════════════════════════════════════════════════════════════════
        /// <param name="houseRoot">"Casa" (greybox)</param>
        /// <param name="floorCenter">c del builder: centro del piso a nivel del suelo interior</param>
        /// <param name="hinge">bisagra de la puerta trasera (componente Door)</param>
        /// <param name="dressing">contenedor con Mesa, Sillas, Mesada, Platos, Foco…</param>
        public static bool SpawnHouse(Transform houseRoot, Vector3 floorCenter, Transform hinge, Transform dressing)
        {
            var fbx = Model("Casa");
            if (!fbx) return false;
            var old = houseRoot.Find("Casa (modelo)");
            if (old) Object.DestroyImmediate(old.gameObject);
            var oldDoor = hinge ? hinge.Find("Casa_Puerta") : null;
            if (oldDoor) Object.DestroyImmediate(oldDoor.gameObject);

            HideRenderers(houseRoot);
            if (dressing)
            {
                foreach (Transform t in dressing)
                {
                    string n = t.name;
                    if (n is "Mesa" or "Mantel" or "Mesada" or "Foco" || n.StartsWith("Silla_") || n.StartsWith("Plato_"))
                        HideRenderers(t);
                    if (n.StartsWith("Silla_") && int.TryParse(n.Substring(6), out int i) && i < ChairYaw.Length)
                        t.rotation = Quaternion.Euler(0f, ChairYaw[i], 0f);
                }
            }

            var house = Place(fbx, houseRoot, floorCenter, "Casa (modelo)");
            SetStatic(house, true);
            foreach (Transform part in house.transform)
            {
                if (part.name.EndsWith("_Solid") && !part.GetComponent<Collider>()) part.gameObject.AddComponent<BoxCollider>();
                if (part.name == "Casa_Vidrio" && part.TryGetComponent<MeshRenderer>(out var gr))
                    gr.shadowCastingMode = ShadowCastingMode.Off;
            }
            var door = house.transform.Find("Casa_Puerta");
            if (door && hinge)
            {
                door.SetParent(hinge, true);
                SetStatic(door.gameObject, false);       // se mueve
            }
            var lamp = house.transform.Find("Casa_Lampara");
            if (lamp) SetStatic(lamp.gameObject, true);
            return true;
        }

        public static bool SpawnSwing(Transform swingRoot, Transform seatPivot)
        {
            var fbx = Model("Columpio");
            if (!fbx) return false;
            var old = swingRoot.Find("Columpio (modelo)");
            if (old) Object.DestroyImmediate(old.gameObject);
            var oldSeat = seatPivot ? seatPivot.Find("Columpio_Asiento") : null;
            if (oldSeat) Object.DestroyImmediate(oldSeat.gameObject);

            HideRenderers(swingRoot);
            var swing = Place(fbx, swingRoot, swingRoot.position, "Columpio (modelo)");
            SetStatic(swing, true);
            var seat = swing.transform.Find("Columpio_Asiento");
            if (seat && seatPivot)
            {
                seat.SetParent(seatPivot, true);
                SetStatic(seat.gameObject, false);       // se mece
            }
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  ESCENA ABIERTA
        // ════════════════════════════════════════════════════════════════
        public static void ReplaceInOpenScene()
        {
            int done = 0;
            var houseRoot = GameObject.Find("Casa");
            var floor = houseRoot ? houseRoot.transform.Find("Piso") : null;
            var door = Object.FindAnyObjectByType<Door>(FindObjectsInactive.Include);
            var dressing = GameObject.Find("Narrative_SetDressing");
            if (houseRoot && floor)
            {
                Vector3 c = floor.position + new Vector3(0f, 0.6f, 0f);
                if (SpawnHouse(houseRoot.transform, c, door ? door.transform : null, dressing ? dressing.transform : null)) done++;
            }
            else Debug.LogWarning("[Entorno] No se encontró la casa (Casa/Piso).");

            var swingRoot = GameObject.Find("Columpio (glider 3 asientos)");
            var pivot = swingRoot ? swingRoot.transform.Find("SwingSeat (pivote)") : null;
            if (swingRoot && SpawnSwing(swingRoot.transform, pivot)) done++;
            else Debug.LogWarning("[Entorno] No se encontró el columpio.");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Entorno] {done}/2 modelos colocados en {scene.name}.");
        }
    }
}
