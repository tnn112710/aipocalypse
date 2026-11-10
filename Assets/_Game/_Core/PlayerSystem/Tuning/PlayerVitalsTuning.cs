using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerVitalsTuning", menuName = "Aipocalypse/Player/Vitals Tuning")]
    public sealed class PlayerVitalsTuning : ScriptableObject
    {
        [Header("Health"), SerializeField, Min(1f)] float _maxHealth = 100f;
        [SerializeField, Min(0f), Tooltip("Seconds a hit pauses healing over time (bandage)")] float _healPauseOnHit = 3f;

        [Header("Stamina"), SerializeField, Min(1f)] float _maxStamina = 100f;
        [SerializeField, Min(0f)] float _staminaRegenPerSecond = 18f;
        [SerializeField, Min(0f), Tooltip("Seconds standing idle after the last spend before regen starts")]
        float _staminaRegenDelay = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Exhausted at 0: no sprint or jump until stamina refills to this fraction")]
        float _exhaustionRecoverFraction = 0.3f;
        [SerializeField, Min(0f), Tooltip("Seconds stamina stays at 0 after running dry before it starts to recover: heavy breathing, slower moves, shakier aim")]
        float _windedTime = 2f;

        [Header("Out of breath (breath sound, heavier aim sway)"), SerializeField, InlineProperty, HideLabel]
        BreathingSettings _breathing = new();

        [Header("Fatigue (low stamina)"), SerializeField, InlineProperty, HideLabel]
        FatigueSettings _fatigue = new();

        [Header("Fall damage (drop height, m)"), SerializeField, Min(0f)] float _safeFallHeight = 3.5f;
        [SerializeField, Min(0f), Tooltip("Drops this high deal max health")]
        float _lethalFallHeight = 14f;

        public float MaxHealth => _maxHealth;
        public float HealPauseOnHit => _healPauseOnHit;
        public float MaxStamina => _maxStamina;
        public float StaminaRegenPerSecond => _staminaRegenPerSecond;
        public float StaminaRegenDelay => _staminaRegenDelay;
        public float ExhaustionRecoverFraction => _exhaustionRecoverFraction;
        public float WindedTime => _windedTime;
        public BreathingSettings Breathing => _breathing;
        public FatigueSettings Fatigue => _fatigue;
        public float SafeFallHeight => _safeFallHeight;
        public float LethalFallHeight => _lethalFallHeight;
    }
}
