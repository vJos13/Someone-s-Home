using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// "Algo parecido a un venado" de pie en dos patas al otro lado del río.
    /// Regla clave del relato: solo cambia cuando NO lo miras.
    ///   Hidden → Standing → (miras a otro lado y vuelves) → Speaking → (miras a otro lado) → Gone
    /// La mandíbula (jaw) se mueve en Speaking; los ojos pueden brillar (material con emisión).
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

        void Awake()
        {
            if (jaw) jawBase = jaw.localRotation;
            SetVisible(false);
        }

        public void Init(Transform playerHead) => player = playerHead;

        public void Appear()
        {
            Current = State.Standing;
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

        void Update()
        {
            if (Current == State.Hidden || Current == State.Gone) return;

            if (player && head)
            {
                Quaternion look = Quaternion.LookRotation(player.position - head.position);
                head.rotation = Quaternion.Slerp(head.rotation, look, headTrackSpeed * Time.deltaTime);
            }

            if (jaw)
            {
                float open = Current == State.Speaking
                    ? Mathf.Abs(Mathf.Sin(Time.time * jawSpeed) * Mathf.PerlinNoise(Time.time * 3f, 0f)) * jawOpenAngle
                    : 0f;
                jaw.localRotation = jawBase * Quaternion.Euler(open, 0f, 0f);
            }
        }
    }
}
