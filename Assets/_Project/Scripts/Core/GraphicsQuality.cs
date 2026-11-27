using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome
{
    /// <summary>
    /// Aplica las opciones de gráficos de GameSettings (menú Configuración → GRÁFICOS / PANTALLA).
    /// "Alta" es la configuración con la que se midió el juego (ver EVIDENCIAS_OPTIMIZACION/OPTIMIZATION_LOG.md);
    /// los niveles más bajos quitan trabajo a la GPU para equipos con gráfica integrada.
    ///
    /// Se trabaja sobre una COPIA del asset de URP: en el Editor, cambiar el asset original en Play lo dejaría
    /// cambiado en disco. El SSAO es una Renderer Feature compartida; su estado original se restaura al salir.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] PresetNames = { "Baja", "Media", "Alta", "Ultra", "Personalizada" };
        public const int Custom = 4;
        public static readonly string[] ShadowNames = { "Desactivadas", "Bajas", "Medias", "Altas", "Muy altas" };
        public static readonly string[] AntiAliasNames = { "Desactivado", "FXAA", "SMAA" };
        public static readonly string[] ViewNames = { "Corta", "Normal", "Lejana" };
        public static readonly string[] TextureNames = { "Media", "Alta" };
        public static readonly string[] FpsLimitNames = { "30", "60", "120", "Sin límite" };
        static readonly int[] FpsLimits = { 30, 60, 120, -1 };

        /// <summary>Valores de cada preset: escala, sombras, SSAO, AA, pasto, distancia, texturas.</summary>
        struct Preset
        {
            public float scale; public int shadows; public bool ssao; public int aa; public float grass; public int view; public int textures;
        }

        static readonly Preset[] Presets =
        {
            new() { scale = 0.67f, shadows = 1, ssao = false, aa = 1, grass = 0.4f, view = 0, textures = 0 },   // Baja
            new() { scale = 0.83f, shadows = 2, ssao = true,  aa = 1, grass = 0.7f, view = 1, textures = 1 },   // Media
            new() { scale = 1f,    shadows = 3, ssao = true,  aa = 1, grass = 1f,   view = 1, textures = 1 },   // Alta (por defecto)
            new() { scale = 1f,    shadows = 4, ssao = true,  aa = 2, grass = 1f,   view = 2, textures = 1 },   // Ultra (4K nativo, SMAA alto)
        };
        public const int DefaultPreset = 2;

        // Sombras por nivel: distancia (m), cascadas, resolución del sol, resolución de luces adicionales (linterna).
        static readonly (float dist, int cascades, int res, int addRes)[] ShadowLevels =
        {
            (0f, 1, 512, 256), (25f, 1, 1024, 512), (40f, 2, 1024, 1024), (50f, 2, 2048, 2048), (70f, 4, 4096, 2048)
        };
        // Distancia de visión: LOD bias, error de píxel del terreno, distancia del pasto. "Normal" = valores medidos.
        static readonly (float lodBias, float pixelError, float grassDist)[] ViewLevels =
        {
            (0.8f, 10f, 30f), (1.25f, 5f, 45f), (2f, 4f, 55f)
        };

        static UniversalRenderPipelineAsset rpCopy;
        static ScriptableRendererFeature ssaoFeature;
        static bool ssaoOriginal;
        static int appliedMipLimit = int.MinValue;
        static FpsCounter fpsCounter;
        // QualitySettings es global del proyecto: en el Editor, lo cambiado en Play no se deshace solo.
        static (float lodBias, int mip, int vsync, int fps)? originalQuality;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            GameSettings.EnsureLoaded();
            GameSettings.Changed -= Apply;
            GameSettings.Changed += Apply;
            Application.quitting -= RestoreShared;
            Application.quitting += RestoreShared;
            originalQuality ??= (QualitySettings.lodBias, QualitySettings.globalTextureMipmapLimit,
                                 QualitySettings.vSyncCount, Application.targetFrameRate);
            Apply();
        }

        /// <summary>Copia los valores del preset a GameSettings (no aplica ni guarda).</summary>
        public static void LoadPreset(int index)
        {
            if (index < 0 || index >= Presets.Length) return;
            var p = Presets[index];
            GameSettings.GfxPreset = index;
            GameSettings.RenderScale = p.scale;
            GameSettings.Shadows = p.shadows;
            GameSettings.Ssao = p.ssao;
            GameSettings.AntiAliasing = p.aa;
            GameSettings.GrassDensity = p.grass;
            GameSettings.ViewDistance = p.view;
            GameSettings.Textures = p.textures;
        }

        /// <summary>El preset que coincide con los valores actuales, o Personalizada.</summary>
        public static int MatchPreset()
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                if (Mathf.Abs(p.scale - GameSettings.RenderScale) < 0.01f && p.shadows == GameSettings.Shadows &&
                    p.ssao == GameSettings.Ssao && p.aa == GameSettings.AntiAliasing &&
                    Mathf.Abs(p.grass - GameSettings.GrassDensity) < 0.01f && p.view == GameSettings.ViewDistance &&
                    p.textures == GameSettings.Textures)
                    return i;
            }
            return Custom;
        }

        public static void Apply()
        {
            var rp = RenderPipeline();
            if (rp)
            {
                rp.renderScale = Mathf.Clamp(GameSettings.RenderScale, 0.5f, 1f);
                // FSR 1 reescala mejor que el bilineal cuando se renderiza a menos resolución.
                rp.upscalingFilter = rp.renderScale < 0.99f ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
                var s = ShadowLevels[Mathf.Clamp(GameSettings.Shadows, 0, ShadowLevels.Length - 1)];
                rp.shadowDistance = s.dist;          // 0 = sin pasadas de sombras
                rp.shadowCascadeCount = s.cascades;
                rp.mainLightShadowmapResolution = s.res;
                rp.additionalLightsShadowmapResolution = s.addRes;
            }

            if (ssaoFeature) ssaoFeature.SetActive(GameSettings.Ssao);

            // 0 nada · 1 FXAA · 2 SMAA (alto en Ultra). Sin TAA: URP lo apaga con cámaras apiladas (la de la mano).
            GameSettings.AntiAliasing = Mathf.Clamp(GameSettings.AntiAliasing, 0, 2);
            var aa = (AntialiasingMode)GameSettings.AntiAliasing;
            foreach (var cam in Camera.allCameras)
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                {
                    if (data.renderType != CameraRenderType.Base) continue;
                    data.antialiasing = aa;
                    data.antialiasingQuality = GameSettings.Shadows >= 4 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
                }
            // Filtrado anisótropo forzado en Ultra: el pasto, el camino y el piso de madera nítidos a lo lejos.
            QualitySettings.anisotropicFiltering = GameSettings.Shadows >= 4 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;

            var v = ViewLevels[Mathf.Clamp(GameSettings.ViewDistance, 0, ViewLevels.Length - 1)];
            QualitySettings.lodBias = v.lodBias;
            var terrain = Terrain.activeTerrain;
            if (terrain)
            {
                terrain.heightmapPixelError = v.pixelError;
                terrain.detailObjectDistance = v.grassDist;
                terrain.detailObjectDensity = Mathf.Clamp01(GameSettings.GrassDensity);
            }

            int mip = GameSettings.Textures == 0 ? 1 : 0;   // Media = texturas a la mitad de resolución
            if (mip != appliedMipLimit) { QualitySettings.globalTextureMipmapLimit = mip; appliedMipLimit = mip; }

            if (!PerfBenchmark.Active && !NavMeshStress.Active)   // las mediciones fijan VSync y FPS por su cuenta
            {
                QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
                Application.targetFrameRate = GameSettings.VSync ? -1 : FpsLimits[Mathf.Clamp(GameSettings.FpsLimit, 0, FpsLimits.Length - 1)];
            }

            ApplyBrightness();

            if (GameSettings.ShowFps && !fpsCounter) fpsCounter = FpsCounter.Create();
            if (fpsCounter) fpsCounter.gameObject.SetActive(GameSettings.ShowFps);
        }

        // ── Brillo ──
        // Un volumen global propio con solo Lift/Gamma/Gain: el color grading de la escena (base y tensión) no se toca,
        // y el ajuste del jugador queda por encima. Vive entre escenas.
        static Volume brightnessVolume;
        static LiftGammaGain brightnessGamma;

        static void ApplyBrightness()
        {
            if (!brightnessVolume)
            {
                var go = new GameObject("Brillo (opciones del jugador)");
                Object.DontDestroyOnLoad(go);
                brightnessVolume = go.AddComponent<Volume>();
                brightnessVolume.isGlobal = true;
                brightnessVolume.priority = 50f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "VP_Brillo (runtime)";
                brightnessGamma = profile.Add<LiftGammaGain>(true);
                brightnessVolume.sharedProfile = profile;
            }
            float b = Mathf.Clamp(GameSettings.Brightness, -0.5f, 0.5f);
            brightnessGamma.gamma.Override(new Vector4(1f, 1f, 1f, b * 0.7f));
            brightnessVolume.enabled = Mathf.Abs(b) > 0.001f;
        }

        public static string BrightnessLabel(float v)
        {
            int p = Mathf.RoundToInt(v * 200f);
            return p == 0 ? "Normal" : (p > 0 ? "+" : "") + p + "%";
        }

        static UniversalRenderPipelineAsset RenderPipeline()
        {
            if (rpCopy && QualitySettings.renderPipeline == rpCopy) return rpCopy;
            var current = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (!current) return null;
            FindSsao(current);
            rpCopy = Object.Instantiate(current);
            rpCopy.name = current.name + " (opciones del jugador)";
            QualitySettings.renderPipeline = rpCopy;
            return rpCopy;
        }

        static void FindSsao(UniversalRenderPipelineAsset rp)
        {
            if (ssaoFeature) return;
            // La lista de renderers no es pública en URP 17; la copia del asset comparte los mismos renderers.
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(rp) is not ScriptableRendererData[] list) return;
            foreach (var data in list)
            {
                if (!data) continue;
                foreach (var f in data.rendererFeatures)
                    if (f is ScreenSpaceAmbientOcclusion) { ssaoFeature = f; ssaoOriginal = f.isActive; return; }
            }
        }

        static void RestoreShared()
        {
            if (ssaoFeature) ssaoFeature.SetActive(ssaoOriginal);
            if (originalQuality is { } q)
            {
                QualitySettings.lodBias = q.lodBias;
                QualitySettings.globalTextureMipmapLimit = q.mip;
                QualitySettings.vSyncCount = q.vsync;
                Application.targetFrameRate = q.fps;
            }
            originalQuality = null;
            rpCopy = null; ssaoFeature = null; appliedMipLimit = int.MinValue;
        }

        public static string ScaleLabel(float v) => Mathf.RoundToInt(v * 100f) + "%";

        /// <summary>Contador de FPS en la esquina (media de medio segundo, sin asignaciones por frame).</summary>
        sealed class FpsCounter : MonoBehaviour
        {
            TextMeshProUGUI text;
            int frames;
            float time;

            public static FpsCounter Create()
            {
                var go = new GameObject("FpsCounter");
                Object.DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                var t = new GameObject("Texto", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                t.transform.SetParent(go.transform, false);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(1f, 1f);
                t.rectTransform.anchoredPosition = new Vector2(-24f, -18f);
                t.rectTransform.sizeDelta = new Vector2(260f, 40f);
                t.alignment = TextAlignmentOptions.TopRight;
                t.fontSize = 22f;
                t.color = new Color(0.93f, 0.93f, 0.91f, 0.85f);
                t.raycastTarget = false;
                var c = go.AddComponent<FpsCounter>();
                c.text = t;
                return c;
            }

            void Update()
            {
                frames++;
                time += Time.unscaledDeltaTime;
                if (time < 0.5f) return;
                float fps = frames / time;
                text.SetText("{0:0} FPS  {1:1} ms", fps, 1000f / fps);
                frames = 0; time = 0f;
            }
        }
    }
}
