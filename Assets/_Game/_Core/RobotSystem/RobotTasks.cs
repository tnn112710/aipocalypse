using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Tasks of the robots' tree (AgentScripts/Robots/Robot.tree.txt). Tuning comes from RobotSettings and the robot's
    // personality; the world (night, a raid, the player) from RobotRoster; requests and what happened to it from its State.

    // Walking helpers shared by the tasks.
    static class RobotWalk
    {
        public static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // Sets off toward `point` at `speed`, giving up after the travel timeout for the distance.
        public static void Go(Robot robot, Vector3 point, float speed)
        {
            RobotSettings settings = robot.Settings;
            robot.Mover.SetSpeed(speed);
            robot.Mover.MoveTo(point, RobotCall.TravelTimeout(Flat(point - robot.transform.position), speed, settings.TravelTimeout));
        }

        // Walks to a reachable point near `target`; false when there's none.
        public static bool TryGo(Robot robot, Vector3 target, float radius, float speed)
        {
            if (!robot.Mover.TryFindDestination(target, radius, 0f, robot.Settings.DestinationAttempts, out Vector3 point))
                return false;
            Go(robot, point, speed);
            return true;
        }

        public static bool Near(Robot robot, Vector3 point, float distance) => Flat(point - robot.transform.position) <= distance;
    }

    // ---- Flinch and knockdown ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsHit : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.State.Hit(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Stands still while the hit clip plays; knocked over, it lies (crossed eyes, set by the hit), then gets up grumpy (the
    // sulk after is its resting mood).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotFlinch : ActionNode
    {
        Robot _robot;
        bool _rising;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Halt();
            _robot.State.ClearStep();
            _rising = false;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            RobotState state = _robot.State;
            if (!_rising && state.Down(now) && !state.Lying(now))
            {
                _rising = true;
                _robot.Hold(RobotExpression.Angry, _robot.Settings.KnockdownRise);
            }
            return state.Hit(now) ? TaskStatus.Running : TaskStatus.Success;
        }
    }

    // ---- Step aside ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsAskedToStep : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.State.StepRequested(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Trots a little way from whoever walked into it: first where its spot allows, else anywhere but a NoGo box (never into
    // the doorway).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotStepAside : ActionNode
    {
        static readonly float[] Turns = { 0f, 60f, -60f, 110f, -110f };

        Robot _robot;
        bool _moving;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            RobotSettings settings = _robot.Settings;
            Vector3 position = _robot.transform.position;
            Vector3 away = position - _robot.State.StepFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -_robot.transform.forward;
            away.Normalize();
            _robot.State.ClearStep();
            _moving = TryStep(position, away, settings, inSpot: true) || TryStep(position, away, settings, inSpot: false);
        }

        bool TryStep(Vector3 position, Vector3 away, RobotSettings settings, bool inSpot)
        {
            foreach (float turn in Turns)
            {
                Vector3 target = position + Quaternion.Euler(0f, turn, 0f) * away * settings.StepAside;
                if (inSpot ? !_robot.Allows(target) : _robot.InNoGo(target))
                    continue;
                if (!_robot.Mover.TryFindDestination(target, 0.6f, settings.StepAside * 0.5f, 2, out Vector3 point) || _robot.InNoGo(point))
                    continue;
                _robot.Mover.SetSpeed(settings.TrotSpeed);
                _robot.Mover.MoveTo(point, settings.StepTimeout);
                return true;
            }
            return false;
        }

        public override TaskStatus OnUpdate() => !_moving || _robot.Mover.Settled ? TaskStatus.Success : TaskStatus.Running;

        public override void OnEnd()
        {
            _robot.Mover.Halt();
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
        }
    }

    // ---- Raid: brave, then hide ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsScared : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Roster.Raid ? TaskStatus.Success : TaskStatus.Failure;
    }

    // A raid: once per raid a moment of brave boxing, then worried, it trots to its shelter and cowers there, looking round
    // now and then, until the raid ends.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotHide : ActionNode
    {
        Robot _robot;
        int _braveFor;
        float _braveUntil;
        bool _walking;
        bool _hidden;
        float _nextLook;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            float now = Time.time;
            RobotSettings settings = _robot.Settings;
            _robot.Mover.Halt();
            _walking = false;
            _hidden = false;
            _braveUntil = float.NegativeInfinity;
            if (_braveFor != _robot.Roster.RaidNumber)
            {
                _braveFor = _robot.Roster.RaidNumber;
                _braveUntil = now + settings.BraveTime;
                _robot.Emote(RobotEmote.Brave);
                _robot.SetMood(RobotExpression.Angry);
                _robot.Beep(RobotSound.Nervous);
            }
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            RobotSettings settings = _robot.Settings;
            if (now < _braveUntil)
                return TaskStatus.Running;
            _robot.SetMood(RobotExpression.Worried);
            if (!_hidden && !_walking)
            {
                if (RobotWalk.Near(_robot, _robot.Shelter, 0.6f) || !RobotWalk.TryGo(_robot, _robot.Shelter, 0.6f, settings.TrotSpeed))
                    Settle(now);
                else
                    _walking = true;
                return TaskStatus.Running;
            }
            if (_walking && _robot.Mover.Settled)
            {
                _walking = false;
                Settle(now);
            }
            if (_hidden && now >= _nextLook)
            {
                _nextLook = now + Random.Range(settings.HideLookInterval.x, settings.HideLookInterval.y);
                _robot.Emote(RobotEmote.LookAround);
            }
            return TaskStatus.Running;
        }

        void Settle(float now)
        {
            _hidden = true;
            _nextLook = now + Random.Range(_robot.Settings.HideLookInterval.x, _robot.Settings.HideLookInterval.y);
            _robot.Mover.Face(_robot.Shelter + _robot.ShelterFacing * 3f);
        }

        public override void OnEnd()
        {
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
            _robot.ClearMood();
        }
    }

    // ---- Answer a call (the whistle, a welcome home) ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsCalled : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Call.Active ? TaskStatus.Success : TaskStatus.Failure;
    }

    // A moment of happy surprise (played when called), a trot to the meeting point (aimed again when the player moves on),
    // then it waits facing the player until the wait runs out or the player walks away, and shrugs before going home.
    // Welcoming the player home, it waves or hops on arrival.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotAnswerCall : ActionNode
    {
        const float ArriveSlack = 0.5f;
        // The player moved this far while it walked: plan the meeting point again.
        const float Replan = 2f;

        Robot _robot;
        Vector3 _heading;
        Vector3 _plannedFor;
        bool _walking;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _walking = false;
            _plannedFor = _robot.Roster.PlayerPosition;
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
                // Reacting first; a call it was already answering (back after a step aside) goes straight on.
                if (now - call.BegunAt < settings.CallReactTime)
                {
                    _robot.Mover.Face(player);
                    return TaskStatus.Running;
                }
                // The player walked on: aim at where they are now (tried once per move, not every think).
                if (RobotWalk.Flat(player - _plannedFor) > Replan)
                {
                    _plannedFor = player;
                    if (_robot.TryPlanMeet(player, out Vector3 again))
                        call.Retarget(again);
                }
                if (!_walking || (call.MeetPoint - _heading).sqrMagnitude > 0.25f)
                {
                    _heading = call.MeetPoint;
                    _walking = true;
                    RobotWalk.Go(_robot, _heading, settings.TrotSpeed);
                    return TaskStatus.Running;
                }
                bool there = RobotWalk.Near(_robot, _heading, ArriveSlack) || RobotWalk.Near(_robot, player, settings.ArriveDistance + 0.3f)
                    || _robot.Mover.Settled;
                if (!there)
                    return TaskStatus.Running;
                _robot.Mover.Halt();
                _robot.Mover.SetSpeed(settings.WalkSpeed);
                call.MarkArrived(now, player);
                if (call.Reason == RobotCallReason.Welcome)
                {
                    _robot.Emote(Random.value < 0.5f ? RobotEmote.Hello : RobotEmote.Jump);
                    _robot.Hold(RobotExpression.Love, 2.5f);
                }
                else
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
            _robot.ClearMood();
        }
    }

    // ---- Nervous ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsNervous : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.State.Nervous(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
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
            _robot.Hold(RobotExpression.Worried, _robot.Settings.NervousTime + 0.3f);
            _robot.Beep(RobotSound.Nervous);
        }

        public override TaskStatus OnUpdate()
        {
            if (!_robot.State.Nervous(Time.time))
                return TaskStatus.Success;
            _robot.Mover.Face(_robot.Roster.PlayerPosition);
            return TaskStatus.Running;
        }
    }

    // ---- Greet and pat ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsGreeting : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.State.Greeting(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Stops, turns to the player and waves (now and then) or talks, happy, while the player keeps looking or the greeting
    // holds; a fresh look or E greets again. A pat: heart eyes and a laugh. Sulking after a knockdown: a grumpy head shake.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotGreet : ActionNode
    {
        Robot _robot;
        int _version;
        int _patVersion;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Halt();
            _version = -1;
            React();
        }

        public override TaskStatus OnUpdate()
        {
            if (!_robot.State.Greeting(Time.time))
                return TaskStatus.Success;
            React();
            _robot.Mover.Face(_robot.Roster.PlayerPosition);
            return TaskStatus.Running;
        }

        public override void OnEnd() => _robot.ClearMood();

        void React()
        {
            RobotState state = _robot.State;
            float now = Time.time;
            if (state.PatVersion != _patVersion)
            {
                _patVersion = state.PatVersion;
                _version = state.GreetingVersion;
                _robot.SetMood(RobotExpression.Happy);
                _robot.Emote(RobotEmote.Laugh);
                _robot.Hold(RobotExpression.Love, _robot.Settings.PatHold);
                _robot.Beep(RobotSound.Chirp);
                return;
            }
            if (state.GreetingVersion == _version)
                return;
            _version = state.GreetingVersion;
            if (state.Sulking(now))
            {
                _robot.SetMood(RobotExpression.Angry);
                _robot.Emote(RobotEmote.No);
                _robot.Beep(RobotSound.Shrug);
                return;
            }
            _robot.SetMood(RobotExpression.Happy);
            if (state.CanWave(now) && Random.value < _robot.Personality.WaveChance)
            {
                state.UseWave(now);
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

    // ---- After a raid: cheer or cry ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsCheering : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.State.Cheering(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
    }

    // A good night: a win pose, then a dance, excited. A bad one: it cries.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotCelebrate : ActionNode
    {
        // The win clip's length, then the dance.
        const float DanceAfter = 2.6f;

        Robot _robot;
        float _danceAt;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Halt();
            _danceAt = float.PositiveInfinity;
            if (_robot.State.Cheer == RobotCheer.Win)
            {
                _danceAt = Time.time + DanceAfter;
                _robot.Emote(RobotEmote.Win);
                _robot.SetMood(RobotExpression.Excited);
                _robot.Beep(RobotSound.Happy);
            }
            else
            {
                _robot.Emote(RobotEmote.Cry);
                _robot.SetMood(RobotExpression.Sad);
                _robot.Beep(RobotSound.Shrug);
            }
        }

        public override TaskStatus OnUpdate()
        {
            if (Time.time >= _danceAt)
            {
                _danceAt = float.PositiveInfinity;
                _robot.Emote(RobotEmote.Dance);
            }
            return _robot.State.Cheering(Time.time) ? TaskStatus.Running : TaskStatus.Success;
        }

        public override void OnEnd() => _robot.ClearMood();
    }

    // ---- Welcome home ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsWelcoming : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.State.WelcomePending ? TaskStatus.Success : TaskStatus.Failure;
    }

    // The player is back: it looks round, spots them with a happy start, then comes over (a call with the welcome reason);
    // with no way to them it waves from where it is.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotWelcome : ActionNode
    {
        Robot _robot;
        float _spotAt;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.State.ClearWelcome();
            _robot.Mover.Halt();
            _spotAt = Time.time + _robot.Settings.WelcomeLookTime;
            _robot.Emote(RobotEmote.LookAround);
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (now < _spotAt || !_robot.Roster.HasPlayer)
                return _robot.Roster.HasPlayer ? TaskStatus.Running : TaskStatus.Success;
            Vector3 player = _robot.Roster.PlayerPosition;
            _robot.Hold(RobotExpression.Surprised, 0.6f);
            _robot.Beep(RobotSound.Greet);
            if (_robot.TryPlanMeet(player, out Vector3 point))
            {
                // Begun as if already reacted: the spotting was its reaction.
                _robot.Call.Begin(point, player, now - _robot.Settings.CallReactTime, RobotCallReason.Welcome);
            }
            else
            {
                _robot.Mover.Face(player);
                _robot.Emote(RobotEmote.Hello);
            }
            return TaskStatus.Success;
        }
    }

    // ---- Night ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsNight : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Asleep ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Bedtime: a yawn, then a slow walk back to its spot.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotGoToSpot : ActionNode
    {
        const float Near = 0.6f;

        Robot _robot;
        bool _walking;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            RobotSettings settings = _robot.Settings;
            _robot.SetMood(RobotExpression.Yawn);
            _walking = !RobotWalk.Near(_robot, _robot.Home, Near)
                && RobotWalk.TryGo(_robot, _robot.Home, 0.5f, settings.WalkSpeed * settings.BedtimePace);
        }

        public override TaskStatus OnUpdate() =>
            _walking && !RobotWalk.Near(_robot, _robot.Home, Near) && !_robot.Mover.Settled ? TaskStatus.Running : TaskStatus.Success;

        public override void OnEnd()
        {
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

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _robot.Mover.Face(_robot.Home + _robot.HomeFacing * 3f);
            _robot.SetStandby(true);
        }

        public override TaskStatus OnUpdate() => TaskStatus.Running;

        public override void OnEnd()
        {
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
    public sealed class RobotHasWork : ConditionalNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override TaskStatus OnUpdate() => _robot.Work != null && _robot.Work.WantsToWork(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Runs its job's work until a stint is done or something above takes over.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotDoWork : ActionNode
    {
        Robot _robot;

        public override void OnAwake() => _robot = GetComponent<Robot>();
        public override void OnStart() => _robot.Work.Begin(Time.time);
        public override TaskStatus OnUpdate() => _robot.Work.Tick(Time.time) ? TaskStatus.Running : TaskStatus.Success;
        public override void OnEnd() => _robot.Work.End(Time.time);
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

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
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
            if (_walking)
                _robot.Mover.Halt();
        }
    }
}
