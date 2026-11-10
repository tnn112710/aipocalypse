using System;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.SurfaceSystem;
using Ami.BroAudio;
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
        readonly SurfaceService _surfaces;
        readonly Transform _body;
        readonly LayerMask _ground;
        float _nextHurtSound;

        public PlayerFeedback(PlayerMotor motor, PlayerHeadBob headBob, HeldItemSway sway, AudioService audio, PlayerSoundTuning sounds,
            PlayerLife life, SurfaceService surfaces, Transform body, LayerMask ground)
        {
            _surfaces = surfaces;
            _body = body;
            _ground = ground;
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

        void OnStepped() => _audio.Play(FootstepSound(),
            _motor.IsSprinting ? _sounds.SprintStepVolume : _motor.IsCrouching ? _sounds.CrouchStepVolume : _sounds.WalkStepVolume);

        // The footstep of what's underfoot; the player's own when that surface has none.
        SoundID FootstepSound()
        {
            if (_surfaces != null && _surfaces.Enabled &&
                Physics.Raycast(_body.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 1.2f, _ground, QueryTriggerInteraction.Ignore))
            {
                SurfaceLook look = _surfaces.LookAt(hit.collider, hit.point);
                if (look != null && look.Footstep.IsValid())
                    return look.Footstep;
            }
            return _sounds.FootstepSound;
        }

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
