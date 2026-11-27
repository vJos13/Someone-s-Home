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
    /// - Cámara: sensibilidad/inversión desde GameSettings, filtro de saltos del puntero (WebGL).
    ///   Enfoque: mantener clic derecho (o gatillo izquierdo) cierra el lente suavemente para mirar de lejos.
    ///   Zoom libre: Ctrl + rueda / pellizco (touchpad o pantalla táctil). La rueda sola cambia de objeto (inventario).
    /// - Head bob sutil con envolvente (entra y sale suave) y pasos (si hay clips asignados).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
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

        [Header("Salto")]
        [Tooltip("Altura del salto en metros (modesto: es un relato, no un plataformas).")]
        [SerializeField] float jumpHeight = 0.6f;
        [Tooltip("Margen para saltar justo después de salir de un borde (s).")]
        [SerializeField] float coyoteTime = 0.12f;
        [Tooltip("Un Espacio apretado un instante antes de tocar el suelo igual salta (s).")]
        [SerializeField] float jumpBuffer = 0.15f;
        [Tooltip("Control en el aire (0 = ninguno, 1 = igual que en el suelo).")]
        [SerializeField] float airControl = 0.35f;
        [SerializeField] float maxFallSpeed = 40f;

        [Header("Carrera")]
        [Tooltip("Grados que se abre el lente al correr (se escala con 'Movimiento de cámara').")]
        [SerializeField] float sprintFovKick = 4f;

        [Header("Cámara")]
        [SerializeField] Transform head;
        [SerializeField] float mouseSensitivity = 0.08f;
        [SerializeField] float gamepadSensitivity = 120f;
        [SerializeField] float pitchLimit = 85f;
        [Tooltip("Saltos del puntero mayores a esto (px en un frame) se descartan: pasa al capturar el puntero en WebGL.")]
        [SerializeField] float maxMouseDelta = 250f;

        [Header("Zoom (FOV de VCam_FPS)")]
        [SerializeField] float minFov = 42f;
        [Tooltip("Cuánto se puede abrir por encima del FOV base.")]
        [SerializeField] float maxFovAboveBase = 6f;
        [SerializeField] float fovPerScrollStep = 3.5f;
        [SerializeField] float zoomSmoothTime = 0.12f;
        [Tooltip("Grados que cierra el lente al enfocar con clic derecho.")]
        [SerializeField] float focusFovDrop = 16f;

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

        // Salto
        float lastGroundedTime = -10f, jumpRequestedAt = -10f;
        bool jumping, airborne;
        float airborneMaxFall;          // velocidad de caída máxima del salto/caída en curso
        float landDip;                  // hundimiento de la cámara al aterrizar
        float sprintBlend;
        readonly Collider[] feetHits = new Collider[8];
        float stepOffsetGround = 0.45f;

        // Zoom
        float baseFov, zoomOffset, currentFov, fovVelocity, focusBlend;
        float lastPinchDistance = -1f;

        /// <summary>Lo usan las cinemáticas y los diálogos para congelar al jugador.</summary>
        public bool InputLocked { get; set; }
        public bool LookLocked { get; set; }
        public Transform Head => head;
        /// <summary>Corriendo por Shift o por doble toque de W.</summary>
        public bool IsSprinting { get; private set; }
        public bool TapSprinting => tapSprint;
        /// <summary>Pisando suelo (CharacterController o la esfera de los pies).</summary>
        public bool IsGrounded { get; private set; }
        /// <summary>Solo pruebas: pide un salto como si se apretara Espacio.</summary>
        public void DebugJump() => jumpRequestedAt = Time.time;

        /// <summary>Lo que el juego quiere para el cursor (lo último pedido con LockCursor).</summary>
        public static bool RequestedCursorLocked { get; private set; } = true;
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
            stepOffsetGround = controller.stepOffset;
            GameSettings.EnsureLoaded();
            // El inventario vive en el jugador (lo usan la linterna, el celular y el relato).
            if (!GetComponent<InventorySystem>()) gameObject.AddComponent<InventorySystem>();
            if (head)
            {
                headBase = head.localPosition;
                fpsCam = head.GetComponentInChildren<CinemachineCamera>(true);
            }
            if (fpsCam) baseFov = currentFov = fpsCam.Lens.FieldOfView;
            BrowserInput.BlockPageZoom();
        }

        void OnEnable() => LockCursor(true);

        public static void LockCursor(bool locked)
        {
            RequestedCursorLocked = locked;
            ApplyCursor(locked);
        }

        /// <summary>Aplica sin cambiar lo que pidió el juego (los menús liberan el cursor temporalmente).</summary>
        public static void ApplyCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (GamePause.IsPaused) return;
            HandleCursor();
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
                var kb = Keyboard.current;
                bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
                if (Mouse.current != null && ctrl)
                {
                    float s = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(s) > 20f) s /= 120f;   // por si llegan valores crudos (120 por muesca)
                    steps += Mathf.Clamp(s, -3f, 3f);
                }
                steps += PinchSteps();
                // Rueda arriba / dedos separándose = acercar (FOV más cerrado).
                zoomOffset = Mathf.Clamp(zoomOffset - steps * fovPerScrollStep, minFov - baseFov, maxFovAboveBase);
            }
            // Enfoque sostenido: clic derecho o gatillo izquierdo.
            bool focus = !LookLocked && !InputLocked
                         && ((Mouse.current != null && Mouse.current.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked)
                             || (GamepadGuard.Current != null && GamepadGuard.Current.leftTrigger.isPressed));
            focusBlend = Mathf.MoveTowards(focusBlend, focus ? 1f : 0f, Time.deltaTime * 4f);
            // Correr abre apenas el lente (solo si de verdad avanza rápido).
            bool fast = IsSprinting && localPlanar.y > walkSpeed * 1.05f && !InputLocked;
            sprintBlend = Mathf.MoveTowards(sprintBlend, fast ? 1f : 0f, Time.deltaTime * (fast ? 2f : 3f));
            float kick = Mathf.SmoothStep(0f, 1f, sprintBlend) * sprintFovKick * GameSettings.CameraMotion;
            float target = Mathf.Max(minFov - 8f, baseFov + zoomOffset + kick - Mathf.SmoothStep(0f, 1f, focusBlend) * focusFovDrop);
            float smooth = Mathf.Lerp(0.03f, zoomSmoothTime, GameSettings.CameraMotion);
            currentFov = Mathf.SmoothDamp(currentFov, target, ref fovVelocity, smooth);
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

        float TargetSpeed(Vector2 input)
        {
            bool shift = !InputLocked && (DebugRun
                || (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
                || (GamepadGuard.Current != null && GamepadGuard.Current.leftStickButton.isPressed));
            bool tap = tapSprint && input.y > 0.1f;
            IsSprinting = shift || tap;
            // Si se usan los dos, gana el más rápido (Shift): no se suman.
            return shift ? runSpeed : tap ? tapSprintSpeed : walkSpeed;
        }

        void Move()
        {
            UpdateTapSprint();
            Vector2 input = ReadMoveInput();
            float speed = TargetSpeed(input);
            Vector2 target = input * speed;
            float rate = target.sqrMagnitude >= localPlanar.sqrMagnitude ? acceleration : deceleration;
            if (InputLocked) rate = deceleration * 2f;   // cinemática: se detiene casi en seco
            if (airborne) rate *= airControl;            // en el aire se conserva el impulso
            localPlanar = Vector2.MoveTowards(localPlanar, target, rate * Time.deltaTime);
            Vector3 move = transform.right * localPlanar.x + transform.forward * localPlanar.y;

            bool grounded = controller.isGrounded;
            // Suelo fiable: además del CharacterController (que a veces parpadea en bordes y escalones),
            // una esfera en los pies. Solo cuenta bajando: al subir de un salto no "re-aterriza".
            bool feet = velocity.y <= 0.1f && FeetOnGround();
            IsGrounded = grounded || feet;
            if (IsGrounded && velocity.y <= 0f) { lastGroundedTime = Time.time; jumping = false; }
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

            // Salto: Espacio (o Y del mando), con margen al salir de un borde y al apretar antes de tocar el suelo.
            if (!InputLocked && !DebugMoveInput.HasValue && JumpPressed()) jumpRequestedAt = Time.time;
            bool steep = grounded && slopeAngle > controller.slopeLimit;
            if (!InputLocked && !jumping && !steep
                && Time.time - jumpRequestedAt <= jumpBuffer && Time.time - lastGroundedTime <= coyoteTime)
            {
                velocity.y = Mathf.Sqrt(2f * -gravity * jumpHeight);
                jumping = true;
                jumpRequestedAt = lastGroundedTime = -10f;
            }

            velocity.y = Mathf.Max(velocity.y + gravity * Time.deltaTime, -maxFallSpeed);
            Vector3 before = transform.position;
            // En el aire no se "sube" a bordes por el stepOffset (evita trepar paredes saltando).
            controller.stepOffset = IsGrounded ? stepOffsetGround : 0.05f;
            controller.Move((move + velocity) * Time.deltaTime);
            // Cabeza contra el techo: corta el impulso hacia arriba.
            if ((controller.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            // Aterrizaje: hundimiento breve de la cámara y un paso, proporcional a la caída.
            bool nowGrounded = controller.isGrounded || (velocity.y <= 0f && FeetOnGround());
            if (!nowGrounded) { airborne = true; airborneMaxFall = Mathf.Max(airborneMaxFall, -velocity.y); }
            else if (airborne)
            {
                airborne = false;
                if (airborneMaxFall > 3f)
                {
                    landDip = Mathf.Clamp(airborneMaxFall * 0.012f, 0f, 0.09f) * GameSettings.CameraMotion;
                    PlayFootstep();
                }
                airborneMaxFall = 0f;
            }

            // Si una pared frena al jugador, la inercia acumulada no se queda "cargada" contra ella.
            if ((controller.collisionFlags & CollisionFlags.Sides) != 0 && Time.deltaTime > 0f)
            {
                Vector3 real = (transform.position - before) / Time.deltaTime;
                Vector2 realLocal = new(Vector3.Dot(real, transform.right), Vector3.Dot(real, transform.forward));
                // Solo si de verdad está bloqueado (no al subir el borde de una roca del sendero).
                if (realLocal.magnitude < localPlanar.magnitude * 0.5f) localPlanar = Vector2.Lerp(localPlanar, realLocal, 0.5f);
            }

            HeadBobAndSteps(localPlanar.magnitude, grounded);
        }

        bool JumpPressed()
        {
            var kb = Keyboard.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame)
                   || (GamepadGuard.Current != null && GamepadGuard.Current.buttonNorth.wasPressedThisFrame);
        }

        /// <summary>Esfera en los pies: ¿hay algo sólido debajo (que no sea el propio jugador)?</summary>
        bool FeetOnGround()
        {
            float r = controller.radius * 0.9f;
            Vector3 bottom = transform.position + controller.center - Vector3.up * (controller.height * 0.5f);
            Vector3 c = bottom + Vector3.up * (r - 0.08f);
            int n = Physics.OverlapSphereNonAlloc(c, r, feetHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (feetHits[i] != controller && !feetHits[i].transform.IsChildOf(transform)) return true;
            return false;
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

            float amp = GameSettings.HeadBob ? bobAmplitude * GameSettings.CameraMotion * bobWeight : 0f;
            if (planarSpeed > walkSpeed * 1.2f) amp *= 1.15f;                  // corriendo, apenas más
            Vector3 p = headBase;
            landDip = Mathf.MoveTowards(landDip, 0f, Time.deltaTime * 0.35f);
            p.y += Mathf.Sin(bobTimer) * amp - landDip;
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
            jumping = airborne = false;
            airborneMaxFall = 0f;
            suppressLookFrames = 2;
        }
    }
}
