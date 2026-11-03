using System.Collections.Generic;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Effects;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.EnemySystem
{
    // An enemy's physical side: takes hits, knockback and its stagger meter (flinch, stagger: EnemyBrain), reacts and cries, and when killed lies, sinks and disables itself
    // (enabling revives it). No Update: EnemyBodyManager advances it while it moves or dies.
    [RequireComponent(typeof(EnemyBrain), typeof(EnemyMover))]
    public sealed class EnemyBody : MonoBehaviour, IDamageable, IShowsOwnHits
    {
        [SerializeField, Tooltip("Sinks into the ground after death: the Model (or mesh) child")] Transform _visual;

        [ShowInInspector, ReadOnly, HideInEditorMode] Health _health;

        readonly List<Collider> _colliders = new();
        EnemyBodyManager _manager;
        EffectPool _effects;
        EnemyBrain _brain;
        EnemyMover _mover;
        EnemyReactionSettings _settings;
        Knockback _knockback;
        StaggerMeter _stagger;
        EnemyEyes _eyes;
        Vector3 _visualRest;
        float _deadFor;

        public bool IsDead { get; private set; }
        public Health Health => _health;
        public Transform Visual => _visual;
        // In EnemyBodyManager's active list.
        internal bool Tracked { get; set; }

        [Inject]
        void Construct(EnemyBodyManager manager, EnemySystemSettings settings, EffectPool effects)
        {
            _manager = manager;
            _effects = effects;
            uint layer = settings.RenderingLayer.value;
            if (layer == 0)
                return;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                r.renderingLayerMask |= layer;
        }

        // On every activation (EnemyBrain): full health from the stats, colliders back on, the visual back in place.
        public void Revive(EnemyStats stats, EnemyReactionSettings settings)
        {
            if (_brain == null)
            {
                _brain = GetComponent<EnemyBrain>();
                _mover = GetComponent<EnemyMover>();
                _eyes = GetComponent<EnemyEyes>();
                foreach (Collider c in GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger)
                        _colliders.Add(c);
                if (_visual != null)
                    _visualRest = _visual.localPosition;
            }
            _settings = settings;
            _knockback = settings != null ? new Knockback(settings) : null;
            _stagger = settings != null && settings.Stagger != null ? new StaggerMeter(settings.Stagger) : null;
            _health = new Health(stats.Get(EnemyStat.MaxHealth));
            IsDead = false;
            _deadFor = 0f;
            foreach (Collider c in _colliders)
                c.enabled = true;
            if (_visual != null)
                _visual.localPosition = _visualRest;
            if (_eyes != null)
                _eyes.SetLit(true);
        }

        public void SetMaxHealth(float max) => _health?.SetMax(max);

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (IsDead || _health == null)
                return default;
            float applied = _health.Damage(info.Amount);
            bool killed = _health.IsDead;

            Vector3 push = info.Force;
            StaggerProfile weight = _settings != null ? _settings.Stagger : null;
            _knockback?.Add(push * ((weight != null ? weight.PushScale : 1f) * (killed ? _settings.KillPushScale : 1f)));
            HitReaction reaction = _stagger != null ? _stagger.Add(push.magnitude, info.Critical, Time.time) : HitReaction.Twitch;
            if (push.sqrMagnitude < 0.0001f)
                push = transform.position - info.Point;
            bool heavy = reaction == HitReaction.Stagger || (_knockback != null && _knockback.Speed >= _settings.HeavyHitSpeed);
            if (killed)
                Die(push);
            else
            {
                if (reaction == HitReaction.Stagger)
                    _brain.Stagger(weight.StaggerTime);
                else if (reaction == HitReaction.Flinch)
                    _brain.Flinch(weight.FlinchSlow, weight.FlinchTime);
                if (_brain.Animation != null)
                    _brain.Animation.Hit(push, heavy);
                _brain.Voice?.Hurt();
            }
            if (_knockback != null && (_knockback.Active || IsDead))
                _manager?.Track(this);
            ShowHit(info, killed);
            return new DamageResult(applied, killed, reaction == HitReaction.Stagger && !killed);
        }

        // Blood (the kind's effects): the hit spurts back towards the shooter, a kill sprays out along the shot.
        void ShowHit(in DamageInfo info, bool killed)
        {
            if (_effects == null || _settings == null)
                return;
            Vector3 along = info.Force.sqrMagnitude > 0.0001f ? info.Force.normalized : (transform.position - info.Point).normalized;
            if (along.sqrMagnitude < 0.0001f)
                along = -transform.forward;
            PooledEffect hit = info.Critical && _settings.CriticalEffect != null ? _settings.CriticalEffect : _settings.HitEffect;
            _effects.Spawn(hit, info.Point, Quaternion.LookRotation(-along));
            if (killed)
                _effects.Spawn(_settings.KillEffect, info.Point, Quaternion.LookRotation(along));
        }

        void Die(Vector3 push)
        {
            IsDead = true;
            _deadFor = 0f;
            foreach (Collider c in _colliders)
                c.enabled = false;
            if (_eyes != null)
                _eyes.SetLit(false);
            _brain.Die();
            if (_brain.Animation != null)
                _brain.Animation.Die(push);
            _brain.Voice?.Death();
        }

        // Returns false once there's nothing left to move (EnemyBodyManager drops it).
        internal bool Advance(float deltaTime)
        {
            if (!isActiveAndEnabled)
                return false;
            if (_knockback != null)
            {
                Vector3 step = _knockback.Advance(deltaTime);
                if (step != Vector3.zero)
                    _mover.Push(step);
            }
            if (!IsDead)
                return _knockback != null && _knockback.Active;

            _deadFor += deltaTime;
            float sink = Mathf.Clamp01((_deadFor - _settings.CorpseTime) / _settings.SinkTime);
            if (_visual != null)
                _visual.localPosition = _visualRest + Vector3.down * (sink * _settings.SinkDepth);
            if (sink < 1f)
                return true;
            gameObject.SetActive(false);
            return false;
        }
    }
}
