using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Genera sonidos ambientales sin archivos de audio (funciona en WebGL, que no soporta OnAudioFilterRead).
    /// Son PLACEHOLDERS: el equipo puede reemplazar el clip asignando uno real en el AudioSource
    /// y desactivando este componente.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralAudio : MonoBehaviour
    {
        public enum Kind { Wind, River, Crickets, Whisper, Creak }

        [SerializeField] Kind kind;
        [SerializeField] float seconds = 6f;
        [SerializeField] int seed = 1;
        [SerializeField] bool playOnStart = true;

        const int Rate = 22050;

        void Awake()
        {
            var src = GetComponent<AudioSource>();
            if (src.clip == null) src.clip = Build(kind, seconds, seed);
        }

        void Start()
        {
            var src = GetComponent<AudioSource>();
            if (playOnStart && !src.isPlaying) src.Play();
        }

        public static AudioClip Build(Kind kind, float seconds, int seed)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float Rand() => (float)(rng.NextDouble() * 2.0 - 1.0);

            switch (kind)
            {
                case Kind.Wind:
                {
                    // Ruido marrón con ráfagas lentas.
                    float b = 0;
                    for (int i = 0; i < n; i++)
                    {
                        b = Mathf.Clamp(b + Rand() * 0.02f, -1f, 1f);
                        float t = i / (float)Rate;
                        float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.7f + Mathf.Sin(t * 0.23f) * 2f);
                        data[i] = b * gust * 0.9f;
                    }
                    break;
                }
                case Kind.River:
                {
                    // Ruido rosa aproximado + burbujeo.
                    float b0 = 0, b1 = 0, b2 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float w = Rand();
                        b0 = 0.99765f * b0 + w * 0.0990460f;
                        b1 = 0.96300f * b1 + w * 0.2965164f;
                        b2 = 0.57000f * b2 + w * 1.0526913f;
                        float pink = (b0 + b1 + b2 + w * 0.1848f) * 0.12f;
                        float t = i / (float)Rate;
                        data[i] = pink * (0.8f + 0.2f * Mathf.Sin(t * 3.1f));
                    }
                    break;
                }
                case Kind.Crickets:
                {
                    // Chirridos: ráfagas de un tono agudo modulado.
                    float next = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        if (t >= next) next = t + 0.35f + (float)rng.NextDouble() * 0.5f;
                        float phase = (next - t);
                        float env = phase < 0.12f ? Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 40f)) * (phase / 0.12f) : 0f;
                        data[i] = Mathf.Sin(t * 2f * Mathf.PI * 4300f) * env * 0.25f;
                    }
                    break;
                }
                case Kind.Whisper:
                {
                    // Ruido filtrado con envolvente de "sílabas": A... lex...
                    float lp = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (Rand() - lp) * 0.35f;
                        float syll = Mathf.Max(0, Mathf.Sin(t * Mathf.PI * 2.2f)) * Mathf.Clamp01(1.5f - t / seconds * 1.5f + 0.5f);
                        data[i] = lp * syll * 0.8f;
                    }
                    break;
                }
                case Kind.Creak:
                {
                    // Crujido metálico lento (columpio).
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float cycle = t % 2.4f;
                        float env = cycle < 0.6f ? Mathf.Sin(cycle / 0.6f * Mathf.PI) : 0f;
                        float f = 180f + 60f * Mathf.Sin(cycle * 5f);
                        float saw = ((t * f) % 1f) * 2f - 1f;
                        data[i] = saw * env * 0.12f * (0.7f + 0.3f * Rand());
                    }
                    break;
                }
            }

            var clip = AudioClip.Create($"PROC_{kind}", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
