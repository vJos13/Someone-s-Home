using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Barra de inventario abajo al centro (5 espacios). Minimalista: se atenúa cuando no se usa y se aviva al
    /// cambiar de objeto o recoger algo. El espacio de la linterna muestra la batería y el del celular la señal.
    /// Se arma por código sobre el Canvas del HUD (no hace falta tocar la escena).
    /// </summary>
    public class InventoryHotbar : MonoBehaviour
    {
        const float SlotSize = 64f, Gap = 10f;
        const float IdleAlpha = 0.45f;
        const float LabelHeight = 26f, ToastHeight = 30f;
        static readonly Color Accent = new(0.91f, 0.76f, 0.48f);      // ámbar cálido (como los nombres del diálogo)

        sealed class Slot
        {
            public RectTransform rect;
            public Image bg, glow;
            public Image[] border;           // 4 barras: borde real (no un relleno que se transparente bajo el fondo)
            public RawImage icon;
            public TMP_Text number;
            public RectTransform meter;          // batería (barra) o señal (4 barras)
            public Image meterFill;
            public Image[] bars;
            public float flash;
            public ItemId shown = (ItemId)(-1);
        }

        InventorySystem inventory;
        CanvasGroup group;
        TMP_Text nameText;
        CanvasGroup nameGroup;
        readonly Slot[] slots = new Slot[InventorySystem.SlotCount];
        float wake;               // segundos de "barra viva"
        float nameTimer;
        // Etiqueta del espacio activo: sigue al espacio elegido ("LINTERNA · [Q] soltar" o "Vacío").
        RectTransform activeLabel;
        TMP_Text activeText;
        float activeX;
        Flashlight flashlight;

        public static InventoryHotbar Create(InventorySystem inv)
        {
            var hud = HUD.Instance;
            var canvas = hud ? hud.GetComponentInParent<Canvas>() : null;
            if (!canvas) return null;
            var root = MenuWidgets.Rect("Inventario", canvas.transform);
            var bar = root.gameObject.AddComponent<InventoryHotbar>();
            bar.Build(inv, root);
            return bar;
        }

        void Build(InventorySystem inv, RectTransform root)
        {
            inventory = inv;
            inventory.Changed += Refresh;
            inventory.EquippedChanged += _ => Refresh();      // el nombre lo muestra la etiqueta del espacio activo
            flashlight = inv.GetComponentInChildren<Flashlight>(true);

            float width = InventorySystem.SlotCount * SlotSize + (InventorySystem.SlotCount - 1) * Gap;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(width, SlotSize + LabelHeight + ToastHeight + 8f);
            root.anchoredPosition = new Vector2(0f, 22f);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            group.alpha = 0f;

            var font = InspectModels.Font;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i] = new Slot();
                s.rect = MenuWidgets.Rect("Espacio" + (i + 1), root);
                s.rect.anchorMin = s.rect.anchorMax = s.rect.pivot = new Vector2(0f, 0f);
                s.rect.sizeDelta = new Vector2(SlotSize, SlotSize);
                s.rect.anchoredPosition = new Vector2(i * (SlotSize + Gap), 0f);
                // Halo exterior (solo el activo) + marco: el activo lleva un borde ámbar de 3 px.
                s.glow = MenuWidgets.Image("Halo", s.rect, new Color(Accent.r, Accent.g, Accent.b, 0f));
                MenuWidgets.Stretch(s.glow.rectTransform, -7f, -7f, -7f, -7f);
                s.bg = MenuWidgets.Image("Fondo", s.rect, new Color(0.02f, 0.02f, 0.025f, 0.62f));
                MenuWidgets.Stretch(s.bg.rectTransform);
                s.border = new Image[4];
                for (int b = 0; b < 4; b++) s.border[b] = MenuWidgets.Image("Borde" + b, s.rect, Color.white);
                s.icon = new GameObject("Icono", typeof(RectTransform)).AddComponent<RawImage>();
                s.icon.transform.SetParent(s.rect, false);
                s.icon.raycastTarget = false;
                MenuWidgets.Stretch(s.icon.rectTransform, 10f, 10f, 8f, 12f);
                s.number = MenuWidgets.Text("Numero", s.rect, font, (i + 1).ToString(), 13f, MenuWidgets.TextDim, TextAlignmentOptions.TopLeft);
                MenuWidgets.Stretch(s.number.rectTransform, 5f, 0f, 2f, 0f);
            }

            // Etiqueta del espacio activo, justo encima de él.
            activeLabel = MenuWidgets.Rect("EspacioActivo", root);
            activeLabel.anchorMin = activeLabel.anchorMax = activeLabel.pivot = new Vector2(0f, 0f);
            activeLabel.sizeDelta = new Vector2(260f, LabelHeight);
            activeText = MenuWidgets.Text("Etiqueta", activeLabel, font, "", 18f, Accent, TextAlignmentOptions.Bottom, 3f);
            MenuWidgets.Stretch(activeText.rectTransform);
            activeText.textWrappingMode = TextWrappingModes.NoWrap;

            // Avisos breves ("Recogiste: …"), por encima de la etiqueta.
            nameText = MenuWidgets.Text("Nombre", root, font, "", 19f, MenuWidgets.TextBright, TextAlignmentOptions.Bottom, 4f);
            nameText.rectTransform.anchorMin = new Vector2(0f, 1f); nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
            nameText.rectTransform.offsetMin = new Vector2(-200f, -ToastHeight); nameText.rectTransform.offsetMax = new Vector2(200f, 0f);
            nameGroup = nameText.gameObject.AddComponent<CanvasGroup>();
            nameGroup.alpha = 0f;
            Refresh();
        }

        void OnDestroy()
        {
            if (inventory) inventory.Changed -= Refresh;
        }

        /// <summary>Recogiste algo en este espacio: destello y barra visible.</summary>
        public void Flash(int slot)
        {
            if (slot >= 0 && slot < slots.Length) slots[slot].flash = 1f;
            Wake();
        }

        public void Wake() => wake = 3.5f;

        /// <summary>Cambió el espacio activo: la barra se aviva y la etiqueta se actualiza ya.</summary>
        public void Selected()
        {
            Wake();
            RefreshActiveLabel();
        }

        void RefreshActiveLabel()
        {
            if (!activeText) return;
            var id = inventory.Equipped;
            var def = InventorySystem.Def(id);
            if (def == null) activeText.text = "<color=#FFFFFF80>Vacío</color>";
            else activeText.text = "<b>" + def.name.ToUpperInvariant() + "</b>"
                                   + (def.droppable ? "  <size=80%><color=#FFFFFF8C>[Q] soltar</color></size>" : "");
        }


        /// <summary>Texto breve sobre la barra ("Recogiste: Bolsa de basura").</summary>
        public void Toast(string message)
        {
            nameText.text = message;
            nameTimer = 2.4f;
            Wake();
        }


        void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                var id = inventory[i];
                if (s.shown != id)
                {
                    s.shown = id;
                    s.icon.texture = ItemIcons.Get(id);
                    s.icon.enabled = id != ItemId.None;
                    BuildMeter(s, id);
                }
            }
            RefreshActiveLabel();
            Wake();
        }

        /// <summary>Borde por fuera del espacio, de 'width' px (arriba, abajo, izquierda, derecha).</summary>
        static void SetBorder(Slot s, float width, Color color)
        {
            for (int b = 0; b < 4; b++)
            {
                var img = s.border[b];
                img.color = color;
                var rt = img.rectTransform;
                switch (b)
                {
                    case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                            rt.offsetMin = new Vector2(-width, 0f); rt.offsetMax = new Vector2(width, width); break;
                    case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
                            rt.offsetMin = new Vector2(-width, -width); rt.offsetMax = new Vector2(width, 0f); break;
                    case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
                            rt.offsetMin = new Vector2(-width, 0f); rt.offsetMax = new Vector2(0f, 0f); break;
                    default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                            rt.offsetMin = new Vector2(0f, 0f); rt.offsetMax = new Vector2(width, 0f); break;
                }
            }
        }

        void BuildMeter(Slot s, ItemId id)
        {
            if (s.meter) Destroy(s.meter.gameObject);
            s.meter = null; s.meterFill = null; s.bars = null;
            if (id == ItemId.Flashlight)
            {
                s.meter = MenuWidgets.Rect("Bateria", s.rect);
                s.meter.anchorMin = new Vector2(0f, 0f); s.meter.anchorMax = new Vector2(1f, 0f); s.meter.pivot = new Vector2(0.5f, 0f);
                s.meter.offsetMin = new Vector2(10f, 5f); s.meter.offsetMax = new Vector2(-10f, 8f);
                var back = MenuWidgets.Image("Vacio", s.meter, new Color(1f, 1f, 1f, 0.12f));
                MenuWidgets.Stretch(back.rectTransform);
                s.meterFill = MenuWidgets.Image("Carga", s.meter, new Color(0.9f, 0.88f, 0.8f, 0.9f));
                MenuWidgets.Stretch(s.meterFill.rectTransform);
                s.meterFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            else if (id == ItemId.Phone)
            {
                s.meter = MenuWidgets.Rect("Senal", s.rect);
                s.meter.anchorMin = s.meter.anchorMax = s.meter.pivot = new Vector2(1f, 1f);
                s.meter.anchoredPosition = new Vector2(-6f, -5f);
                s.meter.sizeDelta = new Vector2(16f, 10f);
                s.bars = new Image[4];
                for (int b = 0; b < 4; b++)
                {
                    var bar = MenuWidgets.Image("Barra" + b, s.meter, Color.white);
                    var rt = bar.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(3f, 3f + b * 2.4f);
                    rt.anchoredPosition = new Vector2(b * 4.2f, 0f);
                    s.bars[b] = bar;
                }
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool any = false;
            for (int i = 0; i < slots.Length; i++) any |= inventory[i] != ItemId.None;
            bool hidden = !any || inventory.InputBlocked && !GamePause.IsPaused;
            wake = Mathf.Max(0f, wake - dt);
            float target = hidden ? 0f : wake > 0f ? 1f : IdleAlpha;
            group.alpha = Mathf.MoveTowards(group.alpha, target, dt * (target > group.alpha ? 4f : 1.2f));

            nameTimer = Mathf.Max(0f, nameTimer - dt);
            nameGroup.alpha = Mathf.MoveTowards(nameGroup.alpha, nameTimer > 0f && !hidden ? 1f : 0f, dt * 3f);

            // La etiqueta sigue al espacio activo (con un deslizamiento corto) y siempre se lee entera.
            float targetX = inventory.SelectedSlot * (SlotSize + Gap) + SlotSize * 0.5f;
            activeX = Mathf.Abs(activeX - targetX) > 400f ? targetX : Mathf.Lerp(activeX, targetX, 1f - Mathf.Exp(-dt * 18f));
            activeLabel.anchoredPosition = new Vector2(activeX - activeLabel.sizeDelta.x * 0.5f, SlotSize + 5f);
            activeText.alpha = Mathf.Max(0.75f, group.alpha);

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                bool sel = inventory.SelectedSlot == i;
                s.flash = Mathf.MoveTowards(s.flash, 0f, dt * 1.5f);
                // Activo: borde ámbar grueso, halo, fondo más claro y 1.08×. Inactivo: borde fino y tenue.
                float frameA = (sel ? 0.95f : 0.16f) + s.flash * 0.6f;
                var borderColor = sel ? new Color(Accent.r, Accent.g, Accent.b, Mathf.Clamp01(frameA))
                                      : new Color(1f, 1f, 1f, Mathf.Clamp01(frameA));
                SetBorder(s, sel ? 3f : 1f, borderColor);
                s.glow.color = new Color(Accent.r, Accent.g, Accent.b, sel ? 0.1f + s.flash * 0.2f : s.flash * 0.25f);
                s.bg.color = sel ? new Color(0.17f, 0.15f, 0.12f, 0.97f) : new Color(0.02f, 0.02f, 0.025f, 0.62f);
                float scale = Mathf.Lerp(s.rect.localScale.x, (sel ? 1.08f : 1f) + s.flash * 0.22f, 1f - Mathf.Exp(-dt * 14f));
                s.rect.localScale = new Vector3(scale, scale, 1f);
                s.icon.color = new Color(1f, 1f, 1f, sel ? 0.95f : 0.6f);
                s.number.color = sel ? MenuWidgets.TextBright : MenuWidgets.TextDim;

                if (s.meterFill && flashlight)
                {
                    float b = flashlight.Battery;
                    s.meterFill.rectTransform.localScale = new Vector3(Mathf.Max(0.001f, b), 1f, 1f);
                    s.meterFill.color = b < 0.2f ? new Color(0.85f, 0.32f, 0.25f, 0.95f) : new Color(0.9f, 0.88f, 0.8f, 0.9f);
                }
                if (s.bars != null && Phone.Instance)
                {
                    int bars = Phone.Instance.SignalBars;
                    for (int b = 0; b < 4; b++) s.bars[b].color = new Color(1f, 1f, 1f, b < bars ? 0.9f : 0.18f);
                }
            }
        }
    }
}
