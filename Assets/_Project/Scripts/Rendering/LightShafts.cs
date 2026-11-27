using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome
{
    /// <summary>
    /// Rayos de luz entre los árboles (shader SomeonesHome/SunShafts). Calcula dónde está el sol en la imagen y cuánto
    /// se ve; el pase solo se encola cuando el sol está delante de la cámara. Más fuertes con el sol bajo (atardecer).
    /// Opción: Configuración → GRÁFICOS → "Rayos de luz".
    /// </summary>
    public class LightShafts : MonoBehaviour
    {
        static readonly int PosId = Shader.PropertyToID("_SunScreenPos");
        static readonly int ColorId = Shader.PropertyToID("_SunShaftColor");
        public static float Visibility { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Visibility = 0f;

        void LateUpdate()
        {
            var cam = Camera.main;
            var sun = RenderSettings.sun;
            float vis = 0f;
            Vector3 vp = Vector3.zero;
            Color color = Color.black;
            if (GameSettings.LightShafts && GameSettings.Shadows > 0 && cam && sun && sun.isActiveAndEnabled && sun.intensity > 0.001f)
            {
                Vector3 toSun = -sun.transform.forward;
                vp = cam.WorldToViewportPoint(cam.transform.position + toSun * 1000f);
                float facing = Vector3.Dot(cam.transform.forward, toSun);
                float elevation = Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg;
                float low = elevation < -3f ? 0f : 1f - Mathf.Clamp01((elevation - 5f) / 45f) * 0.6f;   // atardecer: más largos
                float onScreen = Mathf.Clamp01(1.4f - Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f)) * 2f);
                vis = vp.z > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, facing)) * onScreen * low : 0f;
                color = Color.Lerp(sun.color, new Color(1f, 0.78f, 0.5f), 0.4f);
                color.a = 0.9f;
            }
            Visibility = Mathf.MoveTowards(Visibility, vis, Time.unscaledDeltaTime * 3f);
            Shader.SetGlobalVector(PosId, new Vector4(vp.x, vp.y, Visibility, 0f));
            Shader.SetGlobalVector(ColorId, color);
        }
    }
}
