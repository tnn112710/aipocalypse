using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // How one throw leaves the hand.
    public struct RangedThrow
    {
        public Vector3 Velocity;
        public float FlightTime;
        // A lob over what's in the way, rather than a straight throw.
        public bool Lobbed;
        // Where it's aimed to land.
        public Vector3 AimPoint;
    }

    // Picks a throw: straight when nothing stands between the thrower and the target, a lob (at a random angle) when
    // something does or by chance; aimed with a lead on the target's movement and a random error. The physics checks are
    // sphere-casts against RangedSettings.Obstacles, ignoring the thrower and the target themselves.
    public static class RangedAim
    {
        // Pieces a lob's path is checked in.
        const int LobSegments = 8;
        // The last stretch before the aim point isn't checked: the target's own cover (a fence it stands behind) is the lob's point.
        const float LobCheckedShare = 0.85f;
        // Steeper tries when a lob is blocked.
        const int LobTries = 3;

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        // False when no throw reaches the target from here. `lobOnly`: no straight throw (one of the thrower's own stands in the line).
        public static bool TryPlan(RangedSettings settings, Vector3 from, Vector3 targetPoint, Vector3 targetVelocity, Transform thrower,
            Transform target, out RangedThrow plan, bool lobOnly = false)
        {
            plan = default;
            ProjectileDefinition projectile = settings.Projectile;
            if (projectile == null)
                return false;
            float gravity = projectile.Gravity;
            float radius = projectile.Radius;

            bool clear = !lobOnly && IsClear(from, targetPoint, radius, settings.Obstacles, thrower, target);
            bool lob = !clear || Random.value < settings.LobAnyway;
            if (!lob && TryStraight(settings, from, targetPoint, targetVelocity, gravity, out plan))
                return true;

            // A blocked lob tries again steeper.
            float angle = Random.Range(settings.LobAngle.x, settings.LobAngle.y);
            for (int i = 0; i < LobTries; i++)
            {
                if (TryLob(settings, from, targetPoint, targetVelocity, angle, gravity, out plan)
                    && IsLobClear(from, plan, gravity, radius, settings.Obstacles, thrower, target))
                    return true;
                angle = Mathf.Min(80f, angle + 10f);
            }
            // Nothing over the top: the straight throw after all, if the line is open.
            return clear && TryStraight(settings, from, targetPoint, targetVelocity, gravity, out plan);
        }

        static bool TryStraight(RangedSettings settings, Vector3 from, Vector3 point, Vector3 targetVelocity, float gravity, out RangedThrow plan)
        {
            plan = default;
            if (!Ballistics.TryLowAngle(from, point, settings.StraightSpeed, gravity, out _, out float time))
                return false;
            Vector3 aim = Aim(settings, from, point, targetVelocity, time);
            if (!Ballistics.TryLowAngle(from, aim, settings.StraightSpeed, gravity, out Vector3 velocity, out time))
                return false;
            plan = new RangedThrow { Velocity = velocity, FlightTime = time, Lobbed = false, AimPoint = aim };
            return true;
        }

        static bool TryLob(RangedSettings settings, Vector3 from, Vector3 point, Vector3 targetVelocity, float angle, float gravity,
            out RangedThrow plan)
        {
            plan = default;
            if (!Ballistics.TryAtAngle(from, point, angle, gravity, out _, out float time))
                return false;
            Vector3 aim = Aim(settings, from, point, targetVelocity, time);
            if (!Ballistics.TryAtAngle(from, aim, angle, gravity, out Vector3 velocity, out time)
                || velocity.sqrMagnitude > settings.MaxLobSpeed * settings.MaxLobSpeed)
                return false;
            plan = new RangedThrow { Velocity = velocity, FlightTime = time, Lobbed = true, AimPoint = aim };
            return true;
        }

        // Where to aim: ahead of a moving target by a random share of its movement over the flight, off by a random error
        // that grows with the distance (the Spread cone).
        public static Vector3 Aim(RangedSettings settings, Vector3 from, Vector3 point, Vector3 targetVelocity, float flightTime)
        {
            targetVelocity.y = 0f;
            Vector3 aim = point + targetVelocity * (flightTime * Random.Range(settings.Lead.x, settings.Lead.y));
            float error = Vector3.Distance(from, aim) * Mathf.Tan(settings.Spread * Mathf.Deg2Rad);
            Vector2 off = Random.insideUnitCircle * error;
            Vector3 heading = aim - from;
            heading.y = 0f;
            Vector3 side = heading.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, heading.normalized) : Vector3.right;
            return aim + side * off.x + Vector3.up * (off.y * 0.5f);
        }

        // One of the thrower's own side (`friends` layers, not the thrower itself) stands in the straight line to `to`.
        public static bool FriendInLine(Vector3 from, Vector3 to, float radius, int friends, Transform thrower)
        {
            Vector3 path = to - from;
            float distance = path.magnitude;
            return distance >= 0.01f && Blocked(from, path / distance, distance, radius, friends, thrower, null);
        }

        static bool IsClear(Vector3 from, Vector3 to, float radius, int mask, Transform thrower, Transform target)
        {
            Vector3 path = to - from;
            float distance = path.magnitude;
            return distance < 0.01f || !Blocked(from, path / distance, distance, radius, mask, thrower, target);
        }

        static bool IsLobClear(Vector3 from, in RangedThrow plan, float gravity, float radius, int mask, Transform thrower, Transform target)
        {
            Vector3 previous = from;
            for (int i = 1; i <= LobSegments; i++)
            {
                float time = plan.FlightTime * LobCheckedShare * i / LobSegments;
                Vector3 point = Ballistics.PointAt(from, plan.Velocity, gravity, time);
                Vector3 step = point - previous;
                float distance = step.magnitude;
                if (distance > 0.01f && Blocked(previous, step / distance, distance, radius, mask, thrower, target))
                    return false;
                previous = point;
            }
            return true;
        }

        static bool Blocked(Vector3 from, Vector3 direction, float distance, float radius, int mask, Transform thrower, Transform target)
        {
            int count = Physics.SphereCastNonAlloc(from, radius, direction, Hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = Hits[i].collider.transform;
                if ((thrower != null && hit.IsChildOf(thrower)) || (target != null && hit.IsChildOf(target)))
                    continue;
                return true;
            }
            return false;
        }
    }
}
