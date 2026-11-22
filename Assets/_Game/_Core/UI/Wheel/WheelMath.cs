using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    public static class WheelMath
    {
        // The slice a direction from the wheel's centre points at: slice 0 centred straight up, the rest clockwise; −1
        // inside the dead zone.
        public static int SliceAt(Vector2 fromCentre, int count, float deadZone)
        {
            if (count <= 0 || fromCentre.sqrMagnitude < deadZone * deadZone)
                return -1;
            float angle = Mathf.Atan2(fromCentre.x, fromCentre.y) * Mathf.Rad2Deg;
            float width = 360f / count;
            float shifted = Mathf.Repeat(angle + width * 0.5f, 360f);
            return Mathf.Min(count - 1, Mathf.FloorToInt(shifted / width));
        }
    }
}
