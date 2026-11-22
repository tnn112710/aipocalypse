using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RobotSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using Pathfinding;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

// Plays the service robots in WorldMap (enter Play first, then set Application.runInBackground = true). Each entry
// returns PASS / FAIL lines. Run: unity command script run --file AgentScripts/Robots/RobotPlayCheck.cs --entry
// RobotPlayCheck.<Entry> --args '[...]'. Robots are named by prefab: Robot_Shop (garden), Robot_Upgrades (house).
public static class RobotPlayCheck
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // Both spots on the navmesh, in the same walkable region as the player.
    public static string Navmesh()
    {
        var sb = new StringBuilder();
        var player = AstarPath.active.GetNearest(Player.transform.position, NearestNodeConstraint.Walkable);
        foreach (Robot robot in Robots)
        {
            var nearest = AstarPath.active.GetNearest(robot.Spot.Center, NearestNodeConstraint.Walkable);
            float off = nearest.node == null ? float.PositiveInfinity : Vector3.Distance(nearest.position, robot.Spot.Center);
            bool ok = nearest.node != null && off < 0.6f && PathUtilities.IsPathPossible(player.node, nearest.node);
            sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {robot.name} spot on navmesh (off {off:0.00} m, reachable {nearest.node != null && PathUtilities.IsPathPossible(player.node, nearest.node)})");
        }
        return sb.ToString();
    }

    // Watches both robots idle: each stays within its spot's radius and out of its NoGo boxes; reports how far they moved.
    public static async Task<string> Idle(float seconds)
    {
        Robot[] robots = Robots;
        var start = robots.Select(r => r.transform.position).ToArray();
        var moved = new float[robots.Length];
        var outside = new bool[robots.Length];
        Vector3[] last = start.ToArray();
        StandAway();
        await Seconds(seconds, () =>
        {
            for (int i = 0; i < robots.Length; i++)
            {
                Vector3 p = robots[i].transform.position;
                moved[i] += Vector3.Distance(p, last[i]);
                last[i] = p;
                if (!robots[i].Spot.Allows(p) && Flat(p - robots[i].Spot.Center) > 0.6f)
                    outside[i] = true;
            }
        });
        var sb = new StringBuilder();
        for (int i = 0; i < robots.Length; i++)
            sb.AppendLine($"{(outside[i] ? "FAIL" : "PASS")} {robots[i].name} idled {seconds:0} s: walked {moved[i]:0.0} m, stayed in its area {!outside[i]}; {Line(robots[i])}");
        return sb.ToString();
    }

    // Stands the player 2.2 m in front of the robot looking at it: it should halt, face the player and greet.
    public static async Task<string> Look(string name)
    {
        Robot robot = Find(name);
        Vector3 at = robot.transform.position + robot.transform.forward * 2.2f;
        PlacePlayer(at, robot.Head.position);
        bool greeted = false;
        await Seconds(2.5f, () =>
        {
            Aim(robot.Head.position);
            greeted |= robot.State.Greeting(Time.time);
        });
        float facing = Vector3.Angle(robot.transform.forward, Vector3.ProjectOnPlane(Player.transform.position - robot.transform.position, Vector3.up));
        string prompt = Player.Interactor.HasPrompt ? $"{Player.Interactor.Prompt.Verb} {Player.Interactor.Prompt.Target}" : "(none)";
        return $"{(greeted && facing < 20f ? "PASS" : "FAIL")} {name} greets a look: greeting {greeted}, facing off {facing:0}°, prompt \"{prompt}\"; {Line(robot)}";
    }

    // Whistles at the robot from `distance` m along `bearing` (degrees, from the yard's frame) and watches it come.
    public static async Task<string> Whistle(string name, float distance, float bearing, float seconds)
    {
        Robot robot = Find(name);
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Vector3 from = robot.transform.position + Quaternion.Euler(0f, yard.transform.eulerAngles.y + bearing, 0f) * Vector3.forward * distance;
        from = Ground(from);
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.3f, () => Aim(robot.Head.position));
        bool insideStart = yard.Contains(from);
        Transform eye = Camera.main.transform;
        string firstHit = Physics.SphereCast(eye.position, 0.35f, eye.forward, out RaycastHit seen, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? $"{seen.collider.name} at {seen.distance:0.0} m" : "nothing";
        object whistle = typeof(Player).GetField("_whistle", Private).GetValue(Player);
        var target = (MB13.Aipocalypse.Core.Interaction.IWhistleTarget)whistle.GetType().GetMethod("Tick").Invoke(whistle, new object[] { true, Time.time + 100f });
        bool answered = ReferenceEquals(target, robot);
        bool leftYard = false;
        await Seconds(seconds, () =>
        {
            Aim(robot.Head.position);
            leftYard |= !yard.Contains(robot.transform.position);
        });
        Vector3 meet = robot.Call.MeetPoint;
        float toPlayer = Flat(Player.transform.position - robot.transform.position);
        return $"whistle at {name} from {distance:0} m ({(insideStart ? "inside" : "outside")} the yard, aim hits {firstHit}): answered {answered}, arrived {robot.Call.Arrived}, " +
               $"{toPlayer:0.0} m from the player, meet point inside the yard {yard.Contains(meet)}, left the yard {leftYard}; {Line(robot)}";
    }

    // Whistles at the robot from a point given in the yard's frame (x, z m from its centre), and watches it come.
    public static async Task<string> WhistleFromYard(string name, float x, float z, float seconds)
    {
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Robot robot = Find(name);
        Vector3 from = yard.transform.TransformPoint(new Vector3(x, 0f, z));
        float distance = Flat(from - robot.transform.position);
        float bearing = Vector3.SignedAngle(yard.transform.forward, from - robot.transform.position, Vector3.up);
        return await Whistle(name, distance, bearing, seconds);
    }

    // Whistles from a point in the house's frame (its door at z = 3): through the open door from outside.
    public static async Task<string> WhistleFromHouse(string name, float x, float z, float seconds)
    {
        Transform house = GameObject.Find("Environment/Landmarks/Home Base/House_1").transform;
        Robot robot = Find(name);
        Vector3 from = house.TransformPoint(new Vector3(x, 0f, z));
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        float bearing = Vector3.SignedAngle(yard.transform.forward, from - robot.transform.position, Vector3.up);
        return await Whistle(name, Flat(from - robot.transform.position), bearing, seconds);
    }

    // Whistles from a yard point, then walks the player 8 m toward the robot over 4 s: it should meet them and wait, not
    // shrug and leave; then walks into it while it waits: it should step aside and keep waiting.
    public static async Task<string> Approach(string name, float x, float z)
    {
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Robot robot = Find(name);
        Vector3 from = Ground(yard.transform.TransformPoint(new Vector3(x, 0f, z)));
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.3f, () => Aim(robot.Head.position));
        object whistle = typeof(Player).GetField("_whistle", Private).GetValue(Player);
        bool answered = whistle.GetType().GetMethod("Tick").Invoke(whistle, new object[] { true, Time.time + 200f }) != null;
        Vector3 toward = Vector3.ProjectOnPlane(robot.transform.position - from, Vector3.up).normalized;
        float walked = 0f;
        await Seconds(4f, () =>
        {
            walked = Mathf.Min(8f, walked + Time.deltaTime * 2f);
            PlacePlayer(Ground(from + toward * walked), robot.Head.position);
        });
        await Seconds(10f, () => Aim(robot.Head.position));
        bool waiting = robot.Call.Active && robot.Call.Arrived;
        float gap = Flat(Player.transform.position - robot.transform.position);
        Vector3 before = robot.transform.position;
        PlacePlayer(before + Vector3.ProjectOnPlane(Player.transform.position - before, Vector3.up).normalized * 0.5f, robot.Head.position);
        await Seconds(2.5f, null);
        float stepped = Flat(robot.transform.position - before);
        bool stillWaiting = robot.Call.Active;
        bool ok = answered && waiting && gap > 1f && stepped >= 0.9f && stillWaiting;
        return $"{(ok ? "PASS" : "FAIL")} {name} met a player walking toward it: answered {answered}, waiting {waiting} {gap:0.0} m away; walked into, it stepped {stepped:0.0} m and still waits {stillWaiting}; {Line(robot)}";
    }

    // Walks the player into the robot (puts them 0.5 m from it) and checks it moves away.
    public static async Task<string> Bump(string name)
    {
        Robot robot = Find(name);
        Vector3 start = robot.transform.position;
        PlacePlayer(start + robot.transform.forward * 0.5f, robot.Head.position);
        await Seconds(2.5f, null);
        float moved = Flat(robot.transform.position - start);
        return $"{(moved >= 0.9f ? "PASS" : "FAIL")} {name} steps aside: moved {moved:0.0} m; {Line(robot)}";
    }

    // A gunshot hit through the damage service: it flinches, takes nothing, and nothing is published (no hit marker).
    public static async Task<string> Shoot(string name)
    {
        Robot robot = Find(name);
        int published = 0;
        using IDisposable subscription = Scope.Resolve<ISubscriber<DamageDealt>>().Subscribe(_ => published++);
        Vector3 chest = robot.transform.position + Vector3.up * 0.9f;
        Vector3 from = chest + robot.transform.forward * 5f;
        PlacePlayer(Ground(robot.transform.position + robot.transform.forward * 6f), chest);
        await Seconds(0.5f, () => Aim(chest));
        bool hitIt = Physics.Raycast(from, chest - from, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        IDamageable target = hitIt ? Scope.Resolve<DamageService>().DealHit(hit, new DamageInfo(30f, DamageType.Gunshot, hit.point, Vector3.zero, Player.gameObject)) : null;
        string state = "";
        await Seconds(1f, () =>
        {
            Aim(chest);
            string now = StateName(robot.Animator);
            if (now.StartsWith("Hit_"))
                state = now;
        });
        bool ok = ReferenceEquals(target, robot) && published == 0 && state.StartsWith("Hit_");
        return $"{(ok ? "PASS" : "FAIL")} {name} shot: hit the robot {ReferenceEquals(target, robot)}, DamageDealt published {published}, state {state}; {Line(robot)}";
    }

    // Gives the player the AK47 and points it at the robot's head from 4 m: worried face and a head shake.
    public static async Task<string> Nervous(string name, float distance)
    {
        Robot robot = Find(name);
        Player player = Player;
        ItemDefinition definition = AssetDatabase.FindAssets("AK47 t:" + nameof(ItemDefinition), new[] { "Assets/_Game/Content" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).First(i => i.name == "AK47");
        int slot = -1;
        for (int i = 0; i < player.Equipment.Hotbar.Count && slot < 0; i++)
            if (player.Equipment.Hotbar[i]?.Definition == definition)
                slot = i;
        if (slot < 0)
        {
            ItemInstance gun = definition.CreateInstance();
            player.Equipment.TryPickUp(gun, out _);
            slot = player.Equipment.Hotbar.IndexOf(gun);
        }
        player.Equipment.Select(slot);
        await Seconds(1.2f, null);
        PlacePlayer(robot.transform.position + robot.transform.forward * distance, robot.Head.position);
        bool nervous = false;
        string state = "";
        await Seconds(2f, () =>
        {
            Aim(robot.Head.position);
            string now = StateName(robot.Animator);
            if (now == "Emote_6")
                state = now;
            nervous |= robot.State.Nervous(Time.time);
        });
        return $"{(nervous && state == "Emote_6" ? "PASS" : "FAIL")} {name} nervous at a pointed gun: {nervous}, state {state}; {Line(robot)}";
    }

    // Sets the clock to `hour` and waits: at night both walk to their spots and stand by, faces glowing.
    public static async Task<string> Night(float hour, float seconds)
    {
        Scope.Resolve<WorldClock>().JumpTo(hour);
        StandAway();
        await Seconds(seconds, null);
        var sb = new StringBuilder();
        foreach (Robot robot in Robots)
        {
            float fromSpot = Flat(robot.transform.position - robot.Spot.Center);
            bool standby = robot.Animator.GetBool("Standby");
            float glow = robot.GetComponent<RobotFaceGlow>().Weight;
            bool ok = robot.Asleep ? standby && fromSpot < 1f : !standby;
            sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {robot.name} at {hour:0}:00: asleep {robot.Asleep}, standby {standby}, {fromSpot:0.0} m from its spot, glow {glow:0.00}");
        }
        return sb.ToString();
    }

    // Several beeps at once: only the first plays.
    public static string Beeps()
    {
        RobotManager manager = Scope.Resolve<RobotManager>();
        Robot[] robots = Robots;
        int played = 0;
        for (int i = 0; i < 5; i++)
            if (manager.TryBeep(robots[i % robots.Length], RobotSound.Chirp))
                played++;
        return $"{(played <= 1 ? "PASS" : "FAIL")} a burst of 5 beeps played {played}";
    }

    public static string Status() => string.Join("\n", Robots.Select(Line));

    // Stands the player `distance` m in front of the robot, a little to the side, looking at it (for a screenshot).
    public static async Task<string> Frame(string name, float distance, float side)
    {
        Robot robot = Find(name);
        Vector3 at = robot.transform.position + robot.transform.forward * distance + robot.transform.right * side;
        PlacePlayer(at, robot.transform.position + Vector3.up * 0.8f);
        await Seconds(1.5f, () => Aim(robot.transform.position + Vector3.up * 0.8f));
        return Line(robot);
    }

    // ---- Helpers ----

    static string Line(Robot robot)
    {
        float fromSpot = Flat(robot.transform.position - robot.Spot.Center);
        return $"{robot.name}: {fromSpot:0.0} m from spot, speed {robot.Mover.Velocity.magnitude:0.00}, state {StateName(robot.Animator)}, mood {robot.Face.Mood}, " +
               $"called {robot.Call.Active}/{robot.Call.Arrived}, greeting {robot.State.Greeting(Time.time)}, hit {robot.State.Hit(Time.time)}, nervous {robot.State.Nervous(Time.time)}, asleep {robot.Asleep}";
    }

    // ---- Pass 2 ----

    static Transform House => GameObject.Find("Environment/Landmarks/Home Base/House_1").transform;
    static RobotManager Manager => Scope.Resolve<RobotManager>();

    // Pip is whistled from in front of the house: it should come out through the door (hop down), meet the player outside,
    // then, when the player walks off, go back in.
    public static async Task<string> Door(float seconds)
    {
        Robot robot = Find("Robot_Upgrades");
        Transform house = House;
        float floor = robot.Spot.Center.y;
        // Called as by the console (robot.call): aiming at it through the narrow door is a separate check (WhistleFromHouse).
        Vector3 from = Ground(house.TransformPoint(new Vector3(-2.6f, 0f, 7.5f)));
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.3f, null);
        bool answered = robot.TryWhistle(Player.gameObject) && robot.Call.Active;
        bool cameOut = false;
        bool hopped = false;
        await Seconds(seconds, () =>
        {
            Aim(robot.Head.position);
            cameOut |= robot.transform.position.y < floor - 0.6f;
            hopped |= StateName(robot.Animator) == "Emote_9";
        });
        bool arrived = robot.Call.Arrived;
        float toPlayer = Flat(Player.transform.position - robot.transform.position);
        // Walk off: it shrugs and goes home, back up through the door.
        Vector3 away = Vector3.ProjectOnPlane(from - house.position, Vector3.up).normalized;
        PlacePlayer(Ground(from + away * 12f), house.position);
        bool wentIn = false;
        await Seconds(seconds + 10f, () => wentIn |= robot.transform.position.y > floor - 0.2f && Flat(robot.transform.position - robot.Spot.Center) < 2.5f);
        bool ok = answered && cameOut && arrived && wentIn;
        return $"{(ok ? "PASS" : "FAIL")} Pip through the door: answered {answered}, came out {cameOut}, hop clip {hopped}, arrived {arrived} {toPlayer:0.0} m from the player, back inside {wentIn}; {Line(robot)}";
    }

    // The house interior is robots-only: a walker's path to the robot's spot ends outside; a robot's goes in.
    public static string Tags()
    {
        Robot pip = Find("Robot_Upgrades");
        GraphNode inside = AstarPath.active.GetNearest(pip.Spot.Center, NearestNodeConstraint.Walkable).node;
        Vector3 start = Ground(House.TransformPoint(new Vector3(-1.4f, 0f, 9f)));
        string Path(int tags)
        {
            ABPath path = ABPath.Construct(start, pip.Spot.Center, null);
            path.enabledTags = tags;
            AstarPath.StartPath(path);
            path.BlockUntilCalculated();
            Vector3 end = path.vectorPath.Count > 0 ? path.vectorPath[path.vectorPath.Count - 1] : Vector3.positiveInfinity;
            return $"{path.CompleteState} end y {end.y:0.00} ({Flat(end - pip.Spot.Center):0.0} m from the spot)";
        }
        string walker = Path(MB13.Aipocalypse.Core.EnemySystem.NavTags.Walkers);
        string robot = Path(MB13.Aipocalypse.Core.EnemySystem.NavTags.Robots);
        bool ok = inside != null && inside.Tag == MB13.Aipocalypse.Core.EnemySystem.NavTags.RobotsOnly;
        return $"{(ok ? "PASS" : "FAIL")} interior tag {inside?.Tag}; walker path {walker}; robot path {robot}";
    }

    // Aiming at a robot from `distance` m shows "[Q] Call <name>" (when E doesn't already reach it).
    public static async Task<string> Prompt(string name, float distance, float bearing)
    {
        Robot robot = Find(name);
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Vector3 from = Ground(robot.transform.position + Quaternion.Euler(0f, yard.transform.eulerAngles.y + bearing, 0f) * Vector3.forward * distance);
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.4f, () => Aim(robot.Head.position));
        PlayerWhistle whistle = Player.Whistle;
        var hud = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.UI.InteractPromptHud>();
        Transform line = hud != null ? hud.transform.Find("WhistlePrompt") : null;
        string shown = line != null ? string.Join(" ", line.GetComponentsInChildren<TMPro.TMP_Text>().Where(t => t.isActiveAndEnabled).Select(t => t.text)) : "(no HUD line)";
        bool ok = whistle.HasPrompt && ReferenceEquals(whistle.Target, robot) && shown.Contains(whistle.Prompt.Target);
        return $"{(ok ? "PASS" : "FAIL")} prompt at {distance:0} m: {whistle.HasPrompt} \"{whistle.Prompt.Verb} {whistle.Prompt.Target}\", HUD \"{shown}\"";
    }

    // Three quick shots knock it over: crossed eyes while down, a fall, lying, getting up, then a sulk.
    public static async Task<string> Knockdown(string name)
    {
        Robot robot = Find(name);
        Vector3 chest = robot.transform.position + Vector3.up * 0.9f;
        PlacePlayer(Ground(robot.transform.position + robot.transform.forward * 5f), chest);
        await Seconds(0.3f, () => Aim(chest));
        for (int i = 0; i < 3; i++)
        {
            chest = robot.transform.position + Vector3.up * 0.9f;
            robot.TakeDamage(new DamageInfo(10f, DamageType.Gunshot, chest + robot.transform.forward * 0.2f, Vector3.zero, Player.gameObject));
            await Seconds(0.3f, null);
        }
        var seen = new System.Collections.Generic.List<string>();
        bool dizzy = false;
        await Seconds(6f, () =>
        {
            string state = StateName(robot.Animator);
            if (seen.Count == 0 || seen[seen.Count - 1] != state)
                seen.Add(state);
            dizzy |= robot.Face.Shown == RobotExpression.Dizzy;
        });
        bool sulking = robot.State.Sulking(Time.time);
        bool ok = seen.Contains("Fall_0") && seen.Contains("Lie_0") && seen.Contains("Rise_0") && dizzy && sulking;
        return $"{(ok ? "PASS" : "FAIL")} {name} knocked down: states {string.Join(" > ", seen)}, crossed eyes {dizzy}, sulking {sulking}; {Line(robot)}";
    }

    // Hold-E: heart eyes and a laugh; it forgives a sulk.
    public static async Task<string> Pat(string name)
    {
        Robot robot = Find(name);
        PlacePlayer(Ground(robot.transform.position + robot.transform.forward * 1.6f), robot.Head.position);
        await Seconds(0.3f, () => Aim(robot.Head.position));
        bool offered = robot.TryGetHoldPrompt(Player.gameObject, out var prompt);
        robot.InteractHold(Player.gameObject);
        bool love = false;
        bool laugh = false;
        await Seconds(2f, () =>
        {
            Aim(robot.Head.position);
            love |= robot.Face.Shown == RobotExpression.Love;
            laugh |= StateName(robot.Animator) == "Emote_4";
        });
        bool ok = offered && love && laugh && !robot.State.Sulking(Time.time);
        return $"{(ok ? "PASS" : "FAIL")} {name} patted: prompt \"{prompt.Verb} {prompt.Target}\", hearts {love}, laugh {laugh}, sulking {robot.State.Sulking(Time.time)}; {Line(robot)}";
    }

    // A raid: brave boxing, then both hide at their shelters, worried; then the end: win and dance, or cry.
    public static async Task<string> Raid(string outcome, float hideSeconds)
    {
        StandAway();
        Robot[] robots = Robots;
        var brave = robots.ToDictionary(r => r, _ => false);
        Manager.StartRaid();
        await Seconds(hideSeconds, () =>
        {
            foreach (Robot r in robots)
                brave[r] |= StateName(r.Animator).StartsWith("Emote_11");
        });
        var sb = new StringBuilder();
        foreach (Robot r in robots)
        {
            float fromShelter = Flat(r.transform.position - r.Shelter);
            bool ok = brave[r] && fromShelter < 1f && r.Face.Shown == RobotExpression.Worried;
            sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {r.name} hides: brave {brave[r]}, {fromShelter:0.0} m from its shelter, face {r.Face.Shown}");
        }
        Manager.EndRaid(outcome == "lose" ? RaidEndReason.Defeat : RaidEndReason.Cleared);
        var seen = robots.ToDictionary(r => r, _ => new System.Collections.Generic.HashSet<string>());
        var faces = robots.ToDictionary(r => r, _ => new System.Collections.Generic.HashSet<RobotExpression>());
        await Seconds(4.5f, () =>
        {
            foreach (Robot r in robots)
            {
                seen[r].Add(StateName(r.Animator));
                faces[r].Add(r.Face.Shown);
            }
        });
        foreach (Robot r in robots)
        {
            bool ok = outcome == "lose"
                ? seen[r].Contains("Emote_10") && faces[r].Contains(RobotExpression.Sad)
                : seen[r].Contains("Emote_7") && seen[r].Contains("Emote_8") && faces[r].Contains(RobotExpression.Excited);
            sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {r.name} after a {outcome}: {string.Join(", ", seen[r])}; faces {string.Join(", ", faces[r])}");
        }
        return sb.ToString().TrimEnd();
    }

    // The player is back from a trip: robots look round, then come over and greet with hearts.
    public static async Task<string> Welcome(float seconds)
    {
        Robot robot = Find("Robot_Shop");
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Vector3 at = Ground(yard.transform.TransformPoint(new Vector3(-4f, 0f, 6f)));
        PlacePlayer(at, robot.Head.position);
        await Seconds(0.3f, null);
        Manager.WelcomeHome();
        bool looked = false;
        bool love = false;
        await Seconds(seconds, () =>
        {
            looked |= StateName(robot.Animator) == "Emote_12";
            love |= robot.Face.Shown == RobotExpression.Love;
        });
        bool ok = looked && robot.Call.Arrived && robot.Call.Reason == RobotCallReason.Welcome && love;
        return $"{(ok ? "PASS" : "FAIL")} welcome home: looked round {looked}, came {robot.Call.Arrived} ({robot.Call.Reason}), hearts {love}, {Flat(Player.transform.position - robot.transform.position):0.0} m from the player; {Line(robot)}";
    }

    // Talking swaps the mouth for the scrolling speech line.
    public static async Task<string> Speech(string name)
    {
        Robot robot = Find(name);
        robot.Hold(RobotExpression.Talk, 2f);
        await Seconds(0.3f, null);
        Renderer[] renderers = robot.GetComponentsInChildren<Renderer>(true);
        Renderer speech = renderers.First(r => r.name == "Rob15_mouthSpeech");
        Renderer mouth = renderers.First(r => r.name == "Rob15_mouth");
        var block = new MaterialPropertyBlock();
        speech.GetPropertyBlock(block);
        float x0 = block.GetVector("_BaseMap_ST").z;
        await Seconds(0.5f, null);
        speech.GetPropertyBlock(block);
        float x1 = block.GetVector("_BaseMap_ST").z;
        bool talking = speech.enabled && speech.gameObject.activeInHierarchy && !mouth.enabled && Mathf.Abs(x1 - x0) > 0.01f;
        await Seconds(2f, null);
        bool back = !speech.enabled && mouth.enabled;
        bool ok = talking && back;
        return $"{(ok ? "PASS" : "FAIL")} {name} speech line while talking {talking} (scroll {x0:0.00} > {x1:0.00}), mouth back after {back}";
    }

    // At night it stands by facing out from its spot; in the morning it hops awake.
    public static async Task<string> NightFacing(float seconds)
    {
        StandAway();
        Scope.Resolve<WorldClock>().JumpTo(23f);
        // A raid may start at night: stopped, so the robots stand by instead of hiding.
        await Seconds(1f, null);
        var director = Scope.Resolve<MB13.Aipocalypse.Core.RaidSystem.RaidDirector>();
        if (director.IsRunning)
            director.Stop();
        await Seconds(seconds, null);
        var sb = new StringBuilder();
        foreach (Robot r in Robots)
        {
            float off = Vector3.Angle(Vector3.ProjectOnPlane(r.transform.forward, Vector3.up), Vector3.ProjectOnPlane(r.HomeFacing, Vector3.up));
            sb.AppendLine($"{(off < 20f ? "PASS" : "FAIL")} {r.name} stands by facing its spot's way: {off:0} deg off, {StateName(r.Animator)}");
        }
        Scope.Resolve<WorldClock>().JumpTo(7f);
        var hopped = Robots.ToDictionary(r => r, _ => false);
        await Seconds(3f, () =>
        {
            foreach (Robot r in Robots)
                hopped[r] |= StateName(r.Animator) == "Emote_9";
        });
        foreach (Robot r in Robots)
            sb.AppendLine($"{(hopped[r] ? "PASS" : "FAIL")} {r.name} wakes with a hop {hopped[r]}");
        return sb.ToString().TrimEnd();
    }

    // ---- Pass 3 ----

    // From beside the house, a wall between the player and Pip: aiming near Pip still finds it (prompt, then the whistle).
    public static async Task<string> HeardThroughWall()
    {
        Robot robot = Find("Robot_Upgrades");
        Vector3 from = Ground(House.TransformPoint(new Vector3(8f, 0f, -0.6f)));
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.5f, () => Aim(robot.Head.position));
        Transform eye = Camera.main.transform;
        string firstHit = Physics.SphereCast(eye.position, 0.35f, eye.forward, out RaycastHit seen, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? seen.collider.name : "nothing";
        PlayerWhistle whistle = Player.Whistle;
        bool prompted = ReferenceEquals(whistle.Target, robot) && whistle.HasPrompt;
        var target = whistle.Tick(true, Time.time + 100f);
        bool answered = ReferenceEquals(target, robot) && robot.Call.Active;
        string got = target is Component c ? c.name : "nobody";
        robot.Call.End();
        bool ok = prompted && answered && firstHit != robot.name;
        return $"{(ok ? "PASS" : "FAIL")} whistle heard through a wall: aim hits {firstHit}, prompt {prompted} (\"{whistle.Prompt.Verb} {whistle.Prompt.Target}\"), " +
               $"answered by {got}, coming {robot.Call.Active || answered}; {Line(robot)}";
    }

    // The player walks out of the yard near Bolt: it turns to them and waves goodbye.
    public static async Task<string> SeeOff()
    {
        Robot robot = Find("Robot_Shop");
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Vector3 local = yard.transform.InverseTransformPoint(robot.transform.position);
        Vector3 inside = Ground(yard.transform.TransformPoint(new Vector3(local.x, 0f, yard.HalfSize.z - 2f)));
        Vector3 outside = Ground(yard.transform.TransformPoint(new Vector3(local.x, 0f, yard.HalfSize.z + 3f)));
        Vector3 away = outside + (outside - inside) * 3f;
        PlacePlayer(inside, away);
        await Seconds(1f, () => Aim(away));
        PlacePlayer(outside, away);
        bool sawOff = false;
        bool waved = false;
        await Seconds(2.5f, () =>
        {
            Aim(away);
            sawOff |= robot.State.SeeingOff(Time.time);
            waved |= StateName(robot.Animator) == "Emote_1";
        });
        float facing = Vector3.Angle(robot.transform.forward, Vector3.ProjectOnPlane(Player.transform.position - robot.transform.position, Vector3.up));
        bool ok = sawOff && waved && facing < 30f;
        return $"{(ok ? "PASS" : "FAIL")} see off: seeing off {sawOff}, waved {waved}, facing off {facing:0} deg, {Flat(Player.transform.position - robot.transform.position):0.0} m away; {Line(robot)}";
    }

    // Pip is put 2.5 m from Bolt: both stop, face each other, one waves, the other talks, then they swap (talk, laugh).
    public static async Task<string> Chat()
    {
        StandAway();
        Robot bolt = Find("Robot_Shop");
        Robot pip = Find("Robot_Upgrades");
        Vector3 beside = bolt.transform.position + bolt.transform.right * 2.5f;
        if (!pip.Mover.TryFindDestination(beside, 1f, 0f, 8, out Vector3 point))
            return "FAIL no ground beside Bolt";
        pip.GetComponent<FollowerEntity>().Teleport(point);
        var seen = new System.Collections.Generic.Dictionary<Robot, System.Collections.Generic.HashSet<string>>
        {
            [bolt] = new(),
            [pip] = new(),
        };
        bool chatting = false;
        await Seconds(4.5f, () =>
        {
            chatting |= bolt.State.Chatting(Time.time) && pip.State.Chatting(Time.time);
            foreach (Robot r in seen.Keys)
                seen[r].Add(StateName(r.Animator));
        });
        float Facing(Robot r, Robot other) => Vector3.Angle(r.transform.forward, Vector3.ProjectOnPlane(other.transform.position - r.transform.position, Vector3.up));
        string Clips(Robot r) => string.Join(" ", seen[r].Where(n => n.StartsWith("Emote")));
        bool talked = seen.Values.Any(v => v.Contains("Emote_2")) && seen.Values.Any(v => v.Contains("Emote_1"));
        bool ok = chatting && talked && Facing(bolt, pip) < 35f && Facing(pip, bolt) < 35f;
        string result = $"{(ok ? "PASS" : "FAIL")} chat: both chatting {chatting}, leader Bolt {bolt.State.ChatLeads}, clips Bolt [{Clips(bolt)}] Pip [{Clips(pip)}], " +
                        $"facing off {Facing(bolt, pip):0}/{Facing(pip, bolt):0} deg";
        // Pip walks home again.
        await Seconds(1f, null);
        return result;
    }

    // Whistled four times in a row: annoyed (a head shake, angry, it stops coming), the next whistle is ignored; a pat
    // forgives it and it comes again.
    public static async Task<string> WhistleSpam(string name)
    {
        Robot robot = Find(name);
        Vector3 from = Ground(robot.transform.position + robot.transform.forward * 8f);
        PlacePlayer(from, robot.Head.position);
        await Seconds(0.3f, null);
        bool cameFirst = false;
        for (int i = 0; i < 4; i++)
        {
            robot.TryWhistle(Player.gameObject);
            cameFirst |= i == 0 && robot.Call.Active;
            await Seconds(0.7f, null);
        }
        bool annoyed = robot.State.Annoyed(Time.time) && !robot.Call.Active;
        bool shook = false;
        bool angry = false;
        await Seconds(1.5f, () =>
        {
            shook |= StateName(robot.Animator) == "Emote_6";
            angry |= robot.Face.Shown == RobotExpression.Angry;
        });
        robot.TryWhistle(Player.gameObject);
        bool ignored = !robot.Call.Active;
        robot.InteractHold(Player.gameObject);
        await Seconds(0.5f, null);
        robot.TryWhistle(Player.gameObject);
        bool forgiven = robot.Call.Active;
        robot.Call.End();
        bool ok = cameFirst && annoyed && shook && angry && ignored && forgiven;
        return $"{(ok ? "PASS" : "FAIL")} {name} whistle spam: came at first {cameFirst}, annoyed {annoyed}, head shake {shook}, angry {angry}, next ignored {ignored}, comes after a pat {forgiven}";
    }

    // The night light fades in at night and out by day; bullets on a robot sound like metal.
    public static async Task<string> NightLight()
    {
        var sb = new StringBuilder();
        var surfaces = Scope.Resolve<MB13.Aipocalypse.Core.SurfaceSystem.SurfaceService>();
        foreach (Robot r in Robots)
        {
            var kind = surfaces.KindOf(r.GetComponent<Collider>(), r.transform.position + Vector3.up);
            sb.AppendLine($"{(kind == MB13.Aipocalypse.Core.SurfaceSystem.SurfaceKind.Metal ? "PASS" : "FAIL")} {r.name} is {kind} to bullets");
        }
        var clock = Scope.Resolve<WorldClock>();
        clock.JumpTo(23f);
        await Seconds(3f, null);
        var director = Scope.Resolve<MB13.Aipocalypse.Core.RaidSystem.RaidDirector>();
        if (director.IsRunning)
            director.Stop();
        foreach (Robot r in Robots)
        {
            Light light = r.GetComponentInChildren<Light>(true);
            bool on = light != null && light.enabled && light.intensity > 0.5f;
            sb.AppendLine($"{(on ? "PASS" : "FAIL")} {r.name} night light on at 23:00 ({(light != null ? light.intensity.ToString("0.00") : "none")})");
        }
        return sb.ToString().TrimEnd();
    }

    public static async Task<string> DayLight()
    {
        Scope.Resolve<WorldClock>().JumpTo(12f);
        // The sky's daylight eases in, then the light fades over its response time.
        await Seconds(8f, null);
        var sb = new StringBuilder();
        foreach (Robot r in Robots)
        {
            Light light = r.GetComponentInChildren<Light>(true);
            bool off = light != null && !light.enabled;
            sb.AppendLine($"{(off ? "PASS" : "FAIL")} {r.name} night light off at noon");
        }
        return sb.ToString().TrimEnd();
    }

    static string StateName(Animator animator)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        foreach (string state in new[] { "Locomotion", "Standby", "Hit_0", "Hit_1", "Hit_2", "Fall_0", "Fall_1", "Lie_0", "Lie_1", "Rise_0", "Rise_1", "Emote_11_Punch" }
                     .Concat(Enumerable.Range(1, 12).Select(i => "Emote_" + i)))
            if (info.IsName(state))
                return state;
        return info.shortNameHash.ToString();
    }

    // Moves the player far from both robots (beside the yard's centre, looking away).
    static void StandAway()
    {
        RobotYard yard = Object.FindAnyObjectByType<RobotYard>();
        Vector3 at = Ground(yard.transform.position - yard.transform.forward * 10f);
        PlacePlayer(at, at - yard.transform.forward);
    }

    static void PlacePlayer(Vector3 at, Vector3 lookAt)
    {
        Player player = Player;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = at + Vector3.up * 0.1f;
        controller.enabled = true;
        Aim(lookAt);
    }

    static void Aim(Vector3 point)
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

    static Vector3 Ground(Vector3 point) =>
        Physics.Raycast(point + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point : point;

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    static Robot[] Robots => Object.FindObjectsByType<Robot>().OrderBy(r => r.name).ToArray();
    static Robot Find(string name) => Robots.FirstOrDefault(r => r.name == name) ?? throw new Exception("no robot " + name);
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
