using System.Collections;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Sam, el hermano: sentado en el columpio → de pie mirando el río → temblando → se gira y grita → corre a la casa.
    /// Usa el modelo animado (Resources/Characters/Sam_Rig, <see cref="RiggedCharacter"/>): clips Sit, Idle,
    /// Shiver, Scream, Walk y Run. Si el FBX no está, vuelve a las poses fijas de antes.
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
        RiggedCharacter rig;

        void Awake()
        {
            rig = RiggedCharacter.Attach(gameObject, "Sam_Rig", body, body);
            // El grito no tenía clip asignado ("VO_GritoSam (asignar clip)"): se sintetiza uno.
            foreach (var src in GetComponentsInChildren<AudioSource>(true))
                if (!src.clip && src.name.StartsWith("VO_Grito"))
                {
                    src.clip = ScreamClip();
                    src.name = "VO_GritoSam";
                    src.spatialBlend = 0.85f;
                    src.volume = 1f;
                }
            if (sitPoint) transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);
            SetSitting(true);
        }

        bool HasPoses => !rig && sittingPose && standingPose;

        void ShowPose(GameObject pose)
        {
            if (!HasPoses) return;
            sittingPose.SetActive(pose == sittingPose);
            standingPose.SetActive(pose == standingPose);
            if (runningPose) runningPose.SetActive(pose == runningPose);
        }

        void SetSitting(bool sitting)
        {
            if (rig)
            {
                rig.autoLocomotion = !sitting;
                rig.SetBase(sitting ? "Sit" : "Idle", true);
                if (body) body.localPosition = Vector3.zero;
                return;
            }
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

        public void SetShaking(bool value)
        {
            shaking = value;
            if (rig) rig.SetBase(value ? "Shiver" : "Idle");
        }

        /// <summary>El grito: echa la cabeza atrás y levanta los brazos.</summary>
        public void Scream()
        {
            shaking = false;
            if (rig) rig.SetBase("Scream", true);
        }

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
            if (rig) { rig.autoLocomotion = true; rig.SetBase("Idle"); }
            else if (runningPose) ShowPose(runningPose);
            float bob = 0f;
            foreach (var p in path)
            {
                while ((transform.position - p).sqrMagnitude > 0.04f)
                {
                    Face(p);
                    transform.position = Vector3.MoveTowards(transform.position, p, runSpeed * Time.deltaTime);
                    // Rebote de carrera simple (solo sin modelo animado).
                    bob += Time.deltaTime * 11f;
                    if (body && !rig) body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bob)) * 0.06f, 0f);
                    yield return null;
                }
            }
            if (animator) animator.SetBool("Running", false);
            if (hideAtEnd) gameObject.SetActive(false);
            else SetSitting(false);
        }

        static AudioClip screamClip;

        /// <summary>
        /// Grito de un chico ("¡AAAH!") sintetizado: pulso glótico (diente de sierra) con vibrato y quiebre de voz,
        /// filtrado por los formantes de la vocal /a/ (voz joven), más aire. Sin grabaciones externas.
        /// </summary>
        public static AudioClip ScreamClip()
        {
            if (screamClip) return screamClip;
            const int rate = 22050;
            const float len = 1.25f;
            int n = (int)(rate * len);
            var data = new float[n];
            var rnd = new System.Random(3);
            // Resonadores de 2 polos para F1–F3 de /a/ (escalados para una voz de 14 años).
            float[] fq = { 950f, 1500f, 3100f }, bw = { 110f, 140f, 220f }, gain = { 1f, 0.6f, 0.25f };
            var y1 = new float[3]; var y2 = new float[3]; var a1 = new float[3]; var a2 = new float[3];
            for (int k = 0; k < 3; k++)
            {
                float r = Mathf.Exp(-Mathf.PI * bw[k] / rate);
                a1[k] = 2f * r * Mathf.Cos(2f * Mathf.PI * fq[k] / rate);
                a2[k] = -r * r;
            }
            float phase = 0f, peak = 1e-4f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Sube de golpe, se sostiene tenso y se quiebra al final.
                float f0 = 430f + 140f * Mathf.Min(1f, t * 6f) - 90f * Mathf.Max(0f, t - 0.8f) / 0.45f;
                f0 *= 1f + 0.025f * Mathf.Sin(t * 2f * Mathf.PI * 6.5f) + 0.015f * (float)(rnd.NextDouble() - 0.5);
                phase += f0 / rate;
                if (phase >= 1f) phase -= 1f;
                float glottal = (1f - 2f * phase) * 0.8f + (float)(rnd.NextDouble() * 2 - 1) * 0.25f;   // + aire
                float o = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float y = glottal * (1f - a1[k] - a2[k]) * 0.5f + a1[k] * y1[k] + a2[k] * y2[k];
                    y2[k] = y1[k]; y1[k] = y;
                    o += y * gain[k];
                }
                float env = Mathf.Min(1f, t * 30f) * Mathf.Clamp01((len - t) / 0.35f);
                data[i] = o * env;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i] / peak * 0.85f, -1f, 1f);
            screamClip = AudioClip.Create("VO_GritoSam (sintetizado)", n, 1, rate, false);
            screamClip.SetData(data, 0);
            return screamClip;
        }

        void Update()
        {
            if (!body || rig) return;                 // con modelo animado, el temblor es el clip Shiver
            body.localPosition = shaking ? bodyBase + Random.insideUnitSphere * shakeAmount : bodyBase;
        }
    }
}
