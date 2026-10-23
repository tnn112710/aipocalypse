using System;
using Aipocalypse.Core.Stats;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    public readonly struct LandingInfo
    {
        public readonly float DropHeight;
        public readonly float ImpactSpeed;
        public readonly Vector3 Point;

        public LandingInfo(float dropHeight, float impactSpeed, Vector3 point)
        {
            DropHeight = dropHeight;
            ImpactSpeed = impactSpeed;
            Point = point;
        }
    }

    // CharacterController locomotion: walk/sprint with stamina, jump with coyote time and buffering,
    // sliding on slopes past the slope limit, and landing tracking for fall damage and camera dip.
    public sealed class PlayerMotor
    {
        readonly CharacterController _controller;
        readonly Transform _body;
        readonly PlayerMovementTuning _tuning;
        readonly IStatProvider _stats;
        readonly Stamina _stamina;

        Vector3 _horizontalVelocity;
        float _verticalVelocity;
        float _timeSinceStableGround = float.PositiveInfinity;
        float _jumpBufferLeft;
        float _landingSlowLeft;
        float _apexY;
        bool _isJumping;
        bool _hasGroundHit;
        Vector3 _bestGroundNormal;

        public bool IsGrounded { get; private set; }
        public bool IsOnSteepSlope { get; private set; }
        public bool IsSprinting { get; private set; }
        // Standing still on stable ground with no move input: the only state stamina regenerates in.
        public bool IsIdle { get; private set; }
        public Vector3 HorizontalVelocity => _horizontalVelocity;
        // Up is positive; on the ground it holds the small stick-down speed, not a real fall.
        public float VerticalVelocity => _verticalVelocity;
        public float WalkSpeed => _stats.Get(StatId.WalkSpeed, _tuning.WalkSpeed);
        public float SprintSpeed => _stats.Get(StatId.SprintSpeed, _tuning.SprintSpeed);
        // Ground speed scale set by the player each frame (slower while aiming down sights).
        public float SpeedMultiplier { get; set; } = 1f;

        public event Action Jumped;
        public event Action<LandingInfo> Landed;

        public PlayerMotor(CharacterController controller, PlayerMovementTuning tuning, IStatProvider stats, Stamina stamina)
        {
            _controller = controller;
            _body = controller.transform;
            _tuning = tuning;
            _stats = stats;
            _stamina = stamina;
            _apexY = _body.position.y;
        }

        public void Tick(in PlayerIntent intent, float deltaTime)
        {
            UpdateTimers(intent.Jump, deltaTime);

            bool stableGround = IsGrounded && !IsOnSteepSlope;
            Vector2 move = Vector2.ClampMagnitude(intent.Move, 1f);
            bool wantsSprint = intent.Sprint && move.y >= _tuning.SprintMinForwardInput && _stamina.CanExert;
            IsSprinting = wantsSprint && stableGround;

            UpdateHorizontal(move, wantsSprint, stableGround, deltaTime);
            if (!TryJump())
                UpdateVertical(stableGround, deltaTime);

            bool hasMove = move.sqrMagnitude > 0.0001f;
            IsIdle = !hasMove && stableGround && !_isJumping
                && _horizontalVelocity.sqrMagnitude <= _tuning.IdleMaxSpeed * _tuning.IdleMaxSpeed;

            if (IsSprinting)
                _stamina.Spend(_stats.Get(StatId.SprintStaminaCost, _tuning.SprintStaminaPerSecond) * deltaTime);
            else if (hasMove && stableGround)
                _stamina.Spend(_stats.Get(StatId.WalkStaminaCost, _tuning.WalkStaminaPerSecond) * deltaTime);

            Move(deltaTime);
        }

        // Moves the body without sweeping the controller there, and forgets any momentum, jump or fall (no fall damage from the jump).
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            _body.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _jumpBufferLeft = 0f;
            _landingSlowLeft = 0f;
            _isJumping = false;
            _apexY = position.y;
        }

        // Called from Player.OnControllerColliderHit during Move.
        public void OnControllerHit(ControllerColliderHit hit)
        {
            float bottomSphereY = _body.TransformPoint(_controller.center).y - (_controller.height * 0.5f - _controller.radius);
            if (hit.point.y > bottomSphereY)
                return;

            if (!_hasGroundHit || hit.normal.y > _bestGroundNormal.y)
            {
                _bestGroundNormal = hit.normal;
                _hasGroundHit = true;
            }
        }

        public static float DirectionalSpeedScale(Vector2 moveDirection, float strafeMultiplier, float backwardMultiplier)
        {
            return moveDirection.y >= 0f
                ? Mathf.Lerp(strafeMultiplier, 1f, moveDirection.y)
                : Mathf.Lerp(strafeMultiplier, backwardMultiplier, -moveDirection.y);
        }

        void UpdateTimers(bool jumpPressed, float deltaTime)
        {
            _jumpBufferLeft = jumpPressed ? _tuning.JumpBufferTime : Mathf.Max(0f, _jumpBufferLeft - deltaTime);
            _landingSlowLeft = Mathf.Max(0f, _landingSlowLeft - deltaTime);

            if (IsGrounded && !IsOnSteepSlope)
                _timeSinceStableGround = 0f;
            else
                _timeSinceStableGround += deltaTime;
        }

        void UpdateHorizontal(Vector2 move, bool wantsSprint, bool stableGround, float deltaTime)
        {
            bool hasMove = move.sqrMagnitude > 0.0001f;
            Vector3 forward = Vector3.ProjectOnPlane(_body.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_body.right, Vector3.up).normalized;
            Vector3 wishDirection = hasMove ? (right * move.x + forward * move.y).normalized : Vector3.zero;

            float scale = hasMove
                ? DirectionalSpeedScale(move.normalized, _tuning.StrafeSpeedMultiplier, _tuning.BackwardSpeedMultiplier) * move.magnitude
                : 0f;
            if (_landingSlowLeft > 0f && _tuning.HeavyLandingRecoveryTime > 0f)
                scale *= Mathf.Lerp(1f, _tuning.HeavyLandingSpeedMultiplier, _landingSlowLeft / _tuning.HeavyLandingRecoveryTime);

            if (stableGround)
            {
                Vector3 target = wishDirection * ((wantsSprint ? SprintSpeed : WalkSpeed) * scale * SpeedMultiplier);
                float rate = target.sqrMagnitude >= _horizontalVelocity.sqrMagnitude ? _tuning.GroundAcceleration : _tuning.GroundDeceleration;
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, target, Damping.ByRate(rate, deltaTime));
            }
            else if (hasMove)
            {
                // Airborne: steer, keep momentum, never gain sprint speed mid-air.
                float speed = Mathf.Max(_horizontalVelocity.magnitude, WalkSpeed * scale);
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, wishDirection * speed, Damping.ByRate(_tuning.AirAcceleration, deltaTime));
            }

            if (IsOnSteepSlope)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _bestGroundNormal);
                downhill.y = 0f;
                _horizontalVelocity += downhill.normalized * (_tuning.SteepSlopeSlideAcceleration * deltaTime);
            }
        }

        bool TryJump()
        {
            if (_jumpBufferLeft <= 0f || _isJumping || _timeSinceStableGround > _tuning.CoyoteTime || !_stamina.CanExert)
                return false;

            float height = _stats.Get(StatId.JumpHeight, _tuning.JumpHeight);
            _verticalVelocity = Mathf.Sqrt(2f * _tuning.Gravity * height);
            _jumpBufferLeft = 0f;
            _isJumping = true;
            _timeSinceStableGround = float.PositiveInfinity;
            _stamina.Spend(_stats.Get(StatId.JumpStaminaCost, _tuning.JumpStaminaCost));
            Jumped?.Invoke();
            return true;
        }

        void UpdateVertical(bool stableGround, float deltaTime)
        {
            if (stableGround && _verticalVelocity <= 0f)
                _verticalVelocity = -_tuning.GroundStickSpeed;
            else
                _verticalVelocity = Mathf.Max(_verticalVelocity - _tuning.Gravity * deltaTime, -_tuning.MaxFallSpeed);
        }

        void Move(float deltaTime)
        {
            bool wasGrounded = IsGrounded;
            float impactSpeed = -_verticalVelocity;
            Vector3 before = _body.position;
            _hasGroundHit = false;

            CollisionFlags flags = _controller.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * deltaTime);

            IsGrounded = (flags & CollisionFlags.Below) != 0;
            IsOnSteepSlope = IsGrounded && IsSteep();

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;

            // Drop speed lost against walls so it doesn't come back when the wall ends.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = (_body.position - before) / deltaTime;
                actual.y = 0f;
                if (actual.sqrMagnitude < _horizontalVelocity.sqrMagnitude)
                    _horizontalVelocity = actual;
            }

            if (!IsGrounded)
            {
                if (wasGrounded)
                {
                    _apexY = _body.position.y;
                    if (!_isJumping && _verticalVelocity < 0f)
                        _verticalVelocity = 0f;
                }
                else
                {
                    _apexY = Mathf.Max(_apexY, _body.position.y);
                }
                return;
            }

            if (!wasGrounded)
            {
                float drop = _apexY - _body.position.y;
                if (drop >= _tuning.HeavyLandingHeight)
                    _landingSlowLeft = _tuning.HeavyLandingRecoveryTime;
                Landed?.Invoke(new LandingInfo(drop, impactSpeed, _body.position));
            }

            if (_verticalVelocity <= 0f)
                _isJumping = false;
        }

        // A contact normal past the slope limit can also come from a step edge, so confirm with a ray from the capsule's bottom.
        bool IsSteep()
        {
            if (!_hasGroundHit || Vector3.Angle(_bestGroundNormal, Vector3.up) <= _controller.slopeLimit)
                return false;

            float radius = _controller.radius;
            Vector3 bottomSphere = _body.TransformPoint(_controller.center) - Vector3.up * (_controller.height * 0.5f - radius);
            bool flatBelow = Physics.Raycast(bottomSphere, Vector3.down, out RaycastHit hit, radius + _controller.stepOffset,
                                             _tuning.GroundLayers, QueryTriggerInteraction.Ignore)
                             && Vector3.Angle(hit.normal, Vector3.up) <= _controller.slopeLimit;
            return !flatBelow;
        }
    }
}
