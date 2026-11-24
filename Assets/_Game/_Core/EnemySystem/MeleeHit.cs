using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Where a melee blow lands: reach and arc on the horizontal plane, within MaxHeightDifference up or down.
    public static class MeleeHit
    {
        // Metres: a target farther above or below (on a roof, a ledge) is out of reach.
        public const float MaxHeightDifference = 2f;

        public static float SqrFlatDistance(Vector3 a, Vector3 b) => FlatMath.SqrDistance(a, b);

        // Whether a blow from `attacker` facing `forward` reaches `target`: within `reach` metres and inside an arc of `arc`
        // degrees centred on the facing.
        public static bool Lands(Vector3 attacker, Vector3 forward, Vector3 target, float reach, float arc)
        {
            float sqrDistance = SqrFlatDistance(attacker, target);
            if (sqrDistance > reach * reach || Mathf.Abs(target.y - attacker.y) > MaxHeightDifference)
                return false;
            if (sqrDistance < 0.0001f)
                return true;
            var toTarget = new Vector2(target.x - attacker.x, target.z - attacker.z);
            var facing = new Vector2(forward.x, forward.z);
            if (facing.sqrMagnitude < 0.0001f)
                return false;
            return Vector2.Angle(facing, toTarget) <= arc * 0.5f;
        }
    }
}
