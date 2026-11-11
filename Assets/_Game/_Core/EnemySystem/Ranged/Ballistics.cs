using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Pure throwing maths: launch velocities that carry something from one point to another under gravity (m/s², positive
    // down), and where it is along the way.
    public static class Ballistics
    {
        // The flat throw at `speed`: the lower of the two angles that land on `to`. False when `to` is out of reach at that speed.
        public static bool TryLowAngle(Vector3 from, Vector3 to, float speed, float gravity, out Vector3 velocity, out float time)
        {
            velocity = default;
            time = 0f;
            Flat(from, to, out Vector3 heading, out float x, out float y);
            if (x < 0.001f || speed <= 0f)
                return false;
            if (gravity <= 0f)
            {
                velocity = (to - from).normalized * speed;
                time = (to - from).magnitude / speed;
                return true;
            }
            float v2 = speed * speed;
            float root = v2 * v2 - gravity * (gravity * x * x + 2f * y * v2);
            if (root < 0f)
                return false;
            float angle = Mathf.Atan2(v2 - Mathf.Sqrt(root), gravity * x);
            velocity = (heading * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
            time = x / (speed * Mathf.Cos(angle));
            return true;
        }

        // The lob that leaves at `angle` degrees above the ground and lands on `to`. False when that angle can't reach it
        // (the target is above the line of the throw).
        public static bool TryAtAngle(Vector3 from, Vector3 to, float angle, float gravity, out Vector3 velocity, out float time)
        {
            velocity = default;
            time = 0f;
            Flat(from, to, out Vector3 heading, out float x, out float y);
            float radians = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float drop = x * Mathf.Tan(radians) - y;
            if (x < 0.001f || gravity <= 0f || cos < 0.001f || drop <= 0.001f)
                return false;
            float speed = Mathf.Sqrt(gravity * x * x / (2f * cos * cos * drop));
            velocity = (heading * cos + Vector3.up * Mathf.Sin(radians)) * speed;
            time = x / (speed * cos);
            return true;
        }

        public static Vector3 PointAt(Vector3 from, Vector3 velocity, float gravity, float time) =>
            from + velocity * time + Vector3.down * (0.5f * gravity * time * time);

        static void Flat(Vector3 from, Vector3 to, out Vector3 heading, out float distance, out float rise)
        {
            Vector3 flat = to - from;
            rise = flat.y;
            flat.y = 0f;
            distance = flat.magnitude;
            heading = distance > 0.0001f ? flat / distance : Vector3.forward;
        }
    }
}
