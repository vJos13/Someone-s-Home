using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// "Acechador": la misma cosa del río, vista de lejos. Aparece donde el jugador NO está mirando,
    /// se queda quieta mientras la miras y desaparece cuando apartas la vista (nunca delante de ti).
    /// El director decide dónde (PlaceAt) y consulta si ya la vieron (SeenTime).
    /// </summary>
    public class Stalker : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] Transform head;
        [SerializeField] float seenAngle = 12f;

        Transform eye;
        public bool Visible => visual && visual.gameObject.activeSelf;
        /// <summary>Segundos que el jugador la ha mirado desde que apareció.</summary>
        public float SeenTime { get; private set; }
        public bool WasSeen => SeenTime > 0.35f;
        public Vector3 HeadPosition => head ? head.position : transform.position + Vector3.up * 2.2f;

        public void Init(Transform playerEye)
        {
            eye = playerEye;
            Hide();
        }

        public void PlaceAt(Vector3 position, Vector3 faceTowards)
        {
            transform.position = position;
            Vector3 d = faceTowards - position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            SeenTime = 0f;
            if (visual) visual.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (visual) visual.gameObject.SetActive(false);
        }

        public bool PlayerLooking(float angle) => eye && GazeUtility.IsLookingAt(eye, HeadPosition, angle, true);

        void Update()
        {
            if (!Visible || !eye) return;
            if (PlayerLooking(seenAngle)) SeenTime += Time.deltaTime;
            // Siempre mira al jugador (solo gira en Y).
            Vector3 d = eye.position - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 0.6f);
        }
    }
}
