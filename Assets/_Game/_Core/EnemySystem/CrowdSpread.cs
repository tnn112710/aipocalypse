using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Rules that keep a crowd from moving in lockstep: how each enemy's pace differs and the side each one approaches its target
    // from. Where a group spreads when it spawns is FormationMath.Scatter (SpawnSystem).
    public static class CrowdSpread
    {
        // Multiplier on one life's MoveSpeed: 1 ± `variance`, `roll` in [0, 1].
        public static float SpeedFactor(float variance, float roll) => 1f + Mathf.Lerp(-variance, variance, Mathf.Clamp01(roll));

        // Where a chaser at `self` heads to reach `target` from its own side: a point beside the target that turns its heading
        // `side` × `maxAngle` degrees off the straight line (side in [-1, 1]). Full angle from `fadeFrom` metres out, straight
        // at the target within `fadeTo`.
        public static Vector3 FlankPoint(Vector3 self, Vector3 target, float side, float maxAngle, float fadeFrom, float fadeTo)
        {
            Vector3 away = self - target;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance < 0.001f)
                return target;
            float angle = Mathf.Clamp(side, -1f, 1f) * Mathf.Clamp(maxAngle, 0f, 80f) * FlankWeight(distance, fadeFrom, fadeTo);
            if (Mathf.Abs(angle) < 0.01f)
                return target;
            Vector3 across = new Vector3(-away.z, 0f, away.x) / distance;
            return target + across * (distance * Mathf.Tan(angle * Mathf.Deg2Rad));
        }

        // 0 within `fadeTo` metres of the target, 1 from `fadeFrom` out.
        public static float FlankWeight(float distance, float fadeFrom, float fadeTo) =>
            fadeFrom <= fadeTo ? (distance > fadeTo ? 1f : 0f) : Mathf.InverseLerp(fadeTo, fadeFrom, distance);
    }
}
