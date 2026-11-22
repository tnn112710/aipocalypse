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
            greeted |= robot.Greeting;
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
            nervous |= robot.Nervous;
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
               $"called {robot.Call.Active}/{robot.Call.Arrived}, greeting {robot.Greeting}, hit {robot.Hit}, nervous {robot.Nervous}, asleep {robot.Asleep}";
    }

    static string StateName(Animator animator)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        foreach (string state in new[] { "Locomotion", "Standby", "Hit_0", "Hit_1", "Hit_2" }.Concat(Enumerable.Range(1, 9).Select(i => "Emote_" + i)))
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
