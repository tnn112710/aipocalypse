using System;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // What the body's moves and hurts sound and feel like: a footstep at the bottom of each head-bob step, jump and landing
    // sounds, the head and the held item reacting to jumps and landings, and a grunt and a flinch on a hit that doesn't kill.
    public sealed class PlayerFeedback : IDisposable
    {
        readonly PlayerMotor _motor;
        readonly PlayerHeadBob _headBob;
        readonly HeldItemSway _sway;
        readonly AudioService _audio;
        readonly PlayerSoundTuning _sounds;
        readonly PlayerLife _life;
        float _nextHurtSound;

        public PlayerFeedback(PlayerMotor motor, PlayerHeadBob headBob, HeldItemSway sway, AudioService audio, PlayerSoundTuning sounds,
            PlayerLife life)
        {
            _motor = motor;
            _headBob = headBob;
            _sway = sway;
            _audio = audio;
            _sounds = sounds;
            _life = life;
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _headBob.Stepped += OnStepped;
            _life.Hurt += OnHurt;
        }

        public void Dispose()
        {
            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _headBob.Stepped -= OnStepped;
            _life.Hurt -= OnHurt;
        }

        void OnHurt(DamageInfo info, float applied)
        {
            _headBob.OnHurt(applied);
            if (Time.time < _nextHurtSound)
                return;
            _nextHurtSound = Time.time + _sounds.HurtSoundCooldown;
            _audio.Play(_sounds.HurtSound);
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
