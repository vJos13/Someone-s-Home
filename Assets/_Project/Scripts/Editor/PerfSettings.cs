using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Optimizaciones de rendimiento de la Semana 14, en un solo lugar y repetibles.
    /// Cada regla está medida en EVIDENCIAS_OPTIMIZACION (ver OPTIMIZATION_LOG.md).
    /// NatureSetup.Populate() vuelve a llamar a ApplyShadowRules() para que regenerar la naturaleza no las deshaga.
    /// </summary>
    public static class PerfSettings
    {
        const string RegistryName = "PerfRegistry (optimizaciones medibles)";

        /// <summary>
        /// OPT-02 · Sombras: los arbustos en su LOD lejano y los objetos de menos de 1 m dejan de proyectar sombra.
        /// Sus sombras casi no se ven y cada una se dibuja otra vez en el mapa de sombras.
        /// </summary>
        public static int ApplyShadowRules()
        {
            var lodLevel = new Dictionary<Renderer, (int level, LODGroup group)>();
            foreach (var g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include))
            {
                var lods = g.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers)
                        if (r) lodLevel[r] = (i, g);
            }

            var changed = new List<Renderer>();
            var registry = GetRegistry();
            if (registry.shadowsOff != null) changed.AddRange(registry.shadowsOff);   // lo que ya estaba registrado
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                bool bushFar = lodLevel.TryGetValue(r, out var l) && l.level >= 1 && InGroup(r.transform, "Arbustos");
                bool tiny = WorldSize(r) < 1f;
                if (!bushFar && !tiny) continue;
                Undo.RecordObject(r, "Sombras: optimización");
                r.shadowCastingMode = ShadowCastingMode.Off;
                changed.Add(r);
            }
            changed.RemoveAll(r => !r);
            registry.shadowsOff = changed.Distinct().ToArray();
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(registry.gameObject.scene);
            return registry.shadowsOff.Length;
        }

        /// <summary>
        /// OPT-03 · Occlusion Culling: la cámara ya lo tenía activado, pero nunca se había horneado (no había datos),
        /// así que no ocultaba nada. Los objetos transparentes (vidrios) no pueden ser oclusores: Umbra los trata como
        /// opacos y lo que está detrás desaparecería. Hornea con la casa y el terreno como oclusores.
        /// </summary>
        public static string BakeOcclusion()
        {
            int fixedGlass = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.OccluderStatic) == 0 || !AllTransparent(r)) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.OccluderStatic);
                fixedGlass++;
            }
            StaticOcclusionCulling.smallestOccluder = 3f;     // las paredes de la casa miden ~3 m de alto
            StaticOcclusionCulling.smallestHole = 0.25f;      // ventanas y puertas abiertas dejan ver
            StaticOcclusionCulling.backfaceThreshold = 100f;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok = StaticOcclusionCulling.Compute();
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) cam.useOcclusionCulling = true;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return $"vidrios corregidos={fixedGlass} ok={ok} datos={StaticOcclusionCulling.umbraDataSize / 1024} KB en {sw.Elapsed.TotalSeconds:0.0} s";
        }

        // Valores medidos (ver OPTIMIZATION_LOG.md). Las herramientas que construyen la escena usan estas constantes.
        public const float LodBias = 1.25f;          // antes 2
        public const float CameraFar = 250f;         // antes 400: con la niebla (exp² 0.011) a 250 m queda 0,05 % visible
        public const float GrassDistance = 45f;      // antes 55

        /// <summary>OPT-04 · Distancias: menos polígonos lejos, donde la niebla ya los tapa.</summary>
        public static string ApplyDistances()
        {
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.lodBias > LodBias) QualitySettings.lodBias = LodBias;
            }
            QualitySettings.SetQualityLevel(current, false);
            // Cinemachine copia el lente de la cámara virtual a la cámara real cada frame: hay que cambiarlo ahí.
            foreach (var vcam in Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(vcam, "Far clip");
                var lens = vcam.Lens; lens.FarClipPlane = CameraFar; vcam.Lens = lens;
            }
            var cam = Camera.main;
            if (cam) { Undo.RecordObject(cam, "Far clip"); cam.farClipPlane = CameraFar; }
            var t = Terrain.activeTerrain;
            if (t) { Undo.RecordObject(t, "Grass distance"); t.detailObjectDistance = GrassDistance; }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return $"lodBias={QualitySettings.lodBias} far={(cam ? cam.farClipPlane : 0)} pasto={(t ? t.detailObjectDistance : 0)}";
        }

        /// <summary>OPT-05 · Render: terreno instanciado (menos trabajo de CPU por parcela) y sin Opaque Texture (nada la usa).</summary>
        public static string ApplyRenderSetup()
        {
            var t = Terrain.activeTerrain;
            if (t) { Undo.RecordObject(t, "Terrain instanced"); t.drawInstanced = true; }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!rp) continue;
                rp.supportsCameraOpaqueTexture = false;
                EditorUtility.SetDirty(rp);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return $"terreno instanciado={(t && t.drawInstanced)} opaqueTexture=off";
        }

        static bool AllTransparent(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m && m.renderQueue < 2450 && !(m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f)) return false;
            return r.sharedMaterials.Length > 0;
        }

        /// <summary>Tamaño real (diagonal en metros) a partir de la malla: Renderer.bounds es cero en objetos inactivos.</summary>
        static float WorldSize(Renderer r)
        {
            if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return float.MaxValue;
            return Vector3.Scale(mf.sharedMesh.bounds.size, r.transform.lossyScale).magnitude;
        }

        static bool InGroup(Transform t, string group)
        {
            for (var p = t; p; p = p.parent) if (p.name == group) return true;
            return false;
        }

        static PerfRegistry GetRegistry()
        {
            var reg = Object.FindAnyObjectByType<PerfRegistry>(FindObjectsInactive.Include);
            if (reg) return reg;
            var go = new GameObject(RegistryName);
            var env = GameObject.Find("--- ENVIRONMENT ---");
            if (env) go.transform.SetParent(env.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "PerfRegistry");
            return go.AddComponent<PerfRegistry>();
        }
    }
}
