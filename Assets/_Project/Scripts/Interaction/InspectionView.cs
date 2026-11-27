using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    /// <summary>
    /// Inspección de pistas: el objeto aparece delante de la cámara, sobre la escena oscurecida, y se gira con el
    /// ratón (o el stick derecho). La rueda acerca/aleja. [E], clic derecho o B del mando lo dejan.
    ///
    /// Uso desde el relato:
    ///     InspectionView.Open(InspectModels.Photo(), "Foto vieja");
    ///     yield return hud.SayAndWait(...);          // las frases corren mientras se mira
    ///     yield return InspectionView.WaitClosed();
    ///
    /// Se dibuja en la capa "en mano" (<see cref="ViewModelLayer"/>), así que nunca atraviesa paredes.
    /// Mientras está abierta, el jugador no camina ni gira la cámara (se restaura lo que había al cerrar).
    /// </summary>
    public class InspectionView : MonoBehaviour
    {
        const float Distance = 0.42f;
        const float MinDistance = 0.26f, MaxDistance = 0.7f;
        const float RotateSpeed = 0.28f;       // grados por píxel de ratón
        const float StickSpeed = 160f;         // grados por segundo con el stick
        const float MinOpenTime = 0.35f;       // la misma [E] que abrió no la cierra

        static InspectionView instance;

        /// <summary>Hay un objeto en inspección.</summary>
        public static bool IsOpen => instance && instance.model;
        /// <summary>Pruebas automáticas: si es mayor que 0, la inspección se cierra sola a los N segundos.</summary>
        public static float AutoCloseAfter = -1f;

        // Sin recarga de dominio al entrar en Play, los estáticos sobreviven entre sesiones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { AutoCloseAfter = -1f; instance = null; }

        Transform pivot;            // gira con el ratón
        GameObject model;
        GameObject backdrop;
        Material backdropMat;
        CanvasGroup hintGroup;
        TMP_Text titleText;
        float openedAt;
        float distance = Distance, targetDistance = Distance;
        float appear;               // 0 → 1 al abrir, 1 → 0 al cerrar
        bool closing;
        bool prevInput, prevLook;
        FirstPersonController player;

        // ════════════════════════ API ════════════════════════
        /// <param name="newModel">Modelo ya armado (InspectModels). Se destruye al cerrar.</param>
        /// <param name="title">Nombre que se muestra arriba ("Foto vieja").</param>
        /// <param name="startEuler">Giro inicial (para mostrar primero el lado interesante).</param>
        /// <param name="size">Tamaño del lado más largo, en metros, a la distancia de inspección.</param>
        public static void Open(GameObject newModel, string title, Vector3 startEuler = default, float size = 0.27f)
        {
            if (!newModel) return;
            if (!instance) instance = new GameObject("InspectionView").AddComponent<InspectionView>();
            instance.Show(newModel, title, startEuler, size);
        }

        public static void Close()
        {
            if (instance) instance.closing = true;
        }

        public static IEnumerator WaitClosed()
        {
            while (IsOpen) yield return null;
        }

        // ════════════════════════ ABRIR / CERRAR ════════════════════════
        void Show(GameObject newModel, string title, Vector3 startEuler, float size)
        {
            if (model) Destroy(model);
            var cam = ViewModelLayer.Ensure();
            if (!cam) { Destroy(newModel); return; }
            player = FindAnyObjectByType<FirstPersonController>();

            if (!pivot)
            {
                pivot = new GameObject("Inspeccion_Pivote").transform;
                pivot.SetParent(cam.transform, false);
                BuildBackdrop(cam.transform);
                BuildHint();
            }
            pivot.localPosition = new Vector3(0f, 0f, Distance);
            pivot.localRotation = Quaternion.Euler(startEuler);
            distance = targetDistance = Distance;

            model = newModel;
            ViewModelLayer.Apply(model, sceneLit: false);
            Fit(model.transform, size);

            titleText.text = title ?? "";
            ViewModelLayer.SetFill(3.2f);

            if (player && !closing && appear <= 0f)
            {
                prevInput = player.InputLocked;
                prevLook = player.LookLocked;
            }
            if (player) { player.InputLocked = true; player.LookLocked = true; }
            closing = false;
            openedAt = Time.unscaledTime;
            backdrop.SetActive(true);
        }

        /// <summary>Centra el modelo en el pivote y lo escala para que su lado más largo mida 'size'.</summary>
        void Fit(Transform m, float size)
        {
            m.SetParent(pivot, false);
            m.localPosition = Vector3.zero;
            m.localRotation = Quaternion.identity;
            m.localScale = Vector3.one;
            var rends = m.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            // Límites en el espacio del pivote (independiente de hacia dónde mira la cámara).
            var inv = pivot.worldToLocalMatrix;
            bool first = true;
            var b = new Bounds();
            foreach (var r in rends)
            {
                if (r is ParticleSystemRenderer) continue;
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = inv.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float k = longest > 1e-4f ? size / longest : 1f;
            m.localScale = Vector3.one * k;
            m.localPosition = -b.center * k;
        }

        void Finish()
        {
            if (model) Destroy(model);
            model = null;
            closing = false;
            appear = 0f;
            if (backdrop) backdrop.SetActive(false);
            if (hintGroup) hintGroup.alpha = 0f;
            ViewModelLayer.SetFill(0.25f);
            if (player) { player.InputLocked = prevInput; player.LookLocked = prevLook; }
        }

        // ════════════════════════ UPDATE ════════════════════════
        void Update()
        {
            if (!model) return;
            float dt = Time.unscaledDeltaTime;
            if (!GamePause.IsPaused) HandleInput(dt);

            appear = Mathf.MoveTowards(appear, closing ? 0f : 1f, dt / (closing ? 0.18f : 0.28f));
            float e = Mathf.SmoothStep(0f, 1f, appear);
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-dt * 10f));
            pivot.localPosition = new Vector3(0f, Mathf.Lerp(-0.12f, 0f, e), distance);
            pivot.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, e);
            if (backdropMat) backdropMat.SetColor("_Color", new Color(0f, 0f, 0f, 0.84f * e));
            if (hintGroup) hintGroup.alpha = e;

            if (closing && appear <= 0f) Finish();
            // El relato puede bloquear al jugador por su cuenta: mientras se mira, sigue bloqueado.
            else if (player && !closing) { player.InputLocked = true; player.LookLocked = true; }
        }

        void HandleInput(float dt)
        {
            if (closing) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var pad = GamepadGuard.Current;
            bool canClose = Time.unscaledTime - openedAt > MinOpenTime;

            Vector2 rot = Vector2.zero;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked) rot += mouse.delta.ReadValue() * RotateSpeed;
            else if (mouse != null && mouse.leftButton.isPressed) rot += mouse.delta.ReadValue() * RotateSpeed;   // cursor libre: arrastrar
            if (pad != null) rot += pad.rightStick.ReadValue() * (StickSpeed * dt);
            if (rot.sqrMagnitude > 0f)
            {
                var cam = pivot.parent;
                pivot.Rotate(cam.up, -rot.x, Space.World);
                pivot.Rotate(cam.right, rot.y, Space.World);
            }

            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 20f) s /= 120f;
                targetDistance = Mathf.Clamp(targetDistance - s * 0.04f, MinDistance, MaxDistance);
            }
            if (pad != null) targetDistance = Mathf.Clamp(targetDistance - pad.leftStick.ReadValue().y * dt * 0.3f, MinDistance, MaxDistance);

            bool close = canClose && ((kb != null && (kb.eKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                                      || (mouse != null && mouse.rightButton.wasPressedThisFrame)
                                      || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame)));
            if (AutoCloseAfter > 0f && Time.unscaledTime - openedAt > AutoCloseAfter) close = true;
            if (close) closing = true;
        }

        // ════════════════════════ CONSTRUCCIÓN ════════════════════════
        void BuildBackdrop(Transform cam)
        {
            backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Inspeccion_Velo";
            Destroy(backdrop.GetComponent<Collider>());
            backdrop.transform.SetParent(cam, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            backdrop.transform.localScale = new Vector3(6f, 4f, 1f);     // cubre la vista a esa distancia con cualquier FOV razonable
            var shader = Resources.Load<Shader>("UI/InspectBackdrop");
            backdropMat = new Material(shader) { name = "M_VeloInspeccion (runtime)" };
            backdrop.GetComponent<Renderer>().sharedMaterial = backdropMat;
            ViewModelLayer.Apply(backdrop, sceneLit: false);
            backdrop.SetActive(false);
        }

        void BuildHint()
        {
            var hud = HUD.Instance;
            var canvas = hud ? hud.GetComponentInParent<Canvas>() : null;
            if (!canvas) return;
            var font = InspectModels.Font;
            var root = MenuWidgets.Rect("Inspeccion_Ayuda", canvas.transform);
            MenuWidgets.Stretch(root);
            hintGroup = root.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintGroup.blocksRaycasts = false;
            hintGroup.interactable = false;

            titleText = MenuWidgets.Text("Titulo", root, font, "", 30f, MenuWidgets.TextBright, TextAlignmentOptions.Center, 4f);
            titleText.fontStyle = FontStyles.Italic;
            var t = titleText.rectTransform;
            t.anchorMin = new Vector2(0f, 1f); t.anchorMax = new Vector2(1f, 1f); t.pivot = new Vector2(0.5f, 1f);
            t.offsetMin = new Vector2(0f, -150f); t.offsetMax = new Vector2(0f, -100f);

            var help = MenuWidgets.Text("Controles", root, font,
                "<mark=#FFFFFF26 padding=\"10,10,4,4\"><b> E </b></mark>  DEJAR        ARRASTRAR  GIRAR        RUEDA  ACERCAR",
                17f, MenuWidgets.TextIdle, TextAlignmentOptions.Center, 3f);
            var h = help.rectTransform;
            h.anchorMin = new Vector2(0f, 1f); h.anchorMax = new Vector2(1f, 1f); h.pivot = new Vector2(0.5f, 1f);
            h.offsetMin = new Vector2(0f, -196f); h.offsetMax = new Vector2(0f, -158f);
        }

        void OnDestroy()
        {
            if (model) Finish();
            if (backdropMat) Destroy(backdropMat);
            if (instance == this) instance = null;
        }
    }
}
