using UnityEngine;

namespace MB13.Aipocalypse.Core
{
    // Measures on the ground plane (XZ): height differences ignored.
    public static class FlatMath
    {
        public static float Length(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        public static float Distance(Vector3 a, Vector3 b) => Length(a - b);

        public static float SqrDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }

        // `v` with its height zeroed (not normalized).
        public static Vector3 Flatten(Vector3 v) => new(v.x, 0f, v.z);
    }
}
