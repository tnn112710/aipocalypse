using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerMovementTuning", menuName = "Aipocalypse/Player/Movement Tuning")]
    public sealed class PlayerMovementTuning : ScriptableObject
    {
        [field: Header("Movement (m/s)"), SerializeField, Min(0f)] public float WalkSpeed { get; private set; } = 3.5f;
        [field: SerializeField, Min(0f)] public float SprintSpeed { get; private set; } = 6f;
        [field: SerializeField, Range(0f, 1f)] public float StrafeSpeedMultiplier { get; private set; } = 0.9f;
        [field: SerializeField, Range(0f, 1f)] public float BackwardSpeedMultiplier { get; private set; } = 0.6f;
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

        [field: Header("Stamina costs"), SerializeField, Min(0f)] public float SprintStaminaPerSecond { get; private set; } = 12f;
        [field: SerializeField, Min(0f)] public float JumpStaminaCost { get; private set; } = 12f;
    }
}
