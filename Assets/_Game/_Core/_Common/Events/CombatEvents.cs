using MB13.Aipocalypse.Core.Combat;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Events
{
    // A hit that landed (published by DamageService). Info.Source is who dealt it; Result says how much and whether it killed.
    public readonly struct DamageDealt
    {
        public readonly IDamageable Target;
        public readonly DamageInfo Info;
        public readonly DamageResult Result;

        public DamageDealt(IDamageable target, in DamageInfo info, in DamageResult result)
        {
            Target = target;
            Info = info;
            Result = result;
        }
    }

    public readonly struct PlayerDied
    {
        public readonly Vector3 Position;
        public readonly DamageType Cause;

        public PlayerDied(Vector3 position, DamageType cause)
        {
            Position = position;
            Cause = cause;
        }
    }

    // The death sequence has played out (lying, looking up): the point to fade out and respawn.
    public readonly struct PlayerDeathSequenceEnded
    {
        public readonly Vector3 Position;

        public PlayerDeathSequenceEnded(Vector3 position) => Position = position;
    }
}
