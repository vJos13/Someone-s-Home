using TMPro;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Look de cinta de 1998: maneja la intensidad del filtro VHS (<see cref="VHSRendererFeature"/>) y los "saltos de
    /// tracking" (pulsos de interferencia al empezar, al cortar de escena o cuando la entidad está cerca).
    /// También crea el OSD de la cinta (<see cref="TapeOsd"/>). Se instala solo al cargar la escena.
    /// </summary>
    public class VhsLook : MonoBehaviour
    {
        public static readonly string[] FilterNames = { "Apagado", "Suave", "Cinta VHS" };
        static readonly int AmountId = Shader.PropertyToID("_VHSAmount");
        static readonly int GlitchId = Shader.PropertyToID("_VHSGlitch");

        /// <summary>Intensidad actual del filtro (0–1).</summary>
        public static float Amount { get; private set; }
        /// <summary>Interferencia actual (0–1): pulsos + cercanía de la entidad.</summary>
        public static float Glitch { get; private set; }
        /// <summary>Que la vista de escena del Editor también lo muestre (apagado: se edita limpio).</summary>
        public static bool InSceneView;
        /// <summary>0–1: cuánto "ensucia" la señal la entidad cercana (lo pone la linterna, que ya mide la cercanía).</summary>
        public static float Haunt;

        static float pulse, pulseDecay = 1f;
        static VhsLook instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; pulse = 0f; Haunt = 0f; Amount = Glitch = 0f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var go = new GameObject("VhsLook");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<VhsLook>();
            go.AddComponent<TapeOsd>();
            go.AddComponent<LightShafts>();
            go.AddComponent<WaterReflections>();
        }

        /// <summary>Salto de tracking: la imagen se sacude y baja una banda de ruido (strength 0–1).</summary>
        public static void Pulse(float strength = 1f, float seconds = 0.6f)
        {
            pulse = Mathf.Max(pulse, Mathf.Clamp01(strength));
            pulseDecay = 1f / Mathf.Max(0.05f, seconds);
        }

        void Update()
        {
            float target = GameSettings.VhsFilter switch { 0 => 0f, 1 => 0.5f, _ => 1f };
            Amount = Mathf.MoveTowards(Amount, target, Time.unscaledDeltaTime * 2f);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * pulseDecay);
            // Con el filtro apagado no hay interferencias (la opción es para quien prefiere la imagen limpia).
            Glitch = target > 0f ? Mathf.Clamp01(pulse + Haunt * 0.35f) : 0f;
            Shader.SetGlobalFloat(AmountId, Amount);
            Shader.SetGlobalFloat(GlitchId, Glitch);
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(AmountId, 0f);
            Shader.SetGlobalFloat(GlitchId, 0f);
        }
    }

    /// <summary>Fuente monoespaciada de la época (Noto Sans Mono, OFL) para el OSD, el celular y el menú.</summary>
    public static class RetroFont
    {
        static TMP_FontAsset bold, regular;

        public static TMP_FontAsset Bold => bold ? bold : bold = Load("Fonts/NotoSansMono-Bold");
        public static TMP_FontAsset Regular => regular ? regular : regular = Load("Fonts/NotoSansMono-Regular");

        static TMP_FontAsset Load(string path)
        {
            var font = Resources.Load<Font>(path);
            if (!font) return InspectModels.Font;
            var asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            asset.name = font.name + " SDF (runtime)";
            // Lo que no tenga la fuente (▶, ❚…) sale de la fuente del HUD.
            if (InspectModels.Font) asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { InspectModels.Font };
            return asset;
        }
    }
}
