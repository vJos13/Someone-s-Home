using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Cámara isométrica estilo ARPG (Diablo / Path of Exile) sobre una CinemachineCamera del mismo Brain:
    /// las cinemáticas (CinematicShot) y el cambio de vista se siguen mezclando con el blend del Brain.
    ///  - Seguimiento suave (SmoothDamp) con un poco de anticipación en la dirección de movimiento.
    ///  - Ángulo (pitch/yaw) y distancia configurables; zoom con la rueda / pellizco; girar con botón central
    ///    o stick derecho (opcional).
    ///  - Lo que tape al personaje (árboles, rocas, vehículos) deja de dibujarse (sigue haciendo sombra).
    ///    Los edificios (CutawayZone) se cortan como casa de muñecas: techo y cielo raso con el jugador adentro;
    ///    también paredes, vidrios y puertas cuando el edificio lo tapa desde afuera.
    /// La posición se calcula aquí; la CinemachineCamera no lleva componentes de Body/Aim.
    /// </summary>
    [DefaultExecutionOrder(100)]   // después de que el jugador se movió en Update
    [RequireComponent(typeof(CinemachineCamera))]
    public class IsometricCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] FirstPersonController player;
        [Tooltip("Punto que se encuadra, relativo a los pies del jugador.")]
        [SerializeField] Vector3 targetOffset = new(0f, 1.1f, 0f);

        [Header("Ángulo")]
        [Range(25f, 89f)] [SerializeField] float pitch = 50f;
        [SerializeField] float yaw = 40f;
        [Tooltip("Pitch dentro de una CutawayZone (más cenital para ver por encima de las paredes).")]
        [Range(25f, 89f)] [SerializeField] float interiorPitch = 64f;

        [Header("Distancia y zoom")]
        [SerializeField] float distance = 13f;
        [SerializeField] float minDistance = 6f;
        [SerializeField] float maxDistance = 24f;
        [Tooltip("Metros por muesca de la rueda.")]
        [SerializeField] float zoomStep = 1.6f;
        [SerializeField] float zoomSmoothTime = 0.15f;
        [Tooltip("Distancia máxima dentro de una CutawayZone (no se aleja tanto adentro de la casa).")]
        [SerializeField] float interiorMaxDistance = 13f;

        [Header("Seguimiento")]
        [SerializeField] float followSmoothTime = 0.25f;
        [Tooltip("Segundos de anticipación según la velocidad del jugador (mantiene espacio delante).")]
        [SerializeField] float velocityLookahead = 0.25f;
        [SerializeField] float angleSmoothTime = 0.3f;

        [Header("Girar la cámara")]
        [SerializeField] bool allowRotate = true;
        [Tooltip("Grados por píxel arrastrando con el botón central.")]
        [SerializeField] float mouseRotateSpeed = 0.3f;
        [SerializeField] float stickRotateSpeed = 90f;

        [Header("Prioridad (Cinemachine)")]
        [SerializeField] int livePriority = 10;
        [SerializeField] int idlePriority = 0;

        [Header("Objetos que tapan al jugador")]
        [SerializeField] bool hideOccluders = true;
        [SerializeField] LayerMask occluderLayers = ~0;
        [Tooltip("Objetos más grandes que esto (terreno, agua) nunca se ocultan.")]
        [SerializeField] float maxOccluderSize = 30f;
        [SerializeField] float occluderCheckInterval = 0.1f;
        [Tooltip("Tiempo sin tapar antes de volver a dibujarlo (evita parpadeos).")]
        [SerializeField] float occluderRestoreDelay = 0.35f;
        [Tooltip("Corta los edificios (CutawayZone) cuando el jugador está adentro o detrás.")]
        [SerializeField] bool cutBuildings = true;

        CinemachineCamera vcam;
        Vector3 focus, focusVelocity;
        float currentDistance, distanceVelocity, targetDistance;
        float currentPitch, pitchVelocity, currentYaw, yawVelocity;
        bool live, initialized;
        float lastPinchDistance = -1f;

        readonly List<Renderer> candidates = new();
        readonly Dictionary<Renderer, (ShadowCastingMode mode, float lastHit)> hidden = new();
        readonly List<Renderer> restoreBuffer = new();
        float occluderTimer;

        public bool IsLive => live;
        public float Yaw { get => yaw; set => yaw = value; }
        public float Distance { get => targetDistance; set => targetDistance = Mathf.Clamp(value, minDistance, maxDistance); }
        public CutawayZone CurrentZone { get; private set; }

        /// <summary>El renderer está oculto ahora mismo (tapaba al jugador o es parte de un edificio cortado).</summary>
        public bool IsHidden(Renderer r)
        {
            if (!r) return false;
            if (hidden.ContainsKey(r)) return true;
            foreach (var z in CutawayZone.All) if (z.Hides(r)) return true;
            return false;
        }

        void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            if (!player) player = FindAnyObjectByType<FirstPersonController>();
            targetDistance = currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            currentPitch = pitch;
            currentYaw = yaw;
        }

        void OnEnable()
        {
            if (player) player.Teleported += Snap;
        }

        void OnDisable()
        {
            if (player) player.Teleported -= Snap;
            RestoreAll();
        }

        void Start()
        {
            if (hideOccluders) CollectCandidates();
            Snap();
        }

        /// <summary>FirstPersonController la activa o desactiva al cambiar de vista.</summary>
        public void SetLive(bool value)
        {
            if (!vcam) vcam = GetComponent<CinemachineCamera>();
            live = value;
            vcam.Priority = value ? livePriority : idlePriority;
            if (value) Snap();
            else
            {
                RestoreAll();
                CurrentZone = null;
                foreach (var z in CutawayZone.All) z.SetCut(CutawayZone.CutLevel.None);
            }
        }

        /// <summary>Encuadra al jugador sin suavizado (inicio, teletransportes).</summary>
        public void Snap()
        {
            if (!player) return;
            focus = TargetPoint(false);
            focusVelocity = Vector3.zero;
            currentDistance = targetDistance;
            currentPitch = CurrentZone ? interiorPitch : pitch;
            currentYaw = yaw;
            initialized = true;
            ApplyTransform();
        }

        Vector3 TargetPoint(bool lookahead)
        {
            Vector3 p = player.transform.position + targetOffset;
            if (lookahead) p += player.PlanarVelocity * velocityLookahead;
            return p;
        }

        void LateUpdate()
        {
            if (!player || !initialized) return;
            float dt = Time.deltaTime;
            if (live && !GamePause.IsPaused && !FirstPersonController.UIOwnsCursor) ReadInput(dt);

            UpdateZones();
            float motion = Mathf.Lerp(0.35f, 1f, GameSettings.CameraMotion);   // "movimiento de cámara" reducido = más firme
            focus = Vector3.SmoothDamp(focus, TargetPoint(true), ref focusVelocity, followSmoothTime * motion);
            float maxD = CurrentZone ? Mathf.Min(maxDistance, interiorMaxDistance) : maxDistance;
            currentDistance = Mathf.SmoothDamp(currentDistance, Mathf.Min(targetDistance, maxD), ref distanceVelocity, zoomSmoothTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, CurrentZone ? interiorPitch : pitch, ref pitchVelocity, angleSmoothTime);
            currentYaw = Mathf.SmoothDampAngle(currentYaw, yaw, ref yawVelocity, angleSmoothTime * 0.5f);
            ApplyTransform();

            if (live && hideOccluders)
            {
                occluderTimer -= dt;
                if (occluderTimer <= 0f)
                {
                    occluderTimer = occluderCheckInterval;
                    UpdateOccluders();
                }
            }
        }

        void ApplyTransform()
        {
            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            transform.SetPositionAndRotation(focus - rot * Vector3.forward * currentDistance, rot);
        }

        // ════════════════════════ ENTRADA ════════════════════════
        void ReadInput(float dt)
        {
            var mouse = Mouse.current;
            if (GameSettings.ZoomEnabled)
            {
                float steps = 0f;
                if (mouse != null)
                {
                    float s = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(s) > 20f) s /= 120f;   // valores crudos (120 por muesca)
                    steps += Mathf.Clamp(s, -3f, 3f);
                }
                steps += PinchSteps();
                // Rueda arriba / dedos separándose = acercar.
                if (steps != 0f) targetDistance = Mathf.Clamp(targetDistance - steps * zoomStep, minDistance, maxDistance);
            }
            if (!allowRotate || player.LookLocked) return;
            if (mouse != null && mouse.middleButton.isPressed)
                yaw += mouse.delta.ReadValue().x * mouseRotateSpeed;
            if (GamepadGuard.Current != null)
                yaw += GamepadGuard.Current.rightStick.ReadValue().x * stickRotateSpeed * dt;
        }

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

        // ════════════════════════ INTERIORES ════════════════════════
        void UpdateZones()
        {
            CurrentZone = null;
            if (!live) return;
            Vector3 body = player.transform.position + Vector3.up * 0.5f;
            Vector3 chest = player.transform.position + targetOffset;
            foreach (var z in CutawayZone.All)
            {
                var level = CutawayZone.CutLevel.None;
                if (z.Contains(body)) { level = CutawayZone.CutLevel.Interior; CurrentZone = z; }
                else if (cutBuildings)
                {
                    if (z.Occludes(transform.position, chest) || z.Occludes(transform.position, body)) z.LastOccludedTime = Time.time;
                    if (Time.time - z.LastOccludedTime < occluderRestoreDelay) level = CutawayZone.CutLevel.Full;
                }
                if (!cutBuildings && level == CutawayZone.CutLevel.Interior) level = CutawayZone.CutLevel.None;
                z.SetCut(level);
            }
        }

        // ════════════════════════ OCLUSIÓN ════════════════════════
        void CollectCandidates()
        {
            candidates.Clear();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (r.transform.IsChildOf(player.transform)) continue;
                if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                if ((occluderLayers.value & (1 << r.gameObject.layer)) == 0) continue;
                if (r.bounds.size.magnitude > maxOccluderSize) continue;
                candidates.Add(r);
            }
        }

        void UpdateOccluders()
        {
            Vector3 camPos = transform.position;
            Vector3 target = player.transform.position + targetOffset;
            Vector3 feet = player.transform.position + Vector3.up * 0.3f;
            float now = Time.time;
            foreach (var r in candidates)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (OwnedByZone(r)) continue;   // los edificios se cortan por zona, no pieza por pieza
                if (Blocks(r.bounds, camPos, target) && Blocks(r.bounds, camPos, feet))
                {
                    if (!hidden.ContainsKey(r))
                    {
                        hidden[r] = (r.shadowCastingMode, now);
                        r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    }
                    else hidden[r] = (hidden[r].mode, now);
                }
            }
            restoreBuffer.Clear();
            foreach (var (r, (_, last)) in hidden)
                if (!r || now - last > occluderRestoreDelay || OwnedByZone(r)) restoreBuffer.Add(r);
            foreach (var r in restoreBuffer) Restore(r);
        }

        static bool OwnedByZone(Renderer r)
        {
            foreach (var z in CutawayZone.All) if (z.Owns(r)) return true;
            return false;
        }

        /// <summary>El segmento cámara→punto atraviesa la caja (algo encoge, para no ocultar lo que solo roza).</summary>
        static bool Blocks(Bounds b, Vector3 from, Vector3 to)
        {
            if (b.Contains(to)) return false;   // el jugador está dentro/encima: no es un "tapón"
            b.Expand(-Vector3.Min(b.size * 0.2f, Vector3.one * 0.6f));
            Vector3 d = to - from;
            float len = d.magnitude;
            return b.IntersectRay(new Ray(from, d / len), out float hit) && hit < len - 0.4f;
        }

        void Restore(Renderer r)
        {
            if (hidden.TryGetValue(r, out var h) && r) r.shadowCastingMode = h.mode;
            hidden.Remove(r);
        }

        void RestoreAll()
        {
            restoreBuffer.Clear();
            restoreBuffer.AddRange(hidden.Keys);
            foreach (var r in restoreBuffer) Restore(r);
        }

        void OnValidate()
        {
            minDistance = Mathf.Max(1f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }
}
