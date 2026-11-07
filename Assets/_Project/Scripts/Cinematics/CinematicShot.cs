using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Plano cinemático de Cinemachine 3: sube la prioridad de una CinemachineCamera durante unos segundos,
    /// congela al jugador y luego devuelve el control (el CinemachineBrain hace el blend).
    /// Es la "cámara que revela" de la práctica S12, controlada por el director del nivel.
    /// </summary>
    public class CinematicShot : MonoBehaviour
    {
        [SerializeField] CinemachineCamera vcam;
        [SerializeField] int activePriority = 20;
        [SerializeField] int idlePriority = 0;

        void Awake()
        {
            if (vcam) vcam.Priority = idlePriority;
        }

        public IEnumerator Play(FirstPersonController player, float hold, float blendWait = 1.6f)
        {
            if (!vcam) yield break;
            if (player) { player.InputLocked = true; player.LookLocked = true; }
            vcam.Priority = activePriority;
            yield return new WaitForSeconds(blendWait + hold);
            vcam.Priority = idlePriority;
            yield return new WaitForSeconds(blendWait);
            if (player) { player.InputLocked = false; player.LookLocked = false; }
        }
    }
}
