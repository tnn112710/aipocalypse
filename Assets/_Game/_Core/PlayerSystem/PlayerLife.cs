using System;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // The player's life: health, fall damage, dying and coming back. A killing hit raises Died (the body hands the camera to
    // the death sequence) and publishes PlayerDied; once that sequence has played out, PlayerDeathSequenceEnded is published
    // for the respawn flow. Revive restores health, raises Reviving (the body restores the rest) and publishes PlayerRevived.
    public sealed class PlayerLife
    {
        readonly IDamageable _self;
        readonly Transform _body;
        readonly PlayerVitalsTuning _tuning;
        readonly DamageService _damage;
        readonly IPublisher<PlayerDied> _died;
        readonly IPublisher<PlayerDeathSequenceEnded> _deathEnded;
        readonly IPublisher<PlayerRevived> _revived;
        bool _dying;

        public PlayerLife(IDamageable self, Transform body, float maxHealth, PlayerVitalsTuning tuning, DamageService damage,
            IPublisher<PlayerDied> died, IPublisher<PlayerDeathSequenceEnded> deathEnded, IPublisher<PlayerRevived> revived)
        {
            _self = self;
            _body = body;
            _tuning = tuning;
            _damage = damage;
            _died = died;
            _deathEnded = deathEnded;
            _revived = revived;
            Health = new Health(maxHealth);
            SpawnPosition = body.position;
            SpawnRotation = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
        }

        public event Action<DamageType> Died;
        public event Action Reviving;

        public Health Health { get; }
        public bool IsDead => Health.IsDead;
        // Where the player entered the scene; respawns go back there.
        public Vector3 SpawnPosition { get; }
        public Quaternion SpawnRotation { get; }

        public DamageResult TakeDamage(in DamageInfo info)
        {
            float applied = Health.Damage(info.Amount);
            bool killed = applied > 0f && Health.IsDead;
            if (killed)
            {
                _dying = true;
                Died?.Invoke(info.Type);
                _died.Publish(new PlayerDied(_body.position, info.Type));
            }
            return new DamageResult(applied, killed);
        }

        // Linear between the safe and the lethal drop height, dealt through DamageService like any hit.
        public void OnLanded(LandingInfo landing)
        {
            float damage = FallDamage.For(landing.DropHeight, _tuning.SafeFallHeight, _tuning.LethalFallHeight, Health.Max);
            if (damage > 0f)
                _damage.Deal(_self, new DamageInfo(damage, DamageType.Fall, landing.Point, default, _body.gameObject));
        }

        // The death sequence has played out (lying, looking up).
        public void OnDeathSequenceEnded()
        {
            if (!_dying || !IsDead)
                return;
            _dying = false;
            _deathEnded.Publish(new PlayerDeathSequenceEnded(_body.position));
        }

        public void Revive()
        {
            Health.Restore();
            _dying = false;
            Reviving?.Invoke();
            _revived.Publish(new PlayerRevived(_body.position));
        }
    }
}
