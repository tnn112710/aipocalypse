using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    [Category("Aipocalypse/Wander")]
    public sealed class ChooseWanderDestination : ActionNode
    {
        WanderMover _mover;
        public override void OnAwake() => _mover = GetComponent<WanderMover>();
        public override TaskStatus OnUpdate() => _mover.ChooseDestination() ? TaskStatus.Success : TaskStatus.Failure;
    }

    [Category("Aipocalypse/Wander")]
    public sealed class WalkToWanderDestination : ActionNode
    {
        WanderMover _mover;
        public override void OnAwake() => _mover = GetComponent<WanderMover>();
        public override void OnStart() => _mover.BeginMove();
        public override TaskStatus OnUpdate() => _mover.Arrived ? TaskStatus.Success :
            _mover.TimedOut ? TaskStatus.Failure : TaskStatus.Running;
        public override void OnEnd() => _mover.Halt();
    }

    [Category("Aipocalypse/Wander")]
    public sealed class PauseWander : ActionNode
    {
        WanderMover _mover;
        float _until;
        public override void OnAwake() => _mover = GetComponent<WanderMover>();
        public override void OnStart() => _until = Time.time + _mover.PauseDuration;
        public override TaskStatus OnUpdate() => Time.time >= _until ? TaskStatus.Success : TaskStatus.Running;
    }
}
