using MB13.Aipocalypse.Core.AnimalSystem;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Tasks of the dog's tree (AgentScripts/Dogs/Dog.tree.txt). Tuning comes from DogSettings; the world (night, the
    // player) from DogRoster; orders and what happened to it from its State; where to be while following from its Plan.

    // Moving helpers shared by the tasks.
    static class DogWalk
    {
        public const float Timeout = 8f;

        public static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        public static bool Near(Dog dog, Vector3 point, float distance) => Flat(point - dog.transform.position) <= distance;

        // On its feet and walking toward `point` at `gait`; false while it's still getting up.
        public static bool Go(Dog dog, Vector3 point, DogGait gait)
        {
            if (dog.Pose != DogPose.Stand)
                dog.SetPose(DogPose.Stand);
            if (!dog.Standing)
                return false;
            dog.Legs.SetGait(gait);
            dog.Legs.MoveTo(point, Timeout);
            return true;
        }

        public static bool TryGo(Dog dog, Vector3 center, float radius, DogGait gait, out Vector3 point)
        {
            if (!dog.Legs.TryFindDestination(center, radius, 0.5f, 6, out point))
                return false;
            return Go(dog, point, gait);
        }

        // Stops and settles into `pose` facing `look`.
        public static void Settle(Dog dog, DogPose pose, Vector3 look)
        {
            dog.Legs.Halt();
            dog.SetSniffing(false);
            if (pose == DogPose.Stand || dog.Pose == DogPose.Stand)
                dog.Legs.Face(look);
            dog.SetPose(pose);
        }

        // A random spot inside the yard (shrunk by `margin`), or near the house when there's no yard.
        public static Vector3 InYard(DogRoster roster, Dog dog, float margin)
        {
            var yard = roster.Yard;
            if (yard == null)
                return roster.Home(dog) + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            Vector3 half = yard.HalfSize;
            var local = new Vector3(Random.Range(-half.x + margin, half.x - margin), 0f, Random.Range(-half.z + margin, half.z - margin));
            Vector3 world = yard.WorldToLocal.inverse.MultiplyPoint3x4(local);
            world.y = dog.transform.position.y;
            return world;
        }
    }

    // ---- Knocked down ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsDown : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Down ? TaskStatus.Success : TaskStatus.Failure;
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
    public sealed class DogIsRetreating : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.Manager.InFight && _dog.Roster.HasPlayer && _dog.Manager.TargetId < 0
            && (_dog.State.Retreating(_dog.Manager.Stats) || _dog.State.Limping(Time.time)) ? TaskStatus.Success : TaskStatus.Failure;
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
    public sealed class DogHasFoe : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.Manager.TargetId >= 0 || _dog.Pinning || _dog.BiteLanded == 2 ? TaskStatus.Success : TaskStatus.Failure;
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
                    if (DogWalk.Flat(foe.Position - _dog.transform.position) > 1.4f)
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
    public sealed class DogShouldWarn : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => (_dog.Manager.WarnId >= 0 || _dog.Manager.Warning) && _dog.State.Order == DogOrder.Follow
            ? TaskStatus.Success : TaskStatus.Failure;
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

    // ---- The player badly hurt: it stays close and whines ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogPlayerHurt : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.Manager.PlayerHurt && _dog.State.Order == DogOrder.Follow && _dog.Roster.HasPlayer
            ? TaskStatus.Success : TaskStatus.Failure;
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

    // ---- The farm: herding ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsHerding : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Order == DogOrder.Herding ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Gets behind the animal farthest from home (from home's side), barks it on and hurries it; on to the next. All home,
    // or HerdGiveUp: it stays at home.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogHerd : ActionNode
    {
        Dog _dog;
        float _nextGo;
        float _nextBark;
        readonly System.Collections.Generic.List<HerdView> _views = new();
        readonly System.Collections.Generic.Dictionary<int, float> _bestDistance = new();
        readonly System.Collections.Generic.Dictionary<int, float> _progressAt = new();

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _bestDistance.Clear();
            _progressAt.Clear();
            _dog.SetSniffing(false);
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            DogManager manager = _dog.Manager;
            var herd = manager.Herd;
            if (herd == null || herd.Pen == null)
                return Finish();
            Vector3 home = herd.Pen.transform.position;
            if (DogState.HerdDone(herd.CountHome(), herd.Animals.Count, _dog.State.HerdStartedAt, now))
                return Finish();
            if (now < _nextGo)
                return TaskStatus.Running;
            _nextGo = now + 0.5f;
            _views.Clear();
            for (int i = 0; i < herd.Animals.Count; i++)
            {
                var animal = herd.Animals[i];
                float distance = DogWalk.Flat(animal.transform.position - home);
                if (!_bestDistance.TryGetValue(i, out float best) || distance < best - 0.5f)
                {
                    _bestDistance[i] = distance;
                    _progressAt[i] = now;
                }
                _views.Add(new HerdView(i, animal.transform.position, animal.Mover.Velocity.magnitude, animal.IsHome || animal.Sleeping, now - _progressAt[i]));
            }
            int target = DogState.PickHerdTarget(_views, home);
            if (target < 0)
                return Finish();
            var chosen = herd.Animals[target];
            Vector3 away = chosen.transform.position - home;
            away.y = 0f;
            Vector3 behind = chosen.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * _dog.Settings.HerdBehind;
            if (!DogWalk.Near(_dog, behind, 1.5f))
            {
                DogWalk.Go(_dog, behind, DogGait.Canter);
                return TaskStatus.Running;
            }
            _dog.Legs.Face(chosen.transform.position);
            if (now >= _nextBark)
            {
                _nextBark = now + Random.Range(3f, 6f);
                if (manager.TryPlay(_dog, DogSound.Bark) && _dog.Standing)
                    _dog.Act(DogAct.Bark);
            }
            chosen.Hurry();
            return TaskStatus.Running;
        }

        TaskStatus Finish()
        {
            DogRoster roster = _dog.Roster;
            _dog.State.SetOrder(DogOrder.Stay, roster.Home(_dog));
            _dog.Act(DogAct.Happy);
            return TaskStatus.Success;
        }
    }

    // ---- Raids at home, Passive: keep away ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogRaiderClose : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.Manager.RaiderClose(_dog, out _) ? TaskStatus.Success : TaskStatus.Failure;
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
                float d = DogWalk.Flat(p - raider);
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
    public sealed class DogIsRaidAtHome : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.Roster.Raid && _dog.State.Order != DogOrder.Follow && _dog.Manager.AtHome(_dog)
            ? TaskStatus.Success
            : TaskStatus.Failure;
    }

    // Stands alert where it is, facing the nearest raider it sees (DogManager.RaidBarks does the barking).
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

    // ---- Welcome home: staying at home, the player back after a while ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsWelcoming : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Welcoming(Time.time) && _dog.State.Order == DogOrder.Stay && _dog.Roster.HasPlayer
            ? TaskStatus.Success
            : TaskStatus.Failure;
    }

    // Runs to the player, happy (a bark within the budget), circles them once, then sits facing them a moment.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogWelcome : ActionNode
    {
        enum Phase { Run, Circle, Sit }

        const float Reach = 2.2f;
        const float CircleRadius = 1.6f;
        // The lap's target runs this far ahead of the dog (radians); a slow lap (the happy act, a detour) ends here so
        // the sit still fits in the welcome.
        const float LapLead = 0.8f;
        const float LapGiveUp = 8f;
        const float SitTime = 2.5f;

        Dog _dog;
        Phase _phase;
        float _phaseStart;
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
                        _phase = Phase.Circle;
                        _phaseStart = now;
                        Vector3 away = _dog.transform.position - player;
                        _angle = Mathf.Atan2(away.x, away.z);
                        _turned = 0f;
                        if (_dog.Manager.TryPlay(_dog, DogSound.Bark) && _dog.Standing)
                            _dog.Act(DogAct.Happy);
                    }
                    else if (now >= _nextGo)
                    {
                        // To just in front of the player, not into them.
                        _nextGo = now + 0.3f;
                        Vector3 toDog = _dog.transform.position - player;
                        toDog.y = 0f;
                        DogWalk.Go(_dog, player + (toDog.sqrMagnitude > 0.01f ? toDog.normalized : Vector3.forward) * 1.4f, DogGait.Canter);
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
            return nearest.node != null && DogWalk.Flat(nearest.position - point) < 0.4f;
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
    public sealed class DogIsBedtime : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Order == DogOrder.Follow && _dog.Manager.TryGetBedSpot(out _, out _, out _)
            ? TaskStatus.Success
            : TaskStatus.Failure;
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
    public sealed class DogIsCalled : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Called(Time.time) && _dog.Roster.HasPlayer ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Runs (walks, when annoyed, after a head shake) to the player's side, sits facing them for a moment, done.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogComeToHeel : ActionNode
    {
        const float Heel = 1.6f;
        const float WaitTime = 2.5f;

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
            Vector3 player = roster.PlayerPosition;
            if (!float.IsNaN(_arrivedAt))
            {
                if (!DogWalk.Near(_dog, player, Heel + 2.5f))
                    _arrivedAt = float.NaN;
                else if (now - _arrivedAt >= WaitTime)
                {
                    _dog.State.EndCall();
                    return TaskStatus.Success;
                }
                return TaskStatus.Running;
            }
            if (DogWalk.Near(_dog, player, Heel + 0.4f))
            {
                _arrivedAt = now;
                DogWalk.Settle(_dog, DogPose.Sit, player);
                return TaskStatus.Running;
            }
            if (now >= _nextGo)
            {
                _nextGo = now + 0.4f;
                Vector3 side = _dog.transform.position - player;
                side.y = 0f;
                Vector3 heel = player + (side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right) * Heel;
                float distance = DogWalk.Flat(heel - _dog.transform.position);
                DogGait gait = _dog.State.CallAnnoyed ? DogGait.Walk : distance > 12f ? DogGait.Sprint : DogGait.Canter;
                DogWalk.Go(_dog, heel, gait);
            }
            return TaskStatus.Running;
        }
    }

    // ---- Petted ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsPetted : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Petting(Time.time) && _dog.Roster.HasPlayer ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Comes in front of the player, happy (a roll now and then, or a head tilt), then sits for the rest of it.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogBePetted : ActionNode
    {
        const float Front = 1.2f;

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
                if (!DogWalk.Near(_dog, player, Front + 0.5f))
                {
                    Vector3 forward = roster.PlayerForward;
                    forward.y = 0f;
                    DogWalk.Go(_dog, player + forward.normalized * Front, DogGait.Trot);
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
    public sealed class DogIsSad : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Sad(Time.time) ? TaskStatus.Success : TaskStatus.Failure;
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

    // ---- Following ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogMustCatchUp : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Order == DogOrder.Follow && _dog.Roster.HasPlayer && _dog.Plan.Mode == FollowMode.CatchUp
            ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Fallen far behind or cut off: appears near the player out of their sight (tried every RetryTime until a spot is
    // found), meanwhile running toward them.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogCatchUp : ActionNode
    {
        const float RetryTime = 0.5f;

        Dog _dog;
        float _nextTry;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _nextTry = 0f;
            _dog.ResetBody();
            _dog.SpeedOverride = -1f;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (now < _nextTry)
                return TaskStatus.Running;
            _nextTry = now + RetryTime;
            if (_dog.Manager.TryFindReappearPoint(_dog, _dog.Plan.Goal, out Vector3 point))
            {
                _dog.SetPose(DogPose.Stand);
                _dog.SetSniffing(false);
                _dog.Legs.Warp(point);
                _dog.Follow.Reset(now);
                return TaskStatus.Success;
            }
            DogWalk.Go(_dog, _dog.Roster.PlayerPosition, DogGait.Sprint);
            return TaskStatus.Running;
        }
    }

    // ---- Over the fence ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogMustScramble : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override TaskStatus OnUpdate()
        {
            bool wantsPlayer = _dog.State.Order == DogOrder.Follow || _dog.State.Called(Time.time);
            return _dog.Scrambling || (wantsPlayer && _dog.Manager.WantsScramble) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    // Cut off by the shut fence: trots to the best crossing (the shortest way to the player over it), scrambles over by
    // hand (the body on the arc, the agent waiting) and lands on the far side's navmesh.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogScramble : ActionNode
    {
        Dog _dog;
        Vector3 _from, _to;
        float _startedAt;
        float _nextGo;
        bool _over;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _over = false;
            _nextGo = 0f;
            DogRoster roster = _dog.Roster;
            bool inside = roster.InYard(_dog.transform.position);
            float best = float.MaxValue;
            foreach (DogLinkInfo crossing in roster.Crossings)
            {
                Vector3 near = inside ? crossing.Inside : crossing.Outside;
                Vector3 far = inside ? crossing.Outside : crossing.Inside;
                float cost = DogWalk.Flat(near - _dog.transform.position) + DogWalk.Flat(roster.PlayerPosition - far);
                if (cost >= best)
                    continue;
                best = cost;
                _from = near;
                _to = far;
            }
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (!_over)
            {
                if (!DogWalk.Near(_dog, _from, 0.5f))
                {
                    if (now >= _nextGo)
                    {
                        _nextGo = now + 0.5f;
                        DogWalk.Go(_dog, _from, DogGait.Canter);
                    }
                    return TaskStatus.Running;
                }
                if (!_dog.Standing)
                    return TaskStatus.Running;
                _dog.Legs.Halt();
                _over = true;
                _dog.Scrambling = true;
                _startedAt = now;
                _dog.PlayJump(DogJumpArc.AnimatorKind(JumpKind.Scramble, 0f));
            }
            Vector3 start = _dog.transform.position;
            float obstacle = 2f;
            float peak = DogJumpArc.Peak(JumpKind.Scramble, start, _to, obstacle);
            float duration = DogJumpArc.Duration(JumpKind.Scramble, DogWalk.Flat(_to - start));
            float k = (now - _startedAt) / duration;
            Vector3 across = _to - start;
            across.y = 0f;
            Quaternion facing = across.sqrMagnitude > 0.001f ? Quaternion.LookRotation(across) : _dog.transform.rotation;
            if (k < 1f)
            {
                _dog.SetBody(DogJumpArc.Position(JumpKind.Scramble, start, _to, peak, k) - start, facing);
                return TaskStatus.Running;
            }
            _dog.Legs.Warp(_to);
            _dog.transform.rotation = facing;
            _dog.ResetBody();
            _dog.Scrambling = false;
            return TaskStatus.Success;
        }

        public override void OnEnd()
        {
            if (_dog == null || !_dog.Scrambling)
                return;
            // Cut short mid-air: it lands on the far side at once (never stuck halfway over the fence).
            _dog.Legs.Warp(_to);
            _dog.ResetBody();
            _dog.Scrambling = false;
        }
    }

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsFollowing : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Order == DogOrder.Follow && _dog.Roster.HasPlayer ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Goes where its Plan says (back to the player, ahead, alongside, out of the aim); inside the comfort zone it does dog
    // things: sniffs about, digs or marks a spot, looks round, checks in on the player now and then, and settles (sit,
    // lie, nap) while they stand still.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogFollowPlayer : ActionNode
    {
        enum Free { None, Sniff, Look, CheckIn, Settled }

        Dog _dog;
        bool _started;
        Vector3 _goal;
        float _nextGo;
        Free _free;
        float _freeUntil;
        float _nextCheckIn;
        float _sitAt, _lieAt;
        bool _settleRolled;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _free = Free.None;
            _nextGo = 0f;
            _settleRolled = false;
            DogSettings settings = _dog.Settings;
            _nextCheckIn = Time.time + Random.Range(settings.CheckIn.x, settings.CheckIn.y);
        }

        public override TaskStatus OnUpdate()
        {
            FollowPlan plan = _dog.Plan;
            if (plan.Mode == FollowMode.Free)
                FreeTime();
            else
                Move(plan);
            return TaskStatus.Running;
        }

        void Move(FollowPlan plan)
        {
            float now = Time.time;
            _free = Free.None;
            _settleRolled = false;
            _dog.SetSniffing(false);
            if (now < _nextGo && DogWalk.Flat(plan.Goal - _goal) < 0.75f)
                return;
            _nextGo = now + 0.4f;
            _goal = plan.Goal;
            DogWalk.Go(_dog, plan.Goal, plan.Gait == DogGait.Still ? DogGait.Trot : plan.Gait);
        }

        void FreeTime()
        {
            float now = Time.time;
            DogSettings settings = _dog.Settings;
            DogRoster roster = _dog.Roster;
            Vector3 player = roster.PlayerPosition;
            float still = roster.PlayerStillFor;
            // Just out of the water: stop and shake dry before anything else (DogManager plays it once it stands still).
            if (_dog.ShakePending)
            {
                if (_dog.Pose != DogPose.Stand)
                    _dog.SetPose(DogPose.Stand);
                _dog.Legs.Halt();
                return;
            }
            if (still > 0f)
            {
                if (!_settleRolled)
                {
                    _settleRolled = true;
                    _sitAt = Random.Range(settings.SitAfter.x, settings.SitAfter.y);
                    _lieAt = Random.Range(settings.LieAfter.x, settings.LieAfter.y);
                }
                DogPose pose = DogState.SettlePose(still, settings.NapAfter, _sitAt, _lieAt);
                if (pose != DogPose.Stand)
                {
                    if (_free != Free.Settled || _dog.Pose != pose)
                    {
                        if (_dog.Pose == DogPose.Stand && _dog.Standing && pose == DogPose.Sleep)
                            _dog.Act(DogAct.Yawn);
                        DogWalk.Settle(_dog, pose, player);
                    }
                    _free = Free.Settled;
                    return;
                }
            }
            else
                _settleRolled = false;
            if (_free == Free.Settled)
            {
                _free = Free.None;
                _dog.SetPose(DogPose.Stand);
            }
            if (!_dog.Standing)
                return;

            switch (_free)
            {
                case Free.Sniff when !_dog.Legs.Settled && now < _freeUntil:
                case Free.CheckIn when !_dog.Legs.Settled && now < _freeUntil:
                case Free.Look when now < _freeUntil:
                    return;
                case Free.Sniff:
                    _dog.SetSniffing(false);
                    float roll = Random.value;
                    if (roll < 0.15f)
                        _dog.Act(DogAct.Dig);
                    else if (roll < 0.22f && !roster.InYard(_dog.transform.position))
                        _dog.Act(DogAct.Pee);
                    _free = Free.Look;
                    _freeUntil = now + Random.Range(1.5f, 3f);
                    return;
                case Free.CheckIn:
                    _dog.Legs.Face(player);
                    _dog.Act(DogAct.Happy);
                    _dog.Manager.TryPlay(_dog, DogSound.Pant);
                    _free = Free.Look;
                    _freeUntil = now + 3f;
                    return;
            }

            if (now >= _nextCheckIn && !roster.PlayerAiming)
            {
                _nextCheckIn = now + Random.Range(settings.CheckIn.x, settings.CheckIn.y);
                Vector3 forward = roster.PlayerForward;
                forward.y = 0f;
                if (DogWalk.Go(_dog, player + forward.normalized * settings.CheckInDistance, DogGait.Trot))
                {
                    _free = Free.CheckIn;
                    _freeUntil = now + 6f;
                }
                return;
            }
            if (Random.value < 0.55f && SniffPoint(player, out Vector3 point))
            {
                _dog.SetSniffing(true);
                if (DogWalk.Go(_dog, point, DogGait.Walk))
                {
                    _free = Free.Sniff;
                    _freeUntil = now + 8f;
                    if (Random.value < 0.3f)
                        _dog.Manager.TryPlay(_dog, DogSound.Sniff);
                }
                return;
            }
            _dog.Legs.Halt();
            _dog.Legs.Face(_dog.transform.position + Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * _dog.transform.forward * 3f);
            _free = Free.Look;
            _freeUntil = now + Random.Range(2f, 5f);
        }

        // A spot within SniffRadius of the dog that stays inside the comfort zone round the player.
        bool SniffPoint(Vector3 player, out Vector3 point)
        {
            DogSettings settings = _dog.Settings;
            for (int i = 0; i < 4; i++)
            {
                if (!_dog.Legs.TryFindDestination(_dog.transform.position, settings.SniffRadius, 1.5f, 2, out point))
                    continue;
                if (DogWalk.Flat(point - player) < settings.OuterRadius - 1f)
                    return true;
            }
            point = default;
            return false;
        }

        // Once per run: a second call would halt whatever moves it next.
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.SetSniffing(false);
            _dog.Legs?.Halt();
        }
    }

    // ---- Staying ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsStayingOut : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.State.Order == DogOrder.Stay && !_dog.Roster.InYard(_dog.State.StayPoint)
            ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Waits where it was told: back to the spot when it wandered off, then sits, lies, naps, sniffs nearby, watches the
    // player when they're around.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogStayHere : ActionNode
    {
        Dog _dog;
        bool _started;
        float _until;
        bool _walking;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _until = 0f;
            _walking = false;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            DogSettings settings = _dog.Settings;
            Vector3 spot = _dog.State.StayPoint;
            if (_walking && !_dog.Legs.Settled)
                return TaskStatus.Running;
            if (_walking)
            {
                _walking = false;
                _dog.SetSniffing(false);
            }
            if (!DogWalk.Near(_dog, spot, settings.StayRadius))
            {
                _walking = DogWalk.Go(_dog, spot, DogGait.Trot);
                return TaskStatus.Running;
            }
            if (now < _until)
                return TaskStatus.Running;
            DogRoster roster = _dog.Roster;
            Vector3 look = roster.HasPlayer && DogWalk.Flat(roster.PlayerPosition - spot) < 30f
                ? roster.PlayerPosition
                : _dog.transform.position + Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f) * _dog.transform.forward * 3f;
            float roll = Random.value;
            if (roll < 0.3f && _dog.Legs.TryFindDestination(spot, settings.StayRadius, 1f, 3, out Vector3 sniff))
            {
                _dog.SetSniffing(true);
                _walking = DogWalk.Go(_dog, sniff, DogGait.Walk);
                return TaskStatus.Running;
            }
            DogPose pose = roll < 0.6f ? DogPose.Sit : roll < 0.9f ? DogPose.Lie : DogPose.Sleep;
            DogWalk.Settle(_dog, pose, look);
            _until = now + Random.Range(8f, 20f);
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.SetSniffing(false);
        }
    }

    // ---- At home ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsNight : ConditionalNode
    {
        Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => _dog.Roster.Night ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Goes in its dog house: the agent walks to the door and waits there (the house is cut out of the navmesh); the body
    // (the model) walks in, turns to face out, and lies down. Out again the same way.
    static class DogHouseMoves
    {
        public const float InTime = 1.2f;
        public const float TurnTime = 0.6f;

        // Step: 0 walking to the door, 1 going in, 2 turning, 3 inside. Returns the next step.
        public static int Enter(Dog dog, int step, ref float startedAt)
        {
            DogHouse house = dog.Roster.House;
            float now = Time.time;
            switch (step)
            {
                case 0:
                    if (!DogWalk.Near(dog, house.Door, 0.6f))
                    {
                        DogWalk.Go(dog, house.Door, DogGait.Trot);
                        return 0;
                    }
                    // Up on its feet first (it may have been sitting or lying by the door).
                    if (dog.Pose != DogPose.Stand)
                        dog.SetPose(DogPose.Stand);
                    if (!dog.Standing)
                        return 0;
                    dog.Legs.Halt();
                    startedAt = now;
                    dog.SpeedOverride = dog.Settings.WalkSpeed;
                    return 1;
                case 1:
                {
                    float k = Mathf.Clamp01((now - startedAt) / InTime);
                    Vector3 inward = house.Bed - dog.transform.position;
                    inward.y = 0f;
                    Quaternion facing = inward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(inward) : dog.transform.rotation;
                    dog.SetBody((house.Bed - dog.transform.position) * k, Quaternion.Slerp(dog.transform.rotation, facing, Mathf.Clamp01(k * 3f)));
                    if (k < 1f)
                        return 1;
                    startedAt = now;
                    return 2;
                }
                case 2:
                {
                    float k = Mathf.Clamp01((now - startedAt) / TurnTime);
                    Vector3 inward = house.Bed - dog.transform.position;
                    inward.y = 0f;
                    Quaternion from = inward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(inward) : dog.transform.rotation;
                    dog.SetBody(house.Bed - dog.transform.position, Quaternion.Slerp(from, house.BedFacing, k));
                    if (k < 1f)
                        return 2;
                    dog.SpeedOverride = -1f;
                    return 3;
                }
            }
            return step;
        }

        // Back out to the door, facing out; true when out.
        public static bool Leave(Dog dog, ref bool leaving, ref float startedAt)
        {
            DogHouse house = dog.Roster.House;
            if (!dog.InHouse)
                return true;
            if (!leaving)
            {
                dog.SetPose(DogPose.Stand);
                if (!dog.Standing)
                    return false;
                leaving = true;
                startedAt = Time.time;
                dog.SpeedOverride = dog.Settings.WalkSpeed;
            }
            float k = Mathf.Clamp01((Time.time - startedAt) / InTime);
            dog.SetBody((house.Bed - dog.transform.position) * (1f - k), house.BedFacing);
            if (k < 1f)
                return false;
            dog.SpeedOverride = -1f;
            dog.ResetBody();
            dog.transform.rotation = house.BedFacing;
            leaving = false;
            return true;
        }
    }

    // Night at home: a yawn, into its dog house, asleep with its head out until morning. Once a night (22:00–02:00, a
    // HowlChance roll) it comes out to the door, howls, and goes back in.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogSleepInHouse : ActionNode
    {
        enum Howl { None, Out, Howling }

        Dog _dog;
        bool _started;
        int _step;
        float _startedAt;
        Howl _howl;
        bool _leaving;
        float _howlAt;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _step = 0;
            _howl = Howl.None;
            _leaving = false;
            if (_dog.Standing)
                _dog.Act(DogAct.Yawn);
        }

        public override TaskStatus OnUpdate()
        {
            if (_dog.Roster.House == null)
            {
                DogWalk.Settle(_dog, DogPose.Sleep, _dog.transform.position + _dog.transform.forward);
                return TaskStatus.Running;
            }
            if (_howl != Howl.None)
                return Howling();
            int before = _step;
            _step = DogHouseMoves.Enter(_dog, _step, ref _startedAt);
            if (_step == 3 && before != 3)
                _dog.SetPose(DogPose.Sleep);
            var clock = _dog.Manager.Clock;
            if (_step == 3 && clock != null && _dog.State.TryHowl(clock.Now.Day, clock.Now.Hour, true, _dog.Roster.Raid, Random.value))
                _howl = Howl.Out;
            return TaskStatus.Running;
        }

        // Acts only play on its feet, so it backs out to the door first; then in again from the start.
        TaskStatus Howling()
        {
            float now = Time.time;
            if (_howl == Howl.Out)
            {
                if (!DogHouseMoves.Leave(_dog, ref _leaving, ref _startedAt) || !_dog.Standing)
                    return TaskStatus.Running;
                _dog.Act(DogAct.Howl);
                _dog.Manager.TryPlay(_dog, DogSound.Howl);
                _howl = Howl.Howling;
                _howlAt = now;
                return TaskStatus.Running;
            }
            if (now - _howlAt < 0.5f || !_dog.Standing)
                return TaskStatus.Running;
            _howl = Howl.None;
            _step = 0;
            return TaskStatus.Running;
        }

        // Once per run: a second call while another task has it in the house would pop it out.
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.SpeedOverride = -1f;
            if (!_dog.InHouse)
                return;
            // Woken or ordered out mid-way: the body is back on the agent at the door at once.
            _dog.SetPose(DogPose.Stand);
            _dog.ResetBody();
        }
    }

    // Day at home (told to stay in the yard): never long idle. Strolls the yard, sniffs and digs, trots over to its
    // favourite spots, sits and looks round, naps in its dog house doorway, and now and then a burst of zoomies.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogHomeLife : ActionNode
    {
        enum Activity { None, Stroll, Sniff, Visit, Sit, Nap, Zoomies, Yawn, ChaseSheep, SniffRobot }

        // Relative weights of what it does next.
        static readonly (Activity Activity, float Weight)[] Choices =
        {
            (Activity.Stroll, 28f), (Activity.Sniff, 24f), (Activity.Visit, 15f), (Activity.Sit, 12f), (Activity.Nap, 10f),
            (Activity.Zoomies, 5f), (Activity.Yawn, 6f),
        };

        Dog _dog;
        bool _started;
        Activity _activity;
        float _until;
        int _laps;
        int _step;
        float _startedAt;
        bool _leaving;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _activity = Activity.None;
            _dog.InHomeLife = true;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (_activity == Activity.Nap)
            {
                if (now < _until || _dog.Roster.House == null)
                {
                    if (_dog.Roster.House != null)
                    {
                        int before = _step;
                        _step = DogHouseMoves.Enter(_dog, _step, ref _startedAt);
                        if (_step == 3 && before != 3)
                            _dog.SetPose(Random.value < 0.5f ? DogPose.Lie : DogPose.Sleep);
                    }
                    return TaskStatus.Running;
                }
                if (!DogHouseMoves.Leave(_dog, ref _leaving, ref _startedAt))
                    return TaskStatus.Running;
                _activity = Activity.None;
            }
            if (_activity != Activity.None && !Done(now))
                return TaskStatus.Running;
            _dog.SetSniffing(false);
            if (_activity == Activity.ChaseSheep || _activity == Activity.SniffRobot)
                _dog.GreetedBy = null;
            // Up on its feet before the next thing (after sitting or lying), else a walk would be refused and end at once.
            if (_dog.Pose != DogPose.Stand)
                _dog.SetPose(DogPose.Stand);
            if (!_dog.Standing)
                return TaskStatus.Running;
            Begin(Special(now) ?? Pick(), now);
            return TaskStatus.Running;
        }

        bool Done(float now)
        {
            switch (_activity)
            {
                case Activity.Stroll:
                case Activity.Visit:
                    return _dog.Legs.Settled || now >= _until;
                case Activity.Sniff:
                    if (!_dog.Legs.Settled && now < _until)
                        return false;
                    if (_step == 0)
                    {
                        _step = 1;
                        _dog.SetSniffing(false);
                        if (Random.value < 0.3f)
                            _dog.Act(DogAct.Dig);
                        _until = now + Random.Range(2f, 4f);
                        return false;
                    }
                    return now >= _until;
                case Activity.ChaseSheep:
                    if (now >= _until || _sheep == null)
                    {
                        _dog.Act(DogAct.Happy);
                        return true;
                    }
                    if (now >= _nextStartle)
                    {
                        _nextStartle = now + 0.5f;
                        if (!_sheep.GoingHome)
                            _sheep.Startle(_dog.transform.position, 4f, true);
                        DogWalk.Go(_dog, _sheep.transform.position, DogGait.Sprint);
                    }
                    return false;
                case Activity.SniffRobot:
                    if (_dog.GreetedBy != null && !DogWalk.Near(_dog, _dog.GreetedBy.position, 1.4f) && now < _until)
                        return false;
                    if (_step == 0)
                    {
                        _step = 1;
                        _dog.Legs.Halt();
                        _dog.SetSniffing(true);
                        _until = now + 2.5f;
                        return false;
                    }
                    if (now < _until)
                        return false;
                    _dog.Act(DogAct.Happy);
                    return true;
                case Activity.Zoomies:
                    if (!_dog.Legs.Settled && now < _until)
                        return false;
                    if (--_laps <= 0)
                        return true;
                    Vector3 next = DogWalk.InYard(_dog.Roster, _dog, 2f);
                    DogWalk.Go(_dog, next, DogGait.Sprint);
                    _until = now + 6f;
                    return false;
                default:
                    return now >= _until;
            }
        }

        Animal _sheep;
        float _nextStartle;

        // A robot that just greeted it, or (every 3–6 min, by day) the sheep to chase.
        Activity? Special(float now)
        {
            if (_dog.GreetedBy != null)
                return Activity.SniffRobot;
            bool day = !_dog.Roster.Night;
            if (!_dog.State.TryStartSheepChase(now, day, true, false, Random.value))
                return null;
            _sheep = NearestSheep();
            return _sheep != null ? Activity.ChaseSheep : null;
        }

        Animal NearestSheep()
        {
            var herd = _dog.Manager.Herd;
            if (herd == null)
                return null;
            Animal best = null;
            float bestDistance = _dog.Settings.ChaseRange;
            foreach (Animal animal in herd.Animals)
            {
                if (animal.GoingHome || animal.Sleeping || !animal.Definition.name.Contains("Sheep"))
                    continue;
                float d = DogWalk.Flat(animal.transform.position - _dog.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = animal;
                }
            }
            return best;
        }

        // Behaviour flavour, not a gameplay roll: UnityEngine.Random is fine here.
        static Activity Pick()
        {
            int index = RandomSystem.WeightedPick.Index(Choices, c => c.Weight, Random.value);
            return index >= 0 ? Choices[index].Activity : Activity.Stroll;
        }

        void Begin(Activity activity, float now)
        {
            _activity = activity;
            _step = 0;
            DogRoster roster = _dog.Roster;
            switch (activity)
            {
                case Activity.Stroll:
                    DogWalk.Go(_dog, DogWalk.InYard(roster, _dog, 2f), Random.value < 0.75f ? DogGait.Walk : DogGait.Trot);
                    _until = now + 12f;
                    break;
                case Activity.Sniff:
                    _dog.SetSniffing(true);
                    if (!DogWalk.TryGo(_dog, _dog.transform.position, 5f, DogGait.Walk, out _))
                        _dog.SetSniffing(false);
                    else if (Random.value < 0.3f)
                        _dog.Manager.TryPlay(_dog, DogSound.Sniff);
                    _until = now + 10f;
                    break;
                case Activity.Visit:
                    if (roster.Visits.Count == 0)
                        goto case Activity.Stroll;
                    Transform visit = roster.Visits[Random.Range(0, roster.Visits.Count)];
                    DogWalk.TryGo(_dog, visit.position, 2f, DogGait.Trot, out _);
                    _until = now + 15f;
                    break;
                case Activity.Sit:
                    DogWalk.Settle(_dog, Random.value < 0.6f ? DogPose.Sit : DogPose.Lie,
                        roster.HasPlayer && roster.InYard(roster.PlayerPosition) ? roster.PlayerPosition
                            : _dog.transform.position + Quaternion.Euler(0f, Random.Range(-150f, 150f), 0f) * _dog.transform.forward * 3f);
                    _until = now + Random.Range(6f, 14f);
                    break;
                case Activity.Nap:
                    if (roster.House == null)
                        goto case Activity.Sit;
                    _until = now + Random.Range(20f, 45f);
                    _leaving = false;
                    break;
                case Activity.Zoomies:
                    _laps = Random.Range(2, 4);
                    DogWalk.Go(_dog, DogWalk.InYard(roster, _dog, 2f), DogGait.Sprint);
                    _until = now + 6f;
                    if (Random.value < 0.5f)
                        _dog.Manager.TryPlay(_dog, DogSound.Bark);
                    break;
                case Activity.ChaseSheep:
                    _until = now + 5f;
                    _nextStartle = 0f;
                    _dog.Manager.TryPlay(_dog, DogSound.Bark);
                    break;
                case Activity.SniffRobot:
                    DogWalk.Go(_dog, _dog.GreetedBy.position, DogGait.Trot);
                    _until = now + 6f;
                    break;
                case Activity.Yawn:
                    _dog.Legs.Halt();
                    _dog.Act(Random.value < 0.5f ? DogAct.Yawn : DogAct.Alert);
                    _until = now + 4f;
                    break;
            }
        }

        // Once per run: a second call after it went to sleep in its house would pop it out.
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.InHomeLife = false;
            _dog.GreetedBy = null;
            _dog.SetSniffing(false);
            _dog.SpeedOverride = -1f;
            if (!_dog.InHouse)
                return;
            _dog.SetPose(DogPose.Stand);
            _dog.ResetBody();
        }
    }
}
