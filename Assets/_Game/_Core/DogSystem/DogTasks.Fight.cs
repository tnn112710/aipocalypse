using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's tree (DogTasks.cs): knocked down, backing off hurt, the fight, warning growls, raids at home.

    // ---- Knocked down ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsDown : DogCondition
    {
        protected override bool Check() => _dog.State.Down;
    }

    // Lies where it fell, whining now and then, until helped up (hold E) or it gets up by itself.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogLieDown : ActionNode
    {
        Dog _dog;
        float _nextWhine;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _dog.Legs.Halt();
            _dog.SetCombat(false);
            _dog.SetSniffing(false);
            _dog.Escaping = false;
            _nextWhine = Time.time + 2f;
        }

        public override TaskStatus OnUpdate()
        {
            if (Time.time >= _nextWhine)
            {
                _nextWhine = Time.time + Random.Range(5f, 9f);
                _dog.Manager.TryPlay(_dog, DogSound.Whine);
            }
            return _dog.State.Down ? TaskStatus.Running : TaskStatus.Success;
        }
    }

    // ---- Hurt: back off to the player ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsRetreating : DogCondition
    {
        protected override bool Check() => _dog.Manager.InFight && _dog.Roster.HasPlayer && _dog.Manager.TargetId < 0
            && (_dog.State.Retreating(_dog.Manager.Stats) || _dog.State.Limping(Time.time));
    }

    // Keeps close behind the player while it's hurt in a fight, whimpering.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogBackOff : ActionNode
    {
        Dog _dog;
        float _nextGo;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _dog.SetCombat(false);
            _dog.Manager.TryPlay(_dog, DogSound.Whine);
        }

        public override TaskStatus OnUpdate()
        {
            if (Time.time < _nextGo)
                return TaskStatus.Running;
            _nextGo = Time.time + 0.4f;
            DogRoster roster = _dog.Roster;
            Vector3 behind = roster.PlayerForward;
            behind.y = 0f;
            Vector3 heel = roster.PlayerPosition - (behind.sqrMagnitude > 0.01f ? behind.normalized : Vector3.forward) * 1.6f;
            if (!DogWalk.Near(_dog, heel, 0.8f))
                DogWalk.Go(_dog, heel, DogGait.Canter);
            else
                _dog.Legs.Face(roster.PlayerPosition + behind * 5f);
            return TaskStatus.Running;
        }
    }

    // ---- Fighting: bite and run ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogHasFoe : DogCondition
    {
        protected override bool Check() => _dog.Manager.TargetId >= 0 || _dog.Pinning || _dog.BiteLanded == 2;
    }

    // Circles the enemy, growling (a feint now and then); at an opening with its bite ready it darts in and bites (a
    // stagger, or a takedown that drags the enemy down, held in a pin), then dodges out and circles again. Too big to
    // bite: it only circles and barks. No room to circle: it waits by the player between bites.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogSkirmish : ActionNode
    {
        enum Phase { Circle, Dart, Biting, Pin, Escape }

        Dog _dog;
        bool _started;
        Phase _phase;
        int _foeId;
        float _phaseStart;
        float _circleStart;
        float _angle;
        float _turned;
        int _direction;
        float _nextGo;
        float _nextBark;
        bool _roomChecked, _room;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _dog.SetCombat(true);
            _dog.SetSniffing(false);
            _dog.SetPose(DogPose.Stand);
            Begin(Phase.Circle);
            _foeId = _dog.Manager.TargetId;
            _direction = Random.value < 0.5f ? 1 : -1;
            _roomChecked = false;
            if (_dog.Manager.TryGetFoe(_foeId, out FoeView foe))
            {
                Vector3 away = _dog.transform.position - foe.Position;
                _angle = Mathf.Atan2(away.x, away.z);
            }
        }

        void Begin(Phase phase)
        {
            _phase = phase;
            _phaseStart = Time.time;
            if (phase == Phase.Circle)
            {
                _circleStart = Time.time;
                _turned = 0f;
            }
        }

        public override TaskStatus OnUpdate()
        {
            DogManager manager = _dog.Manager;
            DogStatSheet stats = manager.Stats;
            DogSettings settings = _dog.Settings;
            float now = Time.time;
            // The pin holds over the body (the enemy is already gone from the list).
            if (_phase == Phase.Pin)
            {
                if (now - _phaseStart < stats.Get(DogStat.PinTime))
                    return TaskStatus.Running;
                _dog.SetPin(false);
                return TaskStatus.Success;
            }
            // The bite was asked for last think: a takedown pins (the manager set the pin), else out.
            if (_phase == Phase.Biting)
            {
                if (_dog.BiteLanded == 0)
                    return now - _phaseStart < 0.5f ? TaskStatus.Running : TaskStatus.Success;
                if (_dog.BiteLanded == 2)
                {
                    Begin(Phase.Pin);
                    return TaskStatus.Running;
                }
                if (manager.TryGetFoe(_foeId, out FoeView bitten))
                    Escape(bitten);
                else
                    return TaskStatus.Success;
                return TaskStatus.Running;
            }
            if (manager.TargetId != _foeId)
                return TaskStatus.Success;
            if (!manager.TryGetFoe(_foeId, out FoeView foe))
                return TaskStatus.Success;
            float cooldown = stats.Get(DogStat.BiteCooldown);
            switch (_phase)
            {
                case Phase.Circle:
                {
                    if (!_roomChecked)
                    {
                        _roomChecked = true;
                        _room = Room(foe.Position, stats.Get(DogStat.CircleRadius), settings);
                    }
                    bool canBite = DogCombat.CanBite(foe, stats);
                    bool ready = now >= _dog.State.BiteReadyAt;
                    if (canBite && ready && DogCombat.IsOpening(foe, _dog.transform.position, now - _circleStart, cooldown, settings.OpeningAngle, settings.CircleWait))
                    {
                        Begin(Phase.Dart);
                        return TaskStatus.Running;
                    }
                    if (!canBite && now >= _nextBark)
                    {
                        _nextBark = now + Random.Range(2f, 4f);
                        if (manager.TryPlay(_dog, DogSound.Bark) && _dog.Standing)
                            _dog.Act(DogAct.Bark);
                    }
                    if (now < _nextGo)
                        return TaskStatus.Running;
                    _nextGo = now + 0.3f;
                    if (!_room)
                    {
                        // No room round it: wait by the player.
                        Vector3 side = _dog.transform.position - _dog.Roster.PlayerPosition;
                        side.y = 0f;
                        DogWalk.Go(_dog, _dog.Roster.PlayerPosition + (side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right) * 1.6f, DogGait.Trot);
                        return TaskStatus.Running;
                    }
                    float radius = stats.Get(DogStat.CircleRadius);
                    float step = stats.Get(DogStat.CircleSpeed) / Mathf.Max(radius, 0.5f) * 0.3f * _direction;
                    _angle += step;
                    _turned += Mathf.Abs(step);
                    if (_turned >= Mathf.PI * 2f)
                    {
                        _turned = 0f;
                        if (Random.value < stats.Get(DogStat.FeintChance) && _dog.Standing)
                            _dog.Attack(3);
                    }
                    Vector3 point = foe.Position + new Vector3(Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) * radius;
                    DogWalk.Go(_dog, point, DogGait.Canter);
                    if (Random.value < 0.08f)
                        manager.TryPlay(_dog, DogSound.Growl);
                    return TaskStatus.Running;
                }
                case Phase.Dart:
                {
                    if (FlatMath.Length(foe.Position - _dog.transform.position) > 1.4f)
                    {
                        if (now - _phaseStart > 4f)
                            Begin(Phase.Circle);
                        else if (now >= _nextGo)
                        {
                            _nextGo = now + 0.2f;
                            DogWalk.Go(_dog, foe.Position, DogGait.Sprint);
                        }
                        return TaskStatus.Running;
                    }
                    _dog.Legs.Halt();
                    _dog.Legs.Face(foe.Position);
                    Vector3 local = _dog.transform.InverseTransformPoint(foe.Position);
                    _dog.Attack(Mathf.Abs(local.x) < 0.3f ? 0 : local.x < 0f ? 1 : 2);
                    _dog.State.Bitten(now, cooldown);
                    manager.RequestBite(_dog, _foeId);
                    Begin(Phase.Biting);
                    return TaskStatus.Running;
                }
                case Phase.Escape:
                    if (now - _phaseStart < 1.8f && !(_dog.Legs.Settled && now - _phaseStart > 0.6f))
                        return TaskStatus.Running;
                    _dog.Escaping = false;
                    Begin(Phase.Circle);
                    return TaskStatus.Running;
            }
            return TaskStatus.Running;
        }

        // A dodge out, then a run to circling range on the far side from where the enemy faces.
        void Escape(FoeView foe)
        {
            _dog.Escaping = true;
            Vector3 local = _dog.transform.InverseTransformPoint(foe.Position);
            _dog.Dodge(local.x < 0f ? 1 : 0);
            Vector3 away = _dog.transform.position - foe.Position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -_dog.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away) * _direction;
            Vector3 target = foe.Position + (away + side).normalized * _dog.Manager.Stats.Get(DogStat.CircleRadius);
            _dog.Legs.SetSpeed(_dog.Manager.Stats.Get(DogStat.EscapeSpeed));
            _dog.Legs.MoveTo(target, 3f);
            _angle = Mathf.Atan2(target.x - foe.Position.x, target.z - foe.Position.z);
            Begin(Phase.Escape);
        }

        // Enough reachable points round the enemy to circle it.
        bool Room(Vector3 center, float radius, DogSettings settings)
        {
            int reachable = 0;
            for (int i = 0; i < settings.CirclePoints; i++)
            {
                float a = i * Mathf.PI * 2f / settings.CirclePoints;
                Vector3 p = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                if (_dog.Legs.TryFindDestination(p, 1f, 0f, 1, out _))
                    reachable++;
            }
            return reachable >= settings.CircleRoom;
        }

        // Once per run (Behavior Designer may call OnEnd again on a finished task: see DogWelcome.OnEnd).
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.SetCombat(false);
            _dog.SetPin(false);
            _dog.Escaping = false;
            // A takedown's "landed" keeps DogHasFoe true for the pin only; left set, the fight branch never lets go.
            _dog.BiteLanded = 0;
        }
    }

    // ---- A warning growl at an enemy the player hasn't noticed ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogShouldWarn : DogCondition
    {
        protected override bool Check() => (_dog.Manager.WarnId >= 0 || _dog.Manager.Warning) && _dog.State.Order == DogOrder.Follow;
    }

    // Stops, faces the enemy in its fighting stance and growls (spatial: the player hears which way to look).
    [Category("Aipocalypse/Dogs")]
    public sealed class DogWarn : ActionNode
    {
        Dog _dog;
        float _until;
        float _giveUp;
        bool _growled;
        bool _found;
        bool _started;
        Vector3 _at;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _growled = false;
            _giveUp = Time.time + 6f;
            _found = _dog.Manager.TryGetFoe(_dog.Manager.WarnId, out FoeView foe);
            if (!_found)
                return;
            _dog.Manager.Warned(Time.time);
            _at = foe.Position;
            _dog.SetPose(DogPose.Stand);
            _dog.Legs.Halt();
            _dog.SetCombat(true);
        }

        // Up on its feet first (it may have been lying down), then the growl, facing the enemy.
        public override TaskStatus OnUpdate()
        {
            if (!_found)
                return TaskStatus.Success;
            float now = Time.time;
            _dog.Legs.Face(_at);
            if (!_growled)
            {
                if (!_dog.Standing)
                {
                    if (now < _giveUp)
                        return TaskStatus.Running;
                    _dog.Manager.WarningDone();
                    return TaskStatus.Success;
                }
                _growled = true;
                _until = now + 2.5f;
                _dog.Act(DogAct.Growl);
                _dog.Manager.TryPlay(_dog, DogSound.Growl);
            }
            if (now < _until)
                return TaskStatus.Running;
            _dog.Manager.WarningDone();
            return TaskStatus.Success;
        }

        // Cut off (a whistle, a fight), the warning ends with it (once per run: see DogWelcome.OnEnd).
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.SetCombat(false);
            _dog.Manager.WarningDone();
        }
    }

    // ---- Raids at home, Passive: keep away ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogRaiderClose : DogCondition
    {
        protected override bool Check() => _dog.Manager.RaiderClose(_dog, out _);
    }

    // Runs to the spot in the yard farthest from the raider (never bites); barks from there.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogAvoid : ActionNode
    {
        Dog _dog;
        float _nextGo;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (now < _nextGo)
                return TaskStatus.Running;
            _nextGo = now + 0.6f;
            if (!_dog.Manager.RaiderClose(_dog, out Vector3 raider))
                return TaskStatus.Success;
            Vector3 best = _dog.transform.position;
            float bestDistance = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = DogWalk.InYard(_dog.Roster, _dog, 2f);
                float d = FlatMath.Length(p - raider);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            DogWalk.Go(_dog, best, DogGait.Sprint);
            return TaskStatus.Running;
        }
    }

    // ---- A raid at home: up and watching, never asleep ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsRaidAtHome : DogCondition
    {
        protected override bool Check() => _dog.Roster.Raid && _dog.State.Order != DogOrder.Follow && _dog.Manager.AtHome(_dog);
    }

    // Stands alert where it is, facing the nearest raider it sees (DogFight.RaidBarks does the barking).
    [Category("Aipocalypse/Dogs")]
    public sealed class DogWatchRaid : ActionNode
    {
        Dog _dog;
        float _nextLook;
        bool _alerted;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _dog.SetSniffing(false);
            _dog.SetPose(DogPose.Stand);
            _dog.Legs.Halt();
            _nextLook = 0f;
            _alerted = false;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (now < _nextLook)
                return TaskStatus.Running;
            _nextLook = now + 0.3f;
            if (!_dog.Manager.NearestFoe(_dog, out Vector3 raider))
                return TaskStatus.Running;
            _dog.Legs.Face(raider);
            if (!_alerted && _dog.Standing)
            {
                _alerted = true;
                _dog.Act(DogAct.Alert);
            }
            return TaskStatus.Running;
        }
    }
}
