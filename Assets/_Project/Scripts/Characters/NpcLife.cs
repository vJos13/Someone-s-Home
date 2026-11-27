using System.Collections;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Vida de los personajes de la casa (Tammy, Emily): caminan entre unos puntos con pausas, miran al jugador
    /// cuando está cerca, gesticulan cuando hablan (el subtítulo los nombra) y se corren si el jugador se les
    /// pega, así nunca bloquean un paso. Tienen cuerpo sólido (cápsula): no se los atraviesa.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcLife : MonoBehaviour
    {
        public string speakerName;
        public float walkSpeed = 1.15f;
        public Vector2 pause = new(2.5f, 6f);

        RiggedCharacter rig;
        Transform player, head;
        Vector3[] route;
        int index;
        float waitUntil;
        Quaternion headOffset;
        bool headOffsetReady;
        float headWeight;
        Coroutine walking;
        CapsuleCollider body;

        /// <summary>Está caminando a un punto pedido por el relato (WalkTo).</summary>
        public bool Busy => walking != null;

        public static NpcLife Setup(GameObject host, string rigName, string speaker, params Transform[] hide)
        {
            if (!host) return null;
            var life = host.GetComponent<NpcLife>() ?? host.AddComponent<NpcLife>();
            life.speakerName = speaker;
            life.rig = RiggedCharacter.Attach(host, rigName, null, hide);
            return life;
        }

        void Start()
        {
            var fp = FindAnyObjectByType<FirstPersonController>();
            if (fp) player = fp.transform;
            if (rig) head = rig.Bone("Head");
            gameObject.layer = 2;                          // Ignore Raycast: el [E] y los rayos de suelo la atraviesan
            body = GetComponent<CapsuleCollider>();
            if (!body)
            {
                body = gameObject.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 0.85f, 0f);
                body.height = 1.7f;
                body.radius = 0.22f;
            }
        }

        /// <summary>Recorre estos puntos en bucle, con pausas (null: se queda quieta).</summary>
        public void SetRoute(params Vector3[] points)
        {
            route = points != null && points.Length > 0 ? points : null;
            index = 0;
            waitUntil = Time.time + Random.Range(pause.x, pause.y);
        }

        /// <summary>Camina hasta un punto (el relato espera con yield). Interrumpe la ruta.</summary>
        public IEnumerator WalkTo(Vector3 target, float speed = -1f)
        {
            route = null;
            if (walking != null) StopCoroutine(walking);
            var c = StartCoroutine(Walk(target, speed > 0f ? speed : walkSpeed));
            walking = c;
            yield return c;
            if (walking == c) walking = null;
        }

        public void StopWalking()
        {
            if (walking != null) StopCoroutine(walking);
            walking = null;
        }

        IEnumerator Walk(Vector3 target, float speed)
        {
            while (true)
            {
                Vector3 d = target - transform.position; d.y = 0f;
                if (d.magnitude < 0.12f) break;
                Step(d.normalized, speed);
                yield return null;
            }
        }

        void Step(Vector3 dir, float speed)
        {
            Quaternion want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 300f * Time.deltaTime);
            // No caminar de espaldas: primero gira, después avanza.
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, dir));
            Vector3 p = transform.position + dir * (speed * facing * Time.deltaTime);
            transform.position = Ground(p);
        }

        Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            return p;
        }

        int PlayerMask => player ? 1 << player.gameObject.layer : 0;

        bool Talking => !string.IsNullOrEmpty(speakerName) && HUD.CurrentSpeaker == speakerName;

        void Update()
        {
            if (!rig) return;
            rig.SetBase(Talking && rig.Has("Talk") ? "Talk" : "Idle");

            Vector3 toPlayer = player ? player.position - transform.position : Vector3.one * 99f;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Si el jugador se le pega, se hace a un lado (nunca tapa una puerta ni un pasillo).
            if (player && dist < 0.75f && walking == null)
            {
                Vector3 away = dist > 0.01f ? -toPlayer / dist : -transform.forward;
                if (!Physics.SphereCast(transform.position + Vector3.up * 0.9f, 0.2f, away, out _, 0.5f, Physics.DefaultRaycastLayers & ~PlayerMask, QueryTriggerInteraction.Ignore))
                    transform.position = Ground(transform.position + away * (1.4f * Time.deltaTime));
                return;
            }

            if (walking != null) return;
            if (Talking && player)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 120f * Time.deltaTime);
                return;
            }
            if (route == null || Time.time < waitUntil) return;
            // Con el jugador muy cerca no se mueve (no le pasa por encima).
            if (dist < 1.4f) return;
            Vector3 d = route[index] - transform.position; d.y = 0f;
            if (d.magnitude < 0.12f)
            {
                index = (index + 1) % route.Length;
                waitUntil = Time.time + Random.Range(pause.x, pause.y);
                return;
            }
            Step(d.normalized, walkSpeed);
        }

        /// <summary>La cabeza sigue al jugador cuando está cerca y delante (después de la animación).</summary>
        void LateUpdate()
        {
            if (!head || !player) return;
            if (!headOffsetReady)
            {
                headOffset = Quaternion.Inverse(transform.rotation) * head.rotation;
                headOffsetReady = true;
            }
            var cam = Camera.main ? Camera.main.transform.position : player.position + Vector3.up * 1.6f;
            Vector3 to = cam - head.position;
            float flat = Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z));
            bool want = to.magnitude < 4.5f && flat < 75f;
            headWeight = Mathf.MoveTowards(headWeight, want ? 0.75f : 0f, Time.deltaTime * 1.5f);
            if (headWeight <= 0.001f) return;
            // Rotación del hueso si el cuerpo mirara hacia el jugador, mezclada con la animada.
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up) * headOffset;
            head.rotation = Quaternion.Slerp(head.rotation, look, headWeight);
        }
    }
}
