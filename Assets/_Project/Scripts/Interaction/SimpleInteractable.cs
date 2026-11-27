using UnityEngine;
using UnityEngine.Events;

namespace SomeonesHome
{
    /// <summary>
    /// Objeto con el que se puede interactuar con [E] (platos, bolsa, tacho, interruptor…).
    /// El director decide cuándo está disponible (Available) y qué pasa al usarlo (Used / onUse).
    /// Necesita un Collider que NO sea trigger para que el raycast del Interactor lo encuentre.
    ///
    /// Ítem agarrable: si <see cref="Item"/> no es None, [E] lo AGARRA: el objeto desaparece de la escena y entra al
    /// inventario (primer espacio libre, ya en la mano). El aviso pasa a ser "Agarrar &lt;nombre&gt;".
    /// Los objetos soltados con [Q] son copias con física que vuelven a agarrarse igual (DestroyOnPickup).
    /// </summary>
    public class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "Usar";
        [SerializeField] bool available = true;
        [SerializeField] AudioClip useSound;
        [Tooltip("Si no es None, [E] agarra el objeto hacia el inventario.")]
        [SerializeField] ItemId item = ItemId.None;
        public UnityEvent onUse;

        public event System.Action<SimpleInteractable> Used;
        public int UseCount { get; private set; }

        public ItemId Item { get => item; set => item = value; }
        /// <summary>Copia soltada en el mundo: al agarrarla se destruye (el original de la escena solo se oculta).</summary>
        public bool DestroyOnPickup { get; set; }

        public string Prompt
        {
            get
            {
                if (item == ItemId.None) return prompt;
                var def = InventorySystem.Def(item);
                return "Agarrar " + (def != null ? def.lowerName : prompt);
            }
            set => prompt = value;
        }
        public bool Available { get => available; set => available = value; }
        public bool CanInteract => available && isActiveAndEnabled;

        public void Interact()
        {
            if (!CanInteract) return;
            // Ítem: si no hay lugar en el inventario no pasa nada (lo avisa el inventario).
            if (item != ItemId.None && InventorySystem.Instance && !InventorySystem.Instance.PickUp(this)) return;
            UseCount++;
            if (useSound) OneShotAudio.Play(useSound, transform.position, 0.8f);
            Used?.Invoke(this);
            onUse?.Invoke();
            if (item != ItemId.None)
            {
                if (DestroyOnPickup) Destroy(gameObject);
                else gameObject.SetActive(false);
            }
        }
    }
}
