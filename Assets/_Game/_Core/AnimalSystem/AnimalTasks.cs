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

    // Base of the animals' yes/no conditions: Success while Check() holds. Abstract, so Behavior Designer and TreeKit
    // never list it as a task (trees store the concrete task's type name).
    public abstract class AnimalCondition : ConditionalNode
    {
        protected Animal _animal;

        public override void OnAwake() => _animal = GetComponent<Animal>();
        public override TaskStatus OnUpdate() => Check() ? TaskStatus.Success : TaskStatus.Failure;

        protected abstract bool Check();
    }

    // On its way into the pen: the bell rang, or the player sent it home. Also the abort check of the walk home.
    [Category("Aipocalypse/Animals")]
    public sealed class CalledHome : AnimalCondition
    {
        protected override bool Check() => _animal.GoingHome;
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
        bool _started;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _started = true;
            Plan();
        }

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
            if (!_started)
                return;
            _started = false;
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
    public sealed class IsStartled : AnimalCondition
    {
        protected override bool Check() => _animal.Startled;
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
        bool _started;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _started = true;
            Plan();
        }

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
            if (!_started)
                return;
            _started = false;
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
    public sealed class IsNight : AnimalCondition
    {
        protected override bool Check() => _animal.Herd.Asleep;
    }

    // Lies down where it stands until something aborts it (morning, the bell, a scare).
    [Category("Aipocalypse/Animals")]
    public sealed class SleepHere : ActionNode
    {
        Animal _animal;
        EnemyMover _mover;
        bool _started;

        public override void OnAwake()
        {
            _animal = GetComponent<Animal>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _started = true;
            _mover.Halt();
            _animal.Sleeping = true;
        }

        public override TaskStatus OnUpdate() => TaskStatus.Running;
        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _animal.Sleeping = false;
        }
    }
}
