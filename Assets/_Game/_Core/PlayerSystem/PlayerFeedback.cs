using System;
using Aipocalypse.Core.AudioSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // What the body's moves sound and feel like: a footstep at the bottom of each head-bob step, jump and landing sounds, and
    // the head and the held item reacting to jumps and landings.
    public sealed class PlayerFeedback : IDisposable
    {
        readonly PlayerMotor _motor;
        readonly PlayerHeadBob _headBob;
        readonly HeldItemSway _sway;
        readonly AudioService _audio;
        readonly PlayerSoundTuning _sounds;

        public PlayerFeedback(PlayerMotor motor, PlayerHeadBob headBob, HeldItemSway sway, AudioService audio, PlayerSoundTuning sounds)
        {
            _motor = motor;
            _headBob = headBob;
            _sway = sway;
            _audio = audio;
            _sounds = sounds;
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _headBob.Stepped += OnStepped;
        }

        public void Dispose()
        {
            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _headBob.Stepped -= OnStepped;
        }

        void OnStepped() => _audio.Play(_sounds.FootstepSound, _motor.IsSprinting ? _sounds.SprintStepVolume : _sounds.WalkStepVolume);

        void OnJumped()
        {
            _sway.OnJumped();
            _audio.Play(_sounds.JumpSound);
        }

        void OnLanded(LandingInfo landing)
        {
            _headBob.OnLanded(landing.ImpactSpeed);
            _sway.OnLanded(landing.ImpactSpeed);
            if (landing.ImpactSpeed >= _sounds.LandingSoundMinSpeed)
                _audio.Play(_sounds.LandSound, Mathf.Clamp01(landing.ImpactSpeed / _sounds.LandingSoundFullSpeed));
        }
    }
}
