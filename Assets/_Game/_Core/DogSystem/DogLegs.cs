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
        const float ArcTimeout = 3f;
        const float ArcGround = 0.5f;

        EnemyMover _mover;
        AstarPath _navigation;
        DogSettings _settings;
        bool _arcing;
        Vector3 _arcDirection;

        [Inject]
        void Construct(AstarPath navigation) => _navigation = navigation;

        EnemyMover Mover => _mover != null ? _mover : _mover = GetComponent<EnemyMover>();

        public Vector3 Velocity => Mover.Velocity;
        public bool Settled => Mover.Settled;
        public bool Arrived => Mover.Arrived;
        // On its feet and free to step round (Dog sets it each frame: standing, not swimming, in its house or mid-jump).
        public bool CanStep { get; set; }
        // Walking an arc to face something; settling waits for it.
        public bool Arcing => _arcing && !Mover.Settled;

        public void Begin(DogSettings settings)
        {
            _settings = settings;
            Mover.Initialize(_navigation, settings.TrotSpeed, StopDistance, holdGround: true, traversableTags: NavTags.All,
                faceTurnSpeed: settings.FaceTurnSpeed);
        }

        public void SetGait(DogGait gait) => Mover.SetSpeed(Speed(gait));
        public void SetSpeed(float speed) => Mover.SetSpeed(speed);
        public void MoveTo(Vector3 destination, float timeout)
        {
            _arcing = false;
            Mover.MoveTo(destination, timeout);
        }

        // Turns to face `point`: a big turn as a short walked arc (when it can step, there's ground to land on, and the
        // landing keeps it near `keepNear` if given), a small one on the spot.
        public void Face(Vector3 point) => Face(point, null);

        public void Face(Vector3 point, Vector3? keepNear)
        {
            if (!TryArc(point, keepNear))
                FaceInPlace(point);
        }

        public void FaceInPlace(Vector3 point)
        {
            _arcing = false;
            Mover.Face(point);
        }

        public bool TryArc(Vector3 point, Vector3? keepNear)
        {
            Vector3 from = transform.position;
            Vector3 direction = point - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return false;
            direction.Normalize();
            if (Arcing)
            {
                // The arc under way already points there: keep going.
                if (Vector3.Angle(_arcDirection, direction) < DogTurn.ArcDone)
                    return true;
                _arcing = false;
            }
            if (!CanStep || !DogTurn.WantsArc(transform.forward, direction, _settings.ArcTurnAngle))
                return false;
            Vector3 landing = DogTurn.Landing(from, direction, _settings.ArcTurnStep);
            if (keepNear.HasValue && !DogTurn.KeepsNear(from, landing, keepNear.Value))
                return false;
            if (!Mover.OnGround(landing, ArcGround, out Vector3 onMesh))
                return false;
            _arcing = true;
            _arcDirection = direction;
            Mover.SetSpeed(_settings.WalkSpeed);
            Mover.MoveTo(onMesh, direction, ArcTimeout);
            return true;
        }

        public void Halt()
        {
            _arcing = false;
            Mover.Halt();
        }
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
