using UnityEngine;
using UnityEngine.Events;

namespace SomeonesHome
{
    /// <summary>
    /// Objeto con el que se puede interactuar con [E] (platos, bolsa, tacho, interruptor…).
    /// El director decide cuándo está disponible (Available) y qué pasa al usarlo (Used / onUse).
    /// Necesita un Collider que NO sea trigger para que el raycast del Interactor lo encuentre.
    /// </summary>
    public class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "Usar";
        [SerializeField] bool available = true;
        [SerializeField] AudioClip useSound;
        public UnityEvent onUse;

        public event System.Action<SimpleInteractable> Used;
        public int UseCount { get; private set; }

        public string Prompt { get => prompt; set => prompt = value; }
        public bool Available { get => available; set => available = value; }
        public bool CanInteract => available && isActiveAndEnabled;

        public void Interact()
        {
            if (!CanInteract) return;
            UseCount++;
            if (useSound) AudioSource.PlayClipAtPoint(useSound, transform.position, 0.8f);
            Used?.Invoke(this);
            onUse?.Invoke();
        }
    }
}
