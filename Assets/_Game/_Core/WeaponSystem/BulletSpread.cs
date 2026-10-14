using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    public static class BulletSpread
    {
        // A random direction within `angle` degrees of `direction`, evenly spread over the cone's cross-section.
        public static Vector3 Apply(Vector3 direction, float angle)
        {
            direction.Normalize();
            if (angle <= 0f)
                return direction;

            Vector2 offset = Random.insideUnitCircle * Mathf.Tan(angle * Mathf.Deg2Rad);
            Quaternion frame = Quaternion.LookRotation(direction);
            return (frame * new Vector3(offset.x, offset.y, 1f)).normalized;
        }
    }
}
