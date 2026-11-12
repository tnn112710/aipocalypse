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
            // Anywhere in the pen, so a herd coming home spreads out instead of packing the middle.
            _towardsPen = TryInside(pen, out Vector3 destination) || _mover.TryFindDestination(pen.Center, pen.Radius, 0f, definition.DestinationAttempts, out destination);
            if (_towardsPen || _mover.TryFindDestination(pen.WaitPoint, _animal.Herd.Settings.WaitRadius, 0f, definition.DestinationAttempts, out destination))
                _mover.MoveTo(destination, definition.TravelTimeout);
            else
                _mover.Halt();
        }

        bool TryInside(AnimalPen pen, out Vector3 destination)
        {
            for (int i = 0; i < _animal.Definition.DestinationAttempts; i++)
                if (_mover.TryFindDestination(pen.RandomPoint(1f), 0.5f, 0f, 1, out destination) && pen.Contains(destination))
                    return true;
            destination = default;
            return false;
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

    // Runs (a gunshot) or steps briskly, at its hurry pace (the player), StartleDistance away from what startled it, then settles. Inside the pen it
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
            float speed = _animal.StartleRuns ? _animal.FleeSpeed : _animal.HurrySpeed;
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
            _animal.ResumePace();
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
                    if (!_mover.TryFindDestination(pen.RandomPoint(1f), 0.5f, 0f, 1, out Vector3 spot) || !pen.Contains(spot))
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

    // Picks where it eats next. Mostly a short step on from where it stands (DriftChance; a chicken may dart it), kept to
    // where it belongs (a follower left behind by its flock trots after it instead); otherwise, released and not sent home, a spot on the pasture near itself (a flock follower: near where
    // its leader is heading; at dusk: on the pasture's home side); otherwise inside the pen, or round where it stands when it
    // was shut out.
    [Category("Aipocalypse/Animals")]
    public sealed class ChooseGrazeSpot : ActionNode
    {
        // A short step goes this far either side of straight ahead (degrees) for half its tries, then any way.
        const float DriftCone = 60f;
        const int DriftAttempts = 6;
        // At dusk a released animal grazes within this share of the pasture's radius of its home side.
        const float DuskShare = 0.35f;

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
            bool released = herd.Orders.Released && !_animal.SentHome && herd.Pasture != null;
            _animal.Darting = false;
            Vector3 destination;
            if (Random.value < definition.DriftChance && TryDrift(herd, definition, released, out destination))
            {
                _animal.Destination = destination;
                _animal.Darting = Random.value < definition.DartChance;
                return TaskStatus.Success;
            }
            if (released && TryOnPasture(herd, definition, out destination))
            {
                _animal.Destination = destination;
                // Left behind by its flock: it trots to catch up.
                _animal.Darting = AnimalFlock.LeftBehind(_animal);
                return TaskStatus.Success;
            }
            bool found = _animal.IsHome
                ? TryInPen(herd, definition, out destination)
                : _mover.TryFindDestination(transform.position, definition.GrazeRadius * 0.5f, definition.MinTravel, definition.DestinationAttempts, out destination);
            if (!found)
                return TaskStatus.Failure;
            _animal.Destination = destination;
            return TaskStatus.Success;
        }

        // In the pen: somewhere near itself first (a walk across the whole pen at grazing pace takes long), else anywhere in it.
        bool TryInPen(AnimalHerd herd, AnimalDefinition definition, out Vector3 destination)
        {
            for (int i = 0; i < definition.DestinationAttempts; i++)
                if (_mover.TryFindDestination(transform.position, definition.GrazeRadius * 0.5f, definition.MinTravel, 1, out destination) && herd.Pen.Contains(destination))
                    return true;
            for (int i = 0; i < definition.DestinationAttempts; i++)
                if (_mover.TryFindDestination(herd.Pen.RandomPoint(1f), 0.5f, definition.MinTravel, 1, out destination) && herd.Pen.Contains(destination))
                    return true;
            destination = default;
            return false;
        }

        // A step on, head down, staying where it belongs: in the pen while home and not released; on the pasture (at dusk, on
        // its home side; a follower, near its leader) while released. Anywhere else, no step: it walks to where it belongs.
        bool TryDrift(AnimalHerd herd, AnimalDefinition definition, bool released, out Vector3 destination)
        {
            destination = default;
            bool roaming = _animal.IsRoaming;
            if (released ? !roaming : !_animal.IsHome)
                return false;
            // Ahead first; then, against a fence or the pasture's edge, any way round.
            for (int i = 0; i < DriftAttempts; i++)
            {
                float cone = i < DriftAttempts / 2 ? DriftCone : 180f;
                Vector3 forward = Quaternion.Euler(0f, Random.Range(-cone, cone), 0f) * transform.forward;
                forward.y = 0f;
                Vector3 target = transform.position + forward.normalized * Random.Range(definition.DriftDistance.x, definition.DriftDistance.y);
                if (_mover.TryFindDestination(target, 0.4f, definition.DriftDistance.x * 0.5f, 1, out destination) && Belongs(herd, released, destination))
                    return true;
            }
            return false;
        }

        bool Belongs(AnimalHerd herd, bool released, Vector3 point)
        {
            if (!released)
                return herd.Pen.Contains(point);
            if (!herd.Pasture.Contains(point))
                return false;
            if (herd.Dusk && AnimalFlock.LeaderOf(_animal) == null)
            {
                float share = herd.Pasture.Radius * DuskShare;
                if ((point - HomeSide(herd)).sqrMagnitude > share * share)
                    return false;
            }
            return !AnimalFlock.Strays(_animal, point);
        }

        // The point of the pasture nearest the pen.
        static Vector3 HomeSide(AnimalHerd herd)
        {
            AnimalPasture pasture = herd.Pasture;
            Vector3 home = herd.Pen.Center - pasture.Center;
            home.y = 0f;
            return pasture.Center + home.normalized * (pasture.Radius * (1f - DuskShare));
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
            else if (herd.Dusk)
            {
                center = HomeSide(herd);
                radius = pasture.Radius * DuskShare;
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
            if (_animal.Darting)
                _mover.SetSpeed(_animal.HurrySpeed);
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
            if (_animal.Darting)
            {
                _animal.Darting = false;
                _animal.ResumePace();
            }
        }
    }

    // Eats where it stands for a while, or now and then (IdleChance) stands about with one of its spare idle clips and looks
    // round once; looks up and turns to the player while they're close, and keeps watching them up to WatchAtMost. Ends early
    // when it is told something new, or, in a flock, once its leader has moved off. Times are its kind's, times its Patience.
    [Category("Aipocalypse/Animals")]
    public sealed class GrazeHere : ActionNode
    {
        // How far it looks round when idle (degrees either side).
        const float LookRound = 70f;
        // How long it may keep watching a player who stays close before it carries on (s).
        const float WatchAtMost = 10f;

        Animal _animal;
        EnemyMover _mover;
        float _until;
        float _lookAt;
        float _watchUntil;
        int _orders;
        bool _idle;
        int _fidget;
        bool _faced;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            AnimalDefinition definition = _animal.Definition;
            _idle = Random.value < definition.IdleChance;
            Vector2 time = _idle ? definition.IdleTime : definition.GrazeTime;
            float length = Random.Range(time.x, time.y) * _animal.Patience;
            _until = Time.time + length;
            _lookAt = _idle ? Time.time + length * Random.Range(0.3f, 0.6f) : float.PositiveInfinity;
            _fidget = _idle && definition.Fidgets > 0 ? Random.Range(0, definition.Fidgets + 1) : 0;
            _orders = _animal.OrdersVersion;
            _faced = false;
            _watchUntil = float.PositiveInfinity;
        }

        public override TaskStatus OnUpdate()
        {
            if (Time.time >= _until || _animal.OrdersVersion != _orders || AnimalFlock.LeftBehind(_animal))
                return TaskStatus.Success;
            AnimalHerd herd = _animal.Herd;
            float notice = herd.Settings.NoticeRadius * _animal.Definition.ScareScale;
            bool noticed = herd.HasPlayer && (herd.PlayerPosition - transform.position).sqrMagnitude <= notice * notice;
            _animal.Eating = !noticed && !_idle;
            _animal.Fidget = !noticed && _idle ? _fidget : 0;
            if (noticed)
            {
                // It keeps watching while the player stays close, for a while.
                if (!_faced)
                    _watchUntil = Time.time + WatchAtMost;
                _until = Mathf.Max(_until, Mathf.Min(Time.time + 1f, _watchUntil));
                _mover.Face(herd.PlayerPosition);
                _faced = true;
            }
            else if (_faced)
            {
                _mover.Halt();
                _faced = false;
            }
            else if (Time.time >= _lookAt)
            {
                _lookAt = float.PositiveInfinity;
                Vector3 look = Quaternion.Euler(0f, Random.Range(-LookRound, LookRound), 0f) * transform.forward;
                _mover.Face(transform.position + look * 3f);
            }
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _animal.Eating = false;
            _animal.Fidget = 0;
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

        // A follower's step that would take it past FlockRadius of its leader.
        public static bool Strays(Animal animal, Vector3 to)
        {
            Animal leader = LeaderOf(animal);
            if (leader == null)
                return false;
            float reach = animal.Definition.FlockRadius;
            return (leader.transform.position - to).sqrMagnitude > reach * reach;
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
