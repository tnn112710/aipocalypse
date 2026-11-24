using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // The robots' tree (RobotTasks.cs): night, work and home life.

    // ---- Night ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsNight : RobotCondition
    {
        protected override bool Check() => _robot.Asleep;
    }

    // Bedtime: a yawn, then a slow walk back to its spot.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotGoToSpot : ActionNode
    {
        const float Near = 0.6f;

        Robot _robot;
        bool _walking;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
            RobotSettings settings = _robot.Settings;
            _robot.SetMood(RobotExpression.Yawn);
            _walking = !RobotWalk.Near(_robot, _robot.Home, Near)
                && RobotWalk.TryGo(_robot, _robot.Home, 0.5f, settings.WalkSpeed * settings.BedtimePace);
        }

        public override TaskStatus OnUpdate() =>
            _walking && !RobotWalk.Near(_robot, _robot.Home, Near) && !_robot.Mover.Settled ? TaskStatus.Running : TaskStatus.Success;

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
            _robot.ClearMood();
        }
    }

    // Night: faces out from its spot and idles slowly with sleepy eyes until morning (a hop and a happy face) or until the
    // player wakes it (a start).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotStandby : ActionNode
    {
        Robot _robot;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
            _robot.Mover.Face(_robot.Home + _robot.HomeFacing * 3f);
            _robot.SetStandby(true);
        }

        public override TaskStatus OnUpdate() => TaskStatus.Running;

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot.SetStandby(false);
            if (_robot.Roster.Night)
                _robot.Hold(RobotExpression.Surprised, 1f);
            else
            {
                _robot.Emote(RobotEmote.Jump);
                _robot.Hold(RobotExpression.Happy, 3f);
            }
            _robot.Beep(RobotSound.Wake);
        }
    }

    // ---- Work (a RobotJob's) ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotHasWork : RobotCondition
    {
        protected override bool Check() => _robot.Work != null && _robot.Work.WantsToWork(Time.time);
    }

    // Runs its job's work until a stint is done or something above takes over.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotDoWork : ActionNode
    {
        Robot _robot;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
            _robot.Work.Begin(Time.time);
        }

        public override TaskStatus OnUpdate() => _robot.Work.Tick(Time.time) ? TaskStatus.Running : TaskStatus.Success;

        // Stopping the tree (a scene unload) ends every task, even on a robot with no job.
        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot?.Work?.End(Time.time);
        }
    }

    // ---- Home life ----

    // Stands about: back at its spot it turns to the spot's facing; small turns to look round, passing looks on its face,
    // and now and then a little dance or laugh at the end.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotIdle : ActionNode
    {
        const float FlourishTime = 3f;
        const float AtSpot = 1f;

        Robot _robot;
        float _until;
        float _nextLook;
        float _nextMood;
        bool _flourishing;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            RobotSettings settings = _robot.Settings;
            float now = Time.time;
            _until = now + Random.Range(settings.IdleTime.x, settings.IdleTime.y) * _robot.Personality.Patience;
            _nextLook = now + Random.Range(settings.LookAroundInterval.x, settings.LookAroundInterval.y);
            _nextMood = now + Random.Range(settings.MoodInterval.x, settings.MoodInterval.y);
            _flourishing = false;
            if (RobotWalk.Near(_robot, _robot.Home, AtSpot))
                _robot.Mover.Face(_robot.Home + _robot.HomeFacing * 3f);
            else
                _robot.Mover.Halt();
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            RobotSettings settings = _robot.Settings;
            if (now >= _nextLook)
            {
                _nextLook = now + Random.Range(settings.LookAroundInterval.x, settings.LookAroundInterval.y);
                float yaw = Random.Range(-settings.LookAroundYaw, settings.LookAroundYaw);
                _robot.Mover.Face(_robot.transform.position + Quaternion.Euler(0f, yaw, 0f) * _robot.HomeFacing * 3f);
            }
            if (now >= _nextMood)
            {
                _nextMood = now + Random.Range(settings.MoodInterval.x, settings.MoodInterval.y);
                _robot.Hold(Random.value < 0.7f ? RobotExpression.Happy : RobotExpression.Surprised, Random.Range(1f, 2.5f));
            }
            if (now < _until)
                return TaskStatus.Running;
            if (_flourishing || Random.value >= _robot.Personality.FlourishChance)
                return TaskStatus.Success;
            _flourishing = true;
            _until = now + FlourishTime;
            _robot.Emote(Random.value < 0.6f ? RobotEmote.Dance : RobotEmote.Laugh);
            _robot.Hold(RobotExpression.Happy, FlourishTime);
            _robot.Beep(RobotSound.Chirp);
            return TaskStatus.Running;
        }
    }

    // After idling: back to its spot when away from it, else (by its stroll chance) a short walk within the spot's radius.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotStroll : ActionNode
    {
        const float AwayFromCenter = 1f;

        Robot _robot;
        bool _walking;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
            _walking = false;
            RobotSettings settings = _robot.Settings;
            bool home = !RobotWalk.Near(_robot, _robot.Home, AwayFromCenter);
            if (home)
            {
                _walking = RobotWalk.TryGo(_robot, _robot.Home, 0.5f, settings.WalkSpeed);
                return;
            }
            if (_robot.HomeRadius <= 0f || Random.value >= _robot.Personality.StrollChance)
                return;
            for (int i = 0; i < settings.DestinationAttempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _robot.HomeRadius;
                Vector3 center = _robot.Home + new Vector3(offset.x, 0f, offset.y);
                if (!_robot.Mover.TryFindDestination(center, 1f, 1.5f, 1, out Vector3 point) || !_robot.Allows(point))
                    continue;
                RobotWalk.Go(_robot, point, settings.WalkSpeed);
                _walking = true;
                return;
            }
        }

        public override TaskStatus OnUpdate() => _walking && !_robot.Mover.Settled ? TaskStatus.Running : TaskStatus.Success;

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            if (_walking)
                _robot.Mover.Halt();
        }
    }
}
