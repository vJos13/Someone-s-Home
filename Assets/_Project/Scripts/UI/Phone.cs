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
    /// Celular del jugador: notificaciones con vibración, chat por contacto y llamadas.
    /// [Tab] abre/cierra el celular. La interfaz se arma por código sobre el Canvas del HUD.
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

        readonly Dictionary<string, List<(bool mine, string text)>> chats = new();
        string currentContact = "";
        AudioSource audioSrc;
        RectTransform toast, panel;
        CanvasGroup toastGroup, panelGroup;
        TMP_Text toastText, headerText, bodyText, footerText;
        Coroutine toastRoutine;
        bool inCall;

        void Awake()
        {
            Instance = this;
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;
            if (!buzzClip) buzzClip = ProceduralAudio.Build(ProceduralAudio.Kind.Buzz, 1f, 3);
            if (!ringClip) ringClip = ProceduralAudio.Build(ProceduralAudio.Kind.Ring, 12f, 4);
            BuildUI();
        }

        // ════════════════════════ API ════════════════════════
        public void Receive(string contact, string text, bool notify = true)
        {
            Chat(contact).Add((false, text));
            currentContact = contact;
            Unread = !IsOpen;
            if (notify)
            {
                audioSrc.PlayOneShot(buzzClip, 0.9f);
                ShowToast($"<b>{contact}</b>\n{text}");
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
            headerText.text = contact;
            bodyText.text = "<align=center>\n\n\n<size=26>Llamando…</size>\n\n<size=18><color=#9aa>" + contact + "</color></size></align>";
            footerText.text = "";
            audioSrc.clip = ringClip;
            audioSrc.loop = true;
            audioSrc.Play();
            yield return new WaitForSeconds(rings * 6f - 4f);
            audioSrc.Stop();
            audioSrc.loop = false;
            bodyText.text = "<align=center>\n\n\n<size=22>Buzón de voz</size>\n\n<size=18><color=#9aa>" + contact + "</color></size></align>";
            if (!string.IsNullOrEmpty(voicemail)) yield return HUD.Instance.SayAndWait(contact + " (buzón)", voicemail);
            yield return new WaitForSeconds(0.5f);
            inCall = false;
            Open(false);
        }

        // ════════════════════════ UPDATE ════════════════════════
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || inCall) return;
            if (kb.tabKey.wasPressedThisFrame) Open(!IsOpen);
            if (CallAvailable && kb.qKey.wasPressedThisFrame) CallRequested = true;
        }

        // ════════════════════════ UI ════════════════════════
        List<(bool, string)> Chat(string contact)
        {
            if (!chats.TryGetValue(contact, out var list)) chats[contact] = list = new List<(bool, string)>();
            return list;
        }

        void Refresh()
        {
            if (!bodyText || inCall) return;
            headerText.text = string.IsNullOrEmpty(currentContact) ? "Mensajes" : currentContact;
            var sb = new StringBuilder();
            if (chats.TryGetValue(currentContact, out var list))
            {
                int from = Mathf.Max(0, list.Count - 9);
                for (int i = from; i < list.Count; i++)
                {
                    var (mine, text) = list[i];
                    sb.Append(mine ? "<align=right><color=#cfe3ff>" : "<align=left><color=#e8e8e8>");
                    sb.Append(text).Append("</color></align>\n");
                }
            }
            bodyText.text = sb.ToString();
            footerText.text = CallAvailable ? $"[Q] Llamar a {CallContact}    [Tab] Guardar" : "[Tab] Guardar";
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
            toastText.text = text + "\n<size=14><color=#9aa>[Tab] Abrir celular</color></size>";
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
            var font = hud.GetComponentInChildren<TMP_Text>(true)?.font;

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
            TMP_Text Txt(RectTransform rt, float size, TextAlignmentOptions align)
            {
                var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
                t.font = font; t.fontSize = size; t.alignment = align; t.color = Color.white;
                t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; t.richText = true;
                return t;
            }

            // Notificación (arriba a la derecha, como un banner).
            toast = Rect("Celular_Notificacion", canvas.transform, new Vector2(1, 1), new Vector2(380, 92), new Vector2(-24, -24));
            Img(toast, new Color(0.08f, 0.09f, 0.1f, 0.92f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            var tt = Rect("Texto", toast, new Vector2(0.5f, 0.5f), new Vector2(350, 80), Vector2.zero);
            toastText = Txt(tt, 19, TextAlignmentOptions.TopLeft);

            // Celular (abajo a la derecha).
            panel = Rect("Celular", canvas.transform, new Vector2(1, 0), new Vector2(300, 540), new Vector2(-40, -560));
            Img(panel, new Color(0.03f, 0.03f, 0.035f, 1f));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            var screen = Rect("Pantalla", panel, new Vector2(0.5f, 0.5f), new Vector2(276, 500), Vector2.zero);
            Img(screen, new Color(0.11f, 0.13f, 0.16f, 1f));
            var header = Rect("Encabezado", screen, new Vector2(0.5f, 1), new Vector2(276, 52), Vector2.zero);
            Img(header, new Color(0.16f, 0.19f, 0.23f, 1f));
            headerText = Txt(Rect("Contacto", header, new Vector2(0.5f, 0.5f), new Vector2(250, 44), Vector2.zero), 22, TextAlignmentOptions.Center);
            bodyText = Txt(Rect("Mensajes", screen, new Vector2(0.5f, 0.5f), new Vector2(250, 380), new Vector2(0, 4)), 19, TextAlignmentOptions.BottomLeft);
            bodyText.lineSpacing = 18f;
            footerText = Txt(Rect("Ayuda", screen, new Vector2(0.5f, 0), new Vector2(260, 40), new Vector2(0, 6)), 15, TextAlignmentOptions.Center);
            footerText.color = new Color(0.65f, 0.7f, 0.72f);
        }
    }
}
