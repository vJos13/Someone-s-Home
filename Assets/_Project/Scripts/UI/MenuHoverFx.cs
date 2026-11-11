using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SomeonesHome
{
    /// <summary>
    /// Respuesta visual de los botones del menú (ratón y mando): el texto pasa de gris a blanco, aparece una
    /// línea fina a la izquierda y un velo muy tenue detrás. Usa tiempo sin escalar (funciona en pausa).
    /// </summary>
    public class MenuHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler
    {
        public TMP_Text label;
        public Image veil;
        public RectTransform accent;
        public Color idle = new(0.62f, 0.62f, 0.62f, 1f);
        public Color hot = new(0.96f, 0.96f, 0.94f, 1f);
        public Color disabled = new(0.32f, 0.32f, 0.32f, 1f);
        public float accentWidth = 26f;

        Selectable selectable;
        bool hover, selected;
        float t, press;

        void Awake() => selectable = GetComponent<Selectable>();

        void OnDisable() { hover = selected = false; t = 0f; Apply(); }

        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerDown(PointerEventData e) => press = 1f;

        void Update()
        {
            bool on = (hover || selected) && (!selectable || selectable.IsInteractable());
            t = Mathf.MoveTowards(t, on ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            press = Mathf.MoveTowards(press, 0f, Time.unscaledDeltaTime * 5f);
            Apply();
        }

        void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            bool enabled = !selectable || selectable.IsInteractable();
            if (label)
            {
                label.color = enabled ? Color.Lerp(idle, hot, e) : disabled;
                label.characterSpacing = Mathf.Lerp(6f, 9f, e);
            }
            if (veil) veil.color = new Color(1f, 1f, 1f, 0.06f * e + 0.05f * press);
            if (accent) accent.sizeDelta = new Vector2(accentWidth * e, accent.sizeDelta.y);
        }
    }
}
