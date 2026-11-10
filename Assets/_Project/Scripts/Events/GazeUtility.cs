using UnityEngine;

namespace SomeonesHome
{
    /// <summary>¿El jugador está mirando un punto? Ángulo de visión + línea de vista sin obstáculos.</summary>
    public static class GazeUtility
    {
        public static bool IsLookingAt(Transform eye, Vector3 point, float maxAngle = 25f, bool checkOcclusion = true)
        {
            Vector3 dir = point - eye.position;
            if (Vector3.Angle(eye.forward, dir) > maxAngle) return false;
            if (!checkOcclusion) return true;
            // Si algo opaco se interpone (la colina, una pared), no cuenta. El cuerpo del jugador no tapa.
            return !Raycast(eye.position, dir.normalized, dir.magnitude - 0.5f, eye.root, out _);
        }

        /// <summary>Raycast que ignora los colliders de 'ignoreRoot' (el propio jugador: la cabeza está dentro de su cápsula).</summary>
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, Transform ignoreRoot, out RaycastHit hit)
        {
            hit = default;
            bool found = false;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignoreRoot && h.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        /// <summary>Ángulo horizontal entre la vista del jugador y un punto (0 = de frente, 180 = de espaldas).</summary>
        public static float FlatAngleTo(Transform eye, Vector3 point)
        {
            Vector3 f = eye.forward; f.y = 0;
            Vector3 d = point - eye.position; d.y = 0;
            return Vector3.Angle(f, d);
        }
    }
}
