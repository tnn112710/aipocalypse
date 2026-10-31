using Pathfinding;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Navmesh questions on the scene's graph (the injected AstarPath, never AstarPath.active): spawners use it to keep
    // enemies on walkable, connected ground.
    public sealed class EnemyNavigation
    {
        readonly AstarPath _astar;

        public EnemyNavigation(AstarPath astar) => _astar = astar;

        // The closest walkable point within `maxDistance` (horizontally) of `point`.
        public bool TrySnap(Vector3 point, float maxDistance, out Vector3 snapped)
        {
            snapped = default;
            NNInfo nearest = _astar.GetNearest(point, NearestNodeConstraint.Walkable);
            if (nearest.node == null || MeleeHit.SqrFlatDistance(nearest.position, point) > maxDistance * maxDistance)
                return false;
            snapped = nearest.position;
            return true;
        }

        // Whether a walker could get from `from` to `to` (same connected area of the graph).
        public bool IsReachable(Vector3 from, Vector3 to)
        {
            NNInfo a = _astar.GetNearest(from, NearestNodeConstraint.Walkable);
            NNInfo b = _astar.GetNearest(to, NearestNodeConstraint.Walkable);
            return a.node != null && b.node != null && PathUtilities.IsPathPossible(a.node, b.node);
        }
    }
}
