using UnityEngine;

namespace MB13.Aipocalypse.Core
{
    // Damped spring towards a target, integrated in fixed substeps so stiff springs stay stable through long frames.
    public static class Spring
    {
        const float SubStep = 1f / 120f;

        public static void Step(ref float value, ref float velocity, float target, float stiffness, float dampingRatio, float deltaTime)
        {
            float damping = 2f * dampingRatio * Mathf.Sqrt(stiffness);
            for (float left = deltaTime; left > 0f; left -= SubStep)
            {
                float dt = Mathf.Min(left, SubStep);
                velocity += (stiffness * (target - value) - damping * velocity) * dt;
                value += velocity * dt;
            }
        }

        public static void Step(ref Vector2 value, ref Vector2 velocity, Vector2 target, float stiffness, float dampingRatio, float deltaTime)
        {
            float damping = 2f * dampingRatio * Mathf.Sqrt(stiffness);
            for (float left = deltaTime; left > 0f; left -= SubStep)
            {
                float dt = Mathf.Min(left, SubStep);
                velocity += (stiffness * (target - value) - damping * velocity) * dt;
                value += velocity * dt;
            }
        }

        public static void Step(ref Vector3 value, ref Vector3 velocity, Vector3 target, float stiffness, float dampingRatio, float deltaTime)
        {
            float damping = 2f * dampingRatio * Mathf.Sqrt(stiffness);
            for (float left = deltaTime; left > 0f; left -= SubStep)
            {
                float dt = Mathf.Min(left, SubStep);
                velocity += (stiffness * (target - value) - damping * velocity) * dt;
                value += velocity * dt;
            }
        }
    }
}
