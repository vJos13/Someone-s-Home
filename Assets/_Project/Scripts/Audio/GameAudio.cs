using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Volúmenes por categoría (General / Música / Efectos / Ambiente) sin AudioMixer, para no tocar la escena:
    ///  - General → AudioListener.volume.
    ///  - Cada AudioSource de la escena guarda su volumen de diseño y se escala por su categoría.
    ///    Categoría por nombre: "MUS_*" música, "AMB_*" ambiente, el resto efectos (voces, golpes, susurros…).
    ///  - Las fuentes cuyo volumen maneja un sistema cada frame (los grillos de TensionManager) usan Scale().
    /// También hace el fundido de entrada al cargar la escena y el de salida al reiniciar (sin cortes bruscos).
    /// Vive en el objeto de GameMenu (uno por escena).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public enum Category { Music, Sfx, Ambience }

        public static GameAudio Instance { get; private set; }

        readonly List<(AudioSource src, float baseVolume, Category cat)> sources = new();
        float fade = 0f;          // fundido global (0 = silencio)
        Coroutine fadeRoutine;

        public static Category Classify(AudioSource s)
        {
            string n = s.name;
            if (n.StartsWith("MUS_")) return Category.Music;
            if (n.StartsWith("AMB_")) return Category.Ambience;
            return Category.Sfx;
        }

        public static float Scale(Category c) => c switch
        {
            Category.Music => GameSettings.MusicVolume,
            Category.Ambience => GameSettings.AmbienceVolume,
            _ => GameSettings.SfxVolume,
        };

        void Awake()
        {
            Instance = this;
            GameSettings.EnsureLoaded();
            GameSettings.Changed += Apply;
            AudioListener.volume = 0f;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= Apply;
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            // Después de todos los Awake (HUD y Phone crean sus AudioSource ahí).
            yield return null;
            Collect();
            Apply();
            FadeTo(1f, 1.2f);
        }

        void Collect()
        {
            sources.Clear();
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                sources.Add((s, s.volume, Classify(s)));
        }

        public void Apply()
        {
            foreach (var (src, baseVolume, cat) in sources)
                if (src) src.volume = baseVolume * Scale(cat);
            AudioListener.volume = GameSettings.MasterVolume * fade;
        }

        public void FadeTo(float target, float seconds)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(target, seconds));
        }

        public IEnumerator FadeRoutine(float target, float seconds)
        {
            float from = fade;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                AudioListener.volume = GameSettings.MasterVolume * fade;
                yield return null;
            }
            fade = target;
            AudioListener.volume = GameSettings.MasterVolume * fade;
            fadeRoutine = null;
        }
    }
}
