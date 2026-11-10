using System.Collections;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Sam, el hermano. Estados simples para el greybox:
    /// sentado en el columpio → de pie mirando el río → temblando → se gira y grita → corre a la casa.
    /// Cuando el equipo tenga un modelo animado, estos métodos pueden disparar triggers del Animator.
    /// </summary>
    public class BrotherNPC : MonoBehaviour
    {
        [SerializeField] Transform body;          // hijo visual (cápsula o modelo)
        [SerializeField] Transform sitPoint;
        [SerializeField] Transform standPoint;
        [SerializeField] float shakeAmount = 0.015f;
        [Tooltip("Cuánto baja el cuerpo al estar sentado (greybox).")]
        [SerializeField] float sitDrop = 0.45f;
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] Animator animator;      // opcional

        [Header("Poses del modelo (Blender). Si están, se usan en vez de bajar el cuerpo.")]
        [SerializeField] GameObject sittingPose;
        [SerializeField] GameObject standingPose;
        [SerializeField] GameObject runningPose;

        bool shaking;
        Vector3 bodyBase;

        void Awake()
        {
            if (sitPoint) transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);
            SetSitting(true);
        }

        bool HasPoses => sittingPose && standingPose;

        void ShowPose(GameObject pose)
        {
            if (!HasPoses) return;
            sittingPose.SetActive(pose == sittingPose);
            standingPose.SetActive(pose == standingPose);
            if (runningPose) runningPose.SetActive(pose == runningPose);
        }

        void SetSitting(bool sitting)
        {
            ShowPose(sitting ? sittingPose : standingPose);
            bodyBase = sitting && !HasPoses ? new Vector3(0f, -sitDrop, 0f) : Vector3.zero;
            if (body) body.localPosition = bodyBase;
            if (animator) animator.SetBool("Sitting", sitting);
        }

        public void StandAndStare(Vector3 target)
        {
            if (standPoint) transform.position = standPoint.position;
            SetSitting(false);
            Face(target);
        }

        public void SetShaking(bool value) => shaking = value;

        /// <summary>De pie en cualquier lugar (llegada, mañana siguiente).</summary>
        public void PlaceStanding(Vector3 position, Vector3 faceTowards)
        {
            gameObject.SetActive(true);
            shaking = false;
            transform.position = position;
            SetSitting(false);
            Face(faceTowards);
        }

        /// <summary>De vuelta sentado en el columpio (después de la llegada).</summary>
        public void ResetToSwing()
        {
            gameObject.SetActive(true);
            shaking = false;
            if (animator) animator.SetBool("Running", false);
            if (sitPoint) transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);
            SetSitting(true);
        }

        public void Face(Vector3 target)
        {
            Vector3 d = target - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        public IEnumerator TurnTo(Vector3 target, float time)
        {
            Quaternion from = transform.rotation;
            Vector3 d = target - transform.position; d.y = 0;
            Quaternion to = Quaternion.LookRotation(d);
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t / time));
                yield return null;
            }
            transform.rotation = to;
        }

        /// <summary>Corre siguiendo puntos (p. ej. el sendero de rocas) y desaparece al llegar.</summary>
        public IEnumerator RunAlong(Transform[] path) => RunAlong(System.Array.ConvertAll(path, p => p ? p.position : transform.position), true);

        public IEnumerator RunAlong(Vector3[] path, bool hideAtEnd)
        {
            shaking = false;
            if (animator) animator.SetBool("Running", true);
            if (runningPose) ShowPose(runningPose);
            float bob = 0f;
            foreach (var p in path)
            {
                while ((transform.position - p).sqrMagnitude > 0.04f)
                {
                    Face(p);
                    transform.position = Vector3.MoveTowards(transform.position, p, runSpeed * Time.deltaTime);
                    // Rebote de carrera simple (sin animaciones).
                    bob += Time.deltaTime * 11f;
                    if (body) body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bob)) * 0.06f, 0f);
                    yield return null;
                }
            }
            if (animator) animator.SetBool("Running", false);
            if (hideAtEnd) gameObject.SetActive(false);
            else SetSitting(false);
        }

        void Update()
        {
            if (!body) return;
            body.localPosition = shaking ? bodyBase + Random.insideUnitSphere * shakeAmount : bodyBase;
        }
    }
}
