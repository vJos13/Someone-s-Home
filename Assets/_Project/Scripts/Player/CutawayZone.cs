using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Edificio que la cámara isométrica "corta" como una casa de muñecas (caja local, sin collider).
    ///  - Interior (el jugador está adentro): se ocultan techo y fachada (hideRenderers) y el cielo raso
    ///    (submateriales que contienen hideMaterialKeywords; va en la misma malla que el piso).
    ///  - Completo (el edificio tapa al jugador desde afuera): además las paredes, vidrios y puertas
    ///    (fullHideRenderers y todos los submateriales salvo fullKeepMaterialKeywords: piso y cimiento).
    /// Lo oculto sigue haciendo sombra cuando es un renderer entero (ShadowsOnly); los submateriales se cambian
    /// por un material invisible. Los colliders no cambian: puertas e interactuables siguen funcionando.
    /// </summary>
    public class CutawayZone : MonoBehaviour
    {
        public enum CutLevel { None, Interior, Full }

        public static readonly List<CutawayZone> All = new();

        [SerializeField] Vector3 center = Vector3.zero;
        [SerializeField] Vector3 size = new(12f, 3f, 9f);
        [Tooltip("Altura extra (techo a dos aguas) para decidir si el edificio tapa al jugador.")]
        [SerializeField] float roofHeight = 2.5f;

        [Header("Corte interior (jugador adentro)")]
        [SerializeField] Renderer[] hideRenderers;
        [SerializeField] string[] hideMaterialKeywords = { "TechoInt" };

        [Header("Corte completo (el edificio tapa al jugador)")]
        [SerializeField] Renderer[] fullHideRenderers;
        [Tooltip("En el corte completo se ocultan todos los submateriales menos estos.")]
        [SerializeField] string[] fullKeepMaterialKeywords = { "Piso", "Concreto" };

        [Header("Submateriales")]
        [Tooltip("Mallas con varias partes en un solo renderer (estructura: piso + paredes + cielo raso).")]
        [SerializeField] Renderer[] submaterialRenderers;
        [Tooltip("Material que no dibuja nada (transparente, alfa 0, sin sombra).")]
        [SerializeField] Material invisibleMaterial;

        readonly Dictionary<Renderer, ShadowCastingMode> savedModes = new();
        readonly Dictionary<Renderer, Material[]> savedMaterials = new();
        readonly HashSet<Renderer> owned = new();
        readonly HashSet<Renderer> hiddenNow = new();

        public CutLevel Level { get; private set; } = CutLevel.None;
        /// <summary>Última vez que el edificio tapó al jugador (histéresis del corte completo).</summary>
        public float LastOccludedTime { get; set; } = -10f;

        void OnEnable()
        {
            All.Add(this);
            owned.Clear();
            Add(hideRenderers);
            Add(fullHideRenderers);
            Add(submaterialRenderers);
            // Todo lo que está dentro del edificio (muebles, cocina) lo maneja la zona, no la oclusión genérica:
            // así la casa nunca desaparece entera dejando objetos flotando.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (Contains(r.bounds.center, roofHeight)) owned.Add(r);
        }

        void Add(Renderer[] list)
        {
            if (list != null) foreach (var r in list) if (r) owned.Add(r);
        }

        void OnDisable()
        {
            SetCut(CutLevel.None);
            All.Remove(this);
        }

        public bool Contains(Vector3 world, float extraHeight = 0f)
        {
            var b = new Bounds(center + Vector3.up * extraHeight * 0.5f, size + Vector3.up * extraHeight);
            return b.Contains(transform.InverseTransformPoint(world));
        }

        /// <summary>¿El segmento (cámara → jugador) atraviesa el edificio, techo incluido?</summary>
        public bool Occludes(Vector3 from, Vector3 to)
        {
            if (Contains(to)) return false;
            Vector3 a = transform.InverseTransformPoint(from), b = transform.InverseTransformPoint(to);
            var box = new Bounds(center + Vector3.up * roofHeight * 0.5f, size + Vector3.up * roofHeight);
            Vector3 d = b - a;
            float len = d.magnitude;
            return len > 0.01f && box.IntersectRay(new Ray(a, d / len), out float hit) && hit < len;
        }

        /// <summary>La zona maneja este renderer (la oclusión genérica de la cámara no lo toca).</summary>
        public bool Owns(Renderer r) => owned.Contains(r);

        /// <summary>Oculto por completo ahora (los clics lo atraviesan).</summary>
        public bool Hides(Renderer r) => hiddenNow.Contains(r);

        public void SetCut(CutLevel level)
        {
            if (level == Level) return;
            Restore();
            Level = level;
            if (level == CutLevel.None) return;

            Hide(hideRenderers);
            if (level == CutLevel.Full) Hide(fullHideRenderers);

            if (!invisibleMaterial || submaterialRenderers == null) return;
            foreach (var r in submaterialRenderers)
            {
                if (!r) continue;
                var mats = r.sharedMaterials;
                savedMaterials[r] = (Material[])mats.Clone();
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    bool hide = level == CutLevel.Full ? !Matches(mats[i].name, fullKeepMaterialKeywords)
                                                       : Matches(mats[i].name, hideMaterialKeywords);
                    if (hide) mats[i] = invisibleMaterial;
                }
                r.sharedMaterials = mats;
            }
        }

        void Hide(Renderer[] list)
        {
            if (list == null) return;
            foreach (var r in list)
            {
                if (!r || hiddenNow.Contains(r)) continue;
                savedModes[r] = r.shadowCastingMode;
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                hiddenNow.Add(r);
            }
        }

        void Restore()
        {
            foreach (var (r, mode) in savedModes) if (r) r.shadowCastingMode = mode;
            foreach (var (r, mats) in savedMaterials) if (r) r.sharedMaterials = mats;
            savedModes.Clear();
            savedMaterials.Clear();
            hiddenNow.Clear();
        }

        static bool Matches(string materialName, string[] keywords)
        {
            if (keywords == null) return false;
            foreach (var k in keywords)
                if (!string.IsNullOrEmpty(k) && materialName.Contains(k)) return true;
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireCube(center + Vector3.up * roofHeight * 0.5f, size + Vector3.up * roofHeight);
        }
    }
}
