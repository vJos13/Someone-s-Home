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
            if (open && !openedOnce)
            {
                openedOnce = true;
                onOpened?.Invoke();
            }
        }

        void Update()
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, open ? openRot : closedRot, Time.deltaTime * speed);
        }
    }
}
