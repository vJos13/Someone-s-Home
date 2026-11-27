using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Brillo sutil y pulsante sobre un objeto de misión (borde cálido, aditivo, que la niebla apaga de lejos).
    ///
    /// Uso: <c>ObjectiveHighlighter.Mark(objeto, true)</c> cuando el objetivo lo pide. El brillo se ve solo mientras
    /// el objeto está activo y, si tiene <see cref="SimpleInteractable"/>, mientras está disponible: así se apaga solo
    /// al agarrarlo o usarlo, y en una entrega (destino disponible solo si llevas el objeto) "salta" al destino.
    ///
    /// Con malla propia se dibuja una copia inflada de la malla. Si el objeto está en un lote estático (su malla es la
    /// combinada de la escena) o no tiene malla (zona de entrega), se envuelve con una caja o cilindro a su medida.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveHighlighter : MonoBehaviour
    {
        const float PeakIntensity = 1.0f;
        const float PulseSpeed = 2.3f;
        static readonly Color Warm = new(1f, 0.8f, 0.5f);
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static Shader shader;

        bool marked;
        float level;                    // 0 → 1 (fundido de entrada / salida)
        SimpleInteractable interactable;
        readonly List<Renderer> glows = new();
        Material mat, shellMat;         // la envoltura (caja/cilindro) es más grande: brilla menos
        bool built;

        /// <summary>Prende o apaga el resaltado de un objeto (le agrega el componente si hace falta).</summary>
        public static void Mark(Component target, bool on)
        {
            if (!target) return;
            var h = target.GetComponent<ObjectiveHighlighter>();
            if (!h)
            {
                if (!on) return;
                h = target.gameObject.AddComponent<ObjectiveHighlighter>();
            }
            h.marked = on;
        }

        public static void Mark(Component target) => Mark(target, true);

        /// <summary>Está brillando (o fundiéndose) ahora mismo.</summary>
        public bool Visible => level > 0.001f;

        void Awake() => interactable = GetComponent<SimpleInteractable>();

        bool Wanted => marked && (!interactable || interactable.Available) && !MainMenuController.Active;

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            // Se enciende suave; se apaga rápido (al agarrarlo desaparece "de inmediato").
            level = Mathf.MoveTowards(level, Wanted ? 1f : 0f, dt * (Wanted ? 1.5f : 6f));
            if (level <= 0f)
            {
                SetGlowsActive(false);
                return;
            }
            if (!built) Build();
            SetGlowsActive(true);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
            mat.SetFloat(IntensityId, PeakIntensity * level * pulse);
            if (shellMat) shellMat.SetFloat(IntensityId, PeakIntensity * 0.45f * level * pulse);
        }

        void SetGlowsActive(bool on)
        {
            foreach (var g in glows)
                if (g && g.enabled != on) g.enabled = on;
        }

        void OnDisable() => level = 0f;          // al reaparecer (p. ej. objeto soltado) vuelve a entrar suave

        void OnDestroy()
        {
            foreach (var g in glows) if (g) Destroy(g.gameObject);
            if (mat) Destroy(mat);
            if (shellMat) Destroy(shellMat);
        }

        // ════════════════════════ CONSTRUCCIÓN ════════════════════════
        void Build()
        {
            built = true;
            if (!shader) shader = Resources.Load<Shader>("VFX/ObjectiveGlow");
            mat = new Material(shader ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_Resaltado (runtime)" };
            mat.SetColor("_Color", Warm);
            mat.SetFloat(IntensityId, 0f);

            bool any = false;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("Resaltado")) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                if (r.isPartOfStaticBatch) { Shell(r.bounds, r.transform); any = true; continue; }
                var go = new GameObject("Resaltado", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(r.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                AddGlowRenderer(go, mf.sharedMesh.subMeshCount, r.gameObject.layer);
                any = true;
            }
            if (!any)
            {
                // Zona sin malla (p. ej. "dejar la mochila aquí"): se envuelve el collider.
                var col = GetComponent<Collider>();
                var b = col ? col.bounds : new Bounds(transform.position, Vector3.zero);
                if (b.size.sqrMagnitude < 0.01f) b = new Bounds(transform.position, Vector3.one * 0.5f);
                Shell(b, transform);
            }
        }

        /// <summary>Caja o cilindro (según la planta) que envuelve unos límites, un poco más grande.</summary>
        void Shell(Bounds b, Transform parent)
        {
            bool round = Mathf.Abs(b.size.x - b.size.z) < Mathf.Max(b.size.x, b.size.z) * 0.2f;
            var prim = GameObject.CreatePrimitive(round ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            Destroy(prim.GetComponent<Collider>());
            prim.name = "Resaltado";
            var t = prim.transform;
            t.SetParent(parent, true);
            t.SetPositionAndRotation(b.center, Quaternion.identity);
            Vector3 size = b.size + Vector3.one * 0.03f;
            if (round) size.y *= 0.5f;                       // el cilindro primitivo mide 2 de alto
            t.localScale = Vector3.one;
            var lossy = t.lossyScale;
            t.localScale = new Vector3(size.x / lossy.x, size.y / lossy.y, size.z / lossy.z);
            if (!shellMat)
            {
                shellMat = new Material(mat) { name = "M_ResaltadoEnvoltura (runtime)" };
                shellMat.SetFloat("_Fill", 0.03f);
                shellMat.SetFloat("_Power", 3f);
            }
            AddGlowRenderer(prim, 1, parent.gameObject.layer, shellMat);
        }

        void AddGlowRenderer(GameObject go, int subMeshes, int layer, Material material = null)
        {
            go.layer = layer;
            var mr = go.GetComponent<MeshRenderer>();
            if (!mr) mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[Mathf.Max(1, subMeshes)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material ? material : mat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.enabled = false;
            glows.Add(mr);
        }
    }
}
