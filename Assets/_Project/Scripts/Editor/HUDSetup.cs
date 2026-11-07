using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// HUD con TextMeshPro. (UI.Text legacy no se dibujaba en algunas GPUs/plataformas → pantalla negra sin texto.)
    ///  - LoadFont(): devuelve la fuente TMP por defecto (Assets/TextMesh Pro, ya incluida en el repo).
    ///  - MigrateOpenScene(): convierte los UI.Text del HUD de la escena abierta a TextMeshProUGUI y reconecta HUD.
    /// Lo ejecuta el asistente vía Unity Pipeline: SomeonesHome.EditorTools.HUDSetup.MigrateOpenScene()
    /// </summary>
    public static class HUDSetup
    {
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font) return font;
            if (!font) Debug.LogError("[HUD] Faltan los TMP Essential Resources (Assets/TextMesh Pro). Window > TextMeshPro > Import TMP Essential Resources.");
            return font;
        }

        public static void Style(TMP_Text t, TMP_FontAsset font, float size, TextAnchor anchor, Color color)
        {
            if (font) t.font = font;
            t.fontSize = size;
            t.alignment = anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center,
            };
            t.color = color;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.outlineWidth = 0.18f;                         // contorno para leer sobre el cielo/pasto
            t.outlineColor = new Color32(0, 0, 0, 220);
        }

        public static void MigrateOpenScene()
        {
            var font = LoadFont();
            var hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
            if (!hud) { Debug.LogError("[HUD] No hay HUD en la escena."); return; }

            var converted = new Dictionary<string, TMP_Text>();
            foreach (var old in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                var go = old.gameObject;
                string text = old.text;
                int size = old.fontSize;
                var anchor = old.alignment;
                var color = old.color;
                foreach (var sh in go.GetComponents<UnityEngine.UI.Shadow>()) Object.DestroyImmediate(sh);
                Object.DestroyImmediate(old);
                var t = go.AddComponent<TextMeshProUGUI>();
                Style(t, font, size, anchor, color);
                t.text = text;
                if (go.name == "Tarjeta") t.lineSpacing = 20f;
                converted[go.name] = t;
            }
            // Si ya estaba migrado, igual reconecta.
            foreach (var t in hud.GetComponentsInChildren<TMP_Text>(true)) converted.TryAdd(t.name, t);

            var so = new SerializedObject(hud);
            void Link(string field, string goName)
            {
                if (converted.TryGetValue(goName, out var t)) so.FindProperty(field).objectReferenceValue = t;
                else Debug.LogWarning($"[HUD] Falta {goName}");
            }
            Link("objectiveText", "ObjetivoTexto");
            Link("subtitleText", "SubtituloTexto");
            Link("promptText", "Prompt");
            Link("cardText", "Tarjeta");
            so.ApplyModifiedPropertiesWithoutUndo();

            var scene = hud.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[HUD] {converted.Count} textos en TextMeshPro.");
        }
    }
}
