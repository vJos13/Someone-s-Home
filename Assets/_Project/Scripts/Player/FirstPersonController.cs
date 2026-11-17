using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    /// <summary>
    /// Controlador en primera persona con CharacterController (gravedad manual, como en la semana 9).
    /// - Movimiento lento y pesado (terror, no acción), con aceleración/frenado suaves en espacio local
    ///   (girar la cámara no "arrastra" la inercia) y diagonal normalizada.
    /// - Correr: Shift (o clic del stick) o doble toque de W (aceleración moderada mientras se mantenga W).
    /// - En pendientes más empinadas que slopeLimit el jugador resbala: la colina solo se baja por las rocas.
    /// - Cámara: sensibilidad/inversión desde GameSettings, filtro de saltos del puntero (WebGL), zoom con
    ///   rueda / pellizco (touchpad del navegador o pantalla táctil) sobre el lente de VCam_FPS.
    /// - Head bob sutil con envolvente (entra y sale suave) y pasos (si hay clips asignados).
    /// - Vista ISOMÉTRICA (estilo ARPG, por defecto): cursor libre, cámara IsometricCamera, movimiento por clic
    ///   (ClickToMove → NavigationInput) o WASD relativo a la cámara, el cuerpo gira hacia donde camina y la
    ///   cabeza (Head: linterna, Interactor, mecánicas de mirada) apunta al cursor (AimPoint). [V] alterna vistas.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public enum ViewMode { FirstPerson, Isometric }

        [Header("Vista")]
        [SerializeField] ViewMode viewMode = ViewMode.Isometric;
        [Tooltip("Permite alternar primera persona / isométrica en juego.")]
        [SerializeField] bool allowViewToggle = true;
        [SerializeField] Key toggleViewKey = Key.V;
        [Tooltip("Cámara isométrica. Si está vacío se busca en la escena.")]
        [SerializeField] IsometricCamera isoCamera;

        [Header("Isométrica")]
        [Tooltip("Rapidez con que el cuerpo gira hacia la dirección de movimiento.")]
        [SerializeField] float turnSharpness = 13f;
        [Tooltip("Rapidez con que la cabeza (linterna, mirada) sigue al cursor.")]
        [SerializeField] float headAimSharpness = 10f;
        [Tooltip("Inclinación máxima de la mirada hacia arriba/abajo al apuntar con el cursor.")]
        [SerializeField] float aimPitchLimit = 60f;

        [Header("Movimiento")]
        [SerializeField] float walkSpeed = 2.6f;
        [SerializeField] float runSpeed = 4.4f;
        [Tooltip("Velocidad con doble toque de W (entre caminar y Shift: aceleración moderada).")]
        [SerializeField] float tapSprintSpeed = 3.7f;
        [Tooltip("Ventana para el doble toque de W (segundos).")]
        [SerializeField] float doubleTapWindow = 0.28f;
        [Tooltip("m/s² al arrancar / al frenar.")]
        [SerializeField] float acceleration = 11f;
        [SerializeField] float deceleration = 14f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float slideSpeed = 7f;

        [Header("Cámara")]
        [SerializeField] Transform head;
        [SerializeField] float mouseSensitivity = 0.08f;
        [SerializeField] float gamepadSensitivity = 120f;
        [SerializeField] float pitchLimit = 80f;
        [Tooltip("Saltos del puntero mayores a esto (px en un frame) se descartan: pasa al capturar el puntero en WebGL.")]
        [SerializeField] float maxMouseDelta = 250f;

        [Header("Zoom (FOV de VCam_FPS)")]
        [SerializeField] float minFov = 42f;
        [Tooltip("Cuánto se puede abrir por encima del FOV base.")]
        [SerializeField] float maxFovAboveBase = 6f;
        [SerializeField] float fovPerScrollStep = 3.5f;
        [SerializeField] float zoomSmoothTime = 0.12f;

        [Header("Head bob")]
        [SerializeField] float bobFrequency = 1.8f;
        [SerializeField] float bobAmplitude = 0.035f;

        [Header("Pasos")]
        [SerializeField] AudioSource footstepSource;
        [SerializeField] AudioClip[] footstepClips;
        [SerializeField] float stepDistance = 1.7f;

        CharacterController controller;
        CinemachineCamera fpsCam;
        Vector3 velocity;            // solo componente vertical (gravedad)
        Vector2 localPlanar;         // velocidad horizontal suavizada, en ejes del jugador (x = derecha, y = adelante)
        Vector3 isoPlanar;           // vista isométrica: velocidad horizontal suavizada en ejes del mundo
        float pitch;
        float bobTimer, bobWeight;
        Vector3 headBase;
        float stepAccumulator;
        Vector3 groundNormal = Vector3.up;
        bool hasGroundHit;
        bool wasLookLocked;
        int suppressLookFrames;
        bool wasCursorLocked;

        // Doble toque de W
        float lastForwardPress = -10f;
        bool lastPressWasTap;        // la pulsación anterior se soltó dentro de la ventana
        bool tapSprint;

        // Zoom
        float baseFov, zoomOffset, currentFov, fovVelocity;
        float lastPinchDistance = -1f;

        /// <summary>Lo usan las cinemáticas y los diálogos para congelar al jugador.</summary>
        public bool InputLocked { get; set; }
        public bool LookLocked { get; set; }
        public Transform Head => head;
        /// <summary>Corriendo por Shift o por doble toque de W.</summary>
        public bool IsSprinting { get; private set; }
        public bool TapSprinting => tapSprint;

        static bool cursorRequested = true;
        /// <summary>
        /// Lo que el juego quiere para el cursor (lo último pedido con LockCursor). En la vista isométrica el
        /// cursor nunca se captura (se apunta y se hace clic con él), así que vale false.
        /// </summary>
        public static bool RequestedCursorLocked => cursorRequested && !IsometricActive;
        /// <summary>Hay un jugador en vista isométrica (HUD, menús y cursor lo consultan).</summary>
        public static bool IsometricActive { get; private set; }

        public ViewMode Mode { get => viewMode; set => ApplyViewMode(value); }
        public bool IsIsometric => viewMode == ViewMode.Isometric;
        public IsometricCamera IsoCamera => isoCamera;
        public event System.Action<ViewMode> ViewModeChanged;
        /// <summary>Tras Teleport (la cámara salta, la ruta de ClickToMove se cancela).</summary>
        public event System.Action Teleported;

        /// <summary>Isométrica: dirección deseada en el mundo (magnitud 0..1) que pone ClickToMove; null = sin ruta.</summary>
        public Vector3? NavigationInput { get; set; }
        /// <summary>Isométrica: la ruta actual se recorre corriendo (doble clic).</summary>
        public bool NavigationRun { get; set; }
        /// <summary>Isométrica: punto del mundo bajo el cursor; la cabeza lo mira. null = mirar al frente.</summary>
        public Vector3? AimPoint { get; set; }
        /// <summary>Isométrica: el jugador está usando WASD / stick este frame (cancela la ruta del clic).</summary>
        public bool HasDirectInput { get; private set; }
        /// <summary>Velocidad horizontal real (m/s, mundo): la usan el avatar y sus animaciones.</summary>
        public Vector3 PlanarVelocity { get; private set; }
        public bool IsGrounded => controller && controller.isGrounded;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        /// <summary>Un menú (pausa, intro) tiene el cursor: el clic no lo captura.</summary>
        public static bool UIOwnsCursor { get; set; }

        /// <summary>Inclinación vertical de la cámara (grados, + mira hacia abajo).</summary>
        public float Pitch
        {
            get => pitch;
            set { pitch = Mathf.Clamp(value, -pitchLimit, pitchLimit); if (head) head.localRotation = Quaternion.Euler(pitch, 0f, 0f); }
        }

        /// <summary>Solo pruebas automáticas (AutoPlaytest): si tiene valor, sustituye al teclado/mando.</summary>
        public Vector2? DebugMoveInput { get; set; }
        public bool DebugRun { get; set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            GameSettings.EnsureLoaded();
            if (head)
            {
                headBase = head.localPosition;
                fpsCam = head.GetComponentInChildren<CinemachineCamera>(true);
            }
            if (fpsCam) baseFov = currentFov = fpsCam.Lens.FieldOfView;
            if (!isoCamera) isoCamera = FindAnyObjectByType<IsometricCamera>(FindObjectsInactive.Include);
            BrowserInput.BlockPageZoom();
            // Antes de OnEnable (que pide el cursor): así la vista isométrica nunca lo captura.
            IsometricActive = viewMode == ViewMode.Isometric && isoCamera;
        }

        void Start() => ApplyViewMode(viewMode, true);

        void OnEnable() => LockCursor(true);

        void OnDestroy()
        {
            if (IsIsometric) IsometricActive = false;
        }

        public static void LockCursor(bool locked)
        {
            cursorRequested = locked;
            ApplyCursor(locked);
        }

        /// <summary>Aplica sin cambiar lo que pidió el juego (los menús liberan el cursor temporalmente).</summary>
        public static void ApplyCursor(bool locked)
        {
            if (IsometricActive) locked = false;   // isométrica: el cursor siempre visible y libre
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ════════════════════════ VISTA ════════════════════════
        void ApplyViewMode(ViewMode mode, bool force = false)
        {
            if (mode == ViewMode.Isometric && !isoCamera)
            {
                Debug.LogWarning("[Jugador] No hay IsometricCamera en la escena: se usa la primera persona.");
                mode = ViewMode.FirstPerson;
            }
            if (mode == viewMode && !force) return;

            // La inercia pasa de una representación a la otra (sin tirón al cambiar).
            Vector3 world = viewMode == ViewMode.Isometric ? isoPlanar : transform.right * localPlanar.x + transform.forward * localPlanar.y;
            viewMode = mode;
            bool iso = mode == ViewMode.Isometric;
            IsometricActive = iso;
            isoPlanar = world;
            localPlanar = new Vector2(Vector3.Dot(world, transform.right), Vector3.Dot(world, transform.forward));
            NavigationInput = null;
            AimPoint = null;
            zoomOffset = 0f;

            if (fpsCam) fpsCam.Priority = iso ? 0 : 10;
            if (isoCamera) isoCamera.SetLive(iso);
            if (head)
            {
                head.localPosition = headBase;
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            suppressLookFrames = 2;
            ApplyCursor(cursorRequested && !UIOwnsCursor);
            ViewModeChanged?.Invoke(mode);
        }

        void HandleViewToggle()
        {
            if (!allowViewToggle || InputLocked || LookLocked || UIOwnsCursor || Keyboard.current == null) return;
            if (Keyboard.current[toggleViewKey].wasPressedThisFrame)
                ApplyViewMode(IsIsometric ? ViewMode.FirstPerson : ViewMode.Isometric);
        }

        void Update()
        {
            if (GamePause.IsPaused) return;
            HandleViewToggle();
            HandleCursor();
            if (IsIsometric)
            {
                AimHead();
                Move();
                return;
            }
            if (LookLocked) { wasLookLocked = true; suppressLookFrames = 2; }
            else
            {
                // Al recuperar el control tras una cinemática no se acumula el movimiento del ratón de ese tiempo.
                if (wasLookLocked) { wasLookLocked = false; suppressLookFrames = 2; }
                Look();
            }
            Zoom();
            Move();
        }

        /// <summary>
        /// Isométrica: la cabeza mira al cursor (así la linterna alumbra hacia ahí y "mirar" algo es apuntarle).
        /// En cinemáticas/diálogos (LookLocked) o con un bot de pruebas, mira al frente con el Pitch pedido.
        /// </summary>
        void AimHead()
        {
            if (!head) return;
            if (LookLocked || DebugMoveInput.HasValue || !AimPoint.HasValue)
            {
                Quaternion local = Quaternion.Euler(pitch, 0f, 0f);
                head.localRotation = LookLocked || DebugMoveInput.HasValue
                    ? local
                    : Quaternion.Slerp(head.localRotation, local, 1f - Mathf.Exp(-headAimSharpness * Time.deltaTime));
                return;
            }
            Vector3 d = AimPoint.Value - head.position;
            if (d.sqrMagnitude < 0.04f) return;
            Vector3 e = Quaternion.LookRotation(d).eulerAngles;
            float p = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -aimPitchLimit, aimPitchLimit);
            Quaternion target = Quaternion.Euler(p, e.y, 0f);
            head.rotation = Quaternion.Slerp(head.rotation, target, 1f - Mathf.Exp(-headAimSharpness * Time.deltaTime));
        }

        void HandleCursor()
        {
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked && !wasCursorLocked) suppressLookFrames = 2;   // primer delta tras capturar = salto
            wasCursorLocked = locked;
            if (UIOwnsCursor || !RequestedCursorLocked) return;
            // En WebGL el navegador exige un clic para capturar el puntero.
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !locked) ApplyCursor(true);
        }

        void Look()
        {
            Vector2 delta = Vector2.zero;
            // Si un bot de pruebas maneja al jugador, el mouse no gira la cámara.
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && !DebugMoveInput.HasValue)
            {
                Vector2 raw = Mouse.current.delta.ReadValue();
                if (suppressLookFrames > 0 || raw.magnitude > maxMouseDelta) raw = Vector2.zero;
                delta += new Vector2(raw.x * GameSettings.SensitivityX, raw.y * GameSettings.SensitivityY) * mouseSensitivity;
            }
            if (GamepadGuard.Current != null)
                delta += GamepadGuard.Current.rightStick.ReadValue() * (gamepadSensitivity * GameSettings.StickLookSpeed * Time.deltaTime);
            if (suppressLookFrames > 0) suppressLookFrames--;
            if (GameSettings.InvertY) delta.y = -delta.y;

            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            if (head) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // ════════════════════════ ZOOM ════════════════════════
        void Zoom()
        {
            if (!fpsCam) return;
            bool allowed = GameSettings.ZoomEnabled && !LookLocked && !InputLocked;
            if (!allowed) zoomOffset = 0f;
            else
            {
                float steps = 0f;
                if (Mouse.current != null)
                {
                    float s = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(s) > 20f) s /= 120f;   // por si llegan valores crudos (120 por muesca)
                    steps += Mathf.Clamp(s, -3f, 3f);    // pellizco en touchpad (Chrome/Edge/Firefox: rueda + Ctrl) entra aquí
                }
                steps += PinchSteps();
                // Rueda arriba / dedos separándose = acercar (FOV más cerrado).
                zoomOffset = Mathf.Clamp(zoomOffset - steps * fovPerScrollStep, minFov - baseFov, maxFovAboveBase);
            }
            float smooth = Mathf.Lerp(0.03f, zoomSmoothTime, GameSettings.CameraMotion);
            currentFov = Mathf.SmoothDamp(currentFov, baseFov + zoomOffset, ref fovVelocity, smooth);
            fpsCam.Lens.FieldOfView = currentFov;
        }

        /// <summary>Pantalla táctil: dos dedos que se separan = acercar. Devuelve "muescas" equivalentes.</summary>
        float PinchSteps()
        {
            var ts = Touchscreen.current;
            if (ts == null) { lastPinchDistance = -1f; return 0f; }
            var t = ts.touches;
            if (t.Count < 2 || !t[0].press.isPressed || !t[1].press.isPressed) { lastPinchDistance = -1f; return 0f; }
            float d = Vector2.Distance(t[0].position.ReadValue(), t[1].position.ReadValue());
            float steps = lastPinchDistance < 0f ? 0f : (d - lastPinchDistance) / Mathf.Max(1f, Screen.height) * 12f;
            lastPinchDistance = d;
            return steps;
        }

        // ════════════════════════ MOVIMIENTO ════════════════════════
        Vector2 ReadMoveInput()
        {
            if (InputLocked) return Vector2.zero;
            if (DebugMoveInput.HasValue) return Vector2.ClampMagnitude(DebugMoveInput.Value, 1f);
            Vector2 input = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
            }
            if (GamepadGuard.Current != null) input += GamepadGuard.Current.leftStick.ReadValue();
            return Vector2.ClampMagnitude(input, 1f);   // diagonal = misma velocidad que recto
        }

        /// <summary>
        /// Doble toque de W: la primera pulsación tiene que ser un toque (soltada dentro de la ventana) y la
        /// segunda llegar dentro de la ventana desde la primera. Mantener W apretada nunca lo activa.
        /// Se mantiene mientras W siga apretada.
        /// </summary>
        void UpdateTapSprint()
        {
            var kb = Keyboard.current;
            if (kb == null || InputLocked || DebugMoveInput.HasValue) { tapSprint = false; return; }
            var w = kb.wKey;
            var up = kb.upArrowKey;
            bool held = w.isPressed || up.isPressed;
            float now = Time.unscaledTime;
            if (w.wasPressedThisFrame || up.wasPressedThisFrame)
            {
                if (lastPressWasTap && now - lastForwardPress <= doubleTapWindow) tapSprint = true;
                lastForwardPress = now;
                lastPressWasTap = false;
            }
            if ((w.wasReleasedThisFrame || up.wasReleasedThisFrame) && !held)
                lastPressWasTap = now - lastForwardPress <= doubleTapWindow;
            if (!held) tapSprint = false;
        }

        bool ShiftHeld() => !InputLocked && (DebugRun
                || (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
                || (GamepadGuard.Current != null && GamepadGuard.Current.leftStickButton.isPressed));

        float TargetSpeed(Vector2 input)
        {
            bool shift = ShiftHeld();
            bool tap = tapSprint && input.y > 0.1f;
            IsSprinting = shift || tap;
            // Si se usan los dos, gana el más rápido (Shift): no se suman.
            return shift ? runSpeed : tap ? tapSprintSpeed : walkSpeed;
        }

        /// <summary>
        /// Isométrica: WASD/stick relativos a la cámara o la ruta de ClickToMove, en ejes del mundo.
        /// El cuerpo gira suave hacia donde camina (nunca de golpe).
        /// </summary>
        Vector3 IsoMove()
        {
            Vector3 desired = Vector3.zero;
            bool run = false;
            Vector2 keys = ReadMoveInput();
            HasDirectInput = keys.sqrMagnitude > 0.01f;
            if (HasDirectInput)
            {
                Transform cam = isoCamera ? isoCamera.transform : Camera.main ? Camera.main.transform : transform;
                Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                if (f.sqrMagnitude < 0.001f) f = Vector3.ProjectOnPlane(cam.up, Vector3.up);
                f.Normalize();
                Vector3 r = Vector3.Cross(Vector3.up, f);
                desired = r * keys.x + f * keys.y;
                run = ShiftHeld();
            }
            else if (!InputLocked && NavigationInput.HasValue)
            {
                desired = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(NavigationInput.Value, Vector3.up), 1f);
                run = NavigationRun || ShiftHeld();
            }
            IsSprinting = run && desired.sqrMagnitude > 0.01f;
            desired *= run ? runSpeed : walkSpeed;

            float rate = desired.sqrMagnitude >= isoPlanar.sqrMagnitude ? acceleration : deceleration;
            if (InputLocked) rate = deceleration * 2f;
            isoPlanar = Vector3.MoveTowards(isoPlanar, desired, rate * Time.deltaTime);

            Vector3 face = desired.sqrMagnitude > 0.01f ? desired : isoPlanar;
            if (face.sqrMagnitude > 0.09f && !LookLocked)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face),
                                                      1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            return isoPlanar;
        }

        void Move()
        {
            UpdateTapSprint();
            // Los bots de pruebas (DebugMoveInput) siempre usan el control clásico en ejes del jugador.
            bool iso = IsIsometric && !DebugMoveInput.HasValue;
            Vector3 move;
            if (iso) move = IsoMove();
            else
            {
                HasDirectInput = false;
                Vector2 input = ReadMoveInput();
                float speed = TargetSpeed(input);
                Vector2 target = input * speed;
                float rate = target.sqrMagnitude >= localPlanar.sqrMagnitude ? acceleration : deceleration;
                if (InputLocked) rate = deceleration * 2f;   // cinemática: se detiene casi en seco
                localPlanar = Vector2.MoveTowards(localPlanar, target, rate * Time.deltaTime);
                move = transform.right * localPlanar.x + transform.forward * localPlanar.y;
            }

            bool grounded = controller.isGrounded;
            if (grounded && velocity.y < 0f) velocity.y = -2f; // pegado al suelo

            // Resbalar en pendientes imposibles: "o pisas las rocas o sales volando".
            // groundNormal viene de los choques del frame anterior.
            Vector3 normal = groundNormal;
            groundNormal = Vector3.up; // se vuelve a llenar en OnControllerColliderHit durante Move()
            hasGroundHit = false;
            float slopeAngle = Vector3.Angle(normal, Vector3.up);
            if (grounded && slopeAngle > controller.slopeLimit)
            {
                Vector3 slideDir = new Vector3(normal.x, 0f, normal.z).normalized;
                move = move * 0.2f + slideDir * slideSpeed;
            }

            velocity.y += gravity * Time.deltaTime;
            Vector3 before = transform.position;
            controller.Move((move + velocity) * Time.deltaTime);

            Vector3 realPlanar = Time.deltaTime > 0f ? Vector3.ProjectOnPlane((transform.position - before) / Time.deltaTime, Vector3.up) : Vector3.zero;
            PlanarVelocity = realPlanar;
            // Si una pared frena al jugador, la inercia acumulada no se queda "cargada" contra ella.
            if ((controller.collisionFlags & CollisionFlags.Sides) != 0 && Time.deltaTime > 0f)
            {
                if (iso)
                {
                    if (realPlanar.magnitude < isoPlanar.magnitude * 0.5f) isoPlanar = Vector3.Lerp(isoPlanar, realPlanar, 0.5f);
                }
                else
                {
                    Vector2 realLocal = new(Vector3.Dot(realPlanar, transform.right), Vector3.Dot(realPlanar, transform.forward));
                    // Solo si de verdad está bloqueado (no al subir el borde de una roca del sendero).
                    if (realLocal.magnitude < localPlanar.magnitude * 0.5f) localPlanar = Vector2.Lerp(localPlanar, realLocal, 0.5f);
                }
            }

            HeadBobAndSteps(iso ? isoPlanar.magnitude : localPlanar.magnitude, grounded);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Nos quedamos con la superficie MÁS PLANA tocada este frame (así las rocas "ganan" a la pendiente).
            Vector3 n = hit.normal;
            if (n.y <= 0.1f) return;
            // En aristas (borde de una roca) Unity da una normal "redondeada" que parece pendiente empinada
            // y el jugador resbalaba hacia atrás. Medimos la superficie real con un rayo corto hacia abajo:
            // encima de la roca sale plana; en la colina empinada sigue saliendo empinada.
            if (n.y < 0.95f && Physics.Raycast(hit.point + Vector3.up * 0.08f, Vector3.down, out var surface, 0.2f, ~0, QueryTriggerInteraction.Ignore)
                && surface.normal.y > n.y)
                n = surface.normal;
            if (!hasGroundHit || n.y > groundNormal.y) groundNormal = n;
            hasGroundHit = true;
        }

        void HeadBobAndSteps(float planarSpeed, bool grounded)
        {
            if (!head) return;
            bool moving = grounded && planarSpeed > 0.15f;
            // Envolvente: el balanceo entra y sale suave (antes se "rebobinaba" la fase al detenerse).
            bobWeight = Mathf.MoveTowards(bobWeight, moving ? Mathf.Clamp01(planarSpeed / walkSpeed) : 0f, Time.deltaTime * 3f);
            if (moving)
            {
                float freq = bobFrequency * Mathf.Max(0.6f, planarSpeed / walkSpeed);
                bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;
                stepAccumulator += planarSpeed * Time.deltaTime;
                if (stepAccumulator >= stepDistance)
                {
                    stepAccumulator = 0f;
                    PlayFootstep();
                }
            }

            // En isométrica la cabeza no se balancea (la cámara no está en ella): solo suenan los pasos.
            float amp = GameSettings.HeadBob && !IsIsometric ? bobAmplitude * GameSettings.CameraMotion * bobWeight : 0f;
            if (planarSpeed > walkSpeed * 1.2f) amp *= 1.15f;                  // corriendo, apenas más
            Vector3 p = headBase;
            p.y += Mathf.Sin(bobTimer) * amp;
            p.x += Mathf.Cos(bobTimer * 0.5f) * amp * 0.35f;                  // vaivén lateral mínimo
            head.localPosition = p;
        }

        void PlayFootstep()
        {
            if (!footstepSource || footstepClips == null || footstepClips.Length == 0) return;
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(footstepClips[Random.Range(0, footstepClips.Length)]);
        }

        /// <summary>Gira suavemente la vista hacia un punto (útil tras cinemáticas).</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            velocity = Vector3.zero;
            localPlanar = Vector2.zero;
            isoPlanar = Vector3.zero;
            PlanarVelocity = Vector3.zero;
            NavigationInput = null;
            suppressLookFrames = 2;
            Teleported?.Invoke();
        }
    }
}
