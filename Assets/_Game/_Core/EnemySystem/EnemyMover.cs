using Pathfinding;
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

        // Where it was when it started (wander circles around it).
        public Vector3 Home { get; private set; }
        public bool Arrived => _agent.reachedDestination;
        public bool TimedOut => Time.time >= _deadline;
        // World velocity (m/s), for animation.
        public Vector3 Velocity => _agent.velocity;

        public void Initialize(AstarPath navigation, EnemyDefinition definition, EnemyStats stats)
        {
            _navigation = navigation;
            _agent = GetComponent<FollowerEntity>();
            if (!_avoidsRead)
            {
                _avoids = _agent.enableLocalAvoidance;
                _avoidsRead = true;
            }
            _agent.enableLocalAvoidance = _avoids;
            Home = transform.position;
            _agent.maxSpeed = stats.Get(EnemyStat.MoveSpeed);
            _agent.stopDistance = definition.StopDistance;
            _agent.isStopped = true;
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
                if (nearest.node == null || !PathUtilities.IsPathPossible(origin.node, nearest.node))
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
            _agent.isStopped = false;
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
            _agent.SetDestination(transform.position, direction);
            _deadline = float.PositiveInfinity;
            _agent.isStopped = false;
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
            if (_agent != null)
                _agent.isStopped = true;
        }
    }
}
