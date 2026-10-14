using UnityEngine;

namespace Aipocalypse.Core.Combat
{
    public enum DamageType
    {
        Generic,
        Fall,
        Gunshot,
    }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly Vector3 Point;

        public DamageInfo(float amount, DamageType type, Vector3 point)
        {
            Amount = amount;
            Type = type;
            Point = point;
        }
    }
}
