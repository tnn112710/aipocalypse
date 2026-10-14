using UnityEngine;

namespace Aipocalypse.Core.Combat
{
    public enum DamageType
    {
        Generic,
        Fall,
        Gunshot,
        Melee,
    }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly Vector3 Point;
        // Knockback impulse (N·s, world space); the target decides how it moves. Zero = no push.
        public readonly Vector3 Force;

        public DamageInfo(float amount, DamageType type, Vector3 point, Vector3 force = default)
        {
            Amount = amount;
            Type = type;
            Point = point;
            Force = force;
        }
    }
}
