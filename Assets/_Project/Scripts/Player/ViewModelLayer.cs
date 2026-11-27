using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome
{
    /// <summary>
    /// Capa "en mano": lo que el jugador sostiene (inventario) y lo que examina (inspección) se dibuja con una
    /// cámara Overlay apilada sobre la principal. Así nunca atraviesa una pared aunque el jugador esté pegado a ella,
    /// y el FOV es fijo (el zoom de la cámara no agranda la mano).
    ///
    /// · Capa de física/render: "Inspeccion" (índice 8 en Tags and Layers). La cámara principal la excluye.
    /// · Luz: los objetos en mano usan además la Light Layer 7, que solo ilumina la luz de relleno de esta capa;
    ///   en la inspección se usa SOLO esa capa, así la pista se ve igual de día y de noche.
    /// </summary>
    public static class ViewModelLayer
    {
        public const string LayerName = "Inspeccion";
        const int FallbackLayer = 8;
        /// <summary>Light Layer 7 (máscara de rendering layers de URP).</summary>
        public const uint FillLightLayer = 1u << 7;
        public const float Fov = 58f;

        static Camera overlay;
        static Camera owner;
        static Light fill;

        public static int Layer
        {
            get
            {
                int l = LayerMask.NameToLayer(LayerName);
                return l >= 0 ? l : FallbackLayer;
            }
        }

        public static Camera Camera => overlay;

        /// <summary>Crea (una vez por escena) la cámara overlay hija de la cámara principal y la apila.</summary>
        public static Camera Ensure()
        {
            var main = Camera.main;
            if (!main) return null;
            if (overlay && owner == main) return overlay;

            var go = new GameObject("Camara_EnMano");
            go.transform.SetParent(main.transform, false);
            overlay = go.AddComponent<Camera>();
            overlay.clearFlags = CameraClearFlags.Depth;
            overlay.cullingMask = 1 << Layer;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 5f;
            overlay.fieldOfView = Fov;
            overlay.depth = main.depth + 1;
            var data = overlay.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;

            var mainData = main.GetUniversalAdditionalCameraData();
            if (!mainData.cameraStack.Contains(overlay)) mainData.cameraStack.Add(overlay);
            main.cullingMask &= ~(1 << Layer);
            owner = main;

            // Luz de relleno: solo afecta a la Light Layer 7 (objetos en mano / inspección).
            var lgo = new GameObject("Luz_EnMano");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(-0.16f, 0.2f, 0.12f);
            fill = lgo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 2.2f;
            fill.intensity = 0.25f;
            fill.color = new Color(1f, 0.93f, 0.84f);
            fill.shadows = LightShadows.None;
            fill.renderingLayerMask = (int)FillLightLayer;
            return overlay;
        }

        /// <summary>Intensidad de la luz de relleno (más fuerte mientras se inspecciona: ~3).</summary>
        public static void SetFill(float intensity)
        {
            if (fill) fill.intensity = intensity;
        }

        /// <summary>Pone el objeto y sus hijos en la capa en mano, sin sombras ni colisiones.</summary>
        /// <param name="sceneLit">true: lo ilumina la escena + el relleno (mano). false: solo el relleno (inspección).</param>
        public static void Apply(GameObject root, bool sceneLit)
        {
            int layer = Layer;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.renderingLayerMask = sceneLit ? (r.renderingLayerMask | FillLightLayer) : FillLightLayer;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }
    }
}
