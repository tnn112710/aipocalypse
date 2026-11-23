using Pathfinding;
using Pathfinding.ECS;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // An enemy's legs: walks its FollowerEntity to a destination at its MoveSpeed stat. Behaviour tasks decide where; hits
    // push it (EnemyBody).
    [RequireComponent(typeof(FollowerEntity))]
    public sealed class EnemyMover : MonoBehaviour
    {
        // Face re-targets once the wanted facing turns past this (cos 10 deg).
        const float FacingTolerance = 0.985f;

        FollowerEntity _agent;
        AstarPath _navigation;
        // The prefab's local avoidance setting, restored on each activation.
        bool _avoids;
        bool _avoidsRead;
        float _deadline;
        // Facing set by Face (zero while walking or halted), so a still target doesn't re-path every think.
        Vector3 _facing;
        // Standing still (halted or facing) locks its local avoidance: others steer round it instead of nudging it, which
        // made crowded farm animals shake. Off for enemies.
        bool _holdGround;
        int _traversableTags = NavTags.Walkers;
        float _movedAt = float.NegativeInfinity;
        // Turning on the spot (Face): A* turns a standing agent at its max rotation speed, too quick for a body whose legs
        // step round slower. When set, Face lowers the max to this and walking restores the prefab's.
        float _faceTurnSpeed;
        float _walkTurnSpeed;
        bool _walkTurnRead;

        // Where it was when it started (wander circles around it), or where its last hunt for an objective ended (Rehome).
        public Vector3 Home { get; private set; }

        public void Rehome() => Home = transform.position;
        public bool Arrived => _agent.reachedDestination;
        public bool TimedOut => Time.time >= _deadline;
        // A walk is over: arrived (once the new path has had a moment to replace the old one's arrival) or timed out.
        public bool Settled => TimedOut || (Time.time - _movedAt > 0.3f && Arrived);
        // World velocity (m/s), for animation.
        public Vector3 Velocity => _agent.velocity;
        // Its body's radius on the ground (the FollowerEntity's): whether it fits through a gap.
        public float Radius => _agent.radius;

        public void Initialize(AstarPath navigation, EnemyDefinition definition, EnemyStats stats) =>
            Initialize(navigation, stats.Get(EnemyStat.MoveSpeed), definition.StopDistance);

        // For walkers that aren't enemies (farm animals, robots). `holdGround`: can't be pushed by others while standing still.
        // `traversableTags`: which A* tags it may walk (NavTags; robots alone walk RobotsOnly ground).
        // `faceTurnSpeed`: degrees a second when turning on the spot (0: the agent's max rotation speed).
        public void Initialize(AstarPath navigation, float speed, float stopDistance, bool holdGround = false, int traversableTags = NavTags.Walkers,
            float faceTurnSpeed = 0f)
        {
            _holdGround = holdGround;
            _faceTurnSpeed = faceTurnSpeed;
            _traversableTags = traversableTags;
            _navigation = navigation;
            _agent = GetComponent<FollowerEntity>();
            if (!_avoidsRead)
            {
                _avoids = _agent.enableLocalAvoidance;
                _avoidsRead = true;
            }
            _agent.enableLocalAvoidance = _avoids;
            if (!_walkTurnRead)
            {
                _walkTurnSpeed = _agent.movementSettings.follower.maxRotationSpeed;
                _walkTurnRead = true;
            }
            MaxTurn(_walkTurnSpeed);
            PathRequestSettings pathfinding = _agent.pathfindingSettings;
            pathfinding.traversableTags = traversableTags;
            _agent.pathfindingSettings = pathfinding;
            Home = transform.position;
            _agent.maxSpeed = speed;
            _agent.stopDistance = stopDistance;
            _agent.isStopped = true;
            Lock(true);
        }

        // Puts it at `position` with no path left over (pooled respawns: call after enabling, the agent's entity exists then).
        public void Teleport(Vector3 position)
        {
            if (_agent == null)
                _agent = GetComponent<FollowerEntity>();
            _agent.Teleport(position, true);
        }

        public void SetSpeed(float speed)
        {
            if (_agent != null)
                _agent.maxSpeed = speed;
        }

        // Moves it by `delta` on top of its own walking (knockback); the agent keeps itself on the navmesh.
        public void Push(Vector3 delta)
        {
            if (_agent != null)
                _agent.Move(delta);
        }

        // A walkable point within `radius` of `center`, reachable from here and at least `minDistance` away.
        public bool TryFindDestination(Vector3 center, float radius, float minDistance, int attempts, out Vector3 destination)
        {
            destination = default;
            var origin = _navigation.GetNearest(transform.position, NearestNodeConstraint.Walkable);
            if (origin.node == null)
                return false;
            for (int i = 0; i < attempts; i++)
            {
                var offset = Random.insideUnitCircle * radius;
                var nearest = _navigation.GetNearest(center + new Vector3(offset.x, 0f, offset.y), NearestNodeConstraint.Walkable);
                if (nearest.node == null || !NavTags.Allows(_traversableTags, nearest.node.Tag) || !PathUtilities.IsPathPossible(origin.node, nearest.node))
                    continue;
                if ((nearest.position - center).sqrMagnitude > radius * radius)
                    continue;
                if ((nearest.position - transform.position).sqrMagnitude < minDistance * minDistance)
                    continue;
                destination = nearest.position;
                return true;
            }
            return false;
        }

        // Sets off; TimedOut turns true after `timeout` seconds.
        public void MoveTo(Vector3 destination, float timeout)
        {
            _agent.destination = destination;
            _facing = Vector3.zero;
            _deadline = Time.time + timeout;
            _movedAt = Time.time;
            _agent.isStopped = false;
            MaxTurn(_walkTurnSpeed);
            Lock(false);
        }

        // Walks to `destination` and ends up facing `facing` (A* leads in on an arc): a dog stepping round to face something.
        public void MoveTo(Vector3 destination, Vector3 facing, float timeout)
        {
            _agent.SetDestination(destination, facing);
            _facing = Vector3.zero;
            _deadline = Time.time + timeout;
            _movedAt = Time.time;
            _agent.isStopped = false;
            MaxTurn(_walkTurnSpeed);
            Lock(false);
        }

        // The navmesh point under `point` when one is within `tolerance` of it (flat).
        public bool OnGround(Vector3 point, float tolerance, out Vector3 onMesh)
        {
            var nearest = _navigation.GetNearest(point, NearestNodeConstraint.Walkable);
            onMesh = nearest.position;
            if (nearest.node == null || !NavTags.Allows(_traversableTags, nearest.node.Tag))
                return false;
            Vector3 off = onMesh - point;
            off.y = 0f;
            return off.magnitude <= tolerance;
        }

        // Stands where it is and turns towards `point` (A* turns an agent in place toward the facing of a destination it's on).
        public void Face(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            direction.Normalize();
            if (!_agent.isStopped && Vector3.Dot(direction, _facing) >= FacingTolerance)
                return;
            _facing = direction;
            if (_faceTurnSpeed > 0f)
                MaxTurn(_faceTurnSpeed);
            _agent.SetDestination(transform.position, direction);
            _deadline = float.PositiveInfinity;
            _agent.isStopped = false;
            Lock(true);
        }

        // Dead: others walk through the body instead of steering round it (its colliders are off too).
        public void StopAvoiding()
        {
            if (_agent != null)
                _agent.enableLocalAvoidance = false;
        }

        public void Halt()
        {
            _facing = Vector3.zero;
            if (_agent == null)
                return;
            _agent.isStopped = true;
            Lock(true);
        }

        void MaxTurn(float degreesPerSecond)
        {
            if (!_walkTurnRead)
                return;
            MovementSettings settings = _agent.movementSettings;
            if (Mathf.Approximately(settings.follower.maxRotationSpeed, degreesPerSecond))
                return;
            settings.follower.maxRotationSpeed = degreesPerSecond;
            _agent.movementSettings = settings;
        }

        void Lock(bool standing)
        {
            if (!_holdGround)
                return;
            Pathfinding.ECS.RVO.RVOAgent settings = _agent.rvoSettings;
            if (settings.locked == standing)
                return;
            settings.locked = standing;
            _agent.rvoSettings = settings;
        }
    }
}
