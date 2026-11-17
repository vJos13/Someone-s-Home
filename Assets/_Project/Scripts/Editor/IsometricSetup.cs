using System.Linq;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Convierte la escena abierta a la vista isométrica estilo ARPG reutilizando lo que ya hay:
    ///  - NavMesh: agente con las medidas del CharacterController del jugador, NavMeshSurface por colliders
    ///    físicos (ignora triggers, al jugador y las puertas, para que el vano quede transitable) y horneado.
    ///  - VCam_Iso (CinemachineCamera + IsometricCamera) en el mismo Brain que VCam_FPS y las cinemáticas.
    ///  - ClickToMove en el jugador y el cuerpo visible de Alex (PlayerAvatar con las poses de Sam recoloreadas).
    ///  - CutawayZone en la casa: con el jugador adentro se ocultan techo, fachada y cielo raso; si la casa lo
    ///    tapa desde afuera, también paredes, vidrios y puertas (queda el piso con los muebles).
    /// Es idempotente: se puede volver a ejecutar (reemplaza lo que creó antes).
    /// Menú: Someone's Home ▸ Vista isométrica ▸ Aplicar a la escena abierta. Relato1Builder también la llama.
    /// </summary>
    public static class IsometricSetup
    {
        const string MatDir = "Assets/_Project/Art/Materials";
        const string NavMeshPath = "Assets/_Project/Scenes/Hito3_Main_NavMesh.asset";

        [MenuItem("Someone's Home/Vista isométrica/Aplicar a la escena abierta")]
        public static void ApplyMenu() => Apply(true);

        [MenuItem("Someone's Home/Vista isométrica/Solo hornear NavMesh")]
        public static void BakeMenu()
        {
            BakeNavMesh();
            SaveScene();
        }

        public static void Apply(bool save)
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (!player) { Debug.LogError("[Isométrica] No hay FirstPersonController en la escena."); return; }

            var iso = SetupCamera(player);
            SetupPlayer(player, iso);
            SetupAvatar(player);
            SetupCutaway();
            BakeNavMesh();
            if (save) SaveScene();
            Debug.Log("[Isométrica] Escena configurada: VCam_Iso, ClickToMove, avatar, cutaway de la casa y NavMesh.");
        }

        static void SaveScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Isométrica] {target.GetType().Name} no tiene el campo {field}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ════════════════════════ CÁMARA ════════════════════════
        static IsometricCamera SetupCamera(FirstPersonController player)
        {
            var iso = Object.FindAnyObjectByType<IsometricCamera>(FindObjectsInactive.Include);
            if (!iso)
            {
                var go = new GameObject("VCam_Iso");
                var brain = Object.FindAnyObjectByType<CinemachineBrain>();
                var parent = GameObject.Find("--- CAMERAS ---");
                if (parent) go.transform.SetParent(parent.transform, false);
                else if (brain && brain.transform.parent) go.transform.SetParent(brain.transform.parent, false);
                var vcam = go.AddComponent<CinemachineCamera>();
                var lens = LensSettings.Default;
                lens.FieldOfView = 35f; lens.NearClipPlane = 0.3f; lens.FarClipPlane = 400f;
                vcam.Lens = lens;
                vcam.Priority = 10;
                iso = go.AddComponent<IsometricCamera>();
            }
            Set(iso, "player", player);
            return iso;
        }

        // ════════════════════════ JUGADOR ════════════════════════
        static void SetupPlayer(FirstPersonController player, IsometricCamera iso)
        {
            Set(player, "isoCamera", iso);
            var so = new SerializedObject(player);
            so.FindProperty("viewMode").enumValueIndex = (int)FirstPersonController.ViewMode.Isometric;
            so.ApplyModifiedPropertiesWithoutUndo();

            var ctm = player.GetComponent<ClickToMove>();
            if (!ctm) ctm = player.gameObject.AddComponent<ClickToMove>();
            Set(ctm, "markerMaterial", MarkerMaterial());
        }

        static Material MarkerMaterial()
        {
            string path = $"{MatDir}/M_ClickMarker.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            MakeTransparent(m);
            m.SetColor("_BaseColor", new Color(0.95f, 0.85f, 0.6f, 0.8f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material InvisibleMaterial()
        {
            string path = $"{MatDir}/M_Cutaway_Invisible.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            // Unlit transparente con alfa 0 y sin pasada de sombra: no dibuja nada.
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            MakeTransparent(m);
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ════════════════════════ AVATAR ════════════════════════
        static void SetupAvatar(FirstPersonController player)
        {
            var old = player.transform.Find("Avatar (Alex)");
            if (old) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject("Avatar (Alex)").transform;
            root.SetParent(player.transform, false);
            var avatar = root.gameObject.AddComponent<PlayerAvatar>();
            Set(avatar, "player", player);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterSetup.ModelDir}/Sam.fbx");
            if (!fbx) { Debug.LogWarning("[Isométrica] No está Sam.fbx: el avatar queda sin poses."); return; }
            var idle = Pose(fbx, root, "Sam_Standing");
            var walk = Pose(fbx, root, "Sam_Running");
            if (!idle || !walk) return;

            // Alex es el hermano mayor: se escala a ~1.72 m (el CharacterController mide 1.75).
            var b = Bounds(idle);
            float scale = b.size.y > 0.1f ? 1.72f / b.size.y : 1f;
            foreach (var p in new[] { idle, walk })
            {
                p.transform.localScale = Vector3.one * scale;
                var pb = Bounds(p);
                p.transform.position += Vector3.up * (player.transform.position.y - pb.min.y);
                foreach (var r in p.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            }
            walk.SetActive(false);
            Set(avatar, "idlePose", idle);
            Set(avatar, "walkPose", walk);
        }

        static GameObject Pose(GameObject fbx, Transform parent, string child)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var c = go.transform.Find(child);
            if (!c) { Object.DestroyImmediate(go); Debug.LogWarning($"[Isométrica] Sam.fbx no tiene {child}."); return null; }
            c.SetParent(parent, false);
            Object.DestroyImmediate(go);
            c.localPosition = Vector3.zero;
            c.localRotation = Quaternion.identity;
            c.name = child.Replace("Sam_", "Alex_");
            foreach (var col in c.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            return c.gameObject;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ════════════════════════ CASA ════════════════════════
        static void SetupCutaway()
        {
            var casa = GameObject.Find("Casa");
            var piso = casa ? casa.transform.Find("Piso") : null;
            if (!casa || !piso) { Debug.LogWarning("[Isométrica] No se encontró Casa/Piso: sin cutaway."); return; }
            var zone = casa.GetComponent<CutawayZone>();
            if (!zone) zone = casa.AddComponent<CutawayZone>();

            var floor = piso.GetComponent<Collider>().bounds;
            Vector3 c = casa.transform.InverseTransformPoint(new Vector3(floor.center.x, floor.max.y + 1.5f, floor.center.z));
            Vector3 s = casa.transform.InverseTransformVector(new Vector3(floor.size.x - 0.2f, 3.0f, floor.size.z - 0.2f));
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

            Renderer R(string name)
            {
                var t = casa.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                return t ? t.GetComponent<Renderer>() : null;
            }
            var hide = new[] { R("Casa_Techo"), R("Casa_Exterior"), R("Techo") }.Where(r => r).Cast<Object>().ToArray();
            var full = new[] { R("Casa_Vidrio"), R("Casa_Puerta"), R("Casa_CortinaCerrada"), R("Casa_PuertaInterior") }
                .Where(r => r).Cast<Object>().ToArray();
            var sub = new[] { R("Casa_Estructura") }.Where(r => r).Cast<Object>().ToArray();

            var so = new SerializedObject(zone);
            so.FindProperty("center").vector3Value = c;
            so.FindProperty("size").vector3Value = s;
            SetArray(so.FindProperty("hideRenderers"), hide);
            SetArray(so.FindProperty("fullHideRenderers"), full);
            SetArray(so.FindProperty("submaterialRenderers"), sub);
            so.FindProperty("invisibleMaterial").objectReferenceValue = InvisibleMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(SerializedProperty p, Object[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ════════════════════════ NAVMESH ════════════════════════
        /// <summary>El agente Humanoid (no lo usa nadie más) toma las medidas del CharacterController del jugador.</summary>
        static void ConfigureAgent(CharacterController cc)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").FirstOrDefault();
            if (!asset) return;
            var so = new SerializedObject(asset);
            var a = so.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            a.FindPropertyRelative("agentRadius").floatValue = cc ? cc.radius : 0.3f;
            a.FindPropertyRelative("agentHeight").floatValue = cc ? cc.height : 1.75f;
            a.FindPropertyRelative("agentClimb").floatValue = cc ? Mathf.Min(cc.stepOffset, 0.45f) : 0.45f;
            // Igual que el slopeLimit: la colina empinada no es caminable, la ruta baja por las rocas.
            a.FindPropertyRelative("agentSlope").floatValue = cc ? cc.slopeLimit : 40f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Ignore(GameObject go, bool children = true)
        {
            var m = go.GetComponent<NavMeshModifier>();
            if (!m) m = go.AddComponent<NavMeshModifier>();
            m.ignoreFromBuild = true;
            m.applyToChildren = children;
        }

        public static void BakeNavMesh()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            ConfigureAgent(player ? player.GetComponent<CharacterController>() : null);

            var env = GameObject.Find("--- ENVIRONMENT ---");
            var host = GameObject.Find("NavMesh (isométrica)");
            if (!host)
            {
                host = new GameObject("NavMesh (isométrica)");
                if (env) host.transform.SetParent(env.transform, false);
            }
            var surface = host.GetComponent<NavMeshSurface>();
            if (!surface) surface = host.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;   // radio 0.3 / 3: el vano de la puerta y el sendero de rocas quedan transitables

            // Fuera del horneado: el jugador, los triggers narrativos y las puertas (se abren y cierran).
            if (player) Ignore(player.gameObject);
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
                if (col.isTrigger) Ignore(col.gameObject, false);
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include)) Ignore(door.gameObject);

            surface.BuildNavMesh();
            var data = surface.navMeshData;
            if (!data) { Debug.LogError("[Isométrica] El horneado del NavMesh no produjo datos."); return; }
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath)) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(data, NavMeshPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            surface.RemoveData();
            surface.AddData();
            EditorUtility.SetDirty(surface);

            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[Isométrica] NavMesh horneado: {tri.vertices.Length} vértices, {tri.indices.Length / 3} triángulos → {NavMeshPath}");
        }
    }
}
