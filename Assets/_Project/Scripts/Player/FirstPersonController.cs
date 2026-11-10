using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    /// <summary>
    /// Controlador en primera persona con CharacterController (gravedad manual, como en la semana 9).
    /// - Movimiento lento y pesado (terror, no acción).
    /// - En pendientes más empinadas que slopeLimit el jugador resbala: la colina solo se baja por las rocas.
    /// - Head bob sutil y pasos (si hay clips asignados).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] float walkSpeed = 2.6f;
        [SerializeField] float runSpeed = 4.4f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float slideSpeed = 7f;

        [Header("Cámara")]
        [SerializeField] Transform head;
        [SerializeField] float mouseSensitivity = 0.08f;
        [SerializeField] float gamepadSensitivity = 120f;
        [SerializeField] float pitchLimit = 80f;

        [Header("Head bob")]
        [SerializeField] float bobFrequency = 1.8f;
        [SerializeField] float bobAmplitude = 0.035f;

        [Header("Pasos")]
        [SerializeField] AudioSource footstepSource;
        [SerializeField] AudioClip[] footstepClips;
        [SerializeField] float stepDistance = 1.7f;

        CharacterController controller;
        Vector3 velocity;
        float pitch;
        float bobTimer;
        float headBaseY;
        float stepAccumulator;
        Vector3 groundNormal = Vector3.up;
        bool hasGroundHit;

        /// <summary>Lo usan las cinemáticas y los diálogos para congelar al jugador.</summary>
        public bool InputLocked { get; set; }
        public bool LookLocked { get; set; }
        public Transform Head => head;

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
            if (head) headBaseY = head.localPosition.y;
        }

        void OnEnable() => LockCursor(true);

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            HandleCursor();
            if (!LookLocked) Look();
            Move();
        }

        void HandleCursor()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            // En WebGL el navegador exige un clic para capturar el puntero.
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);
        }

        void Look()
        {
            Vector2 delta = Vector2.zero;
            // Si un bot de pruebas maneja al jugador, el mouse no gira la cámara.
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && !DebugMoveInput.HasValue)
                delta += Mouse.current.delta.ReadValue() * mouseSensitivity;
            if (GamepadGuard.Current != null)
                delta += GamepadGuard.Current.rightStick.ReadValue() * gamepadSensitivity * Time.deltaTime;

            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            if (head) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

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
            return Vector2.ClampMagnitude(input, 1f);
        }

        bool RunPressed()
        {
            if (InputLocked) return false;
            if (DebugRun) return true;
            return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                || (GamepadGuard.Current != null && GamepadGuard.Current.leftStickButton.isPressed);
        }

        void Move()
        {
            Vector2 input = ReadMoveInput();
            float speed = RunPressed() ? runSpeed : walkSpeed;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

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
            controller.Move((move + velocity) * Time.deltaTime);

            HeadBobAndSteps(input, speed, grounded);
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

        void HeadBobAndSteps(Vector2 input, float speed, bool grounded)
        {
            if (!head) return;
            bool moving = grounded && input.sqrMagnitude > 0.01f;
            if (moving)
            {
                float freq = bobFrequency * (speed / walkSpeed);
                bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;
                stepAccumulator += speed * Time.deltaTime;
                if (stepAccumulator >= stepDistance)
                {
                    stepAccumulator = 0f;
                    PlayFootstep();
                }
            }
            else
            {
                bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 4f);
            }

            Vector3 p = head.localPosition;
            p.y = headBaseY + Mathf.Sin(bobTimer) * bobAmplitude;
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
        }
    }
}
