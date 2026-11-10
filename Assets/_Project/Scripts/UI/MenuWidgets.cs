using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Piezas de interfaz del menú construidas por código (uGUI + TextMeshPro, sin sprites: rectángulos planos).
    /// Estética: negro/gris/blanco, transparencias suaves, tipografía espaciada. Lo usa GameMenu.
    /// </summary>
    public static class MenuWidgets
    {
        public static readonly Color TextIdle = new(0.62f, 0.62f, 0.62f, 1f);
        public static readonly Color TextBright = new(0.93f, 0.93f, 0.91f, 1f);
        public static readonly Color TextDim = new(0.45f, 0.45f, 0.45f, 1f);
        public static readonly Color Line = new(1f, 1f, 1f, 0.14f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size,
                                           Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.richText = true;
            return t;
        }

        public static LayoutElement Layout(Component c, float height = -1, float width = -1, float flexW = -1)
        {
            if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) le.preferredHeight = le.minHeight = height;
            if (width >= 0) le.preferredWidth = le.minWidth = width;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        /// <summary>Botón de texto con velo, línea de acento y animación al pasar el cursor / seleccionar.</summary>
        public static Button Button(Transform parent, TMP_FontAsset font, string label, Action onClick, float size = 30f,
                                    float height = 58f, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rt = Rect("Btn_" + label, parent);
            Layout(rt, height);
            var veil = rt.gameObject.AddComponent<Image>();
            veil.color = new Color(1f, 1f, 1f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = veil;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var accent = Image("Acento", rt, TextBright);
            accent.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            accent.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            accent.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            float pad = align == TextAlignmentOptions.Left ? 40f : 0f;
            var text = Text("Texto", rt, font, label, size, TextIdle, align, 6f);
            Stretch(text.rectTransform, pad, 0, 0, 0);

            var fx = rt.gameObject.AddComponent<MenuHoverFx>();
            fx.label = text; fx.veil = veil; fx.accent = accent.rectTransform;
            if (align != TextAlignmentOptions.Left) fx.accentWidth = 0f;
            return btn;
        }

        /// <summary>Fila "Etiqueta ······ [control] valor".</summary>
        public static RectTransform Row(Transform parent, TMP_FontAsset font, string label, out TextMeshProUGUI labelText)
        {
            var row = Rect("Fila_" + label, parent);
            Layout(row, 52f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.spacing = 18f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            labelText = Text("Etiqueta", row, font, label, 24f, TextIdle, TextAlignmentOptions.Left, 1.5f);
            Layout(labelText, 40f, 380f, 1f);
            return row;
        }

        public static Slider Slider(Transform row, TMP_FontAsset font, float min, float max, float value,
                                    Func<float, string> format, Action<float> onChange)
        {
            var root = Rect("Slider", row);
            Layout(root, 30f, 300f);
            var bg = Image("Fondo", root, Line);
            bg.rectTransform.anchorMin = new Vector2(0f, 0.5f); bg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bg.rectTransform.offsetMin = new Vector2(0f, -1.5f); bg.rectTransform.offsetMax = new Vector2(0f, 1.5f);

            var fillArea = Rect("Relleno", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(0f, -1.5f); fillArea.offsetMax = new Vector2(0f, 1.5f);
            var fill = Image("Barra", fillArea, new Color(0.85f, 0.85f, 0.83f, 0.85f));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var slideArea = Rect("Area", root);
            Stretch(slideArea, 7f, 7f, 0f, 0f);
            var handle = Image("Tirador", slideArea, TextBright, true);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(14f, 14f);

            // zona de clic más alta que la línea
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.8f, 0.8f, 0.8f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.fadeDuration = 0.12f;
            s.colors = colors;
            s.value = value;

            var valueText = Text("Valor", row, font, format(value), 22f, TextBright, TextAlignmentOptions.Right, 1f);
            Layout(valueText, 40f, 90f);
            s.onValueChanged.AddListener(v => { valueText.text = format(v); onChange(v); });
            return s;
        }

        public static Toggle Toggle(Transform row, bool value, Action<bool> onChange)
        {
            var root = Rect("Toggle", row);
            Layout(root, 30f, 300f);
            var box = Image("Caja", root, new Color(1f, 1f, 1f, 0.06f), true);
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            box.rectTransform.pivot = new Vector2(0f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(26f, 26f);
            var border = box.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.35f);
            border.effectDistance = new Vector2(1f, -1f);
            var check = Image("Marca", box.transform, TextBright);
            check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            check.rectTransform.sizeDelta = new Vector2(12f, 12f);

            var t = root.gameObject.AddComponent<Toggle>();
            t.targetGraphic = box;
            t.graphic = check;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.75f, 0.75f, 0.75f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.12f;
            t.colors = colors;
            t.isOn = value;
            t.onValueChanged.AddListener(v => onChange(v));
            return t;
        }
    }
}
