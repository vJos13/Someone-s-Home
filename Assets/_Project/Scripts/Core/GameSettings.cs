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
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            SensitivityX = SensitivityY = 1f; InvertY = false; StickLookSpeed = 1f; ZoomEnabled = true;
            MasterVolume = 1f; MusicVolume = 0.8f; SfxVolume = 1f; AmbienceVolume = 1f;
            HeadBob = true; CameraMotion = 1f; TextSpeed = 1f; SubtitleBackground = false;
            NotifyChanged();
        }
    }
}
