using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Variable global de tensión (0 = calma, 1 = terror).
    /// Controla el peso del Volume de tensión, el color/intensidad del sol, la niebla y el volumen de los grillos.
    /// Los eventos solo llaman a SetTarget(); el valor se interpola suavemente.
    /// </summary>
    public class TensionManager : MonoBehaviour
    {
        public static TensionManager Instance { get; private set; }

        [Header("Estado")]
        [Range(0, 1)] [SerializeField] float tension;
        [SerializeField] float smoothSpeed = 0.35f;

        [Header("Post-procesamiento")]
        [Tooltip("Volume global con el perfil VP_Tension. Su peso = tensión.")]
        [SerializeField] Volume tensionVolume;

        [Header("Sol (atardecer → anochecer)")]
        [SerializeField] Light sun;
        [SerializeField] Gradient sunColor;
        [SerializeField] AnimationCurve sunIntensity = AnimationCurve.Linear(0, 1.6f, 1, 0.8f);

        [Header("Niebla")]
        [SerializeField] Gradient fogColor;

        [Header("Ambiente")]
        [Tooltip("Los grillos bajan cuando sube la tensión (la ausencia de sonido asusta).")]
        [SerializeField] AudioSource crickets;
        [SerializeField] float cricketsMaxVolume = 0.35f;

        float target;
        bool cricketsSilenced;

        public float Tension => tension;

        void Awake()
        {
            Instance = this;
            target = tension;
        }

        public void SetTarget(float value) => target = Mathf.Clamp01(value);

        /// <summary>Corta los grillos de golpe (evento) o los devuelve.</summary>
        public void SilenceCrickets(bool silence) => cricketsSilenced = silence;

        void Update()
        {
            tension = Mathf.MoveTowards(tension, target, smoothSpeed * Time.deltaTime);

            if (tensionVolume) tensionVolume.weight = tension;

            if (sun)
            {
                if (sunColor != null) sun.color = sunColor.Evaluate(tension);
                sun.intensity = sunIntensity.Evaluate(tension);
            }

            if (fogColor != null && RenderSettings.fog)
                RenderSettings.fogColor = fogColor.Evaluate(tension);

            if (crickets)
            {
                float goal = cricketsSilenced ? 0f : cricketsMaxVolume * (1f - tension * 0.6f);
                // Silencio brusco, regreso lento.
                float speed = cricketsSilenced ? 3f : 0.1f;
                crickets.volume = Mathf.MoveTowards(crickets.volume, goal, speed * Time.deltaTime);
            }
        }
    }
}
