using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace SomeonesHome
{
    /// <summary>
    /// Sonidos 3D de un solo disparo con Object Pooling (Semana 14, OPT-06).
    /// Reemplaza a AudioSource.PlayClipAtPoint, que en cada llamada crea un GameObject con su AudioSource
    /// y lo destruye al terminar (Instantiate + Destroy = basura para el GC). Aquí las fuentes se crean una vez,
    /// se apagan al terminar el clip y se reutilizan. Además respeta el volumen de Efectos del menú.
    /// </summary>
    public class OneShotAudio : MonoBehaviour
    {
        const int Prewarm = 4;
        const int MaxPooled = 16;

        static OneShotAudio instance;
        ObjectPool<AudioSource> pool;
        readonly List<AudioSource> playing = new(MaxPooled);

        /// <summary>Igual que AudioSource.PlayClipAtPoint, pero sin crear ni destruir objetos.</summary>
        public static void Play(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (!clip) return;
            if (!instance)
            {
                instance = new GameObject("OneShotAudio (pool)").AddComponent<OneShotAudio>();
                instance.Init();
            }
            var src = instance.pool.Get();
            src.transform.position = position;
            src.clip = clip;
            src.volume = volume * GameAudio.Scale(GameAudio.Category.Sfx);
            src.Play();
            instance.playing.Add(src);
        }

        void Init()
        {
            pool = new ObjectPool<AudioSource>(
                createFunc: Create,
                actionOnGet: s => s.gameObject.SetActive(true),
                actionOnRelease: s => { s.Stop(); s.clip = null; s.gameObject.SetActive(false); },
                actionOnDestroy: s => { if (s) Destroy(s.gameObject); },
                collectionCheck: false, defaultCapacity: Prewarm, maxSize: MaxPooled);
            // Se crean al inicio para que ninguna interacción pague el coste de crear la fuente.
            var warm = new AudioSource[Prewarm];
            for (int i = 0; i < Prewarm; i++) warm[i] = pool.Get();
            for (int i = 0; i < Prewarm; i++) pool.Release(warm[i]);
        }

        AudioSource Create()
        {
            var go = new GameObject("OneShot");
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;                       // 3D, como PlayClipAtPoint
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            return s;
        }

        void Update()
        {
            // Devuelve al pool las fuentes que ya terminaron (sin corrutinas: no generan basura).
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var s = playing[i];
                if (s && s.isPlaying) continue;
                playing.RemoveAt(i);
                if (s) pool.Release(s);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
