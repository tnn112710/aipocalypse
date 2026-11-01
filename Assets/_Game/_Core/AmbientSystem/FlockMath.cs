using UnityEngine;

namespace Aipocalypse.Core.AmbientSystem
{
    // Boids for flocks and schools: where one member wants to go given its neighbours, its home, its height band and the
    // player. Pure maths for Flock and its tests.
    public static class FlockMath
    {
        // The velocity member `self` steers towards. `wander` drifts slowly over time (seconds); `threat` is the player;
        // `roamRadius` below 0 = the definition's.
        public static Vector3 Steer(int self, Vector3[] positions, Vector3[] velocities, int count, Vector3 home, float floor,
            float ceiling, FlockDefinition flock, Vector3 threat, bool hasThreat, float wander, float roamRadius = -1f)
        {
            Vector3 position = positions[self];
            Vector3 separation = Vector3.zero;
            Vector3 heading = Vector3.zero;
            Vector3 center = Vector3.zero;
            int neighbours = 0;
            float reach = flock.NeighbourRadius * flock.NeighbourRadius;
            float apart = flock.Separation * flock.Separation;
            for (int i = 0; i < count; i++)
            {
                if (i == self)
                    continue;
                Vector3 offset = positions[i] - position;
                float distance = offset.sqrMagnitude;
                if (distance > reach)
                    continue;
                neighbours++;
                heading += velocities[i];
                center += positions[i];
                if (distance < apart && distance > 1e-6f)
                    separation -= offset / distance;
            }

            Vector3 steer = separation * flock.SeparationWeight;
            if (neighbours > 0)
            {
                steer += Direction(heading) * flock.Alignment;
                steer += Direction(center / neighbours - position) * flock.Cohesion;
            }
            Vector3 toHome = home - position;
            toHome.y = 0f;
            float overshoot = toHome.magnitude - (roamRadius >= 0f ? roamRadius : flock.RoamRadius);
            if (overshoot > 0f)
                steer += Direction(toHome) * (flock.HomePull * (1f + overshoot));
            float angle = Mathf.PerlinNoise(wander * 0.3f, self * 1.7f) * 4f * Mathf.PI;
            float rise = Mathf.PerlinNoise(self * 0.37f, wander * 0.2f) - 0.5f;
            steer += new Vector3(Mathf.Cos(angle), rise, Mathf.Sin(angle)) * flock.Wander;

            float speed = flock.Speed;
            if (hasThreat && flock.FleeRadius > 0f)
            {
                Vector3 away = position - threat;
                float distance = away.magnitude;
                if (distance < flock.FleeRadius)
                {
                    steer += Direction(away) * (flock.FleeWeight * (1f - distance / flock.FleeRadius));
                    speed *= flock.FleeSpeedMultiplier;
                }
            }
            steer.y *= flock.VerticalFreedom;
            // Added after the vertical damping, so the band always wins and nothing leaves it for long.
            steer.y += BandPush(position.y, floor, ceiling) * flock.BandPull;
            if (steer.sqrMagnitude < 1e-6f)
                steer = velocities[self];
            return Direction(steer) * speed;
        }

        // +1 under the band's lower margin, -1 over its upper one, 0 inside (a margin of a quarter of the band, up to 1 m).
        public static float BandPush(float y, float floor, float ceiling)
        {
            float margin = Mathf.Min(1f, (ceiling - floor) * 0.25f);
            if (y < floor + margin)
                return 1f;
            if (y > ceiling - margin)
                return -1f;
            return 0f;
        }

        static Vector3 Direction(Vector3 vector)
        {
            float length = vector.magnitude;
            return length > 1e-5f ? vector / length : Vector3.zero;
        }
    }
}
