using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerVitalsTuning", menuName = "Aipocalypse/Player/Vitals Tuning")]
    public sealed class PlayerVitalsTuning : ScriptableObject
    {
        [field: Header("Health"), SerializeField, Min(1f)] public float MaxHealth { get; private set; } = 100f;

        [field: Header("Stamina"), SerializeField, Min(1f)] public float MaxStamina { get; private set; } = 100f;
        [field: SerializeField, Min(0f)] public float StaminaRegenPerSecond { get; private set; } = 18f;
        [field: SerializeField, Min(0f), Tooltip("Seconds after the last spend before regen starts")]
        public float StaminaRegenDelay { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Exhausted at 0: no sprint or jump until stamina refills to this fraction")]
        public float ExhaustionRecoverFraction { get; private set; } = 0.3f;

        [field: Header("Fatigue (low stamina)"), SerializeField, InlineProperty, HideLabel]
        public FatigueSettings Fatigue { get; private set; } = new();

        [field: Header("Fall damage (drop height, m)"), SerializeField, Min(0f)] public float SafeFallHeight { get; private set; } = 3.5f;
        [field: SerializeField, Min(0f), Tooltip("Drops this high deal max health")]
        public float LethalFallHeight { get; private set; } = 14f;
    }
}
