using System;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    public enum SpawnMedium
    {
        // On the ground: walkers, props, items.
        Ground,
        // A height band above the ground or water: flyers, birds.
        Air,
        // A depth band under the surface of a water body: fish, swimmers.
        Water,
    }

    public enum NavmeshRule
    {
        // On the walkable navmesh, with a path to the area's origin (walkers).
        Require,
        // Snapped to the navmesh when it's close, else on the ground.
        Prefer,
        // The ground itself (props, items, trees).
        Ignore,
    }

    // Where one kind can stand. Lives on its SpawnCard.
    [Serializable]
    public sealed class SpawnPlacement
    {
        public SpawnMedium Medium;
        [Tooltip("Ground only: whether points must be on the navmesh")] public NavmeshRule Navmesh;
        [Tooltip("Air: metres above the surface, min and max. Water: metres below the surface, min and max (the water must be at " +
            "least min deep, plus the margins)")]
        public Vector2 Height;
        [Min(0f), Tooltip("Metres of free space round the point (nothing on SpawnRules.ObstacleMask); under water also kept from the " +
            "surface and the bed. 0 = no check")]
        public float Clearance;
        [Range(0f, 90f), Tooltip("Ground: steepest slope (degrees) a point may stand on; 90 = any")] public float MaxSlope = 90f;
        [Tooltip("Ground: tilt with the ground's slope (props, items) instead of standing upright")] public bool AlignToGround;
        [Tooltip("Ground walkers whose own spot in a group can't be placed stand at the group's anchor instead of being dropped " +
            "(local avoidance spreads them)")]
        public bool FallBackToAnchor = true;
    }
}
