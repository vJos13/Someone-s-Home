using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        void Interact();
    }

    /// <summary>Raycast desde la cámara: muestra "[E] ..." y ejecuta la interacción.</summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Transform rayOrigin;
        [SerializeField] float range = 2.5f;
        [SerializeField] LayerMask mask = ~0;

        FirstPersonController player;
        IInteractable current;

        void Awake() => player = GetComponent<FirstPersonController>();

        /// <summary>Lo que está apuntando ahora mismo (pruebas automáticas).</summary>
        public IInteractable Current => current;

        /// <summary>Igual que pulsar E (pruebas automáticas).</summary>
        public bool TryInteract()
        {
            if (current == null) return false;
            current.Interact();
            return true;
        }

        void Update()
        {
            current = null;
            if (player && player.InputLocked) { HUD.Instance?.SetPrompt(null); return; }

            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate != null && candidate.CanInteract) current = candidate;
            }

            HUD.Instance?.SetPrompt(current != null ? $"[E] {current.Prompt}" : null);

            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                        || (GamepadGuard.Current != null && GamepadGuard.Current.buttonSouth.wasPressedThisFrame);
            if (pressed && current != null) current.Interact();
        }
    }
}
