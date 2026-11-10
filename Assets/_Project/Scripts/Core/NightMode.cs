using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Cambia la luz de la escena sin hornear luces nuevas:
    /// atardecer (lo que trae la escena) → noche (luna fría, niebla oscura) → mañana (sol bajo del este, neblina).
    /// </summary>
    public class NightMode : MonoBehaviour
    {
        [SerializeField] Light sun;
        [Header("Noche")]
        [SerializeField] Color moonColor = new(0.45f, 0.55f, 0.8f);
        [SerializeField] float moonIntensity = 0.12f;
        [SerializeField] Vector3 moonRotation = new(38f, -150f, 0f);
        [SerializeField] Color nightAmbient = new(0.035f, 0.045f, 0.07f);
        [SerializeField] Color nightFog = new(0.03f, 0.04f, 0.06f);
        [SerializeField] float nightFogDensity = 0.03f;
        [SerializeField] float skyExposure = 0.08f;
        [Header("Mañana")]
        [SerializeField] Color morningSun = new(1f, 0.88f, 0.74f);
        [SerializeField] Vector3 morningRotation = new(14f, -100f, 0f);
        [SerializeField] Color morningFogTint = new(0.72f, 0.76f, 0.8f);

        public bool IsNight { get; private set; }

        // Estado del atardecer (para la mañana).
        bool captured;
        Color daySunColor, dayAmbient, dayFog, daySky, dayEquator, dayGround;
        float daySunIntensity, dayFogDensity, dayAmbientIntensity;
        Quaternion daySunRotation;
        UnityEngine.Rendering.AmbientMode dayAmbientMode;
        Material daySkybox;

        void Awake() => Capture();

        void FindSun()
        {
            if (sun) return;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional) { sun = l; break; }
        }

        void Capture()
        {
            if (captured) return;
            FindSun();
            captured = true;
            if (sun) { daySunColor = sun.color; daySunIntensity = sun.intensity; daySunRotation = sun.transform.rotation; }
            dayAmbientMode = RenderSettings.ambientMode;
            dayAmbient = RenderSettings.ambientLight;
            daySky = RenderSettings.ambientSkyColor;
            dayEquator = RenderSettings.ambientEquatorColor;
            dayGround = RenderSettings.ambientGroundColor;
            dayAmbientIntensity = RenderSettings.ambientIntensity;
            dayFog = RenderSettings.fogColor;
            dayFogDensity = RenderSettings.fogDensity;
            daySkybox = RenderSettings.skybox;
        }

        public void Apply()
        {
            Capture();
            IsNight = true;
            if (sun)
            {
                sun.color = moonColor;
                sun.intensity = moonIntensity;
                sun.transform.rotation = Quaternion.Euler(moonRotation);
                sun.shadowStrength = 0.6f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = nightAmbient;
            RenderSettings.fogColor = nightFog;
            RenderSettings.fogDensity = nightFogDensity;
            if (daySkybox)
            {
                // Instancia: no se toca el material del proyecto.
                var sky = new Material(daySkybox);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", skyExposure);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.2f, 0.25f, 0.4f));
                if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.02f);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", nightFog);
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>La mañana siguiente: luz pálida y baja desde el este, neblina fría.</summary>
        public void Morning()
        {
            Capture();
            IsNight = false;
            if (sun)
            {
                sun.color = morningSun;
                sun.intensity = daySunIntensity * 0.85f;
                sun.transform.rotation = Quaternion.Euler(morningRotation);
                sun.shadowStrength = 0.8f;
            }
            RenderSettings.ambientMode = dayAmbientMode;
            RenderSettings.ambientLight = dayAmbient * 0.9f;
            RenderSettings.ambientSkyColor = Color.Lerp(daySky, morningFogTint, 0.4f);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayEquator, morningFogTint, 0.4f);
            RenderSettings.ambientGroundColor = dayGround;
            RenderSettings.ambientIntensity = dayAmbientIntensity;
            var fog = Color.Lerp(dayFog, morningFogTint, 0.65f);
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = dayFogDensity * 1.4f;
            if (daySkybox)
            {
                var sky = new Material(daySkybox);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.55f, 0.6f, 0.68f));
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", sky.GetFloat("_Exposure") * 0.9f);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", fog);
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
