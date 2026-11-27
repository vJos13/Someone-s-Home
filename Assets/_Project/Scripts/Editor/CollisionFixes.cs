using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Arreglos de colisión en la escena abierta (se puede correr varias veces):
    ///  · Rocas grandes de Naturaleza/Rocas sin collider (se atravesaban): MeshCollider con su malla (LOD0).
    /// Los arbustos (Artemisa) quedan sin collider a propósito: se camina entre ellos.
    /// </summary>
    public static class CollisionFixes
    {
        [MenuItem("Someone's Home/Arreglar colisiones (rocas)")]
        public static string Apply()
        {
            int added = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                var t = mf.transform;
                if (!InRocks(t) || !mf.sharedMesh) continue;
                if (t.name.Contains("_LOD") && !t.name.EndsWith("_LOD0")) continue;
                var r = mf.GetComponent<Renderer>();
                if (!r || r.bounds.size.y < 0.35f) continue;            // piedritas: se pisan
                var lodRoot = t.parent && t.parent.GetComponent<LODGroup>() ? t.parent : t;
                if (lodRoot.GetComponentInChildren<Collider>(true)) continue;
                var mc = Undo.AddComponent<MeshCollider>(t.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                added++;
            }
            if (added > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return $"MeshCollider agregados a {added} rocas";
        }

        static bool InRocks(Transform t)
        {
            for (var p = t; p; p = p.parent)
                if (p.name == "Rocas") return true;
            return false;
        }
    }
}
