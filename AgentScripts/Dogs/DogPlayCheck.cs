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

    // The player walks `metres` across the yard and stops (20 m; 12 m ends under the house floor, Ground() keeps terrain
    // hits only): the dog keeps within the zone, isn't stopped on the frame the
    // player stops, and settles (sits) within 7.5 s of the stop.
    public static async Task<string> Follow(float metres)
    {
        Dog dog = Dog;
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        Vector3 start = Yard(-10f, -2f);
        PlacePlayer(start, start + YardRight);
        dog.Legs.Warp(Yard(-11f, 0f));
        // On its feet first (a previous probe may have left it lying down: getting up, and the happy wag the order asked
        // for, take a few seconds); a fixed wait let it settle again next to the still player.
        dog.SetPose(DogPose.Stand);
        await Seconds(1f, null);
        for (int i = 0; i < 50 && !dog.Standing; i++)
            await Seconds(0.2f, null);
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
        // Getting up from a sit and a gait at a time to the sprint take a moment.
        bool ok = caughtAt >= 0f && caughtAt <= 6f;
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

    // Across a fallen (unbuilt) segment the dog walks through, never scrambling; across a standing one far from any gap it
    // still scrambles. Picks the crossings itself (a new game: the front half of the fence is debris).
    public static async Task<string> Gap()
    {
        Dog dog = Dog;
        DogRoster roster = Scope.Resolve<DogRoster>();
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Follow);
        DogLinkInfo gap = roster.Crossings.FirstOrDefault(c => !c.Standing);
        if (gap == null)
            return "FAIL gap: no fallen segment in this game (start a new game)";
        float detour = roster.Settings.GapDetour + 8f;
        DogLinkInfo wall = roster.Crossings.Where(c => c.Standing)
            .FirstOrDefault(c => roster.Crossings.Where(g => !g.Standing).All(g => Flat(g.Inside - c.Inside) > detour));
        var sb = new StringBuilder();
        bool ok = true;
        foreach ((DogLinkInfo crossing, bool walk) in new[] { (gap, true), (wall, false) })
        {
            if (crossing == null)
            {
                sb.Append("no standing segment far from a gap; ");
                continue;
            }
            Vector3 across = (crossing.Outside - crossing.Inside).normalized;
            Vector3 inside = Ground(crossing.Inside - across * 5f);
            Vector3 beyond = Ground(crossing.Outside + across * 6f);
            PlacePlayer(inside, beyond);
            dog.Legs.Warp(Ground(crossing.Inside - across * 3f));
            await Seconds(2f, null);
            PlacePlayer(beyond, inside);
            bool scrambled = false;
            await Seconds(12f, () => scrambled |= dog.Scrambling);
            bool outside = !yard.Contains(dog.transform.position);
            bool pass = outside && scrambled != walk;
            ok &= pass;
            sb.Append($"{crossing.name} ({(walk ? "fallen" : "standing")}): out {outside}, scrambled {scrambled}, " +
                      $"{Flat(dog.transform.position - beyond):0.0} m from the player {(pass ? "ok" : "WRONG")}; ");
        }
        return $"{Pass(ok)} gap: {sb}{Line(dog)}";
    }

    // The body rig (DogBody), its brain held: the head turns toward the player standing off to its side; paws held planted
    // through a sit-down don't move (the clip alone slides them about 12 / 16 / 54 / 36 cm at 2×, front L / R, hind L / R:
    // the hind ones step over instead); set panting, the jaw opens. Ends standing, panting, the player in front of its face (for a shot).
    public static async Task<string> Body()
    {
        Dog dog = Dog;
        var body = dog.GetComponentInChildren<DogBody>();
        Transform[] bones = dog.GetComponentsInChildren<Transform>();
        Transform Bone(string name) => bones.First(b => b.name == name);
        Transform head = Bone("Head"), jaw = Bone("Jaw");
        Transform[] paws = { Bone("L Finger0"), Bone("R Finger0"), Bone("L Toe0"), Bone("R Toe0") };
        dog.NextThinkAt = float.MaxValue;
        dog.Legs.Halt();
        dog.SetPose(DogPose.Stand);
        for (int i = 0; i < 40 && !dog.Standing; i++)
            await Seconds(0.25f, null);
        Vector3 at = Yard(-10f, 2f);
        dog.Legs.Warp(at);
        dog.transform.rotation = Quaternion.LookRotation(YardRight);
        // The player 5 m off its left front, then its right front: the head's yaw against the body's differs between them.
        float HeadYaw() => Vector3.SignedAngle(Vector3.ProjectOnPlane(dog.transform.forward, Vector3.up), Vector3.ProjectOnPlane(head.up, Vector3.up), Vector3.up);
        PlacePlayer(Ground(at + Quaternion.AngleAxis(-55f, Vector3.up) * YardRight * 5f), at);
        await Seconds(2f, null);
        float left = HeadYaw();
        PlacePlayer(Ground(at + Quaternion.AngleAxis(55f, Vector3.up) * YardRight * 5f), at);
        await Seconds(2f, null);
        float right = HeadYaw();
        bool looks = right - left > 30f;

        var slide = new float[paws.Length];
        var last = paws.Select(p => p.position).ToArray();
        // Slide: a paw moving while held planted (a step lifts it over on purpose and is counted apart).
        FieldInfo plantField = typeof(DogBody).GetField("_plant", Private);
        var steps = new int[paws.Length];
        var wasStepping = new bool[paws.Length];
        dog.SetPose(DogPose.Sit);
        await Seconds(3f, () =>
        {
            var plant = (Array)plantField.GetValue(body);
            for (int i = 0; i < paws.Length; i++)
            {
                Vector3 p = paws[i].position;
                string state = plant.GetValue(i).ToString();
                if (state == "Held")
                    slide[i] += Flat(p - last[i]);
                bool stepping = state == "Stepping";
                if (stepping && !wasStepping[i])
                    steps[i]++;
                wasStepping[i] = stepping;
                last[i] = p;
            }
        });
        bool planted = slide.All(s => s < 0.05f);
        dog.SetPose(DogPose.Stand);
        for (int i = 0; i < 40 && !dog.Standing; i++)
            await Seconds(0.25f, null);
        await Seconds(0.5f, null);

        var pant = (DogPant)typeof(DogBody).GetField("_pant", Private).GetValue(body);
        FieldInfo until = typeof(DogPant).GetField("_until", Private);
        until.SetValue(pant, float.NegativeInfinity);
        await Seconds(1.5f, null);
        Quaternion rest = jaw.localRotation;
        until.SetValue(pant, Time.time + 20f);
        float open = 0f;
        await Seconds(2.5f, () => open = Mathf.Max(open, Quaternion.Angle(rest, jaw.localRotation)));
        bool pants = open > 8f;
        Vector3 face = Ground(dog.transform.position + dog.transform.forward * 2.6f);
        PlacePlayer(face, head.position);
        dog.NextThinkAt = 0f;
        return $"{Pass(looks && planted && pants)} body: head yaw {left:0}° with the player left, {right:0}° right; sit-down: held paws slid " +
               string.Join(" / ", slide.Select(s => (s * 100f).ToString("0.0"))) + " cm, steps " + string.Join(" / ", steps) + $"; jaw opens {open:0}° panting; {Line(dog)}";
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
            // It sits CallDistance in front of the player and waits for them to come.
            if (arrivedAt < 0f && dog.Pose == DogPose.Sit && Flat(dog.transform.position - Player.transform.position) < dog.Settings.CallDistance + 0.7f)
                arrivedAt = Time.time - t0;
        });
        float gap = Flat(dog.transform.position - Player.transform.position);
        bool ok = answered && arrivedAt >= 0f && gap > dog.Settings.CallDistance - 0.8f && dog.State.Order == DogOrder.Follow;
        return $"{Pass(ok)} whistle from {metres:0} m: answered {answered}, sat in front after {arrivedAt:0.0} s at {gap:0.0} m, order {dog.State.Order}; {Line(dog)}";
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
        // It stops PetDistance in front (the player walks the last bit).
        return $"{Pass(petting && sat && closest < dog.Settings.PetDistance + 0.7f)} pet: petting {petting}, came to {closest:0.0} m, sat {sat}; {Line(dog)}";
    }

    // Looked at from 4 m for 1.5 s while it stands: a head tilt.
    public static async Task<string> HeadTilt()
    {
        Dog dog = Dog;
        // Daytime, no enemies, the player alive (a real raid at 21:00 kills an idle player, and a dead one isn't looked for).
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        Player.Invulnerable = true;
        DogManager manager = Scope.Resolve<DogManager>();
        manager.Order(dog, DogOrder.Follow);
        dog.SetPose(DogPose.Stand);
        Vector3 at = Ground(dog.transform.position + dog.transform.forward * 4f);
        PlacePlayer(at, dog.transform.position + Vector3.up * 0.5f);
        bool tilted = false;
        var animator = dog.GetComponentInChildren<Animator>();
        int tilt = Animator.StringToHash("Act HeadTilt");
        DogRoster roster = Scope.Resolve<DogRoster>();
        // Looking away until it's up (from a nap) and standing still at its spot by the player; then the player "just
        // stopped" and looks at it (one standing still for long has a dog sitting, which can't tilt).
        float ready = -1f;
        for (int i = 0; i < 60 && ready < 0f; i++)
        {
            AimAt(at + YardRight * 5f + Vector3.up * 1.5f);
            await Seconds(0.2f, null);
            if (dog.Standing && dog.Pose == DogPose.Stand && Flat(dog.Legs.Velocity) < 0.3f && Flat(dog.transform.position - at) < 5f)
                ready = i * 0.2f;
        }
        roster.PlayerStillFor = 0f;
        await Seconds(4f, () =>
        {
            AimAt(dog.transform.position + Vector3.up * 0.5f);
            tilted |= animator.GetCurrentAnimatorStateInfo(0).shortNameHash == tilt || animator.GetNextAnimatorStateInfo(0).shortNameHash == tilt;
        });
        return $"{Pass(tilted)} head tilt when looked at: {tilted} (standing still after {ready:0.0} s); {Line(dog)}";
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
        // A quiet night (no raid: with one on it stays up and guards the yard).
        await Seconds(30f, () => { if (Scope.Resolve<DogRoster>().Raid) Console("raid.stop"); });
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
        return $"{Pass(swam && lift > 0f && shook)} swim at {spot}: swam {swam} (model up {lift:0.00} m), shook dry {shook}, swimming now {dog.Swimming}; {Line(dog)}";
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
            // After the pin it must leave the fight and follow again.
            Vector3 from = Player.transform.position;
            PlacePlayer(Ground(from - YardRight * 10f), Ground(from - YardRight * 20f));
            await Seconds(0.2f, null);
            float gap = Flat(dog.transform.position - Player.transform.position);
            // It may have dozed off next to the still player during the fight's wait: getting up takes ~4.5 s.
            await Seconds(10f, null);
            float after = Flat(dog.transform.position - Player.transform.position);
            // Closing the gap (hurt, it may limp and come slower); stuck in the fight branch it doesn't move.
            bool follows = after < 8.5f || after < gap - 2f;
            return $"{Pass(diedAt >= 0f && pinned && follows)} takedown: goblin dragged down after {diedAt:0.0} s, pinned {pinned} (flag {pinFlag}, bit {bit}); then followed {follows} ({gap:0.0} > {after:0.0} m, limping {dog.State.Limping(Time.time)}, bite landed {dog.BiteLanded}); {Line(dog)}";
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
        return $"{Pass(!bitWhilePassive && target != null && bit)} sic: passive bit on its own {bitWhilePassive}, whistle answered {target != null}, bit when sent {bit}; {Line(dog)}";
    }

    // The aim was on a goblin (the fallback saw it), then moved onto the dog itself: Q calls the dog, it isn't sent.
    public static async Task<string> WhistleAtDogNearEnemy()
    {
        Dog dog = await Arena(DogStance.Passive);
        DogManager manager = Scope.Resolve<DogManager>();
        Console("enemy.spawn goblin 1");
        await Seconds(0.5f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL whistle at dog: no goblin";
        goblin.Mover.Teleport(Ground(Player.transform.position + YardRight * 6f));
        await Seconds(0.3f, null);
        Vector3 eye = Camera.main.transform.position;
        manager.TryAnswer(Player.gameObject, eye, (goblin.transform.position + Vector3.up - eye).normalized, out _);
        bool answered = dog.TryWhistle(Player.gameObject);
        int sent = (int)typeof(DogManager).GetField("_sentId", Private).GetValue(manager);
        bool called = dog.State.Called(Time.time);
        Console("enemy.clear");
        return $"{Pass(answered && called && sent < 0)} whistle at the dog after aiming at a goblin: answered {answered}, called {called}, sent at {sent} (want -1); {Line(dog)}";
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

    // A warning growl cut off by a whistle: the warning ends with it (left on, the next warn faces the map origin).
    public static async Task<string> WarnInterrupted()
    {
        Dog dog = await Arena(DogStance.Active);
        Player.IgnoredByEnemies = true;
        DogManager manager = Scope.Resolve<DogManager>();
        Console("enemy.spawn goblin 1");
        await Seconds(0.3f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL warn interrupted: no goblin";
        goblin.Mover.Teleport(Player.transform.position + YardRight * 18f);
        float giveUp = Time.realtimeSinceStartup + 7f;
        while (!manager.Warning && Time.realtimeSinceStartup < giveUp)
            await Seconds(0.05f, null);
        bool warning = manager.Warning;
        await Seconds(0.3f, null);
        if (!warning)
        {
            Console("enemy.clear");
            Player.IgnoredByEnemies = false;
            return "FAIL warn interrupted: it never started a warning";
        }
        dog.State.Call(Time.time, annoyed: false);
        await Seconds(0.6f, null);
        bool stillWarning = manager.Warning;
        dog.State.EndCall();
        Console("enemy.clear");
        Player.IgnoredByEnemies = false;
        return $"{Pass(!stillWarning)} warn interrupted: warning left on after the whistle cut it {stillWarning}; {Line(dog)}";
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

    // ---- The farm (Phase 3) ----

    // The herd let out, then Herding: it brings them all home (or keeps at it), then stays at home.
    public static async Task<string> Herd(float outFor, float herdFor)
    {
        Dog dog = Dog;
        Console("enemy.clear");
        var herd = Scope.Resolve<MB13.Aipocalypse.Core.AnimalSystem.AnimalHerd>();
        Scope.Resolve<WorldClock>().JumpTo(10f);
        if (!herd.Orders.Released)
            herd.Orders.Bell();
        await Seconds(outFor, null);
        int outBefore = herd.Animals.Count - herd.CountHome();
        DogManager manager = Scope.Resolve<DogManager>();
        manager.BringHerdHome(dog);
        bool herding = dog.State.Order == DogOrder.Herding;
        int minOut = outBefore;
        await Seconds(herdFor, () => minOut = Mathf.Min(minOut, herd.Animals.Count - herd.CountHome()));
        int outAfter = herd.Animals.Count - herd.CountHome();
        bool ok = herding && outBefore > 0 && (outAfter == 0 || outAfter < outBefore);
        return $"{Pass(ok)} herd: out before {outBefore}, herding {herding}, out after {outAfter} (fewest {minOut}), order now {dog.State.Order}; {Line(dog)}";
    }

    // At home near Bolt: Bolt greets it and it goes over to sniff.
    public static async Task<string> RobotGreet()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        Robot bolt = Object.FindObjectsByType<Robot>().First(r => r.name == "Robot_Shop");
        // Told to stay in the yard (home life), not wherever the last probe left it.
        dog.Legs.Warp(Ground(Yard(0f, 0f)));
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Stay);
        await Seconds(7f, null);
        dog.Legs.Warp(Ground(bolt.transform.position + bolt.transform.forward * 2.5f));
        bool greeted = false;
        bool sniffed = false;
        await Seconds(8f, () =>
        {
            greeted |= dog.GreetedBy != null;
            sniffed |= greeted && Flat(dog.transform.position - bolt.transform.position) < 2f;
        });
        return $"{Pass(greeted && sniffed)} robot greet: greeted by Bolt {greeted}, came to sniff {sniffed}; {Line(dog)}";
    }

    // No greeting from a busy robot (chatting), nor while the dog follows the player past a robot.
    public static async Task<string> RobotGreetBusy()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        DogManager manager = Scope.Resolve<DogManager>();
        Robot[] robots = Object.FindObjectsByType<Robot>();
        Robot bolt = robots.First(r => r.name == "Robot_Shop");
        Robot other = robots.First(r => r != bolt);
        manager.Order(dog, DogOrder.Stay);
        await Seconds(7f, null);
        dog.GreetedBy = null;
        bolt.State.Chat(other.transform.position + Vector3.up, leads: true, Time.time);
        dog.Legs.Warp(Ground(bolt.transform.position + bolt.transform.forward * 2.5f));
        bool busyGreeted = false;
        await Seconds(2.5f, () => busyGreeted |= dog.GreetedBy != null);
        PlacePlayer(Ground(other.transform.position + other.transform.forward * 3f), other.transform.position);
        manager.Order(dog, DogOrder.Follow);
        dog.Legs.Warp(Ground(other.transform.position + other.transform.forward * 2f));
        bool followGreeted = false;
        await Seconds(3f, () => followGreeted |= dog.GreetedBy != null);
        return $"{Pass(!busyGreeted && !followGreeted)} robot greet when busy: chatting robot greeted {busyGreeted}, greeted while following {followGreeted}; {Line(dog)}";
    }

    // dog.chase at home: it runs at a sheep, which flees.
    public static async Task<string> SheepChase()
    {
        Dog dog = Dog;
        Scope.Resolve<WorldClock>().JumpTo(10f);
        var herd = Scope.Resolve<MB13.Aipocalypse.Core.AnimalSystem.AnimalHerd>();
        var sheep = herd.Animals.Where(a => a.Definition.name.Contains("Sheep")).ToArray();
        if (sheep.Length == 0)
            return "FAIL sheep chase: no sheep";
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Stay);
        await Seconds(7f, null);
        dog.Legs.Warp(Ground(sheep[0].transform.position + YardRight * 6f));
        Console("dog.chase");
        bool startled = false;
        await Seconds(9f, () => startled |= sheep.Any(a => a.Startled));
        return $"{Pass(startled)} sheep chase: a sheep fled {startled}; {Line(dog)}";
    }

    // A raid with the dog at home: Active, it goes for a raider in the yard; Passive, it keeps away and never bites.
    public static async Task<string> RaidHome(string stance)
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        DogManager manager = Scope.Resolve<DogManager>();
        Player.Invulnerable = true;
        Player.IgnoredByEnemies = true;
        manager.Order(dog, DogOrder.Stay);
        dog.State.SetStance(stance == "passive" ? DogStance.Passive : DogStance.Active);
        dog.State.ResetToughness(manager.Stats);
        Vector3 at = Ground(Yard(-12f, 6f));
        PlacePlayer(at, Ground(Yard(0f, 6f)));
        dog.Legs.Warp(Ground(Yard(-6f, 6f)));
        await Seconds(7f, null);
        Scope.Resolve<IPublisher<RaidStarted>>().Publish(new RaidStarted(1));
        Console("enemy.spawn goblin 1");
        await Seconds(1f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL raid home: no goblin";
        goblin.Mover.Teleport(Ground(dog.transform.position + YardRight * 4f));
        float ready = dog.State.BiteReadyAt;
        bool bit = false;
        float closest = float.MaxValue;
        float far = 0f;
        await Seconds(12f, () =>
        {
            bit |= dog.State.BiteReadyAt != ready;
            if (goblin != null && !goblin.IsDead)
            {
                float d = Flat(goblin.transform.position - dog.transform.position);
                closest = Mathf.Min(closest, d);
                far = Mathf.Max(far, d);
            }
        });
        Scope.Resolve<IPublisher<RaidEnded>>().Publish(new RaidEnded(1, RaidEndReason.Stopped));
        Console("enemy.clear");
        Player.IgnoredByEnemies = false;
        bool ok = stance == "passive" ? !bit && far >= 6f : bit;
        return $"{Pass(ok)} raid at home ({stance}): bit {bit}, closest {closest:0.0} m, farthest {far:0.0} m; {Line(dog)}";
    }

    // A night raid with the dog at home: it stays out of its house, standing, facing the raider outside the fence.
    public static async Task<string> RaidNight(float trace = 0f, bool raider = true)
    {
        Dog dog = Dog;
        Console("enemy.clear");
        DogManager manager = Scope.Resolve<DogManager>();
        Player.Invulnerable = true;
        Player.IgnoredByEnemies = true;
        manager.Order(dog, DogOrder.Stay);
        dog.State.SetStance(DogStance.Active);
        PlacePlayer(Ground(Yard(-12f, 6f)), Ground(Yard(0f, 6f)));
        dog.Legs.Warp(Ground(Yard(-4f, 0f)));
        Scope.Resolve<WorldClock>().JumpTo(21f);
        // Night starts the real raid: stop it (its raiders walk in through the open front), then raise our own.
        await Seconds(1f, null);
        Console("raid.stop");
        Console("enemy.clear");
        Scope.Resolve<IPublisher<RaidStarted>>().Publish(new RaidStarted(1));
        await Seconds(6f, null);
        MB13.Aipocalypse.Core.EnemySystem.EnemyBrain goblin = null;
        if (raider)
        {
            Console("enemy.spawn goblin 1");
            await Seconds(1f, null);
            goblin = Goblins().FirstOrDefault();
            if (goblin == null)
                return "FAIL raid night: no goblin";
            goblin.Mover.Teleport(Ground(Yard(-4f, -22f)));
            // It spawns by the player (in the yard): let the dog drop that fight before measuring.
            await Seconds(2f, null);
        }
        bool everInHouse = false;
        bool fought = false;
        var timeline = new System.Text.StringBuilder();
        float nextMark = Time.time;
        await Seconds(trace > 0f ? trace : 8f, () =>
        {
            everInHouse |= dog.InHouse;
            fought |= manager.TargetId >= 0;
            if (trace > 0f && Time.time >= nextMark)
            {
                nextMark = Time.time + 1f;
                timeline.Append($" [{Time.time:0} {dog.Pose} house {dog.InHouse} night {dog.Roster.Night} raid {dog.Roster.Raid} at {dog.transform.position:F0}]");
            }
        });
        Vector3 to = goblin != null ? goblin.transform.position - dog.transform.position : dog.transform.forward;
        to.y = 0f;
        float facing = Vector3.Angle(dog.transform.forward, to);
        bool inHouse = dog.InHouse;
        DogPose pose = dog.Pose;
        // The front half of the fence starts down: a raider may walk in, and then the dog fights it instead of watching.
        bool raiderIn = goblin != null && dog.Roster.InYard(goblin.transform.position);
        Scope.Resolve<IPublisher<RaidEnded>>().Publish(new RaidEnded(1, RaidEndReason.Stopped));
        Console("enemy.clear");
        Player.IgnoredByEnemies = false;
        bool ok = !everInHouse && pose == DogPose.Stand && (goblin == null || (raiderIn ? fought : facing < 50f));
        return $"{Pass(ok)} raid night: in its house {inHouse} (ever {everInHouse}), pose {pose}, raider in the yard {raiderIn} (fought {fought}), facing it within {facing:0} deg; {Line(dog)}{timeline}";
    }

    // ---- Polish: turning on the spot, rain, welcome home, bedtime ----

    // A raid at home: a raider appears outside the back fence behind it (it only watches those) and it turns on the spot
    // to face it; its legs step round (the animator's Speed comes up while it turns).
    public static async Task<string> TurnInPlace()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        DogManager manager = Scope.Resolve<DogManager>();
        Player.IgnoredByEnemies = true;
        manager.Order(dog, DogOrder.Stay);
        dog.State.SetStance(DogStance.Active);
        PlacePlayer(Ground(Yard(-14f, 10f)), Ground(Yard(0f, 10f)));
        dog.Legs.Warp(Ground(Yard(0f, -11f)));
        Scope.Resolve<IPublisher<RaidStarted>>().Publish(new RaidStarted(1));
        Console("enemy.spawn goblin 1");
        await Seconds(0.5f, null);
        var goblin = Goblins().FirstOrDefault();
        if (goblin == null)
            return "FAIL turn in place: no goblin";
        goblin.Mover.Teleport(Ground(Yard(0f, -45f)));
        // Past the sad reaction to "Stay" (6 s), facing into the yard.
        Vector3 inward = Ground(Yard(0f, 0f));
        await Seconds(8f, () => dog.Legs.Face(inward));
        var animator = dog.GetComponentInChildren<Animator>();
        float startYaw = dog.transform.eulerAngles.y;
        goblin.Mover.Teleport(Ground(Yard(0f, -18f)));
        float maxSpeed = 0f, turned = 0f, moved = 0f;
        Vector3 from = dog.transform.position;
        var trace = new System.Text.StringBuilder();
        float nextMark = 0f;
        await Seconds(2.5f, () =>
        {
            maxSpeed = Mathf.Max(maxSpeed, animator.GetFloat("Speed"));
            turned = Mathf.Max(turned, Mathf.Abs(Mathf.DeltaAngle(startYaw, dog.transform.eulerAngles.y)));
            moved = Mathf.Max(moved, Flat(dog.transform.position - from));
            if (Time.time >= nextMark)
            {
                nextMark = Time.time + 0.5f;
                manager.NearestFoe(dog, out Vector3 nf);
                trace.Append($" [raid {dog.Roster.Raid} home {manager.AtHome(dog)} homelife {dog.InHomeLife} foes {manager.Foes.Count} target {manager.TargetId} yaw {dog.transform.eulerAngles.y:0} toFoe {Quaternion.LookRotation(Vector3.ProjectOnPlane(nf - dog.transform.position, Vector3.up)).eulerAngles.y:0} pose {dog.Pose}]");
            }
        });
        Scope.Resolve<IPublisher<RaidEnded>>().Publish(new RaidEnded(1, RaidEndReason.Stopped));
        Console("enemy.clear");
        Player.IgnoredByEnemies = false;
        bool ok = turned > 90f && moved < 1f && maxSpeed > 0.25f;
        return $"{Pass(ok)} turn in place: turned {turned:0} deg, moved {moved:0.0} m, animator speed up to {maxSpeed:0.00}; {Line(dog)}{trace}";
    }

    // Following in heavy rain long enough to be soaked, then the rain stops: it shakes itself dry (never while still in it).
    public static async Task<string> RainShake()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        var weather = Scope.Resolve<MB13.Aipocalypse.Core.EnvironmentSystem.WeatherService>();
        DogManager manager = Scope.Resolve<DogManager>();
        manager.Order(dog, DogOrder.Follow);
        PlacePlayer(Ground(Yard(-8f, 4f)), Ground(Yard(0f, 4f)));
        dog.Legs.Warp(Ground(Yard(-6f, 4f)));
        var animator = dog.GetComponentInChildren<Animator>();
        bool shookInRain = false, shook = false;
        var trace = new System.Text.StringBuilder();
        try
        {
            // Held (the console's set), so the weather rules don't swap it back.
            Console("weather.set HeavyRain Gale 0");
            var rainedOn = typeof(DogManager).GetField("_rainedOn", Private);
            var wetnessField = typeof(DogManager).GetField("_wetness", Private);
            var wetField = typeof(DogWetness).GetField("_wet", Private);
            float rainMark = 0f;
            await Seconds(24f, () =>
            {
                shookInRain |= animator.GetCurrentAnimatorStateInfo(0).IsName("Act ShakeWater");
                if (Time.time >= rainMark)
                {
                    rainMark = Time.time + 3f;
                    trace.Append($" <rain {weather.Mix.Rain:0.00} sky {weather.Sky} on {rainedOn.GetValue(manager)} wet {wetField.GetValue(wetnessField.GetValue(manager))} at {dog.transform.position:F1}>");
                }
            });
            Console("weather.set Clear Calm 0");
            float nextMark = 0f;
            await Seconds(12f, () =>
            {
                shook |= animator.GetCurrentAnimatorStateInfo(0).IsName("Act ShakeWater");
                if (Time.time >= nextMark)
                {
                    nextMark = Time.time + 1f;
                    trace.Append($" [pending {dog.ShakePending} pose {dog.Pose} standing {dog.Standing} v {dog.Legs.Velocity.magnitude:0.0} homelife {dog.InHomeLife} hurt {manager.PlayerHurt} rain {weather.Mix.Rain:0.00}]");
                }
            });
        }
        finally
        {
            Console("weather.auto");
        }
        return $"{Pass(shook && !shookInRain)} rain shake: shook in the rain {shookInRain}, shook once it stopped {shook}; {Line(dog)}{(shook && !shookInRain ? "" : trace.ToString())}";
    }

    // Behavior Designer may call OnEnd on a task that isn't running (a later abort's interrupted pass): it must change nothing.
    public static string SpuriousEnd()
    {
        Dog dog = Dog;
        var sb = new StringBuilder();
        bool ok = true;
        FieldInfo sniffing = typeof(Dog).GetField("_sniffing", Private);
        float speed = dog.SpeedOverride;
        bool homeLife = dog.InHomeLife, escaping = dog.Escaping, sniff = (bool)sniffing.GetValue(dog);
        void Check(Type type, Action set, Func<bool> intact, string what)
        {
            object task = Activator.CreateInstance(type);
            type.GetField("_dog", Private).SetValue(task, dog);
            set();
            type.GetMethod("OnEnd").Invoke(task, null);
            bool kept = intact();
            ok &= kept;
            sb.Append($"{type.Name} {(kept ? "kept" : "changed")} {what}; ");
        }
        try
        {
            Check(typeof(DogHomeLife), () => dog.InHomeLife = true, () => dog.InHomeLife, "home life");
            Check(typeof(DogSleepInHouse), () => dog.SpeedOverride = 2f, () => Mathf.Approximately(dog.SpeedOverride, 2f), "speed");
            Check(typeof(DogSkirmish), () => dog.Escaping = true, () => dog.Escaping, "escaping");
            Check(typeof(DogStayHere), () => sniffing.SetValue(dog, true), () => (bool)sniffing.GetValue(dog), "sniffing");
            Check(typeof(DogFollowPlayer), () => sniffing.SetValue(dog, true), () => (bool)sniffing.GetValue(dog), "sniffing");
            Check(typeof(DogWelcome), () => { }, () => true, "-");
        }
        finally
        {
            dog.SpeedOverride = speed;
            dog.InHomeLife = homeLife;
            dog.Escaping = escaping;
            dog.SetSniffing(sniff);
        }
        return $"{Pass(ok)} spurious OnEnd: {sb}";
    }

    // A quiet night at home with the howl certain: out of its house to the door, a howl, back in asleep.
    public static async Task<string> NightHowl()
    {
        Dog dog = Dog;
        var clock = Scope.Resolve<WorldClock>();
        DogRoster roster = Scope.Resolve<DogRoster>();
        Console("enemy.clear");
        Scope.Resolve<DogManager>().Order(dog, DogOrder.Stay);
        PlacePlayer(Ground(Yard(-14f, -10f)), roster.House.transform.position);
        float chance = dog.Settings.HowlChance;
        dog.Settings.HowlChance = 1f;
        typeof(DogState).GetField("_howledDay", Private).SetValue(dog.State, -1);
        bool howled = false, outside = false;
        int howledNight = -1;
        try
        {
            clock.JumpTo(22.5f);
            await Seconds(35f, () =>
            {
                if (roster.Raid)
                    Console("raid.stop");
                AnimatorStateInfo info = dog.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0);
                if (info.IsName("Act Howl"))
                {
                    howled = true;
                    outside |= !dog.InHouse;
                }
            });
            howledNight = dog.State.HowledNight;
        }
        finally
        {
            dog.Settings.HowlChance = chance;
        }
        bool backIn = dog.InHouse && dog.Pose == DogPose.Sleep;
        bool ok = howled && outside && backIn && howledNight == clock.Now.Day;
        return $"{Pass(ok)} night howl: howled {howled} (outside {outside}), back in asleep {backIn}, night used {howledNight} (day {clock.Now.Day}); {Line(dog)}";
    }

    // Herding survives a save and load: it keeps herding, with the time it had left.
    public static async Task<string> HerdAfterLoad()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        var herd = Scope.Resolve<MB13.Aipocalypse.Core.AnimalSystem.AnimalHerd>();
        Scope.Resolve<WorldClock>().JumpTo(10f);
        if (!herd.Orders.Released)
            herd.Orders.Bell();
        for (int i = 0; i < 30 && herd.Animals.Count == herd.CountHome(); i++)
            await Seconds(1f, null);
        if (herd.Animals.Count == herd.CountHome())
            return "SKIP herd after load: no animal out";
        DogManager manager = Scope.Resolve<DogManager>();
        manager.BringHerdHome(dog);
        await Seconds(3f, null);
        DogSaveSection section = Scope.Resolve<SaveRegistry>().Sections.OfType<DogSaveSection>().First();
        var data = Scope.Resolve<SaveService>().Create();
        section.Capture(data);
        data.TryGet(DogSaveSection.Key, out DogSaveSection.DogSave saved);
        section.Restore(data);
        await Seconds(3f, null);
        bool ok = saved.HerdFor > 2f && saved.HerdFor < 10f && dog.State.Order == DogOrder.Herding;
        return $"{Pass(ok)} herd after load: saved herding for {saved.HerdFor:0.0} s, order after the load {dog.State.Order}; {Line(dog)}";
    }

    // Told to stay at home, the player away a while, then back in the yard: it runs over happy, circles, then sits by them.
    public static async Task<string> WelcomeHome()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(10f);
        DogManager manager = Scope.Resolve<DogManager>();
        var after = typeof(DogSettings).GetField("WelcomeAfter");
        object was = after?.GetValue(dog.Settings);
        try
        {
            after?.SetValue(dog.Settings, 3f);
            var stateType0 = typeof(DogState);
            string atStart = $"welcoming {dog.State.Welcoming(Time.time)} away {stateType0.GetField("_awaySince", Private).GetValue(dog.State)} now {Time.time:0.0} player {Player.transform.position:F0} inYard {dog.Roster.InYard(Player.transform.position)}";
            PlacePlayer(Ground(Yard(-6f, 0f)), Ground(Yard(0f, 0f)));
            dog.Legs.Warp(Ground(Yard(6f, 0f)));
            manager.Order(dog, DogOrder.Stay);
            await Seconds(7f, null);
            string afterStay = $"welcoming {dog.State.Welcoming(Time.time)} away {stateType0.GetField("_awaySince", Private).GetValue(dog.State)} now {Time.time:0.0}";
            PlacePlayer(Ground(Yard(-40f, 0f)), Ground(Yard(-60f, 0f)));
            await Seconds(5f, null);
            var stateType = typeof(DogState);
            string Fields() => $"away {stateType.GetField("_awaySince", Private).GetValue(dog.State)} until {stateType.GetField("_welcomeUntil", Private).GetValue(dog.State)} now {Time.time:0.0} home {manager.AtHome(dog)} raid {dog.Roster.Raid} player {dog.Roster.HasPlayer} order {dog.State.Order} pos {dog.transform.position:F0} stateWA {((DogSettings)stateType.GetField("_settings", Private).GetValue(dog.State)).WelcomeAfter} dogWA {dog.Settings.WelcomeAfter} same {ReferenceEquals(stateType.GetField("_settings", Private).GetValue(dog.State), dog.Settings)}";
            string beforeBack = Fields();
            Vector3 back = Ground(Yard(-10f, 4f));
            PlacePlayer(back, Ground(Yard(0f, 4f)));
            int frames = 0, welcomingFrames = 0;
            string firstTrue = "";
            await Seconds(0.5f, () =>
            {
                frames++;
                if (dog.State.Welcoming(Time.time))
                {
                    welcomingFrames++;
                    if (firstTrue == "")
                        firstTrue = $"frame {frames} homelife {dog.InHomeLife}";
                }
            });
            string afterBack = Fields() + $" welcoming {welcomingFrames}/{frames} first {firstTrue} playerInYard {dog.Roster.InYard(dog.Roster.PlayerPosition)} rosterPlayer {dog.Roster.PlayerPosition:F0}";
            float closest = float.MaxValue;
            bool sat = false;
            string closeAt = "";
            float welcomeFrom = Time.time;
            float nextPolar = 0f;
            var polar = new System.Text.StringBuilder();
            await Seconds(22f, () =>
            {
                if (!dog.State.Welcoming(Time.time))
                    return;
                float d = Flat(dog.transform.position - Player.transform.position);
                if (Time.time >= nextPolar)
                {
                    nextPolar = Time.time + 0.4f;
                    Vector3 rel = dog.transform.position - Player.transform.position;
                    polar.Append($" [{Time.time - welcomeFrom:0.0}s {d:0.0}m {Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg:0}deg v{dog.Legs.Velocity.magnitude:0.0} {dog.Pose}]");
                }
                if (d < closest)
                    closeAt = $"{d:0.0} m at {Time.time - welcomeFrom:0.0} s, pose {dog.Pose}, v {dog.Legs.Velocity.magnitude:0.0}";
                closest = Mathf.Min(closest, d);
                // A lap ends at 2.4 m; a bounce, roll or bark at about 3 m (it greets from there, like a call).
                sat |= closest < 3.3f && dog.Pose == DogPose.Sit;
            });
            bool ok = closest < 3.3f && closest > 0.6f && sat && dog.State.Order == DogOrder.Stay;
            return $"{Pass(ok)} welcome home: came to {closest:0.0} m ({closeAt}), sat by the player {sat}, order {dog.State.Order}; {Line(dog)}";
        }
        finally
        {
            if (after != null)
                after.SetValue(dog.Settings, was);
        }
    }

    // Following into the house; the player goes to bed: it lies down beside the bed while they fall asleep, and wakes at
    // its dog house.
    public static async Task<string> Bedtime()
    {
        Dog dog = Dog;
        Console("enemy.clear");
        Scope.Resolve<WorldClock>().JumpTo(8f);
        DogManager manager = Scope.Resolve<DogManager>();
        var sleep = Scope.Resolve<MB13.Aipocalypse.Core.SleepSystem.SleepService>();
        var bed = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.SleepSystem.Bed>();
        if (bed == null)
            return "FAIL bedtime: no bed";
        manager.Order(dog, DogOrder.Follow);
        Vector3 anchor = bed.Anchor.position;
        PlacePlayer(anchor + bed.Anchor.right * 1.2f, anchor);
        // Following, it's indoors with the player (the floor: Ground() would find the terrain under the house).
        dog.Legs.Warp(AstarPath.active.GetNearest(anchor + bed.Anchor.right * 3.5f, NearestNodeConstraint.Walkable).position);
        await Seconds(1f, null);
        if (!sleep.Begin(bed, Player))
            return "FAIL bedtime: the bed refused";
        // A few seconds picking the wake-up time, as a player would.
        await Seconds(4f, null);
        sleep.Shift(2);
        sleep.Confirm();
        float closest = float.MaxValue;
        bool lay = false;
        bool homeAtWake = false, woke = false;
        var trace = new System.Text.StringBuilder();
        float nextMark = 0f;
        var fe = dog.GetComponent<Pathfinding.FollowerEntity>();
        var anim = dog.GetComponentInChildren<Animator>();
        await Seconds(25f, () =>
        {
            if (sleep.Stage == MB13.Aipocalypse.Core.SleepSystem.SleepStage.None)
                return;
            if (!woke && sleep.Stage == MB13.Aipocalypse.Core.SleepSystem.SleepStage.Waking)
            {
                woke = true;
                homeAtWake = dog.Roster.House != null && Flat(dog.transform.position - dog.Roster.House.transform.position) < 4f;
            }
            if (Time.time >= nextMark)
            {
                nextMark = Time.time + 1f;
                bool has = manager.TryGetBedSpot(out Vector3 sp, out _, out _);
                float t = Time.time;
                trace.Append($" [{sleep.Stage} spot {has} {sp:F1} dog {dog.transform.position:F1} pose {dog.Pose} v {dog.Legs.Velocity.magnitude:0.0} | down {dog.State.Down} scr {dog.Scrambling}/{manager.WantsScramble} called {dog.State.Called(t)} pet {dog.State.Petting(t)} sad {dog.State.Sad(t)} plan {dog.Plan.Mode} target {manager.TargetId} warn {manager.WarnId}/{manager.Warning} hurt {manager.PlayerHurt} welcome {dog.State.Welcoming(t)} hasPlayer {dog.Roster.HasPlayer} homelife {dog.InHomeLife} dest {fe.destination:F1} stopped {fe.isStopped} reached {fe.reachedDestination} standing {dog.Standing} hasPath {fe.hasPath} pending {fe.pathPending} remaining {fe.remainingDistance:0.0} end {fe.endOfPath:F1} tags {fe.pathfindingSettings.traversableTags} clip {(anim.GetCurrentAnimatorClipInfo(0).Length > 0 ? anim.GetCurrentAnimatorClipInfo(0)[0].clip.name : "-")} trans {anim.IsInTransition(0)}]");
            }
            float d = Flat(dog.transform.position - anchor);
            closest = Mathf.Min(closest, d);
            lay |= d < 2.2f && (dog.Pose == DogPose.Lie || dog.Pose == DogPose.Sleep);
        });
        return $"{Pass(lay && homeAtWake)} bedtime: closest to the bed {closest:0.0} m, lay down by it {lay}, at its dog house on waking {homeAtWake}; {Line(dog)}{(lay && homeAtWake ? "" : trace.ToString())}";
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

    static object WhistleTick()
    {
        object whistle = typeof(Player).GetField("_whistle", Private).GetValue(Player);
        return whistle.GetType().GetMethod("Tick").Invoke(whistle, new object[] { true, Time.time + 100f });
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
