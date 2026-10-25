using Aipocalypse.Core.EnvironmentSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.EnemySystem
{
    // An enemy's bridge to the enemy system: once injected, starts its Manual-mode tree and joins the EnemyRegistry while
    // enabled, where EnemyManager ticks it (and the scene's GrassBendField, if any, bends grass around it). Its kind (speed,
    // behaviour tuning) is its EnemyDefinition.
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
        public BehaviorTree Tree { get; private set; }
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

        void OnDisable()
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
            _mover.Initialize(_navigation, _definition);
            Tree.StartBehavior();
            _registry.Add(this);
            if (_grassBending != null)
                _grassBending.Add(transform, _definition.GrassBendRadius);
        }
    }
}
