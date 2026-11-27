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
        /// <summary>Quién habla en el subtítulo actual (vacío: pensamiento o nadie). Lo usan los personajes para gesticular.</summary>
        public static string CurrentSpeaker { get; private set; } = "";

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
        /// <summary>Borde inferior del bloque de subtítulos (px de referencia): por encima de la barra de objetos.</summary>
        public const float SubtitleBottom = 172f;

        readonly Queue<(string speaker, string line, float duration)> queue = new();
        Coroutine subtitleRoutine;

        public bool IsSpeaking => subtitleRoutine != null;

        /// <summary>
        /// "Saltar historia": mientras está activo, las tarjetas, fundidos y líneas de diálogo terminan al instante
        /// (las corrutinas del relato siguen su curso, así que ningún evento queda sin ejecutar).
        /// </summary>
        public bool FastForward { get; private set; }

        // Prompt de interacción y mira
        CanvasGroup promptGroup;
        TMP_Text crosshair;
        bool promptWanted;
        float crosshairPulse;
        FirstPersonController player;

        void Awake()
        {
            Instance = this;
            CurrentSpeaker = "";
            GameSettings.EnsureLoaded();
            if (promptText)
            {
                if (!promptText.TryGetComponent(out promptGroup)) promptGroup = promptText.gameObject.AddComponent<CanvasGroup>();
                promptGroup.alpha = 0f;
                promptGroup.blocksRaycasts = false;
            }
            var mira = transform.Find("Mira");
            if (mira) crosshair = mira.GetComponent<TMP_Text>();
            // Menú de pausa, configuración y "Saltar historia" (UI propia, construida por código).
            if (!FindAnyObjectByType<GameMenu>()) new GameObject("GameMenu").AddComponent<GameMenu>().Init(this);
            if (subtitleGroup) subtitleGroup.alpha = 0f;
            if (objectiveGroup) objectiveGroup.alpha = 0f;
            if (promptText) promptText.text = "";
            if (cardText) cardText.text = "";

            // Tarjetas del relato con la letra de la cinta (generador de caracteres del VCR).
            if (cardText) { cardText.font = RetroFont.Bold; cardText.characterSpacing = 4f; }
            // El objetivo arranca debajo del OSD de la cinta (PLAY / contador, arriba a la izquierda).
            if (objectiveText) objectiveText.rectTransform.anchoredPosition += new Vector2(0f, -62f);
            // Subtítulos justo encima de la barra de objetos (que ocupa ~22–150 px desde abajo, con su etiqueta).
            if (subtitleText)
            {
                var r = subtitleText.rectTransform;
                r.anchorMin = new Vector2(0.15f, 0f); r.anchorMax = new Vector2(0.85f, 0f);
                r.pivot = new Vector2(0.5f, 0f);
                r.offsetMin = new Vector2(0f, SubtitleBottom); r.offsetMax = new Vector2(0f, SubtitleBottom + 260f);
                subtitleText.alignment = TextAlignmentOptions.Bottom;
            }

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
            while (last < total && !FastForward)
            {
                shown += cps * GameSettings.TextSpeed * Time.deltaTime;
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
        /// <summary>Texto libre en el prompt central (compatibilidad).</summary>
        public void SetPrompt(string text)
        {
            if (!promptText) return;
            promptWanted = !string.IsNullOrEmpty(text);
            if (promptWanted) promptText.text = text;
        }

        /// <summary>Prompt contextual de interacción: tecla en recuadro + acción. null lo oculta (con fundido).</summary>
        public void SetInteraction(string action)
        {
            if (string.IsNullOrEmpty(action)) { SetPrompt(null); return; }
            SetPrompt($"<mark=#FFFFFF26 padding=\"14,14,6,6\"><b> E </b></mark>   <color=#D9D9D9>{action}</color>");
        }

        /// <summary>Pequeño pulso de la mira al interactuar (feedback).</summary>
        public void PulseInteraction() => crosshairPulse = 1f;

        void Start() => player = FindAnyObjectByType<FirstPersonController>();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateObjective(dt);
            bool cinematic = player && player.InputLocked && player.LookLocked;
            if (promptGroup)
                promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptWanted && !cinematic ? 1f : 0f, dt * 7f);
            if (crosshair)
            {
                crosshairPulse = Mathf.MoveTowards(crosshairPulse, 0f, dt * 4f);
                float a = cinematic || GamePause.IsPaused ? 0f : promptWanted ? 0.95f : 0.45f;
                var c = crosshair.color;
                c.a = Mathf.MoveTowards(c.a, a, dt * 5f);
                crosshair.color = c;
                float s = (promptWanted ? 1.35f : 1f) + crosshairPulse * 0.5f;
                crosshair.transform.localScale = Vector3.Lerp(crosshair.transform.localScale, Vector3.one * s, dt * 12f);
            }
        }

        // ── Objetivo ──
        // Lo que se ve: el objetivo actual (shown) y, por un momento, el anterior ya tachado (prevDone) encima.
        string shown;
        bool shownDone;
        float lingerUntil = -1f;          // tras SetObjective(null) con el objetivo cumplido: se ve tachado un rato
        string prevDone;
        float prevUntil;
        float objectivePop;               // destello al cambiar / cumplir
        AudioClip doneClip;

        /// <summary>
        /// Cambia el objetivo AL INSTANTE (sin esperar fundidos). Si el anterior estaba cumplido, queda un momento
        /// tachado encima del nuevo. Las líneas tachadas con &lt;s&gt; que aparecen respecto del texto anterior
        /// (listas de tareas) suenan como "cumplido". null lo oculta (si estaba cumplido, después de un instante).
        /// </summary>
        public void SetObjective(string text)
        {
            if (!objectiveText) return;
            if (string.IsNullOrEmpty(text))
            {
                if (shown != null && shownDone) lingerUntil = Time.unscaledTime + 1.6f;
                else { shown = null; shownDone = false; }      // el texto se queda mientras se desvanece
                return;
            }
            if (shown != null && shownDone) { prevDone = Strike(shown); prevUntil = Time.unscaledTime + 1.8f; }
            else if (CountStrikes(text) > CountStrikes(shown)) PlayDone();
            shown = text;
            shownDone = false;
            lingerUntil = -1f;
            objectivePop = 1f;
            RenderObjective();
        }

        /// <summary>El jugador cumplió el objetivo actual: se tacha y se atenúa ya mismo, con un sonido suave.</summary>
        public void CompleteObjective()
        {
            if (shown == null || shownDone) return;
            shownDone = true;
            // Si la lista ya estaba toda tachada (cada tarea sonó al cumplirse), no se repite el sonido.
            if (CountStrikes(shown) < shown.Split('\n').Length) { objectivePop = 1f; PlayDone(); }
            RenderObjective();
        }

        void RenderObjective()
        {
            if (!objectiveText) return;
            var sb = new System.Text.StringBuilder();
            if (prevDone != null && Time.unscaledTime < prevUntil) sb.Append("<size=80%>").Append(prevDone).Append("</size>\n");
            if (shown != null) sb.Append(shownDone ? Strike(shown) : shown);
            objectiveText.text = sb.ToString();
        }

        void UpdateObjective(float dt)
        {
            if (!objectiveGroup) return;
            if (prevDone != null && Time.unscaledTime >= prevUntil) { prevDone = null; RenderObjective(); }
            if (shown != null && lingerUntil > 0f && Time.unscaledTime >= lingerUntil)
            {
                shown = null; shownDone = false; lingerUntil = -1f;
            }
            bool visible = shown != null || prevDone != null;
            objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, visible ? 1f : 0f, dt * (visible ? 4f : 2f));
            if (!visible && objectiveGroup.alpha <= 0f && objectiveText.text.Length > 0) objectiveText.text = "";
            objectivePop = Mathf.MoveTowards(objectivePop, 0f, dt * 2.5f);
            objectiveText.transform.localScale = Vector3.one * (1f + objectivePop * 0.04f);
        }

        static string Strike(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (!lines[i].Contains("<s>")) lines[i] = $"<s><color=#999>{lines[i]}</color></s>";
            return string.Join("\n", lines);
        }

        static int CountStrikes(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0, i = 0;
            while ((i = text.IndexOf("<s>", i, System.StringComparison.Ordinal)) >= 0) { n++; i += 3; }
            return n;
        }

        void PlayDone()
        {
            if (!doneClip) doneClip = MakeDoneChime();
            if (blipSource) blipSource.PlayOneShot(doneClip, 0.32f);
        }

        /// <summary>Dos notas suaves ascendentes (quinta), cortas: "cumplido" sin romper el clima.</summary>
        static AudioClip MakeDoneChime()
        {
            const int rate = 44100;
            int n = rate * 55 / 100;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float a = t < 0.09f ? Mathf.Sin(2f * Mathf.PI * 660f * t) * Mathf.Exp(-t * 18f) : 0f;
                float t2 = t - 0.09f;
                float b = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 990f * t2) * Mathf.Exp(-t2 * 7f) * Mathf.Min(1f, t2 * 200f) : 0f;
                data[i] = (a * 0.5f + b * 0.45f) * 0.6f;
            }
            var clip = AudioClip.Create("SFX_ObjetivoCumplido", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ── Subtítulos ──
        /// <summary>Encola una línea. speaker vacío = pensamiento del jugador (cursiva).</summary>
        public void Say(string speaker, string line, float duration = -1f)
        {
            if (FastForward) return;
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
                CurrentSpeaker = speaker ?? "";
                string text = string.IsNullOrEmpty(speaker)
                    ? $"<i>{line}</i>"
                    : $"<b><color=#E8C27A>{speaker}:</color></b> {line}";
                if (GameSettings.SubtitleBackground) text = $"<mark=#000000B0 padding=\"18,18,8,8\">{text}</mark>";
                subtitleText.text = "";
                yield return FadeGroup(subtitleGroup, 1f, 0.15f);
                yield return Typewrite(subtitleText, text, charsPerSecond);
                yield return new WaitForSeconds(duration);
                yield return FadeGroup(subtitleGroup, 0f, 0.25f);
            }
            subtitleRoutine = null;
            CurrentSpeaker = "";
        }

        // ── Fundido y tarjetas ──
        public IEnumerator Fade(float to, float time) => FadeGroup(fader, to, FastForward ? Mathf.Min(time, 0.6f) : time);

        /// <summary>Corta lo que se esté mostrando (subtítulos, tarjeta) y deja correr el relato sin texto.</summary>
        public void BeginFastForward()
        {
            FastForward = true;
            queue.Clear();
            if (subtitleRoutine != null) { StopCoroutine(subtitleRoutine); subtitleRoutine = null; }
            if (subtitleGroup) subtitleGroup.alpha = 0f;
            if (subtitleText) subtitleText.text = "";
            if (cardText) { cardText.text = ""; var c = cardText.color; c.a = 0f; cardText.color = c; }
        }

        public void EndFastForward() => FastForward = false;

        public void SetFadeInstant(float alpha)
        {
            if (fader) fader.alpha = alpha;
        }

        /// <summary>Muestra un texto centrado sobre el negro (intro, epílogo, créditos).</summary>
        public IEnumerator ShowCard(string text, float hold)
        {
            VhsLook.Pulse(0.55f, 0.5f);                     // corte de escena: salto de tracking
            if (!cardText || FastForward) yield break;
            var c = cardText.color;
            c.a = 1f; cardText.color = c;
            yield return Typewrite(cardText, text, cardCharsPerSecond);
            for (float t = 0f; t < hold && !FastForward; t += Time.deltaTime) yield return null;
            for (float t = 1f; t > 0f && !FastForward; t -= Time.deltaTime / 1.2f) { c.a = t; cardText.color = c; yield return null; }
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
