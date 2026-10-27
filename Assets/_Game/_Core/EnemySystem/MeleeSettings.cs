using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Tuning of the Melee subtree (notice the player nearby, chase, swing; wander otherwise), read by its tasks.
    [CreateAssetMenu(fileName = "MeleeSettings", menuName = "Aipocalypse/Enemies/Melee Settings")]
    public sealed class MeleeSettings : ScriptableObject
    {
        [Min(0f), Tooltip("Metres: a player this close is noticed")] public float DetectRadius = 5f;
        [Min(0f), Tooltip("Metres: a chased player farther than this is given up (keep it above DetectRadius so it doesn't flicker)")]
        public float LoseRadius = 8f;
        [Min(0.1f), Tooltip("Metres from the player (horizontal) a swing starts at")] public float AttackRange = 1.5f;
        [Min(1f), Tooltip("Move speed multiplier while chasing (an EnemyStats modifier on MoveSpeed)")] public float ChaseSpeedMultiplier = 2.5f;
        [Min(0.1f), Tooltip("Seconds a swing takes, standing (cover the attack clips)")] public float AttackDuration = 1.2f;
        [Min(0f), Tooltip("Seconds after a swing before the next one")] public float AttackCooldown = 0.6f;
        [Min(0f), Tooltip("Metres the player must move before the chase re-targets the path")] public float RepathDistance = 0.5f;

        // Whether a player `sqrDistance` (m², horizontal) away is in reach: noticed inside DetectRadius, kept until past LoseRadius.
        public bool InReach(bool engaged, float sqrDistance)
        {
            float radius = engaged ? Mathf.Max(LoseRadius, DetectRadius) : DetectRadius;
            return sqrDistance <= radius * radius;
        }
    }
}
