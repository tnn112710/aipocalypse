using System;
using Aipocalypse.Core.Stats;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [Serializable]
    public sealed class HeldSwaySettings
    {
        [Header("Breathing (always on)"), Tooltip("Metres of drift: sideways, up / down, forward")]
        public Vector3 BreathPosition = new(0.0025f, 0.003f, 0f);
        [Tooltip("Degrees: pitch, yaw, roll")] public Vector3 BreathEuler = new(0.35f, 0.25f, 0.3f);
        [Min(0f), Tooltip("Breaths per second")] public float BreathRate = 0.28f;

        [Header("Footstep bob (in step with the head bob)"), Tooltip("Metres at walking speed: sideways, up / down, forward")]
        public Vector3 WalkBobPosition = new(0.011f, 0.007f, 0.004f);
        [Tooltip("Degrees at walking speed: pitch, yaw, roll")] public Vector3 WalkBobEuler = new(0.7f, 0.6f, 1.2f);
        public Vector3 SprintBobPosition = new(0.028f, 0.02f, 0.01f);
        public Vector3 SprintBobEuler = new(2.2f, 2f, 4f);

        [Header("Look lag (trails behind turning)"), Min(0f), Tooltip("Degrees of pitch / yaw per 100°/s of turning")]
        public float LookTilt = 1.5f;
        [Min(0f), Tooltip("Degrees of roll per 100°/s of turning sideways")] public float LookRoll = 1.2f;
        [Min(0f)] public float MaxLookTilt = 4f;
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

    public struct HeldSwayInput
    {
        // This frame's look turn (x = right, y = up).
        public Vector2 LookDegrees;
        // In the body's space: x = strafe, y = vertical (0 on the ground), z = forward.
        public Vector3 LocalVelocity;
        public float BobPhase;
        // 0 standing, 1 at walking speed (from the head bob, so the item bobs in step).
        public float BobIntensity;
        public float SprintBlend;
        public float AimWeight;
    }

    // Procedural motion of the held item relative to the camera: breathing, footstep bob in step with the head bob,
    // and lag behind turning, moving, jumping and landing (springs, so it overshoots a little and settles).
    // Aiming calms each part; the HeldItemSway stat scales all of it (skills can steady the hands).
    public sealed class HeldItemSway
    {
        const float SpringStep = 1f / 120f;
        const float MaxDeltaTime = 0.1f;
        // The item's up / down bob trails the head's by this many radians, so it settles just after each footfall.
        const float BobLag = 0.5f;
        const float TwoPi = Mathf.PI * 2f;

        readonly HeldSwaySettings _settings;
        readonly IStatProvider _stats;

        Vector3 _lagPosition;
        Vector3 _lagPositionVelocity;
        Vector3 _lagEuler;
        Vector3 _lagEulerVelocity;
        float _breathPhase;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;

        public HeldItemSway(HeldSwaySettings settings, IStatProvider stats)
        {
            _settings = settings;
            _stats = stats;
        }

        public void OnLanded(float impactSpeed) => _lagPositionVelocity.y -= impactSpeed * _settings.LandingKick;

        public void Tick(in HeldSwayInput input, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            deltaTime = Mathf.Min(deltaTime, MaxDeltaTime);

            HeldSwaySettings s = _settings;
            float aim = Mathf.Clamp01(input.AimWeight);
            float scale = Mathf.Max(0f, _stats.Get(StatId.HeldItemSway, 1f));

            StepLag(LagTarget(input, aim, deltaTime, out Vector3 eulerTarget), eulerTarget, deltaTime);

            // Breathing: up / down once per breath, slower sideways drift (period 4π covers both).
            _breathPhase = (_breathPhase + deltaTime * s.BreathRate * TwoPi) % (2f * TwoPi);
            float b = _breathPhase;
            float breath = Mathf.Lerp(1f, s.AimBreath, aim);
            Vector3 breathPosition = Vector3.Scale(s.BreathPosition, new Vector3(Mathf.Sin(b * 0.5f + 1.3f), Mathf.Sin(b), 0f)) * breath;
            Vector3 breathEuler = Vector3.Scale(s.BreathEuler, new Vector3(Mathf.Sin(b + 0.4f), Mathf.Sin(b * 0.5f), Mathf.Sin(b * 0.5f + 0.9f))) * breath;

            // Footstep bob: sideways once per two steps (with the head), up / down once per step.
            float bob = Mathf.Clamp01(input.BobIntensity) * Mathf.Lerp(1f, s.AimBob, aim);
            float sprint = Mathf.Clamp01(input.SprintBlend);
            float side = Mathf.Cos(input.BobPhase);
            float step = Mathf.Sin(input.BobPhase * 2f - BobLag);
            Vector3 bobPosition = Vector3.Scale(Vector3.Lerp(s.WalkBobPosition, s.SprintBobPosition, sprint), new Vector3(side, step, step)) * bob;
            Vector3 bobEuler = Vector3.Scale(Vector3.Lerp(s.WalkBobEuler, s.SprintBobEuler, sprint), new Vector3(step, side, -side)) * bob;

            Position = (_lagPosition + breathPosition + bobPosition) * scale;
            Rotation = Quaternion.Euler((_lagEuler + breathEuler + bobEuler) * scale);
        }

        Vector3 LagTarget(in HeldSwayInput input, float aim, float deltaTime, out Vector3 eulerTarget)
        {
            HeldSwaySettings s = _settings;

            // Turning right leaves the item behind to the left, turned and rolled the other way; looking up leaves it low.
            // In units of 100°/s, as the settings are.
            Vector2 rate = input.LookDegrees / deltaTime * 0.01f;
            float look = Mathf.Lerp(1f, s.AimLook, aim);
            Vector3 lookShift = Vector3.ClampMagnitude(new Vector3(-rate.x, -rate.y, 0f) * s.LookShift, s.MaxLookShift) * look;
            Vector3 lookTilt = new Vector3(
                Mathf.Clamp(rate.y * s.LookTilt, -s.MaxLookTilt, s.MaxLookTilt),
                Mathf.Clamp(-rate.x * s.LookTilt, -s.MaxLookTilt, s.MaxLookTilt),
                Mathf.Clamp(-rate.x * s.LookRoll, -s.MaxLookTilt, s.MaxLookTilt)) * look;

            // Moving leaves it behind; strafing rolls it into the turn; a jump sinks it and a fall lifts it.
            Vector3 v = input.LocalVelocity;
            float move = Mathf.Lerp(1f, s.AimMove, aim);
            Vector3 moveShift = new Vector3(
                -v.x * s.MoveShift,
                Mathf.Clamp(-v.y * s.VerticalShift, -s.MaxVerticalShift, s.MaxVerticalShift),
                -v.z * s.MoveShift) * move;

            eulerTarget = lookTilt + new Vector3(0f, 0f, -v.x * s.StrafeRoll * move);
            return lookShift + moveShift;
        }

        void StepLag(Vector3 positionTarget, Vector3 eulerTarget, float deltaTime)
        {
            float stiffness = _settings.Stiffness;
            float damping = 2f * _settings.DampingRatio * Mathf.Sqrt(stiffness);
            for (float left = deltaTime; left > 0f; left -= SpringStep)
            {
                float dt = Mathf.Min(left, SpringStep);
                _lagPositionVelocity += (stiffness * (positionTarget - _lagPosition) - damping * _lagPositionVelocity) * dt;
                _lagPosition += _lagPositionVelocity * dt;
                _lagEulerVelocity += (stiffness * (eulerTarget - _lagEuler) - damping * _lagEulerVelocity) * dt;
                _lagEuler += _lagEulerVelocity * dt;
            }
        }
    }
}
