using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    public static class FallDamage
    {
        // None up to the safe height, then rising linearly to maxHealth at the lethal height.
        public static float For(float dropHeight, float safeHeight, float lethalHeight, float maxHealth)
        {
            if (dropHeight <= safeHeight)
                return 0f;

            if (lethalHeight <= safeHeight)
                return maxHealth;

            return maxHealth * Mathf.Clamp01((dropHeight - safeHeight) / (lethalHeight - safeHeight));
        }
    }
}
