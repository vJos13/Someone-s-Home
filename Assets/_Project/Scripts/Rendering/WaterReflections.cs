using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Reflejos del río: una sonda de reflexión en tiempo real sobre el agua que se vuelve a capturar cuando cambia la
    /// hora del relato (atardecer, noche, mañana) y cada tanto, repartida en varios frames (barato).
    /// Opción: Configuración → GRÁFICOS → "Reflejos del agua" (apagada, el agua usa el reflejo del cielo de siempre).
    /// </summary>
    public class WaterReflections : MonoBehaviour
    {
        const float RefreshEvery = 20f;

        ReflectionProbe probe;
        Renderer water;
        Level1Director director;
        string lastBeat;
        float nextRefresh;
        bool wasOn;
        // QualitySettings es del proyecto: en el Editor lo cambiado en Play quedaría así. Se restaura al salir.
        static bool? originalRealtime;

        void OnEnable() => Application.quitting += Restore;
        void OnDisable() { Application.quitting -= Restore; Restore(); }
        static void Restore()
        {
            if (originalRealtime.HasValue) QualitySettings.realtimeReflectionProbes = originalRealtime.Value;
            originalRealtime = null;
        }

        void Update()
        {
            if (!water)
            {
                if (Time.frameCount % 30 != 0) return;      // se busca de vez en cuando (al cargar la escena)
                var go = GameObject.Find("Water_Rio");
                if (!go || !go.TryGetComponent(out water)) return;
                Build();
            }
            bool on = GameSettings.WaterReflections && GameSettings.Shadows >= 2;
            if (probe.enabled != on) probe.enabled = on;
            if (on && !QualitySettings.realtimeReflectionProbes)
            {
                originalRealtime ??= false;
                QualitySettings.realtimeReflectionProbes = true;   // sin esto Unity no dibuja sondas en tiempo real
            }
            if (!on) { wasOn = false; return; }

            if (!director) director = FindAnyObjectByType<Level1Director>();
            string beat = director ? director.Beat : null;
            if (!wasOn || beat != lastBeat || Time.time >= nextRefresh)
            {
                wasOn = true;
                lastBeat = beat;
                nextRefresh = Time.time + RefreshEvery;
                probe.RenderProbe();
            }
        }

        void Build()
        {
            var b = water.bounds;
            var go = new GameObject("Reflejo_Rio");
            go.transform.SetParent(transform, false);
            // A la altura de los ojos de quien mira el río desde la orilla.
            go.transform.position = new Vector3(b.center.x, b.max.y + 1.6f, b.center.z);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = false;          // a ras del agua: reflejo "infinito" del cielo sobre la otra orilla
            probe.intensity = 1.7f;               // el agua quieta refleja casi todo a ángulo rasante
            probe.size = new Vector3(b.size.x + 20f, 40f, b.size.z + 40f);
            probe.center = Vector3.zero;
            probe.importance = 10;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 250f;
            probe.shadowDistance = 0f;
            probe.cullingMask = ~((1 << ViewModelLayer.Layer) | (1 << LayerMask.NameToLayer("UI")));
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            // Que solo la use el agua: los demás objetos siguen con su sonda / cielo.
            water.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
        }
    }
}
