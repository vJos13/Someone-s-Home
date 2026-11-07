using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Aplica las texturas PBR generadas en Blender (Tools/Blender/generate_pbr_textures.py)
    /// a los materiales URP Lit y a las Terrain Layers.
    /// Lo ejecuta el asistente vía Unity Pipeline: SomeonesHome.EditorTools.PBRMaterialSetup.Apply()
    ///
    /// Se puede ejecutar las veces que se quiera (no toca la escena, solo assets).
    /// </summary>
    public static class PBRMaterialSetup
    {
        const string PbrDir = "Assets/_Project/Art/Textures/PBR";
        const string MatDir = "Assets/_Project/Art/Materials/Greybox";
        const string TerrainDir = "Assets/_Project/Art/Terrain";

        // material → (set de texturas, tiling)
        static readonly Dictionary<string, (string set, Vector2 tiling)> Materials = new()
        {
            { "M_PisoMadera",    ("MaderaVieja", new Vector2(4, 3)) },
            { "M_Mueble",        ("MaderaVieja", new Vector2(1, 1)) },
            { "M_MaderaGastada", ("MaderaVieja", new Vector2(2, 0.5f)) },
            { "M_Puerta",        ("MaderaVieja", new Vector2(0.5f, 2)) },
            { "M_Techo",         ("MaderaVieja", new Vector2(4, 3)) },
            { "M_Roca",          ("Roca",        new Vector2(1, 1)) },
            { "M_Tronco",        ("Corteza",     new Vector2(1, 3)) },
            { "M_ParedCasa",     ("Yeso",        new Vector2(3, 1)) },
            { "M_MetalPintado",  ("MetalOxidado", new Vector2(1, 2)) },
            { "M_Mantel",        ("Mantel",      new Vector2(1, 1)) },
            // Base PBR de la criatura: el shader propio del equipo puede reutilizar estos mapas.
            { "M_Creature",      ("PielCriatura", new Vector2(3, 3)) },
            { "M_CreatureDark",  ("PielCriatura", new Vector2(3, 3)) },
        };

        // Tinte opcional (multiplica el BaseMap). Por defecto blanco.
        static readonly Dictionary<string, Color> Tints = new()
        {
            { "M_CreatureDark", new Color(0.45f, 0.4f, 0.38f) },
        };

        // terrain layer → (set, tamaño del tile en metros)
        static readonly Dictionary<string, (string set, float tile)> TerrainLayers = new()
        {
            { "TL_Pasto",      ("PastoSeco", 5f) },
            { "TL_TierraRoca", ("Tierra",    4f) },
            { "TL_Orilla",     ("Barro",     4f) },
        };

        public static void Apply()
        {
            ConfigureImporters();

            int mats = 0, layers = 0;
            foreach (var (matName, (set, tiling)) in Materials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{matName}.mat");
                if (!mat) { Debug.LogWarning($"[PBR] No existe {matName}"); continue; }
                ApplyToMaterial(mat, set, tiling, Tints.TryGetValue(matName, out var tint) ? tint : Color.white);
                mats++;
            }

            foreach (var (layerName, (set, tile)) in TerrainLayers)
            {
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{TerrainDir}/{layerName}.terrainlayer");
                if (!layer) { Debug.LogWarning($"[PBR] No existe {layerName}"); continue; }
                layer.diffuseTexture = Tex(set, "BaseColor");
                layer.normalMapTexture = Tex(set, "Normal");
                layer.maskMapTexture = Tex(set, "TerrainMask");
                layer.normalScale = 1f;
                layer.tileSize = new Vector2(tile, tile);
                layer.smoothness = 1f;     // con mask map, el canal A manda
                layer.metallic = 0f;
                EditorUtility.SetDirty(layer);
                layers++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PBR] Texturas aplicadas: {mats} materiales, {layers} terrain layers.");
        }

        public static void ApplyToMaterial(Material m, string set, Vector2 tiling, Color tint)
        {
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", Tex(set, "BaseColor"));
            m.SetTextureScale("_BaseMap", tiling);

            m.SetTexture("_BumpMap", Tex(set, "Normal"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");

            m.SetTexture("_MetallicGlossMap", Tex(set, "MetallicSmoothness"));
            m.SetFloat("_Smoothness", 1f);                 // multiplica el canal A del mapa
            m.SetFloat("_SmoothnessTextureChannel", 0f);   // A del metallic map
            m.EnableKeyword("_METALLICSPECGLOSSMAP");

            m.SetTexture("_OcclusionMap", Tex(set, "AO"));
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
        }

        public static Texture2D Tex(string set, string map) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{PbrDir}/{set}/T_{set}_{map}.png");

        /// <summary>Normal maps como NormalMap, mapas de datos en Linear, todo a 1024 (WebGL).</summary>
        public static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PbrDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                bool changed = false;
                void SetIf<T>(T cur, T val, System.Action<T> set) { if (!Equals(cur, val)) { set(val); changed = true; } }

                bool isNormal = path.EndsWith("_Normal.png");
                bool isColor = path.EndsWith("_BaseColor.png");
                SetIf(imp.textureType, isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => imp.textureType = v);
                SetIf(imp.sRGBTexture, isColor, v => imp.sRGBTexture = v);
                SetIf(imp.maxTextureSize, 1024, v => imp.maxTextureSize = v);
                SetIf(imp.mipmapEnabled, true, v => imp.mipmapEnabled = v);
                SetIf(imp.alphaSource, path.EndsWith("_MetallicSmoothness.png") || path.EndsWith("_TerrainMask.png")
                    ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None, v => imp.alphaSource = v);
                SetIf(imp.textureCompression, TextureImporterCompression.Compressed, v => imp.textureCompression = v);
                if (changed) imp.SaveAndReimport();
            }
        }
    }
}
