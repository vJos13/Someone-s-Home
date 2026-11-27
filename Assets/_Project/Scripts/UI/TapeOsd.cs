using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Indicadores de la cinta (estilo OSD de VCR / camcorder de 1998), sobre la imagen con el filtro VHS:
    ///   arriba a la izquierda  "PLAY ▶  SP  0:12:34"  (PAUSE ❚❚ en la pausa, STOP ■ en el menú principal)
    ///   abajo a la derecha     "PM  6:12" / "AUG.14 1998"
    ///
    /// La hora de la cinta sigue al relato: cada momento (Level1Director.Beat) tiene su hora y desde ahí corre en tiempo
    /// real. El reloj de la cocina está parado a las 3:07; la cinta pasa por esa hora durante el apagón.
    /// Se oculta con "Fecha y contador de la cinta" en Opciones → PANTALLA.
    /// </summary>
    public class TapeOsd : MonoBehaviour
    {
        // Hora de la cinta al empezar cada momento del relato (14 de agosto de 1998, un viernes).
        static readonly Dictionary<string, (int day, int h, int m)> BeatClock = new()
        {
            ["intro"] = (14, 18, 12), ["mochila"] = (14, 18, 14), ["explorar"] = (14, 18, 19), ["cocina"] = (14, 18, 31),
            ["tareas"] = (14, 18, 34), ["vistazo"] = (14, 18, 40), ["llamar"] = (14, 18, 44), ["campo"] = (14, 18, 47),
            ["colina"] = (14, 18, 53), ["columpio"] = (14, 19, 1), ["regreso"] = (14, 19, 22), ["noche"] = (14, 23, 41),
            ["mensajes"] = (14, 23, 58), ["ventana"] = (15, 0, 36), ["voz_puerta"] = (15, 0, 41), ["asegurar"] = (15, 0, 43),
            ["acostarse"] = (15, 0, 58), ["apagon"] = (15, 2, 58), ["huellas"] = (15, 7, 14), ["rastro"] = (15, 7, 17),
            ["papa"] = (15, 7, 41), ["epilogo"] = (15, 7, 55),
        };

        CanvasGroup group, dateGroup;
        TMP_Text stateText, counterText, timeText, dateText;
        RawImage icon;
        Texture2D playTex, pauseTex, stopTex;
        Level1Director director;
        string lastBeat;
        DateTime clockBase = new(1998, 8, 14, 18, 12, 0);
        float clockSince, tapeSeconds;
        int mode = -1;

        void Start() => Build();

        void Build()
        {
            var canvasGo = new GameObject("OSD_Cinta", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;                       // sobre el HUD (10), bajo el menú principal (50) y la pausa (100)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var root = (RectTransform)canvasGo.transform;

            playTex = Glyph((x, y) => x >= 2 && x <= 13 && Mathf.Abs(y - 7.5f) <= (13 - x) * 0.55f);
            pauseTex = Glyph((x, y) => y >= 1 && y <= 14 && ((x >= 2 && x <= 5) || (x >= 9 && x <= 12)));
            stopTex = Glyph((x, y) => x >= 2 && x <= 13 && y >= 2 && y <= 13);

            stateText = Label(root, "Estado", new Vector2(0, 1), new Vector2(60, -44), TextAlignmentOptions.TopLeft, 34f);
            icon = new GameObject("Icono", typeof(RectTransform)).AddComponent<RawImage>();
            icon.transform.SetParent(root, false);
            var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0, 1);
            ir.sizeDelta = new Vector2(28, 28);
            ir.anchoredPosition = new Vector2(166, -50);
            icon.raycastTarget = false;
            counterText = Label(root, "Contador", new Vector2(0, 1), new Vector2(214, -44), TextAlignmentOptions.TopLeft, 34f);

            var dateRoot = new GameObject("Fecha", typeof(RectTransform)).GetComponent<RectTransform>();
            dateRoot.SetParent(root, false);
            dateRoot.anchorMin = dateRoot.anchorMax = dateRoot.pivot = new Vector2(1, 0);
            dateRoot.sizeDelta = new Vector2(420, 100);
            dateRoot.anchoredPosition = new Vector2(-60, 44);
            dateGroup = dateRoot.gameObject.AddComponent<CanvasGroup>();
            timeText = Label(dateRoot, "Hora", new Vector2(1, 1), Vector2.zero, TextAlignmentOptions.TopRight, 34f);
            dateText = Label(dateRoot, "Dia", new Vector2(1, 0), Vector2.zero, TextAlignmentOptions.BottomRight, 34f);
        }

        /// <summary>Texto del OSD: blanco, monoespaciado, con sombra dura (como el generador de caracteres del VCR).</summary>
        static TMP_Text Label(RectTransform parent, string name, Vector2 anchor, Vector2 pos, TextAlignmentOptions align, float size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(420, 48);
            rt.anchoredPosition = pos;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = RetroFont.Bold;
            t.fontSize = size;
            t.alignment = align;
            t.characterSpacing = 6f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.color = new Color(0.97f, 0.97f, 0.94f, 0.92f);
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        static Texture2D Glyph(Func<int, int, bool> inside)
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * 16 + x] = inside(x, y) ? new Color32(248, 248, 240, 235) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        void Update()
        {
            if (!group) return;
            float dt = Time.unscaledDeltaTime;
            bool menu = MainMenuController.Active;
            bool paused = GamePause.IsPaused && !menu;
            group.alpha = Mathf.MoveTowards(group.alpha, GameSettings.TapeOsd ? 1f : 0f, dt * 4f);
            // Con el celular abierto, la fecha (abajo a la derecha) se esconde detrás de él.
            bool phone = Phone.Instance && Phone.Instance.IsOpen;
            dateGroup.alpha = Mathf.MoveTowards(dateGroup.alpha, phone ? 0f : 1f, dt * 6f);

            int newMode = menu ? 0 : paused ? 2 : 1;
            if (newMode != mode)
            {
                if (mode == 0 && newMode == 1) tapeSeconds = 0f;     // empieza la cinta
                mode = newMode;
                stateText.text = mode == 0 ? "STOP" : mode == 2 ? "PAUSE" : "PLAY";
                icon.texture = mode == 0 ? stopTex : mode == 2 ? pauseTex : playTex;
            }
            // PAUSE parpadea; PLAY y STOP quedan fijos.
            float blink = mode == 2 && Mathf.Repeat(Time.unscaledTime, 1f) > 0.6f ? 0f : 1f;
            stateText.alpha = blink;
            icon.color = new Color(1, 1, 1, blink);

            if (mode == 1) tapeSeconds += Time.deltaTime;
            // Los textos solo se arman cuando cambia lo que muestran (cada segundo / minuto / día): antes se creaban
            // 3 strings por frame (315 B de GC por frame medidos en el Profiler, QA-07) y TMP regeneraba la malla.
            int s = Mathf.FloorToInt(tapeSeconds);
            if (s != shownSeconds)
            {
                shownSeconds = s;
                counterText.text = $"SP  {s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
            }

            UpdateClock();
            var now = clockBase.AddSeconds(Time.time - clockSince);
            int minuteKey = now.Hour * 60 + now.Minute;
            if (minuteKey != shownMinute)
            {
                shownMinute = minuteKey;
                int h12 = now.Hour % 12 == 0 ? 12 : now.Hour % 12;
                timeText.text = $"{(now.Hour < 12 ? "AM" : "PM")} {h12,2}:{now.Minute:00}";
            }
            if (now.Day != shownDay)
            {
                shownDay = now.Day;
                dateText.text = $"AUG.{now.Day,2} {now.Year}";
            }
        }

        int shownSeconds = -1, shownMinute = -1, shownDay = -1;

        void UpdateClock()
        {
            if (!director)
            {
                director = FindAnyObjectByType<Level1Director>();
                if (!director) return;
                // Escena nueva (reinicio / volver al menú): la cinta vuelve al principio del relato.
                clockBase = new DateTime(1998, 8, 14, 18, 12, 0);
                clockSince = Time.time;
                lastBeat = null;
            }
            string beat = director.Beat;
            if (beat == lastBeat) return;
            lastBeat = beat;
            if (beat != null && BeatClock.TryGetValue(beat, out var c))
            {
                var target = new DateTime(1998, 8, c.day, c.h, c.m, 0);
                // Nunca hacia atrás (si un momento se repite o llega antes de lo previsto, sigue corriendo).
                var current = clockBase.AddSeconds(Time.time - clockSince);
                if (target > current) { clockBase = target; clockSince = Time.time; }
            }
        }
    }
}
