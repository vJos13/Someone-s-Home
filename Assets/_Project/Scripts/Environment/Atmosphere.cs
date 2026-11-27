using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

        [Header("Atardecer guiado por el relato")]
        [Tooltip("Elevación del sol al terminar el atardecer (ya bajo el horizonte: crepúsculo).")]
        [SerializeField] float duskEndElevation = -5f;
        [Tooltip("Cuánto tarda en recorrer todo el atardecer si la historia saltara de golpe (s).")]
        [SerializeField] float duskSeconds = 70f;

        [Header("Noche con luna")]
        [Tooltip("Intensidad mínima de la luz de luna (la noche se lee, sin dejar de dar miedo).")]
        [SerializeField] float moonLight = 0.9f;
        [SerializeField] Color moonTint = new(0.62f, 0.72f, 0.95f);
        [Tooltip("Ambiente mínimo de noche (cielo con luna): las siluetas se distinguen sin linterna.")]
        [SerializeField] Color nightAmbientMin = new(0.16f, 0.19f, 0.27f);
        [SerializeField] Color nightFogMin = new(0.07f, 0.09f, 0.14f);
        [SerializeField] float nightSkyExposure = 0.22f;

        [SerializeField] Light sun;

        public static Atmosphere Instance { get; private set; }

        /// <summary>
        /// Avance del atardecer según el momento del relato (0 = llegada, sol dorado · 1 = entrar a la casa,
        /// crepúsculo azul). El cielo pasa por dorado → naranja → rojo → carmesí → violeta, poco a poco.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, float> DuskByBeat = new()
        {
            { "intro", 0f }, { "llegada", 0f }, { "mochila", 0.05f }, { "explorar", 0.14f }, { "cocina", 0.14f },
            { "tareas", 0.24f }, { "vistazo", 0.3f }, { "llamar", 0.36f }, { "campo", 0.45f }, { "colina", 0.53f },
            { "columpio", 0.6f }, { "voz", 0.66f }, { "revelacion", 0.72f }, { "habla", 0.76f }, { "dialogo", 0.8f },
            { "regreso", 0.9f },
        };
        public float Dusk => dusk;
        float dusk, duskEl0, thickness0 = -1f, sunSize0 = -1f;
        Color skyTint0;
        bool hasSkyTint;
        Level1Director director;
        Volume duskGrade, moonGrade;
        ColorAdjustments duskAdjust;

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
        /// <summary>Detiene el reloj del sol (PerfBenchmark: misma luz en todas las corridas).</summary>
        [System.NonSerialized] public bool frozen;

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
            BuildDuskGrade();
        }

        /// <summary>
        /// Gradación del atardecer (Volume global creado en tiempo de ejecución): altas luces rojo-anaranjadas y
        /// sombras violetas (split toning) y un filtro rojizo. El cielo procedural de Unity se vuelve ocre con el sol
        /// bajo el horizonte; esto lo lleva al rojo/carmesí de un atardecer real. Su peso sigue a <see cref="Dusk"/>.
        /// </summary>
        void BuildDuskGrade()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "VP_Atardecer (runtime)";
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.42f, 0.34f, 0.72f));
            split.highlights.Override(new Color(1f, 0.42f, 0.26f));
            split.balance.Override(-15f);
            duskAdjust = profile.Add<ColorAdjustments>(true);
            duskAdjust.colorFilter.Override(new Color(1f, 0.84f, 0.84f));
            duskAdjust.saturation.Override(12f);
            duskAdjust.hueShift.Override(-6f);                    // ocre → naranja rojizo
            var go = new GameObject("Atardecer_Gradacion");
            go.transform.SetParent(transform, false);
            duskGrade = go.AddComponent<Volume>();
            duskGrade.isGlobal = true;
            duskGrade.priority = 20f;
            duskGrade.sharedProfile = profile;
            duskGrade.weight = 0f;

            // Noche: luz de luna fría (sombras azuladas) y un poco más de exposición, para que se vea el campo.
            var night = ScriptableObject.CreateInstance<VolumeProfile>();
            night.name = "VP_Luna (runtime)";
            var nSplit = night.Add<SplitToning>(true);
            nSplit.shadows.Override(new Color(0.36f, 0.46f, 0.78f));
            nSplit.highlights.Override(new Color(0.72f, 0.8f, 1f));
            nSplit.balance.Override(10f);
            var nAdj = night.Add<ColorAdjustments>(true);
            nAdj.postExposure.Override(0.7f);
            nAdj.saturation.Override(-30f);
            nAdj.colorFilter.Override(new Color(0.86f, 0.92f, 1f));
            var ngo = new GameObject("Luna_Gradacion");
            ngo.transform.SetParent(transform, false);
            moonGrade = ngo.AddComponent<Volume>();
            moonGrade.isGlobal = true;
            moonGrade.priority = 21f;
            moonGrade.sharedProfile = night;
            moonGrade.weight = 0f;
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
            duskEl0 = Mathf.Max(el0, 6f);
            if (p == NightMode.Phase.Dusk) dusk = 0f;

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

            if (moon)
            {
                // La luna ilumina: luz fría con un mínimo, cielo y niebla que no quedan en negro puro.
                intensity0 = Mathf.Max(intensity0, moonLight);
                color0 = Color.Lerp(color0, moonTint, 0.5f);
                shadow0 = Mathf.Max(shadow0, 0.75f);
                ambFlat0 = Max(ambFlat0, nightAmbientMin);
                ambSky0 = Max(ambSky0, nightAmbientMin * 1.2f);
                ambEq0 = Max(ambEq0, nightAmbientMin);
                ambGround0 = Max(ambGround0, nightAmbientMin * 0.5f);
                fog0 = Max(fog0, nightFogMin);
                fogDensity0 *= 0.8f;
            }

            // Copia propia del cielo: nunca se modifica el material del proyecto.
            sky = null; exposure0 = -1f;
            if (RenderSettings.skybox)
            {
                sky = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (Atmosfera)" };
                RenderSettings.skybox = sky;
                if (sky.HasProperty("_Exposure")) exposure0 = sky.GetFloat("_Exposure");
                if (moon && exposure0 >= 0f)
                {
                    exposure0 = Mathf.Max(exposure0, nightSkyExposure);
                    if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.34f, 0.39f, 0.52f));
                    if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.035f);      // disco de la luna
                    // Poca dispersión: horizonte azul de noche, sin la franja amarilla del atardecer.
                    if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.55f);
                    if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", fog0);
                }
                hasSkyTint = sky.HasProperty("_SkyTint");
                if (hasSkyTint) skyTint0 = sky.GetColor("_SkyTint");
                thickness0 = sky.HasProperty("_AtmosphereThickness") ? sky.GetFloat("_AtmosphereThickness") : -1f;
                sunSize0 = sky.HasProperty("_SunSize") ? sky.GetFloat("_SunSize") : -1f;
            }
        }

        static Color Max(Color a, Color b) => new(Mathf.Max(a.r, b.r), Mathf.Max(a.g, b.g), Mathf.Max(a.b, b.b), 1f);

        // ── Curvas del atardecer (k = avance 0..1) ──
        static Color Lerp3(Color a, Color b, Color c, float k) => k < 0.5f ? Color.Lerp(a, b, k * 2f) : Color.Lerp(b, c, k * 2f - 1f);

        /// <summary>Tinte de ambiente y niebla: neutro → cálido rojizo → violeta → azul del crepúsculo.</summary>
        static Color DuskTint(float k)
        {
            if (k < 0.55f) return Color.Lerp(Color.white, new Color(1.08f, 0.8f, 0.68f), k / 0.55f);
            if (k < 0.85f) return Color.Lerp(new Color(1.08f, 0.8f, 0.68f), new Color(0.95f, 0.62f, 0.7f), (k - 0.55f) / 0.3f);
            return Color.Lerp(new Color(0.95f, 0.62f, 0.7f), new Color(0.58f, 0.62f, 0.95f), (k - 0.85f) / 0.15f);
        }

        /// <summary>Color de la luz rasante cuando el sol ya casi no está: rojo → carmesí → violeta.</summary>
        static Color GlowColor(float elevation)
        {
            if (elevation > 0f) return Color.Lerp(new Color(1f, 0.34f, 0.16f), new Color(1f, 0.55f, 0.3f), Mathf.Clamp01(elevation / 4f));
            return Color.Lerp(new Color(0.95f, 0.3f, 0.28f), new Color(0.5f, 0.38f, 0.75f), Mathf.Clamp01(-elevation / 5f));
        }

        void Update()
        {
            if (!sun) return;

            // ── Avanza el reloj ──
            float step = frozen ? 0f : Time.deltaTime * timeScale / 3600f * rate;
            float next = hourAngle + step;
            float elNext = ElevationAt(next);
            bool hold = Phase switch
            {
                NightMode.Phase.Dusk => elNext < minDuskElevation,
                NightMode.Phase.Night => elNext > moonMaxElevation || elNext < moonMinElevation,
                _ => elNext > maxMorningElevation,
            };
            if (Phase == NightMode.Phase.Dusk)
            {
                // El atardecer avanza con la historia (y despacio): el sol se pone de verdad al volver del río.
                if (!director) director = FindAnyObjectByType<Level1Director>();
                float target = director && DuskByBeat.TryGetValue(director.Beat, out var v) ? v : dusk;
                if (director && director.Chasing) target = 0.96f;
                if (!frozen) dusk = Mathf.MoveTowards(dusk, Mathf.Max(dusk, target), Time.deltaTime / duskSeconds);
                float e = Mathf.SmoothStep(0f, 1f, dusk);
                // La gradación rojiza entra con el sol bajo y llega a su máximo cuando ya se puso.
                if (duskGrade) duskGrade.weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, dusk)) * Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.85f, 1f, dusk));
                Elevation = Mathf.Lerp(duskEl0, duskEndElevation, Mathf.Pow(e, 0.85f));
                hourAngle = HourAngleFor(Mathf.Max(Elevation, -18f), false);
            }
            else
            {
                if (duskGrade) duskGrade.weight = Mathf.MoveTowards(duskGrade.weight, 0f, Time.deltaTime * 0.5f);
                if (moonGrade) moonGrade.weight = Mathf.MoveTowards(moonGrade.weight, Phase == NightMode.Phase.Night ? 1f : 0f, Time.deltaTime * 0.4f);
                if (!hold) hourAngle = next;
                Elevation = ElevationAt(hourAngle);
            }

            float yaw = yaw0 + Mathf.DeltaAngle(az0, Azimuth(hourAngle));
            // Con el sol bajo el horizonte la luz queda rasante (resplandor del cielo del oeste), nunca desde abajo.
            sun.transform.rotation = Quaternion.Euler(Mathf.Max(Elevation, 2.5f), yaw, 0f);

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
            float glow = 0f;
            if (Phase == NightMode.Phase.Dusk)
            {
                tensionI = Mathf.Max(tensionI, 0.75f);                 // la tensión no apaga el cielo
                // Bajo los 5°: la luz se vuelve roja y, ya puesto el sol, queda un resplandor violeta tenue.
                float low = Mathf.InverseLerp(5f, -1f, Elevation);
                c = Color.Lerp(c, GlowColor(Elevation), low);
                glow = Mathf.Lerp(0.22f, 0.07f, Mathf.InverseLerp(0f, duskEndElevation, Elevation)) * Mathf.InverseLerp(4f, 0.5f, Elevation);
            }
            if (Phase == NightMode.Phase.Night) tensionI = Mathf.Max(tensionI, 0.85f);   // el miedo no apaga la luna
            sun.color = c;
            // De noche las nubes atenúan menos la luna (si no, el campo queda en negro).
            float dim = Phase == NightMode.Phase.Night ? Mathf.Max(lightDimming, 0.8f) : lightDimming;
            sun.intensity = intensity0 * (Brightness * extra + glow) * dim * tensionI;
            sun.shadowStrength = shadow0 * Mathf.Lerp(0.55f, 1f, lightDimming);

            // ── Ambiente, niebla y cielo ──
            float a = Mathf.Lerp(1f, Mathf.Max(ambientFloor, Mathf.Pow(Brightness, 0.5f)), ambientFollow);
            var duskTint = Color.white;
            if (Phase == NightMode.Phase.Dusk)
            {
                // Oscurece de a poco: el cielo sigue iluminando después de que el sol se pone.
                float k = Mathf.SmoothStep(0f, 1f, dusk);
                a = Mathf.Lerp(1f, 0.4f, k * k);
                duskTint = DuskTint(dusk);
            }
            a = Mathf.Min(a, 1.35f) * ambientDimming;
            var flashTint = new Color(0.55f, 0.6f, 0.75f) * flash;
            if (ambMode == UnityEngine.Rendering.AmbientMode.Flat)
                RenderSettings.ambientLight = ambFlat0 * duskTint * a + flashTint;
            else
            {
                RenderSettings.ambientSkyColor = ambSky0 * duskTint * a + flashTint;
                RenderSettings.ambientEquatorColor = ambEq0 * duskTint * a + flashTint * 0.7f;
                RenderSettings.ambientGroundColor = ambGround0 * duskTint * a + flashTint * 0.3f;
                RenderSettings.ambientLight = ambFlat0 * duskTint * a + flashTint;
            }
            var fogTint = tm && Phase == NightMode.Phase.Dusk ? tm.FogTint : Color.white;
            // En el atardecer la niebla oscurece igual que el cielo (si no, los árboles lejanos "brillan" contra él).
            float fogLevel = Phase == NightMode.Phase.Dusk ? a * Mathf.Lerp(1f, 0.75f, dusk) : Mathf.Lerp(a, 1f, 0.25f);
            RenderSettings.fogColor = fog0 * fogTint * duskTint * fogLevel + flashTint * 0.35f;
            RenderSettings.fogDensity = fogDensity0 * fogMultiplier * (Phase == NightMode.Phase.Dusk ? Mathf.Lerp(1f, 1.35f, dusk) : 1f);
            if (sky && exposure0 >= 0f)
            {
                float exp = Phase == NightMode.Phase.Dusk
                    ? Mathf.Lerp(1f, 0.85f, Mathf.InverseLerp(0f, 0.6f, dusk)) * Mathf.Lerp(1f, 0.42f, Mathf.InverseLerp(0.6f, 1f, dusk))
                    : Mathf.Clamp(Mathf.Pow(Brightness, 0.4f), 0.45f, 1.3f);
                sky.SetFloat("_Exposure", exposure0 * exp * Mathf.Lerp(0.7f, 1f, ambientDimming) + flash * 0.6f);
                if (Phase == NightMode.Phase.Dusk)
                {
                    // Cielo: más aire = horizonte más rojo. El tinte conserva el azul (cénit violeta, no verdoso)
                    // y termina en el azul profundo del crepúsculo.
                    if (hasSkyTint)
                        sky.SetColor("_SkyTint", Lerp3(skyTint0, new Color(0.5f, 0.34f, 0.66f), new Color(0.3f, 0.27f, 0.72f), Mathf.Clamp01(dusk * 1.05f)));
                    if (thickness0 > 0f)
                        sky.SetFloat("_AtmosphereThickness", dusk < 0.7f ? Mathf.Lerp(thickness0, 1.95f, dusk / 0.7f) : Mathf.Lerp(1.95f, 1.6f, (dusk - 0.7f) / 0.3f));
                    // El disco del sol se esconde en el horizonte (la luz rasante sigue, el disco no).
                    if (sunSize0 > 0f)
                        sky.SetFloat("_SunSize", sunSize0 * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.8f, 1.2f, Elevation)));
                }
            }
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
