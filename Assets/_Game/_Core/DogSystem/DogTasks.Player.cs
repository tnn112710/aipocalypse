using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's tree (DogTasks.cs) with the player: a badly hurt player, welcome home, bedtime, the whistle's call,
    // petting, a sad stay.

    // ---- The player badly hurt: it stays close and whines ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogPlayerHurt : DogCondition
    {
        protected override bool Check() => _dog.Manager.PlayerHurt && _dog.State.Order == DogOrder.Follow && _dog.Roster.HasPlayer;
    }

    [Category("Aipocalypse/Dogs")]
    public sealed class DogStayCloseHurtPlayer : ActionNode
    {
        Dog _dog;
        float _nextGo, _nextWhine;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            Vector3 player = _dog.Roster.PlayerPosition;
            if (now >= _nextWhine)
            {
                _nextWhine = now + Random.Range(8f, 14f);
                _dog.Manager.TryPlay(_dog, DogSound.Whine);
            }
            if (now >= _nextGo)
            {
                _nextGo = now + 0.5f;
                if (!DogWalk.Near(_dog, player, 2f))
                {
                    Vector3 side = _dog.transform.position - player;
                    side.y = 0f;
                    DogWalk.Go(_dog, player + (side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right) * 1.3f, DogGait.Trot);
                }
                else
                {
                    _dog.Legs.Halt();
                    _dog.Legs.Face(player);
                }
            }
            return TaskStatus.Running;
        }
    }

    // ---- Welcome home: staying at home, the player back after a while ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsWelcoming : DogCondition
    {
        protected override bool Check() => _dog.State.Welcoming(Time.time) && _dog.State.Order == DogOrder.Stay && _dog.Roster.HasPlayer;
    }

    // Runs to the player, happy (a bark within the budget), circles them once, then sits facing them a moment.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogWelcome : ActionNode
    {
        enum Phase { Run, Circle, Greet, Sit }

        const float Reach = 3f;
        // A greeting act (a bounce, a roll, a bark) has this long to start before the sit.
        const float GreetStart = 0.5f;
        const float CircleRadius = 2.4f;
        // The lap's target runs this far ahead of the dog (radians); a slow lap (the happy act, a detour) ends here so
        // the sit still fits in the welcome.
        const float LapLead = 0.8f;
        const float LapGiveUp = 8f;
        const float SitTime = 2.5f;

        Dog _dog;
        Phase _phase;
        DogWelcomeStyle _style;
        float _phaseStart;
        float _greetAt;
        float _angle;
        float _turned;
        float _nextGo;
        bool _started;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _dog.SetSniffing(false);
            if (_dog.InHouse)
                _dog.ResetBody();
            _dog.SetPose(DogPose.Stand);
            _phase = Phase.Run;
            _phaseStart = Time.time;
            _nextGo = 0f;
            _greetAt = float.NaN;
            if (!_dog.Settings.Welcomes.TryPick(_dog.Manager.Roll(), out _style))
                _style = DogWelcomeStyle.Circle;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            Vector3 player = _dog.Roster.PlayerPosition;
            if (!_dog.State.Welcoming(now))
                return TaskStatus.Success;
            switch (_phase)
            {
                case Phase.Run:
                    if (DogWalk.Near(_dog, player, Reach))
                    {
                        _phaseStart = now;
                        Vector3 away = _dog.transform.position - player;
                        _angle = Mathf.Atan2(away.x, away.z);
                        _turned = 0f;
                        if (_style == DogWelcomeStyle.Circle)
                        {
                            _phase = Phase.Circle;
                            if (_dog.Manager.TryPlay(_dog, DogSound.Bark) && _dog.Standing)
                                _dog.Act(DogAct.Happy);
                            return TaskStatus.Running;
                        }
                        _phase = Phase.Greet;
                        DogWalk.Settle(_dog, DogPose.Stand, player);
                        return TaskStatus.Running;
                    }
                    else if (now >= _nextGo)
                    {
                        // To just in front of the player, not into them.
                        _nextGo = now + 0.3f;
                        Vector3 toDog = _dog.transform.position - player;
                        toDog.y = 0f;
                        DogWalk.Go(_dog, player + (toDog.sqrMagnitude > 0.01f ? toDog.normalized : Vector3.forward) * 2f, DogGait.Canter);
                    }
                    return TaskStatus.Running;
                case Phase.Circle:
                {
                    // Steered from where it really is round the player (never by the clock: during the happy act it
                    // can't walk, and a clock-driven target would end up across the circle).
                    if (now < _nextGo || !_dog.Standing)
                        return TaskStatus.Running;
                    _nextGo = now + 0.25f;
                    Vector3 away = _dog.transform.position - player;
                    float angle = Mathf.Atan2(away.x, away.z);
                    _turned += Mathf.Max(0f, Mathf.DeltaAngle(_angle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                    _angle = angle;
                    Vector3 next = player + new Vector3(Mathf.Sin(angle + LapLead), 0f, Mathf.Cos(angle + LapLead)) * CircleRadius;
                    // A lap, or the circle runs into something (a wall, its house): it sits there instead of cutting past
                    // the player.
                    if (_turned >= Mathf.PI * 2f || !OnGround(next) || now - _phaseStart > LapGiveUp)
                    {
                        _phase = Phase.Sit;
                        _phaseStart = now;
                        DogWalk.Settle(_dog, DogPose.Sit, player);
                        return TaskStatus.Running;
                    }
                    DogWalk.Go(_dog, next, DogGait.Trot);
                    return TaskStatus.Running;
                }
                case Phase.Greet:
                    // Facing them (an arc first, when it ran up from the side), the act, then the sit once it's over.
                    if (_dog.Legs.Arcing)
                        return TaskStatus.Running;
                    if (float.IsNaN(_greetAt))
                    {
                        _greetAt = now + GreetStart;
                        _dog.Legs.Halt();
                        if (_style == DogWelcomeStyle.Bark)
                            _dog.Manager.TryPlay(_dog, DogSound.Bark);
                        _dog.Act(_style == DogWelcomeStyle.Roll ? DogAct.Roll : _style == DogWelcomeStyle.Bark ? DogAct.Bark : DogAct.Happy);
                        return TaskStatus.Running;
                    }
                    if (now < _greetAt || !_dog.Standing)
                        return TaskStatus.Running;
                    _phase = Phase.Sit;
                    _phaseStart = now;
                    DogWalk.Settle(_dog, DogPose.Sit, player);
                    return TaskStatus.Running;
                default:
                    if (now - _phaseStart < SitTime)
                        return TaskStatus.Running;
                    _dog.State.WelcomeDone();
                    return TaskStatus.Success;
            }
        }

        static bool OnGround(Vector3 point)
        {
            AstarPath navigation = AstarPath.active;
            if (navigation == null)
                return true;
            Pathfinding.NNInfo nearest = navigation.GetNearest(point, Pathfinding.NearestNodeConstraint.Walkable);
            return nearest.node != null && FlatMath.Length(nearest.position - point) < 0.4f;
        }

        // Cut off (a whistle, a raid), the welcome is over too. Only once per run: Behavior Designer calls OnEnd again on a
        // finished task when a later abort interrupts the tree, which would cancel the next welcome before it starts.
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.State.WelcomeDone();
        }
    }

    // ---- Bedtime: following, the player lies down in bed ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsBedtime : DogCondition
    {
        protected override bool Check() => _dog.State.Order == DogOrder.Follow && _dog.Manager.TryGetBedSpot(out _, out _, out _);
    }

    // Trots to the floor beside the bed and sits while the player picks a time; when they lie down it lies down facing the
    // bed and falls asleep (the time skip then sends it home).
    [Category("Aipocalypse/Dogs")]
    public sealed class DogLieByBed : ActionNode
    {
        Dog _dog;
        float _nextGo;
        bool _arrived;
        float _layAt;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _dog.SetSniffing(false);
            _nextGo = 0f;
            _arrived = false;
            _layAt = float.NaN;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (!_dog.Manager.TryGetBedSpot(out Vector3 spot, out Vector3 bed, out bool lyingDown))
                return TaskStatus.Success;
            if (_arrived)
            {
                if (!lyingDown)
                    return TaskStatus.Running;
                if (float.IsNaN(_layAt))
                {
                    _layAt = now;
                    DogWalk.Settle(_dog, DogPose.Lie, bed);
                }
                else if (now - _layAt > 2f && _dog.Pose == DogPose.Lie)
                    _dog.SetPose(DogPose.Sleep);
                return TaskStatus.Running;
            }
            if (DogWalk.Near(_dog, spot, 0.6f))
            {
                _arrived = true;
                DogWalk.Settle(_dog, DogPose.Sit, bed);
                return TaskStatus.Running;
            }
            if (now >= _nextGo)
            {
                _nextGo = now + 0.4f;
                if (_dog.Pose != DogPose.Stand)
                    _dog.SetPose(DogPose.Stand);
                DogWalk.Go(_dog, spot, DogWalk.Near(_dog, spot, 4f) ? DogGait.Trot : DogGait.Canter);
            }
            return TaskStatus.Running;
        }
    }

    // ---- Called by the whistle: come to heel ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsCalled : DogCondition
    {
        protected override bool Check() => _dog.State.Called(Time.time) && _dog.Roster.HasPlayer;
    }

    // Runs (walks, when annoyed, after a head shake) to CallDistance in front of the player and sits facing them, until
    // they come to it (ReachMe) or CallWait passes; done.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogComeToHeel : ActionNode
    {
        const float Slack = 0.5f;

        Dog _dog;
        float _arrivedAt;
        float _nextGo;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _arrivedAt = float.NaN;
            _nextGo = 0f;
            if (_dog.State.CallAnnoyed && _dog.Standing)
                _dog.Act(DogAct.No);
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            DogRoster roster = _dog.Roster;
            DogSettings settings = _dog.Settings;
            Vector3 player = roster.PlayerPosition;
            if (!float.IsNaN(_arrivedAt))
            {
                if (!DogWalk.Near(_dog, player, settings.CallDistance + 2.5f))
                    _arrivedAt = float.NaN;
                else if (DogState.CallOver(now - _arrivedAt, FlatMath.Length(player - _dog.transform.position), settings))
                {
                    _dog.State.EndCall();
                    return TaskStatus.Success;
                }
                return TaskStatus.Running;
            }
            if (DogWalk.Near(_dog, player, settings.CallDistance + Slack))
            {
                _arrivedAt = now;
                DogWalk.Settle(_dog, DogPose.Sit, player);
                return TaskStatus.Running;
            }
            if (now >= _nextGo)
            {
                _nextGo = now + 0.4f;
                Vector3 forward = roster.PlayerForward;
                forward.y = 0f;
                Vector3 front = player + (forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward) * settings.CallDistance;
                float distance = FlatMath.Length(front - _dog.transform.position);
                DogGait gait = _dog.State.CallAnnoyed ? DogGait.Walk : distance > 12f ? DogGait.Sprint : DogGait.Canter;
                DogWalk.Go(_dog, front, gait);
            }
            return TaskStatus.Running;
        }
    }

    // ---- Petted ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsPetted : DogCondition
    {
        protected override bool Check() => _dog.State.Petting(Time.time) && _dog.Roster.HasPlayer;
    }

    // Comes in front of the player, happy (a roll now and then, or a head tilt), then sits for the rest of it.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogBePetted : ActionNode
    {
        Dog _dog;
        bool _acted;
        float _sitAt;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _acted = false;
            _sitAt = float.NaN;
        }

        public override TaskStatus OnUpdate()
        {
            DogRoster roster = _dog.Roster;
            Vector3 player = roster.PlayerPosition;
            if (!_acted)
            {
                float front = _dog.Settings.PetDistance;
                if (!DogWalk.Near(_dog, player, front + 0.5f))
                {
                    Vector3 forward = roster.PlayerForward;
                    forward.y = 0f;
                    DogWalk.Go(_dog, player + forward.normalized * front, DogGait.Trot);
                    return TaskStatus.Running;
                }
                DogWalk.Settle(_dog, DogPose.Stand, player);
                if (!_dog.Standing)
                    return TaskStatus.Running;
                float roll = Random.value;
                _dog.Act(roll < 0.25f ? DogAct.Roll : roll < 0.6f ? DogAct.HeadTilt : DogAct.Happy);
                _acted = true;
                // The petting counts from here (getting up and coming over may take a while).
                _dog.State.PetArrived(Time.time);
                _sitAt = Time.time + 2.6f;
                return TaskStatus.Running;
            }
            if (!float.IsNaN(_sitAt) && Time.time >= _sitAt)
            {
                _sitAt = float.NaN;
                DogWalk.Settle(_dog, DogPose.Sit, player);
            }
            return _dog.State.Petting(Time.time) ? TaskStatus.Running : TaskStatus.Success;
        }
    }

    // ---- Told to stay: a little sad ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsSad : DogCondition
    {
        protected override bool Check() => _dog.State.Sad(Time.time);
    }

    // Stops and watches the player go: a whine, a slow sit, then down on the ground with its head on its paws.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogSadStay : ActionNode
    {
        Dog _dog;
        float _startedAt;
        int _step;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _startedAt = Time.time;
            _step = 0;
            DogWalk.Settle(_dog, DogPose.Stand, _dog.Roster.PlayerPosition);
            _dog.Manager.TryPlay(_dog, DogSound.Whine);
        }

        public override TaskStatus OnUpdate()
        {
            float t = Time.time - _startedAt;
            Vector3 player = _dog.Roster.PlayerPosition;
            if (_step == 0 && t > 1.2f)
            {
                _step = 1;
                DogWalk.Settle(_dog, DogPose.Sit, player);
            }
            else if (_step == 1 && t > 3.5f)
            {
                _step = 2;
                _dog.SetPose(DogPose.Lie);
            }
            else if (_step < 2 && _dog.Pose == DogPose.Stand)
                _dog.Legs.Face(player);
            return _dog.State.Sad(Time.time) ? TaskStatus.Running : TaskStatus.Success;
        }
    }
}
