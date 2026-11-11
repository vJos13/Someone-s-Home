using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Capa de interfaz "de sistema" sobre el HUD (la crea HUD.Awake; no vive en la escena):
    ///  - ESC (o Start del mando) → menú de pausa: Continuar / Configuración / Reiniciar relato / Menú principal.
    ///    En WebGL el navegador consume ESC para soltar el puntero: perder la captura del puntero sin pedirlo
    ///    también abre la pausa.
    ///  - Configuración: cámara, audio y accesibilidad (GameSettings, guardado con PlayerPrefs).
    ///  - "Saltar historia" arriba a la derecha durante la intro (Level1Director.SkipIntro). Clic o Enter.
    ///  - Aviso "Haz clic para mirar" cuando el juego quiere el puntero capturado y el navegador no lo tiene.
    ///  - Pantalla de carga al reiniciar, con fundido de imagen y de audio.
    /// También es dueño del cursor mientras hay UI que lo necesita (FirstPersonController.UIOwnsCursor).
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        /// <summary>Si algún día existe una escena de menú con este nombre en Build Settings, el botón la usa.</summary>
        public const string MainMenuScene = "MenuPrincipal";

        HUD hud;
        TMP_FontAsset font;
        Level1Director director;
        FirstPersonController player;

        Canvas canvas;
        CanvasGroup pauseGroup, settingsGroup, confirmGroup, loadingGroup, skipGroup, hintGroup;
        RectTransform pausePanel, settingsPanel;
        Button continueButton;
        TextMeshProUGUI confirmText, loadingText;
        Action confirmAction;
        readonly List<(Button tab, CanvasGroup page)> tabs = new();
        readonly List<Action> refreshers = new();
        readonly Dictionary<RectTransform, Vector2> homePositions = new();

        enum MenuScreen { None, Pause, Settings, Confirm, Loading }
        MenuScreen screen = MenuScreen.None;
        bool uiOwnsCursor;
        bool wasLocked;
        float sceneTime;
        float hintTimer;

        public static bool IsOpen { get; private set; }

        public void Init(HUD owner)
        {
            hud = owner;
            var anyText = owner.GetComponentInChildren<TMP_Text>(true);
            font = anyText ? anyText.font : TMP_Settings.defaultFontAsset;
            Build();
        }

        void Awake()
        {
            GamePause.ResetState();
            GameSettings.EnsureLoaded();
            gameObject.AddComponent<GameAudio>();
            EnsureEventSystem();
        }

        void Start()
        {
            director = FindAnyObjectByType<Level1Director>();
            player = FindAnyObjectByType<FirstPersonController>();
        }

        void OnDestroy()
        {
            if (GamePause.IsPaused) GamePause.ResetState();
            FirstPersonController.UIOwnsCursor = false;
            IsOpen = false;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            if (!module.actionsAsset) module.AssignDefaultActions();
        }

        // ════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN
        // ════════════════════════════════════════════════════════════════
        void Build()
        {
            gameObject.layer = LayerMask.NameToLayer("UI");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;                    // sobre el HUD (10)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildSkip();
            BuildHint();
            BuildPause();
            BuildSettings();
            BuildConfirm();
            BuildLoading();
        }

        CanvasGroup Group(string name, Transform parent, bool fullScreen = true)
        {
            var rt = MenuWidgets.Rect(name, parent);
            if (fullScreen) MenuWidgets.Stretch(rt);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            Show(g, false, true);
            return g;
        }

        static void Show(CanvasGroup g, bool on, bool instant = false)
        {
            if (instant) g.alpha = on ? 1f : 0f;
            g.interactable = on;
            g.blocksRaycasts = on;
        }

        void BuildSkip()
        {
            skipGroup = Group("SaltarHistoria", transform, false);
            var rt = (RectTransform)skipGroup.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-48f, -40f);
            rt.sizeDelta = new Vector2(330f, 54f);
            var btn = MenuWidgets.Button(rt, font, "SALTAR HISTORIA", SkipStory, 21f, 54f, TextAlignmentOptions.Right);
            MenuWidgets.Stretch((RectTransform)btn.transform);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            label.text = "SALTAR HISTORIA  <size=15><color=#7A7A7A>ENTER</color></size>";
            label.rectTransform.offsetMax = new Vector2(-16f, 0f);
            var line = MenuWidgets.Image("Linea", rt, MenuWidgets.Line);
            line.rectTransform.anchorMin = new Vector2(0.18f, 0f); line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.offsetMin = new Vector2(0f, 0f); line.rectTransform.offsetMax = new Vector2(-16f, 1f);
        }

        void BuildHint()
        {
            hintGroup = Group("AvisoClic", transform, false);
            var rt = (RectTransform)hintGroup.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -90f);
            rt.sizeDelta = new Vector2(600f, 40f);
            var t = MenuWidgets.Text("Texto", rt, font, "HAZ CLIC PARA MIRAR", 20f, MenuWidgets.TextIdle, TextAlignmentOptions.Center, 8f);
            MenuWidgets.Stretch(t.rectTransform);
            hintGroup.blocksRaycasts = false;
        }

        void BuildPause()
        {
            pauseGroup = Group("Pausa", transform);
            var dim = MenuWidgets.Image("Velo", pauseGroup.transform, new Color(0.01f, 0.01f, 0.012f, 0.78f), true);
            MenuWidgets.Stretch(dim.rectTransform);
            // Viñeta lateral: más oscuro a la izquierda, donde está la columna de texto.
            var side = MenuWidgets.Image("Sombra", pauseGroup.transform, new Color(0f, 0f, 0f, 0.45f));
            side.rectTransform.anchorMin = Vector2.zero; side.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            side.rectTransform.offsetMin = side.rectTransform.offsetMax = Vector2.zero;

            pausePanel = MenuWidgets.Rect("Columna", pauseGroup.transform);
            pausePanel.anchorMin = new Vector2(0f, 0.5f); pausePanel.anchorMax = new Vector2(0f, 0.5f);
            pausePanel.pivot = new Vector2(0f, 0.5f);
            pausePanel.anchoredPosition = new Vector2(120f, 0f);
            pausePanel.sizeDelta = new Vector2(620f, 560f);
            var v = pausePanel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;

            var title = MenuWidgets.Text("Titulo", pausePanel, font, "SOMEONE'S HOME", 20f, MenuWidgets.TextDim, TextAlignmentOptions.Left, 14f);
            title.margin = new Vector4(40f, 0, 0, 0);
            MenuWidgets.Layout(title, 34f);
            var sub = MenuWidgets.Text("Pausa", pausePanel, font, "En pausa", 52f, MenuWidgets.TextBright, TextAlignmentOptions.Left, 2f);
            sub.fontStyle = FontStyles.Italic;
            sub.margin = new Vector4(40f, 0, 0, 0);
            MenuWidgets.Layout(sub, 84f);
            var sep = MenuWidgets.Image("Separador", pausePanel, MenuWidgets.Line);
            MenuWidgets.Layout(sep, 1f);
            MenuWidgets.Layout(MenuWidgets.Rect("Aire", pausePanel), 18f);

            continueButton = MenuWidgets.Button(pausePanel, font, "CONTINUAR", Resume);
            MenuWidgets.Button(pausePanel, font, "CONFIGURACIÓN", OpenSettings);
            MenuWidgets.Button(pausePanel, font, "REINICIAR RELATO", () =>
                Confirm("¿Reiniciar el relato?\n<size=22><color=#8A8A8A>Se perderá el progreso de esta partida.</color></size>", RestartStory));
            bool hasMenu = Application.CanStreamedLevelBeLoaded(MainMenuScene);
            var menuBtn = MenuWidgets.Button(pausePanel, font, "VOLVER AL MENÚ PRINCIPAL", () =>
                Confirm("¿Volver al menú principal?\n<size=22><color=#8A8A8A>Se perderá el progreso de esta partida.</color></size>",
                        () => StartCoroutine(LoadScene(MainMenuScene))));
            if (!hasMenu)
            {
                // No hay escena de menú todavía: la opción queda visible pero desactivada (no rompe la navegación).
                menuBtn.interactable = false;
                var label = menuBtn.GetComponentInChildren<TextMeshProUGUI>();
                label.text = "VOLVER AL MENÚ PRINCIPAL  <size=16><color=#5A5A5A>NO DISPONIBLE</color></size>";
            }

            var foot = MenuWidgets.Text("Ayuda", pauseGroup.transform, font,
                "<color=#8A8A8A>ESC</color>  CONTINUAR", 16f, MenuWidgets.TextDim, TextAlignmentOptions.Left, 6f);
            foot.rectTransform.anchorMin = foot.rectTransform.anchorMax = foot.rectTransform.pivot = Vector2.zero;
            foot.rectTransform.anchoredPosition = new Vector2(160f, 60f);
            foot.rectTransform.sizeDelta = new Vector2(500f, 30f);
        }

        void BuildSettings()
        {
            settingsGroup = Group("Configuracion", transform);
            var dim = MenuWidgets.Image("Velo", settingsGroup.transform, new Color(0.01f, 0.01f, 0.012f, 0.86f), true);
            MenuWidgets.Stretch(dim.rectTransform);

            settingsPanel = MenuWidgets.Rect("Panel", settingsGroup.transform);
            settingsPanel.anchorMin = settingsPanel.anchorMax = settingsPanel.pivot = new Vector2(0.5f, 0.5f);
            settingsPanel.sizeDelta = new Vector2(980f, 820f);
            var bg = settingsPanel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.045f, 0.92f);
            var frame = settingsPanel.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(1f, 1f, 1f, 0.08f);
            frame.effectDistance = new Vector2(1f, -1f);

            var title = MenuWidgets.Text("Titulo", settingsPanel, font, "Configuración", 44f, MenuWidgets.TextBright, TextAlignmentOptions.Left, 2f);
            title.fontStyle = FontStyles.Italic;
            title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(60f, -110f); title.rectTransform.offsetMax = new Vector2(-60f, -40f);

            // Pestañas
            var tabRow = MenuWidgets.Rect("Pestanas", settingsPanel);
            tabRow.anchorMin = new Vector2(0f, 1f); tabRow.anchorMax = new Vector2(1f, 1f); tabRow.pivot = new Vector2(0.5f, 1f);
            tabRow.offsetMin = new Vector2(40f, -176f); tabRow.offsetMax = new Vector2(-60f, -122f);
            var h = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.spacing = 10f;
            var sep = MenuWidgets.Image("Separador", settingsPanel, MenuWidgets.Line);
            sep.rectTransform.anchorMin = new Vector2(0f, 1f); sep.rectTransform.anchorMax = new Vector2(1f, 1f);
            sep.rectTransform.offsetMin = new Vector2(60f, -182f); sep.rectTransform.offsetMax = new Vector2(-60f, -181f);

            var pages = MenuWidgets.Rect("Paginas", settingsPanel);
            MenuWidgets.Stretch(pages, 60f, 60f, 206f, 120f);

            var cam = Page(pages, tabRow, "CÁMARA");
            Slider(cam, "Sensibilidad horizontal", 0.2f, 3f, () => GameSettings.SensitivityX, v => GameSettings.SensitivityX = v, Mult);
            Slider(cam, "Sensibilidad vertical", 0.2f, 3f, () => GameSettings.SensitivityY, v => GameSettings.SensitivityY = v, Mult);
            Toggle(cam, "Invertir eje vertical", () => GameSettings.InvertY, v => GameSettings.InvertY = v);
            Slider(cam, "Velocidad de cámara (mando)", 0.3f, 2.5f, () => GameSettings.StickLookSpeed, v => GameSettings.StickLookSpeed = v, Mult);
            Toggle(cam, "Zoom (rueda / pellizco)", () => GameSettings.ZoomEnabled, v => GameSettings.ZoomEnabled = v);

            var audio = Page(pages, tabRow, "AUDIO");
            Slider(audio, "Volumen general", 0f, 1f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Pct);
            Slider(audio, "Música", 0f, 1f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Pct);
            Slider(audio, "Efectos de sonido", 0f, 1f, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Pct);
            Slider(audio, "Ambiente", 0f, 1f, () => GameSettings.AmbienceVolume, v => GameSettings.AmbienceVolume = v, Pct);

            var acc = Page(pages, tabRow, "ACCESIBILIDAD");
            Toggle(acc, "Balanceo de cabeza al caminar", () => GameSettings.HeadBob, v => GameSettings.HeadBob = v);
            Slider(acc, "Intensidad del movimiento de cámara", 0f, 1f, () => GameSettings.CameraMotion, v => GameSettings.CameraMotion = v, Pct);
            Slider(acc, "Velocidad del texto", 0.5f, 2.5f, () => GameSettings.TextSpeed, v => GameSettings.TextSpeed = v, Mult);
            Toggle(acc, "Fondo detrás de los subtítulos", () => GameSettings.SubtitleBackground, v => GameSettings.SubtitleBackground = v);
            var note = MenuWidgets.Text("Nota", acc, font,
                "Los diálogos no tienen voz grabada: los subtítulos son el diálogo y siempre se muestran.",
                18f, MenuWidgets.TextDim, TextAlignmentOptions.Left, 0.5f);
            note.textWrappingMode = TextWrappingModes.Normal;
            MenuWidgets.Layout(note, 60f);

            // Pie: restablecer / volver
            var foot = MenuWidgets.Rect("Pie", settingsPanel);
            foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(0.5f, 0f);
            foot.offsetMin = new Vector2(40f, 36f); foot.offsetMax = new Vector2(-60f, 96f);
            var fh = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            fh.childControlWidth = true; fh.childControlHeight = true; fh.childForceExpandWidth = false; fh.spacing = 12f;
            var reset = MenuWidgets.Button(foot, font, "RESTABLECER", () => { GameSettings.ResetToDefaults(); RefreshSettings(); }, 22f, 56f);
            MenuWidgets.Layout(reset, 56f, 300f);
            MenuWidgets.Layout(MenuWidgets.Rect("Espacio", foot), 56f, -1f, 1f);
            var back = MenuWidgets.Button(foot, font, "VOLVER", CloseSettings, 22f, 56f);
            MenuWidgets.Layout(back, 56f, 220f);

            SelectTab(0);
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
        static string Mult(float v) => v.ToString("0.0") + "×";

        Transform Page(Transform pages, Transform tabRow, string name)
        {
            int index = tabs.Count;
            var tab = MenuWidgets.Button(tabRow, font, name, () => SelectTab(index), 20f, 54f);
            MenuWidgets.Layout(tab, 54f, name.Length * 17f + 90f);
            var page = MenuWidgets.Rect("Pagina_" + name, pages);
            MenuWidgets.Stretch(page);
            var v = page.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
            var g = page.gameObject.AddComponent<CanvasGroup>();
            tabs.Add((tab, g));
            return page;
        }

        void SelectTab(int index)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == index;
                var (tab, page) = tabs[i];
                page.alpha = on ? 1f : 0f;
                page.interactable = page.blocksRaycasts = on;
                var fx = tab.GetComponent<MenuHoverFx>();
                fx.idle = on ? MenuWidgets.TextBright : MenuWidgets.TextIdle;
            }
        }

        void Slider(Transform page, string label, float min, float max, Func<float> get, Action<float> set, Func<float, string> fmt)
        {
            var row = MenuWidgets.Row(page, font, label, out _);
            var s = MenuWidgets.Slider(row, font, min, max, get(), fmt, v => { set(v); GameSettings.NotifyChanged(); });
            refreshers.Add(() => s.SetValueWithoutNotify(get()));
            refreshers.Add(() => row.Find("Valor").GetComponent<TMP_Text>().text = fmt(get()));
        }

        void Toggle(Transform page, string label, Func<bool> get, Action<bool> set)
        {
            var row = MenuWidgets.Row(page, font, label, out _);
            var t = MenuWidgets.Toggle(row, get(), v => { set(v); GameSettings.NotifyChanged(); });
            refreshers.Add(() => t.SetIsOnWithoutNotify(get()));
        }

        void RefreshSettings()
        {
            foreach (var r in refreshers) r();
        }

        void BuildConfirm()
        {
            confirmGroup = Group("Confirmar", transform);
            var dim = MenuWidgets.Image("Velo", confirmGroup.transform, new Color(0f, 0f, 0f, 0.7f), true);
            MenuWidgets.Stretch(dim.rectTransform);
            var box = MenuWidgets.Rect("Caja", confirmGroup.transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(760f, 330f);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(0.035f, 0.035f, 0.04f, 0.96f);
            var o = box.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(1f, 1f, 1f, 0.1f); o.effectDistance = new Vector2(1f, -1f);
            confirmText = MenuWidgets.Text("Texto", box, font, "", 34f, MenuWidgets.TextBright, TextAlignmentOptions.Center, 1f);
            confirmText.textWrappingMode = TextWrappingModes.Normal;
            MenuWidgets.Stretch(confirmText.rectTransform, 50f, 50f, 50f, 120f);
            var row = MenuWidgets.Rect("Botones", box);
            row.anchorMin = new Vector2(0f, 0f); row.anchorMax = new Vector2(1f, 0f); row.pivot = new Vector2(0.5f, 0f);
            row.offsetMin = new Vector2(80f, 40f); row.offsetMax = new Vector2(-80f, 100f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.spacing = 30f;
            MenuWidgets.Button(row, font, "CANCELAR", CloseConfirm, 24f, 58f, TextAlignmentOptions.Center);
            MenuWidgets.Button(row, font, "ACEPTAR", () => { var a = confirmAction; CloseConfirm(); a?.Invoke(); }, 24f, 58f, TextAlignmentOptions.Center);
        }

        void BuildLoading()
        {
            loadingGroup = Group("Carga", transform);
            var black = MenuWidgets.Image("Negro", loadingGroup.transform, Color.black, true);
            MenuWidgets.Stretch(black.rectTransform);
            loadingText = MenuWidgets.Text("Texto", loadingGroup.transform, font, "CARGANDO", 20f, MenuWidgets.TextIdle, TextAlignmentOptions.BottomRight, 10f);
            MenuWidgets.Stretch(loadingText.rectTransform, 0f, 70f, 0f, 60f);
        }

        // ════════════════════════════════════════════════════════════════
        //  FLUJO
        // ════════════════════════════════════════════════════════════════
        void Update()
        {
            sceneTime += Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            bool back = pad != null && pad.buttonEast.wasPressedThisFrame;
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (screen != MenuScreen.Loading)
            {
                if (esc || (back && screen != MenuScreen.None)) Back();
                // WebGL: el navegador suelta el puntero con ESC (y no siempre nos llega la tecla). Si el juego lo
                // tenía capturado y lo perdió sin pedirlo, es lo mismo que pulsar ESC.
                else if (screen == MenuScreen.None && wasLocked && !locked && !uiOwnsCursor
                         && FirstPersonController.RequestedCursorLocked && Application.isFocused)
                    OpenPause();
            }

            UpdateSkip(kb, pad);
            UpdateCursorOwnership();
            UpdateHint(locked);
            wasLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        void Back()
        {
            switch (screen)
            {
                case MenuScreen.None: OpenPause(); break;
                case MenuScreen.Pause: Resume(); break;
                case MenuScreen.Settings: CloseSettings(); break;
                case MenuScreen.Confirm: CloseConfirm(); break;
            }
        }

        void OpenPause()
        {
            if (screen != MenuScreen.None) return;
            GamePause.Set(true);
            screen = MenuScreen.Pause;
            IsOpen = true;
            StartCoroutine(Transition(pauseGroup, pausePanel, true, new Vector2(-24f, 0f)));
            Select(continueButton);
        }

        void Resume()
        {
            if (screen != MenuScreen.Pause) return;
            screen = MenuScreen.None;
            IsOpen = false;
            GameSettings.Save();
            StartCoroutine(Transition(pauseGroup, pausePanel, false, new Vector2(-24f, 0f)));
            EventSystem.current?.SetSelectedGameObject(null);
            GamePause.Set(false);
        }

        void OpenSettings()
        {
            screen = MenuScreen.Settings;
            RefreshSettings();
            StartCoroutine(Transition(pauseGroup, pausePanel, false, new Vector2(-24f, 0f)));
            StartCoroutine(Transition(settingsGroup, settingsPanel, true, new Vector2(0f, -16f)));
            SelectTab(0);
            Select(tabs[0].tab);
        }

        void CloseSettings()
        {
            GameSettings.Save();
            screen = MenuScreen.Pause;
            StartCoroutine(Transition(settingsGroup, settingsPanel, false, new Vector2(0f, -16f)));
            StartCoroutine(Transition(pauseGroup, pausePanel, true, new Vector2(-24f, 0f)));
            Select(continueButton);
        }

        MenuScreen beforeConfirm;
        void Confirm(string text, Action onAccept)
        {
            beforeConfirm = screen;
            screen = MenuScreen.Confirm;
            confirmText.text = text;
            confirmAction = onAccept;
            StartCoroutine(Transition(confirmGroup, null, true, Vector2.zero));
            Select(confirmGroup.GetComponentInChildren<Button>());   // CANCELAR por defecto
        }

        void CloseConfirm()
        {
            screen = beforeConfirm;
            confirmAction = null;
            StartCoroutine(Transition(confirmGroup, null, false, Vector2.zero));
            if (screen == MenuScreen.Pause) Select(continueButton);
        }

        void RestartStory() => StartCoroutine(LoadScene(SceneManager.GetActiveScene().name));

        IEnumerator LoadScene(string sceneName)
        {
            screen = MenuScreen.Loading;
            loadingText.text = "CARGANDO";
            Show(loadingGroup, true);
            var audio = GameAudio.Instance;
            if (audio) audio.FadeTo(0f, 0.7f);
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                loadingGroup.alpha = Mathf.SmoothStep(0f, 1f, t / 0.7f);
                yield return null;
            }
            loadingGroup.alpha = 1f;
            Show(pauseGroup, false, true);
            Show(settingsGroup, false, true);
            GamePause.ResetState();
            var op = SceneManager.LoadSceneAsync(sceneName);
            int dots = 0;
            while (op != null && !op.isDone)
            {
                loadingText.text = "CARGANDO" + new string('.', 1 + (dots++ / 20) % 3);
                yield return null;
            }
        }

        IEnumerator Transition(CanvasGroup g, RectTransform panel, bool show, Vector2 offset)
        {
            Show(g, show);
            float from = g.alpha, to = show ? 1f : 0f;
            Vector2 basePos = Vector2.zero;
            if (panel && !homePositions.TryGetValue(panel, out basePos)) homePositions[panel] = basePos = panel.anchoredPosition;
            const float time = 0.22f;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                g.alpha = Mathf.Lerp(from, to, k);
                if (panel) panel.anchoredPosition = basePos + offset * (show ? 1f - k : k);
                yield return null;
            }
            g.alpha = to;
            if (panel) panel.anchoredPosition = basePos;
        }

        static void Select(Selectable s)
        {
            if (!s || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(null);
            // Con ratón no se marca nada (solo hover); con mando/teclado sí, para poder navegar.
            if (Gamepad.current != null || Cursor.lockState == CursorLockMode.Locked)
                EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        // ── Saltar historia ──
        bool SkipAvailable => director && director.CanSkipIntro && !director.IntroSkipped;

        void UpdateSkip(Keyboard kb, Gamepad pad)
        {
            bool visible = SkipAvailable && screen == MenuScreen.None && sceneTime > 1.2f;
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * (visible ? 1.5f : 4f));
            skipGroup.interactable = skipGroup.blocksRaycasts = visible;
            if (visible && ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                            || (pad != null && pad.selectButton.wasPressedThisFrame)))
                SkipStory();
        }

        void SkipStory()
        {
            if (!SkipAvailable) return;
            director.SkipIntro();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        // ── Cursor ──
        void UpdateCursorOwnership()
        {
            // Durante las tarjetas de la intro (sin poder mirar) el cursor queda libre para el botón Saltar.
            bool want = screen != MenuScreen.None || (SkipAvailable && player && player.LookLocked && sceneTime > 1.2f);
            if (want == uiOwnsCursor) return;
            uiOwnsCursor = want;
            FirstPersonController.UIOwnsCursor = want;
            if (want) FirstPersonController.ApplyCursor(false);
            else if (FirstPersonController.RequestedCursorLocked) FirstPersonController.ApplyCursor(true);
            wasLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        void UpdateHint(bool locked)
        {
            // Solo si el jugador podría estar mirando y el navegador no le dio el puntero.
            bool need = screen == MenuScreen.None && !uiOwnsCursor && !locked && FirstPersonController.RequestedCursorLocked
                        && player && !player.LookLocked && Mouse.current != null;
            hintTimer = need ? hintTimer + Time.unscaledDeltaTime : 0f;
            bool show = hintTimer > 0.6f;
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, show ? 0.85f : 0f, Time.unscaledDeltaTime * 3f);
        }
    }
}
