using UnityEditor;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Importa los personajes con esqueleto (Tools/Blender/build_rigged_characters.py → Resources/Characters/*_Rig.fbx):
    /// animación Legacy (la reproduce <see cref="RiggedCharacter"/> con el componente Animation, sin Animator Controller),
    /// sin cámaras ni luces, y los materiales M_Char_* / M_Creature* remapeados a los materiales URP del proyecto.
    /// </summary>
    public class RiggedCharacterImport : AssetPostprocessor
    {
        static readonly string[] MaterialDirs = { "Assets/_Project/Art/Materials/Characters", "Assets/_MyShaders/Materials" };

        static bool IsRig(string path) => path.Replace('\\', '/').Contains("/Resources/Characters/") && path.EndsWith("_Rig.fbx");

        void OnPreprocessModel()
        {
            if (!IsRig(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Low;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsRig(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            bool changed = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    var name = m.name;
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                    if (mi.GetExternalObjectMap().ContainsKey(id)) continue;
                    foreach (var dir in MaterialDirs)
                    {
                        var target = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/{name}.mat");
                        if (!target) continue;
                        mi.AddRemap(id, target);
                        changed = true;
                        break;
                    }
                }
            // Los remapeos se aplican en la siguiente importación.
            if (changed) EditorApplication.delayCall += () => AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Someone's Home/Reimportar personajes animados")]
        public static string ReimportAll()
        {
            var guids = AssetDatabase.FindAssets("_Rig t:Model", new[] { "Assets/_Project/Resources/Characters" });
            foreach (var g in guids) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            return $"reimportados {guids.Length}";
        }
    }
}
