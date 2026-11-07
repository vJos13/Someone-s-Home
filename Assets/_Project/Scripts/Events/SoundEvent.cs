using System.Collections;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Evento de tensión "algo suena, ve a revisarlo", sin programar:
    ///  1. Crea un GameObject vacío DONDE debe sonar (p. ej. junto a la ventana) y añade este componente.
    ///  2. Arrastra tu audio (.wav/.ogg de Assets/_Project/Audio/SFX) a "Clip".
    ///  3. Elige cuándo se dispara: al empezar, tras X segundos, o conectando el onEnter de un NarrativeTrigger
    ///     al método SoundEvent.Play() desde el Inspector.
    /// Opcional: un pensamiento del jugador, un objetivo ("Revisa la ventana") y subir la tensión.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundEvent : MonoBehaviour
    {
        public enum When { Manual, OnStart, AfterDelay }

        [Header("Sonido")]
        [SerializeField] AudioClip clip;
        [SerializeField] [Range(0, 1)] float volume = 0.9f;
        [Tooltip("Variación aleatoria del tono para que no suene siempre igual.")]
        [SerializeField] Vector2 pitchRange = new(0.95f, 1.05f);
        [Tooltip("Distancia a la que se deja de oír (sonido 3D: se oye de dónde viene).")]
        [SerializeField] float maxDistance = 25f;

        [Header("Cuándo")]
        [SerializeField] When trigger = When.Manual;
        [SerializeField] float delay = 10f;
        [SerializeField] bool onlyOnce = true;

        [Header("Narrativa (opcional)")]
        [Tooltip("Pensamiento en cursiva tras el sonido. Vacío = nada.")]
        [SerializeField] string thought = "";
        [SerializeField] float thoughtDelay = 1.2f;
        [Tooltip("Objetivo nuevo, p. ej. 'Revisa la ventana'. Vacío = no cambia.")]
        [SerializeField] string objective = "";
        [Tooltip("Tensión 0..1 a la que lleva el evento. Negativo = no la toca.")]
        [SerializeField] float tension = -1f;

        AudioSource src;
        bool played;

        void Awake()
        {
            src = GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1.5f;
            src.maxDistance = maxDistance;
        }

        void Start()
        {
            if (trigger == When.OnStart) Play();
            else if (trigger == When.AfterDelay) StartCoroutine(Delayed());
        }

        IEnumerator Delayed()
        {
            yield return new WaitForSeconds(delay);
            Play();
        }

        /// <summary>Conectable desde UnityEvents (NarrativeTrigger.onEnter, Door.onOpened…).</summary>
        public void Play()
        {
            if (played && onlyOnce) return;
            played = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (clip)
            {
                src.pitch = Random.Range(pitchRange.x, pitchRange.y);
                src.PlayOneShot(clip, volume);
            }
            if (tension >= 0f && TensionManager.Instance) TensionManager.Instance.SetTarget(tension);
            yield return new WaitForSeconds(thoughtDelay);
            var hud = HUD.Instance;
            if (!hud) yield break;
            if (!string.IsNullOrEmpty(thought)) hud.Say("", thought);
            if (!string.IsNullOrEmpty(objective)) hud.SetObjective(objective);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
