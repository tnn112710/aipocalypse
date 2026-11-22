using MB13.Aipocalypse.Core.EnemySystem;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Tasks of the service robots' tree (AgentScripts/Robots/Robot.tree.txt). Tuning comes from RobotSettings and the
    // robot's personality; what the world looks like (night, the player) from RobotRoster; requests (greet, whistle, step
    // aside, a hit, a gun pointed at it) from the Robot, set by RobotManager or the player.

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsHit : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Hit ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Stands still while the hit clip plays (Robot.TakeDamage started it).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotFlinch : ActionNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override void OnStart() => _robot.Mover.Halt();
        public override TaskStatus OnUpdate() => _robot.Hit ? TaskStatus.Running : TaskStatus.Success;
    }

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsAskedToStep : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.StepRequested ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Trots a little way from whoever walked into it, preferring places its spot allows (never into the doorway).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotStepAside : ActionNode
    {
        const float Timeout = 2.5f;
        static readonly float[] Turns = { 0f, 60f, -60f, 110f, -110f };

        Robot _robot;
        float _startedAt;
        bool _moving;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _startedAt = Time.time;
            RobotSettings settings = _robot.Settings;
            Vector3 position = _robot.transform.position;
            Vector3 away = position - _robot.StepFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -_robot.transform.forward;
            away.Normalize();
            _robot.ClearStep();
            _moving = false;
            foreach (bool needAllowed in new[] { true, false })
                foreach (float turn in Turns)
                {
                    Vector3 target = position + Quaternion.Euler(0f, turn, 0f) * away * settings.StepAside;
                    if (needAllowed && !_robot.Spot.Allows(target))
                        continue;
                    if (!_robot.Mover.TryFindDestination(target, 0.6f, settings.StepAside * 0.5f, 2, out Vector3 point))
                        continue;
                    _robot.Mover.SetSpeed(settings.TrotSpeed);
                    _robot.Mover.MoveTo(point, Timeout);
                    _moving = true;
                    return;
                }
        }

        public override TaskStatus OnUpdate()
        {
            if (!_moving)
                return TaskStatus.Success;
            bool settled = Time.time - _startedAt > 0.3f && _robot.Mover.Arrived;
            return settled || _robot.Mover.TimedOut ? TaskStatus.Success : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _robot.Mover.Halt();
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
        }
    }

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsCalled : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Call.Active ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Whistled: a moment of happy surprise (Robot.TryWhistle played it), a trot to the meeting point (aimed again when the
    // player moves on), then it waits facing the player until the wait runs out or the player walks away, and shrugs
    // before going home.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotAnswerCall : ActionNode
    {
        const float ReactTime = 0.9f;
        const float ArriveSlack = 0.5f;
        // The player moved this far while it walked: plan the meeting point again.
        const float Replan = 2f;

        Robot _robot;
        float _startedAt;
        Vector3 _heading;
        Vector3 _plannedFor;
        bool _walking;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _startedAt = Time.time;
            _walking = false;
            _robot.Mover.Halt();
            _robot.SetMood(RobotExpression.Happy);
        }

        public override TaskStatus OnUpdate()
        {
            RobotSettings settings = _robot.Settings;
            RobotCall call = _robot.Call;
            float now = Time.time;
            Vector3 player = _robot.Roster.PlayerPosition;
            if (!call.Arrived)
            {
                if (now - _startedAt < ReactTime)
                {
                    _robot.Mover.Face(player);
                    return TaskStatus.Running;
                }
                // The player walked on: aim at where they are now (a new whistle moved the point too).
                if (_walking && Flat(player - _plannedFor) > Replan && _robot.TryPlanMeet(player, out Vector3 again))
                {
                    call.Retarget(again);
                    _plannedFor = player;
                }
                if (!_walking || (call.MeetPoint - _heading).sqrMagnitude > 0.25f)
                {
                    if (!_walking)
                        _plannedFor = player;
                    _heading = call.MeetPoint;
                    _walking = true;
                    _robot.Mover.SetSpeed(settings.TrotSpeed);
                    _robot.Mover.MoveTo(_heading, RobotCall.TravelTimeout(Flat(_heading - _robot.transform.position), settings.TrotSpeed, settings.TravelTimeout));
                    return TaskStatus.Running;
                }
                bool there = Flat(_heading - _robot.transform.position) <= ArriveSlack
                    || Flat(player - _robot.transform.position) <= settings.ArriveDistance + 0.3f
                    || (now - _startedAt > ReactTime + 0.3f && _robot.Mover.Arrived);
                if (!there && !_robot.Mover.TimedOut)
                    return TaskStatus.Running;
                _robot.Mover.Halt();
                _robot.Mover.SetSpeed(settings.WalkSpeed);
                call.MarkArrived(now, player);
                _robot.Hold(RobotExpression.Happy, 2f);
                _robot.Beep(RobotSound.Happy);
            }

            _robot.Mover.Face(player);
            if (!call.ShouldLeave(now, player, settings.CallWait * _robot.Personality.Patience, settings.LeaveDistance))
                return TaskStatus.Running;
            _robot.Emote(RobotEmote.DontKnow);
            _robot.Hold(RobotExpression.Sad, 2.5f);
            _robot.Beep(RobotSound.Shrug);
            call.End();
            return TaskStatus.Success;
        }

        public override void OnEnd()
        {
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
            _robot.SetMood(RobotExpression.Neutral);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsGreeting : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Greeting ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Stops, turns to the player and waves (now and then) or talks, happy, for as long as the player keeps looking or the
    // greeting holds; a fresh look or E greets again.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotGreet : ActionNode
    {
        Robot _robot;
        int _version;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Halt();
            _robot.SetMood(RobotExpression.Happy);
            Greet();
        }

        public override TaskStatus OnUpdate()
        {
            if (!_robot.Greeting)
                return TaskStatus.Success;
            if (_robot.GreetingVersion != _version)
                Greet();
            _robot.Mover.Face(_robot.Roster.PlayerPosition);
            return TaskStatus.Running;
        }

        public override void OnEnd() => _robot.SetMood(RobotExpression.Neutral);

        void Greet()
        {
            _version = _robot.GreetingVersion;
            float now = Time.time;
            if (_robot.CanWave(now) && Random.value < _robot.Personality.WaveChance)
            {
                _robot.UseWave(now);
                _robot.Emote(RobotEmote.Hello);
            }
            else
            {
                _robot.Emote(RobotEmote.Talk);
                _robot.Hold(RobotExpression.Talk, 2.5f);
            }
            _robot.Beep(RobotSound.Greet);
        }
    }

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsNervous : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Nervous ? TaskStatus.Success : TaskStatus.Failure;
    }

    // A gun pointed at it: it faces the player, worried, and shakes its head.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotNervous : ActionNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Halt();
            _robot.Emote(RobotEmote.No);
            _robot.Hold(RobotExpression.Worried, 2.5f);
            _robot.Beep(RobotSound.Nervous);
        }

        public override TaskStatus OnUpdate()
        {
            if (!_robot.Nervous)
                return TaskStatus.Success;
            _robot.Mover.Face(_robot.Roster.PlayerPosition);
            return TaskStatus.Running;
        }
    }

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsNight : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Asleep ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Walks back to its spot and turns to the spot's facing.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotGoToSpot : ActionNode
    {
        const float Near = 0.6f;

        Robot _robot;
        bool _walking;
        float _startedAt;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _startedAt = Time.time;
            _walking = false;
            if (IsNear() || !_robot.Mover.TryFindDestination(_robot.Spot.Center, 0.5f, 0f, _robot.Settings.DestinationAttempts, out Vector3 point))
                return;
            _walking = true;
            _robot.Mover.MoveTo(point, RobotCall.TravelTimeout(Vector3.Distance(point, _robot.transform.position), _robot.Settings.WalkSpeed, _robot.Settings.TravelTimeout));
        }

        public override TaskStatus OnUpdate()
        {
            if (_walking && !IsNear() && !_robot.Mover.TimedOut && !(Time.time - _startedAt > 0.3f && _robot.Mover.Arrived))
                return TaskStatus.Running;
            _robot.Mover.Face(_robot.Spot.Center + _robot.Spot.Facing * 3f);
            return TaskStatus.Success;
        }

        bool IsNear()
        {
            Vector3 offset = _robot.Spot.Center - _robot.transform.position;
            offset.y = 0f;
            return offset.magnitude <= Near;
        }
    }

    // Night: idles slowly with sleepy eyes until morning (or something above wakes it).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotStandby : ActionNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Halt();
            _robot.SetStandby(true);
        }

        public override TaskStatus OnUpdate() => TaskStatus.Running;

        public override void OnEnd()
        {
            _robot.SetStandby(false);
            _robot.Beep(RobotSound.Wake);
        }
    }

    // Stands about: small turns to look round, blinks and passing looks on its face, and now and then a little dance or
    // laugh at the end.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotIdle : ActionNode
    {
        const float FlourishTime = 3f;

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
            _nextLook = now + Random.Range(1.5f, 3.5f);
            _nextMood = now + Random.Range(settings.MoodInterval.x, settings.MoodInterval.y);
            _flourishing = false;
            _robot.Mover.Halt();
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            RobotSettings settings = _robot.Settings;
            if (now >= _nextLook)
            {
                _nextLook = now + Random.Range(2f, 4.5f);
                float yaw = Random.Range(-70f, 70f);
                _robot.Mover.Face(_robot.transform.position + Quaternion.Euler(0f, yaw, 0f) * _robot.Spot.Facing * 3f);
            }
            if (now >= _nextMood)
            {
                _nextMood = now + Random.Range(settings.MoodInterval.x, settings.MoodInterval.y);
                _robot.Hold(Random.value < 0.7f ? RobotExpression.Happy : RobotExpression.Surprised, Random.Range(1f, 2.5f));
            }
            if (now < _until)
                return TaskStatus.Running;
            if (_flourishing)
                return TaskStatus.Success;
            if (Random.value >= _robot.Personality.FlourishChance)
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
        bool _home;
        float _startedAt;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _startedAt = Time.time;
            _walking = false;
            RobotSettings settings = _robot.Settings;
            RobotSpot spot = _robot.Spot;
            Vector3 offset = spot.Center - _robot.transform.position;
            offset.y = 0f;
            _home = offset.magnitude > AwayFromCenter;
            if (!_home && Random.value >= _robot.Personality.StrollChance)
                return;
            for (int i = 0; i < settings.DestinationAttempts; i++)
            {
                Vector3 center = _home ? spot.Center : spot.Center + Random.insideUnitSphere * spot.Radius;
                if (!_robot.Mover.TryFindDestination(center, _home ? 0.5f : 1f, _home ? 0f : 1.5f, 1, out Vector3 point) || !spot.Allows(point))
                    continue;
                _robot.Mover.SetSpeed(settings.WalkSpeed);
                _robot.Mover.MoveTo(point, RobotCall.TravelTimeout(Vector3.Distance(point, _robot.transform.position), settings.WalkSpeed, settings.TravelTimeout));
                _walking = true;
                return;
            }
        }

        public override TaskStatus OnUpdate()
        {
            if (!_walking)
                return TaskStatus.Success;
            if (!_robot.Mover.TimedOut && !(Time.time - _startedAt > 0.3f && _robot.Mover.Arrived))
                return TaskStatus.Running;
            if (_home)
                _robot.Mover.Face(_robot.Spot.Center + _robot.Spot.Facing * 3f);
            return TaskStatus.Success;
        }

        public override void OnEnd()
        {
            if (_walking && !_home)
                _robot.Mover.Halt();
        }
    }
}
