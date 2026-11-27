using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Registro de las optimizaciones por objeto (Semana 14). Guarda qué renderers dejaron de proyectar
    /// sombra para que <see cref="PerfBenchmark"/> pueda medir "antes" y "después" en la misma build,
    /// volviendo atrás solo ese cambio. No afecta al juego.
    /// </summary>
    public class PerfRegistry : MonoBehaviour
    {
        [Tooltip("Renderers que proyectaban sombra y ya no (arbustos lejanos y objetos de menos de 1 m).")]
        public Renderer[] shadowsOff;

        public static PerfRegistry Instance => FindAnyObjectByType<PerfRegistry>(FindObjectsInactive.Include);
    }
}
