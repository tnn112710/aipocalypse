using System;
using Aipocalypse.Core.Stats;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Model-only motion that doesn't change where the item points: position drift and roll around the barrel.
    [Serializable]
    public sealed class HeldSwaySettings
    {
        [Header("Breathing (always on)"), Tooltip("Metres of drift: sideways, up / down, forward")]
        public Vector3 BreathPosition = new(0.0025f, 0.003f, 0f);
        [Tooltip("Degrees of roll")] public float BreathRoll = 0.3f;

        [Header("Footstep bob (in step with the head bob)"), Tooltip("Metres at walking speed: sideways, up / down, forward")]
        public Vector3 WalkBobPosition = new(0.011f, 0.007f, 0.004f);
        [Tooltip("Degrees of roll at walking speed")] public float WalkBobRoll = 1.2f;
        public Vector3 SprintBobPosition = new(0.028f, 0.02f, 0.01f);
        public float SprintBobRoll = 4f;

        [Header("Look lag (trails behind turning)"), Min(0f), Tooltip("Degrees of roll per 100°/s of turning sideways")]
        public float LookRoll = 1.2f;
        [Min(0f)] public float MaxLookRoll = 4f;
        [Min(0f), Tooltip("Metres per 100°/s of turning")] public float LookShift = 0.008f;
        [Min(0f)] public float MaxLookShift = 0.025f;

        [Header("Move lag"), Min(0f), Tooltip("Metres the item trails per m/s of walking")]
        public float MoveShift = 0.004f;
        [Min(0f), Tooltip("Degrees of roll per m/s of strafing")] public float StrafeRoll = 0.8f;
        [Min(0f), Tooltip("Metres per m/s of vertical speed: sinks on a jump, rises while falling")] public float VerticalShift = 0.003f;
        [Min(0f)] public float MaxVerticalShift = 0.04f;
        [Min(0f), Tooltip("Downward kick per m/s of landing speed")] public float LandingKick = 0.03f;

        [Header("Lag spring"), Min(1f), Tooltip("Higher = follows faster")] public float Stiffness = 120f;
        [Range(0.1f, 1f), Tooltip("Below 1 overshoots a little before settling")] public float DampingRatio = 0.6f;

        [Header("Aiming down sights (share kept)"), Range(0f, 1f)] public float AimBreath = 0.35f;
        [Range(0f, 1f)] public float AimBob = 0.15f;
        [Range(0f, 1f)] public float AimLook = 0.3f;
        [Range(0f, 1f)] public float AimMove = 0.25f;
    }

    // Where the item points, off the screen centre: the crosshair, the shots and the model all follow it.
    // Degrees, x = sideways (right), y = up / down (up).
    [Serializable]
    public sealed class AimSwaySettings
    {
        [Header("Breathing"), Tooltip("Degrees at rest: sideways, up / down (a slow figure-8)")]
        public Vector2 Breath = new(0.15f, 0.2f);
        [Min(0f), Tooltip("Breaths per second at rest")] public float BreathRate = 0.28f;
        [Min(0f), Tooltip("Breaths per second when gasping (out of breath)")] public float TiredBreathRate = 0.75f;
        [Min(1f), Tooltip("Breath sway multiplier when gasping; builds as stamina drops under the breathing threshold")]
        public float TiredBreath = 3f;
        [Min(1f), Tooltip("Extra breath sway multiplier while winded (stamina just ran dry)")] public float WindedBreath = 1.5f;

        [Header("Walking (in step with the head bob)"), Tooltip("Degrees at walking speed, moving forward / back: sideways (once per two steps), up / down (each step)")]
        public Vector2 WalkBob = new(0.35f, 1f);
        [Tooltip("Degrees at walking speed, strafing")] public Vector2 StrafeBob = new(1.6f, 0.3f);
        [Min(0f), Tooltip("Bob multiplier at full sprint (the crosshair is hidden; the gun swings)")] public float SprintBob = 2.5f;
        [Tooltip("Degrees the aim trails per m/s: against a strafe (sideways), down while moving forward. Springs back with a small overshoot when you stop")]
        public Vector2 MoveDrift = new(0.2f, 0.08f);

        [Header("Turning"), Min(0f), Tooltip("Degrees the aim trails per 100°/s of turning")] public float LookLag = 0.6f;
        [Min(0f)] public float MaxLookLag = 1f;

        [Header("Jumping and landing"), Min(0f), Tooltip("Degrees the aim dips on takeoff")] public float JumpDip = 1.2f;
        [Min(0f), Tooltip("Degrees of slow random drift while airborne")] public float AirDrift = 2.5f;
        [Min(0f), Tooltip("Air drift speed (cycles per second)")] public float AirDriftRate = 0.6f;
        [Min(0f), Tooltip("Landings slower than this (m/s) don't shake the aim")] public float MinLandingSpeed = 2f;
        [Min(0f), Tooltip("Degrees the aim dips per m/s of landing speed")] public float LandingDip = 0.4f;
        [Min(0f)] public float MaxLandingDip = 4f;
        [Range(0f, 1f), Tooltip("Random sideways jolt on landing, as a share of the dip")] public float LandingJolt = 0.3f;

        [Header("Spring (drift, lag, kicks)"), Min(1f), Tooltip("Higher = settles faster")] public float Stiffness = 90f;
        [Range(0.1f, 1f), Tooltip("Below 1 overshoots and wobbles before settling")] public float DampingRatio = 0.5f;

        [Header("Aiming down sights (share kept)"), Range(0f, 1f)] public float AimBreath = 0.7f;
        [Range(0f, 1f)] public float AimBob = 0.3f;
        [Range(0f, 1f), Tooltip("Move drift and air drift")] public float AimMove = 0.3f;
        [Range(0f, 1f)] public float AimLook = 0.5f;
        [Range(0f, 1f), Tooltip("Jump and landing kicks")] public float AimLanding = 0.5f;
        [Min(0f), Tooltip("Degrees the sights overshoot upwards when raised, then settle")] public float AimInKick = 0.5f;

        [Header("Limits"), Min(0f), Tooltip("Most degrees off the screen centre, at the hip")] public float MaxOffset = 4f;
        [Min(0f)] public float AimMaxOffset = 2f;
    }

    public struct HeldSwayInput
    {
        // This frame's look turn (x = right, y = up).
        public Vector2 LookDegrees;
        // In the body's space: x = strafe, y = vertical (0 on the ground), z = forward.
        public Vector3 LocalVelocity;
        public bool IsAirborne;
        public float BobPhase;
        // 0 standing, 1 at walking speed (from the head bob, so the item bobs in step).
        public float BobIntensity;
        public float SprintBlend;
        public float AimWeight;
        public bool IsAiming;
        // 0 calm .. 1 gasping (PlayerBreathing).
        public float Tiredness;
        public bool IsWinded;
    }

    // Procedural motion of the held item: breathing, footstep bob in step with the head bob, and springs that lag behind
    // turning, moving, jumping and landing. Split in two so the barrel never points away from the crosshair:
    // AimOffset (pitch / yaw) moves the crosshair, the shots and the model together; Position and Rotation (roll only)
    // are model-only extras. Aiming calms each part, being out of breath makes the breathing heavier, and the
    // HeldItemSway stat scales all of it (skills can steady the hands).
    public sealed class HeldItemSway
    {
        const float SpringStep = 1f / 120f;
        const float MaxDeltaTime = 0.1f;
        // The item's up / down bob trails the head's by this many radians, so it settles just after each footfall.
        const float BobLag = 0.5f;
        const float TwoPi = Mathf.PI * 2f;
        // Seconds to blend into / out of the airborne drift, the winded breathing and a new move direction.
        const float BlendTime = 0.15f;

        readonly HeldSwaySettings _settings;
        readonly AimSwaySettings _aim;
        readonly IStatProvider _stats;
        readonly System.Random _random = new(7919);

        Vector3 _lagPosition;
        Vector3 _lagPositionVelocity;
        float _lagRoll;
        float _lagRollVelocity;
        Vector2 _aimLag;
        Vector2 _aimLagVelocity;
        float _breathPhase;
        float _time;
        float _airborne;
        float _winded;
        float _strafeShare;
        float _aimWeight;
        bool _wasAiming;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        // Degrees off the screen centre: x = right, y = up.
        public Vector2 AimOffset { get; private set; }
        // AimOffset as a rotation in the eye's space.
        public Quaternion AimRotation => Quaternion.Euler(-AimOffset.y, AimOffset.x, 0f);

        public HeldItemSway(HeldSwaySettings settings, AimSwaySettings aim, IStatProvider stats)
        {
            _settings = settings;
            _aim = aim;
            _stats = stats;
        }

        public void OnJumped() => Kick(new Vector2(0f, -_aim.JumpDip) * Mathf.Lerp(1f, _aim.AimLanding, _aimWeight));

        public void OnLanded(float impactSpeed)
        {
            _lagPositionVelocity.y -= impactSpeed * _settings.LandingKick;
            if (impactSpeed < _aim.MinLandingSpeed)
                return;
            float dip = Mathf.Min(impactSpeed * _aim.LandingDip, _aim.MaxLandingDip) * Mathf.Lerp(1f, _aim.AimLanding, _aimWeight);
            float jolt = ((float)_random.NextDouble() * 2f - 1f) * _aim.LandingJolt * dip;
            Kick(new Vector2(jolt, -dip));
        }

        public void Tick(in HeldSwayInput input, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            deltaTime = Mathf.Min(deltaTime, MaxDeltaTime);

            float aim = _aimWeight = Mathf.Clamp01(input.AimWeight);
            float scale = Mathf.Max(0f, _stats.Get(StatId.HeldItemSway, 1f));
            float blend = 1f - Mathf.Exp(-deltaTime / BlendTime);
            _time += deltaTime;
            _airborne = Mathf.Lerp(_airborne, input.IsAirborne ? 1f : 0f, blend);
            _winded = Mathf.Lerp(_winded, input.IsWinded ? 1f : 0f, blend);
            if (input.IsAiming && !_wasAiming)
                Kick(new Vector2(((float)_random.NextDouble() * 2f - 1f) * 0.5f, 1f) * _aim.AimInKick);
            _wasAiming = input.IsAiming;

            // Breathing: up / down once per breath, slower sideways drift (period 4π covers both): a figure-8.
            // Faster and bigger out of breath.
            float tiredness = Mathf.Clamp01(input.Tiredness);
            float breathRate = Mathf.Lerp(_aim.BreathRate, _aim.TiredBreathRate, tiredness);
            _breathPhase = (_breathPhase + deltaTime * breathRate * TwoPi) % (2f * TwoPi);
            float b = _breathPhase;
            float heavy = Mathf.Lerp(1f, _aim.TiredBreath, tiredness) * Mathf.Lerp(1f, _aim.WindedBreath, _winded);

            // Footstep bob: sideways once per two steps (with the head), up / down once per step.
            float bob = Mathf.Clamp01(input.BobIntensity);
            float sprint = Mathf.Clamp01(input.SprintBlend);
            float side = Mathf.Cos(input.BobPhase);
            float step = Mathf.Sin(input.BobPhase * 2f - BobLag);

            Vector2 aimSway = AimTick(input, aim, b, heavy, bob * Mathf.Lerp(1f, _aim.SprintBob, sprint), side, step, blend, deltaTime);
            AimOffset = Vector2.ClampMagnitude(aimSway * scale, Mathf.Lerp(_aim.MaxOffset, _aim.AimMaxOffset, aim));
            ModelTick(input, aim, b, heavy, bob, sprint, side, step, scale, deltaTime);
        }

        Vector2 AimTick(in HeldSwayInput input, float aim, float b, float heavy, float bob, float side, float step, float blend, float deltaTime)
        {
            AimSwaySettings s = _aim;
            float move = Mathf.Lerp(1f, s.AimMove, aim);

            // Lag behind moving (a strafe pushes the aim the other way, walking forward dips it) and turning.
            Vector3 v = input.LocalVelocity;
            Vector2 drift = new Vector2(-v.x * s.MoveDrift.x, -v.z * s.MoveDrift.y) * move;
            Vector2 rate = input.LookDegrees / deltaTime * 0.01f;
            Vector2 look = Vector2.ClampMagnitude(-rate * s.LookLag, s.MaxLookLag) * Mathf.Lerp(1f, s.AimLook, aim);
            StepSpring(ref _aimLag, ref _aimLagVelocity, drift + look, s.Stiffness, s.DampingRatio, deltaTime);

            Vector2 breath = Vector2.Scale(s.Breath, new Vector2(Mathf.Sin(b * 0.5f + 1.3f), Mathf.Sin(b))) * (heavy * Mathf.Lerp(1f, s.AimBreath, aim));

            // Forward / back bobs mostly up and down, a strafe mostly sideways; diagonals blend by direction.
            float horizontalSpeed = v.x * v.x + v.z * v.z;
            if (horizontalSpeed > 0.01f)
                _strafeShare = Mathf.Lerp(_strafeShare, v.x * v.x / horizontalSpeed, blend);
            Vector2 bobAmplitude = Vector2.Lerp(s.WalkBob, s.StrafeBob, _strafeShare);
            Vector2 walk = Vector2.Scale(bobAmplitude, new Vector2(side, step)) * (bob * Mathf.Lerp(1f, s.AimBob, aim));

            float t = _time * s.AirDriftRate;
            Vector2 air = new Vector2(Drift(t, 0.31f), Drift(0.73f, t)) * (s.AirDrift * _airborne * move);

            return _aimLag + breath + walk + air;
        }

        void ModelTick(in HeldSwayInput input, float aim, float b, float heavy, float bob, float sprint, float side, float step, float scale, float deltaTime)
        {
            HeldSwaySettings s = _settings;

            // Turning right leaves the item behind to the left, rolled the other way; looking up leaves it low.
            Vector2 rate = input.LookDegrees / deltaTime * 0.01f;
            float look = Mathf.Lerp(1f, s.AimLook, aim);
            Vector3 lookShift = Vector3.ClampMagnitude(new Vector3(-rate.x, -rate.y, 0f) * s.LookShift, s.MaxLookShift) * look;
            float lookRoll = Mathf.Clamp(-rate.x * s.LookRoll, -s.MaxLookRoll, s.MaxLookRoll) * look;

            // Moving leaves it behind; strafing rolls it into the turn; a jump sinks it and a fall lifts it.
            Vector3 v = input.LocalVelocity;
            float move = Mathf.Lerp(1f, s.AimMove, aim);
            Vector3 moveShift = new Vector3(
                -v.x * s.MoveShift,
                Mathf.Clamp(-v.y * s.VerticalShift, -s.MaxVerticalShift, s.MaxVerticalShift),
                -v.z * s.MoveShift) * move;

            StepModelLag(lookShift + moveShift, lookRoll - v.x * s.StrafeRoll * move, deltaTime);

            float breath = heavy * Mathf.Lerp(1f, s.AimBreath, aim);
            Vector3 breathPosition = Vector3.Scale(s.BreathPosition, new Vector3(Mathf.Sin(b * 0.5f + 1.3f), Mathf.Sin(b), 0f)) * breath;
            float breathRoll = s.BreathRoll * Mathf.Sin(b * 0.5f + 0.9f) * breath;

            bob *= Mathf.Lerp(1f, s.AimBob, aim);
            Vector3 bobPosition = Vector3.Scale(Vector3.Lerp(s.WalkBobPosition, s.SprintBobPosition, sprint), new Vector3(side, step, step)) * bob;
            float bobRoll = -Mathf.Lerp(s.WalkBobRoll, s.SprintBobRoll, sprint) * side * bob;

            Position = (_lagPosition + breathPosition + bobPosition) * scale;
            Rotation = Quaternion.Euler(0f, 0f, (_lagRoll + breathRoll + bobRoll) * scale);
        }

        // Smooth noise in -1..1. Perlin mostly stays within ±0.25 of its middle, so it's stretched to reach the full drift.
        static float Drift(float x, float y) => Mathf.Clamp((Mathf.PerlinNoise(x, y) - 0.5f) * 4f, -1f, 1f);

        // A jolt to the aim spring sized so the aim peaks about `degrees` off before it settles.
        void Kick(Vector2 degrees)
        {
            float omega = Mathf.Sqrt(_aim.Stiffness);
            _aimLagVelocity += degrees * (omega / PeakShare(_aim.DampingRatio));
        }

        // Peak displacement of a damped spring kicked from rest, per unit of (kick speed / natural frequency).
        static float PeakShare(float dampingRatio)
        {
            float zeta = Mathf.Clamp(dampingRatio, 0.05f, 0.999f);
            float root = Mathf.Sqrt(1f - zeta * zeta);
            return Mathf.Exp(-zeta / root * Mathf.Atan2(root, zeta));
        }

        void StepModelLag(Vector3 positionTarget, float rollTarget, float deltaTime)
        {
            float stiffness = _settings.Stiffness;
            float damping = 2f * _settings.DampingRatio * Mathf.Sqrt(stiffness);
            for (float left = deltaTime; left > 0f; left -= SpringStep)
            {
                float dt = Mathf.Min(left, SpringStep);
                _lagPositionVelocity += (stiffness * (positionTarget - _lagPosition) - damping * _lagPositionVelocity) * dt;
                _lagPosition += _lagPositionVelocity * dt;
                _lagRollVelocity += (stiffness * (rollTarget - _lagRoll) - damping * _lagRollVelocity) * dt;
                _lagRoll += _lagRollVelocity * dt;
            }
        }

        static void StepSpring(ref Vector2 value, ref Vector2 velocity, Vector2 target, float stiffness, float dampingRatio, float deltaTime)
        {
            float damping = 2f * dampingRatio * Mathf.Sqrt(stiffness);
            for (float left = deltaTime; left > 0f; left -= SpringStep)
            {
                float dt = Mathf.Min(left, SpringStep);
                velocity += (stiffness * (target - value) - damping * velocity) * dt;
                value += velocity * dt;
            }
        }
    }
}
