using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Tasks of the shared Wander subtree. Tuning comes from the enemy's definition (EnemyDefinition.Wander).
    [Category("Aipocalypse/Wander")]
    public sealed class ChooseWanderDestination : ActionNode
    {
        EnemyBrain _brain;
        EnemyMover _mover;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override TaskStatus OnUpdate()
        {
            WanderSettings wander = _brain.Definition.Wander;
            if (!_mover.TryFindDestination(_mover.Home, wander.Radius, wander.MinTravelDistance, wander.DestinationAttempts, out Vector3 destination))
                return TaskStatus.Failure;
            _brain.Destination = destination;
            return TaskStatus.Success;
        }
    }

    [Category("Aipocalypse/Wander")]
    public sealed class WalkToWanderDestination : ActionNode
    {
        EnemyBrain _brain;
        EnemyMover _mover;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart() => _mover.MoveTo(_brain.Destination, _brain.Definition.Wander.TravelTimeout);
        public override TaskStatus OnUpdate() => _mover.Arrived ? TaskStatus.Success : _mover.TimedOut ? TaskStatus.Failure : TaskStatus.Running;
        public override void OnEnd() => _mover.Halt();
    }

    [Category("Aipocalypse/Wander")]
    public sealed class PauseWander : ActionNode
    {
        EnemyBrain _brain;
        float _until;

        public override void OnAwake() => _brain = GetComponent<EnemyBrain>();
        public override void OnStart() => _until = Time.time + _brain.Definition.Wander.PauseDuration;
        public override TaskStatus OnUpdate() => Time.time >= _until ? TaskStatus.Success : TaskStatus.Running;
    }
}
