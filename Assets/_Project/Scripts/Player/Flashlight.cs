using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    /// <summary>Linterna en la cabeza del jugador. [F] la prende/apaga cuando ya la tienes. Puede parpadear (sustos).</summary>
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] Light beam;
        [SerializeField] AudioSource click;

        public bool Owned { get; private set; }
        public bool On => beam && beam.enabled;
        bool flickering;

        public void Give()
        {
            Owned = true;
            SetOn(true);
        }

        public void Take()
        {
            Owned = false;
            SetOn(false);
        }

        public void SetOn(bool on)
        {
            if (!beam) return;
            if (beam.enabled != on && click) click.Play();
            beam.enabled = on;
        }

        /// <summary>Parpadea y se apaga 'darkTime' segundos; 'during' corre mientras está apagada.</summary>
        public IEnumerator Flicker(float darkTime, System.Action during = null)
        {
            if (!beam || !Owned) { during?.Invoke(); yield break; }
            flickering = true;
            float baseI = beam.intensity;
            for (int i = 0; i < 4; i++)
            {
                beam.intensity = baseI * Random.Range(0.05f, 0.5f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.1f));
                beam.intensity = baseI;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            }
            beam.enabled = false;
            during?.Invoke();
            yield return new WaitForSeconds(darkTime);
            beam.enabled = true;
            beam.intensity = baseI;
            flickering = false;
        }

        void Update()
        {
            if (!Owned || flickering || GamePause.IsPaused) return;
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) SetOn(!On);
        }
    }
}
