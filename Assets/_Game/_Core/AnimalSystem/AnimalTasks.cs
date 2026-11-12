using MB13.Aipocalypse.Core.EnemySystem;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Tasks of the farm animals' tree (AgentScripts/Animals/Animal.tree.txt). Tuning comes from the animal's definition and
    // the herd's settings; what the herd was told is AnimalHerd.Orders.

    // The bell rang and it isn't in the pen yet. Also the abort check of the walk home.
    [Category("Aipocalypse/Animals")]
    public sealed class CalledHome : ConditionalNode
    {
        Animal _animal;

        public override void OnAwake() => _animal = GetComponent<Animal>();
        public override TaskStatus OnUpdate() => _animal.Herd.Orders.Called && !_animal.IsHome ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Walks into the pen (running once hurried). With no way in (a shut gate) it waits just outside the pen gate and looks
    // again every few seconds.
    [Category("Aipocalypse/Animals")]
    public sealed class WalkHome : ActionNode
    {
        const float RetryInterval = 2f;

        Animal _animal;
        EnemyMover _mover;
        bool _towardsPen;
        float _retryAt;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart() => Plan();

        public override TaskStatus OnUpdate()
        {
            if (_animal.IsHome)
                return TaskStatus.Success;
            if (_towardsPen ? _mover.TimedOut : Time.time >= _retryAt)
                Plan();
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _animal.Calm();
        }

        void Plan()
        {
            AnimalDefinition definition = _animal.Definition;
            AnimalPen pen = _animal.Herd.Pen;
            _retryAt = Time.time + RetryInterval;
            _towardsPen = _mover.TryFindDestination(pen.Center, pen.Radius, 0f, definition.DestinationAttempts, out Vector3 destination);
            if (_towardsPen || _mover.TryFindDestination(pen.WaitPoint, _animal.Herd.Settings.WaitRadius, 0f, definition.DestinationAttempts, out destination))
                _mover.MoveTo(destination, definition.TravelTimeout);
            else
                _mover.Halt();
        }
    }

    // Outside the waking hours. Also the abort check of the sleep.
    [Category("Aipocalypse/Animals")]
    public sealed class IsNight : ConditionalNode
    {
        Animal _animal;

        public override void OnAwake() => _animal = GetComponent<Animal>();
        public override TaskStatus OnUpdate() => _animal.Herd.Asleep ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Lies down where it stands until something aborts it (morning, or the bell).
    [Category("Aipocalypse/Animals")]
    public sealed class SleepHere : ActionNode
    {
        Animal _animal;
        EnemyMover _mover;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _mover.Halt();
            _animal.Sleeping = true;
        }

        public override TaskStatus OnUpdate() => TaskStatus.Running;
        public override void OnEnd() => _animal.Sleeping = false;
    }

    // Picks where it eats next: released, a spot on the pasture near itself (a flock follower: near its leader); otherwise
    // inside the pen, or round where it stands when it was shut out.
    [Category("Aipocalypse/Animals")]
    public sealed class ChooseGrazeSpot : ActionNode
    {
        Animal _animal;
        EnemyMover _mover;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override TaskStatus OnUpdate()
        {
            AnimalHerd herd = _animal.Herd;
            AnimalDefinition definition = _animal.Definition;
            Vector3 destination;
            if (herd.Orders.Released && herd.Pasture != null && TryOnPasture(herd, definition, out destination))
            {
                _animal.Destination = destination;
                return TaskStatus.Success;
            }
            bool found = _animal.IsHome
                ? _mover.TryFindDestination(herd.Pen.Center, herd.Pen.Radius, definition.MinTravel, definition.DestinationAttempts, out destination)
                : _mover.TryFindDestination(transform.position, definition.GrazeRadius * 0.5f, definition.MinTravel, definition.DestinationAttempts, out destination);
            if (!found)
                return TaskStatus.Failure;
            _animal.Destination = destination;
            return TaskStatus.Success;
        }

        bool TryOnPasture(AnimalHerd herd, AnimalDefinition definition, out Vector3 destination)
        {
            AnimalPasture pasture = herd.Pasture;
            Vector3 center = pasture.Center;
            float radius = pasture.Radius * 0.5f;
            Animal leader = definition.FollowsLeader ? herd.LeaderOf(definition) : null;
            if (leader != null && leader != _animal && leader.IsRoaming)
            {
                center = leader.transform.position;
                radius = definition.FlockRadius;
            }
            else if (_animal.IsRoaming)
            {
                center = transform.position;
                radius = definition.GrazeRadius;
            }
            for (int i = 0; i < definition.DestinationAttempts; i++)
                if (_mover.TryFindDestination(center, radius, definition.MinTravel, 1, out destination) && pasture.Contains(destination))
                    return true;
            destination = default;
            return false;
        }
    }

    // Walks to the chosen spot; gives up when the herd is told something new, so it chooses again at once.
    [Category("Aipocalypse/Animals")]
    public sealed class WalkToGrazeSpot : ActionNode
    {
        Animal _animal;
        EnemyMover _mover;
        int _orders;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _orders = _animal.Herd.Orders.Version;
            _mover.MoveTo(_animal.Destination, _animal.Definition.TravelTimeout);
        }

        public override TaskStatus OnUpdate()
        {
            if (_mover.Arrived)
                return TaskStatus.Success;
            return _mover.TimedOut || _animal.Herd.Orders.Version != _orders ? TaskStatus.Failure : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _animal.Calm();
        }
    }

    // Eats where it stands for a while; ends early when the herd is told something new.
    [Category("Aipocalypse/Animals")]
    public sealed class GrazeHere : ActionNode
    {
        Animal _animal;
        float _until;
        int _orders;

        public override void OnAwake() => _animal = GetComponent<Animal>();

        public override void OnStart()
        {
            Vector2 time = _animal.Definition.GrazeTime;
            _until = Time.time + Random.Range(time.x, time.y);
            _orders = _animal.Herd.Orders.Version;
            _animal.Eating = true;
        }

        public override TaskStatus OnUpdate() =>
            Time.time >= _until || _animal.Herd.Orders.Version != _orders ? TaskStatus.Success : TaskStatus.Running;

        public override void OnEnd() => _animal.Eating = false;
    }
}
