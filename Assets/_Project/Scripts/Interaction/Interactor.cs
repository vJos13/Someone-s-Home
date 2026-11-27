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

        FirstPersonController player;
        IInteractable current;
        // Caché del último collider apuntado: GetComponentInParent recorre la jerarquía y se llamaba cada frame
        // aunque se mirara el mismo objeto (checklist Semana 14: nada de GetComponent en Update).
        Collider lastCollider;
        IInteractable lastInteractable;

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
            if (GamePause.IsPaused) return;
            current = null;
            if (player && player.InputLocked) { HUD.Instance?.SetInteraction(null); return; }

            if (GazeUtility.Raycast(rayOrigin.position, rayOrigin.forward, range, transform, out var hit))
            {
                if (hit.collider != lastCollider)
                {
                    lastCollider = hit.collider;
                    lastInteractable = lastCollider.GetComponentInParent<IInteractable>();
                }
                var candidate = lastInteractable;
                // (Object)candidate: si el interactuable se destruyó, la referencia de Unity es "null" aunque la interfaz no.
                if (candidate != null && !(candidate is Object o && !o) && candidate.CanInteract) current = candidate;
            }

            HUD.Instance?.SetInteraction(current?.Prompt);

            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                        || (GamepadGuard.Current != null && GamepadGuard.Current.buttonSouth.wasPressedThisFrame);
            if (pressed && current != null) { current.Interact(); HUD.Instance?.PulseInteraction(); }
        }
    }
}
