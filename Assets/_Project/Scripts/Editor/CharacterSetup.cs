using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Integra los personajes modelados en Blender (Tools/Blender/build_characters.py → Art/Models/Characters/*.fbx).
    ///  - Configura los importadores FBX y remapea sus materiales a materiales URP del proyecto.
    ///  - Crea los materiales de ropa con las texturas PBR (Tela, Mezclilla) tintadas.
    ///  - ReplaceInOpenScene(): cambia las cápsulas greybox de la escena abierta por los modelos,
    ///    conservando los objetos raíz (y por tanto las referencias de BrotherNPC / RiverCreature / Director).
    /// Relato1Builder también usa los Spawn* para que una reconstrucción mantenga los modelos.
    /// Lo ejecuta el asistente vía Unity Pipeline: SomeonesHome.EditorTools.CharacterSetup.ApplyAll()
    /// </summary>
    public static class CharacterSetup
    {
        public const string ModelDir = "Assets/_Project/Art/Models/Characters";
        const string CharMatDir = "Assets/_Project/Art/Materials/Characters";
        const string GreyboxMatDir = "Assets/_Project/Art/Materials/Greybox";

        // material del FBX → (set PBR o null, tinte/color, smoothness, tiling)
        static readonly Dictionary<string, (string set, Color color, float smooth, float tiling)> CharMaterials = new()
        {
            { "M_Char_Skin",      (null,        new Color(0.80f, 0.60f, 0.48f), 0.35f, 1f) },
            { "M_Char_SkinTammy", (null,        new Color(0.70f, 0.52f, 0.41f), 0.35f, 1f) },
            { "M_Char_HairDark",  ("Tela",      new Color(0.12f, 0.09f, 0.07f), 0.3f, 3f) },
            { "M_Char_HairBrown", ("Tela",      new Color(0.36f, 0.22f, 0.12f), 0.3f, 3f) },
            { "M_Char_Eyes",      (null,        new Color(0.03f, 0.025f, 0.02f), 0.85f, 1f) },
            { "M_Sam_Hoodie",     ("Tela",      new Color(0.24f, 0.33f, 0.52f), 1f, 2f) },
            { "M_Char_Jeans",     ("Mezclilla", Color.white,                    1f, 2f) },
            { "M_Char_Shoes",     ("Tela",      new Color(0.9f, 0.9f, 0.87f),  1f, 4f) },
            { "M_Tammy_Sweater",  ("Tela",      new Color(0.58f, 0.38f, 0.55f), 1f, 2f) },
            { "M_Tammy_Apron",    ("Mantel",    Color.white,                    1f, 0.5f) },
            { "M_Char_Slacks",    ("Tela",      new Color(0.28f, 0.26f, 0.25f), 1f, 2f) },
            { "M_Emily_Shirt",    ("Tela",      new Color(0.42f, 0.66f, 0.5f), 1f, 2f) },
        };

        // Materiales de la criatura: usan los shaders propios (SG_Criatura / SG_OjosCriatura).
        const string CreatureMatDir = "Assets/_MyShaders/Materials";
        static readonly string[] CreatureMaterials = { "M_Creature", "M_CreatureDark", "M_CreatureEyes" };

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
            if (!AssetDatabase.IsValidFolder(CharMatDir))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Characters");

            var remap = new Dictionary<string, Material>();
            foreach (var (name, (set, color, smooth, tiling)) in CharMaterials)
            {
                string path = $"{CharMatDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, path);
                }
                if (set != null) PBRMaterialSetup.ApplyToMaterial(m, set, new Vector2(tiling, tiling), color);
                else
                {
                    m.SetColor("_BaseColor", color);
                    m.SetFloat("_Smoothness", smooth);
                }
                EditorUtility.SetDirty(m);
                remap[name] = m;
            }
            foreach (var name in CreatureMaterials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{CreatureMatDir}/{name}.mat");
                if (m) remap[name] = m;
            }
            AssetDatabase.SaveAssets();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter imp) continue;
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.bakeAxisConversion = true;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importBlendShapes = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.isReadable = false;
                imp.meshCompression = ModelImporterMeshCompression.Medium;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var (name, mat) in remap)
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                imp.SaveAndReimport();
            }
            Debug.Log($"[Personajes] Modelos configurados ({remap.Count} materiales remapeados).");
        }

        static GameObject Model(string file) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{file}.fbx");

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        static GameObject Instance(GameObject prefab, Transform parent, string childName = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (childName != null)
            {
                // Solo un hijo del FBX (p. ej. una pose de Sam): se desempaqueta y se queda ese.
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var child = go.transform.Find(childName);
                child.SetParent(parent, false);
                Object.DestroyImmediate(go);
                go = child.gameObject;
            }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        // ════════════════════════════════════════════════════════════════
        //  SPAWN (usados por el builder y por ReplaceInOpenScene)
        // ════════════════════════════════════════════════════════════════
        /// <summary>Pone el modelo de Tammy/Emily como hijo de root (pies en el origen). false si no hay FBX.</summary>
        public static bool SpawnHuman(Transform root, string name)
        {
            var fbx = Model(name);
            if (!fbx) return false;
            Clear(root);
            var go = Instance(fbx, root);
            go.name = $"{name} (modelo)";
            return true;
        }

        /// <summary>Tres poses bajo body; devuelve (sentado, de pie, corriendo) o null si no hay FBX.</summary>
        public static GameObject[] SpawnSam(Transform body)
        {
            var fbx = Model("Sam");
            if (!fbx) return null;
            Clear(body);
            var poses = new[] { "Sam_Sitting", "Sam_Standing", "Sam_Running" };
            var result = new GameObject[poses.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                result[i] = Instance(fbx, body, poses[i]);
                result[i].SetActive(i == 0);
            }
            return result;
        }

        /// <summary>
        /// Cambia el contenido de visualRoot por el modelo. Devuelve (head, jaw) del modelo:
        /// Head tiene el pivote en el cuello (seguimiento al jugador), Jaw en la bisagra (habla).
        /// </summary>
        public static (Transform head, Transform jaw)? SpawnCreature(Transform visualRoot)
        {
            var fbx = Model("LaCosa");
            if (!fbx) return null;
            Clear(visualRoot);
            var go = Instance(fbx, visualRoot);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "LaCosa (modelo)";
            var head = FindDeep(go.transform, "LaCosa_Head");
            var jaw = FindDeep(go.transform, "LaCosa_Jaw");
            var eyes = FindDeep(go.transform, "LaCosa_Eyes");
            // El FBX viene plano: ojos y mandíbula siguen a la cabeza.
            if (jaw && head) jaw.SetParent(head, true);
            if (eyes && head) eyes.SetParent(head, true);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return (head, jaw);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }

        // ════════════════════════════════════════════════════════════════
        //  ESCENA ABIERTA
        // ════════════════════════════════════════════════════════════════
        /// <summary>Tammy y Emily miran hacia donde aparece el jugador (la escena empieza hablando con ellas).</summary>
        public static void FaceNpcsToPlayer()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (!player) return;
            foreach (var name in new[] { "Tammy", "Emily" })
            {
                var go = GameObject.Find(name);
                if (!go) continue;
                Vector3 d = player.transform.position - go.transform.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(d);
            }
        }

        public static void ReplaceInOpenScene()
        {
            int done = 0;
            foreach (var name in new[] { "Tammy", "Emily" })
            {
                var go = GameObject.Find(name);
                if (go && SpawnHuman(go.transform, name)) done++;
                else Debug.LogWarning($"[Personajes] No se encontró {name} en la escena.");
            }

            var brother = Object.FindAnyObjectByType<BrotherNPC>(FindObjectsInactive.Include);
            if (brother)
            {
                var so = new SerializedObject(brother);
                var body = so.FindProperty("body").objectReferenceValue as Transform;
                var poses = body ? SpawnSam(body) : null;
                if (poses != null)
                {
                    body.localPosition = Vector3.zero;
                    so.FindProperty("sittingPose").objectReferenceValue = poses[0];
                    so.FindProperty("standingPose").objectReferenceValue = poses[1];
                    so.FindProperty("runningPose").objectReferenceValue = poses[2];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    done++;
                }
            }

            var creature = Object.FindAnyObjectByType<RiverCreature>(FindObjectsInactive.Include);
            if (creature)
            {
                var so = new SerializedObject(creature);
                var vis = so.FindProperty("visualRoot").objectReferenceValue as Transform;
                var parts = vis ? SpawnCreature(vis) : null;
                if (parts is { } p)
                {
                    so.FindProperty("head").objectReferenceValue = p.head;
                    so.FindProperty("jaw").objectReferenceValue = p.jaw;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    done++;
                }
            }

            FaceNpcsToPlayer();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Personajes] {done}/4 personajes reemplazados en {scene.name}.");
        }
    }
}
