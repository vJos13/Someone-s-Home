using System.Runtime.InteropServices;

namespace SomeonesHome
{
    /// <summary>
    /// Ajustes de entrada del navegador (solo build WebGL; en el Editor y escritorio no hace nada).
    /// El pellizco de dos dedos en el touchpad llega al navegador como "rueda + Ctrl": por defecto haría zoom a
    /// TODA la página. Plugins/WebGL/BrowserInput.jslib lo cancela sobre el canvas, así Unity lo recibe como
    /// scroll y FirstPersonController lo usa para el zoom de la cámara. En Safari bloquea el gesto nativo.
    /// </summary>
    public static class BrowserInput
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SH_BlockPageZoom();
        static bool done;
        public static void BlockPageZoom()
        {
            if (done) return;
            done = true;
            try { SH_BlockPageZoom(); } catch (System.Exception) { /* plantilla sin canvas estándar */ }
        }
#else
        public static void BlockPageZoom() { }
#endif
    }
}
