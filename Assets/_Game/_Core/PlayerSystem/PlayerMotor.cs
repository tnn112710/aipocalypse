using System;
using MB13.Aipocalypse.Core.Stats;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
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

    // CharacterController locomotion: walk/sprint with stamina, crouch, jump with coyote time and buffering,
    // sliding on slopes past the slope limit, and landing tracking for fall damage and camera dip.
    public sealed class PlayerMotor
    {
        static readonly Collider[] StandUpHits = new Collider[8];

        readonly CharacterController _controller;
        readonly Transform _body;
        readonly PlayerMovementTuning _tuning;
        readonly IStatProvider _stats;
        readonly Stamina _stamina;
        readonly float _standHeight;
        readonly Vector3 _standCenter;
        readonly float _crouchHeight;

        Vector3 _horizontalVelocity;
        float _verticalVelocity;
        float _timeSinceStableGround = float.PositiveInfinity;
        float _jumpBufferLeft;
        float _landingSlowLeft;
        float _apexY;
        bool _isJumping;
        bool _hasGroundHit;
        Vector3 _bestGroundNormal;
        float _crouchBlend;
        // Sprint was held when crouching: it stands up only after a fresh press.
        bool _sprintLatched;

        public bool IsGrounded { get; private set; }
        public bool IsOnSteepSlope { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        // How far the eye sits below standing height (m), eased over CrouchTransitionTime.
        public float CrouchEyeDrop => (_standHeight - _crouchHeight) * Mathf.SmoothStep(0f, 1f, _crouchBlend);
        // Standing still on stable ground with no move input: stamina regenerates at the full rate.
        public bool IsIdle { get; private set; }
        // Moving or coasting on stable ground without sprinting (crouch walking too): stamina regenerates at a share of the rate.
        public bool IsWalking { get; private set; }
        public Vector3 HorizontalVelocity => _horizontalVelocity;
        // Up is positive; on the ground it holds the small stick-down speed, not a real fall.
        public float VerticalVelocity => _verticalVelocity;
        public float WalkSpeed => _stats.Get(StatId.WalkSpeed, _tuning.WalkSpeed);
        public float SprintSpeed => WalkSpeed * _tuning.SprintMultiplier * _stats.GetScale(StatId.SprintSpeedScale);
        public float CrouchSpeed => WalkSpeed * _tuning.CrouchSpeedMultiplier;
        // Ground speed scale set by the player each frame (slower while aiming down sights).
        public float SpeedMultiplier { get; set; } = 1f;
        // Ground speed scale from what the feet are in (mud, snow, water), set by the player each frame (PlayerFooting).
        public float GroundMultiplier { get; set; } = 1f;
        // Sprint stamina cost scale set by the player each frame (a heavy item in hand costs more).
        public float SprintCostMultiplier { get; set; } = 1f;
        // Ground acceleration and braking scale set by the player each frame (a heavy item in hand gets going and stops slower).
        public float AccelerationMultiplier { get; set; } = 1f;
        // Last tick's speed share from the move's angle to the look (1 ahead), and the speed it steered towards (m/s).
        public float DirectionShare { get; private set; }
        public float TargetSpeed { get; private set; }

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
            _standHeight = controller.height;
            _standCenter = controller.center;
            _crouchHeight = Mathf.Clamp(tuning.CrouchHeight, controller.radius * 2f + 0.01f, _standHeight);
        }

        public void Tick(in PlayerIntent intent, float deltaTime)
        {
            UpdateTimers(intent.Jump, deltaTime);

            bool stableGround = IsGrounded && !IsOnSteepSlope;
            Vector2 move = Vector2.ClampMagnitude(intent.Move, 1f);
            bool sprintInput = intent.Sprint && move.y >= _tuning.SprintMinForwardInput && _stamina.CanExert;
            UpdateCrouch(intent.Crouch, intent.Sprint, sprintInput, stableGround, deltaTime);
            bool wantsSprint = sprintInput && !_sprintLatched && !IsCrouching;
            IsSprinting = wantsSprint && stableGround;

            UpdateHorizontal(move, wantsSprint, stableGround, deltaTime);
            if (!TryJump())
                UpdateVertical(stableGround, deltaTime);

            bool hasMove = move.sqrMagnitude > 0.0001f;
            IsIdle = !hasMove && stableGround && !_isJumping
                && _horizontalVelocity.sqrMagnitude <= _tuning.IdleMaxSpeed * _tuning.IdleMaxSpeed;
            // Coasting to a stop after letting go counts as walking, so the regen delay keeps counting down through it.
            IsWalking = !IsIdle && stableGround && !IsSprinting && !_isJumping;

            if (IsSprinting)
                _stamina.Spend(_stats.Get(StatId.SprintStaminaCost, _tuning.SprintStaminaPerSecond) * SprintCostMultiplier * deltaTime);
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
            // Spawn points are open: stand up without checking.
            SetCrouched(false);
            _crouchBlend = 0f;
            _sprintLatched = false;
        }

        // Stands up if the standing capsule fits; false (still crouched) under something low.
        public bool TryStand()
        {
            if (!IsCrouching)
                return true;
            if (StandUpBlocked())
                return false;
            SetCrouched(false);
            _sprintLatched = false;
            return true;
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

        // Speed share by the angle between the move and the look: 1 straight ahead, strafe at 90°, backward at 180°.
        public static float DirectionalSpeedScale(Vector2 moveDirection, float strafeMultiplier, float backwardMultiplier)
        {
            float angle = Vector2.Angle(Vector2.up, moveDirection);
            return angle <= 90f
                ? Mathf.Lerp(1f, strafeMultiplier, angle / 90f)
                : Mathf.Lerp(strafeMultiplier, backwardMultiplier, (angle - 90f) / 90f);
        }

        // The gait's speed × every slowdown (direction, held item, aiming, fatigue, landing), never under the floor
        // (MinSpeedShare of walking) so stacked slowdowns can't stop the player; a gait slower than the floor keeps its own speed.
        public static float SlowedSpeed(float gaitSpeed, float slowdown, float floorSpeed) =>
            Mathf.Max(gaitSpeed * slowdown, Mathf.Min(gaitSpeed, floorSpeed));

        void UpdateTimers(bool jumpPressed, float deltaTime)
        {
            _jumpBufferLeft = jumpPressed ? _tuning.JumpBufferTime : Mathf.Max(0f, _jumpBufferLeft - deltaTime);
            _landingSlowLeft = Mathf.Max(0f, _landingSlowLeft - deltaTime);

            if (IsGrounded && !IsOnSteepSlope)
                _timeSinceStableGround = 0f;
            else
                _timeSinceStableGround += deltaTime;
        }

        // Crouch toggles, on stable ground only. Jump or a fresh sprint press stands up, when there's room.
        void UpdateCrouch(bool crouchPressed, bool sprintHeld, bool sprintInput, bool stableGround, float deltaTime)
        {
            if (!sprintHeld)
                _sprintLatched = false;

            if (crouchPressed)
            {
                if (IsCrouching)
                    TryStand();
                else if (stableGround)
                {
                    SetCrouched(true);
                    _sprintLatched = sprintHeld;
                }
            }
            else if (IsCrouching && _jumpBufferLeft > 0f)
            {
                // Jump while crouched only stands up (when there's room); the next press jumps.
                _jumpBufferLeft = 0f;
                TryStand();
            }
            else if (IsCrouching && sprintInput && !_sprintLatched)
            {
                TryStand();
            }

            float rate = _tuning.CrouchTransitionTime > 0f ? deltaTime / _tuning.CrouchTransitionTime : 1f;
            _crouchBlend = Mathf.MoveTowards(_crouchBlend, IsCrouching ? 1f : 0f, rate);
        }

        // Keeps the feet where they are: the capsule shrinks from the top.
        void SetCrouched(bool crouched)
        {
            IsCrouching = crouched;
            float height = crouched ? _crouchHeight : _standHeight;
            float feet = _standCenter.y - _standHeight * 0.5f;
            _controller.height = height;
            _controller.center = new Vector3(_standCenter.x, feet + height * 0.5f, _standCenter.z);
        }

        // The space the standing capsule adds above the crouched one, slightly thinner so touching a wall doesn't count.
        bool StandUpBlocked()
        {
            float radius = _controller.radius;
            float feet = _standCenter.y - _standHeight * 0.5f;
            Vector3 bottom = _body.TransformPoint(new Vector3(_standCenter.x, feet + _crouchHeight - radius, _standCenter.z));
            Vector3 top = _body.TransformPoint(new Vector3(_standCenter.x, feet + _standHeight - radius, _standCenter.z));
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.95f, StandUpHits, _tuning.StandUpBlockers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (StandUpHits[i] != _controller && !StandUpHits[i].transform.IsChildOf(_body))
                    return true;
            }
            return false;
        }

        void UpdateHorizontal(Vector2 move, bool wantsSprint, bool stableGround, float deltaTime)
        {
            bool hasMove = move.sqrMagnitude > 0.0001f;
            Vector3 forward = Vector3.ProjectOnPlane(_body.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_body.right, Vector3.up).normalized;
            Vector3 wishDirection = hasMove ? (right * move.x + forward * move.y).normalized : Vector3.zero;

            DirectionShare = hasMove ? DirectionalSpeedScale(move.normalized, _tuning.StrafeSpeedMultiplier, _tuning.BackwardSpeedMultiplier) : 0f;
            float slowdown = DirectionShare * SpeedMultiplier * GroundMultiplier;
            if (_landingSlowLeft > 0f && _tuning.HeavyLandingRecoveryTime > 0f)
                slowdown *= Mathf.Lerp(1f, _tuning.HeavyLandingSpeedMultiplier, _landingSlowLeft / _tuning.HeavyLandingRecoveryTime);
            float floorSpeed = WalkSpeed * _tuning.MinSpeedShare;
            TargetSpeed = 0f;

            if (stableGround)
            {
                float gaitSpeed = wantsSprint ? SprintSpeed : IsCrouching ? CrouchSpeed : WalkSpeed;
                TargetSpeed = SlowedSpeed(gaitSpeed, slowdown, floorSpeed) * move.magnitude;
                Vector3 target = wishDirection * TargetSpeed;
                float rate = target.sqrMagnitude >= _horizontalVelocity.sqrMagnitude ? _tuning.GroundAcceleration : _tuning.GroundDeceleration;
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, target, Damping.ByRate(rate * AccelerationMultiplier, deltaTime));
            }
            else if (hasMove)
            {
                // Airborne: steer, keep momentum, never gain sprint speed mid-air; slowed like on foot, so hops don't outrun aiming.
                float gaitSpeed = IsCrouching ? CrouchSpeed : WalkSpeed;
                TargetSpeed = SlowedSpeed(gaitSpeed, slowdown, floorSpeed) * move.magnitude;
                float speed = Mathf.Max(_horizontalVelocity.magnitude, TargetSpeed);
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
            bool wasStable = IsGrounded && !IsOnSteepSlope;
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

            // Sliding down a steep slope counts as falling: the drop and the landing are measured to stable ground.
            if (!IsGrounded || IsOnSteepSlope)
            {
                if (wasStable)
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

            if (!wasStable)
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
