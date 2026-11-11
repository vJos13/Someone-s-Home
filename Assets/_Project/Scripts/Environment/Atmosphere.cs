using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomeonesHome
{
    /// <summary>
    /// Sol y luna que se mueven como en el cielo real, y la luz de la escena que resulta de eso.
    ///
    /// · Posición: astronomía básica (latitud, día del año, ángulo horario). El reloj avanza a
    ///   <see cref="timeScale"/>× el tiempo de juego, así que el sol baja de verdad durante el atardecer.
    /// · Cada fase (atardecer → noche → mañana, ver <see cref="NightMode"/>) parte de la luz que ya trae
    ///   la escena o que pone NightMode: lo diseñado a mano se respeta en el primer cuadro y desde ahí evoluciona.
    /// · Color e intensidad: el sol atraviesa más aire cuanto más bajo está (masa de aire de Kasten–Young),
    ///   el azul se pierde primero → amarillo, naranja, rojo. Ambiente, niebla y cielo acompañan.
    /// · Las nubes, la lluvia y los relámpagos (<see cref="Weather"/>) atenúan o iluminan encima de esto.
    ///
    /// Se instala solo en escenas con NightMode o terreno (no hace falta tocar la escena).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class Atmosphere : MonoBehaviour
    {
        [Header("Cielo real")]
        [Tooltip("Latitud del lugar (grados, + norte).")]
        [SerializeField] float latitude = 47.4f;           // Wenatchee, Washington
        [Tooltip("Día del año (1–365). 295 ≈ fines de octubre.")]
        [SerializeField, Range(1, 365)] int dayOfYear = 295;
        [Tooltip("Declinación de la luna esa noche (grados).")]
        [SerializeField] float moonDeclination = 8f;

        [Header("Tiempo")]
        [Tooltip("Segundos de reloj por segundo de juego. 1 = tiempo real; 6 = el sol de las 18 h se pone en ~8 min.")]
        [SerializeField] float timeScale = 6f;
        [Tooltip("En el atardecer el sol baja hasta aquí y espera a que la historia pase a la noche (no deja la escena a oscuras antes de tiempo).")]
        [SerializeField] float minDuskElevation = 0.8f;
        [SerializeField] float moonMinElevation = 12f, moonMaxElevation = 65f;
        [SerializeField] float maxMorningElevation = 45f;

        [Header("Respuesta de la luz")]
        [Tooltip("Cuánto sigue el ambiente al brillo del sol (0 = fijo, 1 = proporcional).")]
        [SerializeField, Range(0, 1)] float ambientFollow = 0.55f;
        [SerializeField, Range(0, 1)] float ambientFloor = 0.35f;

        [SerializeField] Light sun;

        public static Atmosphere Instance { get; private set; }

        // ── Para Weather y otros ──
        public Light Sun => sun;
        public NightMode.Phase Phase { get; private set; } = NightMode.Phase.Dusk;
        /// <summary>Elevación actual del sol (o de la luna, de noche), en grados.</summary>
        public float Elevation { get; private set; }
        /// <summary>Dirección desde el suelo hacia el sol/luna.</summary>
        public Vector3 ToLight => sun ? -sun.transform.forward : Vector3.up;
        /// <summary>Brillo relativo al inicio de la fase (1 = como estaba diseñado).</summary>
        public float Brightness { get; private set; } = 1f;
        /// <summary>Hora solar local aproximada (solo informativa).</summary>
        public float ClockHours => 12f + hourAngle / 15f;

        // Ajustes que pone Weather.
        [System.NonSerialized] public float lightDimming = 1f;    // nubes sobre el sol
        [System.NonSerialized] public float ambientDimming = 1f;  // cielo cubierto
        [System.NonSerialized] public float fogMultiplier = 1f;   // lluvia, neblina
        [System.NonSerialized] public float flash;                // relámpago (0..1)

        // ── Estado de la fase ──
        NightMode night;
        float hourAngle, hourAngle0, declination, yaw0, az0, rate;
        bool rising;
        Color color0, modelColor0;
        float intensity0, model0, shadow0;
        UnityEngine.Rendering.AmbientMode ambMode;
        Color ambFlat0, ambSky0, ambEq0, ambGround0, fog0;
        float fogDensity0, exposure0 = -1f;
        Material sky;

        // ════════════════════════════════════════════════════════════════
        //  Instalación automática
        // ════════════════════════════════════════════════════════════════
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

        static void Ensure()
        {
            if (FindAnyObjectByType<Atmosphere>(FindObjectsInactive.Include)) return;
            if (!FindAnyObjectByType<NightMode>(FindObjectsInactive.Include) && !Terrain.activeTerrain) return;
            var go = new GameObject("Atmosfera");
            go.AddComponent<Atmosphere>();
            if (!go.GetComponent<Weather>()) go.AddComponent<Weather>();
        }

        // ════════════════════════════════════════════════════════════════
        void Awake()
        {
            Instance = this;
            if (!sun) sun = RenderSettings.sun;
            if (!sun)
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                    if (l.type == LightType.Directional) { sun = l; break; }
            NightMode.PhaseChanged += OnPhase;
        }

        void OnDestroy()
        {
            NightMode.PhaseChanged -= OnPhase;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            night = FindAnyObjectByType<NightMode>(FindObjectsInactive.Include);
            if (sun) RenderSettings.sun = sun;
            Begin(night ? night.CurrentPhase : NightMode.Phase.Dusk);
        }

        void OnPhase(NightMode n, NightMode.Phase p)
        {
            night = n;
            Begin(p);
        }

        /// <summary>Toma la luz actual como punto de partida de la fase.</summary>
        void Begin(NightMode.Phase p)
        {
            Phase = p;
            if (!sun) return;

            Vector3 f = sun.transform.forward;
            float el0 = Mathf.Asin(Mathf.Clamp(-f.y, -1f, 1f)) * Mathf.Rad2Deg;
            yaw0 = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

            bool moon = p == NightMode.Phase.Night;
            declination = moon ? moonDeclination : SolarDeclination(dayOfYear);
            rate = moon ? 14.5f : 15f;                           // grados de ángulo horario por hora
            rising = p != NightMode.Phase.Dusk;                  // la luna sale, el sol de la mañana sube
            hourAngle0 = HourAngleFor(el0, rising);
            hourAngle = hourAngle0;
            az0 = Azimuth(hourAngle0);
            Elevation = el0;

            color0 = sun.color;
            intensity0 = sun.intensity;
            shadow0 = sun.shadowStrength;
            modelColor0 = ModelColor(el0);
            model0 = Mathf.Max(1e-4f, ModelBrightness(el0));

            ambMode = RenderSettings.ambientMode;
            ambFlat0 = RenderSettings.ambientLight;
            ambSky0 = RenderSettings.ambientSkyColor;
            ambEq0 = RenderSettings.ambientEquatorColor;
            ambGround0 = RenderSettings.ambientGroundColor;
            fog0 = RenderSettings.fogColor;
            fogDensity0 = RenderSettings.fogDensity;

            // Copia propia del cielo: nunca se modifica el material del proyecto.
            sky = null; exposure0 = -1f;
            if (RenderSettings.skybox)
            {
                sky = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (Atmosfera)" };
                RenderSettings.skybox = sky;
                if (sky.HasProperty("_Exposure")) exposure0 = sky.GetFloat("_Exposure");
            }
        }

        void Update()
        {
            if (!sun) return;

            // ── Avanza el reloj ──
            float step = Time.deltaTime * timeScale / 3600f * rate;
            float next = hourAngle + step;
            float elNext = ElevationAt(next);
            bool hold = Phase switch
            {
                NightMode.Phase.Dusk => elNext < minDuskElevation,
                NightMode.Phase.Night => elNext > moonMaxElevation || elNext < moonMinElevation,
                _ => elNext > maxMorningElevation,
            };
            if (!hold) hourAngle = next;
            Elevation = ElevationAt(hourAngle);

            float yaw = yaw0 + Mathf.DeltaAngle(az0, Azimuth(hourAngle));
            sun.transform.rotation = Quaternion.Euler(Elevation, yaw, 0f);

            // ── Color e intensidad ──
            Color mc = ModelColor(Elevation);
            var c = new Color(color0.r * mc.r / Mathf.Max(1e-3f, modelColor0.r),
                              color0.g * mc.g / Mathf.Max(1e-3f, modelColor0.g),
                              color0.b * mc.b / Mathf.Max(1e-3f, modelColor0.b));
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float extra = 1f;
            if (peak > 1f) { c /= peak; extra = peak; }          // el excedente pasa a intensidad
            Brightness = ModelBrightness(Elevation) / model0;
            // Tensión de la historia: el atardecer se enrojece y la luz cae (de noche/mañana solo la intensidad).
            var tm = TensionManager.Instance;
            float tensionI = tm ? tm.SunFactor : 1f;
            if (tm && Phase == NightMode.Phase.Dusk)
            {
                c = new Color(c.r * tm.SunTint.r, c.g * tm.SunTint.g, c.b * tm.SunTint.b);
                float pk = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (pk > 1f) c /= pk;
            }
            sun.color = c;
            sun.intensity = intensity0 * Brightness * extra * lightDimming * tensionI;
            sun.shadowStrength = shadow0 * Mathf.Lerp(0.55f, 1f, lightDimming);

            // ── Ambiente, niebla y cielo ──
            float a = Mathf.Lerp(1f, Mathf.Max(ambientFloor, Mathf.Pow(Brightness, 0.5f)), ambientFollow);
            a = Mathf.Min(a, 1.35f) * ambientDimming;
            var flashTint = new Color(0.55f, 0.6f, 0.75f) * flash;
            if (ambMode == UnityEngine.Rendering.AmbientMode.Flat)
                RenderSettings.ambientLight = ambFlat0 * a + flashTint;
            else
            {
                RenderSettings.ambientSkyColor = ambSky0 * a + flashTint;
                RenderSettings.ambientEquatorColor = ambEq0 * a + flashTint * 0.7f;
                RenderSettings.ambientGroundColor = ambGround0 * a + flashTint * 0.3f;
                RenderSettings.ambientLight = ambFlat0 * a + flashTint;
            }
            var fogTint = tm && Phase == NightMode.Phase.Dusk ? tm.FogTint : Color.white;
            RenderSettings.fogColor = fog0 * fogTint * Mathf.Lerp(a, 1f, 0.25f) + flashTint * 0.35f;
            RenderSettings.fogDensity = fogDensity0 * fogMultiplier;
            if (sky && exposure0 >= 0f)
                sky.SetFloat("_Exposure", exposure0 * Mathf.Clamp(Mathf.Pow(Brightness, 0.4f), 0.45f, 1.3f) * Mathf.Lerp(0.7f, 1f, ambientDimming) + flash * 0.6f);
        }

        // ════════════════════════════════════════════════════════════════
        //  Astronomía
        // ════════════════════════════════════════════════════════════════
        static float SolarDeclination(int day) => -23.44f * Mathf.Cos(2f * Mathf.PI / 365f * (day + 10));

        float ElevationAt(float h)
        {
            float phi = latitude * Mathf.Deg2Rad, d = declination * Mathf.Deg2Rad, H = h * Mathf.Deg2Rad;
            float s = Mathf.Sin(phi) * Mathf.Sin(d) + Mathf.Cos(phi) * Mathf.Cos(d) * Mathf.Cos(H);
            return Mathf.Asin(Mathf.Clamp(s, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Acimut desde el norte, en sentido horario (grados).</summary>
        float Azimuth(float h)
        {
            float phi = latitude * Mathf.Deg2Rad, d = declination * Mathf.Deg2Rad, H = h * Mathf.Deg2Rad;
            float az = Mathf.Atan2(Mathf.Sin(H), Mathf.Cos(H) * Mathf.Sin(phi) - Mathf.Tan(d) * Mathf.Cos(phi));
            return az * Mathf.Rad2Deg + 180f;
        }

        /// <summary>Ángulo horario en que el astro tiene esa elevación (negativo = subiendo, positivo = bajando).</summary>
        float HourAngleFor(float elevation, bool goingUp)
        {
            float phi = latitude * Mathf.Deg2Rad, d = declination * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            float cosH = (Mathf.Sin(e) - Mathf.Sin(phi) * Mathf.Sin(d)) / Mathf.Max(1e-4f, Mathf.Cos(phi) * Mathf.Cos(d));
            float H = Mathf.Acos(Mathf.Clamp(cosH, -1f, 1f)) * Mathf.Rad2Deg;
            return goingUp ? -H : H;
        }

        // ════════════════════════════════════════════════════════════════
        //  Luz que atraviesa la atmósfera
        // ════════════════════════════════════════════════════════════════
        static readonly Vector3 Tau = new(0.035f, 0.075f, 0.17f);   // extinción por canal (R, G, B)

        static float AirMass(float elevationDeg)
        {
            float e = Mathf.Max(0f, elevationDeg);
            return 1f / (Mathf.Sin(e * Mathf.Deg2Rad) + 0.50572f * Mathf.Pow(e + 6.07995f, -1.6364f));
        }

        static Color Transmittance(float elevationDeg)
        {
            float m = AirMass(elevationDeg);
            return new Color(Mathf.Exp(-Tau.x * m), Mathf.Exp(-Tau.y * m), Mathf.Exp(-Tau.z * m));
        }

        static Color ModelColor(float elevationDeg)
        {
            var t = Transmittance(elevationDeg);
            float mx = Mathf.Max(1e-4f, Mathf.Max(t.r, Mathf.Max(t.g, t.b)));
            return new Color(t.r / mx, t.g / mx, t.b / mx);
        }

        static float ModelBrightness(float elevationDeg)
        {
            var t = Transmittance(elevationDeg);
            float lum = 0.2126f * t.r + 0.7152f * t.g + 0.0722f * t.b;
            // El disco se esconde tras el horizonte (y tras cerros y árboles) en los últimos grados.
            float horizon = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 2.5f, elevationDeg));
            return lum * horizon;
        }
    }
}
