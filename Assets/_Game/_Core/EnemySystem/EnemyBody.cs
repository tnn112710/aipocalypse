using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // An enemy's physical side: takes hits, knockback and its stagger meter (flinch, stagger: EnemyBrain), reacts and cries, and when killed lies, sinks and disables itself
    // (enabling revives it). No Update: EnemyBodyManager advances it while it moves or dies. A body that rolled loot (its
    // kind's LootTable, into its LootContainer) lies until it's emptied, then sinks a little later.
    [RequireComponent(typeof(EnemyBrain), typeof(EnemyMover))]
    public sealed class EnemyBody : MonoBehaviour, IDamageable, IShowsOwnHits
    {
        const string LootStream = "loot";
        // One block for every flash: its only value is set right before each use.
        static MaterialPropertyBlock _flashBlock;

        [SerializeField, Tooltip("Sinks into the ground after death: the Model (or mesh) child")] Transform _visual;

        [ShowInInspector, ReadOnly, HideInEditorMode] Health _health;

        readonly List<Collider> _colliders = new();
        readonly List<ItemInstance> _lootRoll = new();
        EnemyBodyManager _manager;
        EffectPool _effects;
        EnemySystemSettings _system;
        RandomStream _lootRandom;
        EnemyBrain _brain;
        EnemyMover _mover;
        EnemyReactionSettings _settings;
        Knockback _knockback;
        StaggerMeter _stagger;
        EnemyEyes _eyes;
        LootContainer _loot;
        Vector3 _visualRest;
        float _deadFor;
        SkinnedMeshRenderer[] _flashRenderers;
        float _flashLeft;

        public bool IsDead { get; private set; }
        public Health Health => _health;
        public Transform Visual => _visual;
        // Dead and lying with loot to search.
        public bool HoldsLoot { get; private set; }
        public LootContainer Loot => _loot;
        // In EnemyBodyManager's active list.
        internal bool Tracked { get; set; }

        [Inject]
        void Construct(EnemyBodyManager manager, EnemySystemSettings settings, EffectPool effects, RandomService random)
        {
            _manager = manager;
            _effects = effects;
            _system = settings;
            _lootRandom = random.Stream(LootStream);
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
                if (TryGetComponent(out _loot))
                    _loot.Emptied += OnLootEmptied;
                _flashRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
            ClearFlash();
            _settings = settings;
            _knockback = settings != null ? new Knockback(settings) : null;
            _stagger = settings != null && settings.Stagger != null ? new StaggerMeter(settings.Stagger) : null;
            _health = new Health(stats.Get(EnemyStat.MaxHealth));
            IsDead = false;
            _deadFor = 0f;
            if (HoldsLoot)
                _manager?.RemoveLootBody(this);
            HoldsLoot = false;
            if (_loot != null)
            {
                _loot.Clear();
                _loot.SetSearchable(false);
            }
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
            Flash();
            if ((_knockback != null && (_knockback.Active || IsDead)) || _flashLeft > 0f)
                _manager?.Track(this);
            ShowHit(info, killed);
            return new DamageResult(applied, killed, reaction == HitReaction.Stagger && !killed);
        }

        // The body lights up for a moment. A property block takes a renderer off the SRP Batcher, so it's only on while the
        // flash lasts.
        void Flash()
        {
            if (_settings == null || _settings.HitFlashTime <= 0f || _flashRenderers == null || _flashRenderers.Length == 0)
                return;
            _flashBlock ??= new MaterialPropertyBlock();
            _flashBlock.SetColor(_settings.HitFlashProperty, _settings.HitFlashColor);
            foreach (SkinnedMeshRenderer r in _flashRenderers)
                r.SetPropertyBlock(_flashBlock);
            _flashLeft = _settings.HitFlashTime;
        }

        void ClearFlash()
        {
            if (_flashLeft <= 0f || _flashRenderers == null)
                return;
            _flashLeft = 0f;
            foreach (SkinnedMeshRenderer r in _flashRenderers)
                r.SetPropertyBlock(null);
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
            RollLoot();
        }

        void RollLoot()
        {
            LootTable table = _brain.Definition != null ? _brain.Definition.Loot : null;
            if (_loot == null || table == null || _lootRandom == null)
                return;
            _lootRoll.Clear();
            table.Roll(_lootRandom, _lootRoll);
            BurstLoot();
            _loot.Fill(_lootRoll);
            HoldsLoot = !_loot.Items.IsEmpty;
            _loot.SetSearchable(HoldsLoot);
            if (HoldsLoot)
                _manager?.AddLootBody(this);
        }

        // Auto-collected loot (Tokens) leaves the roll and flies out of the body as a few pickups, for the pickup magnet.
        void BurstLoot()
        {
            int pieces = _system != null ? _system.LootBurstPieces : 0;
            if (pieces <= 0)
                return;
            Vector3 from = transform.position + Vector3.up * _system.LootBurstHeight;
            for (int i = _lootRoll.Count - 1; i >= 0; i--)
            {
                ItemInstance stack = _lootRoll[i];
                ItemDefinition definition = stack.Definition;
                if (!definition.AutoCollect || definition.Prefab == null)
                    continue;
                _lootRoll.RemoveAt(i);
                int left = stack.Count;
                for (int piece = Mathf.Min(pieces, left); piece > 0; piece--)
                {
                    int share = Mathf.CeilToInt(left / (float)piece);
                    left -= share;
                    WorldItem pickup = WorldItem.Spawn(definition.CreateInstance(share), from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null);
                    if (pickup == null)
                        continue;
                    Vector2 side = Random.insideUnitCircle * _system.LootBurstSpeed;
                    pickup.Throw(new Vector3(side.x, _system.LootBurstLift * Random.Range(0.7f, 1.1f), side.y));
                }
            }
        }

        // The cap or dawn took its loot: it sinks now.
        internal void ExpireLoot()
        {
            if (!HoldsLoot)
                return;
            ReleaseLoot(0f);
            _loot.Clear();
        }

        void OnLootEmptied(LootContainer _)
        {
            if (IsDead && HoldsLoot)
                ReleaseLoot(_system != null ? _system.LootedBodySinkDelay : 0f);
        }

        // Stops holding the body: it sinks after `delay` seconds.
        void ReleaseLoot(float delay)
        {
            HoldsLoot = false;
            _manager?.RemoveLootBody(this);
            _loot.SetSearchable(false);
            if (_settings != null)
                _deadFor = Mathf.Max(0f, _settings.CorpseTime - delay);
        }

        // Returns false once there's nothing left to move (EnemyBodyManager drops it).
        internal bool Advance(float deltaTime)
        {
            if (!isActiveAndEnabled)
                return false;
            if (_flashLeft > 0f && _flashLeft - deltaTime <= 0f)
                ClearFlash();
            else if (_flashLeft > 0f)
                _flashLeft -= deltaTime;
            if (_knockback != null)
            {
                Vector3 step = _knockback.Advance(deltaTime);
                if (step != Vector3.zero)
                    _mover.Push(step);
            }
            if (!IsDead)
                return (_knockback != null && _knockback.Active) || _flashLeft > 0f;

            _deadFor += deltaTime;
            if (HoldsLoot)
            {
                // Its death clip has played out by CorpseTime: the Animator stops (it was set to animate off screen too).
                if (_deadFor >= _settings.CorpseTime && _brain.Animation != null)
                    _brain.Animation.Freeze();
                _deadFor = Mathf.Min(_deadFor, _settings.CorpseTime);
                return true;
            }
            float sink =Mathf.Clamp01((_deadFor - _settings.CorpseTime) / _settings.SinkTime);
            if (_visual != null)
                _visual.localPosition = _visualRest + Vector3.down * (sink * _settings.SinkDepth);
            if (sink < 1f)
                return true;
            gameObject.SetActive(false);
            return false;
        }
    }
}
