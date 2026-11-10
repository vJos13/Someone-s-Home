using UnityEngine;
using UnityEngine.Events;

namespace SomeonesHome
{
    /// <summary>Puerta con bisagra. El pivote del objeto debe estar en la bisagra.</summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] float openAngle = 100f;
        [SerializeField] float speed = 2.5f;
        [SerializeField] bool locked;
        [SerializeField] string openPrompt = "Abrir";
        [SerializeField] string closePrompt = "Cerrar";
        [SerializeField] AudioSource sfx;
        public UnityEvent onOpened;
        /// <summary>Cada vez que se abre (onOpened solo la primera).</summary>
        public event System.Action Opened;

        bool open;
        bool openedOnce;
        Quaternion closedRot, openRot;

        public string Prompt => open ? closePrompt : openPrompt;
        public bool CanInteract => !locked;
        public bool IsOpen => open;

        void Awake()
        {
            closedRot = transform.localRotation;
            openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        }

        public void SetLocked(bool value) => locked = value;

        public void Interact()
        {
            open = !open;
            if (sfx) sfx.Play();
            if (open) Opened?.Invoke();
            if (open && !openedOnce)
            {
                openedOnce = true;
                onOpened?.Invoke();
            }
        }

        /// <summary>Cierra sin sonido ni eventos (cambio de escena / noche).</summary>
        public void ForceClose(bool instant = true)
        {
            open = false;
            if (instant && Application.isPlaying) transform.localRotation = closedRot;
        }

        /// <summary>Abre sin sonido ni eventos (p. ej. alguien la dejó abierta).</summary>
        public void ForceOpen()
        {
            open = true;
            openedOnce = true;
        }

        void Update()
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, open ? openRot : closedRot, Time.deltaTime * speed);
        }
    }
}
