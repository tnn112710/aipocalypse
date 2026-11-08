using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Where a shot starts and which way it goes, for anything that aims through a holder's eye and leaves a muzzle on a model
    // (guns now; bows, turrets or an enemy rifle can use the same rules).
    public static class GunAim
    {
        // Aim points nearer the muzzle than this would send bullets off sideways; the eye's direction is used instead.
        public const float MinConvergeDistance = 1f;

        // `range` and `layers` are the shot's (BulletSettings). The holder's AimFrom picks the origin; a muzzle beyond cover
        // (the view model draws over it) shoots from the eye into the cover's near face instead.
        public static void Shot(IGunUser user, Transform muzzle, float range, int layers, BulletSystem bullets, out Vector3 origin, out Vector3 direction)
        {
            Transform eye = user.Eye;
            Vector3 aim = eye.rotation * (user.AimOffset * Vector3.forward);
            Vector3 toMuzzle = muzzle.position - eye.position;
            if (user.AimFrom != AimOrigin.Camera && toMuzzle.sqrMagnitude > 0.000001f &&
                bullets.TryHit(eye.position, toMuzzle.normalized, toMuzzle.magnitude, layers, user.Root, out _))
            {
                direction = toMuzzle.normalized;
                origin = eye.position;
                return;
            }
            switch (user.AimFrom)
            {
                case AimOrigin.Camera:
                    origin = eye.position;
                    direction = aim;
                    return;

                case AimOrigin.MuzzleToCrosshair:
                    Vector3 aimPoint = bullets.TryHit(eye.position, aim, range, layers, user.Root, out RaycastHit hit)
                        ? hit.point
                        : eye.position + aim * range;
                    origin = muzzle.position;
                    direction = MuzzleToPoint(origin, aim, aimPoint);
                    return;

                default:
                    origin = muzzle.position;
                    direction = muzzle.forward;
                    return;
            }
        }

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
