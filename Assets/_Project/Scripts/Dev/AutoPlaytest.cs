using System.Collections.Generic;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Bot de pruebas: camina por una ruta de puntos usando el mismo controlador que el jugador
    /// (colisiones, gravedad y pendientes reales) y avisa si se queda atascado o se cae del mapa.
    /// No está en la escena: se añade en Play desde código (Unity Pipeline / consola de pruebas).
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class AutoPlaytest : MonoBehaviour
    {
        public float arriveRadius = 0.7f;
        public float turnSpeed = 140f;
        public bool run;

        public string Status { get; private set; } = "idle";
        public int Index { get; private set; }
        public int StuckCount { get; private set; }
        public float MinY { get; private set; } = float.MaxValue;

        /// <summary>Si tiene valor (y no está caminando), gira la vista suavemente hacia ese punto.</summary>
        public Vector3? LookAtPoint { get; set; }
        public float lookSpeed = 70f;

        readonly List<Vector3> route = new();
        FirstPersonController player;
        Vector3 lastPos;
        float checkTimer;

        void Awake() => player = GetComponent<FirstPersonController>();

        public void Go(params Vector3[] points)
        {
            route.Clear();
            route.AddRange(points);
            Index = 0;
            Status = route.Count > 0 ? "walking" : "arrived";
            checkTimer = 0f;
            lastPos = transform.position;
        }

        public void Stop()
        {
            Status = "idle";
            player.DebugMoveInput = Vector2.zero;
            player.DebugRun = false;
        }

        void OnDisable()
        {
            if (player) { player.DebugMoveInput = null; player.DebugRun = false; }
        }

        /// <summary>true cuando la vista ya apunta al punto (±2°).</summary>
        public bool LookDone(Vector3 p)
        {
            Vector3 d = p - player.Head.position;
            float yawErr = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg));
            float pitchErr = Mathf.Abs(Mathf.DeltaAngle(player.Pitch, TargetPitch(d)));
            return yawErr < 2f && pitchErr < 2f;
        }

        static float TargetPitch(Vector3 d) => -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;

        void SmoothLook(Vector3 p)
        {
            Vector3 d = p - player.Head.position;
            if (new Vector2(d.x, d.z).sqrMagnitude < 0.01f) return;
            float targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float yaw = transform.eulerAngles.y;
            // Arranque y frenado suaves: la velocidad baja al acercarse al objetivo.
            float err = Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw));
            float step = Mathf.Min(lookSpeed, 15f + err * 2.2f) * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(yaw, targetYaw, step), 0f);
            player.Pitch = Mathf.MoveTowardsAngle(player.Pitch, TargetPitch(d), step * 0.7f);
        }

        void Update()
        {
            MinY = Mathf.Min(MinY, transform.position.y);
            if (transform.position.y < -5f && Status != "fell")
            {
                Debug.LogError($"[Playtest] Se cayó del mapa en {transform.position}");
                Status = "fell";
            }
            if (Status != "walking")
            {
                player.DebugMoveInput = Vector2.zero;
                if (LookAtPoint.HasValue && !player.LookLocked && !player.InputLocked) SmoothLook(LookAtPoint.Value);
                return;
            }
            if (Index >= route.Count) { Status = "arrived"; player.DebugMoveInput = Vector2.zero; return; }

            Vector3 d = route[Index] - transform.position;
            d.y = 0f;
            if (d.magnitude < arriveRadius) { Index++; return; }
            if (player.InputLocked) { checkTimer = 0f; lastPos = transform.position; return; }

            var target = Quaternion.LookRotation(d);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            // Como un jugador con WASD: se mueve hacia el punto aunque aún esté girando (no orbita).
            Vector3 local = transform.InverseTransformDirection(d.normalized);
            player.DebugRun = run;
            player.DebugMoveInput = new Vector2(local.x, local.z).normalized;
            player.Pitch = Mathf.MoveTowards(player.Pitch, 4f, 40f * Time.deltaTime);

            checkTimer += Time.deltaTime;
            if (checkTimer > 3f)
            {
                if (Vector3.Distance(transform.position, lastPos) < 0.3f)
                {
                    StuckCount++;
                    Status = "stuck";
                    Debug.LogWarning($"[Playtest] Atascado en {transform.position} rumbo al punto {Index} {route[Index]}");
                }
                checkTimer = 0f;
                lastPos = transform.position;
            }
        }
    }
}
