using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Tasks of the robots' tree (AgentScripts/Robots/Robot.tree.txt). Tuning comes from RobotSettings and the robot's
    // personality; the world (night, a raid, the player) from RobotRoster; requests and what happened to it from its State.

    // Walking helpers shared by the tasks.
    static class RobotWalk
    {
        // Sets off toward `point` at `speed`, giving up after the travel timeout for the distance.
        public static void Go(Robot robot, Vector3 point, float speed)
        {
            RobotSettings settings = robot.Settings;
            robot.Mover.SetSpeed(speed);
            robot.Mover.MoveTo(point, RobotCall.TravelTimeout(FlatMath.Length(point - robot.transform.position), speed, settings.TravelTimeout));
        }

        // Walks to a reachable point near `target`; false when there's none.
        public static bool TryGo(Robot robot, Vector3 target, float radius, float speed)
        {
            if (!robot.Mover.TryFindDestination(target, radius, 0f, robot.Settings.DestinationAttempts, out Vector3 point))
                return false;
            Go(robot, point, speed);
            return true;
        }

        public static bool Near(Robot robot, Vector3 point, float distance) => FlatMath.Length(point - robot.transform.position) <= distance;
    }

    // Base of the robots' yes/no conditions: Success while Check() holds. Abstract, so Behavior Designer and TreeKit
    // never list it as a task (trees store the concrete task's type name).
    public abstract class RobotCondition : ConditionalNode
    {
        protected Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => Check() ? TaskStatus.Success : TaskStatus.Failure;

        protected abstract bool Check();
    }
}
