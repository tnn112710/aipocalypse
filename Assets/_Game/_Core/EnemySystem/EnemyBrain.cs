using Aipocalypse.Core.EnvironmentSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.EnemySystem
{
    // An enemy's bridge to the enemy system: once injected, starts its Manual-mode tree and joins the EnemyRegistry while
    // enabled, where EnemyManager ticks it (and the scene's GrassBendField, if any, bends grass around it). Its kind (base
    // stats, behaviour tuning) is its EnemyDefinition; each activation gets fresh EnemyStats and a revived EnemyBody.
    [RequireComponent(typeof(BehaviorTree), typeof(EnemyMover))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        [SerializeField, Tooltip("This enemy's kind: Content/Enemies/<Name>/<Name>.asset")] EnemyDefinition _definition;

        EnemyRegistry _registry;
        AstarPath _navigation;
        GrassBendField _grassBending;
        EnemyMover _mover;
        bool _started;

        public EnemyDefinition Definition => _definition;
        // This life's stats (base values + modifiers); null before the first activation.
        public EnemyStats Stats { get; private set; }
        // Null for enemies that can't be hurt.
        public EnemyBody Body { get; private set; }
        public bool IsDead => Body != null && Body.IsDead;
        public BehaviorTree Tree { get; private set; }
        // Null for enemies without an animated model.
        public EnemyAnimator Animation { get; private set; }
        // Where the current walk goes; set by the task that chooses it.
        public Vector3 Destination { get; set; }
        // Slot in the registry's list; -1 while not registered.
        internal int RegistryIndex { get; set; } = -1;

        [Inject]
        void Construct(EnemyRegistry registry, AstarPath navigation, IObjectResolver resolver)
        {
            _registry = registry;
            _navigation = navigation;
            resolver.TryResolve(out _grassBending);
            if (_started && isActiveAndEnabled)
                Activate();
        }

        // First activation waits for Start, so the FollowerEntity and tree have woken up.
        void Start()
        {
            _mover = GetComponent<EnemyMover>();
            Tree = GetComponent<BehaviorTree>();
            Animation = GetComponent<EnemyAnimator>();
            Body = GetComponent<EnemyBody>();
            _started = true;
            if (_definition == null)
                Debug.LogError($"{name}: no EnemyDefinition assigned.", this);
            else if (_registry != null)
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its EnemyInstaller lists it, or spawn it through an EnemySpawner.", this);
        }

        void OnEnable()
        {
            if (_started && _registry != null && _definition != null)
                Activate();
        }

        void OnDisable() => StopActing();

        // Killed (EnemyBody): stops thinking and walking; the body stays until it disables itself, and enabling revives it.
        public void Die() => StopActing();

        void StopActing()
        {
            _registry?.Remove(this);
            if (_grassBending != null)
                _grassBending.Remove(transform);
            if (Tree != null)
                Tree.StopBehavior(false);
            if (_mover != null)
                _mover.Halt();
        }

        void Activate()
        {
            if (RegistryIndex >= 0)
                return;
            Stats = new EnemyStats(_definition);
            Stats.Changed += ApplyStat;
            _mover.Initialize(_navigation, _definition, Stats);
            if (Body != null)
                Body.Revive(Stats, _definition.Reaction);
            if (Animation != null)
                Animation.Initialize(_definition.Animation);
            Tree.StartBehavior();
            _registry.Add(this);
            if (_grassBending != null)
                _grassBending.Add(transform, _definition.GrassBendRadius);
        }

        void ApplyStat(EnemyStat stat)
        {
            switch (stat)
            {
                case EnemyStat.MoveSpeed:
                    _mover.SetSpeed(Stats.Get(stat));
                    break;
                case EnemyStat.MaxHealth:
                    if (Body != null)
                        Body.SetMaxHealth(Stats.Get(stat));
                    break;
            }
        }
    }
}
