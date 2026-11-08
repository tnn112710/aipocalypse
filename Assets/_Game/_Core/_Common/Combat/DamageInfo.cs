using UnityEngine;

namespace MB13.Aipocalypse.Core.Combat
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
        // Landed on a critical hitbox (a head): shown as a critical hit.
        public readonly bool Critical;

        public DamageInfo(float amount, DamageType type, Vector3 point, Vector3 force = default, GameObject source = null, bool critical = false)
        {
            Amount = amount;
            Type = type;
            Point = point;
            Force = force;
            Source = source;
            Critical = critical;
        }

        // The same hit landing on a hitbox: amount × multiplier, critical if either is.
        public DamageInfo OnHitbox(float multiplier, bool critical) =>
            new(Amount * multiplier, Type, Point, Force, Source, Critical || critical);
    }

    // What a hit actually did: damage taken after the target's own rules, whether it emptied the target's health, and whether
    // it staggered the target (stopped it for a moment; enemies only).
    public readonly struct DamageResult
    {
        public readonly float Applied;
        public readonly bool Killed;
        public readonly bool Staggered;

        public DamageResult(float applied, bool killed, bool staggered = false)
        {
            Applied = applied;
            Killed = killed;
            Staggered = staggered;
        }
    }
}
