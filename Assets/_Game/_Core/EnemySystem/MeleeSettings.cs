using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
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
        [Min(0f), Tooltip("With a way in open, metres the walk through it may add to going straight at the target before it breaks " +
            "its own way in instead (the gap is on the far side)")]
        public float GapDetour = 15f;
        [Range(0f, 1f), Tooltip("With a way in open, this share of the enemies hunting keep breaking a second one: those at the fence " +
            "for whom the open way is furthest")]
        public float BreakerShare = 0.34f;
        [Min(0), Tooltip("At least this many keep breaking while a way in is open (never all of them: a lone one takes the gap)")]
        public int MinBreakers = 1;
        [Min(0f), Tooltip("Metres a new breach keeps from an open way in, so breakers make another gap instead of widening the first")]
        public float GapSpacing = 6f;
        [Min(0f), Tooltip("Within this many metres of a way in it fits through (wider than its body), it always goes through instead " +
            "of breaking a new one. Ways in narrower than its body it widens instead")]
        public float GapNear = 8f;
        [Min(0f), Tooltip("Metres from the target where it takes its fighting stance and sprints")]
        public float EngageRadius = 5f;
        [Min(0.1f), Tooltip("Move speed multiplier while hunting from afar (an EnemyStats modifier on MoveSpeed)")]
        public float ApproachSpeedMultiplier = 1.6f;
        [Min(1f), Tooltip("Move speed multiplier inside EngageRadius")] public float ChaseSpeedMultiplier = 2.5f;
        [Min(0f), Tooltip("Metres the target must move before the chase re-paths; far away, a tenth of the distance if that's more")]
        public float RepathDistance = 0.5f;
        [Min(0.5f), Tooltip("Metres from the target where an enemy without an attack slot (AttackSlots) waits its turn, on its own " +
            "spot round the target so a crowd surrounds instead of queueing")]
        public float HoldDistance = 3.5f;
        [Min(0f), Tooltip("Seconds a waiting enemy keeps its spot round the target before it picks another; 0 = it keeps it")]
        public float HoldShuffleTime = 4f;
        [Min(0f), Tooltip("Metres from its objective (the farm's middle, for a raider that hasn't noticed the player) where it counts " +
            "as arrived and roams there instead")]
        public float ObjectiveRadius = 10f;
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
