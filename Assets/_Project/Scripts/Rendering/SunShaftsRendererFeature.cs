using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome
{
    /// <summary>Pase de los rayos: solo en la cámara base (necesita su profundidad) y con el sol a la vista.</summary>
    public class SunShaftsRendererFeature : FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            ref var cam = ref renderingData.cameraData;
            if (cam.renderType != CameraRenderType.Base || cam.cameraType == CameraType.SceneView) return;
            if (LightShafts.Visibility <= 0.001f) return;
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
