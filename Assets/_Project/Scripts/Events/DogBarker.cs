using System.Collections;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>El perro de los vecinos: ladra a ratos; Frantic() ladra sin parar; Silence() lo corta en seco.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class DogBarker : MonoBehaviour
    {
        [SerializeField] AudioClip[] barks;
        AudioSource src;
        Coroutine routine;
        public bool Barking => routine != null;

        void Awake()
        {
            src = GetComponent<AudioSource>();
            if (barks == null || barks.Length == 0)
                barks = new[]
                {
                    ProceduralAudio.Build(ProceduralAudio.Kind.Bark, 0.22f, 11),
                    ProceduralAudio.Build(ProceduralAudio.Kind.Bark, 0.18f, 12),
                    ProceduralAudio.Build(ProceduralAudio.Kind.Bark, 0.26f, 13),
                };
        }

        public void StartBarking() { Stop(); routine = StartCoroutine(Loop(false)); }
        public void Frantic() { Stop(); routine = StartCoroutine(Loop(true)); }
        public void Silence() => Stop();

        void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            src.Stop();
        }

        IEnumerator Loop(bool frantic)
        {
            while (true)
            {
                int n = frantic ? Random.Range(3, 6) : Random.Range(1, 4);
                for (int i = 0; i < n; i++)
                {
                    src.pitch = Random.Range(0.92f, 1.08f) * (frantic ? 1.08f : 1f);
                    src.PlayOneShot(barks[Random.Range(0, barks.Length)], frantic ? 1f : 0.8f);
                    yield return new WaitForSeconds(frantic ? Random.Range(0.22f, 0.32f) : Random.Range(0.35f, 0.55f));
                }
                yield return new WaitForSeconds(frantic ? Random.Range(0.3f, 0.7f) : Random.Range(2.5f, 6f));
            }
        }
    }
}
