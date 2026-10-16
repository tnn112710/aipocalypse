using Opsive.BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    [RequireComponent(typeof(BehaviorTree), typeof(WanderMover))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        WanderMover _mover;

        public BehaviorTree Tree { get; private set; }

        public void Initialize(AstarPath navigation)
        {
            _mover = GetComponent<WanderMover>();
            _mover.Initialize(navigation);
            Tree = GetComponent<BehaviorTree>();
            Tree.StartBehavior();
        }

        void OnDisable()
        {
            if (Tree != null) Tree.StopBehavior(false);
            if (_mover != null) _mover.Halt();
        }
    }
}
