using System;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Procedural head motion (through the camera rig): footstep bob driven by distance travelled, and a spring dip on landing.
    // Stepped fires at the bottom of each bob, when a foot plants.
    public sealed class PlayerHeadBob
    {
        const float MaxSpringStep = 0.05f;

        readonly PlayerCameraRig _rig;
        readonly PlayerCameraTuning _tuning;

        float _phase;
        float _intensity;
        float _sprintBlend;
        float _dip;
        float _dipVelocity;

        public event Action Stepped;

        // Read by the held item's bob so it moves in step: phase advances π per step, intensity is 0 standing .. 1 walking.
        public float Phase => _phase;
        public float Intensity => _intensity;
        public float SprintBlend => _sprintBlend;

        public PlayerHeadBob(PlayerCameraRig rig, PlayerCameraTuning tuning)
        {
            _rig = rig;
            _tuning = tuning;
        }

        public void OnLanded(float impactSpeed)
        {
            if (impactSpeed >= _tuning.LandingDipMinSpeed)
                _dipVelocity -= impactSpeed * _tuning.LandingDipPerSpeed;
        }

        public void Tick(PlayerMotor motor, float deltaTime)
        {
            float speed = motor.HorizontalVelocity.magnitude;
            float fade = Damping.ByRate(_tuning.BobFadeSpeed, deltaTime);
            float targetIntensity = motor.IsGrounded ? Mathf.Clamp01(speed / Mathf.Max(motor.WalkSpeed, 0.01f)) : 0f;
            _intensity = Mathf.Lerp(_intensity, targetIntensity, fade);
            _sprintBlend = Mathf.Lerp(_sprintBlend, motor.IsSprinting ? 1f : 0f, fade);

            // Half a cycle per step: vertical bob once per step, sideways sway once per two steps.
            if (motor.IsGrounded)
            {
                float previous = _phase;
                _phase = (_phase + speed * deltaTime / _tuning.StepLength * Mathf.PI) % (2f * Mathf.PI);
                // The bob (sin 2·phase) bottoms out at 3π/4 and 7π/4.
                if (StepIndex(_phase) != StepIndex(previous) && _intensity > _tuning.StepMinIntensity)
                    Stepped?.Invoke();
            }

            float amplitude = Mathf.Lerp(_tuning.WalkBobAmplitude, _tuning.SprintBobAmplitude, _sprintBlend) * _intensity;
            Vector3 bob = new(
                Mathf.Cos(_phase) * amplitude * _tuning.BobSideRatio,
                Mathf.Sin(_phase * 2f) * amplitude,
                0f);

            UpdateDip(Mathf.Min(deltaTime, MaxSpringStep));

            _rig.SetHeadMotion(bob + Vector3.up * _dip, -_dip * _tuning.LandingDipPitch);
        }

        // Which half of the cycle; flips at each bob bottom, not at the 2π wrap.
        static int StepIndex(float phase) => Mathf.FloorToInt((phase + Mathf.PI * 0.25f) / Mathf.PI) % 2;

        // Critically damped spring back to rest.
        void UpdateDip(float deltaTime)
        {
            float stiffness = _tuning.LandingDipStiffness;
            float acceleration = -stiffness * _dip - 2f * Mathf.Sqrt(stiffness) * _dipVelocity;
            _dipVelocity += acceleration * deltaTime;
            _dip += _dipVelocity * deltaTime;

            if (_dip < -_tuning.LandingDipMax)
            {
                _dip = -_tuning.LandingDipMax;
                _dipVelocity = Mathf.Max(0f, _dipVelocity);
            }
        }
    }
}
