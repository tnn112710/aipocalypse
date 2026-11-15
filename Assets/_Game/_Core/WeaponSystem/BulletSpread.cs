using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    public static class BulletSpread
    {
        // A direction within `angle` degrees of `direction`, evenly spread over the cone's cross-section: `unitDisc` is a
        // roll inside the unit circle (RandomStream.InsideUnitCircle), so tests and replays can fix it.
        public static Vector3 Apply(Vector3 direction, float angle, Vector2 unitDisc)
        {
            direction.Normalize();
            if (angle <= 0f)
                return direction;

            Vector2 offset = unitDisc * Mathf.Tan(angle * Mathf.Deg2Rad);
            Quaternion frame = Quaternion.LookRotation(direction);
            return (frame * new Vector3(offset.x, offset.y, 1f)).normalized;
        }
    }
}
