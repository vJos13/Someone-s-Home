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

    /// <summary>
    /// Primera persona: raycast desde la cámara, muestra "[E] ..." y ejecuta la interacción.
    /// Isométrica: el objetivo es lo que está bajo el cursor (ClickToMove.Hovered). [E] o clic: si está al
    /// alcance se usa; si no, el jugador camina hasta él y lo usa al llegar.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Transform rayOrigin;
        [SerializeField] float range = 2.5f;

        FirstPersonController player;
        ClickToMove clickToMove;
        IInteractable current;
        Collider currentCollider;

        void Awake()
        {
            player = GetComponent<FirstPersonController>();
            clickToMove = GetComponent<ClickToMove>();
        }

        /// <summary>Lo que está apuntando ahora mismo (pruebas automáticas).</summary>
        public IInteractable Current => current;
        public float Range => range;

        bool UseCursor => player && player.IsIsometric && clickToMove && clickToMove.enabled && !player.DebugMoveInput.HasValue;

        /// <summary>Igual que pulsar E (pruebas automáticas).</summary>
        public bool TryInteract()
        {
            if (current == null) return false;
            current.Interact();
            return true;
        }

        /// <summary>¿El collider está al alcance de la mano (desde la cabeza)?</summary>
        public bool InRange(Collider c)
        {
            if (!c) return false;
            Vector3 eye = rayOrigin ? rayOrigin.position : transform.position + Vector3.up * 1.6f;
            bool supported = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mc && mc.convex);
            Vector3 p = supported ? c.ClosestPoint(eye) : c.bounds.ClosestPoint(eye);
            return Vector3.Distance(eye, p) <= range;
        }

        public void InteractWith(IInteractable target)
        {
            if (target == null || !target.CanInteract) return;
            target.Interact();
            HUD.Instance?.PulseInteraction();
        }

        /// <summary>Isométrica: usar ahora si está al alcance; si no, caminar hasta él.</summary>
        public void Request(IInteractable target, Collider col)
        {
            if (target == null || !target.CanInteract || (player && player.InputLocked)) return;
            if (!col || InRange(col) || !clickToMove) InteractWith(target);
            else clickToMove.WalkToInteract(target, col);
        }

        void Update()
        {
            if (GamePause.IsPaused) return;
            current = null;
            currentCollider = null;
            if (player && player.InputLocked) { HUD.Instance?.SetInteraction(null); return; }

            if (UseCursor)
            {
                if (clickToMove.Hovered != null && clickToMove.Hovered.CanInteract)
                {
                    current = clickToMove.Hovered;
                    currentCollider = clickToMove.HoveredCollider;
                }
            }
            else if (GazeUtility.Raycast(rayOrigin.position, rayOrigin.forward, range, transform, out var hit))
            {
                var candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate != null && candidate.CanInteract) { current = candidate; currentCollider = hit.collider; }
            }

            HUD.Instance?.SetInteraction(current?.Prompt);

            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                        || (GamepadGuard.Current != null && GamepadGuard.Current.buttonSouth.wasPressedThisFrame);
            if (!pressed || current == null) return;
            if (UseCursor) Request(current, currentCollider);
            else { current.Interact(); HUD.Instance?.PulseInteraction(); }
        }
    }
}
