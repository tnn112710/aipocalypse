using MB13.Aipocalypse.Core.EnemySystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A walking robot's legs: its EnemyMover on the navmesh, allowed on robots-only ground (the house and its door link).
    [RequireComponent(typeof(EnemyMover))]
    [DisallowMultipleComponent]
    public sealed class RobotGroundLegs : MonoBehaviour, IRobotLegs
    {
        const float StopDistance = 0.2f;

        EnemyMover _mover;
        AstarPath _navigation;

        [Inject]
        void Construct(AstarPath navigation) => _navigation = navigation;

        EnemyMover Mover => _mover != null ? _mover : _mover = GetComponent<EnemyMover>();

        public Vector3 Velocity => Mover.Velocity;
        public bool Settled => Mover.Settled;

        public void Begin(RobotSettings settings) =>
            Mover.Initialize(_navigation, settings.WalkSpeed, StopDistance, holdGround: true, traversableTags: NavTags.Robots,
                faceTurnSpeed: settings.FaceTurnSpeed);

        public void SetSpeed(float speed) => Mover.SetSpeed(speed);
        public void MoveTo(Vector3 destination, float timeout) => Mover.MoveTo(destination, timeout);
        public void Face(Vector3 point) => Mover.Face(point);
        public void Halt() => Mover.Halt();

        public bool TryFindDestination(Vector3 center, float radius, float minDistance, int attempts, out Vector3 destination) =>
            Mover.TryFindDestination(center, radius, minDistance, attempts, out destination);
    }
}
