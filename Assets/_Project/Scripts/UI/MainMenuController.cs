using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Menú principal en tiempo real, dentro de la misma escena del relato: la cámara pasea lenta junto al columpio
    /// del río (que se mece solo), con la niebla del atardecer, luciérnagas y polvo en el aire. Sin pantalla de carga:
    /// JUGAR funde a negro con una ráfaga de viento y el relato empieza ahí mismo (la intro ya arranca en negro).
    ///
    /// · Se instala solo al cargar una escena con <see cref="Level1Director"/> si <see cref="ShowOnNextLoad"/> está activo
    ///   (al abrir el juego y con "Volver al menú principal"). "Reiniciar relato" y [R] lo saltan.
    /// · Mientras <see cref="Active"/>, el director espera y ni la pausa (ESC), ni el inventario ni el celular responden.
    /// · OPCIONES abre la Configuración del juego (GameMenu) sin la pausa detrás: cámara, audio, brillo, gráficos…
    /// · Se salta con "-skipMenu" o "-benchmark" en la línea de comandos (mediciones y pruebas automáticas).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static MainMenuController Instance { get; private set; }
        /// <summary>Mostrar el menú la próxima vez que cargue el relato (se consume al cargar).</summary>
        public static bool ShowOnNextLoad = true;

        const float OrbitRadius = 6.6f, OrbitHeight = 1.55f;
        const float OrbitCenterDeg = 182f, OrbitSwingDeg = 32f, OrbitPeriod = 80f;

        HUD hud;
        GameMenu gameMenu;
        CinemachineBrain brain;
        CinemachineBlendDefinition originalBlend;
        CinemachineCamera vcam;
        Transform swingBase;
        Vector3 center;
        float t0;
        Canvas canvas;
        CanvasGroup group;
        RectTransform column;
        Button playButton, optionsButton;
        bool starting;
        readonly System.Collections.Generic.List<GameObject> spawned = new();

        // ════════════════════════ INSTALACIÓN ════════════════════════
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            Active = false;
            var args = System.Environment.GetCommandLineArgs();
            ShowOnNextLoad = System.Array.IndexOf(args, "-skipMenu") < 0 && System.Array.IndexOf(args, "-benchmark") < 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // sceneLoaded llega después de Awake/OnEnable y antes de los Start: el director ya ve Active al arrancar.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Install();
        }

        // En el Editor con "Enter Play Mode Options" sin recarga de escena, sceneLoaded no llega para la escena
        // inicial: este enganche (después de Awake, antes de Start) cubre ese caso. Install() ignora la segunda llamada.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstScene() => Install();

        static void Install()
        {
            if (Instance) return;
            bool want = ShowOnNextLoad;
            ShowOnNextLoad = false;
            Active = false;
            if (!want || !FindAnyObjectByType<Level1Director>()) return;
            Active = true;
            new GameObject("MenuPrincipal").AddComponent<MainMenuController>();
        }

        /// <summary>Pruebas automáticas: entra al relato sin animación.</summary>
        public static void SkipNow()
        {
            if (Instance) Instance.StartCoroutine(Instance.Play(true));
            else Active = false;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Active = false; }
            foreach (var go in spawned) if (go) Destroy(go);
        }

        IEnumerator Start()
        {
            hud = HUD.Instance;
            gameMenu = FindAnyObjectByType<GameMenu>();
            var director = FindAnyObjectByType<Level1Director>();
            swingBase = director && director.SwingSeat ? (director.SwingSeat.parent ? director.SwingSeat.parent : director.SwingSeat) : null;
            center = swingBase ? swingBase.position : Vector3.zero;
            FirstPersonController.LockCursor(false);

            BuildCamera();
            BuildParticles();
            BuildUI();
            if (hud) hud.SetFadeInstant(1f);
            t0 = Time.time;
            UpdateCamera(0f);

            // Revelado: de negro a la escena, después el texto.
            yield return null;
            if (hud) StartCoroutine(hud.Fade(0f, 2.6f));
            yield return new WaitForSeconds(1.2f);
            for (float t = 0f; t < 1f && !starting; t += Time.unscaledDeltaTime / 1.4f) { group.alpha = Mathf.SmoothStep(0f, 1f, t); yield return null; }
            if (!starting) { group.alpha = 1f; group.interactable = group.blocksRaycasts = true; SelectDefault(); }
        }

        // ════════════════════════ CÁMARA ════════════════════════
        void BuildCamera()
        {
            brain = FindAnyObjectByType<CinemachineBrain>();
            if (brain)
            {
                // Corte seco al menú: sin esto, la cámara "volaría" desde la casa hasta el río.
                originalBlend = brain.DefaultBlend;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }
            var go = new GameObject("VCam_MenuPrincipal");
            spawned.Add(go);
            vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = 500;
            var lens = vcam.Lens;
            lens.FieldOfView = 46f;
            lens.NearClipPlane = 0.1f;
            vcam.Lens = lens;
        }

        void Update()
        {
            if (vcam) UpdateCamera(Time.time - t0);
            // Con teclado o mando, la primera flecha marca JUGAR (con ratón no se marca nada, solo el hover).
            if (starting || !group || !group.interactable || !EventSystem.current || EventSystem.current.currentSelectedGameObject) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame || pad.leftStick.ReadValue().sqrMagnitude > 0.25f)))
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        /// <summary>Arco lento detrás del columpio, mirando hacia el río y el bosque de enfrente.</summary>
        void UpdateCamera(float t)
        {
            float k = Mathf.Sin(t * Mathf.PI * 2f / OrbitPeriod);
            float ang = (OrbitCenterDeg + k * OrbitSwingDeg) * Mathf.Deg2Rad;
            float r = OrbitRadius + Mathf.Sin(t * 0.09f) * 0.5f;
            Vector3 pos = center + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r;
            var terrain = Terrain.activeTerrain;
            float ground = terrain ? terrain.SampleHeight(pos) + terrain.transform.position.y : center.y;
            pos.y = Mathf.Max(center.y, ground) + OrbitHeight + Mathf.Sin(t * 0.21f) * 0.12f;
            // Mira un poco más allá del columpio: el río y la orilla de enfrente quedan en cuadro.
            Vector3 look = center + new Vector3(0f, 1.1f, 3.2f) + new Vector3(Mathf.Sin(t * 0.13f) * 0.4f, Mathf.Sin(t * 0.17f) * 0.15f, 0f);
            vcam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.11f) * 0.8f));
        }

        // ════════════════════════ PARTÍCULAS ════════════════════════
        void BuildParticles()
        {
            var shader = Resources.Load<Shader>("VFX/SoftGlow");
            if (!shader || !shader.isSupported) return;
            spawned.Add(Particles("Menu_Luciernagas", shader, new Color(0.95f, 0.85f, 0.4f), 3.2f, 7.5f, center + new Vector3(0f, 1.2f, 1.5f),
                                  new Vector3(16f, 2.4f, 12f), 22, new Vector2(0.05f, 0.09f), new Vector2(5f, 9f), 0.35f, true));
            spawned.Add(Particles("Menu_Polvo", shader, new Color(0.85f, 0.82f, 0.75f), 0.9f, 2f, center + new Vector3(0f, 1.4f, -3.5f),
                                  new Vector3(9f, 3f, 7f), 60, new Vector2(0.012f, 0.025f), new Vector2(8f, 14f), 0.08f, false));
        }

        static GameObject Particles(string name, Shader shader, Color color, float intensity, float core, Vector3 pos, Vector3 box,
                                    float rate, Vector2 size, Vector2 life, float wander, bool blink)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = wander;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            if (blink)
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.1f, 0.35f),
                                  new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0.05f, 0.75f), new GradientAlphaKey(0.8f, 0.88f),
                                  new GradientAlphaKey(0f, 1f) });
            else
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(shader) { name = name + " (runtime)" };
            mat.SetColor("_Color", color);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_Core", core);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return go;
        }

        // ════════════════════════ INTERFAZ ════════════════════════
        void BuildUI()
        {
            var font = InspectModels.Font;
            var go = new GameObject("MenuPrincipal_UI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer("UI");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;                       // sobre el HUD (10) y bajo Configuración (100)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;

            // Sombra a la izquierda para leer el texto sobre la escena: degradado horizontal suave.
            var shade = new GameObject("Sombra", typeof(RectTransform)).AddComponent<RawImage>();
            shade.transform.SetParent(go.transform, false);
            shade.raycastTarget = false;
            shade.texture = HorizontalFade(0.78f);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            var low = new GameObject("SombraPie", typeof(RectTransform)).AddComponent<RawImage>();
            low.transform.SetParent(go.transform, false);
            low.raycastTarget = false;
            low.rectTransform.anchorMin = Vector2.zero;
            low.rectTransform.anchorMax = new Vector2(1f, 0.16f);
            low.rectTransform.offsetMin = low.rectTransform.offsetMax = Vector2.zero;
            low.texture = VerticalFade(0.55f);

            column = MenuWidgets.Rect("Columna", go.transform);
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0f, 0.5f);
            column.anchoredPosition = new Vector2(120f, -10f);
            column.sizeDelta = new Vector2(760f, 640f);
            var v = column.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;

            // Estilo de cinta de 1998: tipografía monoespaciada, como la etiqueta y el OSD del VCR.
            var mono = RetroFont.Bold;
            var label = MenuWidgets.Text("Etiqueta", column, mono, "VHS · SP · T-120     CINTA 1 DE 1", 17f, new Color(0.62f, 0.6f, 0.56f), TextAlignmentOptions.Left, 4f);
            label.margin = new Vector4(44f, 0, 0, 0);
            MenuWidgets.Layout(label, 30f);
            var title = MenuWidgets.Text("Titulo", column, mono, "SOMEONE'S HOME", 66f, MenuWidgets.TextBright, TextAlignmentOptions.Left, 10f);
            title.margin = new Vector4(40f, 0, 0, 0);
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
            MenuWidgets.Layout(title, 92f);
            var sub = MenuWidgets.Text("Subtitulo", column, mono, "RELATO 1 · ACROSS THE RIVER", 26f, new Color(0.78f, 0.76f, 0.72f), TextAlignmentOptions.Left, 6f);
            sub.margin = new Vector4(44f, 0, 0, 0);
            MenuWidgets.Layout(sub, 46f);
            MenuWidgets.Layout(MenuWidgets.Rect("Aire", column), 22f);
            var sep = MenuWidgets.Image("Separador", column, MenuWidgets.Line);
            MenuWidgets.Layout(sep, 1f, 460f);
            MenuWidgets.Layout(MenuWidgets.Rect("Aire2", column), 22f);

            playButton = MenuWidgets.Button(column, mono, "JUGAR", () => StartCoroutine(Play(false)));
            optionsButton = MenuWidgets.Button(column, mono, "OPCIONES", OpenOptions);
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                MenuWidgets.Button(column, mono, "SALIR", Quit);
            foreach (var b in column.GetComponentsInChildren<Button>()) MenuWidgets.Layout(b, 58f, 460f);

            var foot = MenuWidgets.Text("Pie", go.transform, mono,
                "<color=#8A8A8A>BASADO EN HECHOS REALES.</color>   WENATCHEE, WASHINGTON · AGOSTO DE 1998", 15f, MenuWidgets.TextDim, TextAlignmentOptions.Left, 6f);
            foot.rectTransform.anchorMin = foot.rectTransform.anchorMax = foot.rectTransform.pivot = Vector2.zero;
            foot.rectTransform.anchoredPosition = new Vector2(160f, 60f);
            foot.rectTransform.sizeDelta = new Vector2(900f, 30f);
            var hint = MenuWidgets.Text("Consejo", go.transform, mono, "JUEGA CON AUDIFONOS.", 15f, MenuWidgets.TextDim, TextAlignmentOptions.Right, 6f);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(-80f, 60f);
            hint.rectTransform.sizeDelta = new Vector2(600f, 30f);
        }

        /// <summary>Negro que se desvanece hacia la derecha (curva suave, sin bandas).</summary>
        static Texture2D HorizontalFade(float maxAlpha)
        {
            var t = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_Degradado (runtime)" };
            for (int x = 0; x < 256; x++)
            {
                float k = 1f - x / 255f;
                t.SetPixel(x, 0, new Color(0f, 0f, 0f, maxAlpha * k * k * (3f - 2f * k)));
            }
            t.Apply();
            return t;
        }

        /// <summary>Negro que se desvanece hacia arriba (para el pie de pantalla).</summary>
        static Texture2D VerticalFade(float maxAlpha)
        {
            var t = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_DegradadoV (runtime)" };
            for (int y = 0; y < 128; y++)
            {
                float k = 1f - y / 127f;
                t.SetPixel(0, y, new Color(0f, 0f, 0f, maxAlpha * k * k));
            }
            t.Apply();
            return t;
        }

        void SelectDefault()
        {
            if (!EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(null);
            if (Gamepad.current != null) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        void OpenOptions()
        {
            if (starting || !gameMenu) return;
            StartCoroutine(FadeUI(0f, 0.2f));
            gameMenu.OpenSettingsStandalone(() =>
            {
                StartCoroutine(FadeUI(1f, 0.25f));
                if (EventSystem.current && Gamepad.current != null) EventSystem.current.SetSelectedGameObject(optionsButton.gameObject);
            });
        }

        IEnumerator FadeUI(float to, float time)
        {
            group.interactable = group.blocksRaycasts = to > 0.5f;
            float from = group.alpha;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime) { group.alpha = Mathf.Lerp(from, to, t / time); yield return null; }
            group.alpha = to;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ════════════════════════ JUGAR ════════════════════════
        IEnumerator Play(bool instant)
        {
            if (starting) yield break;
            starting = true;
            group.interactable = group.blocksRaycasts = false;
            EventSystem.current?.SetSelectedGameObject(null);
            if (!instant)
            {
                // La cinta entra a la casetera: golpe mecánico + salto de tracking, y una ráfaga de viento.
                OneShotAudio.Play(ProceduralAudio.Build(ProceduralAudio.Kind.Knock, 0.18f, 77), Camera.main ? Camera.main.transform.position : Vector3.zero, 0.6f);
                VhsLook.Pulse(1f, 1.4f);
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = ProceduralAudio.Build(ProceduralAudio.Kind.Wind, 4f, 11);
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.Play();
                StartCoroutine(FadeUI(0f, 0.6f));
                if (hud) StartCoroutine(hud.Fade(1f, 1.8f));
                for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime)
                {
                    src.volume = Mathf.Sin(Mathf.Clamp01(t / 2.2f) * Mathf.PI) * 0.55f * GameSettings.MasterVolume * GameSettings.AmbienceVolume;
                    yield return null;
                }
            }
            if (hud) hud.SetFadeInstant(1f);
            // Corte seco a la cámara del jugador (está en negro) y vuelve la mezcla original para el resto del juego.
            if (vcam) Destroy(vcam.gameObject);
            yield return null;
            yield return null;
            if (brain) brain.DefaultBlend = originalBlend;
            FirstPersonController.LockCursor(true);
            Active = false;
            Destroy(gameObject);
        }
    }
}
