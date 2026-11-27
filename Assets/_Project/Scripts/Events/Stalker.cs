using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// "Acechador": la misma cosa del río, vista de lejos. Aparece donde el jugador NO está mirando,
    /// se queda quieta mientras la miras y desaparece cuando apartas la vista (nunca delante de ti).
    /// El director decide dónde (PlaceAt) y consulta si ya la vieron (SeenTime).
    ///
    /// Persecución (<see cref="ChaseStep"/>): corre hacia el jugador pegada al suelo; el modelo animado
    /// (LaCosa_Rig) pasa solo de Idle a Walk y a Run según la velocidad.
    /// </summary>
    public class Stalker : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] Transform head;
        [SerializeField] float seenAngle = 12f;

        Transform eye;
        RiggedCharacter rig;
        public bool Visible => visual && visual.gameObject.activeSelf;
        /// <summary>Segundos que el jugador la ha mirado desde que apareció.</summary>
        public float SeenTime { get; private set; }
        public bool WasSeen => SeenTime > 0.35f;
        public Vector3 HeadPosition => head ? head.position : transform.position + Vector3.up * 2.2f;
        /// <summary>Mientras persigue, el director la mueve: no se gira sola hacia el jugador.</summary>
        public bool Chasing { get; set; }

        public void Init(Transform playerEye)
        {
            eye = playerEye;
            if (!rig && visual)
            {
                var oldModel = visual.Find("LaCosa (modelo)");
                rig = RiggedCharacter.Attach(gameObject, "LaCosa_Rig", visual, oldModel);
                if (rig)
                {
                    rig.walkReference = 1.2f; rig.runReference = 5.5f; rig.runThreshold = 3f;
                    if (rig.Bone("Head")) head = rig.Bone("Head");
                }
            }
            Hide();
        }

        public void PlaceAt(Vector3 position, Vector3 faceTowards)
        {
            transform.position = position;
            Vector3 d = faceTowards - position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            SeenTime = 0f;
            if (visual) visual.gameObject.SetActive(true);
        }

        public void Hide()
        {
            Chasing = false;
            if (visual) visual.gameObject.SetActive(false);
        }

        public bool PlayerLooking(float angle) => eye && GazeUtility.IsLookingAt(eye, HeadPosition, angle, true);

        /// <summary>Un paso de persecución hacia <paramref name="target"/> a <paramref name="speed"/> m/s, sobre el terreno.</summary>
        public void ChaseStep(Vector3 target, float speed, float dt)
        {
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            Vector3 dir = d.normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 240f * dt);
            Vector3 p = transform.position + dir * Mathf.Min(speed * dt, d.magnitude);
            transform.position = Ground(p);
        }

        public static Vector3 Ground(Vector3 p)
        {
            var terrain = Terrain.activeTerrain;
            // Solo el terreno: las cercas y rocas no la levantan (las salta / las atraviesa como una sombra).
            if (terrain) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        void Update()
        {
            if (!Visible || !eye) return;
            if (PlayerLooking(seenAngle)) SeenTime += Time.deltaTime;
            if (Chasing) return;
            // Siempre mira al jugador (solo gira en Y).
            Vector3 d = eye.position - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 0.6f);
        }
    }
}
