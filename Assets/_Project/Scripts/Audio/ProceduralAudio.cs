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
        // Los nuevos van AL FINAL: el enum se guarda como número en la escena.
        public enum Kind { Wind, River, Crickets, Whisper, Creak, Bark, Knock, Buzz, Ring, Rattle, Step, Horn, Click }

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
                case Kind.Bark:
                {
                    // Un ladrido: tono que cae (600→300 Hz) con armónicos + ruido, envolvente seca.
                    float ph = 0, lp = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float u = t / seconds;
                        float f = Mathf.Lerp(620f, 300f, Mathf.Sqrt(u));
                        ph += f / Rate;
                        float tone = Mathf.Sin(ph * 2f * Mathf.PI) + 0.6f * Mathf.Sin(ph * 4f * Mathf.PI) + 0.35f * Mathf.Sin(ph * 6f * Mathf.PI);
                        lp += (Rand() - lp) * 0.5f;
                        float env = Mathf.Min(1f, t / 0.012f) * Mathf.Pow(1f - u, 1.6f);
                        data[i] = Mathf.Clamp((tone * 0.55f + lp * 0.7f) * env, -1f, 1f) * 0.6f;
                    }
                    break;
                }
                case Kind.Knock:
                {
                    // Tres golpes en madera: golpe grave (95 Hz) + clic.
                    float[] at = { 0f, 0.26f, 0.5f };
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float v = 0f;
                        foreach (float a in at)
                        {
                            float d = t - a;
                            if (d < 0f || d > 0.25f) continue;
                            v += Mathf.Sin(d * 2f * Mathf.PI * 95f) * Mathf.Exp(-d * 28f) * 0.9f
                               + Mathf.Sin(d * 2f * Mathf.PI * 210f) * Mathf.Exp(-d * 45f) * 0.4f
                               + Rand() * Mathf.Exp(-d * 400f) * 0.5f;
                        }
                        data[i] = Mathf.Clamp(v, -1f, 1f);
                    }
                    break;
                }
                case Kind.Buzz:
                {
                    // Vibración de celular: dos pulsos de 160 Hz con zumbido.
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        bool on = (t < 0.38f) || (t > 0.55f && t < 0.93f);
                        float env = on ? 1f : 0f;
                        float sq = Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 160f)) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 320f) * 0.3f;
                        data[i] = sq * env * 0.35f;
                    }
                    break;
                }
                case Kind.Ring:
                {
                    // Tono de llamada (440 + 480 Hz, 2 s sonando / 4 s de silencio, como en EE. UU.).
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float c = t % 6f;
                        float env = c < 2f ? Mathf.Min(1f, c / 0.02f) * Mathf.Min(1f, (2f - c) / 0.02f) : 0f;
                        data[i] = (Mathf.Sin(t * 2f * Mathf.PI * 440f) + Mathf.Sin(t * 2f * Mathf.PI * 480f)) * 0.15f * env;
                    }
                    break;
                }
                case Kind.Rattle:
                {
                    // Manija que alguien sacude: clics metálicos rápidos e irregulares.
                    float next = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        if (t >= next) next = t + 0.06f + (float)rng.NextDouble() * 0.09f;
                        float d = 0.15f - (next - t);
                        float click = d >= 0f ? Mathf.Exp(-d * 120f) : 0f;
                        float body = Mathf.Sin(t * 2f * Mathf.PI * 1850f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 2730f) * 0.3f;
                        data[i] = (body + Rand() * 0.6f) * click * 0.5f * Mathf.Clamp01((seconds - t) * 4f);
                    }
                    break;
                }
                case Kind.Step:
                {
                    // Un paso sobre pasto seco: crujido corto de ruido filtrado.
                    float lp = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (Rand() - lp) * 0.25f;
                        float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 14f) * (0.6f + 0.4f * Mathf.Abs(Rand()));
                        data[i] = lp * env * 1.4f;
                    }
                    break;
                }
                case Kind.Horn:
                {
                    // Bocina de camioneta: dos toques (acorde 350 + 440 Hz con armónicos).
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        bool on = t < 0.35f || (t > 0.5f && t < 1.25f);
                        float env = on ? 1f : 0f;
                        float w = 0f;
                        foreach (float f in new[] { 350f, 440f })
                            w += Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * f)) * 0.3f + Mathf.Sin(t * 2f * Mathf.PI * f * 2f) * 0.15f;
                        data[i] = w * env * 0.3f;
                    }
                    break;
                }
                case Kind.Click:
                {
                    // Clic seco (interruptor, breaker) + golpe grave.
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        data[i] = (Rand() * Mathf.Exp(-t * 300f) + Mathf.Sin(t * 2f * Mathf.PI * 90f) * Mathf.Exp(-t * 25f) * 0.6f) * 0.8f;
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
