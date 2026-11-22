using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // The yard as a turned box seen from above (height ignored): is a point inside, and the nearest point inside it.
    public static class YardMath
    {
        // Inside, and at least `margin` from every edge.
        public static bool Contains(Matrix4x4 worldToLocal, Vector3 halfSize, Vector3 point, float margin = 0f)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(point);
            return Mathf.Abs(local.x) <= halfSize.x - margin && Mathf.Abs(local.z) <= halfSize.z - margin;
        }

        // Clamped to margin inside the edges; the point's height is kept.
        public static Vector3 ClosestInside(Matrix4x4 worldToLocal, Vector3 halfSize, Vector3 point, float margin)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(point);
            float x = Mathf.Max(0f, halfSize.x - margin);
            float z = Mathf.Max(0f, halfSize.z - margin);
            local.x = Mathf.Clamp(local.x, -x, x);
            local.z = Mathf.Clamp(local.z, -z, z);
            Vector3 world = worldToLocal.inverse.MultiplyPoint3x4(local);
            world.y = point.y;
            return world;
        }
    }
}
