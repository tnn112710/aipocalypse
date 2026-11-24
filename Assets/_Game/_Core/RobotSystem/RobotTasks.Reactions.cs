using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // The robots' tree (RobotTasks.cs): reactions to hits, bumps, raids and a pointed gun.

    // ---- Flinch and knockdown ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsHit : RobotCondition
    {
        protected override bool Check() => _robot.State.Hit(Time.time);
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
    public sealed class RobotIsAskedToStep : RobotCondition
    {
        protected override bool Check() => _robot.State.StepRequested(Time.time);
    }

    // Trots a little way from whoever walked into it: first where its spot allows, else anywhere but a NoGo box (never into
    // the doorway).
    [Category("Aipocalypse/Robots")]
    public sealed class RobotStepAside : ActionNode
    {
        static readonly float[] Turns = { 0f, 60f, -60f, 110f, -110f };

        Robot _robot;
        bool _moving;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
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
            if (!_started)
                return;
            _started = false;
            _robot.Mover.Halt();
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
        }
    }

    // ---- Raid: brave, then hide ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsScared : RobotCondition
    {
        protected override bool Check() => _robot.Roster.Raid;
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
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
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
            if (!_started)
                return;
            _started = false;
            _robot.Mover.SetSpeed(_robot.Settings.WalkSpeed);
            _robot.ClearMood();
        }
    }

    // ---- Nervous ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsNervous : RobotCondition
    {
        protected override bool Check() => _robot.State.Nervous(Time.time);
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

    // ---- After a raid: cheer or cry ----

    [Category("Aipocalypse/Robots")]
    public sealed class RobotIsCheering : RobotCondition
    {
        protected override bool Check() => _robot.State.Cheering(Time.time);
    }

    // A good night: a win pose, then a dance, excited. A bad one: it cries.
    [Category("Aipocalypse/Robots")]
    public sealed class RobotCelebrate : ActionNode
    {
        // The win clip's length, then the dance.
        const float DanceAfter = 2.6f;

        Robot _robot;
        float _danceAt;
        bool _started;

        public override void OnAwake() => _robot = GetComponent<Robot>();

        public override void OnStart()
        {
            _started = true;
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

        public override void OnEnd()
        {
            if (!_started)
                return;
            _started = false;
            _robot.ClearMood();
        }
    }
}
