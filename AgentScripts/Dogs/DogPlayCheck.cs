using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.DogSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RobotSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using Pathfinding;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

// Plays Loki in WorldMap (enter Play first, then set Application.runInBackground = true). Each entry returns PASS / FAIL
// lines. Run: unity command script run --file AgentScripts/Dogs/DogPlayCheck.cs --entry DogPlayCheck.<Entry> --args '[...]'.
// Positions are given in the yard's frame (x, z m from its centre, turned with it).
public static class DogPlayCheck
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // Injected, active, following, at its house; the house door on the navmesh and reachable from the player.
    public static string Ready()
    {
        Dog dog = Dog;
        DogRoster roster = Scope.Resolve<DogRoster>();
        var door = AstarPath.active.GetNearest(roster.House.Door, NearestNodeConstraint.Walkable);
        var player = AstarPath.active.GetNearest(Player.transform.position, NearestNodeConstraint.Walkable);
        float off = door.node == null ? float.PositiveInfinity : Vector3.Distance(door.position, roster.House.Door);
        bool reach = door.node != null && player.node != null && PathUtilities.IsPathPossible(door.node, player.node);
        bool ok = dog.Active && roster.Dog == dog && off < 0.5f && reach;
        return $"{Pass(ok)} ready: active {dog.Active}, order {dog.State.Order}, door on navmesh (off {off:0.00} m), reachable {reach}; {Line(dog)}";
    }

    // The player walks `metres` across the yard and stops: the dog keeps within the zone, isn't stopped on the frame the
    // player stops, and settles (sits) within 7.5 s of the stop.
    public static async Task<string> Follow(float metres)
    {
        Dog dog = Dog;
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        Vector3 start = Yard(-10f, -2f);
        PlacePlayer(start, start + YardRight);
        dog.Legs.Warp(Yard(-11f, 0f));
        // On its feet first (a previous probe may have left it asleep: getting up takes a few seconds).
        dog.SetPose(DogPose.Stand);
        await Seconds(4f, null);
        float farthest = 0f;
        Vector3 end = Yard(-10f + metres, -2f);
        float walkTime = metres / 3f;
        float t0 = Time.time;
        await Seconds(walkTime, () =>
        {
            float k = Mathf.Clamp01((Time.time - t0) / walkTime);
            PlacePlayer(Vector3.Lerp(start, end, k), end + YardRight * 5f);
            farthest = Mathf.Max(farthest, Flat(dog.transform.position - Player.transform.position));
        });
        float speedAtStop = Flat(dog.Legs.Velocity);
        string modeAtStop = dog.Plan.Mode.ToString();
        DogPose settled = DogPose.Stand;
        float settledAt = -1f;
        float stop = Time.time;
        await Seconds(8f, () =>
        {
            if (settledAt < 0f && dog.Pose != DogPose.Stand)
            {
                settled = dog.Pose;
                settledAt = Time.time - stop;
            }
        });
        float final = Flat(dog.transform.position - Player.transform.position);
        bool ok = final <= 8.5f && farthest <= 16f && settledAt >= 0f && settledAt <= 7.5f;
        return $"{Pass(ok)} follow {metres:0} m: farthest {farthest:0.0} m, at stop mode {modeAtStop} speed {speedAtStop:0.0}, final {final:0.0} m, " +
               $"settled {settled} after {settledAt:0.0} s; {Line(dog)}";
    }

    // A steady walk: at some point the dog is ahead of the player (scouting).
    public static async Task<string> Scout()
    {
        Dog dog = Dog;
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        // A clear line across the north of the yard (clear of the house, the herd and the robots' spots).
        Vector3 start = Yard(-14f, 8f);
        Vector3 end = Yard(14f, 8f);
        PlacePlayer(start, end);
        dog.Legs.Warp(Yard(-13f, 6.5f));
        await Seconds(1.5f, null);
        bool ahead = false;
        bool scouted = false;
        float t0 = Time.time;
        const float time = 13f;
        await Seconds(time, () =>
        {
            float k = Mathf.Clamp01((Time.time - t0) / time);
            PlacePlayer(Vector3.Lerp(start, end, k), end + YardRight * 3f);
            scouted |= dog.Plan.Mode == FollowMode.Scout;
            ahead |= Vector3.Dot(dog.transform.position - Player.transform.position, (end - start).normalized) > 2f;
        });
        return $"{Pass(scouted && ahead)} scout: mode Scout seen {scouted}, ran ahead {ahead}; {Line(dog)}";
    }

    // Teleports the player `metres` away (in the yard's frame direction): the dog reappears near them, out of view.
    public static async Task<string> CatchUp(float x, float z)
    {
        Dog dog = Dog;
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        Vector3 to = Ground(Yard(x, z));
        PlacePlayer(to, to + YardRight * 10f);
        float before = Flat(dog.transform.position - to);
        float caughtAt = -1f;
        float t0 = Time.time;
        await Seconds(6f, () =>
        {
            if (caughtAt < 0f && Flat(dog.transform.position - Player.transform.position) < 16f)
                caughtAt = Time.time - t0;
        });
        bool ok = caughtAt >= 0f && caughtAt <= 4f;
        return $"{Pass(ok)} catch up from {before:0} m: within 16 m after {caughtAt:0.0} s, now {Flat(dog.transform.position - Player.transform.position):0.0} m; {Line(dog)}";
    }

    // The player stands `outside` m beyond the fence on the yard's west side: the dog gets out (over the fence by a link,
    // or round by a gap) and back in when the player returns.
    public static async Task<string> Fence(float outside)
    {
        Dog dog = Dog;
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        Vector3 inside = Ground(Yard(-12f, 0f));
        PlacePlayer(inside, inside + YardRight);
        dog.Legs.Warp(Ground(Yard(-10f, 2f)));
        await Seconds(2f, null);
        Vector3 beyond = Ground(Yard(-yard.HalfSize.x - outside, 0f));
        PlacePlayer(beyond, beyond - YardRight);
        bool linked = false;
        var agent = dog.GetComponent<FollowerEntity>();
        await Seconds(18f, () => linked |= agent.isTraversingOffMeshLink);
        bool outOk = !yard.Contains(dog.transform.position) && Flat(dog.transform.position - beyond) < 10f;
        string outLine = $"out: outside {!yard.Contains(dog.transform.position)}, {Flat(dog.transform.position - beyond):0.0} m from the player, over a link {linked}";
        linked = false;
        PlacePlayer(inside, inside + YardRight);
        await Seconds(18f, () => linked |= agent.isTraversingOffMeshLink);
        bool inOk = yard.Contains(dog.transform.position) && Flat(dog.transform.position - inside) < 10f;
        return $"{Pass(outOk && inOk)} fence: {outLine}; back in: inside {yard.Contains(dog.transform.position)}, over a link {linked}; {Line(dog)}";
    }

    // Dog inside at (ix, iz), player outside at (ox, oz), yard frame, where no gap is near: it scrambles over the fence by
    // a dog-only link and comes back over when the player returns inside.
    public static async Task<string> Scramble(float ix, float iz, float ox, float oz)
    {
        Dog dog = Dog;
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        Vector3 inside = Yard(ix, iz);
        Vector3 beyond = Yard(ox, oz);
        PlacePlayer(inside, beyond);
        dog.Legs.Warp(Yard(ix + 1f, iz + 1f));
        await Seconds(2f, null);
        PlacePlayer(beyond, inside);
        bool linked = false;
        float peak = float.MinValue;
        Transform model = dog.transform.Find("Model");
        await Seconds(15f, () =>
        {
            linked |= dog.Scrambling;
            if (dog.Scrambling)
                peak = Mathf.Max(peak, model.position.y - beyond.y);
        });
        bool outOk = !yard.Contains(dog.transform.position) && linked;
        string outLine = $"over: outside {!yard.Contains(dog.transform.position)} by a link {linked} (peak {peak:0.0} m), {Flat(dog.transform.position - beyond):0.0} m from the player";
        linked = false;
        PlacePlayer(inside, beyond);
        await Seconds(15f, () => linked |= dog.Scrambling);
        bool inOk = yard.Contains(dog.transform.position) && linked;
        return $"{Pass(outOk && inOk)} scramble: {outLine}; back: inside {yard.Contains(dog.transform.position)} by a link {linked}; {Line(dog)}";
    }

    // The player goes into the house: the dog follows through the door (the robots' link) and back out.
    public static async Task<string> Indoors()
    {
        Dog dog = Dog;
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        Transform houseSpot = GameObject.Find("Spot_House").transform;
        Transform outside = GameObject.Find("DoorLink_House/Outside").transform;
        PlacePlayer(outside.position + (outside.position - houseSpot.position).normalized * 3f, houseSpot.position);
        dog.Legs.Warp(outside.position + (outside.position - houseSpot.position).normalized * 5f);
        await Seconds(2f, null);
        PlacePlayer(houseSpot.position, outside.position);
        await Seconds(15f, null);
        float floor = houseSpot.position.y;
        bool inside = Mathf.Abs(dog.transform.position.y - floor) < 0.35f && Flat(dog.transform.position - houseSpot.position) < 5f;
        string inLine = $"in: on the house floor {inside} (y {dog.transform.position.y:0.00} vs {floor:0.00}), {Flat(dog.transform.position - houseSpot.position):0.0} m from the player";
        PlacePlayer(outside.position + (outside.position - houseSpot.position).normalized * 4f, houseSpot.position);
        await Seconds(12f, null);
        bool back = dog.transform.position.y < floor - 0.5f;
        return $"{Pass(inside && back)} indoors: {inLine}; back out {back}; {Line(dog)}";
    }

    // Told to stay (in the yard): a sad moment (whine, sit, lie), then it stays put while the player walks away.
    public static async Task<string> Stay()
    {
        Dog dog = Dog;
        DogManager manager = Scope.Resolve<DogManager>();
        Vector3 at = Ground(Yard(4f, 6f));
        PlacePlayer(at, at + YardRight);
        dog.Legs.Warp(Ground(Yard(4f, 4f)));
        manager.Order(dog, DogOrder.Follow);
        await Seconds(2f, null);
        manager.Order(dog, DogOrder.Stay);
        bool sad = dog.State.Sad(Time.time);
        bool sat = false, lay = false;
        await Seconds(5f, () =>
        {
            sat |= dog.Pose == DogPose.Sit;
            lay |= dog.Pose == DogPose.Lie;
        });
        Vector3 away = Ground(Yard(-12f, -6f));
        PlacePlayer(away, away + YardRight);
        Vector3 stayedAt = dog.transform.position;
        await Seconds(8f, null);
        bool followed = Flat(dog.transform.position - away) < 6f;
        bool ok = sad && sat && lay && !followed;
        return $"{Pass(ok)} stay: sad {sad}, sat {sat}, lay down {lay}, followed the player {followed}, order {dog.State.Order}; {Line(dog)}";
    }

    // At home in the day: it never stands still long. Reports how far it moved and what it did over `seconds`.
    public static async Task<string> HomeLife(float seconds)
    {
        Dog dog = Dog;
        Scope.Resolve<WorldClock>().JumpTo(10f);
        DogManager manager = Scope.Resolve<DogManager>();
        manager.Order(dog, DogOrder.Stay);
        PlacePlayer(Ground(Yard(-14f, -10f)), Ground(Yard(0f, 0f)));
        float moved = 0f;
        Vector3 last = dog.transform.position;
        float longestStill = 0f, still = 0f;
        await Seconds(seconds, () =>
        {
            float step = Flat(dog.transform.position - last);
            last = dog.transform.position;
            moved += step;
            still = step < 0.002f ? still + Time.deltaTime : 0f;
            longestStill = Mathf.Max(longestStill, still);
        });
        bool ok = moved > 8f && longestStill < 50f;
        return $"{Pass(ok)} home life {seconds:0} s: moved {moved:0.0} m, longest still {longestStill:0} s; {Line(dog)}";
    }

    // A plain whistle (aim at the sky) from `metres` away calls the dog to heel and makes it follow.
    public static async Task<string> Whistle(float metres)
    {
        Dog dog = Dog;
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Stay);
        dog.State.Pet(Time.time - 10f);
        Vector3 from = Ground(dog.transform.position + YardRight * metres);
        PlacePlayer(from, from + Vector3.up * 30f + YardRight);
        await Seconds(1f, () => AimAt(from + YardRight * 5f + Vector3.up * 30f));
        var target = WhistleTick();
        bool answered = ReferenceEquals(target, dog);
        float arrivedAt = -1f;
        float t0 = Time.time;
        await Seconds(20f, () =>
        {
            if (arrivedAt < 0f && Flat(dog.transform.position - Player.transform.position) < 2.6f)
                arrivedAt = Time.time - t0;
        });
        bool ok = answered && arrivedAt >= 0f && dog.State.Order == DogOrder.Follow;
        return $"{Pass(ok)} whistle from {metres:0} m: answered {answered}, at heel after {arrivedAt:0.0} s, order {dog.State.Order}; {Line(dog)}";
    }

    // Four whistles in a row annoy it (the next is ignored); a pet forgives.
    public static async Task<string> WhistleSpam()
    {
        Dog dog = Dog;
        DogManager manager = Scope.Resolve<DogManager>();
        manager.Pet(dog);
        await Seconds(0.5f, null);
        GameObject actor = Player.gameObject;
        for (int i = 0; i < 4; i++)
            manager.Whistled(dog, actor);
        bool annoyed = dog.State.Annoyed(Time.time) && dog.State.CallAnnoyed;
        manager.Whistled(dog, actor);
        bool stillAnnoyed = dog.State.Annoyed(Time.time);
        manager.Pet(dog);
        bool forgiven = !dog.State.Annoyed(Time.time);
        await Seconds(1f, null);
        return $"{Pass(annoyed && stillAnnoyed && forgiven)} whistle spam: annoyed {annoyed}, ignored the next {stillAnnoyed}, forgiven by a pet {forgiven}; {Line(dog)}";
    }

    // Aimed at a robot, the robot answers the whistle, not the dog.
    public static async Task<string> WhistleRobot(string robotName)
    {
        Robot robot = Object.FindObjectsByType<Robot>().First(r => r.name == robotName);
        Vector3 from = Ground(robot.transform.position + robot.transform.forward * 8f);
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.5f, () => AimAt(robot.Head.position));
        var target = WhistleTick();
        await Seconds(1f, null);
        robot.Call.End();
        return $"{Pass(ReferenceEquals(target, robot))} whistle at {robotName}: answered by {(target as Component)?.name ?? "nobody"}";
    }

    // Hold E: it comes in front of the player, happy, then sits.
    public static async Task<string> Pet()
    {
        Dog dog = Dog;
        Vector3 at = Yard(6f, 6f);
        dog.Legs.Warp(Yard(2f, 6f));
        PlacePlayer(at, dog.transform.position);
        await Seconds(1f, null);
        dog.InteractHold(Player.gameObject);
        bool petting = dog.State.Petting(Time.time);
        bool sat = false;
        float closest = float.MaxValue;
        await Seconds(9f, () =>
        {
            sat |= dog.Pose == DogPose.Sit;
            closest = Mathf.Min(closest, Flat(dog.transform.position - Player.transform.position));
        });
        return $"{Pass(petting && sat && closest < 2.2f)} pet: petting {petting}, came to {closest:0.0} m, sat {sat}; {Line(dog)}";
    }

    // Looked at from 4 m for 1.5 s while it stands: a head tilt.
    public static async Task<string> HeadTilt()
    {
        Dog dog = Dog;
        DogManager manager = Scope.Resolve<DogManager>();
        manager.Order(dog, DogOrder.Follow);
        dog.SetPose(DogPose.Stand);
        await Seconds(2.5f, null);
        Vector3 at = Ground(dog.transform.position + dog.transform.forward * 4f);
        PlacePlayer(at, dog.transform.position + Vector3.up * 0.5f);
        bool tilted = false;
        var animator = dog.GetComponentInChildren<Animator>();
        int tilt = Animator.StringToHash("Act HeadTilt");
        DogRoster roster = Scope.Resolve<DogRoster>();
        await Seconds(3f, () =>
        {
            // A player who just stopped to look (one standing still for long has a dog sitting down, which can't tilt).
            roster.PlayerStillFor = 0f;
            AimAt(dog.transform.position + Vector3.up * 0.5f);
            tilted |= animator.GetCurrentAnimatorStateInfo(0).shortNameHash == tilt || animator.GetNextAnimatorStateInfo(0).shortNameHash == tilt;
        });
        return $"{Pass(tilted)} head tilt when looked at: {tilted}; {Line(dog)}";
    }

    // Tap E: the wheel opens slowing time; picking Stay closes it, gives time and input back, and the dog stays.
    public static async Task<string> Wheel()
    {
        Dog dog = Dog;
        var wheel = Scope.Resolve<CommandWheelPresenter>();
        var input = Scope.Resolve<MB13.Aipocalypse.Input.InputService>();
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        dog.Interact(Player.gameObject);
        await Seconds(0.3f, null);
        bool open = wheel.IsOpen;
        float scale = Time.timeScale;
        bool ui = input.IsUIRequested;
        bool picked = wheel.Pick(1);
        await Seconds(0.3f, null);
        bool ok = open && scale < 0.5f && ui && picked && !wheel.IsOpen && Mathf.Approximately(Time.timeScale, 1f) && !input.IsUIRequested
                  && dog.State.Order == DogOrder.Stay;
        return $"{Pass(ok)} wheel: opened {open} (time {scale:0.00}, UI {ui}), picked Stay {picked}, closed {!wheel.IsOpen}, time {Time.timeScale:0.00}, " +
               $"UI {input.IsUIRequested}, order {dog.State.Order}";
    }

    // The wheel open when a raid starts: it closes and gives everything back.
    public static async Task<string> WheelInterrupted()
    {
        Dog dog = Dog;
        var wheel = Scope.Resolve<CommandWheelPresenter>();
        var input = Scope.Resolve<MB13.Aipocalypse.Input.InputService>();
        dog.Interact(Player.gameObject);
        await Seconds(0.3f, null);
        bool open = wheel.IsOpen;
        Scope.Resolve<IPublisher<RaidStarted>>().Publish(default);
        await Seconds(0.3f, null);
        bool released = !wheel.IsOpen && Mathf.Approximately(Time.timeScale, 1f) && !input.IsUIRequested;
        Scope.Resolve<IPublisher<RaidEnded>>().Publish(new RaidEnded(default, RaidEndReason.Stopped));
        return $"{Pass(open && released)} wheel interrupted by a raid: opened {open}, released {released}";
    }

    // At home at 23:00 it goes into its dog house and sleeps; at 07:00 it comes out.
    public static async Task<string> NightHouse()
    {
        Dog dog = Dog;
        var clock = Scope.Resolve<WorldClock>();
        DogRoster roster = Scope.Resolve<DogRoster>();
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Stay);
        PlacePlayer(Ground(Yard(-14f, -10f)), roster.House.transform.position);
        clock.JumpTo(23f);
        await Seconds(30f, null);
        bool inside = dog.InHouse && Flat(dog.transform.Find("Model").position - roster.House.Bed) < 0.3f;
        DogPose pose = dog.Pose;
        clock.JumpTo(7f);
        await Seconds(8f, null);
        bool out_ = !dog.InHouse;
        return $"{Pass(inside && pose == DogPose.Sleep && out_)} night: in its house {inside}, pose {pose}; morning out {out_}; {Line(dog)}";
    }

    // The player shoots it: a flinch and (rate-limited) yelp, no damage event.
    public static async Task<string> FriendlyFire()
    {
        Dog dog = Dog;
        int dealt = 0;
        using IDisposable sub = Scope.Resolve<ISubscriber<DamageDealt>>().Subscribe(_ => dealt++);
        var info = new DamageInfo(30f, DamageType.Gunshot, dog.transform.position + dog.transform.forward * 0.3f + Vector3.up * 0.5f,
            dog.transform.forward * -5f, Player.gameObject);
        DamageResult result = Scope.Resolve<DamageService>().Deal(dog, info);
        var animator = dog.GetComponentInChildren<Animator>();
        bool flinched = false;
        await Seconds(0.6f, () => flinched |= animator.GetCurrentAnimatorStateInfo(0).IsName("Hit Front_Left") || animator.GetCurrentAnimatorStateInfo(0).IsName("Hit Front_Right"));
        return $"{Pass(result.Applied == 0f && dealt == 0 && flinched)} friendly fire: applied {result.Applied}, damage events {dealt}, flinched {flinched}";
    }

    // Save, move it and change its order, load: it's back where it was with the same order.
    public static async Task<string> AfterLoad()
    {
        Dog dog = Dog;
        DogManager manager = Scope.Resolve<DogManager>();
        manager.Order(dog, DogOrder.Stay);
        await Seconds(1f, null);
        var section = Scope.Resolve<SaveRegistry>().Sections.OfType<DogSaveSection>().First();
        var saves = Scope.Resolve<SaveService>();
        SaveData data = saves.Create();
        section.Capture(data);
        Vector3 saved = dog.transform.position;
        dog.Legs.Warp(saved + YardRight * 6f);
        manager.Order(dog, DogOrder.Follow);
        section.Restore(data);
        await Seconds(0.5f, null);
        float off = Flat(dog.transform.position - saved);
        return $"{Pass(off < 0.6f && dog.State.Order == DogOrder.Stay)} after load: {off:0.00} m from where it was saved, order {dog.State.Order}";
    }

    // Finds a wadeable stream spot (0.6–1.3 m deep, on the navmesh) within `radius` m of the yard: the dog there swims
    // (the model floating up), and shakes itself dry back on land.
    public static async Task<string> Swim(float radius)
    {
        Dog dog = Dog;
        var water = Scope.Resolve<MB13.Aipocalypse.Core.SpawnSystem.IWaterLevel>();
        Vector3 center = Yard(0f, 0f);
        Vector3 spot = default;
        bool found = false;
        for (float r = 20f; r <= radius && !found; r += 5f)
        for (float a = 0f; a < 360f && !found; a += 2.5f / r * Mathf.Rad2Deg)
        {
            Vector3 p = center + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
            Vector3 ground = Ground(p);
            if (!water.TryGetLevel(ground, out float level) || level - ground.y < 0.6f || level - ground.y > 1.3f)
                continue;
            var nearest = AstarPath.active.GetNearest(ground, NearestNodeConstraint.Walkable);
            if (nearest.node == null || Vector3.Distance(nearest.position, ground) > 0.5f)
                continue;
            spot = nearest.position;
            found = true;
        }
        if (!found)
            return $"FAIL swim: no wadeable stream spot within {radius:0} m";
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        PlacePlayer(spot, spot + YardRight);
        dog.Legs.Warp(spot + YardRight * 1.5f);
        bool swam = false;
        float lift = 0f;
        await Seconds(4f, () =>
        {
            swam |= dog.Swimming;
            lift = Mathf.Max(lift, dog.transform.Find("Model").localPosition.y);
        });
        Vector3 dry = spot;
        for (int i = 1; i <= 12; i++)
        {
            Vector3 p = Ground(spot + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * 14f);
            if (!water.TryGetLevel(p, out float level) || level - p.y < 0.1f)
            {
                dry = p;
                break;
            }
        }
        PlacePlayer(dry, dry + YardRight);
        var animator = dog.GetComponentInChildren<Animator>();
        bool shook = false;
        await Seconds(20f, () => shook |= animator.GetCurrentAnimatorStateInfo(0).IsName("Act ShakeWater"));
        return $"{Pass(swam && lift > 0.1f && shook)} swim at {spot}: swam {swam} (model up {lift:0.00} m), shook dry {shook}, swimming now {dog.Swimming}; {Line(dog)}";
    }

    // ---- Fighting (Phase 2) ----

    // Clears enemies, puts the player (invulnerable) and the dog in the open field north of the gate, dog following.
    static async Task<Dog> Arena(DogStance stance)
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Player.Invulnerable = true;
        Vector3 at = Ground(Yard(-3f, 42f));
        PlacePlayer(at, at + YardRight * 10f);
        dog.Legs.Warp(at - YardRight * 2f);
        dog.State.ResetToughness(Scope.Resolve<DogManager>().Stats);
        dog.State.SetStance(stance);
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        dog.SetPose(DogPose.Stand);
        await Seconds(2.5f, null);
        return dog;
    }

    static void Console(string line) => Scope.Resolve<MB13.Aipocalypse.Core.Debugging.ConsoleService>().Run(line);

    static MB13.Aipocalypse.Core.EnemySystem.EnemyBrain[] Goblins() =>
        Object.FindObjectsByType<MB13.Aipocalypse.Core.EnemySystem.EnemyBrain>().Where(b => !b.IsDead && b.isActiveAndEnabled).ToArray();

    // An aware goblin near the player: the dog circles, bites within cooldown × 1.5 + a few s, gets out to >= 3.5 m; the
    // goblin only goes for the dog inside its window and reach (never a chase).
    public static async Task<string> Skirmish()
    {
        Dog dog = await Arena(DogStance.Active);
        Console("enemy.spawn goblin 1");
        await Seconds(1f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL skirmish: no goblin spawned";
        goblin.Mover.Teleport(Ground(Player.transform.position + YardRight * 6f));
        goblin.Notice(true);
        float firstBite = -1f, escapedTo = 0f, chaseAt = -1f;
        float bitAt = -1f;
        float t0 = Time.time;
        float ready = dog.State.BiteReadyAt;
        await Seconds(16f, () =>
        {
            if (goblin == null || goblin.IsDead)
                return;
            float d = Flat(goblin.transform.position - dog.transform.position);
            if (dog.State.BiteReadyAt != ready)
            {
                ready = dog.State.BiteReadyAt;
                bitAt = Time.time;
                if (firstBite < 0f)
                    firstBite = Time.time - t0;
            }
            if (bitAt > 0f && Time.time - bitAt < 2.5f)
                escapedTo = Mathf.Max(escapedTo, d);
            var melee = goblin.Definition.Melee;
            Vector3 toDog = dog.transform.position - goblin.transform.position;
            toDog.y = 0f;
            bool movingAtDog = Vector3.Dot(goblin.Mover.Velocity, toDog.normalized) > 0.8f;
            if (ReferenceEquals(goblin.Target, dog) && melee != null && d > melee.AttackRange + 1.5f && movingAtDog && chaseAt < 0f)
                chaseAt = Time.time - t0;
        });
        bool ok = firstBite >= 0f && firstBite <= 8f * 1.5f + 4f && escapedTo >= 3.5f && chaseAt < 0f;
        return $"{Pass(ok)} skirmish: first bite after {firstBite:0.0} s, got out to {escapedTo:0.0} m, goblin chased the dog at {chaseAt:0.0} s (want never), " +
               $"goblin health {(goblin != null && goblin.Body != null ? goblin.Body.Health.Normalized : 0f):0.00}; {Line(dog)}";
    }

    sealed class Always : IDogUpgradeSource
    {
        public float Modifier(DogStat stat) => stat == DogStat.TakedownChanceBase ? 1f : 0f;
    }

    // A takedown forced (chance 1): a bite drags the goblin down; the dog pins it.
    public static async Task<string> Takedown()
    {
        Dog dog = await Arena(DogStance.Active);
        var stats = Scope.Resolve<DogManager>().Stats;
        var always = new Always();
        stats.Add(always);
        try
        {
            Console("enemy.spawn goblin 1");
            await Seconds(1f, null);
            var goblin = Goblins().FirstOrDefault();
            if (goblin == null)
                return "FAIL takedown: no goblin";
            goblin.Mover.Teleport(Ground(Player.transform.position + YardRight * 6f));
        goblin.Notice(true);
            var animator = dog.GetComponentInChildren<Animator>();
            bool pinned = false, pinFlag = false, bit = false;
            float startReady = dog.State.BiteReadyAt;
            float diedAt = -1f;
            float t0 = Time.time;
            await Seconds(18f, () =>
            {
                pinned |= animator.GetCurrentAnimatorStateInfo(0).IsName("Pin");
                pinFlag |= dog.Pinning;
                bit |= dog.State.BiteReadyAt > 0f && Time.time - t0 > 0.5f && dog.State.BiteReadyAt != startReady;
                if (diedAt < 0f && (goblin == null || goblin.IsDead))
                    diedAt = Time.time - t0;
            });
            return $"{Pass(diedAt >= 0f && pinned)} takedown: goblin dragged down after {diedAt:0.0} s, pinned {pinned} (flag {pinFlag}, bit {bit}); {Line(dog)}";
        }
        finally
        {
            stats.Remove(always);
        }
    }

    // Passive: it doesn't fight; a whistle aimed at the goblin sends it, and it bites.
    public static async Task<string> Sic()
    {
        Dog dog = await Arena(DogStance.Passive);
        Console("enemy.spawn goblin 1");
        await Seconds(0.5f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL sic: no goblin";
        goblin.Mover.Teleport(Ground(Player.transform.position + YardRight * 6f));
        goblin.Notice(true);
        float ready = dog.State.BiteReadyAt;
        await Seconds(5f, null);
        bool bitWhilePassive = dog.State.BiteReadyAt != ready;
        Vector3 aimAt = goblin.transform.position + Vector3.up * 1f;
        AimAt(aimAt);
        await Seconds(0.2f, () => AimAt(aimAt));
        var target = WhistleTick();
        bool bit = false;
        await Seconds(14f, () => bit |= dog.State.BiteReadyAt != ready);
        return $"{Pass(!bitWhilePassive && ReferenceEquals(target, dog) && bit)} sic: passive bit on its own {bitWhilePassive}, whistle answered by the dog {ReferenceEquals(target, dog)}, bit when sent {bit}; {Line(dog)}";
    }

    // A plain whistle in a fight: it backs off to the player and doesn't bite until the fight is over.
    public static async Task<string> BackOff()
    {
        Dog dog = await Arena(DogStance.Active);
        Console("enemy.spawn goblin 1");
        await Seconds(0.5f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL back off: no goblin";
        goblin.Mover.Teleport(Ground(Player.transform.position + YardRight * 6f));
        goblin.Notice(true);
        await Seconds(3f, null);
        Vector3 sky = Player.transform.position + Vector3.up * 40f + YardRight;
        AimAt(sky);
        WhistleTick();
        float ready = dog.State.BiteReadyAt;
        float farthest = 0f;
        await Seconds(8f, () => farthest = Mathf.Max(farthest, Flat(dog.transform.position - Player.transform.position)));
        bool bit = dog.State.BiteReadyAt != ready;
        Console("enemy.clear");
        return $"{Pass(!bit && farthest < 8f)} back off: bit after the whistle {bit}, farthest from the player {farthest:0.0} m; {Line(dog)}";
    }

    // Toughness 0: down, nobody may target it; hold E: up at 30% and limping.
    public static async Task<string> Down()
    {
        Dog dog = await Arena(DogStance.Active);
        DogManager manager = Scope.Resolve<DogManager>();
        dog.State.Hurt(1000f, Time.time, manager.Stats);
        await Seconds(2f, null);
        bool down = dog.State.Down && !dog.IsTargetable;
        var animator = dog.GetComponentInChildren<Animator>();
        bool lying = animator.GetCurrentAnimatorStateInfo(0).IsName("Down Left") || animator.GetCurrentAnimatorStateInfo(0).IsName("Down Right");
        bool prompt = dog.TryGetHoldPrompt(Player.gameObject, out var hold);
        dog.InteractHold(Player.gameObject);
        await Seconds(2.5f, null);
        float toughness = dog.State.Toughness;
        bool limping = dog.State.Limping(Time.time);
        return $"{Pass(down && lying && prompt && !dog.State.Down && limping && Mathf.Abs(toughness - 30f) < 2f)} down: down and untargetable {down}, lying {lying}, " +
               $"prompt \"{hold.Verb} {hold.Target}\", helped up {!dog.State.Down}, toughness {toughness:0}, limping {limping}; {Line(dog)}";
    }

    // An unaware goblin near the dog (no fight on): it faces it and growls.
    public static async Task<string> Warn()
    {
        Dog dog = await Arena(DogStance.Active);
        // Unseen by enemies (cheat.notarget): the goblin stays unaware of the player.
        Player.IgnoredByEnemies = true;
        Console("enemy.spawn goblin 1");
        await Seconds(0.3f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL warn: no goblin";
        goblin.Mover.Teleport(Player.transform.position + YardRight * 18f);
        var animator = dog.GetComponentInChildren<Animator>();
        bool growled = false, bit = false;
        float ready = dog.State.BiteReadyAt;
        DogManager wm = Scope.Resolve<DogManager>();
        string trace = "";
        int frame = 0;
        await Seconds(7f, () =>
        {
            if (frame++ % 60 == 0)
                trace += $" [warn {wm.WarnId} target {wm.TargetId} infight {wm.InFight} foes {wm.Foes.Count} alerted {goblin.Alerted} pose {dog.Pose}]";
            growled |= animator.GetCurrentAnimatorStateInfo(0).IsName("Act Growl") || animator.GetCurrentAnimatorStateInfo(0).IsName("Combat");
            bit |= dog.State.BiteReadyAt != ready;
        });
        Console("enemy.clear");
        Player.IgnoredByEnemies = false;
        return $"{Pass(growled && !bit)} warn: growled {growled}, bit an unaware goblin {bit}; {Line(dog)}{trace}";
    }

    public static async Task<string> FightDebug()
    {
        Dog dog = await Arena(DogStance.Active);
        DogManager m = Scope.Resolve<DogManager>();
        Console("enemy.spawn goblin 1");
        await Seconds(1f, null);
        var goblin = Goblins().FirstOrDefault();
        var sb = new StringBuilder();
        sb.AppendLine($"goblins {Goblins().Length} enemies registry {Scope.Resolve<MB13.Aipocalypse.Core.EnemySystem.EnemyRegistry>().Count} player {Player.transform.position} dog {dog.transform.position} goblin {goblin?.transform.position}");
        if (goblin != null)
        {
            goblin.Mover.Teleport(Ground(Player.transform.position + YardRight * 6f));
            goblin.Notice(true);
        }
        for (int i = 0; i < 6; i++)
        {
            await Seconds(1f, null);
            sb.AppendLine($"t{i}: foes {m.Foes.Count} target {m.TargetId} infight {m.InFight} hurt {m.PlayerHurt} alerted {goblin?.Alerted} gd {(goblin != null ? Flat(goblin.transform.position - dog.transform.position) : -1f):0.0} pd {(goblin != null ? Flat(goblin.transform.position - Player.transform.position) : -1f):0.0} hasPlayer {dog.Roster.HasPlayer} retreat {dog.State.Retreating(m.Stats)} limp {dog.State.Limping(Time.time)} stance {dog.State.Stance}");
        }
        return sb.ToString();
    }

    public static async Task<string> TakedownDebug()
    {
        Dog dog = await Arena(DogStance.Passive);
        DogManager m = Scope.Resolve<DogManager>();
        var always = new Always();
        m.Stats.Add(always);
        Console("enemy.spawn goblin 1");
        await Seconds(1f, null);
        var goblin = Goblins().FirstOrDefault();
        goblin.Mover.Teleport(Ground(dog.transform.position + YardRight * 1.2f));
        await Seconds(0.5f, null);
        string before = $"chance {DogState.TakedownChance(1f, m.Stats):0.00} maxClass {m.Stats.GetClass(DogStat.MaxTakedownClass)} foes {m.Foes.Count}";
        int id = m.Foes.Count > 0 ? m.Foes[0].Id : -1;
        int cls = m.Foes.Count > 0 ? m.Foes[0].Class : -1;
        bool result = m.Bite(dog, id);
        await Seconds(0.3f, null);
        m.Stats.Remove(always);
        return $"{before} id {id} class {cls} bite→takedown {result} goblin dead {goblin == null || goblin.IsDead}";
    }

    // Renders the player camera to a PNG (outside Assets).
    public static string Shot(string png)
    {
        Camera camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = previous;
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes(png, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        target.Release();
        return png;
    }

    // Stands the player at a yard point looking at the dog.
    public static string LookAtDog(float x, float z)
    {
        Vector3 at = Ground(Yard(x, z));
        PlacePlayer(at, Dog.transform.position + Vector3.up * 0.4f);
        return Line(Dog);
    }

    static Component WhistleTick()
    {
        object whistle = typeof(Player).GetField("_whistle", Private).GetValue(Player);
        return whistle.GetType().GetMethod("Tick").Invoke(whistle, new object[] { true, Time.time + 100f }) as Component;
    }

    static string Pass(bool ok) => ok ? "PASS" : "FAIL";

    static string Line(Dog dog) => $"[{dog.State.Order}, plan {dog.Plan.Mode}, pose {dog.Pose}, at {dog.transform.position}, " +
                                   $"{Flat(dog.transform.position - Player.transform.position):0.0} m from the player]";

    static Vector3 Yard(float x, float z)
    {
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        return Ground(yard.WorldToLocal.inverse.MultiplyPoint3x4(new Vector3(x, 0f, z)));
    }

    static Vector3 YardRight => Object.FindAnyObjectByType<RobotYard>().WorldToLocal.inverse.MultiplyVector(Vector3.right).normalized;

    static void PlacePlayer(Vector3 at, Vector3 lookAt)
    {
        Player player = Player;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = at + Vector3.up * 0.1f;
        controller.enabled = true;
        AimAt(lookAt);
    }

    static void AimAt(Vector3 point)
    {
        Player player = Player;
        Vector3 eye = Camera.main.transform.position;
        Vector3 flat = Vector3.ProjectOnPlane(point - eye, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f)
            return;
        player.transform.rotation = Quaternion.LookRotation(flat);
        object look = typeof(Player).GetField("_look", Private).GetValue(player);
        float pitch = -Mathf.Atan2(point.y - eye.y, flat.magnitude) * Mathf.Rad2Deg;
        look.GetType().GetMethod("ResetPitch").Invoke(look, new object[] { pitch });
    }

    static Vector3 Ground(Vector3 point)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 30f, Vector3.down, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (hit.collider is TerrainCollider)
                return hit.point;
        return point;
    }

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    static Dog Dog => Object.FindAnyObjectByType<Dog>();
    static Player Player => Object.FindObjectsByType<Player>().First();
    static IObjectResolver Scope => Object.FindAnyObjectByType<GameLifetimeScope>().Container;

    // Steps on EditorApplication.update (an await continuation would run inside the PlayerLoop).
    static Task Seconds(float seconds, Action everyFrame)
    {
        var done = new TaskCompletionSource<bool>();
        float end = Time.realtimeSinceStartup + seconds;
        int frame = -1;
        void Tick()
        {
            if (Time.frameCount == frame)
                return;
            frame = Time.frameCount;
            everyFrame?.Invoke();
            if (Time.realtimeSinceStartup < end)
                return;
            EditorApplication.update -= Tick;
            done.TrySetResult(true);
        }
        EditorApplication.update += Tick;
        return done.Task;
    }
}
