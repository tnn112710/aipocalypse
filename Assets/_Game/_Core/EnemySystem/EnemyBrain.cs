using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // An enemy's bridge to the enemy system: while enabled, its Manual tree runs and it's in the EnemyRegistry (ticked by
    // EnemyManager); each activation gets fresh EnemyStats from its EnemyDefinition. Spawned through SpawnService, which wakes
    // it from its pool (Spawnable) and returns it there when it disables.
    [RequireComponent(typeof(BehaviorTree), typeof(EnemyMover), typeof(Spawnable))]
    public sealed class EnemyBrain : MonoBehaviour, ISpawnable
    {
        // Seconds between two looks for the player; the sight slack keeps one it knows a little past SpotRange.
        const float SenseInterval = 0.2f;
        const float KeepSightSlack = 1.5f;
        const string EliteStream = "elites";

        [SerializeField, Tooltip("This enemy's kind: Content/Enemies/<Name>/<Name>.asset")] EnemyDefinition _definition;

        EnemyRegistry _registry;
        AstarPath _navigation;
        TargetRegistry _targetRegistry;
        TargetSelector _targets;
        AttackSlots _slots;
        DamageService _damage;
        RandomService _random;
        ProjectileSystem _projectiles;
        // This life's distance to throw from; 0 = not picked yet.
        float _standOff;
        GrassBendField _grassBending;
        EnemyEyes _eyes;
        ITargetable _target;
        float _nextRetarget;
        ITargetable _retaliate;
        float _retaliateUntil;
        float _retaliateReach;
        int _barrierVersion;
        readonly Awareness _awareness = new();
        float _nextSense;
        SpawnOrders _orders;
        Vector3 _restScale = Vector3.one;
        // Sources of the flinch's and the elite tier's modifiers.
        static readonly object FlinchSource = new();
        static readonly object EliteSource = new();

        // Time.time its appear ends (0: not appearing); requested by the spawner, begun on activation.
        float _appearEnd;
        // Time.time a flinch or a stagger ends (0: none).
        float _flinchEnd;
        float _staggerEnd;
        bool _appearRequested;
        bool _engaged;
        readonly ActivationGate _gate = new();

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
        // Knows the player is there (spotted, hit or alarmed); until then it goes for its objective or roams.
        public bool Alerted => _awareness.Alerted;
        // This life is elite (EnemyDefinition.Elite: rolled, or ordered by its spawner).
        public bool IsElite { get; private set; }
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
        void Construct(EnemyRegistry registry, AstarPath navigation, TargetRegistry targetRegistry, TargetSelector targets, AttackSlots slots,
            DamageService damage, RandomService random, AudioService audio, EffectPool effects, IObjectResolver resolver)
        {
            _registry = registry;
            _navigation = navigation;
            _targetRegistry = targetRegistry;
            _targets = targets;
            _slots = slots;
            _damage = damage;
            _random = random;
            Voice = new EnemyVoice(audio, transform);
            resolver.TryResolve(out _grassBending);
            resolver.TryResolve(out _projectiles);
            PrewarmEffects(effects);
            if (_gate.OnConstructed(isActiveAndEnabled))
                Activate();
        }

        // First activation waits for Start, so the FollowerEntity and tree have woken up.
        void Start()
        {
            CacheParts();
            bool injected = _gate.OnStarted();
            if (_definition == null)
                Debug.LogError($"{name}: no EnemyDefinition assigned.", this);
            else if (injected)
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its EnemyInstaller lists it, or spawn it through the SpawnService.", this);
        }

        void OnEnable()
        {
            if (_gate.OnEnabled() && _definition != null)
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

        // Spawned: the agent drops any old path at its new spot (a fresh instance starts there anyway), takes its spawner's
        // orders, then it appears.
        void ISpawnable.OnSpawned(in SpawnInfo info)
        {
            if (RegistryIndex >= 0)
                Mover.Teleport(transform.position);
            _orders = info.Handle?.Tag is SpawnOrders orders ? orders : default;
            if (Stats != null && _orders.Elite && !IsElite && _definition.Elite != null)
                MakeElite(_definition.Elite);
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

        // It knows the player is there: hit (EnemyBody), or alarmed by a neighbour. The first time it shouts, chooses again at
        // once and, with `alarm`, alarms the enemies within AlertRadius (they don't pass it on).
        public void Notice(bool alarm)
        {
            if (_awareness.Notice(Time.time))
                Noticed(alarm);
        }

        // Hit by `source`: a hit from the player (or from nothing known) gives the player away and alarms its neighbours. A
        // dog's bite or a fence's thorns don't: the dog's bite makes it Retaliate instead.
        public void NoticeHitFrom(GameObject source)
        {
            ITargetable player = _targetRegistry != null ? _targetRegistry.Player : null;
            if (source == null || player == null || source == player.Transform.gameObject)
                Notice(true);
        }

        // One think (EnemyManager): ends a flinch that ran out; nothing while appearing or staggered, else a look for the
        // player and a tick of the tree.
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
            if (now >= _nextSense)
                Sense(now);
            Tree.Tick();
        }

        // Provoked (the dog bit it, or bit a neighbour): it may go for `target` until `until`, but only while it's within `reach`
        // (melee reach plus a little; a thrower's range): it never chases. Checked on every think in ChooseTarget.
        public void Retaliate(ITargetable target, float until, float reach)
        {
            _retaliate = target;
            _retaliateUntil = until;
            _retaliateReach = reach;
            _nextRetarget = 0f;
        }

        public bool Retaliating => _retaliate != null;

        public static bool KeepsRetaliating(Vector3 self, Vector3 target, float reach, float now, float until, bool targetable) =>
            targetable && now < until && MeleeHit.SqrFlatDistance(self, target) <= reach * reach;

        // Picks what to hunt (TargetSelector) every RetargetInterval, or at once when the current one can't be targeted any more,
        // a barrier started or stopped blocking (a gap opened: TargetSelector decides who goes through and who keeps breaking),
        // or it noticed or lost the player. Unaware, it goes for its orders' objective; arrived there, it roams there.
        public ITargetable ChooseTarget()
        {
            MeleeSettings melee = _definition.Melee;
            if (melee == null)
                return Target = null;
            if (_retaliate != null)
            {
                if (KeepsRetaliating(transform.position, _retaliate.Transform.position, _retaliateReach, Time.time, _retaliateUntil, _retaliate.IsTargetable))
                    return Target = _retaliate;
                _retaliate = null;
                _nextRetarget = 0f;
            }
            if (_target != null && _target.IsTargetable && Time.time < _nextRetarget && _barrierVersion == _targets.BarrierVersion)
                return _target;
            _nextRetarget = Time.time + melee.RetargetInterval;
            _barrierVersion = _targets.BarrierVersion;
            var rules = new BreachRules(melee.BreachCandidates, melee.FinishBreachBlows * Stats.Get(EnemyStat.AttackDamage), melee.GapDetour,
                melee.BreakerShare, melee.MinBreakers, melee.GapSpacing, melee.GapNear, 2f * Mover.Radius);
            ITargetable objective = _orders.Objective == TargetKind.Farm ? _targetRegistry.Farm : null;
            ITargetable choice = _targets.Choose(this, transform.position, _definition.Targets, _target, rules, _awareness.Alerted, objective);
            if (choice != null && ReferenceEquals(choice, objective)
                && MeleeHit.SqrFlatDistance(transform.position, choice.Transform.position) <= melee.ObjectiveRadius * melee.ObjectiveRadius)
            {
                if (_target != null)
                    Mover.Rehome();
                choice = null;
            }
            return Target = choice;
        }

        // Holds one of the target's attack slots while true (AttackSlots).
        public bool TryHoldSlot() => _slots.TryTake(this, _target);
        public bool HoldsSlot => _slots.Holds(this, _target);
        public void ReleaseSlot() => _slots.Release(this);

        // A melee blow landing on `target`: AttackDamage through DamageService, and the impact sound (a barrier plays its own).
        // Ranged kinds: where a throw leaves from.
        public Vector3 LaunchPoint => Animation != null ? Animation.LaunchPoint : transform.position + Vector3.up * 1.2f;

        // Metres from its target this life throws from: its own pick inside the kind's band, so a group doesn't line up.
        public float StandOff(RangedSettings ranged)
        {
            if (_standOff <= 0f)
                _standOff = Random.Range(ranged.StandOff.x, ranged.StandOff.y);
            return _standOff;
        }

        // No throw reached from where it stood: it stands closer from now on (never inside MinRange).
        public void CloseIn(RangedSettings ranged, float share) =>
            _standOff = Mathf.Max(ranged.MinRange + 1f, StandOff(ranged) * share);

        public void Throw(in ProjectileShot shot) => _projectiles?.Launch(shot);

        public void Strike(ITargetable target)
        {
            Vector3 point = target.PointFor(transform.position);
            var blow = new DamageInfo(Stats.Get(EnemyStat.AttackDamage), DamageType.Melee, point, default, gameObject);
            DamageResult result = _damage.Deal(target, blow);
            if (result.Applied > 0f && target is not IBreachable)
                Voice.Impact(point);
        }

        // Looks for the player: within SpotRange (1.5 × once it knows) with nothing solid between its eyes and their chest.
        // SpotRange 0 = it always knows.
        void Sense(float now)
        {
            _nextSense = now + SenseInterval;
            ITargetable player = _targetRegistry.Player;
            bool sees = false;
            if (player != null && player.IsTargetable)
            {
                float range = _definition.SpotRange * (_awareness.Alerted ? KeepSightSlack : 1f);
                Vector3 point = player.Transform.position + Vector3.up * EnemySight.TargetHeight;
                sees = _definition.SpotRange <= 0f || (MeleeHit.SqrFlatDistance(transform.position, point) <= range * range
                    && EnemySight.IsClear(transform.position + Vector3.up * EnemySight.EyeHeight, point, _definition.SightBlockers, transform, player.Transform));
            }
            int change = _awareness.Sense(sees, now, _definition.LoseSightTime);
            if (change > 0)
                Noticed(true);
            else if (change < 0)
                Forgot();
        }

        void Noticed(bool alarm)
        {
            _nextRetarget = 0f;
            Voice?.Shout();
            if (!alarm || _registry == null)
                return;
            float radius = _definition.AlertRadius;
            for (int i = 0; i < _registry.Count; i++)
            {
                EnemyBrain other = _registry[i];
                if (other != this && !other.IsDead && !other.Alerted
                    && MeleeHit.SqrFlatDistance(transform.position, other.transform.position) <= radius * radius)
                    other.Notice(false);
            }
        }

        // Lost the player: drops them as a target and chooses again at once (its objective, or nothing).
        void Forgot()
        {
            _nextRetarget = 0f;
            if (_target != null && _target.Kind == TargetKind.Player)
                Target = null;
        }

        void MakeElite(EliteSettings elite)
        {
            IsElite = true;
            Stats.AddModifier(new EnemyStatModifier(EnemyStat.MaxHealth, EliteSource, multiply: elite.HealthMultiplier));
            Stats.AddModifier(new EnemyStatModifier(EnemyStat.AttackDamage, EliteSource, multiply: elite.DamageMultiplier));
            Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, EliteSource, multiply: elite.SpeedMultiplier));
            transform.localScale = _restScale * elite.Scale;
            if (_eyes != null)
                _eyes.SetMaterial(elite.EyeMaterial);
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
            _eyes = GetComponent<EnemyEyes>();
            _restScale = transform.localScale;
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
            _orders = default;
            Target = null;
            _slots?.Release(this);
            _targets?.Forget(this);
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
            _standOff = 0f;
            _nextRetarget = 0f;
            _engaged = false;
            LastTarget = null;
            _appearEnd = 0f;
            _flinchEnd = 0f;
            _staggerEnd = 0f;
            _awareness.Reset();
            _nextSense = Time.time + Random.value * SenseInterval;
            IsElite = false;
            transform.localScale = _restScale;
            if (_eyes != null)
                _eyes.SetMaterial(null);
            Stats = new EnemyStats(_definition);
            Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, this,
                multiply: CrowdSpread.SpeedFactor(_definition.SpeedVariance, Random.value)));
            if (_definition.Elite != null && _random != null && _random.Stream(EliteStream).Chance(_definition.Elite.Chance))
                MakeElite(_definition.Elite);
            // A fresh instance takes its orders before Start activates it (pooled ones get them after, in OnSpawned).
            if (_orders.Elite && !IsElite && _definition.Elite != null)
                MakeElite(_definition.Elite);
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
