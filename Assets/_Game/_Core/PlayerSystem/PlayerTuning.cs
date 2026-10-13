using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerTuning", menuName = "Aipocalypse/Player/Player Tuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [field: Header("Movement (m/s)"), SerializeField, Min(0f)] public float WalkSpeed { get; private set; } = 3.5f;
        [field: SerializeField, Min(0f)] public float SprintSpeed { get; private set; } = 6f;
        [field: SerializeField, Range(0f, 1f)] public float StrafeSpeedMultiplier { get; private set; } = 0.9f;
        [field: SerializeField, Range(0f, 1f)] public float BackwardSpeedMultiplier { get; private set; } = 0.7f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Forward stick/key amount needed to sprint")]
        public float SprintMinForwardInput { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)] public float GroundAcceleration { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float GroundDeceleration { get; private set; } = 12f;
        [field: SerializeField, Min(0f), Tooltip("Steering while airborne; momentum is kept")]
        public float AirAcceleration { get; private set; } = 1.5f;

        [field: Header("Jump & gravity"), SerializeField, Min(0f)] public float JumpHeight { get; private set; } = 1.1f;
        [field: SerializeField, Min(0.01f)] public float Gravity { get; private set; } = 22f;
        [field: SerializeField, Min(0f)] public float MaxFallSpeed { get; private set; } = 50f;
        [field: SerializeField, Min(0f), Tooltip("Downward speed while grounded, keeps contact on slopes and steps")]
        public float GroundStickSpeed { get; private set; } = 5f;
        [field: SerializeField, Min(0f)] public float CoyoteTime { get; private set; } = 0.12f;
        [field: SerializeField, Min(0f)] public float JumpBufferTime { get; private set; } = 0.12f;
        [field: SerializeField, Min(0f), Tooltip("Slide acceleration on slopes steeper than the CharacterController slope limit")]
        public float SteepSlopeSlideAcceleration { get; private set; } = 12f;

        [field: Header("Landing"), SerializeField, Min(0f), Tooltip("Drops at least this high slow you down briefly")]
        public float HeavyLandingHeight { get; private set; } = 2f;
        [field: SerializeField, Range(0f, 1f)] public float HeavyLandingSpeedMultiplier { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)] public float HeavyLandingRecoveryTime { get; private set; } = 0.4f;

        [field: Header("Fall damage (drop height, m)"), SerializeField, Min(0f)] public float SafeFallHeight { get; private set; } = 3.5f;
        [field: SerializeField, Min(0f), Tooltip("Drops this high deal max health")]
        public float LethalFallHeight { get; private set; } = 14f;

        [field: Header("Health"), SerializeField, Min(1f)] public float MaxHealth { get; private set; } = 100f;

        [field: Header("Stamina"), SerializeField, Min(1f)] public float MaxStamina { get; private set; } = 100f;
        [field: SerializeField, Min(0f)] public float SprintStaminaPerSecond { get; private set; } = 12f;
        [field: SerializeField, Min(0f)] public float JumpStaminaCost { get; private set; } = 12f;
        [field: SerializeField, Min(0f)] public float StaminaRegenPerSecond { get; private set; } = 18f;
        [field: SerializeField, Min(0f), Tooltip("Seconds after the last spend before regen starts")]
        public float StaminaRegenDelay { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Exhausted at 0: no sprint or jump until stamina refills to this fraction")]
        public float ExhaustionRecoverFraction { get; private set; } = 0.3f;

        [field: Header("Look (degrees)"), SerializeField, Range(-90f, 0f)] public float MinPitch { get; private set; } = -85f;
        [field: SerializeField, Range(0f, 90f)] public float MaxPitch { get; private set; } = 85f;

        [field: Header("Head bob"), SerializeField, Min(0.1f), Tooltip("Metres per footstep; bob follows distance, not time")]
        public float StepLength { get; private set; } = 1.7f;
        [field: SerializeField, Min(0f)] public float WalkBobAmplitude { get; private set; } = 0.03f;
        [field: SerializeField, Min(0f)] public float SprintBobAmplitude { get; private set; } = 0.055f;
        [field: SerializeField, Range(0f, 2f)] public float BobSideRatio { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)] public float BobFadeSpeed { get; private set; } = 8f;

        [field: Header("Landing dip"), SerializeField, Min(0f), Tooltip("Impacts slower than this (m/s) don't dip")]
        public float LandingDipMinSpeed { get; private set; } = 3f;
        [field: SerializeField, Min(0f), Tooltip("Dip speed per m/s of impact")]
        public float LandingDipPerSpeed { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float LandingDipMax { get; private set; } = 0.25f;
        [field: SerializeField, Min(1f)] public float LandingDipStiffness { get; private set; } = 120f;
        [field: SerializeField, Min(0f), Tooltip("Head nod in degrees per metre of dip")]
        public float LandingDipPitch { get; private set; } = 15f;
    }
}
