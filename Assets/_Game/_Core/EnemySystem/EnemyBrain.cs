using Opsive.BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.EnemySystem
{
    // An enemy's bridge to the enemy system: injected once (by the scene scope when placed, by its spawner when spawned),
    // it starts its Manual-mode tree and joins the EnemyRegistry while enabled; EnemyManager ticks it from there.
    [RequireComponent(typeof(BehaviorTree), typeof(WanderMover))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        EnemyRegistry _registry;
        AstarPath _navigation;
        WanderMover _mover;
        bool _started;

        public BehaviorTree Tree { get; private set; }
        // Slot in the registry's list; -1 while not registered.
        internal int RegistryIndex { get; set; } = -1;

        [Inject]
        void Construct(EnemyRegistry registry, AstarPath navigation)
        {
            _registry = registry;
            _navigation = navigation;
            if (_started && isActiveAndEnabled)
                Activate();
        }

        // First activation waits for Start, so the FollowerEntity and tree have woken up.
        void Start()
        {
            _mover = GetComponent<WanderMover>();
            Tree = GetComponent<BehaviorTree>();
            _started = true;
            if (_registry != null)
                Activate();
            else
                Debug.LogError($"{name}: not injected; list it in the scene's GameLifetimeScope or spawn it through the resolver.", this);
        }

        void OnEnable()
        {
            if (_started && _registry != null)
                Activate();
        }

        void OnDisable()
        {
            _registry?.Remove(this);
            if (Tree != null)
                Tree.StopBehavior(false);
            if (_mover != null)
                _mover.Halt();
        }

        void Activate()
        {
            if (RegistryIndex >= 0)
                return;
            _mover.Initialize(_navigation);
            Tree.StartBehavior();
            _registry.Add(this);
        }
    }
}
