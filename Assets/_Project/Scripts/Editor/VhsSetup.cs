using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Instala los efectos de pantalla propios en los renderers de URP (PC y Mobile):
    /// · Filtro de cinta VHS (<see cref="VHSRendererFeature"/>, material M_VHS), después del post.
    /// · Rayos de luz del sol (<see cref="SunShaftsRendererFeature"/>, material M_SunShafts), antes del post (el bloom
    ///   los suaviza y el tonemapping los integra).
    /// Se puede correr varias veces: si ya están, solo actualiza materiales y ajustes.
    /// </summary>
    public static class VhsSetup
    {
        const string MaterialPath = "Assets/_Project/Art/Materials/PostFX/M_VHS.mat";
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        const string ShaftsMaterialPath = "Assets/_Project/Art/Materials/PostFX/M_SunShafts.mat";

        [MenuItem("Someone's Home/Instalar filtro VHS y rayos de luz")]
        public static string Install()
        {
            return Install<VHSRendererFeature>("SomeonesHome/VHS", MaterialPath, "Filtro VHS",
                       FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing, ScriptableRenderPassInput.Color)
                 + Install<SunShaftsRendererFeature>("SomeonesHome/SunShafts", ShaftsMaterialPath, "Rayos de luz",
                       FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing,
                       ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        static string Install<T>(string shaderName, string materialPath, string featureName,
                                 FullScreenPassRendererFeature.InjectionPoint injection, ScriptableRenderPassInput requirements)
            where T : FullScreenPassRendererFeature
        {
            var shader = Shader.Find(shaderName);
            if (!shader) return "No se encontró el shader " + shaderName + "\n";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!mat)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(materialPath));
                mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(materialPath) };
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            else mat.shader = shader;

            var log = new System.Text.StringBuilder();
            foreach (var path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (!data) { log.AppendLine("sin renderer: " + path); continue; }
                var feature = data.rendererFeatures.OfType<T>().FirstOrDefault();
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<T>();
                    feature.name = featureName;
                    AssetDatabase.AddObjectToAsset(feature, data);
                    // Igual que el botón "Add Renderer Feature": lista de features + mapa de IDs locales.
                    var so = new SerializedObject(data);
                    var list = so.FindProperty("m_RendererFeatures");
                    var map = so.FindProperty("m_RendererFeatureMap");
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    data.SetDirty();
                    log.AppendLine(featureName + ": agregado a " + path);
                }
                else log.AppendLine(featureName + ": ya estaba en " + path);
                feature.passMaterial = mat;
                feature.injectionPoint = injection;
                feature.fetchColorBuffer = true;
                feature.requirements = requirements;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }
    }
}
