using MB13.Aipocalypse.Core.EnemySystem;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Tasks of the farm animals' tree (AgentScripts/Animals/Animal.tree.txt). Tuning comes from the animal's definition and
    // the herd's settings; what the herd was told is AnimalHerd.Orders, what this animal was told is Animal.SentHome.

    // On its way into the pen: the bell rang, or the player sent it home. Also the abort check of the walk home.
    [Category("Aipocalypse/Animals")]
    public sealed class CalledHome : ConditionalNode
    {
        Animal _animal;

        public override void OnAwake() => _animal = GetComponent<Animal>();
        public override TaskStatus OnUpdate() => _animal.GoingHome ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Walks into the pen (trotting once hurried). With no way in (a shut gate) it waits just outside the pen gate and looks
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

    // Something made it move away (a gunshot, the player walking into it). Also the abort check of the move.
    [Category("Aipocalypse/Animals")]
    public sealed class IsStartled : ConditionalNode
    {
        Animal _animal;

        public override void OnAwake() => _animal = GetComponent<Animal>();
        public override TaskStatus OnUpdate() => _animal.Startled ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Runs (a gunshot) or steps (the player) StartleDistance away from what startled it, then settles. Inside the pen it
    // keeps to the pen: the spot furthest from the scare among a few inside it.
    [Category("Aipocalypse/Animals")]
    public sealed class MoveAway : ActionNode
    {
        const float SpareSeconds = 3f;

        Animal _animal;
        EnemyMover _mover;
        float _until;
        bool _runs;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart() => Plan();

        public override TaskStatus OnUpdate()
        {
            // A gunshot while it was stepping aside: run instead.
            if (_animal.StartleRuns != _runs)
                Plan();
            return _mover.Arrived || Time.time >= _until ? TaskStatus.Success : TaskStatus.Running;
        }

        void Plan()
        {
            _runs = _animal.StartleRuns;
            AnimalDefinition definition = _animal.Definition;
            float speed = _animal.StartleRuns ? definition.FleeSpeed : definition.WalkSpeed;
            float distance = _animal.StartleDistance;
            _until = Time.time + distance / speed + SpareSeconds;
            _mover.SetSpeed(speed);
            if (TryChoose(distance, out Vector3 destination))
                _mover.MoveTo(destination, _until - Time.time);
            else
                _until = Time.time;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _mover.SetSpeed(_animal.Hurrying ? _animal.Definition.HurrySpeed : _animal.Definition.WalkSpeed);
            _animal.Settle();
        }

        bool TryChoose(float distance, out Vector3 destination)
        {
            Vector3 here = transform.position;
            Vector3 from = _animal.StartledFrom;
            Vector3 away = here - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            away.Normalize();
            int attempts = _animal.Definition.DestinationAttempts;
            AnimalPen pen = _animal.Herd.Pen;
            if (_animal.IsHome)
            {
                destination = default;
                float best = -1f;
                for (int i = 0; i < attempts; i++)
                {
                    if (!_mover.TryFindDestination(pen.Center, pen.Radius, 0f, 1, out Vector3 spot) || !pen.Contains(spot))
                        continue;
                    float score = (spot - from).sqrMagnitude;
                    if (score <= best)
                        continue;
                    best = score;
                    destination = spot;
                }
                return best >= 0f;
            }
            return _mover.TryFindDestination(here + away * distance, distance * 0.3f, 0f, attempts, out destination);
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

    // Lies down where it stands until something aborts it (morning, the bell, a scare).
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

    // Picks where it eats next: released and not sent home, a spot on the pasture near itself (a flock follower: near where
    // its leader is heading); otherwise inside the pen, or round where it stands when it was shut out.
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
            if (herd.Orders.Released && !_animal.SentHome && herd.Pasture != null && TryOnPasture(herd, definition, out destination))
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
            float minTravel = definition.MinTravel;
            Animal leader = AnimalFlock.LeaderOf(_animal);
            if (leader != null)
            {
                // Where the leader is heading: the flock moves with it instead of gathering where it was.
                center = pasture.Contains(leader.Destination) ? leader.Destination : leader.transform.position;
                radius = definition.FlockRadius;
                minTravel = 0f;
            }
            else if (_animal.IsRoaming)
            {
                center = transform.position;
                radius = definition.GrazeRadius;
            }
            for (int i = 0; i < definition.DestinationAttempts; i++)
                if (_mover.TryFindDestination(center, radius, minTravel, 1, out destination) && pasture.Contains(destination))
                    return true;
            destination = default;
            return false;
        }
    }

    // Walks to the chosen spot; gives up when it is told something new, so it chooses again at once.
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
            _orders = _animal.OrdersVersion;
            _mover.MoveTo(_animal.Destination, _animal.Definition.TravelTimeout);
        }

        public override TaskStatus OnUpdate()
        {
            if (_mover.Arrived)
                return TaskStatus.Success;
            return _mover.TimedOut || _animal.OrdersVersion != _orders ? TaskStatus.Failure : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _animal.Calm();
        }
    }

    // Eats where it stands for a while: looks up and turns to the player while they're close. Ends early when it is told
    // something new, or, in a flock, once its leader has moved off.
    [Category("Aipocalypse/Animals")]
    public sealed class GrazeHere : ActionNode
    {
        Animal _animal;
        EnemyMover _mover;
        float _until;
        int _orders;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            Vector2 time = _animal.Definition.GrazeTime;
            _until = Time.time + Random.Range(time.x, time.y);
            _orders = _animal.OrdersVersion;
        }

        public override TaskStatus OnUpdate()
        {
            if (Time.time >= _until || _animal.OrdersVersion != _orders || AnimalFlock.LeftBehind(_animal))
                return TaskStatus.Success;
            AnimalHerd herd = _animal.Herd;
            float notice = herd.Settings.NoticeRadius;
            bool noticed = herd.HasPlayer && (herd.PlayerPosition - transform.position).sqrMagnitude <= notice * notice;
            _animal.Eating = !noticed;
            if (noticed)
                _mover.Face(herd.PlayerPosition);
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _animal.Eating = false;
            _mover.Halt();
        }
    }

    // The flock rules shared by the grazing tasks.
    static class AnimalFlock
    {
        // How far past FlockRadius a follower lets its leader go before it moves after it.
        const float LeaveFactor = 1.6f;

        // The leader a follower grazes round: out on the pasture, not itself. Null for a leader or a kind without a flock.
        public static Animal LeaderOf(Animal animal)
        {
            if (!animal.Definition.FollowsLeader)
                return null;
            Animal leader = animal.Herd.LeaderOf(animal.Definition);
            return leader != null && leader != animal && leader.IsRoaming ? leader : null;
        }

        public static bool LeftBehind(Animal animal)
        {
            if (!animal.Herd.Orders.Released || animal.SentHome)
                return false;
            Animal leader = LeaderOf(animal);
            if (leader == null)
                return false;
            float reach = animal.Definition.FlockRadius * LeaveFactor;
            return (leader.transform.position - animal.transform.position).sqrMagnitude > reach * reach;
        }
    }
}
