using System;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Opciones del jugador (menú Configuración). Se guardan con PlayerPrefs: en WebGL van a IndexedDB del
    /// navegador, sin rutas de archivos. Los sistemas las leen directamente (FirstPersonController, HUD, GameAudio).
    /// </summary>
    public static class GameSettings
    {
        // ── Cámara ──
        /// <summary>Multiplicador de la sensibilidad horizontal del ratón (1 = valor del controlador).</summary>
        public static float SensitivityX = 1f;
        public static float SensitivityY = 1f;
        public static bool InvertY;
        /// <summary>Velocidad de giro con el stick del mando (multiplicador).</summary>
        public static float StickLookSpeed = 1f;
        public static bool ZoomEnabled = true;

        // ── Audio (0..1) ──
        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.8f;
        public static float SfxVolume = 1f;
        public static float AmbienceVolume = 1f;

        // ── Accesibilidad ──
        public static bool HeadBob = true;
        /// <summary>Intensidad del movimiento de cámara (head bob, transiciones de zoom). 0..1.</summary>
        public static float CameraMotion = 1f;
        /// <summary>Multiplicador de la velocidad de escritura de diálogos y tarjetas.</summary>
        public static float TextSpeed = 1f;
        /// <summary>Banda oscura detrás de los subtítulos (legibilidad).</summary>
        public static bool SubtitleBackground;

        // ── Gráficos (los aplica GraphicsQuality; los valores por defecto son el preset "Alta") ──
        /// <summary>0 Baja · 1 Media · 2 Alta · 3 Ultra · 4 Personalizada.</summary>
        public static int GfxPreset = 2;
        /// <summary>Escala de resolución del render 3D (0,5..1). La interfaz siempre va a resolución completa.</summary>
        public static float RenderScale = 1f;
        /// <summary>0 Desactivadas · 1 Bajas · 2 Medias · 3 Altas · 4 Muy altas.</summary>
        public static int Shadows = 3;
        /// <summary>Oclusión ambiental (SSAO).</summary>
        public static bool Ssao = true;
        /// <summary>0 Desactivado · 1 FXAA · 2 SMAA.</summary>
        public static int AntiAliasing = 1;
        /// <summary>Densidad del pasto (0,2..1).</summary>
        public static float GrassDensity = 1f;
        /// <summary>0 Corta · 1 Normal · 2 Lejana (LOD, terreno y distancia del pasto).</summary>
        public static int ViewDistance = 1;
        /// <summary>0 Media (mitad de resolución) · 1 Alta.</summary>
        public static int Textures = 1;

        // ── Pantalla ──
        public static bool VSync = true;
        /// <summary>Límite de FPS sin VSync: 0 = 30 · 1 = 60 · 2 = 120 · 3 = sin límite.</summary>
        public static int FpsLimit = 3;
        public static bool ShowFps;
        /// <summary>Brillo (gamma de los medios tonos): -0,5 más oscuro · 0 como se diseñó · +0,5 más claro.</summary>
        public static float Brightness;
        /// <summary>Filtro de cinta: 0 = apagado · 1 = suave · 2 = cinta VHS (el look del juego).</summary>
        public static int VhsFilter = 1;
        /// <summary>Indicadores de la cinta en pantalla (PLAY, contador, fecha y hora).</summary>
        public static bool TapeOsd = true;
        /// <summary>Rayos de sol entre los árboles (pase de pantalla).</summary>
        public static bool LightShafts = true;
        /// <summary>Reflejos en tiempo real del río (sonda de reflexión que se actualiza con la hora del relato).</summary>
        public static bool WaterReflections = true;

        public static event Action Changed;

        const string Prefix = "SH.";
        static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            SensitivityX = PlayerPrefs.GetFloat(Prefix + "sensX", SensitivityX);
            SensitivityY = PlayerPrefs.GetFloat(Prefix + "sensY", SensitivityY);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", InvertY ? 1 : 0) == 1;
            StickLookSpeed = PlayerPrefs.GetFloat(Prefix + "stick", StickLookSpeed);
            ZoomEnabled = PlayerPrefs.GetInt(Prefix + "zoom", ZoomEnabled ? 1 : 0) == 1;
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "volMaster", MasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "volMusic", MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "volSfx", SfxVolume);
            AmbienceVolume = PlayerPrefs.GetFloat(Prefix + "volAmb", AmbienceVolume);
            HeadBob = PlayerPrefs.GetInt(Prefix + "bob", HeadBob ? 1 : 0) == 1;
            CameraMotion = PlayerPrefs.GetFloat(Prefix + "motion", CameraMotion);
            TextSpeed = PlayerPrefs.GetFloat(Prefix + "textSpeed", TextSpeed);
            SubtitleBackground = PlayerPrefs.GetInt(Prefix + "subBg", SubtitleBackground ? 1 : 0) == 1;
            GfxPreset = PlayerPrefs.GetInt(Prefix + "gfxPreset", GfxPreset);
            RenderScale = PlayerPrefs.GetFloat(Prefix + "renderScale", RenderScale);
            Shadows = PlayerPrefs.GetInt(Prefix + "shadows", Shadows);
            Ssao = PlayerPrefs.GetInt(Prefix + "ssao", Ssao ? 1 : 0) == 1;
            AntiAliasing = PlayerPrefs.GetInt(Prefix + "aa", AntiAliasing);
            GrassDensity = PlayerPrefs.GetFloat(Prefix + "grass", GrassDensity);
            ViewDistance = PlayerPrefs.GetInt(Prefix + "view", ViewDistance);
            Textures = PlayerPrefs.GetInt(Prefix + "textures", Textures);
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", VSync ? 1 : 0) == 1;
            FpsLimit = PlayerPrefs.GetInt(Prefix + "fpsLimit", FpsLimit);
            ShowFps = PlayerPrefs.GetInt(Prefix + "showFps", ShowFps ? 1 : 0) == 1;
            Brightness = PlayerPrefs.GetFloat(Prefix + "bright", Brightness);
            VhsFilter = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "vhs", VhsFilter), 0, 2);
            TapeOsd = PlayerPrefs.GetInt(Prefix + "osd", TapeOsd ? 1 : 0) == 1;
            LightShafts = PlayerPrefs.GetInt(Prefix + "shafts", LightShafts ? 1 : 0) == 1;
            WaterReflections = PlayerPrefs.GetInt(Prefix + "waterRefl", WaterReflections ? 1 : 0) == 1;
        }

        /// <summary>Avisa a los sistemas (audio, HUD) que algo cambió. No escribe a disco.</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "sensX", SensitivityX);
            PlayerPrefs.SetFloat(Prefix + "sensY", SensitivityY);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "stick", StickLookSpeed);
            PlayerPrefs.SetInt(Prefix + "zoom", ZoomEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "volMaster", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "volMusic", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "volSfx", SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "volAmb", AmbienceVolume);
            PlayerPrefs.SetInt(Prefix + "bob", HeadBob ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "motion", CameraMotion);
            PlayerPrefs.SetFloat(Prefix + "textSpeed", TextSpeed);
            PlayerPrefs.SetInt(Prefix + "subBg", SubtitleBackground ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "gfxPreset", GfxPreset);
            PlayerPrefs.SetFloat(Prefix + "renderScale", RenderScale);
            PlayerPrefs.SetInt(Prefix + "shadows", Shadows);
            PlayerPrefs.SetInt(Prefix + "ssao", Ssao ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "aa", AntiAliasing);
            PlayerPrefs.SetFloat(Prefix + "grass", GrassDensity);
            PlayerPrefs.SetInt(Prefix + "view", ViewDistance);
            PlayerPrefs.SetInt(Prefix + "textures", Textures);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "fpsLimit", FpsLimit);
            PlayerPrefs.SetInt(Prefix + "showFps", ShowFps ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "bright", Brightness);
            PlayerPrefs.SetInt(Prefix + "vhs", VhsFilter);
            PlayerPrefs.SetInt(Prefix + "osd", TapeOsd ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "shafts", LightShafts ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "waterRefl", WaterReflections ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            SensitivityX = SensitivityY = 1f; InvertY = false; StickLookSpeed = 1f; ZoomEnabled = true;
            MasterVolume = 1f; MusicVolume = 0.8f; SfxVolume = 1f; AmbienceVolume = 1f;
            HeadBob = true; CameraMotion = 1f; TextSpeed = 1f; SubtitleBackground = false;
            GraphicsQuality.LoadPreset(GraphicsQuality.DefaultPreset);
            VSync = true; FpsLimit = 3; ShowFps = false; Brightness = 0f; VhsFilter = 1; TapeOsd = true; LightShafts = true; WaterReflections = true;
            NotifyChanged();
        }
    }
}
