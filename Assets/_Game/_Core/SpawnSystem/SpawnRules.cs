using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // How a request picks its spot in an area: how many points it samples, how far from the player and out of whose view.
    [Serializable]
    public sealed class SpawnRules
    {
        [Min(1), Tooltip("Points sampled in the area per pick")] public int Candidates = 8;
        [Min(0f), Tooltip("Metres a sampled point may move to reach the navmesh")] public float SnapDistance = 2f;
        [Min(0f), Tooltip("Never spawn closer than this to the player (m)")] public float MinPlayerDistance = 30f;
        [Min(0f), Tooltip("Points farther than this from the player count as unseen even in view (m)")] public float VisibleDistance = 90f;
        [Range(0f, 30f), Tooltip("Degrees added round the camera's field of view when deciding what the player sees")]
        public float ViewMargin = 10f;
        [Min(0f), Tooltip("Seconds a pick waits for a hidden point before taking the visible one farthest from the view centre")]
        public float MaxHiddenWait = 5f;
        [Min(0f), Tooltip("Height (m) above a spawn point the line-of-sight ray aims at (an enemy's chest)")] public float TargetHeight = 1.2f;
        [Min(0f), Tooltip("Seconds a point the player just saw stays off-limits (with SeenLockRadius); 0 = off")]
        public float SeenLock = 3f;
        [Min(0f), Tooltip("Metres round a just-seen point that stay off-limits")] public float SeenLockRadius = 8f;
        [Tooltip("What finds the ground under a sampled point")] public LayerMask GroundMask = 1;
        [Tooltip("What blocks the player's view of a spawn point (terrain, rocks, buildings)")] public LayerMask SightBlockers = 1;
        [Tooltip("What a point's clearance (SpawnPlacement.Clearance) must keep free of")] public LayerMask ObstacleMask = 1;
    }
}
