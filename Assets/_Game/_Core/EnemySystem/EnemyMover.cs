using Pathfinding;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // An enemy's legs: walks its FollowerEntity to a destination at its MoveSpeed stat. Behaviour tasks decide where; hits
    // push it (EnemyBody).
    [RequireComponent(typeof(FollowerEntity))]
    public sealed class EnemyMover : MonoBehaviour
    {
        FollowerEntity _agent;
        AstarPath _navigation;
        float _deadline;

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
            Home = transform.position;
            _agent.maxSpeed = stats.Get(EnemyStat.MoveSpeed);
            _agent.stopDistance = definition.StopDistance;
            _agent.isStopped = true;
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
            _deadline = Time.time + timeout;
            _agent.isStopped = false;
        }

        public void Halt()
        {
            if (_agent != null)
                _agent.isStopped = true;
        }
    }
}
