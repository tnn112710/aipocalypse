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
        // Root of whoever dealt it (the shooter, the swinger, the player for fall damage); null = the world.
        public readonly GameObject Source;

        public DamageInfo(float amount, DamageType type, Vector3 point, Vector3 force = default, GameObject source = null)
        {
            Amount = amount;
            Type = type;
            Point = point;
            Force = force;
            Source = source;
        }
    }

    // What a hit actually did: damage taken after the target's own rules, and whether it emptied the target's health.
    public readonly struct DamageResult
    {
        public readonly float Applied;
        public readonly bool Killed;

        public DamageResult(float applied, bool killed)
        {
            Applied = applied;
            Killed = killed;
        }
    }
}
