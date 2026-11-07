using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// HUD mínimo estilo "slice-of-life horror": objetivo arriba a la izquierda, subtítulos abajo,
    /// prompt de interacción al centro, fundido a negro y tarjetas de texto (intro/epílogo).
    /// Los diálogos NO tienen voz: el texto aparece letra a letra con un "tic" (máquina de escribir).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        [SerializeField] TMP_Text objectiveText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text cardText;
        [SerializeField] CanvasGroup fader;
        [SerializeField] CanvasGroup subtitleGroup;
        [SerializeField] CanvasGroup objectiveGroup;

        [Header("Máquina de escribir")]
        [SerializeField] float charsPerSecond = 38f;
        [SerializeField] float cardCharsPerSecond = 26f;
        [Tooltip("Sonido por letra. Si está vacío se genera un 'tic' suave por código.")]
        [SerializeField] AudioClip typeBlip;
        [SerializeField] [Range(0, 1)] float blipVolume = 0.35f;
        [SerializeField] int blipEveryNChars = 2;

        AudioSource blipSource;

        readonly Queue<(string speaker, string line, float duration)> queue = new();
        Coroutine subtitleRoutine;

        public bool IsSpeaking => subtitleRoutine != null;

        void Awake()
        {
            Instance = this;
            if (subtitleGroup) subtitleGroup.alpha = 0f;
            if (objectiveGroup) objectiveGroup.alpha = 0f;
            if (promptText) promptText.text = "";
            if (cardText) cardText.text = "";

            blipSource = gameObject.AddComponent<AudioSource>();
            blipSource.playOnAwake = false;
            blipSource.spatialBlend = 0f;
            if (!typeBlip) typeBlip = MakeBlip();
        }

        /// <summary>Tic corto y apagado (tecla de máquina de escribir / blip de diálogo).</summary>
        static AudioClip MakeBlip()
        {
            const int rate = 44100;
            int n = rate * 35 / 1000;
            var data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 140f);
                float tone = Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 1040f * t) * 0.15f;
                float click = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 900f) * 0.5f;
                data[i] = (tone + click) * env * 0.8f;
            }
            var clip = AudioClip.Create("SFX_TypeBlip", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Revela el texto letra a letra con sonido. Termina cuando está completo.</summary>
        IEnumerator Typewrite(TMP_Text label, string text, float cps)
        {
            label.text = text;
            label.maxVisibleCharacters = 0;
            label.ForceMeshUpdate();
            int total = label.textInfo.characterCount;
            float shown = 0f;
            int last = 0;
            while (last < total)
            {
                shown += cps * Time.deltaTime;
                int now = Mathf.Min(total, Mathf.FloorToInt(shown));
                for (int i = last; i < now; i++)
                {
                    char ch = label.textInfo.characterInfo[i].character;
                    if (!char.IsWhiteSpace(ch) && i % Mathf.Max(1, blipEveryNChars) == 0 && typeBlip)
                    {
                        blipSource.pitch = Random.Range(0.92f, 1.1f);
                        blipSource.PlayOneShot(typeBlip, blipVolume);
                    }
                }
                last = now;
                label.maxVisibleCharacters = now;
                // Pequeña pausa dramática en puntuación.
                if (now > 0 && now < total && ".,?!…".IndexOf(label.textInfo.characterInfo[now - 1].character) >= 0)
                    yield return new WaitForSeconds(0.12f);
                yield return null;
            }
            label.maxVisibleCharacters = 99999;
        }

        // ── Prompt ──
        public void SetPrompt(string text)
        {
            if (promptText) promptText.text = text ?? "";
        }

        // ── Objetivo ──
        public void SetObjective(string text)
        {
            StartCoroutine(ObjectiveRoutine(text));
        }

        IEnumerator ObjectiveRoutine(string text)
        {
            if (!objectiveText) yield break;
            yield return FadeGroup(objectiveGroup, 0f, 0.4f);
            objectiveText.text = text ?? "";
            if (!string.IsNullOrEmpty(text)) yield return FadeGroup(objectiveGroup, 1f, 0.8f);
        }

        // ── Subtítulos ──
        /// <summary>Encola una línea. speaker vacío = pensamiento del jugador (cursiva).</summary>
        public void Say(string speaker, string line, float duration = -1f)
        {
            // duration = tiempo de lectura DESPUÉS de que termina de escribirse.
            if (duration <= 0f) duration = Mathf.Clamp(0.9f + line.Length * 0.03f, 1.4f, 4f);
            queue.Enqueue((speaker, line, duration));
            subtitleRoutine ??= StartCoroutine(SubtitleRoutine());
        }

        public IEnumerator SayAndWait(string speaker, string line, float duration = -1f)
        {
            Say(speaker, line, duration);
            while (IsSpeaking) yield return null;
        }

        IEnumerator SubtitleRoutine()
        {
            while (queue.Count > 0)
            {
                var (speaker, line, duration) = queue.Dequeue();
                string text = string.IsNullOrEmpty(speaker)
                    ? $"<i>{line}</i>"
                    : $"<b><color=#E8C27A>{speaker}:</color></b> {line}";
                subtitleText.text = "";
                yield return FadeGroup(subtitleGroup, 1f, 0.15f);
                yield return Typewrite(subtitleText, text, charsPerSecond);
                yield return new WaitForSeconds(duration);
                yield return FadeGroup(subtitleGroup, 0f, 0.25f);
            }
            subtitleRoutine = null;
        }

        // ── Fundido y tarjetas ──
        public IEnumerator Fade(float to, float time) => FadeGroup(fader, to, time);

        public void SetFadeInstant(float alpha)
        {
            if (fader) fader.alpha = alpha;
        }

        /// <summary>Muestra un texto centrado sobre el negro (intro, epílogo, créditos).</summary>
        public IEnumerator ShowCard(string text, float hold)
        {
            if (!cardText) yield break;
            var c = cardText.color;
            c.a = 1f; cardText.color = c;
            yield return Typewrite(cardText, text, cardCharsPerSecond);
            yield return new WaitForSeconds(hold);
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 1.2f) { c.a = t; cardText.color = c; yield return null; }
            c.a = 0f; cardText.color = c;
            cardText.text = "";
        }

        /// <summary>Tarjeta que se queda en pantalla (pantalla final).</summary>
        public IEnumerator ShowCardStay(string text)
        {
            if (!cardText) yield break;
            var c = cardText.color;
            c.a = 1f; cardText.color = c;
            yield return Typewrite(cardText, text, cardCharsPerSecond);
        }

        static IEnumerator FadeGroup(CanvasGroup group, float to, float time)
        {
            if (!group) yield break;
            float from = group.alpha;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
