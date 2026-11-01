using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.SpawnSystem
{
    // Puts a sampled point where a kind can stand (its SpawnPlacement): on the ground (and the navmesh when it walks), in a
    // height band above the ground or water, or in a depth band under a water body's surface.
    public sealed class SpawnPlacer
    {
        // A sampled point looks for the ground from this high above it to as far below it (m).
        const float Probe = 100f;
        // Metres kept from a water body's surface and bed, on top of the kind's clearance.
        const float WaterMargin = 0.3f;

        readonly INavmeshQuery _navmesh;
        readonly IWaterLevel _water;

        public SpawnPlacer(IObjectResolver resolver)
        {
            resolver.TryResolve(out _navmesh);
            resolver.TryResolve(out _water);
        }

        public bool HasNavmesh => _navmesh != null;

        // `point` for `sample` (its height is ignored), and the up the instance stands along. `origin` (when `hasOrigin`) is
        // where navmesh walkers must have a path to; `roll` in [0, 1] picks the height in a band.
        public bool TryPlace(Vector3 sample, SpawnPlacement placement, SpawnRules rules, bool hasOrigin, Vector3 origin, float roll,
            out Vector3 point, out Vector3 up)
        {
            up = Vector3.up;
            point = default;
            switch (placement.Medium)
            {
                case SpawnMedium.Air:
                    if (!TrySurface(sample, rules, out float surface))
                        return false;
                    point = new Vector3(sample.x, PickAltitude(surface, placement.Height, roll), sample.z);
                    return IsClear(point, placement, rules);
                case SpawnMedium.Water:
                    if (_water == null || !_water.TryGetLevel(sample, out float level))
                        return false;
                    var top = new Vector3(sample.x, level + 0.1f, sample.z);
                    float bed = Physics.Raycast(top, Vector3.down, out RaycastHit floor, Probe, rules.GroundMask, QueryTriggerInteraction.Ignore)
                        ? floor.point.y
                        : level - Probe;
                    if (!PickWaterDepth(level, bed, placement.Height, WaterMargin + placement.Clearance, roll, out float y))
                        return false;
                    point = new Vector3(sample.x, y, sample.z);
                    return IsClear(point, placement, rules);
                default:
                    return TryGround(sample, placement, rules, hasOrigin, origin, out point, out up);
            }
        }

        // A height `height` (min, max metres) above `surface`, `roll` in [0, 1].
        public static float PickAltitude(float surface, Vector2 height, float roll) =>
            surface + Mathf.Lerp(Mathf.Min(height.x, height.y), Mathf.Max(height.x, height.y), Mathf.Clamp01(roll));

        // A height under a surface at `level` over a bed at `bed`: `depth` (min, max metres) below the surface, kept `margin`
        // from both. False when the water isn't deep enough there.
        public static bool PickWaterDepth(float level, float bed, Vector2 depth, float margin, float roll, out float y)
        {
            float shallowest = Mathf.Max(Mathf.Min(depth.x, depth.y), margin);
            float deepest = Mathf.Min(Mathf.Max(depth.x, depth.y), level - bed - margin);
            y = level - shallowest;
            if (deepest < shallowest)
                return false;
            y = level - Mathf.Lerp(shallowest, deepest, Mathf.Clamp01(roll));
            return true;
        }

        bool TryGround(Vector3 sample, SpawnPlacement placement, SpawnRules rules, bool hasOrigin, Vector3 origin, out Vector3 point,
            out Vector3 up)
        {
            point = default;
            up = Vector3.up;
            if (!Physics.Raycast(sample + Vector3.up * Probe, Vector3.down, out RaycastHit ground, Probe * 2f, rules.GroundMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            if (placement.MaxSlope < 90f && Vector3.Angle(ground.normal, Vector3.up) > placement.MaxSlope)
                return false;
            point = ground.point;
            if (placement.AlignToGround)
                up = ground.normal;
            switch (placement.Navmesh)
            {
                case NavmeshRule.Require:
                    if (_navmesh == null || !_navmesh.TrySnap(ground.point, rules.SnapDistance, out point))
                        return false;
                    if (hasOrigin && !_navmesh.IsReachable(point, origin))
                        return false;
                    break;
                case NavmeshRule.Prefer:
                    if (_navmesh != null && _navmesh.TrySnap(ground.point, rules.SnapDistance, out Vector3 snapped)
                        && (!hasOrigin || _navmesh.IsReachable(snapped, origin)))
                        point = snapped;
                    break;
            }
            return IsClear(point, placement, rules);
        }

        // The highest of the ground and any water body under `sample`.
        bool TrySurface(Vector3 sample, SpawnRules rules, out float surface)
        {
            surface = float.NegativeInfinity;
            if (Physics.Raycast(sample + Vector3.up * Probe, Vector3.down, out RaycastHit ground, Probe * 2f, rules.GroundMask,
                    QueryTriggerInteraction.Ignore))
                surface = ground.point.y;
            if (_water != null && _water.TryGetLevel(sample, out float level))
                surface = Mathf.Max(surface, level);
            return surface > float.NegativeInfinity;
        }

        static bool IsClear(Vector3 point, SpawnPlacement placement, SpawnRules rules)
        {
            if (placement.Clearance <= 0f)
                return true;
            // Lifted for things standing on the ground, so the ground itself doesn't count as an obstacle.
            Vector3 center = placement.Medium == SpawnMedium.Ground ? point + Vector3.up * (placement.Clearance + 0.05f) : point;
            return !Physics.CheckSphere(center, placement.Clearance, rules.ObstacleMask, QueryTriggerInteraction.Ignore);
        }
    }
}
