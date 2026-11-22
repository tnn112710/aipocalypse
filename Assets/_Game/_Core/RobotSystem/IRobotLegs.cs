using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // How a robot gets about, so the same robot, state and reactions work for any body: RobotGroundLegs walks the navmesh
    // (EnemyMover); a flying robot (a guide) brings legs of its own. One component per robot.
    public interface IRobotLegs
    {
        // World velocity (m/s), for animation.
        Vector3 Velocity { get; }
        // The last MoveTo is over: arrived or timed out.
        bool Settled { get; }
        void Begin(RobotSettings settings);
        void SetSpeed(float speed);
        // Sets off; gives up after `timeout` seconds.
        void MoveTo(Vector3 destination, float timeout);
        // Stands where it is and turns toward `point`.
        void Face(Vector3 point);
        void Halt();
        // A point it can reach within `radius` of `center`, at least `minDistance` from where it is.
        bool TryFindDestination(Vector3 center, float radius, float minDistance, int attempts, out Vector3 destination);
    }
}
