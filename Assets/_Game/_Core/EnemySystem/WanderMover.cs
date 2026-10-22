using Pathfinding;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    [RequireComponent(typeof(FollowerEntity))]
    public sealed class WanderMover : MonoBehaviour
    {
        [SerializeField] WanderSettings _settings;
        FollowerEntity _agent;
        AstarPath _navigation;
        Vector3 _home;
        float _deadline;

        public float PauseDuration => _settings.PauseDuration;
        public bool Arrived => _agent.reachedDestination;
        public bool TimedOut => Time.time >= _deadline;

        public void Initialize(AstarPath navigation)
        {
            _navigation = navigation;
            _agent = GetComponent<FollowerEntity>();
            _home = transform.position;
            _agent.maxSpeed = _settings.MoveSpeed;
            _agent.stopDistance = _settings.StopDistance;
            _agent.isStopped = true;
        }

        public bool ChooseDestination()
        {
            var origin = _navigation.GetNearest(transform.position, NearestNodeConstraint.Walkable);
            if (origin.node == null) return false;
            float minTravel = _settings.MinTravelDistance;
            for (int i = 0; i < _settings.DestinationAttempts; i++)
            {
                var offset = Random.insideUnitCircle * _settings.Radius;
                var candidate = _home + new Vector3(offset.x, 0f, offset.y);
                var nearest = _navigation.GetNearest(candidate, NearestNodeConstraint.Walkable);
                if (nearest.node == null || !PathUtilities.IsPathPossible(origin.node, nearest.node)) continue;
                if ((nearest.position - _home).sqrMagnitude > _settings.Radius * _settings.Radius) continue;
                if ((nearest.position - transform.position).sqrMagnitude < minTravel * minTravel) continue;
                _agent.destination = nearest.position;
                return true;
            }
            return false;
        }

        public void BeginMove()
        {
            _deadline = Time.time + _settings.TravelTimeout;
            _agent.isStopped = false;
        }

        public void Halt()
        {
            if (_agent != null) _agent.isStopped = true;
        }
    }
}
