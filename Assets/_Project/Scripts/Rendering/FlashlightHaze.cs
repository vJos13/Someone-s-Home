using UnityEngine;
using UnityEngine.Rendering;

namespace SomeonesHome
{
    /// <summary>
    /// Haz visible de la linterna en el aire húmedo y la niebla: un cono aditivo pegado a la luz (shader
    /// SomeonesHome/LightCone) que sigue su ángulo, alcance, intensidad y parpadeos. Más visible cuanto más niebla hay.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlashlightHaze : MonoBehaviour
    {
        const float BaseIntensity = 0.05f;
        /// <summary>Solo para medir su coste (PerfBenchmark -benchmarkOff haze): no se crea el cono.</summary>
        public static bool Disabled;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Disabled = false;

        Light beam;
        float referenceIntensity = -1f;
        MeshRenderer cone;
        Material mat;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int LightPosId = Shader.PropertyToID("_LightPos");
        static readonly int LightDirId = Shader.PropertyToID("_LightDir");
        static readonly int CosInnerId = Shader.PropertyToID("_CosInner");

        void Awake()
        {
            beam = GetComponent<Light>();
            var shader = Resources.Load<Shader>("VFX/LightCone") ?? Shader.Find("SomeonesHome/LightCone");
            if (!shader) { enabled = false; return; }
            mat = new Material(shader) { name = "M_HazLinterna (runtime)" };
            var go = new GameObject("Haz_Visible", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = BuildCone(28, 4);
            cone = go.GetComponent<MeshRenderer>();
            cone.sharedMaterial = mat;
            cone.shadowCastingMode = ShadowCastingMode.Off;
            cone.receiveShadows = false;
            cone.lightProbeUsage = LightProbeUsage.Off;
            cone.reflectionProbeUsage = ReflectionProbeUsage.Off;
            cone.enabled = false;
        }

        void LateUpdate()
        {
            if (!cone) return;
            bool on = beam.enabled && beam.intensity > 0.01f && beam.isActiveAndEnabled;
            if (on && referenceIntensity < 0f) referenceIntensity = beam.intensity;
            if (cone.enabled != on) cone.enabled = on;
            if (!on) return;
            // El cono solo envuelve el volumen; el shader calcula la luz dispersada en cada rayo.
            float length = Mathf.Min(beam.range * 0.7f, 18f);
            float radius = Mathf.Tan(beam.spotAngle * 0.5f * Mathf.Deg2Rad) * length * 1.05f;
            cone.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            cone.transform.localScale = new Vector3(radius, radius, length);
            float outer = Mathf.Cos(beam.spotAngle * 0.5f * Mathf.Deg2Rad);
            float inner = Mathf.Cos(Mathf.Max(beam.innerSpotAngle, beam.spotAngle * 0.4f) * 0.5f * Mathf.Deg2Rad);
            mat.SetVector(LightPosId, new Vector4(transform.position.x, transform.position.y, transform.position.z, length));
            mat.SetVector(LightDirId, new Vector4(transform.forward.x, transform.forward.y, transform.forward.z, outer));
            mat.SetFloat(CosInnerId, inner);
            // Más niebla → haz más marcado. Sigue los parpadeos de la luz.
            float fog = RenderSettings.fog ? Mathf.Clamp01(RenderSettings.fogDensity * 18f) : 0.2f;
            float k = referenceIntensity > 0f ? Mathf.Clamp01(beam.intensity / referenceIntensity) : 1f;
            mat.SetFloat(IntensityId, BaseIntensity * (0.35f + fog) * k);
            mat.SetColor("_Color", beam.color);
        }

        void OnDestroy()
        {
            if (mat) Destroy(mat);
        }

        /// <summary>Cono cerrado (con tapa al final) de radio 1 y largo 1 sobre +Z, caras hacia afuera.</summary>
        static Mesh BuildCone(int segments, int rings)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    verts.Add(new Vector3(dir.x * t, dir.y * t, t));
                    normals.Add(new Vector3(dir.x, dir.y, -1f).normalized);
                    uvs.Add(new Vector2(s / (float)segments, t));
                }
            }
            int row = segments + 1;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int i = r * row + s;
                    tris.Add(i); tris.Add(i + row); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + row); tris.Add(i + row + 1);
                }
            // Tapa del extremo: abanico alrededor del centro.
            int center = verts.Count;
            verts.Add(new Vector3(0f, 0f, 1f)); normals.Add(Vector3.forward); uvs.Add(new Vector2(0.5f, 1f));
            int last = rings * row;
            for (int s = 0; s < segments; s++) { tris.Add(center); tris.Add(last + s + 1); tris.Add(last + s); }
            var mesh = new Mesh { name = "Cono_Haz" };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(new Vector3(0, 0, 0.5f), new Vector3(2f, 2f, 1f));
            return mesh;
        }
    }
}
