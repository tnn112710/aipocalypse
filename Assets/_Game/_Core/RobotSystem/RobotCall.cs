using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot walking over to the player (whistled, or welcoming them home): where it meets them, and when it gives up
    // waiting there (the wait ran out, or the player walked away; height ignored). The wait starts once it has arrived.
    public sealed class RobotCall
    {
        float _arrivedAt = float.NaN;

        public bool Active { get; private set; }
        public bool Arrived => Active && !float.IsNaN(_arrivedAt);
        public Vector3 MeetPoint { get; private set; }
        public Vector3 CalledFrom { get; private set; }
        public RobotCallReason Reason { get; private set; }
        // When it was first called (a second whistle while it comes doesn't restart its reaction).
        public float BegunAt { get; private set; }

        public void Begin(Vector3 meetPoint, Vector3 calledFrom, float now, RobotCallReason reason = RobotCallReason.Whistle)
        {
            if (!Active)
                BegunAt = now;
            Active = true;
            Reason = reason;
            MeetPoint = meetPoint;
            CalledFrom = calledFrom;
            _arrivedAt = float.NaN;
        }

        // The player moved on while it walked: head for the new point.
        public void Retarget(Vector3 meetPoint) => MeetPoint = meetPoint;

        // From here on "walked away" is measured from where the player stands now.
        public void MarkArrived(float now, Vector3 player)
        {
            _arrivedAt = now;
            CalledFrom = player;
        }

        // Where a called robot stops: `arriveDistance` from the player on the robot's side, kept `margin` inside the yard
        // (its box seen from above). For a player outside the yard the point is at the robot's height: the ground beyond
        // the fence can be metres off the yard's.
        public static Vector3 PlanMeetPoint(Vector3 robot, Vector3 player, float arriveDistance, Matrix4x4 yardWorldToLocal, Vector3 yardHalfSize, float margin)
        {
            Vector3 away = robot - player;
            away.y = 0f;
            Vector3 target = away.sqrMagnitude > 0.0001f ? player + away.normalized * arriveDistance : player;
            if (!YardMath.Contains(yardWorldToLocal, yardHalfSize, player))
                target.y = robot.y;
            return YardMath.ClosestInside(yardWorldToLocal, yardHalfSize, target, margin);
        }

        // How long a walk of `distance` m at `speed` m/s may take before it gives up.
        public static float TravelTimeout(float distance, float speed, float slack) => distance / Mathf.Max(0.1f, speed) * 1.5f + slack;

        public void End()
        {
            Active = false;
            _arrivedAt = float.NaN;
        }

        public bool ShouldLeave(float now, Vector3 player, float waitSeconds, float leaveDistance)
        {
            if (!Arrived)
                return false;
            Vector3 moved = player - CalledFrom;
            moved.y = 0f;
            return now - _arrivedAt >= waitSeconds || moved.sqrMagnitude > leaveDistance * leaveDistance;
        }
    }
}
