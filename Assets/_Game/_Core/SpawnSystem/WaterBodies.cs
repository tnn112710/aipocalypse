using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // The water level over a point, for spawning under or above water. Optional: without one, Water placement fails and Air
    // flies over the ground only.
    public interface IWaterLevel
    {
        bool TryGetLevel(Vector3 point, out float level);
    }

    // Flat water bodies (lake, ponds) by their renderers: a point over one's bounds has its level, the top of those bounds.
    // Bounds also cover dry banks, so placement checks the depth to the bed. Rivers aren't flat: don't list their segments.
    public sealed class WaterBodies : IWaterLevel
    {
        readonly IReadOnlyList<Renderer> _bodies;

        public WaterBodies(IReadOnlyList<Renderer> bodies) => _bodies = bodies;

        public bool TryGetLevel(Vector3 point, out float level)
        {
            level = float.NegativeInfinity;
            for (int i = 0; i < _bodies.Count; i++)
            {
                Renderer body = _bodies[i];
                if (body == null)
                    continue;
                Bounds bounds = body.bounds;
                if (point.x < bounds.min.x || point.x > bounds.max.x || point.z < bounds.min.z || point.z > bounds.max.z)
                    continue;
                level = Mathf.Max(level, bounds.max.y);
            }
            return level > float.NegativeInfinity;
        }
    }
}
