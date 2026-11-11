using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // An elite tier of an enemy kind (EnemyDefinition.Elite): rolled per life, applied as stat modifiers, marked by its size
    // and eyes. Shared by the goblin kinds in Data/Enemies/.
    [CreateAssetMenu(fileName = "EliteSettings", menuName = "Aipocalypse/Enemies/Elite Settings")]
    public sealed class EliteSettings : ScriptableObject
    {
        [Range(0f, 1f), Tooltip("Chance each life is elite (a spawner can force it: SpawnOrders.Elite). A flat chance until " +
            "the raid's Threat decides")]
        public float Chance = 0.1f;
        [Min(0f), Tooltip("MaxHealth multiplier")] public float HealthMultiplier = 2f;
        [Min(0f), Tooltip("AttackDamage multiplier")] public float DamageMultiplier = 1.5f;
        [Min(0f), Tooltip("MoveSpeed multiplier")] public float SpeedMultiplier = 1.1f;
        [Min(0.1f), Tooltip("Body scale: the visible mark (hit colliders scale with it)")] public float Scale = 1.15f;
        [Tooltip("Its eyes' material while elite (an EnemyEyeGlow material in another colour); empty = the kind's own")]
        public Material EyeMaterial;
    }
}
