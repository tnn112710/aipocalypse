using Aipocalypse.Core.Combat;
using UnityEngine;

namespace Aipocalypse.Core.Events
{
    public readonly struct DamageDealt
    {
        public readonly IDamageable Target;
        public readonly DamageInfo Info;

        public DamageDealt(IDamageable target, in DamageInfo info)
        {
            Target = target;
            Info = info;
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
}
