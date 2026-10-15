using Opsive.BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    [RequireComponent(typeof(BehaviorTree), typeof(WanderMover))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public BehaviorTree Tree { get; private set; }

        public void Initialize(AstarPath navigation)
        {
            GetComponent<WanderMover>().Initialize(navigation);
            Tree = GetComponent<BehaviorTree>();
            Tree.StartBehavior();
        }

        void OnDisable()
        {
            if (Tree != null) Tree.StopBehavior(false);
            GetComponent<WanderMover>().Halt();
        }
    }
}
