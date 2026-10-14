using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    public static class GunAim
    {
        // Aim points nearer the muzzle than this would send bullets off sideways; the eye's direction is used instead.
        public const float MinConvergeDistance = 1f;

        // Muzzle-to-crosshair: from the muzzle towards what the eye is looking at.
        // Falls back to the eye's direction when the point is too close to, or behind, the muzzle.
        public static Vector3 MuzzleToPoint(Vector3 muzzle, Vector3 eyeForward, Vector3 aimPoint)
        {
            Vector3 toPoint = aimPoint - muzzle;
            float distance = toPoint.magnitude;
            if (distance < MinConvergeDistance || Vector3.Dot(toPoint, eyeForward) <= 0f)
                return eyeForward.normalized;
            return toPoint / distance;
        }
    }
}
