using MB13.Aipocalypse.Core.AnimalSystem;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's tree (DogTasks.cs): herding, staying, nights and days at home.

    // ---- The farm: herding ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsHerding : DogCondition
    {
        protected override bool Check() => _dog.State.Order == DogOrder.Herding;
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
                float distance = FlatMath.Length(animal.transform.position - home);
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

    // ---- Staying ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsStayingOut : DogCondition
    {
        protected override bool Check() => _dog.State.Order == DogOrder.Stay && !_dog.Roster.InYard(_dog.State.StayPoint);
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
            Vector3 look = roster.HasPlayer && FlatMath.Length(roster.PlayerPosition - spot) < 30f
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
            DogWalk.Settle(_dog, pose, look, spot);
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
    public sealed class DogIsNight : DogCondition
    {
        protected override bool Check() => _dog.Roster.Night;
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
                if (animal.GoingHome || animal.Sleeping || !animal.Definition.ChasedByDog)
                    continue;
                float d = FlatMath.Length(animal.transform.position - _dog.transform.position);
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
