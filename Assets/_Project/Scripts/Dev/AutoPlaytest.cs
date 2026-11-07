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
        public float turnSpeed = 240f;
        public bool run;

        public string Status { get; private set; } = "idle";
        public int Index { get; private set; }
        public int StuckCount { get; private set; }
        public float MinY { get; private set; } = float.MaxValue;

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

        void Update()
        {
            MinY = Mathf.Min(MinY, transform.position.y);
            if (transform.position.y < -5f && Status != "fell")
            {
                Debug.LogError($"[Playtest] Se cayó del mapa en {transform.position}");
                Status = "fell";
            }
            if (Status != "walking") { player.DebugMoveInput = Vector2.zero; return; }
            if (Index >= route.Count) { Status = "arrived"; player.DebugMoveInput = Vector2.zero; return; }

            Vector3 d = route[Index] - transform.position;
            d.y = 0f;
            if (d.magnitude < arriveRadius) { Index++; return; }
            if (player.InputLocked) { checkTimer = 0f; lastPos = transform.position; return; }

            var target = Quaternion.LookRotation(d);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            float angle = Quaternion.Angle(transform.rotation, target);
            player.DebugRun = run;
            player.DebugMoveInput = new Vector2(0f, angle < 35f ? 1f : 0.2f);

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
