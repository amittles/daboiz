using UnityEngine;

namespace Daboiz.Core
{
    /// <summary>Raycast helpers that ignore a given root (the shooter) and trigger colliders.</summary>
    public static class PhysicsUtil
    {
        private static readonly RaycastHit[] Buffer = new RaycastHit[64];

        public static bool RaycastIgnoring(Vector3 origin, Vector3 dir, float distance, Transform ignore,
            out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, Buffer, distance, ~0, QueryTriggerInteraction.Ignore);
            return PickNearest(n, ignore, null, out best);
        }

        public static bool SphereCastIgnoring(Vector3 origin, float radius, Vector3 dir, float distance,
            Transform ignore, out RaycastHit best)
        {
            int n = Physics.SphereCastNonAlloc(origin, radius, dir, Buffer, distance, ~0,
                QueryTriggerInteraction.Ignore);
            bool ok = PickNearest(n, ignore, null, out best);
            if (ok && best.distance <= 0f && best.point == Vector3.zero)
            {
                // Started overlapping: report a hit right at the origin.
                best.point = origin;
                best.normal = -dir;
            }
            return ok;
        }

        /// <summary>True if nothing blocks the line between a and b (except the two given roots).</summary>
        public static bool LineOfSight(Vector3 a, Vector3 b, Transform ignoreA, Transform ignoreB)
        {
            Vector3 d = b - a;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(a, d / dist, Buffer, dist, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit hit;
            return !PickNearest(n, ignoreA, ignoreB, out hit);
        }

        private static bool PickNearest(int n, Transform ignoreA, Transform ignoreB, out RaycastHit best)
        {
            best = default(RaycastHit);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = Buffer[i];
                if (h.collider == null) continue;
                Transform t = h.collider.transform;
                if (ignoreA != null && t.IsChildOf(ignoreA)) continue;
                if (ignoreB != null && t.IsChildOf(ignoreB)) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }
    }
}
