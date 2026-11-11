using UnityEngine;

namespace MB13.Aipocalypse.Core.MapSystem
{
    // Top-down maths for a heading-up minimap: world offsets (metres, north = +Z, east = +X) to disc positions (pixels from the
    // centre, x right, y up = where the player looks).
    public static class MinimapMath
    {
        // Compass heading of a direction in degrees: 0 = north, 90 = east.
        public static float Heading(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        public static Vector2 ToDisc(Vector3 offset, float heading, float pixelsPerMetre)
        {
            float radians = heading * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(offset.x * cos - offset.z * sin, offset.x * sin + offset.z * cos) * pixelsPerMetre;
        }

        // Pulls a point beyond `radius` back onto the rim. False when it was beyond.
        public static bool ClampToRadius(ref Vector2 point, float radius)
        {
            float squared = point.sqrMagnitude;
            if (squared <= radius * radius)
                return true;
            point *= radius / Mathf.Sqrt(squared);
            return false;
        }
    }
}
