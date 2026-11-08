using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Walkable-ground questions for spawning, answered on the scene's navmesh (EnemyNavigation over A*). Optional: without one,
    // NavmeshRule.Require points can't be placed and Prefer ones stay on the ground.
    public interface INavmeshQuery
    {
        // The closest walkable point within `maxDistance` (horizontally) of `point`.
        bool TrySnap(Vector3 point, float maxDistance, out Vector3 snapped);

        // Whether a walker could get from `from` to `to`.
        bool IsReachable(Vector3 from, Vector3 to);
    }
}
