using System;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Estado de pausa del juego (lo abre GameMenu con ESC). Congela el tiempo y el audio 3D/ambiente;
    /// el jugador, la interacción, el celular y la linterna ignoran la entrada mientras IsPaused.
    /// Las corrutinas del relato usan Time.deltaTime / WaitForSeconds, así que quedan detenidas en su punto exacto.
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        public static event Action<bool> Changed;

        static float timeScaleBefore = 1f;

        public static void Set(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            if (paused)
            {
                timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else Time.timeScale = timeScaleBefore;
            AudioListener.pause = paused;
            Changed?.Invoke(paused);
        }

        /// <summary>Al cargar una escena (reiniciar relato) nunca se arranca en pausa.</summary>
        public static void ResetState()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
