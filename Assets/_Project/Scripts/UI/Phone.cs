using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Celular del jugador (de barra, 1998): pantalla LCD verde monocroma, SMS en mayúsculas, timbre y bips de época.
    /// Notificaciones con vibración, mensajes por contacto y llamadas.
    /// [Tab] abre/cierra el celular (o sacarlo del inventario). La interfaz se arma por código sobre el Canvas del HUD.
    /// Señal: depende de la altura del jugador. Arriba, en la casa y el campo, hay cobertura completa; bajando la
    /// colina hacia el río se pierde. Los mensajes que llegan sin señal esperan y entran todos juntos al recuperarla.
    /// </summary>
    public class Phone : MonoBehaviour
    {
        public static Phone Instance { get; private set; }

        [SerializeField] AudioClip buzzClip;
        [SerializeField] AudioClip ringClip;
        [SerializeField] AudioClip messageSentClip;

        public bool IsOpen { get; private set; }
        /// <summary>Si es true, [Q] pide llamar (lo atiende el director con CallRequested).</summary>
        public bool CallAvailable { get; set; }
        public string CallContact { get; set; } = "";
        public bool CallRequested { get; set; }
        public bool Unread { get; private set; }

        [Header("Señal (altura del jugador, m)")]
        [Tooltip("Por encima de esta altura: 4 barras (la casa está a ~20 m).")]
        [SerializeField] float fullSignalHeight = 17.5f;
        [Tooltip("Por debajo de esta altura: sin servicio (el río está a ~6 m).")]
        [SerializeField] float noSignalHeight = 9.5f;

        /// <summary>Barras de señal (0 = sin servicio … 4).</summary>
        public int SignalBars { get; private set; } = 4;
        public bool HasSignal => SignalBars > 0;
        readonly Queue<(string contact, string text, bool notify)> pending = new();
        Transform playerTf;
        Image[] signalBars;
        TMP_Text signalText;
        bool lostSignalShown;

        readonly Dictionary<string, List<(bool mine, string text)>> chats = new();
        string currentContact = "";
        AudioSource audioSrc;
        RectTransform toast, panel;
        CanvasGroup toastGroup, panelGroup;
        TMP_Text toastText, headerText, bodyText, footerText, toastHint;
        Coroutine toastRoutine;
        bool inCall;
        AudioClip smsClip;
        RectTransform batteryFill;

        // Colores del LCD: verde del cristal líquido y "tinta" oscura.
        static readonly Color LcdGreen = new(0.58f, 0.67f, 0.42f);
        static readonly Color LcdInk = new(0.1f, 0.14f, 0.08f);
        const string InkHex = "#1A2414", FaintHex = "#1A241488";

        void Awake()
        {
            Instance = this;
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;
            if (!buzzClip) buzzClip = ProceduralAudio.Build(ProceduralAudio.Kind.Buzz, 1f, 3);
            if (!ringClip) ringClip = RetroRing(12f);
            smsClip = RetroSmsBeep();
            BuildUI();
        }

        // ════════════════════════ API ════════════════════════
        public void Receive(string contact, string text, bool notify = true)
        {
            if (!HasSignal) { pending.Enqueue((contact, text, notify)); return; }
            Deliver(contact, text, notify);
        }

        void Deliver(string contact, string text, bool notify)
        {
            Chat(contact).Add((false, text));
            currentContact = contact;
            Unread = !IsOpen;
            if (notify)
            {
                audioSrc.PlayOneShot(buzzClip, 0.7f);
                audioSrc.PlayOneShot(smsClip, 0.45f);
                ShowToast($"1 MENSAJE NUEVO\n<b>{Up(contact)}:</b> {Up(text)}");
            }
            Refresh();
        }

        public void Send(string contact, string text)
        {
            Chat(contact).Add((true, text));
            currentContact = contact;
            if (messageSentClip) audioSrc.PlayOneShot(messageSentClip, 0.5f);
            Refresh();
        }

        public void Open(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            if (open) Unread = false;
            StopCoroutineSafe(ref fadeRoutine);
            fadeRoutine = StartCoroutine(FadePanel(open ? 1f : 0f));
            Refresh();
        }

        /// <summary>Llama: suena N veces y, si nadie contesta, muestra el buzón.</summary>
        public IEnumerator Call(string contact, int rings, string voicemail)
        {
            inCall = true;
            currentContact = contact;
            Open(true);
            headerText.text = Up(contact);
            bodyText.text = "<align=center>\n<size=22>LLAMANDO</size>\n<size=22>. . .</size>\n\n" + Up(contact) + "</align>";
            footerText.text = "";
            audioSrc.clip = ringClip;
            audioSrc.loop = true;
            audioSrc.Play();
            yield return new WaitForSeconds(rings * 6f - 4f);
            audioSrc.Stop();
            audioSrc.loop = false;
            bodyText.text = "<align=center>\n<size=20>BUZON DE VOZ</size>\n\n" + Up(contact) + "</align>";
            if (!string.IsNullOrEmpty(voicemail)) yield return HUD.Instance.SayAndWait(contact + " (buzón)", voicemail);
            yield return new WaitForSeconds(0.5f);
            inCall = false;
            Open(false);
        }

        // ════════════════════════ UPDATE ════════════════════════
        void Update()
        {
            UpdateSignal();
            var kb = Keyboard.current;
            if (kb == null || inCall || GamePause.IsPaused) return;
            if (kb.tabKey.wasPressedThisFrame && !InspectionView.IsOpen && !MainMenuController.Active) Open(!IsOpen);
            if (CallAvailable && kb.qKey.wasPressedThisFrame) CallRequested = true;
        }

        // ════════════════════════ SEÑAL ════════════════════════
        // Sacar el celular del inventario lo abre; guardarlo lo cierra.
        InventorySystem inventory;
        ItemId lastEquipped;

        void HookInventory()
        {
            if (inventory || !InventorySystem.Instance) return;
            inventory = InventorySystem.Instance;
            inventory.EquippedChanged += id =>
            {
                if (!inCall)
                {
                    if (id == ItemId.Phone) Open(true);
                    else if (lastEquipped == ItemId.Phone && IsOpen) Open(false);
                }
                lastEquipped = id;
            };
        }

        void UpdateSignal()
        {
            HookInventory();
            if (!playerTf)
            {
                var p = FindAnyObjectByType<FirstPersonController>();
                if (!p) return;
                playerTf = p.transform;
            }
            float k = Mathf.InverseLerp(noSignalHeight, fullSignalHeight, playerTf.position.y);
            // Histéresis leve: la barra no titila justo en el límite.
            int target = k <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(k * 4f - 0.15f), 1, 4);
            if (target == SignalBars) return;
            int before = SignalBars;
            SignalBars = target;
            RefreshSignal();
            if (before > 0 && target == 0 && !lostSignalShown)
            {
                lostSignalShown = true;
                ShowToast("SIN SERVICIO\n<size=85%>NO HAY SEÑAL AQUI ABAJO</size>");
            }
            if (target > 0 && pending.Count > 0)
            {
                while (pending.Count > 0)
                {
                    var (c, t, n) = pending.Dequeue();
                    Deliver(c, t, n && pending.Count == 0);    // una sola vibración para todos
                }
            }
        }

        void RefreshSignal()
        {
            if (signalBars != null)
                for (int b = 0; b < signalBars.Length; b++)
                    signalBars[b].color = new Color(LcdInk.r, LcdInk.g, LcdInk.b, b < SignalBars ? 0.95f : 0.12f);
            if (signalText) signalText.text = SignalBars == 0 ? "SIN SERVICIO" : "";
        }

        // ════════════════════════ UI ════════════════════════
        List<(bool, string)> Chat(string contact)
        {
            if (!chats.TryGetValue(contact, out var list)) chats[contact] = list = new List<(bool, string)>();
            return list;
        }

        /// <summary>Texto como en un LCD de 1998: mayúsculas, sin tildes (la fuente del teléfono no las tenía).</summary>
        static string Up(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.ToUpperInvariant());
            sb.Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U').Replace("…", "...");
            return sb.ToString();
        }

        void Refresh()
        {
            if (!bodyText || inCall) return;
            headerText.text = string.IsNullOrEmpty(currentContact) ? "MENSAJES" : Up(currentContact);
            var sb = new StringBuilder();
            if (chats.TryGetValue(currentContact, out var list))
            {
                // La pantalla es chica: los últimos mensajes, el más nuevo abajo.
                int from = Mathf.Max(0, list.Count - 5);
                for (int i = from; i < list.Count; i++)
                {
                    var (mine, text) = list[i];
                    sb.Append(mine ? $"<color={FaintHex}>YO:</color> " : "> ");
                    sb.Append(Up(text)).Append('\n');
                }
            }
            bodyText.text = sb.ToString();
            footerText.text = CallAvailable ? "Q:LLAMAR      TAB:SALIR" : "               TAB:SALIR";
        }

        Coroutine fadeRoutine;
        void StopCoroutineSafe(ref Coroutine c) { if (c != null) StopCoroutine(c); c = null; }

        IEnumerator FadePanel(float to)
        {
            float from = panelGroup.alpha;
            Vector2 a = panel.anchoredPosition;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                panelGroup.alpha = Mathf.Lerp(from, to, e);
                panel.anchoredPosition = new Vector2(a.x, Mathf.Lerp(to > 0 ? -560f : 24f, to > 0 ? 24f : -560f, e));
                yield return null;
            }
            panelGroup.alpha = to;
            panel.anchoredPosition = new Vector2(a.x, to > 0 ? 24f : -560f);
        }

        void ShowToast(string text)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(text));
        }

        IEnumerator ToastRoutine(string text)
        {
            toastText.text = text;
            toastHint.text = "[Tab] Abrir celular";
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.25f) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 1f;
            float hold = Mathf.Clamp(2.5f + text.Length * 0.04f, 3f, 6f);
            for (float t = 0; t < hold && !IsOpen; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 0f;
        }

        void BuildUI()
        {
            var hud = FindAnyObjectByType<HUD>();
            var canvas = hud ? hud.GetComponentInParent<Canvas>() : null;
            if (!canvas) { Debug.LogWarning("[Phone] No hay Canvas del HUD."); return; }
            var mono = RetroFont.Bold;
            var uiFont = hud.GetComponentInChildren<TMP_Text>(true)?.font;

            RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
            {
                var go = new GameObject(name, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;
                return rt;
            }
            Image Img(RectTransform rt, Color c) { var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return i; }
            TMP_Text Txt(RectTransform rt, float size, TextAlignmentOptions align, TMP_FontAsset f = null)
            {
                var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
                t.font = f ? f : mono; t.fontSize = size; t.alignment = align; t.color = LcdInk;
                t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; t.richText = true;
                return t;
            }
            void PixelGrid(RectTransform lcd)
            {
                // Rejilla de píxeles del LCD (líneas finas más oscuras cada 3 px de pantalla).
                var grid = Rect("Rejilla", lcd, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
                var img = grid.gameObject.AddComponent<RawImage>();
                img.texture = LcdGridTexture();
                img.raycastTarget = false;
                img.uvRect = new Rect(0, 0, lcd.sizeDelta.x / 3f, lcd.sizeDelta.y / 3f);
            }

            // ── Notificación: un celular "asomando" arriba a la derecha con el SMS en su LCD ──
            toast = Rect("Celular_Notificacion", canvas.transform, new Vector2(1, 1), new Vector2(400, 118), new Vector2(-24, -24));
            Img(toast, new Color(0.12f, 0.13f, 0.15f, 0.96f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            var toastLcd = Rect("LCD", toast, new Vector2(0.5f, 1), new Vector2(380, 82), new Vector2(0, -8));
            Img(toastLcd, LcdGreen);
            PixelGrid(toastLcd);
            toastText = Txt(Rect("Texto", toastLcd, new Vector2(0.5f, 0.5f), new Vector2(360, 72), Vector2.zero), 17, TextAlignmentOptions.TopLeft);
            toastHint = Txt(Rect("Ayuda", toast, new Vector2(0.5f, 0), new Vector2(380, 26), new Vector2(0, 2)), 14, TextAlignmentOptions.Center, uiFont);
            toastHint.color = new Color(0.75f, 0.78f, 0.8f);

            // ── El celular (abajo a la derecha) ──
            panel = Rect("Celular", canvas.transform, new Vector2(1, 0), new Vector2(300, 540), new Vector2(-40, -560));
            Img(panel, new Color(0.12f, 0.13f, 0.15f, 1f));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            var antenna = Rect("Antena", panel, new Vector2(1, 1), new Vector2(30, 46), new Vector2(-56, 0));
            antenna.pivot = new Vector2(0.5f, 0);
            antenna.anchoredPosition = new Vector2(-56, 0);
            Img(antenna, new Color(0.06f, 0.06f, 0.07f, 1f));
            var face = Rect("Frente", panel, new Vector2(0.5f, 0.5f), new Vector2(276, 516), Vector2.zero);
            Img(face, new Color(0.08f, 0.085f, 0.1f, 1f));
            Img(Rect("Auricular", face, new Vector2(0.5f, 1), new Vector2(70, 8), new Vector2(0, -18)), new Color(0.02f, 0.02f, 0.025f, 1f));

            var frame = Rect("MarcoLCD", face, new Vector2(0.5f, 1), new Vector2(248, 236), new Vector2(0, -40));
            Img(frame, new Color(0.02f, 0.02f, 0.025f, 1f));
            var lcd = Rect("LCD", frame, new Vector2(0.5f, 0.5f), new Vector2(232, 220), Vector2.zero);
            Img(lcd, LcdGreen);

            // Estado: barras de señal (izquierda), batería (derecha) y "SIN SERVICIO".
            var status = Rect("Estado", lcd, new Vector2(0.5f, 1), new Vector2(220, 20), new Vector2(0, -4));
            signalBars = new Image[4];
            for (int b = 0; b < 4; b++)
            {
                var bar = Rect("Barra" + b, status, new Vector2(0, 0), new Vector2(4, 5 + b * 3f), new Vector2(2 + b * 6f, 1));
                signalBars[b] = Img(bar, LcdInk);
            }
            signalText = Txt(Rect("SinServicio", status, new Vector2(0, 0.5f), new Vector2(140, 18), new Vector2(102, 0)), 12, TextAlignmentOptions.Left);
            var battery = Rect("Bateria", status, new Vector2(1, 0.5f), new Vector2(24, 11), new Vector2(-4, 0));
            Img(battery, LcdInk);
            var batteryInside = Rect("Hueco", battery, new Vector2(0.5f, 0.5f), new Vector2(20, 7), Vector2.zero);
            Img(batteryInside, LcdGreen);
            batteryFill = Rect("Carga", batteryInside, new Vector2(0, 0.5f), new Vector2(15, 5), new Vector2(1, 0));
            Img(batteryFill, LcdInk);
            Img(Rect("Polo", battery, new Vector2(0, 0.5f), new Vector2(3, 5), new Vector2(-3, 0)), LcdInk);
            RefreshSignal();

            headerText = Txt(Rect("Contacto", lcd, new Vector2(0.5f, 1), new Vector2(220, 24), new Vector2(0, -26)), 17, TextAlignmentOptions.Center);
            Img(Rect("Linea", lcd, new Vector2(0.5f, 1), new Vector2(216, 2), new Vector2(0, -52)), LcdInk);
            bodyText = Txt(Rect("Mensajes", lcd, new Vector2(0.5f, 0.5f), new Vector2(216, 132), new Vector2(0, -8)), 14, TextAlignmentOptions.BottomLeft);
            bodyText.lineSpacing = 4f;
            bodyText.overflowMode = TextOverflowModes.Truncate;
            footerText = Txt(Rect("Teclas", lcd, new Vector2(0.5f, 0), new Vector2(220, 20), new Vector2(0, 3)), 12, TextAlignmentOptions.Center);
            PixelGrid(lcd);

            // Teclado: navegación, verde / roja y 4×3 numéricas con sus letras.
            var keyColor = new Color(0.2f, 0.215f, 0.24f, 1f);
            var keys = Rect("Teclado", face, new Vector2(0.5f, 0), new Vector2(248, 230), new Vector2(0, 12));
            Img(Rect("Navegar", keys, new Vector2(0.5f, 1), new Vector2(74, 26), new Vector2(0, -2)), keyColor);
            Img(Rect("Verde", keys, new Vector2(0, 1), new Vector2(54, 22), new Vector2(18, -4)), new Color(0.16f, 0.3f, 0.17f, 1f));
            Img(Rect("Roja", keys, new Vector2(1, 1), new Vector2(54, 22), new Vector2(-18, -4)), new Color(0.38f, 0.12f, 0.1f, 1f));
            string[] labels = { "1", "2 abc", "3 def", "4 ghi", "5 jkl", "6 mno", "7 pqrs", "8 tuv", "9 wxyz", "*", "0 _", "#" };
            for (int i = 0; i < 12; i++)
            {
                int r = i / 3, c = i % 3;
                var k = Rect("Tecla" + labels[i][0], keys, new Vector2(0.5f, 1), new Vector2(66, 36), new Vector2((c - 1) * 76f, -42 - r * 46f));
                Img(k, keyColor);
                var kt = Txt(Rect("Txt", k, new Vector2(0.5f, 0.5f), new Vector2(66, 36), Vector2.zero), 15, TextAlignmentOptions.Center);
                kt.color = new Color(0.78f, 0.8f, 0.82f);
                kt.text = labels[i].Length > 1 ? $"{labels[i][0]}<size=60%> {labels[i].Substring(2)}</size>" : labels[i];
            }
        }

        static Texture2D gridTex;

        static Texture2D LcdGridTexture()
        {
            if (gridTex) return gridTex;
            gridTex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[9];
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    px[y * 3 + x] = (x == 2 || y == 2) ? new Color32(40, 60, 30, 38) : new Color32(0, 0, 0, 0);
            gridTex.SetPixels32(px);
            gridTex.Apply();
            return gridTex;
        }

        // ── Sonidos de época (sintetizados) ──
        /// <summary>Timbre electrónico clásico: trino de dos tonos, 1 s sonando y 2 s de silencio.</summary>
        static AudioClip RetroRing(float seconds)
        {
            const int rate = 22050;
            int n = (int)(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float cycle = t % 3f;
                if (cycle > 1f) continue;
                float f = (Mathf.FloorToInt(t * 20f) % 2 == 0) ? 1320f : 1650f;
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f;
                float env = Mathf.Min(1f, cycle * 60f) * Mathf.Min(1f, (1f - cycle) * 60f);
                data[i] = sq * env * 0.22f;
            }
            var clip = AudioClip.Create("SFX_TimbreCelular98", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Aviso de SMS: tres bips agudos y cortos de parlante chiquito.</summary>
        static AudioClip RetroSmsBeep()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.62f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float slot = t % 0.2f;
                if (t > 0.6f || slot > 0.11f) continue;
                float env = Mathf.Min(1f, slot * 300f) * Mathf.Min(1f, (0.11f - slot) * 300f);
                float s = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 2093f * t));
                data[i] = s * env * 0.18f;
            }
            var clip = AudioClip.Create("SFX_SMS98", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
