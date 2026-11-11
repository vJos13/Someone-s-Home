using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Cuerpo colgado de un álamo al otro lado del río (Relato 1, escena del río).
    /// No toca a la criatura ni al director: solo LEE el estado de RiverCreature.
    ///   - Aparece en el mismo instante que la criatura (Appear(), justo antes del plano de revelación).
    ///   - Se va con ella: cuando la criatura desaparece (Gone) y el jugador no está mirando el cuerpo,
    ///     o cuando el director apaga la criatura al pasar a la noche.
    /// Movimiento: péndulo casi imperceptible + giro lento sobre la cuerda (el viento, nada más).
    /// La geometría (pose, cuerda, soga) la genera HangedFigureSetup (Editor) a partir del modelo de Tammy.
    /// </summary>
    public class HangedFigure : MonoBehaviour
    {
        [SerializeField] RiverCreature creature;
        [Tooltip("Todo lo visible (amarre, cuerda, cuerpo). Se apaga en Awake y se enciende con la criatura.")]
        [SerializeField] GameObject visual;
        [Tooltip("Punto donde la cuerda sale de la rama: el cuerpo pendula alrededor de él.")]
        [SerializeField] Transform swingPivot;
        [Tooltip("Renderer del cuerpo (para saber si el jugador lo está mirando).")]
        [SerializeField] Renderer body;

        [Header("Movimiento (muy sutil)")]
        [Tooltip("Grados de balanceo máximo (un péndulo de ~5 m: 1° ≈ 9 cm en los pies).")]
        [SerializeField] float swayAngle = 0.7f;
        [Tooltip("Periodo del péndulo en segundos (2π·√(L/g) para la cuerda de la escena).")]
        [SerializeField] float swayPeriod = 4.6f;
        [Tooltip("Grados de giro sobre la propia cuerda.")]
        [SerializeField] float twistAngle = 9f;
        [SerializeField] float twistPeriod = 17f;

        [Header("Aparición")]
        [Tooltip("Desaparece junto con la criatura (nunca mientras el jugador lo mira).")]
        [SerializeField] bool vanishWithCreature = true;

        Quaternion pivotBase;
        Transform eye;
        bool shown, done;
        float seed;

        void Awake()
        {
            if (swingPivot) pivotBase = swingPivot.localRotation;
            seed = Random.value * 100f;
            SetVisible(false);
        }

        void Start()
        {
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player) eye = player.Head;
        }

        void Update()
        {
            if (done || !creature) return;
            bool creatureActive = creature.gameObject.activeInHierarchy;

            if (!shown)
            {
                if (creatureActive && (creature.Current == RiverCreature.State.Standing ||
                                       creature.Current == RiverCreature.State.Speaking))
                    SetVisible(true);
                return;
            }

            // Noche: el director apaga la criatura (y al jugador lo teletransporta a la casa).
            if (!creatureActive) { SetVisible(false); done = true; return; }

            if (vanishWithCreature && creature.Current == RiverCreature.State.Gone && !Observed())
            {
                SetVisible(false);
                done = true;
                return;
            }

            Sway();
        }

        void Sway()
        {
            if (!swingPivot) return;
            float t = Time.time;
            // Ráfagas: la amplitud sube y baja despacio, nunca un metrónomo.
            float gust = 0.35f + 0.65f * Mathf.PerlinNoise(seed, t * 0.07f);
            float swing = Mathf.Sin(t * 2f * Mathf.PI / swayPeriod + seed) * swayAngle * gust;
            float side = Mathf.Sin(t * 2f * Mathf.PI / (swayPeriod * 1.13f) + seed * 0.37f) * swayAngle * 0.45f * gust;
            float twist = (Mathf.PerlinNoise(seed + 7.3f, t / twistPeriod) * 2f - 1f) * twistAngle;
            swingPivot.localRotation = pivotBase * Quaternion.AngleAxis(twist, Vector3.up)
                                                 * Quaternion.Euler(swing, 0f, side);
        }

        bool Observed()
        {
            if (!eye || !visual) return false;
            Vector3 center = body ? body.bounds.center : visual.transform.position;
            return GazeUtility.IsLookingAt(eye, center, 50f, false);
        }

        void SetVisible(bool v)
        {
            shown = v;
            if (visual) visual.SetActive(v);
        }
    }
}
