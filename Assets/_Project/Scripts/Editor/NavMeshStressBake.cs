using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Hornea en el Editor el NavMesh de la prueba de estrés (mismos ajustes que <see cref="NavMeshStress"/>) y lo guarda en
    /// Resources/Nav/NavMesh_Estres.asset. En el Editor las mallas de rocas y troncos se leen aunque no tengan Read/Write;
    /// en la build no (QA-06), por eso la build carga este asset en vez de hornear.
    /// </summary>
    public static class NavMeshStressBake
    {
        const string Path = "Assets/_Project/Resources/Nav/NavMesh_Estres.asset";

        [MenuItem("Someone's Home/QA/Hornear NavMesh de la prueba de estrés")]
        public static string Bake()
        {
            var go = new GameObject("NavMesh (horneado de prueba)");
            try
            {
                var s = go.AddComponent<NavMeshSurface>();
                NavMeshStress.Configure(s);
                var times = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 3; i++)
                {
                    s.RemoveData();
                    var sw = Stopwatch.StartNew();
                    s.BuildNavMesh();
                    times.Add(sw.Elapsed.TotalMilliseconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                }
                var tri = NavMesh.CalculateTriangulation();
                var data = Object.Instantiate(s.navMeshData);
                data.name = "NavMesh_Estres";
                System.IO.Directory.CreateDirectory("Assets/_Project/Resources/Nav");
                AssetDatabase.DeleteAsset(Path);
                AssetDatabase.CreateAsset(data, Path);
                AssetDatabase.SaveAssets();
                s.RemoveData();
                return $"bake_ms=[{string.Join(",", times)}] triangulos={tri.indices.Length / 3} vertices={tri.vertices.Length} → {Path}";
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
