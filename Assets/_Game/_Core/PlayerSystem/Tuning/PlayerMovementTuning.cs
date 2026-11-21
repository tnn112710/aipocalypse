using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerMovementTuning", menuName = "Aipocalypse/Player/Movement Tuning")]
    public sealed class PlayerMovementTuning : ScriptableObject
    {
        [Header("Movement (m/s)"), SerializeField, Min(0f)] float _walkSpeed = 2.5f;
        [SerializeField, Min(1f), Tooltip("Sprint speed as a multiple of walking speed")] float _sprintMultiplier = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Speed share moving 90° off where you look; between forward and here it eases with the angle")]
        float _strafeSpeedMultiplier = 0.75f;
        [SerializeField, Range(0f, 1f), Tooltip("Speed share moving straight backward; between the side and here it eases with the angle")]
        float _backwardSpeedMultiplier = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Forward stick/key amount needed to sprint")]
        float _sprintMinForwardInput = 0.5f;
        [SerializeField, Min(0f)] float _groundAcceleration = 10f;
        [SerializeField, Min(0f)] float _groundDeceleration = 12f;
        [SerializeField, Min(0f), Tooltip("Steering while airborne; momentum is kept")]
        float _airAcceleration = 1.5f;

        [Header("Crouch"), SerializeField, Min(0.1f), Tooltip("Capsule height while crouched (m); the eye drops by the difference from standing")]
        float _crouchHeight = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Crouched speed as a share of walking speed")]
        float _crouchSpeedMultiplier = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds for the eye to go down or up")]
        float _crouchTransitionTime = 0.15f;
        [SerializeField, Tooltip("What stops standing up under it (a low beam, a table)")]
        LayerMask _standUpBlockers = Physics.DefaultRaycastLayers;

        [Header("Jump & gravity"), SerializeField, Min(0f)] float _jumpHeight = 1.1f;
        [SerializeField, Min(0.01f)] float _gravity = 22f;
        [SerializeField, Min(0f)] float _maxFallSpeed = 50f;
        [SerializeField, Min(0f), Tooltip("Downward speed while grounded, keeps contact on slopes and steps")]
        float _groundStickSpeed = 5f;
        [SerializeField, Min(0f)] float _coyoteTime = 0.12f;
        [SerializeField, Min(0f)] float _jumpBufferTime = 0.12f;
        [SerializeField, Min(0f), Tooltip("Slide acceleration on slopes steeper than the CharacterController slope limit")]
        float _steepSlopeSlideAcceleration = 12f;
        [SerializeField, Tooltip("What the steep-slope check looks for under the feet (a flat step edge there means no slide)")]
        LayerMask _groundLayers = Physics.DefaultRaycastLayers;

        [Header("Landing"), SerializeField, Min(0f), Tooltip("Drops at least this high slow you down briefly")]
        float _heavyLandingHeight = 2f;
        [SerializeField, Range(0f, 1f)] float _heavyLandingSpeedMultiplier = 0.5f;
        [SerializeField, Min(0f)] float _heavyLandingRecoveryTime = 0.4f;

        [Header("Stamina costs"), SerializeField, Min(0f)] float _walkStaminaPerSecond = 2f;
        [SerializeField, Min(0f)] float _sprintStaminaPerSecond = 12f;
        [SerializeField, Min(0f)] float _jumpStaminaCost = 12f;
        [SerializeField, Min(0f), Tooltip("Stamina regenerates only while standing still: no move input, grounded, and slower than this (m/s)")]
        float _idleMaxSpeed = 0.2f;

        public float WalkSpeed => _walkSpeed;
        public float SprintMultiplier => _sprintMultiplier;
        public float StrafeSpeedMultiplier => _strafeSpeedMultiplier;
        public float BackwardSpeedMultiplier => _backwardSpeedMultiplier;
        public float SprintMinForwardInput => _sprintMinForwardInput;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundDeceleration => _groundDeceleration;
        public float AirAcceleration => _airAcceleration;
        public float CrouchHeight => _crouchHeight;
        public float CrouchSpeedMultiplier => _crouchSpeedMultiplier;
        public float CrouchTransitionTime => _crouchTransitionTime;
        public LayerMask StandUpBlockers => _standUpBlockers;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float MaxFallSpeed => _maxFallSpeed;
        public float GroundStickSpeed => _groundStickSpeed;
        public float CoyoteTime => _coyoteTime;
        public float JumpBufferTime => _jumpBufferTime;
        public float SteepSlopeSlideAcceleration => _steepSlopeSlideAcceleration;
        public LayerMask GroundLayers => _groundLayers;
        public float HeavyLandingHeight => _heavyLandingHeight;
        public float HeavyLandingSpeedMultiplier => _heavyLandingSpeedMultiplier;
        public float HeavyLandingRecoveryTime => _heavyLandingRecoveryTime;
        public float WalkStaminaPerSecond => _walkStaminaPerSecond;
        public float SprintStaminaPerSecond => _sprintStaminaPerSecond;
        public float JumpStaminaCost => _jumpStaminaCost;
        public float IdleMaxSpeed => _idleMaxSpeed;
    }
}
