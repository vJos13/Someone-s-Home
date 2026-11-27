using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// "Algo parecido a un venado" de pie en dos patas al otro lado del río.
    /// Regla clave del relato: solo cambia cuando NO lo miras.
    ///   Hidden → Standing → (miras a otro lado y vuelves) → Speaking → (miras a otro lado) → Gone
    /// La mandíbula (jaw) se mueve en Speaking; los ojos pueden brillar (material con emisión).
    /// Con el modelo animado (Resources/Characters/LaCosa_Rig) respira y se balancea, y la cabeza y la boca son
    /// huesos: se mueven después de la animación (LateUpdate).
    /// </summary>
    public class RiverCreature : MonoBehaviour
    {
        public enum State { Hidden, Standing, Speaking, Gone }

        [SerializeField] Transform visualRoot;
        [SerializeField] Transform jaw;
        [SerializeField] Transform head;
        [SerializeField] float jawOpenAngle = 22f;
        [SerializeField] float jawSpeed = 9f;
        [Tooltip("Gira la cabeza para seguir al jugador (muy lento: inquieta más).")]
        [SerializeField] float headTrackSpeed = 0.6f;
        [SerializeField] AudioSource whisper;

        public State Current { get; private set; } = State.Hidden;
        public Transform LookTarget => head ? head : transform;

        Transform player;
        Quaternion jawBase;
        RiggedCharacter rig;
        Quaternion headLook;
        bool headLookReady;
        Quaternion headOffset;

        void Awake()
        {
            var oldModel = visualRoot ? visualRoot.Find("LaCosa (modelo)") : null;
            rig = RiggedCharacter.Attach(gameObject, "LaCosa_Rig", visualRoot, oldModel);
            if (rig)
            {
                rig.walkReference = 1.2f; rig.runReference = 5.5f; rig.runThreshold = 3f;
                head = rig.Bone("Head") ? rig.Bone("Head") : head;
                jaw = rig.Bone("Jaw") ? rig.Bone("Jaw") : jaw;
            }
            if (jaw) jawBase = jaw.localRotation;
            SetVisible(false);
        }

        public void Init(Transform playerHead) => player = playerHead;

        public void Appear()
        {
            Current = State.Standing;
            headLookReady = false;
            SetVisible(true);
        }

        public void StartSpeaking()
        {
            Current = State.Speaking;
            if (whisper) whisper.Play();
        }

        public void Vanish()
        {
            Current = State.Gone;
            if (whisper) whisper.Stop();
            SetVisible(false);
        }

        void SetVisible(bool v)
        {
            if (visualRoot) visualRoot.gameObject.SetActive(v);
        }

        // Después de la animación: la cabeza gira muy lento hacia el jugador y la boca se abre al "hablar".
        void LateUpdate()
        {
            if (Current == State.Hidden || Current == State.Gone) return;

            if (player && head)
            {
                if (!headLookReady)
                {
                    headOffset = Quaternion.Inverse(transform.rotation) * head.rotation;
                    headLook = head.rotation;
                    headLookReady = true;
                }
                Quaternion look = Quaternion.LookRotation(player.position - head.position) * (rig ? headOffset : Quaternion.identity);
                headLook = Quaternion.Slerp(headLook, look, headTrackSpeed * Time.deltaTime);
                head.rotation = headLook;
            }

            if (jaw)
            {
                float open = Current == State.Speaking
                    ? Mathf.Abs(Mathf.Sin(Time.time * jawSpeed) * Mathf.PerlinNoise(Time.time * 3f, 0f)) * jawOpenAngle
                    : 0f;
                if (rig) jaw.rotation = Quaternion.AngleAxis(open, transform.right) * jaw.rotation;   // sobre la pose animada
                else jaw.localRotation = jawBase * Quaternion.Euler(open, 0f, 0f);
            }
        }
    }
}
