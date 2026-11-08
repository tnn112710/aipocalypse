using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // A push from hits: each hit adds force × KnockbackPerForce (capped at MaxKnockbackSpeed) to a horizontal velocity that
    // dies out exponentially, so the body slides about speed / drag metres.
    public sealed class Knockback
    {
        const float RestSpeed = 0.05f;

        readonly EnemyReactionSettings _settings;

        public Vector3 Velocity { get; private set; }
        public bool Active => Velocity != Vector3.zero;
        public float Speed => Velocity.magnitude;

        public Knockback(EnemyReactionSettings settings) => _settings = settings;

        public void Add(Vector3 force)
        {
            Vector3 push = Vector3.ProjectOnPlane(force, Vector3.up) * _settings.KnockbackPerForce;
            Velocity = Vector3.ClampMagnitude(Velocity + push, _settings.MaxKnockbackSpeed);
        }

        // Returns this step's displacement.
        public Vector3 Advance(float deltaTime)
        {
            if (!Active || deltaTime <= 0f)
                return Vector3.zero;
            // Exact integral of v·e^(-kt) over the step, so the slide length doesn't depend on the frame rate.
            float decay = Mathf.Exp(-_settings.KnockbackDrag * deltaTime);
            Vector3 step = Velocity * ((1f - decay) / _settings.KnockbackDrag);
            Velocity *= decay;
            if (Velocity.sqrMagnitude < RestSpeed * RestSpeed)
                Velocity = Vector3.zero;
            return step;
        }

        public void Clear() => Velocity = Vector3.zero;
    }
}
