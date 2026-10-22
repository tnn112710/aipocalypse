using UnityEngine;

namespace Aipocalypse.Core
{
    // Frame-rate independent smoothing: the share of the way to move towards a target this frame (use with Lerp).
    public static class Damping
    {
        // `rate` per second (higher = faster); 0 or less = instant.
        public static float ByRate(float rate, float deltaTime) => rate > 0f ? 1f - Mathf.Exp(-rate * deltaTime) : 1f;

        // `seconds` to cover most (63%) of the way; 0 or less = instant.
        public static float ByTime(float seconds, float deltaTime) => seconds > 0f ? 1f - Mathf.Exp(-deltaTime / seconds) : 1f;
    }
}
