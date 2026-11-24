using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // The robots' tree (RobotTasks.cs): answering calls, greeting, seeing off, welcoming and chatting.

    // ---- Answer a call (the whistle, a welcome home) ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsCalled : RobotCondition
    {
        protected override bool Check() => _robot.Call.Active;
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
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
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
                if (FlatMath.Length(player - _plannedFor) > Replan)
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
            if (!_started)
                return;
            _started = false;
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
            _robot.ClearMood();
        }
    }

    // ---- Greet and pat ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsGreeting : RobotCondition
    {
        protected override bool Check() => _robot.State.Greeting(Time.time);
    }

    // Stops, turns to the player and waves (now and then) or talks, happy, while the player keeps looking or the greeting
    // holds; a fresh look or E greets again. A pat: heart eyes and a laugh. Sulking after a knockdown: a grumpy head shake.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotGreet : ActionNode
    {
        Robot _robot;
        int _version;
        int _patVersion;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
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

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot.ClearMood();
        }

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

    // ---- See the player off ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsSeeingOff : RobotCondition
    {
        protected override bool Check() => _robot.State.SeeingOff(Time.time);
    }

    // The player walked out of the yard: it stops, turns to them and waves goodbye, happy for a moment, then a little sad.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotSeeOff : ActionNode
    {
        Robot _robot;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
            _robot.Mover.Halt();
            _robot.Emote(RobotEmote.Hello);
            _robot.SetMood(RobotExpression.Sad);
            _robot.Hold(RobotExpression.Happy, 1.5f);
            _robot.Beep(RobotSound.Greet);
        }

        public override TaskStatus OnUpdate()
        {
            if (!_robot.State.SeeingOff(Time.time))
                return TaskStatus.Success;
            _robot.Mover.Face(_robot.Roster.PlayerPosition);
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot.ClearMood();
        }
    }

    // ---- Chat with another robot ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsChatting : RobotCondition
    {
        protected override bool Check() => _robot.State.Chatting(Time.time);
    }

    // Another robot close by: it stops and faces them. The one that starts waves, then talks; the other talks first, then
    // laughs.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotChat : ActionNode
    {
        Robot _robot;
        bool _answered;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
            _robot.Mover.Halt();
            _robot.Mover.Face(_robot.State.ChatWith);
            _robot.SetMood(RobotExpression.Happy);
            _answered = false;
            if (_robot.State.ChatLeads)
                _robot.Emote(RobotEmote.Hello);
            else
                Talk();
            _robot.Beep(RobotSound.Chirp);
        }

        public override TaskStatus OnUpdate()
        {
            RobotState state = _robot.State;
            float now = Time.time;
            if (!state.Chatting(now))
                return TaskStatus.Success;
            if (!_answered && now >= state.ChatStartedAt + _robot.Settings.ChatTime * 0.5f)
            {
                _answered = true;
                if (state.ChatLeads)
                    Talk();
                else
                {
                    _robot.Emote(RobotEmote.Laugh);
                    _robot.Hold(RobotExpression.Happy, 2f);
                }
            }
            return TaskStatus.Running;
        }

        void Talk()
        {
            _robot.Emote(RobotEmote.Talk);
            _robot.Hold(RobotExpression.Talk, _robot.Settings.ChatTime * 0.5f);
        }

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot.ClearMood();
        }
    }

    // ---- Welcome home ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsWelcoming : RobotCondition
    {
        protected override bool Check() => _robot.State.WelcomePending;
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
}
