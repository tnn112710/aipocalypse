using MB13.Aipocalypse.Core.EnemySystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's legs: its EnemyMover on the navmesh, allowed on every tag (robots-only ground, its own fence links).
    [RequireComponent(typeof(EnemyMover))]
    [DisallowMultipleComponent]
    public sealed class DogLegs : MonoBehaviour
    {
        const float StopDistance = 0.25f;

        EnemyMover _mover;
        AstarPath _navigation;
        DogSettings _settings;

        [Inject]
        void Construct(AstarPath navigation) => _navigation = navigation;

        EnemyMover Mover => _mover != null ? _mover : _mover = GetComponent<EnemyMover>();

        public Vector3 Velocity => Mover.Velocity;
        public bool Settled => Mover.Settled;
        public bool Arrived => Mover.Arrived;

        public void Begin(DogSettings settings)
        {
            _settings = settings;
            Mover.Initialize(_navigation, settings.TrotSpeed, StopDistance, holdGround: true, traversableTags: NavTags.All);
        }

        public void SetGait(DogGait gait) => Mover.SetSpeed(Speed(gait));
        public void SetSpeed(float speed) => Mover.SetSpeed(speed);
        public void MoveTo(Vector3 destination, float timeout) => Mover.MoveTo(destination, timeout);
        public void Face(Vector3 point) => Mover.Face(point);
        public void Halt() => Mover.Halt();
        public void Warp(Vector3 position) => Mover.Teleport(position);

        public bool TryFindDestination(Vector3 center, float radius, float minDistance, int attempts, out Vector3 destination) =>
            Mover.TryFindDestination(center, radius, minDistance, attempts, out destination);

        public float Speed(DogGait gait) => gait switch
        {
            DogGait.Walk => _settings.WalkSpeed,
            DogGait.Trot => _settings.TrotSpeed,
            DogGait.Canter => _settings.CanterSpeed,
            DogGait.Sprint => _settings.SprintSpeed,
            _ => _settings.WalkSpeed,
        };
    }
}
