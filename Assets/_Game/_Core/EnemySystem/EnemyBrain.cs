using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Effects;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.SpawnSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.EnemySystem
{
    // An enemy's bridge to the enemy system: while enabled, its Manual tree runs and it's in the EnemyRegistry (ticked by
    // EnemyManager); each activation gets fresh EnemyStats from its EnemyDefinition. Spawned through SpawnService, which wakes
    // it from its pool (Spawnable) and returns it there when it disables.
    [RequireComponent(typeof(BehaviorTree), typeof(EnemyMover), typeof(Spawnable))]
    public sealed class EnemyBrain : MonoBehaviour, ISpawnable
    {
        [SerializeField, Tooltip("This enemy's kind: Content/Enemies/<Name>/<Name>.asset")] EnemyDefinition _definition;

        EnemyRegistry _registry;
        AstarPath _navigation;
        TargetSelector _targets;
        AttackSlots _slots;
        DamageService _damage;
        GrassBendField _grassBending;
        ITargetable _target;
        float _nextRetarget;
        // Source of the flinch's MoveSpeed modifier.
        static readonly object FlinchSource = new();

        // Time.time its appear ends (0: not appearing); requested by the spawner, begun on activation.
        float _appearEnd;
        // Time.time a flinch or a stagger ends (0: none).
        float _flinchEnd;
        float _staggerEnd;
        bool _appearRequested;
        bool _engaged;
        bool _started;

        public EnemyDefinition Definition => _definition;
        // This life's stats (base values + modifiers); null before the first activation.
        public EnemyStats Stats { get; private set; }
        // Null for enemies that can't be hurt.
        public EnemyBody Body { get; private set; }
        public bool IsDead => Body != null && Body.IsDead;
        public BehaviorTree Tree { get; private set; }
        public EnemyMover Mover { get; private set; }
        // Null for enemies without an animated model.
        public EnemyAnimator Animation { get; private set; }
        // Null until injected.
        public EnemyVoice Voice { get; private set; }
        // Where the current walk goes; set by the task that chooses it.
        public Vector3 Destination { get; set; }
        // What this enemy is hunting (chosen by the Melee subtree's ChooseTarget); null while it isn't. Changing it frees the
        // attack slot held on the previous one.
        public ITargetable Target
        {
            get => _target;
            set
            {
                if (ReferenceEquals(_target, value))
                    return;
                _slots?.Release(this);
                _target = value;
                if (value == null)
                    Engaged = false;
                else
                    LastTarget = value;
            }
        }
        // The last target it hunted this life (kept after the hunt ends).
        public ITargetable LastTarget { get; private set; }
        // Hunting nothing because what it last hunted died (the Melee subtree celebrates).
        public bool TargetDefeated => _target == null && LastTarget != null && LastTarget.IsDead;
        // Close to the target: fighting stance and sprint (set by ChaseTarget). Shouts when it starts.
        public bool Engaged
        {
            get => _engaged;
            set
            {
                if (_engaged == value)
                    return;
                _engaged = value;
                if (Animation != null)
                    Animation.SetCombat(value);
                if (value)
                    Voice?.Shout();
            }
        }
        // Earliest Time.time the next swing may start.
        public float NextAttackTime { get; set; }
        // EnemyManager's clock time of its next think while it's far from the player.
        internal float NextThinkAt { get; set; }
        // Slot in the registry's list; -1 while not registered.
        internal int RegistryIndex { get; set; } = -1;
        // Its spawn root: pool, life and whether it counts as alive.
        public Spawnable Spawnable { get; private set; }

        [Inject]
        void Construct(EnemyRegistry registry, AstarPath navigation, TargetSelector targets, AttackSlots slots, DamageService damage,
            AudioService audio, EffectPool effects, IObjectResolver resolver)
        {
            _registry = registry;
            _navigation = navigation;
            _targets = targets;
            _slots = slots;
            _damage = damage;
            Voice = new EnemyVoice(audio, transform);
            resolver.TryResolve(out _grassBending);
            PrewarmEffects(effects);
            if (_started && isActiveAndEnabled)
                Activate();
        }

        // First activation waits for Start, so the FollowerEntity and tree have woken up.
        void Start()
        {
            CacheParts();
            _started = true;
            if (_definition == null)
                Debug.LogError($"{name}: no EnemyDefinition assigned.", this);
            else if (_registry != null)
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its EnemyInstaller lists it, or spawn it through the SpawnService.", this);
        }

        void OnEnable()
        {
            if (_started && _registry != null && _definition != null)
                Activate();
        }

        void OnDisable() => StopActing();

        // Killed (EnemyBody): stops thinking and walking; the body stays until it disables itself, and enabling revives it.
        public void Die()
        {
            StopActing();
            Mover.StopAvoiding();
            if (Spawnable != null)
                Spawnable.Retire();
        }

        // Spawned: the agent drops any old path at its new spot (a fresh instance starts there anyway), then it appears.
        void ISpawnable.OnSpawned(in SpawnInfo info)
        {
            if (RegistryIndex >= 0)
                Mover.Teleport(transform.position);
            Appear();
        }

        void ISpawnable.OnDespawned() { }

        // Just spawned: plays its appear clip and stands still for AppearTime (+ jitter) before its tree first thinks. Called on
        // each spawn; a fresh instance begins it on its first activation.
        public void Appear()
        {
            _appearRequested = true;
            if (RegistryIndex >= 0)
                BeginAppear();
        }

        public bool Appearing => _appearEnd > 0f;
        public bool Staggered => _staggerEnd > 0f;
        // Counts staggers: a task that walks or swings compares it with the count it started with and starts over when it
        // changed (the stagger halted the walk; a swing in progress must not land).
        public int Interruptions { get; private set; }

        // A flinching hit (EnemyBody, StaggerProfile): MoveSpeed × `slow` for `seconds`, restarted by each such hit.
        public void Flinch(float slow, float seconds)
        {
            if (_flinchEnd <= 0f)
                Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, FlinchSource, multiply: slow));
            _flinchEnd = Mathf.Max(_flinchEnd, Time.time + seconds);
        }

        // A staggering hit: stops walking, its swing is cancelled, and the tree doesn't think for `seconds`.
        public void Stagger(float seconds)
        {
            _staggerEnd = Mathf.Max(Time.time + seconds, 0.0001f);
            Interruptions++;
            Mover.Halt();
            if (Animation != null)
                Animation.CancelAttack();
        }

        // One think (EnemyManager): ends a flinch that ran out; nothing while appearing or staggered, else a tick of the tree.
        internal void Think()
        {
            float now = Time.time;
            if (_flinchEnd > 0f && now >= _flinchEnd)
            {
                _flinchEnd = 0f;
                Stats.RemoveModifiers(FlinchSource);
            }
            if (_staggerEnd > 0f)
            {
                if (now < _staggerEnd)
                    return;
                _staggerEnd = 0f;
            }
            if (_appearEnd > 0f)
            {
                if (Time.time < _appearEnd)
                    return;
                _appearEnd = 0f;
                if (Animation != null)
                    Animation.EndAppear();
            }
            Tree.Tick();
        }

        // Picks what to hunt (TargetSelector) every RetargetInterval, or at once when the current one can't be targeted any more.
        public ITargetable ChooseTarget()
        {
            MeleeSettings melee = _definition.Melee;
            if (melee == null)
                return Target = null;
            if (_target != null && _target.IsTargetable && Time.time < _nextRetarget)
                return _target;
            _nextRetarget = Time.time + melee.RetargetInterval;
            return Target = _targets.Choose(transform.position, _definition.Targets);
        }

        // Holds one of the target's attack slots while true (AttackSlots).
        public bool TryHoldSlot() => _slots.TryTake(this, _target);
        public bool HoldsSlot => _slots.Holds(this, _target);
        public void ReleaseSlot() => _slots.Release(this);

        // A melee blow landing on `target`: AttackDamage through DamageService, and the impact sound.
        public void Strike(ITargetable target)
        {
            Vector3 point = target.Transform.position;
            var blow = new DamageInfo(Stats.Get(EnemyStat.AttackDamage), DamageType.Melee, point, default, gameObject);
            DamageResult result = _damage.Deal(target, blow);
            if (result.Applied > 0f)
                Voice.Impact(point);
        }

        void CacheParts()
        {
            if (Mover != null)
                return;
            Mover = GetComponent<EnemyMover>();
            Tree = GetComponent<BehaviorTree>();
            Animation = GetComponent<EnemyAnimator>();
            Body = GetComponent<EnemyBody>();
            Spawnable = GetComponent<Spawnable>();
        }

        // Its kind's hit, critical and kill effects, made when the enemy is (prewarming makes them before a fight).
        void PrewarmEffects(EffectPool effects)
        {
            EnemyReactionSettings reaction = _definition != null ? _definition.Reaction : null;
            if (reaction == null)
                return;
            effects.Prewarm(reaction.HitEffect, reaction.EffectPrewarm);
            effects.Prewarm(reaction.CriticalEffect, reaction.EffectPrewarm);
            effects.Prewarm(reaction.KillEffect, reaction.EffectPrewarm);
        }

        void StopActing()
        {
            _appearRequested = false;
            Target = null;
            _slots?.Release(this);
            _registry?.Remove(this);
            if (_grassBending != null)
                _grassBending.Remove(transform);
            if (Tree != null)
                Tree.StopBehavior(false);
            if (Mover != null)
                Mover.Halt();
        }

        void Activate()
        {
            if (RegistryIndex >= 0)
                return;
            CacheParts();
            NextAttackTime = 0f;
            _nextRetarget = 0f;
            _engaged = false;
            LastTarget = null;
            _appearEnd = 0f;
            _flinchEnd = 0f;
            _staggerEnd = 0f;
            Stats = new EnemyStats(_definition);
            Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, this,
                multiply: CrowdSpread.SpeedFactor(_definition.SpeedVariance, Random.value)));
            Stats.Changed += ApplyStat;
            Mover.Initialize(_navigation, _definition, Stats);
            if (Body != null)
                Body.Revive(Stats, _definition.Reaction);
            if (Animation != null)
                Animation.Initialize(_definition.Animation);
            Voice.Reset(_definition.Sounds);
            Tree.StartBehavior();
            // Woken off-stage while its pool prewarms: set up (the cost paid now) but not in play, so nothing ticks it.
            if (Spawnable != null && Spawnable.State == SpawnState.Warming)
                return;
            _registry.Add(this);
            if (_grassBending != null)
                _grassBending.Add(transform, _definition.GrassBendRadius);
            if (_appearRequested)
                BeginAppear();
        }

        void BeginAppear()
        {
            _appearRequested = false;
            _appearEnd = Mathf.Max(Time.time + _definition.AppearTime + Random.Range(0f, _definition.AppearJitter), 0.0001f);
            if (Animation != null)
                Animation.Appear();
        }

        void ApplyStat(EnemyStat stat)
        {
            switch (stat)
            {
                case EnemyStat.MoveSpeed:
                    Mover.SetSpeed(Stats.Get(stat));
                    break;
                case EnemyStat.MaxHealth:
                    if (Body != null)
                        Body.SetMaxHealth(Stats.Get(stat));
                    break;
            }
        }
    }
}
