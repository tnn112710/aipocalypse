using UnityEngine;

namespace Aipocalypse.Core
{
    public static class PhysicsQueries
    {
        // Nearest of the first `count` hits (NonAlloc results come back unordered) whose collider isn't under `ignore`.
        public static bool Nearest(RaycastHit[] hits, int count, Transform ignore, out RaycastHit nearest)
        {
            nearest = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance >= nearestDistance || (ignore != null && hit.collider.transform.IsChildOf(ignore)))
                    continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            return nearestDistance < float.MaxValue;
        }
    }
}
