using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Tuning of the Melee subtree (hunt a target, chase, swing; wander when there's none), read by its tasks. Damage per blow
    // is the AttackDamage stat.
    [CreateAssetMenu(fileName = "MeleeSettings", menuName = "Aipocalypse/Enemies/Melee Settings")]
    public sealed class MeleeSettings : ScriptableObject
    {
        [Min(0.1f), Tooltip("Seconds between two target choices while hunting (sooner when the target can't be targeted any more)")]
        public float RetargetInterval = 1f;
        [Range(0, TargetSelector.MaxBreachCandidates), Tooltip("With no path to its target, it attacks the weakest of this many " +
            "nearest standing barriers (fence segments, gates). 0 = never; it also needs a Fence weight in the definition's Targets")]
        public int BreachCandidates = 5;
        [Range(0, 10), Tooltip("Once a way in is open (a gap, an open gate) it stops hitting a barrier and goes through, unless this " +
            "many of its own blows or fewer would break it: then it finishes the job first")]
        public int FinishBreachBlows = 2;
        [Min(0f), Tooltip("Metres from the target where it takes its fighting stance and sprints")]
        public float EngageRadius = 5f;
        [Min(0.1f), Tooltip("Move speed multiplier while hunting from afar (an EnemyStats modifier on MoveSpeed)")]
        public float ApproachSpeedMultiplier = 1.6f;
        [Min(1f), Tooltip("Move speed multiplier inside EngageRadius")] public float ChaseSpeedMultiplier = 2.5f;
        [Min(0f), Tooltip("Metres the target must move before the chase re-paths; far away, a tenth of the distance if that's more")]
        public float RepathDistance = 0.5f;
        [Min(0.5f), Tooltip("Metres from the target where an enemy without an attack slot (AttackSlots) waits its turn")]
        public float HoldDistance = 3.5f;
        [Range(0f, 60f), Tooltip("Most degrees an enemy's approach turns off the straight line (each picks its own side and amount), so a group fans out instead of running single file")]
        public float FlankAngle = 30f;
        [Min(0f), Tooltip("Metres from the target beyond which the approach keeps its full angle; it straightens towards the target from here in to EngageRadius")]
        public float FlankFadeDistance = 25f;

        [Header("Swing")]
        [Min(0.1f), Tooltip("Metres from the target (horizontal) a swing starts at")] public float AttackRange = 1.5f;
        [Min(0.1f), Tooltip("Seconds a swing takes, standing (cover the attack clips)")] public float AttackDuration = 1.2f;
        [Min(0f), Tooltip("Seconds after a swing before the next one")] public float AttackCooldown = 0.6f;
        [Min(0f), Tooltip("Seconds into the swing the blow lands; stepping out of reach before then dodges it")]
        public float HitTime = 0.5f;
        [Min(0.1f), Tooltip("Metres the blow reaches when it lands")] public float HitReach = 2f;
        [Range(10f, 360f), Tooltip("Degrees in front of the enemy the blow covers when it lands")] public float HitArc = 90f;

        void OnValidate()
        {
            HitTime = Mathf.Min(HitTime, AttackDuration);
            HitReach = Mathf.Max(HitReach, AttackRange);
            HoldDistance = Mathf.Max(HoldDistance, AttackRange + 0.5f);
        }
    }
}
