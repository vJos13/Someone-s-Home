using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Clic para moverse (vista isométrica). No mueve nada por sí mismo: calcula la ruta con el NavMesh
    /// horneado (NavMesh.CalculatePath) y le pasa la dirección a FirstPersonController.NavigationInput, que
    /// sigue moviendo el CharacterController de siempre (gravedad, pendientes, triggers y colisiones intactos).
    ///  - Clic izquierdo en el terreno: camina hasta ahí (esquiva obstáculos por la ruta). Mantener apretado
    ///    actualiza el destino; doble clic corre. Otro clic mientras camina cambia el destino.
    ///  - Clic en algo interactuable: camina hasta tenerlo al alcance y lo usa (Interactor).
    ///  - El cursor también es la mirada: AimPoint = lo que hay bajo el puntero (linterna, mecánicas de mirada).
    /// </summary>
    [DefaultExecutionOrder(-20)]   // antes de FirstPersonController (lee NavigationInput / AimPoint en su Update)
    [RequireComponent(typeof(FirstPersonController))]
    public class ClickToMove : MonoBehaviour
    {
        [Header("Raycast del clic")]
        [SerializeField] LayerMask clickMask = ~0;
        [SerializeField] float maxRayDistance = 250f;
        [Tooltip("Cuánto se busca el NavMesh alrededor del punto clicado.")]
        [SerializeField] float navSampleRadius = 2.5f;
        [Tooltip("Distancia máxima a la que se resalta un interactuable bajo el cursor.")]
        [SerializeField] float hoverRange = 30f;

        [Header("Ruta")]
        [SerializeField] float stopDistance = 0.12f;
        [Tooltip("A qué distancia de una esquina de la ruta se pasa a la siguiente.")]
        [SerializeField] float cornerReach = 0.35f;
        [Tooltip("Radio en el que frena al acercarse al destino (llega sin patinar).")]
        [SerializeField] float slowRadius = 0.9f;
        [Range(0.05f, 1f)] [SerializeField] float minArrivalSpeed = 0.3f;
        [Tooltip("Sin avanzar este tiempo (puerta cerrada, otro obstáculo) = se cancela la ruta.")]
        [SerializeField] float stuckTime = 0.8f;

        [Header("Clics")]
        [SerializeField] bool holdToMove = true;
        [SerializeField] float holdRepathInterval = 0.12f;
        [SerializeField] bool doubleClickRuns = true;
        [SerializeField] float doubleClickTime = 0.3f;

        [Header("Marcador de destino")]
        [SerializeField] Material markerMaterial;
        [SerializeField] float markerSize = 0.6f;
        [SerializeField] float markerLifetime = 0.6f;

        FirstPersonController player;
        Interactor interactor;
        NavMeshPath navPath;
        readonly List<Vector3> corners = new();
        int cornerIndex;
        bool running;

        IInteractable pendingTarget;
        Collider pendingCollider;

        float lastClickTime = -10f, lastHoldRepath, pressTime;
        float stuckTimer, bestRemaining;
        int stuckCorner;

        Transform marker;
        Material markerInstance;
        float markerTimer;

        // Lo que hay bajo el cursor.
        bool hasGround;
        Vector3 groundPoint;

        public IInteractable Hovered { get; private set; }
        public Collider HoveredCollider { get; private set; }
        public bool HasPath => corners.Count > 0;
        public Vector3? Destination => HasPath ? corners[^1] : null;

        void Awake()
        {
            player = GetComponent<FirstPersonController>();
            interactor = GetComponent<Interactor>();
            navPath = new NavMeshPath();
        }

        void OnEnable() => player.Teleported += Stop;
        void OnDisable()
        {
            player.Teleported -= Stop;
            Stop();
            ClearHover();
        }

        void Update()
        {
            if (!player.IsIsometric)
            {
                if (HasPath) Stop();
                ClearHover();
                return;
            }
            if (GamePause.IsPaused) return;

            UpdatePointer();
            // Cinemáticas, diálogos o un bot de pruebas: nada de rutas.
            if (player.InputLocked || player.DebugMoveInput.HasValue) { if (HasPath) Stop(); return; }
            if (player.HasDirectInput && HasPath) Stop();   // WASD/stick manda sobre el clic

            HandleClicks();
            FollowPath();
            UpdateMarker();
        }

        // ════════════════════════ CURSOR ════════════════════════
        void ClearHover()
        {
            Hovered = null;
            HoveredCollider = null;
            hasGround = false;
            if (player) player.AimPoint = null;
        }

        static bool PointerOverUI() => EventSystem.current && EventSystem.current.IsPointerOverGameObject();

        void UpdatePointer()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || !cam || FirstPersonController.UIOwnsCursor) { ClearHover(); return; }
            if (PointerOverUI()) { Hovered = null; HoveredCollider = null; hasGround = false; return; }   // conserva la mirada

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Hovered = null;
            HoveredCollider = null;
            hasGround = false;
            if (Raycast(ray, out var hit))
            {
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null && target.CanInteract && Vector3.Distance(transform.position, hit.point) <= hoverRange)
                {
                    Hovered = target;
                    HoveredCollider = hit.collider;
                    player.AimPoint = hit.collider.bounds.center;
                }
                else player.AimPoint = hit.point;
                hasGround = true;
                groundPoint = hit.point;
            }
            else
            {
                // Al cielo: se mira hacia ese lado, a la altura de los ojos.
                var plane = new Plane(Vector3.up, transform.position + Vector3.up * 1.6f);
                if (plane.Raycast(ray, out float d)) player.AimPoint = ray.GetPoint(d);
            }
        }

        /// <summary>
        /// Primer collider válido bajo el rayo: ignora triggers, al jugador, colliders sin nada visible (límites,
        /// greybox apagado) y lo que la cámara ocultó (techo de la casa), salvo que sea interactuable.
        /// </summary>
        bool Raycast(Ray ray, out RaycastHit best)
        {
            best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            var iso = player.IsoCamera;
            foreach (var h in Physics.RaycastAll(ray, maxRayDistance, clickMask, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= bestDist || h.transform.IsChildOf(transform)) continue;
                // Los interactuables cuentan siempre: su collider suele ser el del greybox (renderer apagado,
                // lo que se ve es el modelo de Blender), como el panel de la puerta trasera.
                var interactable = h.collider.GetComponentInParent<IInteractable>();
                bool usable = interactable != null && interactable.CanInteract;
                if (!usable && !(h.collider is TerrainCollider))
                {
                    var r = h.collider.GetComponent<Renderer>();
                    if (!r) r = h.collider.GetComponentInParent<Renderer>();
                    // Sin renderer (límites invisibles) o recortado por la cámara (techo de la casa): se atraviesa.
                    if (!r || !r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || (iso && iso.IsHidden(r))) continue;
                }
                best = h;
                bestDist = h.distance;
                found = true;
            }
            return found;
        }

        // ════════════════════════ CLICS ════════════════════════
        void HandleClicks()
        {
            var mouse = Mouse.current;
            if (mouse == null || FirstPersonController.UIOwnsCursor) return;
            float now = Time.unscaledTime;

            if (mouse.leftButton.wasPressedThisFrame && !PointerOverUI())
            {
                bool dbl = now - lastClickTime <= doubleClickTime;
                lastClickTime = now;
                pressTime = now;
                if (Hovered != null && interactor) interactor.Request(Hovered, HoveredCollider);
                else if (hasGround) SetDestination(groundPoint, doubleClickRuns && dbl);
                lastHoldRepath = now;
            }
            else if (holdToMove && mouse.leftButton.isPressed && pendingTarget == null && hasGround
                     && now - pressTime > 0.2f && now - lastHoldRepath > holdRepathInterval && !PointerOverUI())
            {
                lastHoldRepath = now;
                SetDestination(groundPoint, running, false);
            }
        }

        // ════════════════════════ RUTA ════════════════════════
        /// <summary>Calcula una ruta por el NavMesh hasta el punto. false si no hay terreno navegable cerca.</summary>
        public bool SetDestination(Vector3 point, bool run = false, bool showMarker = true)
        {
            pendingTarget = null;
            pendingCollider = null;
            return PathTo(point, run, showMarker);
        }

        /// <summary>Camina hasta tener el objeto al alcance del Interactor y lo usa.</summary>
        public bool WalkToInteract(IInteractable target, Collider col)
        {
            if (target == null || !col) return false;
            Vector3 near = ClosestPoint(col, transform.position);
            bool ok = PathTo(near, false, false);
            if (ok) { pendingTarget = target; pendingCollider = col; }
            return ok;
        }

        bool PathTo(Vector3 point, bool run, bool showMarker)
        {
            if (!NavMesh.SamplePosition(point, out var to, navSampleRadius, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(transform.position, out var from, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, navPath) || navPath.status == NavMeshPathStatus.PathInvalid)
                return false;
            corners.Clear();
            corners.AddRange(navPath.corners);
            cornerIndex = corners.Count > 1 ? 1 : 0;
            running = run;
            player.NavigationRun = run;
            stuckTimer = 0f;
            stuckCorner = -1;
            if (showMarker) ShowMarker(to.position);
            return true;
        }

        public void Stop()
        {
            corners.Clear();
            pendingTarget = null;
            pendingCollider = null;
            running = false;
            if (player)
            {
                player.NavigationInput = null;
                player.NavigationRun = false;
            }
        }

        void FollowPath()
        {
            if (!HasPath) { player.NavigationInput = null; return; }

            // Interacción pendiente: en cuanto está al alcance se detiene y la usa.
            if (pendingTarget != null)
            {
                if (!pendingCollider || !pendingTarget.CanInteract) { Stop(); return; }
                if (interactor && interactor.InRange(pendingCollider))
                {
                    var t = pendingTarget;
                    Stop();
                    interactor.InteractWith(t);
                    return;
                }
            }

            Vector3 pos = transform.position;
            int last = corners.Count - 1;
            Vector3 d = corners[cornerIndex] - pos;
            d.y = 0f;
            while (d.magnitude < (cornerIndex == last ? stopDistance : cornerReach))
            {
                if (cornerIndex == last) { Stop(); return; }
                cornerIndex++;
                d = corners[cornerIndex] - pos;
                d.y = 0f;
            }

            float scale = 1f;
            if (cornerIndex == last) scale = Mathf.Clamp(d.magnitude / slowRadius, minArrivalSpeed, 1f);
            player.NavigationInput = d.normalized * scale;

            // ¿Atascado? (puerta cerrada, empujando contra algo). Se mide el progreso hacia la esquina actual,
            // no el desplazamiento: rebotar contra una puerta no cuenta como avanzar.
            float remaining = d.magnitude;
            if (cornerIndex != stuckCorner || remaining < bestRemaining - 0.1f)
            {
                stuckCorner = cornerIndex;
                bestRemaining = remaining;
                stuckTimer = 0f;
            }
            else if ((stuckTimer += Time.deltaTime) >= stuckTime) Stop();
        }

        static Vector3 ClosestPoint(Collider c, Vector3 p)
        {
            bool supported = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mc && mc.convex);
            return supported ? c.ClosestPoint(p) : c.bounds.ClosestPoint(p);
        }

        // ════════════════════════ MARCADOR ════════════════════════
        void ShowMarker(Vector3 at)
        {
            if (!markerMaterial) return;
            if (!marker)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "ClickMarker";
                Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<MeshRenderer>();
                markerInstance = new Material(markerMaterial);
                r.sharedMaterial = markerInstance;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                marker = go.transform;
            }
            marker.gameObject.SetActive(true);
            marker.position = at + Vector3.up * 0.03f;
            markerTimer = markerLifetime;
        }

        void UpdateMarker()
        {
            if (!marker || !marker.gameObject.activeSelf) return;
            markerTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(markerTimer / markerLifetime);
            if (t <= 0f) { marker.gameObject.SetActive(false); return; }
            float s = markerSize * Mathf.Lerp(0.35f, 1f, t);
            marker.localScale = new Vector3(s, 0.005f, s);
            if (markerInstance && markerInstance.HasProperty("_BaseColor"))
            {
                var c = markerInstance.GetColor("_BaseColor");
                c.a = t;
                markerInstance.SetColor("_BaseColor", c);
            }
        }

        void OnDestroy()
        {
            if (marker) Destroy(marker.gameObject);
            if (markerInstance) Destroy(markerInstance);
        }
    }
}
