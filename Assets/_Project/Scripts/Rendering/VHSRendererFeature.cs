using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome
{
    /// <summary>
    /// Filtro de cinta VHS (shader SomeonesHome/VHS) como pase de pantalla completa después del post.
    /// Igual que el FullScreenPass de URP, pero solo en la ÚLTIMA cámara de la pila: así la imagen de la escena y lo
    /// que se tiene en mano (cámara overlay) pasan una sola vez por el filtro. Con intensidad 0 no se encola el pase.
    ///
    /// La intensidad la maneja <see cref="VhsLook"/> con las variables globales _VHSAmount y _VHSGlitch.
    /// Se agrega a PC_Renderer y Mobile_Renderer con el menú "Someone's Home/Instalar filtro VHS".
    /// </summary>
    public class VHSRendererFeature : FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            ref var cam = ref renderingData.cameraData;
            if (!cam.resolveFinalTarget) return;                     // overlay pendiente: el filtro va al final
            if (cam.cameraType == CameraType.SceneView && !VhsLook.InSceneView) return;
            if (VhsLook.Amount <= 0.001f && VhsLook.Glitch <= 0.001f) return;
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
