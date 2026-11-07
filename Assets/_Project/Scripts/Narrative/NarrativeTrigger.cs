using UnityEngine;
using UnityEngine.Events;

namespace SomeonesHome
{
    /// <summary>
    /// Volumen invisible que avisa cuando entra el jugador. Un beat narrativo ocurre una sola vez
    /// (misma idea que CinematicTrigger de la práctica S12).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NarrativeTrigger : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] bool oneShot = true;
        public UnityEvent onEnter;

        bool fired;
        public string Id => id;
        public bool PlayerInside { get; private set; }
        public event System.Action<NarrativeTrigger> Entered;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            PlayerInside = true;
            if (fired && oneShot) return;
            fired = true;
            Entered?.Invoke(this);
            onEnter?.Invoke();
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) PlayerInside = false;
        }
    }
}
