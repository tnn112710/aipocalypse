// Play-mode perf suite for the world map (run with `unity command script run` while in play mode):
// Views (fixed spots), Actions (move, look, fire, aim, switch, melee), DayNight (hour transitions, night lights), Audio (audio counters), Crowd (enemy count),
// Fight (a night raid fight: firing at spawned goblins), Ab (settings A/B).
// Frame time, PlayerLoop (game cost without the editor loop), FrameTiming CPU/GPU, render counters, GC, cameras per frame,
// and the top profiler markers outside the editor loop. Results also go to %TEMP%/claude_perf_<name>.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public static class PerfSuite
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // name:x,z,yaw; "spawn" uses the player's spawn point
    public const string DefaultViews =
        "spawn_bridge:spawn,50;spawn_house:spawn,230;meadow_east:420,250,90;lake_view:526,196,100;forest_in:450,540,20;" +
        "forest_lake:590,400,120;highland:330,690,80;highland_top:290,760,160;swamp_edge:600,790,60";

    static readonly (ProfilerCategory cat, string name)[] CounterDefs =
    {
        (ProfilerCategory.Render, "Draw Calls Count"),
        (ProfilerCategory.Render, "Batches Count"),
        (ProfilerCategory.Render, "SetPass Calls Count"),
        (ProfilerCategory.Render, "Triangles Count"),
        (ProfilerCategory.Render, "Shadow Casters Count"),
        (ProfilerCategory.Memory, "GC Allocated In Frame"),
    };

    sealed class Seg
    {
        public string Name;
        public readonly List<float> Dt = new(), Main = new(), Render = new(), Gpu = new();
        // Loop = PlayerLoop minus WaitForLastPresentation (the editor's present throttling while Unity is in the background)
        public float[] Loop = Array.Empty<float>(), Editor = Array.Empty<float>(), RenderCams = Array.Empty<float>(), GpuCounter = Array.Empty<float>();
        public int Stalls;
        public long[][] Counters;
        public int ProfFirst = -1, ProfLast = -1;
        public float GameCams, SceneCams, OtherCams;
        public string Note = "";
    }

    // ---------------- entries ----------------

    public static async Task<string> Views(string views = DefaultViews, float seconds = 3f, bool profile = false, float hour = 9f)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header($"views {hour}h");
        using var s = new Session(input: false, profile);
        using var frozen = new FrozenClock(hour);
        await Seconds(6f);
        var player = Player;
        foreach (var v in views.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = v.Split(':')[0];
            var n = v.Split(':')[1].Split(',');
            Vector3 at = n[0] == "spawn" ? player.SpawnPosition : Ground(float.Parse(n[0]), float.Parse(n[1]));
            float yaw = float.Parse(n[^1]);
            Teleport(player, at, yaw, 0f);
            await Seconds(2f);
            var seg = await Measure(name, s, seconds, null, profile);
            sb.AppendLine(Row(seg));
            if (profile) sb.Append(Analyze(seg, 10, 4));
        }
        return Finish($"views_{hour:00}", sb);
    }

    // Home life's cost (clock frozen at `hour`, partly cloudy and breezy): the player in the yard facing the pen, measured with every
    // animal, robot and the dog on, then with each group switched off (SetActive false on their GameObjects), then none.
    public static async Task<string> HomeLife(float seconds = 6f, bool profile = true, float hour = 9f)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header($"home life {hour}h");
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        sb.AppendLine("weather: " + resolver.Resolve<MB13.Aipocalypse.Core.Debugging.CommandRegistry>().Execute("weather.set PartlyCloudy Breezy 0").Message);
        using var s = new Session(input: false, profile);
        using var frozen = new FrozenClock(hour);
        await Seconds(4f);
        var pen = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.AnimalSystem.AnimalPen>();
        var yard = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.RobotSystem.RobotYard>();
        Vector3 toPen = Vector3.ProjectOnPlane(pen.transform.position - yard.transform.position, Vector3.up).normalized;
        Vector3 at = pen.transform.position - toPen * 14f;
        Teleport(Player, Ground(at.x, at.z), Quaternion.LookRotation(toPen).eulerAngles.y, 0f);
        GameObject[] Group<T>() where T : Component => Object.FindObjectsByType<T>().Select(c => c.gameObject).ToArray();
        GameObject[] dog = Group<MB13.Aipocalypse.Core.DogSystem.Dog>();
        GameObject[] animals = Group<MB13.Aipocalypse.Core.AnimalSystem.Animal>();
        GameObject[] robots = Group<MB13.Aipocalypse.Core.RobotSystem.Robot>();
        sb.AppendLine($"dog {dog.Length}, animals {animals.Length}, robots {robots.Length}");
        (string Name, GameObject[][] Off)[] cases =
        {
            ("all_on", new GameObject[0][]), ("no_dog", new[] { dog }), ("no_animals", new[] { animals }), ("no_robots", new[] { robots }),
            ("none", new[] { dog, animals, robots }),
        };
        try
        {
            foreach ((string name, GameObject[][] off) in cases)
            {
                foreach (GameObject go in off.SelectMany(g => g))
                    go.SetActive(false);
                await Seconds(2f);
                var seg = await Measure(name, s, seconds, null, profile);
                sb.AppendLine(Row(seg));
                if (profile) sb.Append(Analyze(seg, 14, 2));
                foreach (GameObject go in off.SelectMany(g => g))
                    go.SetActive(true);
            }
        }
        finally
        {
            foreach (GameObject go in dog.Concat(animals).Concat(robots))
                if (go != null)
                    go.SetActive(true);
        }
        return Finish($"homelife_{hour:00}", sb);
    }

    // At the spawn by day (clock frozen at `hour`): idle, look, walk, sprint, jump, then with guns: idle, hip fire, aim fire,
    // fire while turning, weapon switching, reload, then melee.
    public static async Task<string> Actions(float seconds = 5f, float hour = 9f, string only = "")
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header("actions");
        var player = Player;
        var clock = Clock;
        clock?.SetTime(WorldTime.At(clock.Now.Day, hour));
        clock?.Freeze("PerfSuite");
        var jams = new List<(JamSettings jam, float chance)>();
        using var s = new Session(input: true, profile: true);
        try
        {
            player.Ammo.Infinite = true;
            Vector3 spawn = player.SpawnPosition;
            const float yaw = 50f;
            await Seconds(6f); // the look transition after the time set
            var pitchHold = PitchSetter(player);

            async Task Run(string name, Func<int, (Key[] keys, Vector2 delta, bool lmb, bool rmb)> drive, bool holdPitch = true, bool reset = true)
            {
                if (only.Length > 0 && !only.Split(',').Contains(name)) return;
                if (reset) { Teleport(player, spawn, yaw, 0f); await Seconds(1f); }
                var seg = await Measure(name, s, seconds, i =>
                {
                    var d = drive(i);
                    s.Send(d.keys, d.delta, d.lmb, d.rmb);
                    if (holdPitch) pitchHold(0f);
                    AutoReload(player, s, d.keys, i);
                }, true);
                s.Send(Array.Empty<Key>(), Vector2.zero, false, false);
                seg.Note = $"pos {player.transform.position:F0} held {player.HeldItem?.name ?? "-"}";
                sb.AppendLine(Row(seg));
                sb.Append(Analyze(seg, 14, 3));
                await Frames(10);
            }

            var none = Array.Empty<Key>();
            await Run("idle", _ => (none, Vector2.zero, false, false));
            await Run("look", i => (none, new Vector2(10f, Mathf.Sin(i * 0.05f) * 3f), false, false), holdPitch: false);
            await Run("walk", _ => (new[] { Key.W }, Vector2.zero, false, false));
            await Run("sprint", _ => (new[] { Key.W, Key.LeftShift }, Vector2.zero, false, false));
            await Run("jump", i => (i % 60 < 3 ? new[] { Key.W, Key.Space } : new[] { Key.W }, Vector2.zero, false, false));

            foreach (var item in new[] { "AK47", "Pistol", "FireAxe" })
                Give(player, item);
            foreach (var g in Resources.FindObjectsOfTypeAll<GunDefinition>())
            {
                jams.Add((g.Jam, g.Jam.Chance));
                g.Jam.Chance = 0f;
            }
            player.Equipment.Select(0);
            await Seconds(1.5f);
            await Run("gun_idle", _ => (none, Vector2.zero, false, false));
            await Run("fire_hip", _ => (none, Vector2.zero, true, false));
            await Run("fire_aim", _ => (none, Vector2.zero, true, true));
            await Run("fire_look", i => (none, new Vector2(10f, 0f), true, false));
            await Run("switch", i => ((i / 40) % 2 == 0 ? new[] { Key.Digit1 } : new[] { Key.Digit2 }, Vector2.zero, false, false));
            player.Equipment.Select(0);
            await Seconds(1f);
            await Run("reload", i => (i % 300 is >= 60 and < 63 ? new[] { Key.R } : none, Vector2.zero, i % 300 < 40, false));
            player.Equipment.Select(2);
            await Seconds(1f);
            await Run("melee", _ => (none, Vector2.zero, true, false));
            player.Equipment.Select(0);
        }
        finally
        {
            foreach (var (jam, chance) in jams) jam.Chance = chance;
            player.Ammo.Infinite = false;
            clock?.Release("PerfSuite");
        }
        return Finish("actions", sb);
    }

    // First-use hitches in a fresh play session (run right after entering play mode): pickups, first shots, the pools filling
    // during sustained fire, first switch, first reload, first melee. Reports each phase's worst frames.
    public static async Task<string> FirstUse(float hour = 9f)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header("firstuse");
        var player = Player;
        using var frozen = new FrozenClock(hour);
        var jams = new List<(JamSettings jam, float chance)>();
        using var s = new Session(input: true, profile: true);
        try
        {
            player.Ammo.Infinite = true;
            foreach (var g in Resources.FindObjectsOfTypeAll<GunDefinition>()) { jams.Add((g.Jam, g.Jam.Chance)); g.Jam.Chance = 0f; }
            Teleport(player, player.SpawnPosition, 50f, 0f);
            await Seconds(6f);
            var pitchHold = PitchSetter(player);
            var none = Array.Empty<Key>();

            async Task Run(string name, float seconds, Action before, Func<int, (Key[] keys, bool lmb, bool rmb)> drive)
            {
                before?.Invoke();
                var seg = await Measure(name, s, seconds, i =>
                {
                    var d = drive(i);
                    s.Send(d.keys, Vector2.zero, d.lmb, d.rmb);
                    pitchHold(0f);
                    AutoReload(player, s, d.keys, i);
                }, true);
                s.Send(none, Vector2.zero, false, false);
                sb.AppendLine(Row(seg));
                sb.Append(Analyze(seg, 8, 3));
                await Frames(20);
            }

            await Run("baseline", 2f, null, _ => (none, false, false));
            await Run("pickup AK47", 2f, () => Give(player, "AK47"), _ => (none, false, false));
            await Run("first shots", 2f, null, _ => (none, true, false));
            await Run("sustained fire", 8f, null, _ => (none, true, false));
            await Run("pickup Pistol", 2f, () => Give(player, "Pistol"), _ => (none, false, false));
            await Run("first switch", 3f, null, i => ((i / 60) % 2 == 0 ? new[] { Key.Digit1 } : new[] { Key.Digit2 }, false, false));
            player.Equipment.Select(1);
            await Seconds(1f);
            await Run("pistol fire+reload", 4f, null, i => (i % 240 is >= 100 and < 103 ? new[] { Key.R } : none, i % 240 < 80, false));
            await Run("pickup FireAxe", 2f, () => Give(player, "FireAxe"), _ => (none, false, false));
            await Run("first melee", 3f, null, _ => (none, true, false));
        }
        finally
        {
            foreach (var (jam, chance) in jams) jam.Chance = chance;
            player.Ammo.Infinite = false;
        }
        return Finish("firstuse", sb);
    }

    // Sets the clock just before each hour (running at normal speed), then measures the look transition and the hold after it.
    // Then the night lights: no lantern, a worn lantern, a lantern with shadows.
    public static async Task<string> DayNight(string hours = "6,8,12,17,18,19,20,0,5", float transition = 6.5f, float hold = 4f)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header("daynight");
        var player = Player;
        var clock = Clock;
        var director = Object.FindAnyObjectByType<EnvironmentDirector>();
        if (clock == null) return "no clock";
        using var s = new Session(input: false, profile: true);
        Teleport(player, player.SpawnPosition, 50f, 0f);
        foreach (var h in hours.Split(',').Select(float.Parse))
        {
            clock.SetTime(WorldTime.At(clock.Now.Day + (h < clock.Now.Hour ? 1 : 0), h - 1f / 60f)); // 0.5 s before the hour
            await Frames(2);
            var t = await Measure($"to {h:00}h", s, transition, null, true);
            t.Note = $"look {director.LookHour:F2} sun {RenderSettings.sun?.name} shadows {RenderSettings.sun?.shadows} vision {LightInfo(player)}";
            sb.AppendLine(Row(t));
            sb.Append(Analyze(t, 8, 2));
            var hd = await Measure($"hold {h:00}h", s, hold, null, false);
            hd.Note = t.Note;
            sb.AppendLine(Row(hd));
        }

        clock.SetTime(WorldTime.At(clock.Now.Day + 1, 22f));
        clock.Freeze("PerfSuite");
        try
        {
            await Seconds(7f);
            sb.AppendLine(Row(await Measure("night 22h", s, hold, null, false), LightInfo(player)));
            var lantern = AssetDatabase.LoadAssetAtPath<LanternDefinition>("Assets/_Game/Content/Items/Lantern/Lantern.asset");
            player.Equipment.TryPickUp(lantern.CreateInstance(), out _);
            await Seconds(2f);
            var lit = await Measure("night lantern", s, hold, null, true);
            sb.AppendLine(Row(lit, LightInfo(player)));
            sb.Append(Analyze(lit, 8, 2));
            var light = VisionLight(player);
            var shadows = light.shadows;
            light.shadows = LightShadows.Soft;
            await Seconds(3f);
            sb.AppendLine(Row(await Measure("night lantern+shadows", s, hold, null, false), LightInfo(player)));
            light.shadows = shadows;
            typeof(Equipment).GetProperty(nameof(Equipment.Belt)).SetValue(player.Equipment, null);
            await Seconds(2f);
        }
        finally
        {
            clock.Release("PerfSuite");
        }
        return Finish("daynight", sb);
    }

    // Audio module counters (CPU, voices, memory) at the spawn: idle, hip fire, reload, then day phase changes (ambience beds and
    // calls fading); ends with the pooled AudioSources.
    public static async Task<string> Audio(float seconds = 5f, string hours = "6,18,20")
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header("audio");
        var player = Player;
        var clock = Clock;
        var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        var defs = handles.Select(Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription).Where(d => d.Category == ProfilerCategory.Audio).OrderBy(d => d.Name).ToArray();
        var jams = new List<(JamSettings jam, float chance)>();
        using var s = new Session(input: true, profile: false);

        async Task Run(string name, float secs, Action<int> drive)
        {
            var recs = defs.Select(d => ProfilerRecorder.StartNew(d.Category, d.Name, 6000)).ToArray();
            var seg = await Measure(name, s, secs, drive, false);
            sb.AppendLine(Row(seg));
            sb.Append("    ");
            for (int k = 0; k < defs.Length; k++)
            {
                var v = Samples(recs[k], 1f);
                recs[k].Dispose();
                if (v.Length == 0 || v.Max() == 0f) continue;
                bool time = defs[k].UnitType == ProfilerMarkerDataUnit.TimeNanoseconds, bytes = defs[k].UnitType == ProfilerMarkerDataUnit.Bytes;
                float scale = time ? 1e-6f : bytes ? 1f / (1024f * 1024f) : 1f;
                string unit = time ? "ms" : bytes ? "MB" : "";
                sb.Append($"{defs[k].Name} {Median(v) * scale:0.###}/{v.Max() * scale:0.###}{unit}  ");
            }
            sb.AppendLine();
            await Frames(10);
        }

        try
        {
            clock?.SetTime(WorldTime.At(clock.Now.Day, 9f));
            clock?.Freeze("PerfSuite");
            player.Ammo.Infinite = true;
            Teleport(player, player.SpawnPosition, 50f, 0f);
            await Seconds(6f);
            var none = Array.Empty<Key>();
            await Run("idle 9h", seconds, null);
            Give(player, "AK47");
            foreach (var g in Resources.FindObjectsOfTypeAll<GunDefinition>())
            {
                jams.Add((g.Jam, g.Jam.Chance));
                g.Jam.Chance = 0f;
            }
            player.Equipment.Select(0);
            await Seconds(1.5f);
            var pitchHold = PitchSetter(player);
            await Run("fire_hip", seconds, i => { s.Send(none, Vector2.zero, true, false); pitchHold(0f); AutoReload(player, s, none, i); });
            s.Send(none, Vector2.zero, false, false);
            await Run("reload", seconds, i => s.Send(i % 120 is >= 10 and < 13 ? new[] { Key.R } : none, Vector2.zero, false, false));
            s.Send(none, Vector2.zero, false, false);
            clock?.Release("PerfSuite");
            foreach (var h in hours.Split(',').Select(float.Parse))
            {
                clock.SetTime(WorldTime.At(clock.Now.Day + (h < clock.Now.Hour ? 1 : 0), h - 1f / 60f));
                await Frames(2);
                await Run($"to {h:00}h", 6.5f, null);
            }
        }
        finally
        {
            foreach (var (jam, chance) in jams) jam.Chance = chance;
            player.Ammo.Infinite = false;
            clock?.Release("PerfSuite");
        }

        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        sb.AppendLine($"AudioSources {sources.Length}, playing {sources.Count(a => a.isPlaying)}, 3D {sources.Count(a => a.spatialBlend > 0f)}, listeners {Object.FindObjectsByType<AudioListener>().Length}");
        foreach (var a in sources.Where(a => a.isPlaying || a.clip != null).Take(20))
            sb.AppendLine($"  {a.name}: {a.clip?.name} ch {a.clip?.channels} {a.clip?.loadType} blend {a.spatialBlend} playing {a.isPlaying} group {(a.outputAudioMixerGroup ? a.outputAudioMixerGroup.name : "-")}");
        return Finish("audio", sb);
    }

    // Enemy crowd cost: hides the scene's enemies, spawns goblins (cycling the looks) on a growing disc in front of the player,
    // and measures each count facing the crowd (everything) and facing away (no goblin on screen: think, move, animate,
    // shadows). `ab` then measures runtime variants at the largest count (cull, nodebug, nolayers, noshadow, lodN = force Mesh LOD N, thrX = Mesh LOD threshold X, biasX = goblin Mesh LOD bias X, base),
    // each restored after. Spawned goblins are destroyed and the scene's restored at the end unless `keep`.
    public static async Task<string> Crowd(string counts = "0,10,25,50,100,200", float seconds = 4f, float hour = 9f, bool profile = true, bool keep = false,
        string ab = "")
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header("crowd");
        var player = Player;
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies/Goblin" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p.GetComponent<EnemyBrain>() != null).OrderBy(p => p.name).ToArray();
        var sceneEnemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
        var steps = counts.Split(',').Select(int.Parse).ToArray();
        float Radius(int n) => 1.4f * Mathf.Sqrt(Mathf.Max(1, n));

        Vector3 center = Vector3.zero;
        foreach (var e in sceneEnemies) center += e.transform.position;
        center /= Mathf.Max(1, sceneEnemies.Length);
        Vector3 away = Vector3.ProjectOnPlane(center - player.SpawnPosition, Vector3.up).normalized;
        float back = Radius(steps.Max()) + 12f;
        Vector3 stand = Ground(center.x + away.x * back, center.z + away.z * back);
        float facing = Quaternion.LookRotation(-away).eulerAngles.y;

        var markerNames = new[]
        {
            "Aipocalypse.EnemyManager.Think", "Aipocalypse.EnemyAnimationManager.Animate", "PreLateUpdate.DirectorUpdateAnimationBegin",
            "PreLateUpdate.DirectorUpdateAnimationEnd", "PostLateUpdate.UpdateAllSkinnedMeshes", "Update.ScriptRunBehaviourUpdate",
            "FixedUpdate.PhysicsFixedUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
        };
        var available = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(available);
        var descs = available.Select(ProfilerRecorderHandle.GetDescription).GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.First());

        sb.AppendLine($"looks {prefabs.Length} ({string.Join(",", prefabs.Select(p => p.name))}) | scene enemies hidden {sceneEnemies.Length} | crowd centre {center:F0} stand {stand:F0} yaw {facing:F0}");
        sb.AppendLine("markers (median ms/frame): " + string.Join(", ", markerNames.Select((m, k) => $"m{k}={m}{(descs.ContainsKey(m) ? "" : " (n/a)")}")));
        sb.AppendLine("A* / ECS markers seen: " + string.Join(", ", descs.Keys.Where(k => k.Contains("Follower") || k.Contains("RVO") || k.Contains("SimulationSystemGroup") || k.Contains("Pathfind")).Take(12)));

        var spawned = new List<GameObject>();
        using var s = new Session(input: false, profile: false);
        using var frozen = new FrozenClock(hour);
        var pitch = PitchSetter(player);
        try
        {
            foreach (var e in sceneEnemies) e.gameObject.SetActive(false);
            Teleport(player, stand, facing, 0f);
            await Seconds(4f);

            async Task<Seg> Step(string name, float yaw)
            {
                Teleport(player, stand, yaw, 0f);
                await Seconds(1.5f);
                var recs = markerNames.Where(descs.ContainsKey).Select(m => (name: m, rec: ProfilerRecorder.StartNew(descs[m].Category, m, 6000))).ToList();
                var seg = await Measure(name, s, seconds, _ => pitch(0f), false);
                var planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
                int visible = spawned.Count(g => GeometryUtility.TestPlanesAABB(planes, g.GetComponentInChildren<SkinnedMeshRenderer>().bounds));
                int moving = spawned.Count(g => g.GetComponent<EnemyMover>().Velocity.sqrMagnitude > 0.04f);
                seg.Note = $"on-screen {visible} moving {moving} | " +
                           string.Join(" ", recs.Select(r => $"m{Array.IndexOf(markerNames, r.name)} {Median(Samples(r.rec, 1e-6f)):F2}"));
                foreach (var r in recs) r.rec.Dispose();
                return seg;
            }

            foreach (int n in steps)
            {
                while (spawned.Count < n)
                {
                    int i = spawned.Count;
                    float a = i * 2.39996f, r = 1.4f * Mathf.Sqrt(i + 0.5f);
                    Vector3 at = Ground(center.x + Mathf.Cos(a) * r, center.z + Mathf.Sin(a) * r);
                    var go = Object.Instantiate(prefabs[i % prefabs.Length], at, Quaternion.Euler(0f, i * 137 % 360, 0f));
                    go.name = $"Crowd Goblin {i}";
                    resolver.InjectGameObject(go);
                    spawned.Add(go);
                }
                await Seconds(3f);
                sb.AppendLine(Row(await Step($"{n} facing", facing)));
                sb.AppendLine(Row(await Step($"{n} away", facing + 180f)));
            }
            foreach (string variant in ab.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var undo = new List<Action>();
                foreach (var go in spawned)
                {
                    var animator = go.GetComponentInChildren<Animator>();
                    var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                    var follower = go.GetComponent<Pathfinding.FollowerEntity>();
                    switch (variant)
                    {
                        case "cull":
                            var culling = animator.cullingMode;
                            animator.cullingMode = AnimatorCullingMode.CullCompletely;
                            undo.Add(() => animator.cullingMode = culling);
                            break;
                        case "nodebug":
                            var flags = follower.debugFlags;
                            follower.debugFlags = Pathfinding.PID.PIDMovement.DebugFlags.Nothing;
                            undo.Add(() => follower.debugFlags = flags);
                            break;
                        case "nolayers":
                            for (int l = 1; l < animator.layerCount; l++)
                            {
                                int layer = l;
                                float weight = animator.GetLayerWeight(layer);
                                animator.SetLayerWeight(layer, 0f);
                                undo.Add(() => animator.SetLayerWeight(layer, weight));
                            }
                            break;
                        case "noshadow":
                            var shadows = skin.shadowCastingMode;
                            skin.shadowCastingMode = ShadowCastingMode.Off;
                            undo.Add(() => skin.shadowCastingMode = shadows);
                            break;
                        case { } thr when thr.StartsWith("thr"):
                            float threshold = QualitySettings.meshLodThreshold;
                            QualitySettings.meshLodThreshold = float.Parse(thr[3..]);
                            undo.Add(() => QualitySettings.meshLodThreshold = threshold);
                            break;
                        case { } bias when bias.StartsWith("bias"):
                            float oldBias = skin.meshLodSelectionBias;
                            skin.meshLodSelectionBias = float.Parse(bias[4..]);
                            undo.Add(() => skin.meshLodSelectionBias = oldBias);
                            break;
                        case { } lod when lod.StartsWith("lod"):
                            var forced = skin.forceMeshLod;
                            skin.forceMeshLod = short.Parse(lod[3..]);
                            undo.Add(() => skin.forceMeshLod = forced);
                            break;
                    }
                }
                await Seconds(1f);
                sb.AppendLine(Row(await Step($"{steps.Max()} {variant} facing", facing)));
                sb.AppendLine(Row(await Step($"{steps.Max()} {variant} away", facing + 180f)));
                for (int u = undo.Count - 1; u >= 0; u--) undo[u]();
            }
            Teleport(player, stand, facing, 0f);

            if (profile && steps.Max() > 0)
            {
                using var ps = new Session(input: false, profile: true);
                await Seconds(1.5f);
                var seg = await Measure($"{steps.Max()} profiled", ps, seconds, _ => pitch(0f), true);
                sb.AppendLine(Row(seg));
                sb.Append(Analyze(seg, 24, 2));
            }
        }
        finally
        {
            if (!keep)
            {
                foreach (var go in spawned) Object.Destroy(go);
                foreach (var e in sceneEnemies) if (e != null) e.gameObject.SetActive(true);
            }
        }
        return Finish("crowd", sb);
    }

    static readonly string[] FightMarkers =
    {
        "Aipocalypse.BulletSystem.Advance", "Aipocalypse.DamageService.Deal", "Aipocalypse.DamageService.Publish", "Aipocalypse.SpawnService.Spawn",
        "Aipocalypse.PrefabPool.Prewarm", "Aipocalypse.EnemyManager.Think", "Aipocalypse.EnemyBodyManager.React", "Aipocalypse.EnemyAnimationManager.Animate",
    };

    // A night raid fight on the raid approach: the player stands `standOut` m out from the home base along the raid SpawnZone's
    // forward, and `count` goblins of the scene's first spawn card come through the SpawnService (prewarmed pool, appear)
    // `distance` m further out, topped up as they die, while the player (god mode, AK with infinite ammo, no jams) holds fire
    // at the nearest one. Measures the approach without firing, then the fight (profiled) with its hits, kills, staggers and
    // hit-stop frames, the worst frames' main-thread call trees, and with `gc` the fight's GC.Alloc stacks.
    // Run it right after entering play mode for the cold numbers (first blood, first deaths), then again for warm ones.
    public static async Task<string> Fight(int count = 25, float seconds = 10f, float hour = 22f, float distance = 24f, int dumps = 3,
        float dumpMs = 0.3f, bool gc = false, float aimHeight = 1.2f, float standOut = 35f, int breakdownDepth = 0)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header($"fight {count} at {hour}h");
        var player = Player;
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var commands = resolver.Resolve<CommandRegistry>();
        var spawner = resolver.Resolve<MB13.Aipocalypse.Core.SpawnSystem.SpawnService>();
        var card = resolver.Resolve<EnemySystemSettings>().Cards[0];
        var zone = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.SpawnSystem.SpawnZone>();
        Vector3 home = zone != null ? zone.Origin : player.SpawnPosition;
        Vector3 away = Vector3.ProjectOnPlane(zone != null ? zone.transform.forward : player.transform.forward, Vector3.up).normalized;
        Vector3 stand = Ground(home.x + away.x * standOut, home.z + away.z * standOut);
        Vector3 center = Ground(stand.x + away.x * distance, stand.z + away.z * distance);
        float facing = Quaternion.LookRotation(away).eulerAngles.y;

        int hits = 0, kills = 0, staggers = 0, crits = 0, stops = 0, stopFrames = 0;
        bool inStop = false;
        using var dealt = resolver.Resolve<ISubscriber<DamageDealt>>().Subscribe(e =>
        {
            if (e.Info.Source != player.gameObject) return;
            hits++;
            if (e.Result.Killed) kills++;
            if (e.Result.Staggered) staggers++;
            if (e.Info.Critical) crits++;
        });
        var group = new List<EnemyBrain>();
        var handles = new List<MB13.Aipocalypse.Core.SpawnSystem.SpawnHandle>();
        var alive = new List<MB13.Aipocalypse.Core.SpawnSystem.Spawnable>();
        int Alive()
        {
            alive.Clear();
            group.Clear();
            int pending = 0;
            foreach (var h in handles) { h.GetAlive(alive); pending += h.Pending; }
            foreach (var a in alive) if (a.TryGetComponent(out EnemyBrain e) && !e.IsDead) group.Add(e);
            return group.Count + pending;
        }
        void TopUp()
        {
            int missing = count - Alive();
            if (missing > 0) handles.Add(spawner.Spawn(MB13.Aipocalypse.Core.SpawnSystem.SpawnRequest.At(card, center, missing).Facing(stand - center)));
        }

        var jams = new List<(JamSettings jam, float chance)>();
        var pitch = PitchSetter(player);
        using var frozen = new FrozenClock(hour);
        using var s = new Session(input: true, profile: true);
        var none = Array.Empty<Key>();
        try
        {
            // a raid the hour change started would add its own waves; a death left waiting for its confirm keeps gameplay input off
            await Frames(2);
            foreach (string line in new[] { "cheat.god on", "raid.stop", "enemy.clear" }) commands.Execute(line);
            spawner.CancelQueued();
            var respawn = resolver.Resolve<PlayerRespawnFlow>();
            for (int k = 0; k < 600 && respawn.Stage != RespawnStage.None; k++)
            {
                s.Send(none, Vector2.zero, k % 2 == 0, false);
                await NextFrame();
            }
            player.Ammo.Infinite = true;
            foreach (var g in Resources.FindObjectsOfTypeAll<GunDefinition>()) { jams.Add((g.Jam, g.Jam.Chance)); g.Jam.Chance = 0f; }
            if (!(player.Equipment.Selected is GunInstance))
            {
                Give(player, "AK47");
                await Frames(2);
                for (int slot = 0; slot < 9 && !(player.Equipment.Selected is GunInstance); slot++) player.Equipment.Select(slot);
            }
            Teleport(player, stand, facing, 0f);
            // as a raid does: room for the bodies still lying
            int headroom = resolver.TryResolve(out MB13.Aipocalypse.Core.RaidSystem.RaidSettings raid) ? raid.CorpseHeadroom : 0;
            spawner.Prewarm(card, count + headroom);
            await Seconds(3f);

            void Aim(int i)
            {
                EnemyBrain target = null;
                float best = float.MaxValue;
                foreach (var e in group)
                {
                    if (e == null || !e.isActiveAndEnabled || e.IsDead) continue;
                    float d = (e.transform.position - stand).sqrMagnitude;
                    if (d < best) { best = d; target = e; }
                }
                if (target == null)
                {
                    // nothing to shoot (count 0, the night baseline): hold the same view as a fight
                    player.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                    pitch(3f);
                    return;
                }
                Vector3 eye = Camera.main.transform.position;
                Vector3 to = target.transform.position + Vector3.up * aimHeight - eye;
                float flat = new Vector2(to.x, to.z).magnitude;
                player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + Mathf.Sin(i * 0.15f) * 2f, 0f);
                pitch(-Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg);
            }

            TopUp();
            await Seconds(4.5f); // spawned over a few frames, then each stands through its appear
            var approach = await Measure("approach", s, 3f, i => { s.Send(none, Vector2.zero, false, false); Aim(i); }, true);
            approach.Note = $"alive {Alive()} stand {stand:F0} yaw {facing:F0}";
            sb.AppendLine(Row(approach));
            sb.Append(Analyze(approach, 12, 2));

            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var categories = available.Select(ProfilerRecorderHandle.GetDescription).GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.First().Category);
            var markers = FightMarkers.Select(m => (name: m, rec: ProfilerRecorder.StartNew(categories.TryGetValue(m, out var c) ? c : ProfilerCategory.Scripts, m, 6000))).ToList();
            var fight = await Measure("fight", s, seconds, i =>
            {
                s.Send(none, Vector2.zero, true, false);
                Aim(i);
                AutoReload(player, s, none, i);
                if (Time.timeScale < 0.99f) { stopFrames++; if (!inStop) stops++; inStop = true; }
                else inStop = false;
                if (i % 30 == 0) TopUp();
            }, true);
            s.Send(none, Vector2.zero, false, false);
            fight.Note = $"hits {hits} crit {crits} kills {kills} staggered {staggers} | hit stops {stops} ({stopFrames} frames) | alive {Alive()}";
            sb.AppendLine(Row(fight));
            sb.AppendLine("   markers ms/frame med/p99/max: " + string.Join("; ", markers.Select(m =>
            {
                var v = Samples(m.rec, 1e-6f);
                return $"{m.name.Replace("Aipocalypse.", "")} {Median(v):F2}/{Pct(v, .99f):F2}/{Max(v):F2}";
            })));
            foreach (var m in markers) m.rec.Dispose();
            sb.Append(Analyze(fight, 20, 5));
            sb.Append(Breakdown(fight.ProfFirst, fight.ProfLast, breakdownDepth, 0.05f));
            foreach (int f in WorstFrames(fight.ProfFirst, fight.ProfLast, dumps))
                sb.Append(DumpFrame(f, dumpMs));

            if (gc)
            {
                var prevMode = ProfilerDriver.memoryRecordMode;
                ProfilerDriver.memoryRecordMode = ProfilerMemoryRecordMode.GCAlloc;
                int first = ProfilerDriver.lastFrameIndex + 2;
                await Measure("fight gc", s, 4f, i =>
                {
                    s.Send(none, Vector2.zero, true, false);
                    Aim(i);
                    AutoReload(player, s, none, i);
                    if (i % 30 == 0) TopUp();
                }, false);
                s.Send(none, Vector2.zero, false, false);
                ProfilerDriver.memoryRecordMode = prevMode;
                sb.Append(GcReport("fight", first, ProfilerDriver.lastFrameIndex, 8, 15));
            }
        }
        finally
        {
            s.Send(none, Vector2.zero, false, false);
            foreach (var (jam, chance) in jams) jam.Chance = chance;
            player.Ammo.Infinite = false;
            spawner.CancelQueued();
            commands.Execute("raid.stop");
            commands.Execute("enemy.clear");
            commands.Execute("cheat.god off");
        }
        return Finish("fight", sb);
    }

    // A/B of runtime toggles at the spawn, averaged over four headings. "baseline" runs first and last to catch drift.
    // `only`: comma-separated config names; empty = all. Every change is restored.
    public static async Task<string> Ab(string only = "", int frames = 50, float hour = -1f, string at = "spawn")
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var sb = Header("ab");
        var player = Player;
        var clock = Clock;
        if (hour >= 0f && clock != null)
        {
            clock.SetTime(WorldTime.At(clock.Now.Day, hour));
            clock.Freeze("PerfSuite");
            await Seconds(6f);
        }
        Vector3 pos = at == "spawn" ? player.SpawnPosition : Ground(float.Parse(at.Split(',')[0]), float.Parse(at.Split(',')[1]));
        using var s = new Session(input: false, profile: false);
        var rp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var rpSo = new SerializedObject(rp);
        var rd = (UniversalRendererData)rpSo.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
        var cam = Camera.main;
        var camData = cam.GetUniversalAdditionalCameraData();
        var terrain = Terrain.activeTerrain;
        var restore = new List<Action>();
        void Keep<T>(Func<T> get, Action<T> set) { T v = get(); restore.Add(() => set(v)); }
        void SoProp(string prop, int value) { rpSo.Update(); rpSo.FindProperty(prop).intValue = value; rpSo.ApplyModifiedPropertiesWithoutUndo(); }
        int SoGet(string prop) { rpSo.Update(); return rpSo.FindProperty(prop).intValue; }
        void Feature(string type, bool on) { foreach (var f in rd.rendererFeatures) if (f != null && f.GetType().Name == type) f.SetActive(on); }
        void Post<T>(bool on) where T : VolumeComponent
        {
            foreach (var v in Object.FindObjectsByType<Volume>())
                if (v.sharedProfile != null && v.sharedProfile.TryGet(out T c)) c.active = on;
        }
        void Root(string name, bool on) { var go = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.parent == null && t.name == name); if (go) go.gameObject.SetActive(on); }
        GameObject Find(string path) => GameObject.Find(path);

        Keep(() => rp.shadowDistance, v => rp.shadowDistance = v);
        Keep(() => rp.shadowCascadeCount, v => rp.shadowCascadeCount = v);
        Keep(() => rp.mainLightShadowmapResolution, v => rp.mainLightShadowmapResolution = v);
        Keep(() => SoGet("m_SoftShadowQuality"), v => SoProp("m_SoftShadowQuality", v));
        Keep(() => rp.renderScale, v => rp.renderScale = v);
        Keep(() => rp.gpuResidentDrawerEnableOcclusionCullingInCameras, v => rp.gpuResidentDrawerEnableOcclusionCullingInCameras = v);
        Keep(() => SoGet("m_EnableLODCrossFade"), v => SoProp("m_EnableLODCrossFade", v));
        Keep(() => rp.supportsCameraOpaqueTexture, v => rp.supportsCameraOpaqueTexture = v);
        Keep(() => SoGet("m_SupportsLightCookies"), v => SoProp("m_SupportsLightCookies", v));
        Keep(() => SoGet("m_VolumeFrameworkUpdateMode"), v => SoProp("m_VolumeFrameworkUpdateMode", v));
        Keep(() => camData.renderPostProcessing, v => camData.renderPostProcessing = v);
        Keep(() => QualitySettings.lodBias, v => QualitySettings.lodBias = v);
        Keep(() => terrain.basemapDistance, v => terrain.basemapDistance = v);
        Keep(() => terrain.heightmapPixelError, v => terrain.heightmapPixelError = v);
        Keep(() => terrain.detailObjectDistance, v => terrain.detailObjectDistance = v);
        Keep(() => terrain.treeDistance, v => terrain.treeDistance = v);
        Keep(() => cam.farClipPlane, v => cam.farClipPlane = v);
        Keep(() => QualitySettings.streamingMipmapsActive, v => QualitySettings.streamingMipmapsActive = v);
        var waterFeature = rd.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "StylizedWaterRenderFeature");
        var heightSettings = waterFeature?.GetType().GetField("heightPrePassSettings")?.GetValue(waterFeature);
        var heightEnable = heightSettings?.GetType().GetField("enable");
        if (heightEnable != null) Keep(() => (bool)heightEnable.GetValue(heightSettings), v => heightEnable.SetValue(heightSettings, v));
        foreach (var f in rd.rendererFeatures.Where(f => f != null)) { var feat = f; Keep(() => feat.isActive, v => feat.SetActive(v)); }
        foreach (var v in Object.FindObjectsByType<Volume>())
            if (v.sharedProfile != null)
                foreach (var c in v.sharedProfile.components) { var comp = c; Keep(() => comp.active, a => comp.active = a); }
        foreach (var name in new[] { "Advanced FPS Counter", "HUD" }) { var go = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.parent == null && t.name == name); if (go) { var g = go.gameObject; Keep(() => g.activeSelf, a => g.SetActive(a)); } }
        var atmosphere = Find("Environment/Atmosphere");
        if (atmosphere) Keep(() => atmosphere.activeSelf, a => atmosphere.SetActive(a));
        var water = Find("Environment/Water");
        if (water) Keep(() => water.activeSelf, a => water.SetActive(a));
        var sun = RenderSettings.sun;
        Keep(() => sun.cookie, c => sun.cookie = c);
        // COZY's weather-blocking camera (renders the scene from above so rain stops under roofs).
        var fxCamera = Camera.allCameras.FirstOrDefault(c => c.name == "FX Blocking Camera");
        if (fxCamera) Keep(() => fxCamera.enabled, e => fxCamera.enabled = e);
        var particleRenderers = Object.FindObjectsByType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        restore.Add(() => { foreach (var r in particleRenderers) if (r) r.enabled = true; });
        void Particles(Func<ParticleSystemRenderer, bool> which) { foreach (var r in particleRenderers) if (r && which(r)) r.enabled = false; }
        GameObject[] home = Object.FindObjectsByType<MB13.Aipocalypse.Core.DogSystem.Dog>().Select(c => c.gameObject)
            .Concat(Object.FindObjectsByType<MB13.Aipocalypse.Core.AnimalSystem.Animal>().Select(c => c.gameObject))
            .Concat(Object.FindObjectsByType<MB13.Aipocalypse.Core.RobotSystem.Robot>().Select(c => c.gameObject)).ToArray();
        restore.Add(() => { foreach (var go in home) if (go) go.SetActive(true); });

        var configs = new List<(string name, Action apply)>
        {
            ("baseline", () => { }),
            ("post off", () => camData.renderPostProcessing = false),
            ("bloom off", () => Post<Bloom>(false)),
            ("tonemap off", () => Post<Tonemapping>(false)),
            ("grading off", () => { Post<ColorAdjustments>(false); Post<WhiteBalance>(false); Post<LiftGammaGain>(false); }),
            ("vignette off", () => Post<Vignette>(false)),
            ("volume update via script", () => SoProp("m_VolumeFrameworkUpdateMode", 1)),
            ("soft shadows medium", () => SoProp("m_SoftShadowQuality", 2)),
            ("soft shadows low", () => SoProp("m_SoftShadowQuality", 1)),
            ("shadows 2 cascades", () => rp.shadowCascadeCount = 2),
            ("shadowmap 1024", () => rp.mainLightShadowmapResolution = 1024),
            ("shadows off (dist 0)", () => rp.shadowDistance = 0f),
            ("sun cookie off", () => sun.cookie = null),
            ("gpu occlusion on", () => rp.gpuResidentDrawerEnableOcclusionCullingInCameras = true),
            ("lod crossfade off", () => SoProp("m_EnableLODCrossFade", 0)),
            ("render scale 0.8", () => rp.renderScale = 0.8f),
            ("opaque tex off", () => rp.supportsCameraOpaqueTexture = false),
            ("water feature off", () => Feature("StylizedWaterRenderFeature", false)),
            ("water objects off", () => { if (water) water.SetActive(false); }),
            ("grass feature off", () => Feature("GrassRenderFeature", false)),
            ("decal feature off", () => Feature("DecalRendererFeature", false)),
            ("first person off", () => Feature("FirstPersonRenderFeature", false)),
            ("terrain basemap 250", () => terrain.basemapDistance = 250f),
            ("terrain pixelError 6", () => terrain.heightmapPixelError = 6f),
            ("terrain detail 80", () => terrain.detailObjectDistance = 80f),
            ("terrain tree 500", () => terrain.treeDistance = 500f),
            ("lodBias 0.5", () => QualitySettings.lodBias = 0.5f),
            ("far clip 800", () => cam.farClipPlane = 800f),
            ("atmosphere off", () => { if (atmosphere) atmosphere.SetActive(false); }),
            ("fps counter off", () => Root("Advanced FPS Counter", false)),
            ("mip streaming off", () => QualitySettings.streamingMipmapsActive = false),
            ("water height prepass off", () => heightEnable?.SetValue(heightSettings, false)),
            ("hud off", () => Root("HUD", false)),
            ("terrain details off", () => terrain.detailObjectDistance = 0f),
            ("terrain trees off", () => terrain.treeDistance = 0f),
            ("ssao off", () => Feature("ScreenSpaceAmbientOcclusion", false)),
            ("volumetric fog off", () => Feature("VolumetricFogRenderFeature", false)),
            ("cozy feature off", () => Feature("CozyRendererFeature", false)),
            ("cozy fx camera off", () => { if (fxCamera) fxCamera.enabled = false; }),
            ("fog particles off", () => Particles(p => p.name.StartsWith("Fog"))),
            ("dust particles off", () => Particles(p => p.name.StartsWith("TEM_Dust"))),
            ("all particles off", () => Particles(_ => true)),
            ("home life off", () => { foreach (var go in home) go.SetActive(false); }),
            ("baseline again", () => { }),
        };
        var wanted = only.Length == 0 ? null : only.Split(',').Select(x => x.Trim()).ToHashSet();
        sb.AppendLine($"at {pos:F0}; per config: loop/gpu(ms) at yaw 0/90/180/270; Δ vs baseline");
        double baseLoop = 0, baseGpu = 0, baseRt = 0;
        try
        {
            foreach (var (name, apply) in configs)
            {
                if (wanted != null && name != "baseline" && name != "baseline again" && !wanted.Contains(name)) continue;
                foreach (var r in restore) r();
                apply();
                await Frames(30);
                var row = new StringBuilder();
                double loop = 0, gpu = 0, rt = 0, draws = 0;
                foreach (var yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    Teleport(player, pos, yaw, 0f);
                    await Frames(20);
                    var m = await MeasureFrames(name, s, frames);
                    float l = Median(m.Loop), g = Median(m.Gpu), r = Median(m.Render);
                    loop += l / 4; gpu += g / 4; rt += r / 4; draws += Median(m.Counters[0]) / 4.0;
                    row.Append($" {l,5:F2}/{g,4:F2}");
                }
                if (name == "baseline") { baseLoop = loop; baseGpu = gpu; baseRt = rt; }
                sb.AppendLine($"{name,-26} loop {loop,5:F2} ({loop - baseLoop:+0.00;-0.00})  rt {rt,5:F2} ({rt - baseRt:+0.00;-0.00})  gpu {gpu,5:F2} ({gpu - baseGpu:+0.00;-0.00})  draws {draws,5:F0} |{row}");
            }
        }
        finally
        {
            foreach (var r in restore) r();
            if (hour >= 0f) clock?.Release("PerfSuite");
        }
        return Finish("ab", sb);
    }

    // Frame time with as little measuring as possible: optional FrameTiming capture, memory recorder, FPS counter on/off.
    // Lists frames over `spikeMs` with their time since the start, to see if hitches are periodic.
    public static async Task<string> Bare(float seconds = 6f, bool ftm = false, bool memRecorder = false, bool fpsCounter = true, float spikeMs = 60f)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        Application.runInBackground = true;
        var counter = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.parent == null && t.name == "Advanced FPS Counter")?.gameObject;
        bool counterWas = counter && counter.activeSelf;
        if (counter) counter.SetActive(fpsCounter);
        var mem = memRecorder ? ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame") : default;
        var dts = new List<float>();
        var spikes = new List<string>();
        var timing = new FrameTiming[1];
        float t0 = Time.realtimeSinceStartup;
        var tcs = new TaskCompletionSource<bool>();
        int last = Time.frameCount;
        void Tick()
        {
            if (Time.frameCount == last) return;
            last = Time.frameCount;
            float dt = Time.unscaledDeltaTime * 1000f;
            dts.Add(dt);
            if (dt > spikeMs) spikes.Add($"{Time.realtimeSinceStartup - t0:F2}s:{dt:F0}");
            if (ftm) { FrameTimingManager.CaptureFrameTimings(); FrameTimingManager.GetLatestTimings(1, timing); }
            if (Time.realtimeSinceStartup - t0 > seconds) { EditorApplication.update -= Tick; tcs.TrySetResult(true); }
        }
        await Seconds(1f);
        t0 = Time.realtimeSinceStartup;
        EditorApplication.update += Tick;
        await tcs.Task;
        if (memRecorder) mem.Dispose();
        if (counter) counter.SetActive(counterWas);
        return $"ftm {ftm} mem {memRecorder} fpsCounter {fpsCounter}: {dts.Count}f dt med {Median(dts):F1} p95 {Pct(dts, .95f):F1} max {Max(dts):F0} | spikes>{spikeMs}ms: {string.Join(" ", spikes)}";
    }

    // Records `seconds` with the profiler, then prints the call tree (total ms >= minMs) of every thread in the worst frame.
    // Gives the player an item mid-capture and dumps the worst frame (the pickup: held item spawn, inject, prewarm).
    public static Task<string> PickupDump(string item, float minMs = 0.2f) => SpikeDump(1.5f, minMs, 1f, 1, () => Give(Player, item), profileEditor: true);

    // rank: 1 = worst frame, 0.5 = median frame (by main-thread frame time). `during` runs 0.3 s into the capture.
    public static async Task<string> SpikeDump(float seconds = 3f, float minMs = 1f, float rank = 1f, int maxThreads = 128, Action during = null, bool profileEditor = false)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        Application.runInBackground = true;
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = profileEditor;
        ProfilerDriver.enabled = true;
        if (during != null) { await Seconds(0.3f); during(); }
        await Seconds(seconds);
        ProfilerDriver.enabled = false;
        ProfilerDriver.profileEditor = false;
        var frames = new List<(int f, float ms)>();
        // the first frames carry the profiler start and the script's compile
        for (int f = ProfilerDriver.firstFrameIndex + 10; f <= ProfilerDriver.lastFrameIndex; f++)
        {
            using var v = ProfilerDriver.GetRawFrameDataView(f, 0);
            if (v != null && v.valid) frames.Add((f, v.frameTimeMs));
        }
        frames.Sort((a, b) => a.ms.CompareTo(b.ms));
        var (worst, worstMs) = frames[Mathf.Clamp((int)(rank * frames.Count), 0, frames.Count - 1)];
        var sb = new StringBuilder($"frame {worst} (rank {rank} of {frames.Count}): {worstMs:F1} ms\n");
        for (int t = 0; t < maxThreads; t++)
        {
            using var raw = ProfilerDriver.GetRawFrameDataView(worst, t);
            if (raw == null || !raw.valid) break;
            using var view = ProfilerDriver.GetHierarchyFrameDataView(worst, t, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false);
            var lines = new StringBuilder();
            void Dump(int id, int depth)
            {
                var kids = new List<int>();
                view.GetItemChildren(id, kids);
                foreach (int c in kids)
                {
                    double total = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
                    if (total < minMs) continue;
                    lines.AppendLine($"    {new string(' ', depth * 2)}{view.GetItemName(c)} {total:F1} (self {view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnSelfTime):F1})");
                    if (depth < 30) Dump(c, depth + 1);
                }
            }
            Dump(view.GetRootItemID(), 0);
            if (lines.Length > 0) sb.Append($"  [{t}] {raw.threadGroupName}/{raw.threadName}\n").Append(lines);
        }
        return sb.ToString();
    }

    // GPU Usage profiler module: median frame GPU time and the GPU time of each pass (internal hierarchy columns 8/9 =
    // total/self GPU ms, attributed to the CPU markers that issued the work).
    public static async Task<string> Gpu(float seconds = 3f, int top = 25)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var (summary, _) = await GpuCapture(seconds, top);
        return summary;
    }

    static async Task<(string text, float median)> GpuCapture(float seconds, int top)
    {
        Application.runInBackground = true;
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        bool gpuArea = ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU);
        ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, true);
        ProfilerDriver.enabled = true;
        await Seconds(0.5f);
        int first = ProfilerDriver.lastFrameIndex + 1;
        await Seconds(seconds);
        int last = ProfilerDriver.lastFrameIndex;
        ProfilerDriver.enabled = false;
        ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, gpuArea);
        const int totalGpu = 8, selfGpu = 9;
        var frameGpu = new List<float>();
        var total = new Dictionary<string, double>();
        var self = new Dictionary<string, double>();
        int n = 0;
        for (int f = first; f <= last; f++)
        {
            bool any = false;
            foreach (int t in new[] { 0, ThreadIndex(f, "Render Thread") })
            {
                if (t < 0) continue;
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, t, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, totalGpu, false);
                if (view == null || !view.valid) continue;
                if (t == 0 && view.frameGpuTimeMs > 0) frameGpu.Add(view.frameGpuTimeMs);
                var stack = new Stack<(int id, string path, int depth)>();
                stack.Push((view.GetRootItemID(), "", 0));
                var kids = new List<int>();
                while (stack.Count > 0)
                {
                    var (id, path, depth) = stack.Pop();
                    kids.Clear();
                    view.GetItemChildren(id, kids);
                    foreach (int c in kids)
                    {
                        double tg = view.GetItemColumnDataAsDouble(c, totalGpu);
                        if (tg <= 0.005) continue;
                        any = true;
                        string name = view.GetItemName(c);
                        self[name] = self.GetValueOrDefault(name) + view.GetItemColumnDataAsDouble(c, selfGpu);
                        // by name: URP pass names are unique enough; skip the wrapper markers every pass sits under
                        if (depth >= 6) total[name] = total.GetValueOrDefault(name) + tg;
                        stack.Push((c, name, depth + 1));
                    }
                }
            }
            if (any) n++;
        }
        n = Math.Max(1, n);
        float median = Median(frameGpu);
        var sb = new StringBuilder($"GPU module: {frameGpu.Count} frames, frame GPU median {median:F2} ms p95 {Pct(frameGpu, .95f):F2} (frames with samples {n})\n");
        sb.AppendLine("  self GPU ms/frame: " + string.Join("; ", self.OrderByDescending(k => k.Value).Take(top).Select(k => $"{k.Key} {k.Value / n:F3}")));
        sb.AppendLine("  total GPU by marker (>=0.03 ms): " + string.Join("; ", total.Where(k => k.Value / n >= 0.03).OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value / n:F2}")));
        return (sb.ToString(), median);
    }

    // Live MonoBehaviours whose type declares OnGUI (IMGUI runs, and allocates, every frame for them), in every loaded scene.
    public static string OnGuiUsers()
    {
        var hits = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(m => m != null && m.isActiveAndEnabled && m.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(m))
            .Where(m => m.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
            .Select(m => $"{m.GetType().FullName} on {m.gameObject.scene.name}/{m.name}");
        return string.Join("\n", hits.Distinct());
    }

    // Sets the clock just before `hour`, records the crossing, and dumps the worst frame's main thread (Deep Profile off).
    public static async Task<string> PhaseSpike(float hour, float minMs = 0.3f)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var clock = Clock;
        clock.Release("PerfSuite");
        clock.SetTime(WorldTime.At(clock.Now.Day + (hour <= clock.Now.Hour ? 1 : 0), hour - 1f / 60f));
        return await SpikeDump(4f, minMs, 1f, 1);
    }

    // Texture streaming state and memory, and the renderers / LOD groups whose transforms changed over a few frames
    // (each costs the GPU Resident Drawer a transform update).
    public static async Task<string> Movers(int frames = 30)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"mip streaming {QualitySettings.streamingMipmapsActive} budget {QualitySettings.streamingMipmapsMemoryBudget} MB renderersPerFrame {QualitySettings.streamingMipmapsRenderersPerFrame} " +
                      $"maxLevelReduction {QualitySettings.streamingMipmapsMaxLevelReduction} addAllCameras {QualitySettings.streamingMipmapsAddAllCameras} | streaming textures {Texture.streamingTextureCount} " +
                      $"total {Texture.totalTextureMemory / 1048576} MB desired {Texture.desiredTextureMemory / 1048576} MB target {Texture.targetTextureMemory / 1048576} MB nonStreaming {Texture.nonStreamingTextureMemory / 1048576} MB current {Texture.currentTextureMemory / 1048576} MB");
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r is MeshRenderer).ToArray();
        var lods = Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude);
        foreach (var r in renderers) r.transform.hasChanged = false;
        foreach (var l in lods) l.transform.hasChanged = false;
        var moved = new Dictionary<string, int>();
        for (int i = 0; i < frames; i++)
        {
            await NextFrame();
            foreach (var r in renderers)
                if (r && r.transform.hasChanged) { r.transform.hasChanged = false; string k = "R " + Path(r.transform); moved[k] = moved.GetValueOrDefault(k) + 1; }
            foreach (var l in lods)
                if (l && l.transform.hasChanged) { l.transform.hasChanged = false; string k = "L " + Path(l.transform); moved[k] = moved.GetValueOrDefault(k) + 1; }
        }
        sb.AppendLine($"{renderers.Length} mesh renderers, {lods.Length} LOD groups; moved in {frames} frames: {moved.Count}");
        foreach (var kv in moved.OrderByDescending(k => k.Value).Take(40)) sb.AppendLine($"  {kv.Value,3}  {kv.Key}");
        return sb.ToString();
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    // GC.Alloc call stacks on the main thread while `mode` runs (idle, switch, fire, walk), grouped by the first frames of
    // the stack; bytes per frame. Needs guns in slots 1 and 2 for "switch".
    public static async Task<string> GcStacks(string mode = "switch", float seconds = 3f, int depth = 6)
    {
        if (!EditorApplication.isPlaying) return "enter play mode first";
        var player = Player;
        var pitchHold = PitchSetter(player);
        using var s = new Session(input: true, profile: false);
        player.Ammo.Infinite = true;
        var prevMode = ProfilerDriver.memoryRecordMode;
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.memoryRecordMode = ProfilerMemoryRecordMode.GCAlloc;
        ProfilerDriver.enabled = true;
        int first = -1;
        var seg = await Record(mode, s, i => i < (int)(seconds * 100), i =>
        {
            if (first < 0) first = ProfilerDriver.lastFrameIndex + 2;
            var none = Array.Empty<Key>();
            switch (mode)
            {
                case "switch": s.Send((i / 40) % 2 == 0 ? new[] { Key.Digit1 } : new[] { Key.Digit2 }, Vector2.zero, false, false); break;
                case "fire": s.Send(none, Vector2.zero, true, false); pitchHold(0f); AutoReload(player, s, none, i); break;
                case "walk": s.Send(new[] { Key.W }, Vector2.zero, false, false); break;
                case "look": s.Send(none, new Vector2(10f, 0f), false, false); break;
                default: s.Send(none, Vector2.zero, false, false); break;
            }
        }, null);
        s.Send(Array.Empty<Key>(), Vector2.zero, false, false);
        int last = ProfilerDriver.lastFrameIndex;
        ProfilerDriver.enabled = false;
        ProfilerDriver.memoryRecordMode = prevMode;
        player.Ammo.Infinite = false;
        return GcReport($"mode {mode}", first, last, depth, 15);
    }

    // GC.Alloc samples on the main thread between two captured frames (memoryRecordMode GCAlloc), grouped by call stack.
    static string GcReport(string what, int first, int last, int depth, int top)
    {
        var bytes = new Dictionary<string, long>();
        var counts = new Dictionary<string, int>();
        var stack = new List<ulong>();
        int frames = 0;
        for (int f = Math.Max(first, ProfilerDriver.firstFrameIndex); f <= last; f++)
        {
            using var raw = ProfilerDriver.GetRawFrameDataView(f, 0);
            if (raw == null || !raw.valid) continue;
            frames++;
            for (int i = 0; i < raw.sampleCount; i++)
            {
                if (raw.GetSampleName(i) != "GC.Alloc") continue;
                long size = raw.GetSampleMetadataCount(i) > 0 ? raw.GetSampleMetadataAsLong(i, 0) : 0;
                raw.GetSampleCallstack(i, stack);
                var names = stack.Select(a => raw.ResolveMethodInfo(a)).Where(m => m.methodName != null && m.methodName.Length > 0)
                    .Select(m => m.methodName + (m.sourceFileLine > 0 ? $":{m.sourceFileLine}" : "")).Take(depth);
                string key = string.Join(" < ", names);
                if (key.Length == 0) key = "(no stack)";
                bytes[key] = bytes.GetValueOrDefault(key) + size;
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }
        frames = Math.Max(1, frames);
        var sb = new StringBuilder($"GC.Alloc stacks, {what}, {frames} frames, total {bytes.Values.Sum() / (float)frames:F0} B/frame\n");
        foreach (var kv in bytes.OrderByDescending(k => k.Value).Take(top))
            sb.AppendLine($"  {kv.Value / (float)frames,7:F0} B/f  x{counts[kv.Key] / (float)frames:F2}  {kv.Key}");
        return sb.ToString();
    }

    // The `n` captured frames with the most game time (PlayerLoop minus the present wait), worst first.
    static List<int> WorstFrames(int first, int last, int n)
    {
        var frames = new List<(int f, double ms)>();
        for (int f = Math.Max(first, ProfilerDriver.firstFrameIndex); f <= last; f++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
            if (view == null || !view.valid) continue;
            frames.Add((f, GameMs(view, view.GetRootItemID(), false)));
        }
        return frames.OrderByDescending(x => x.ms).Take(n).Select(x => x.f).ToList();
    }

    // Markers where the main thread only waits: the editor's present throttling, a player's frame cap and vsync.
    static readonly HashSet<string> IdleWaits = new() { "WaitForLastPresentation", "WaitForTargetFPS", "Gfx.WaitForPresentOnGfxThread" };

    static double GameMs(HierarchyFrameDataView v, int id, bool inLoop)
    {
        double ms = 0;
        var kids = new List<int>();
        v.GetItemChildren(id, kids);
        foreach (int c in kids)
        {
            string name = v.GetItemName(c);
            double total = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
            if (!inLoop && name == "PlayerLoop") ms += total + GameMs(v, c, true);
            else if (inLoop && IdleWaits.Contains(name)) ms -= total;
            else if (inLoop || name != "EditorLoop") ms += GameMs(v, c, inLoop);
        }
        return ms;
    }

    // Average main-thread time per frame of each call-tree item inside PlayerLoop (by path, `depth` levels below it, items of at
    // least `minMs`; the present wait left out), children under their parent, largest first. 0 = none.
    static string Breakdown(int first, int last, int depth, float minMs)
    {
        if (depth <= 0 || first < 0) return "";
        var totals = new Dictionary<string, double>();
        var selfs = new Dictionary<string, double>();
        int n = 0;
        for (int f = Math.Max(first, ProfilerDriver.firstFrameIndex); f <= last; f++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
            if (view == null || !view.valid) continue;
            n++;
            void Walk(int id, string path, int level)
            {
                var kids = new List<int>();
                view.GetItemChildren(id, kids);
                foreach (int c in kids)
                {
                    string name = view.GetItemName(c);
                    if (name == "EditorLoop" || name == "WaitForLastPresentation") continue;
                    if (level < 0 && name != "PlayerLoop") continue;
                    string key = path + "/" + name;
                    totals[key] = totals.GetValueOrDefault(key) + view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
                    selfs[key] = selfs.GetValueOrDefault(key) + view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnSelfTime);
                    if (level < depth) Walk(c, key, level + 1);
                }
            }
            Walk(view.GetRootItemID(), "", -1);
        }
        if (n == 0) return "";
        var sb = new StringBuilder($"   breakdown (ms/frame, {n} frames; total / self):\n");
        void Print(string parent, int indent)
        {
            foreach (var kv in totals.Where(k => k.Key.Substring(0, k.Key.LastIndexOf('/')) == parent && k.Value / n >= minMs).OrderByDescending(k => k.Value))
            {
                string name = kv.Key.Substring(kv.Key.LastIndexOf('/') + 1).Replace("UnityEngine.CoreModule.dll!", "").Replace("MB13.Aipocalypse.Core.dll!", "");
                sb.AppendLine($"     {new string(' ', indent * 2)}{name} {kv.Value / n:F2} / {selfs[kv.Key] / n:F2}");
                Print(kv.Key, indent + 1);
            }
        }
        Print("", 0);
        return sb.ToString();
    }

    // Main-thread call tree of one captured frame: items of at least `minMs` inside PlayerLoop, the present wait left out.
    static string DumpFrame(int frame, float minMs)
    {
        using var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
        if (view == null || !view.valid) return "";
        var sb = new StringBuilder($"   frame {frame}: game {GameMs(view, view.GetRootItemID(), false):F1} ms\n");
        void Dump(int id, int depth)
        {
            var kids = new List<int>();
            view.GetItemChildren(id, kids);
            foreach (int c in kids)
            {
                string name = view.GetItemName(c);
                double total = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
                if (total < minMs || name == "EditorLoop" || name == "WaitForLastPresentation") continue;
                sb.AppendLine($"     {new string(' ', depth * 2)}{name} {total:F2} (self {view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnSelfTime):F2})");
                if (depth < 24) Dump(c, depth + 1);
            }
        }
        Dump(view.GetRootItemID(), 0);
        return sb.ToString();
    }

    // A capture a player streamed (-profiler-log-file, see PerfBuild): loads it into the Profiler, skips the first `skip` frames
    // (loading, the fight setting up), then frame time, main-thread game time (PlayerLoop minus the idle waits), the busiest
    // items `depth` levels deep, top self markers, render thread, and the `dumps` worst frames' call trees.
    public static string Capture(string rawPath, int skip = 600, int depth = 6, int dumps = 3, float dumpMs = 0.3f)
    {
        if (EditorApplication.isPlaying) return "leave play mode first";
        if (!ProfilerDriver.LoadProfile(rawPath, false)) return "couldn't load " + rawPath;
        int first = ProfilerDriver.firstFrameIndex + skip, last = ProfilerDriver.lastFrameIndex;
        var frame = new List<float>();
        var game = new List<float>();
        for (int f = first; f <= last; f++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
            if (view == null || !view.valid) continue;
            frame.Add(view.frameTimeMs);
            game.Add((float)GameMs(view, view.GetRootItemID(), false));
        }
        var sb = new StringBuilder($"== capture {System.IO.Path.GetFileName(rawPath)}: frames {ProfilerDriver.firstFrameIndex}..{last}, analysed {frame.Count} from {first}\n");
        sb.AppendLine($"frame ms med/p95/p99/max {Median(frame):F2}/{Pct(frame, .95f):F2}/{Pct(frame, .99f):F2}/{Max(frame):F1} | " +
                      $"game (PlayerLoop minus waits) {Median(game):F2}/{Pct(game, .95f):F2}/{Pct(game, .99f):F2}/{Max(game):F1}");
        var seg = new Seg { Name = "capture", ProfFirst = first, ProfLast = last };
        sb.Append(Analyze(seg, 25, 5));
        sb.Append(Breakdown(first, last, depth, 0.05f));
        foreach (int f in WorstFrames(first, last, dumps))
            sb.Append(DumpFrame(f, dumpMs));
        return Finish("capture", sb);
    }

    public static string GpuArea(bool on)
    {
        ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, on);
        return State();
    }

    public static string State()
    {
        var clock = Clock;
        var sun = RenderSettings.sun;
        return $"active {InternalEditorUtility.isApplicationActive} clock {clock?.Now} phase {clock?.Phase} | sun {sun?.name} shadows {sun?.shadows} int {sun?.intensity:F2} " +
               $"| shadowDist {((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).shadowDistance} | gpuArea {ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU)} profiler {ProfilerDriver.enabled}";
    }

    public static string SetHour(float hour, bool freeze)
    {
        var clock = Clock;
        clock.SetTime(WorldTime.At(clock.Now.Day, hour));
        if (freeze) clock.Freeze("PerfSuite"); else clock.Release("PerfSuite");
        return State();
    }

    // Static facts the numbers depend on.
    public static string Info()
    {
        var sb = new StringBuilder();
        var bloom = Object.FindObjectsByType<Volume>().Select(v => v.sharedProfile).Where(p => p != null).Select(p => p.TryGet(out Bloom b) ? b : null).FirstOrDefault(b => b != null);
        sb.AppendLine($"gfx {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} cpu {SystemInfo.processorType} x{SystemInfo.processorCount} | screen {Screen.width}x{Screen.height} | graphicsJobs {PlayerSettings.graphicsJobs} {PlayerSettings.graphicsJobMode} | vsync {QualitySettings.vSyncCount} target {Application.targetFrameRate}");
        if (bloom != null) sb.AppendLine($"bloom: intensity {bloom.intensity.value} threshold {bloom.threshold.value} hq {bloom.highQualityFiltering.value} downscale {bloom.downscale.value} maxIter {bloom.maxIterations.value} filter {GetValue(bloom, "filter")}");
        sb.AppendLine($"scene views {SceneView.sceneViews.Count}, game views {Resources.FindObjectsOfTypeAll(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Length}, hierarchy windows {Resources.FindObjectsOfTypeAll(typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow")).Length}");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            sb.AppendLine($"  light {l.name} {l.type} enabled {l.isActiveAndEnabled} shadows {l.shadows} res {l.shadowResolution} cookie {(l.cookie ? l.cookie.name : "-")} range {l.range:F1} int {l.intensity:F2}");
        return sb.ToString();
    }

    static string GetValue(object o, string field)
    {
        var f = o.GetType().GetField(field);
        var p = f?.GetValue(o);
        return p == null ? "n/a" : p.GetType().GetProperty("value")?.GetValue(p)?.ToString() ?? "null";
    }

    // ---------------- measurement ----------------

    static async Task<Seg> Measure(string name, Session s, float seconds, Action<int> drive, bool profile)
    {
        int first = ProfilerDriver.enabled ? ProfilerDriver.lastFrameIndex + 2 : -1;
        var seg = await Record(name, s, i => Time.realtimeSinceStartup < _end, drive, () => _end = Time.realtimeSinceStartup + seconds);
        if (profile && ProfilerDriver.enabled) { seg.ProfFirst = first; seg.ProfLast = ProfilerDriver.lastFrameIndex; }
        return seg;
    }

    static float _end;

    static Task<Seg> MeasureFrames(string name, Session s, int frames) => Record(name, s, i => i < frames, null, null);

    // Samples and drives from EditorApplication.update, so none of this runs inside PlayerLoop (await continuations would).
    static Task<Seg> Record(string name, Session s, Func<int, bool> more, Action<int> drive, Action start)
    {
        var seg = new Seg { Name = name };
        var tcs = new TaskCompletionSource<Seg>();
        const int cap = 6000;
        var recs = CounterDefs.Select(c => ProfilerRecorder.StartNew(c.cat, c.name, cap)).ToArray();
        var loop = ProfilerRecorder.StartNew(new ProfilerCategory("Internal"), "PlayerLoop", cap);
        var editor = ProfilerRecorder.StartNew(new ProfilerCategory("Internal"), "EditorLoop", cap);
        var render = ProfilerRecorder.StartNew(ProfilerCategory.Render, "RenderPlayModeViewCameras", cap);
        var wait = ProfilerRecorder.StartNew(ProfilerCategory.Render, "WaitForLastPresentation", cap);
        var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "FrameTime.GPU", cap);
        var timing = new FrameTiming[1];
        s.ResetCams();
        start?.Invoke();
        int i = 0, lastFrame = Time.frameCount;

        void Done(Exception e)
        {
            EditorApplication.update -= Tick;
            try
            {
                if (e == null)
                {
                    var loopMs = Samples(loop, 1e-6f);
                    var waitMs = Samples(wait, 1e-6f);
                    if (waitMs.Length == loopMs.Length)
                    {
                        seg.Stalls = waitMs.Count(w => w > 100f);
                        seg.Loop = loopMs.Select((l, k) => l - waitMs[k]).ToArray();
                    }
                    else
                    {
                        seg.Stalls = loopMs.Count(l => l > 250f);
                        seg.Loop = loopMs.Where(l => l < 250f).ToArray();
                    }
                    seg.Editor = Samples(editor, 1e-6f);
                    seg.RenderCams = Samples(render, 1e-6f);
                    seg.GpuCounter = Samples(gpu, 1e-6f).Where(g => g > 0f).ToArray();
                    seg.Dt.RemoveAll(d => d > 250f);
                    seg.Counters = recs.Select(r => Samples(r, 1f).Select(x => (long)x).ToArray()).ToArray();
                    int n = Math.Max(1, i);
                    seg.GameCams = s.Cams(CameraType.Game) / (float)n;
                    seg.SceneCams = s.Cams(CameraType.SceneView) / (float)n;
                    seg.OtherCams = s.CamsOther() / (float)n;
                }
            }
            finally
            {
                foreach (var r in recs) r.Dispose();
                loop.Dispose(); editor.Dispose(); render.Dispose(); wait.Dispose(); gpu.Dispose();
            }
            if (e != null) tcs.TrySetException(e);
            else tcs.TrySetResult(seg);
        }

        void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                i++;
                seg.Dt.Add(Time.unscaledDeltaTime * 1000f);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    seg.Main.Add((float)timing[0].cpuMainThreadFrameTime);
                    seg.Render.Add((float)timing[0].cpuRenderThreadFrameTime);
                    if (timing[0].gpuFrameTime > 0) seg.Gpu.Add((float)timing[0].gpuFrameTime);
                }
                if (!more(i)) { Done(null); return; }
                drive?.Invoke(i);
            }
            catch (Exception e)
            {
                Done(e);
            }
        }

        drive?.Invoke(0);
        EditorApplication.update += Tick;
        return tcs.Task;
    }

    static float[] Samples(ProfilerRecorder r, float scale)
    {
        var list = new List<ProfilerRecorderSample>(r.Count);
        r.CopyTo(list);
        // the first sample can predate the window
        return list.Skip(list.Count > 4 ? 1 : 0).Select(x => x.Value * scale).ToArray();
    }

    static string Row(Seg s, string extra = "")
    {
        string c(int k, float scale = 1f) => s.Counters == null || s.Counters[k].Length == 0 ? "n/a" : (Median(s.Counters[k]) * scale).ToString(scale < 1 ? "F1" : "F0");
        float gcKb = s.Counters == null || s.Counters[5].Length == 0 ? 0 : (float)s.Counters[5].Average() / 1024f;
        return $"{s.Name,-22} {s.Dt.Count,4}f stalls {s.Stalls} dt {Median(s.Dt),5:F1}/{Pct(s.Dt, .95f),5:F1}/{Pct(s.Dt, .99f),5:F1}/{Max(s.Dt),5:F1} " +
               $"| game {Median(s.Loop),5:F2}/{Pct(s.Loop, .95f),5:F2}/{Pct(s.Loop, .99f),5:F2}/{Max(s.Loop),5:F1} rend {Median(s.RenderCams),5:F2} ed {Median(s.Editor),4:F1} " +
               $"| gpu {Median(s.GpuCounter),5:F2}/{Pct(s.GpuCounter, .95f),5:F2} ftm {Median(s.Gpu),5:F2} rt {Median(s.Render),5:F2} " +
               $"| draws {c(0)} bat {c(1)} setp {c(2)} Mtri {c(3, 1e-6f)} cast {c(4)} gc {gcKb:F1}KB " +
               $"| cams g{s.GameCams:F1} s{s.SceneCams:F1} o{s.OtherCams:F1} {s.Note} {extra}";
    }

    // Top self-time markers on the main thread outside the editor loop, the render thread, GC by parent, and the worst frames.
    static string Analyze(Seg seg, int topMain, int spikes)
    {
        if (seg.ProfFirst < 0 || seg.ProfLast < seg.ProfFirst) return "";
        int first = Math.Max(seg.ProfFirst, ProfilerDriver.firstFrameIndex), last = seg.ProfLast;
        if (last < first) return "  (profiler frames rolled off)\n";
        var sb = new StringBuilder();
        var self = new Dictionary<string, double>();
        var calls = new Dictionary<string, double>();
        var gc = new Dictionary<string, double>();
        var rtSelf = new Dictionary<string, double>();
        var frameLoop = new List<(int f, double ms, Dictionary<string, double> top)>();
        int n = 0, rtN = 0;
        for (int f = first; f <= last; f++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
            if (view == null || !view.valid) continue;
            n++;
            var frameSelf = new Dictionary<string, double>();
            double loopMs = Walk(view, view.GetRootItemID(), "", self, calls, gc, frameSelf, skipEditor: true);
            frameLoop.Add((f, loopMs, frameSelf));
            int rt = ThreadIndex(f, "Render Thread");
            if (rt > 0)
            {
                using var rv = ProfilerDriver.GetHierarchyFrameDataView(f, rt, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
                if (rv != null && rv.valid) { rtN++; Walk(rv, rv.GetRootItemID(), "", rtSelf, null, null, null, skipEditor: false); }
            }
        }
        if (n == 0) return "  (no profiler frames)\n";
        string Clean(string k) => k.Replace("UnityEngine.CoreModule.dll!", "").Replace("MB13.Aipocalypse.Core.dll!", "").Replace("Assembly-CSharp.dll!", "");
        sb.AppendLine($"   main (self ms/frame, no EditorLoop, {n} frames): " + string.Join("; ", self.Where(k => k.Key != "PlayerLoop").OrderByDescending(k => k.Value).Take(topMain).Select(k => $"{Clean(k.Key)} {k.Value / n:F2} x{calls[k.Key] / n:F0}")));
        if (rtN > 0) sb.AppendLine($"   render thread: " + string.Join("; ", rtSelf.OrderByDescending(k => k.Value).Take(8).Select(k => $"{Clean(k.Key)} {k.Value / rtN:F2}")));
        var gcTop = gc.Where(k => k.Value / n > 64).OrderByDescending(k => k.Value).Take(6).ToList();
        if (gcTop.Count > 0) sb.AppendLine($"   gc (B/frame by parent): " + string.Join("; ", gcTop.Select(k => $"{Clean(k.Key)} {k.Value / n:F0}")));
        foreach (var (f, ms, top) in frameLoop.OrderByDescending(x => x.ms).Take(spikes))
            sb.AppendLine($"   spike {ms:F1}ms: " + string.Join("; ", top.Where(k => k.Key != "PlayerLoop").OrderByDescending(k => k.Value).Take(6).Select(k => $"{Clean(k.Key)} {k.Value:F2}")));
        return sb.ToString();
    }

    // Returns the PlayerLoop total of the frame.
    static double Walk(HierarchyFrameDataView v, int id, string parent, Dictionary<string, double> self, Dictionary<string, double> calls,
        Dictionary<string, double> gc, Dictionary<string, double> frameSelf, bool skipEditor)
    {
        double loop = 0;
        var kids = new List<int>();
        v.GetItemChildren(id, kids);
        foreach (int c in kids)
        {
            string name = v.GetItemName(c);
            if (skipEditor && name == "EditorLoop") continue;
            if (name == "PlayerLoop") loop += v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
            double s = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnSelfTime);
            self[name] = self.GetValueOrDefault(name) + s;
            if (frameSelf != null) frameSelf[name] = frameSelf.GetValueOrDefault(name) + s;
            if (calls != null) calls[name] = calls.GetValueOrDefault(name) + v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnCalls);
            if (gc != null && name == "GC.Alloc") gc[parent] = gc.GetValueOrDefault(parent) + v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory);
            loop += Walk(v, c, name, self, calls, gc, frameSelf, skipEditor);
        }
        return loop;
    }

    static readonly Dictionary<string, int> ThreadCache = new();

    static int ThreadIndex(int frame, string name)
    {
        if (ThreadCache.TryGetValue(name, out int cached))
        {
            using var raw = ProfilerDriver.GetRawFrameDataView(frame, cached);
            if (raw != null && raw.valid && raw.threadName == name) return cached;
        }
        for (int i = 1; i < 128; i++)
        {
            using var raw = ProfilerDriver.GetRawFrameDataView(frame, i);
            if (raw == null || !raw.valid) return -1;
            if (raw.threadName == name) { ThreadCache[name] = i; return i; }
        }
        return -1;
    }

    // ---------------- session: virtual input, profiler, camera counts ----------------

    sealed class Session : IDisposable
    {
        public readonly Mouse Mouse;
        public readonly Keyboard Keyboard;
        readonly InputSettings.BackgroundBehavior _bg;
        readonly InputSettings.EditorInputBehaviorInPlayMode _ed;
        readonly bool _input, _profile;
        readonly Dictionary<CameraType, int> _cams = new();

        public Session(bool input, bool profile)
        {
            _input = input;
            _profile = profile;
            Application.runInBackground = true;
            RenderPipelineManager.beginCameraRendering += OnCamera;
            if (input)
            {
                var settings = InputSystem.settings;
                _bg = settings.backgroundBehavior;
                _ed = settings.editorInputBehaviorInPlayMode;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Mouse = InputSystem.AddDevice<Mouse>("PerfMouse");
                Keyboard = InputSystem.AddDevice<Keyboard>("PerfKeyboard");
            }
            if (profile)
            {
                Type.GetType("UnityEditor.Profiling.ProfilerUserSettings, UnityEditor")?.GetProperty("frameCount")?.SetValue(null, 2000);
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = false;
                ProfilerDriver.enabled = true;
            }
        }

        void OnCamera(ScriptableRenderContext context, Camera camera) => _cams[camera.cameraType] = _cams.GetValueOrDefault(camera.cameraType) + 1;
        public void ResetCams() => _cams.Clear();
        public int Cams(CameraType type) => _cams.GetValueOrDefault(type);
        public int CamsOther() => _cams.Where(k => k.Key != CameraType.Game && k.Key != CameraType.SceneView).Sum(k => k.Value);

        public void Send(Key[] keys, Vector2 delta, bool lmb, bool rmb)
        {
            if (!_input) return;
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
            var m = new MouseState { delta = delta };
            if (lmb) m = m.WithButton(MouseButton.Left);
            if (rmb) m = m.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(Mouse, m);
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= OnCamera;
            if (_input)
            {
                InputSystem.RemoveDevice(Mouse);
                InputSystem.RemoveDevice(Keyboard);
                InputSystem.settings.backgroundBehavior = _bg;
                InputSystem.settings.editorInputBehaviorInPlayMode = _ed;
            }
            if (_profile) ProfilerDriver.enabled = false;
        }
    }

    // Sets and freezes the world clock at `hour` (negative = leave it running); releases it on Dispose.
    sealed class FrozenClock : IDisposable
    {
        readonly WorldClock _clock;

        public FrozenClock(float hour)
        {
            if (hour < 0f) return;
            _clock = Clock;
            _clock?.SetTime(WorldTime.At(_clock.Now.Day, hour));
            _clock?.Freeze("PerfSuite");
        }

        public void Dispose() => _clock?.Release("PerfSuite");
    }

    // ---------------- game helpers ----------------

    static Player Player => Object.FindObjectsByType<Player>().First();
    static WorldClock Clock
    {
        get
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            return scope != null && scope.Container.TryResolve(out WorldClock clock) ? clock : null;
        }
    }

    static Vector3 Ground(float x, float z)
    {
        var t = Terrain.activeTerrain;
        float y = t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y;
        if (Physics.Raycast(new Vector3(x, y + 50f, z), Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y;
        return new Vector3(x, y + 0.1f, z);
    }

    static void Teleport(Player p, Vector3 at, float yaw, float pitch)
    {
        var cc = p.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        p.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc) cc.enabled = true;
        PitchSetter(p)(pitch);
    }

    static Action<float> PitchSetter(Player p)
    {
        var look = typeof(Player).GetField("_look", Private)?.GetValue(p);
        var field = look?.GetType().GetField("_pitch", Private);
        return v => field?.SetValue(look, v);
    }

    static void Give(Player p, string item)
    {
        var def = AssetDatabase.FindAssets($"{item} t:{nameof(ItemDefinition)}", new[] { "Assets/_Game/Content" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .First(i => i.name == item);
        p.Equipment.TryPickUp(def.CreateInstance(), out _);
    }

    // Empty magazine: press R for a few frames (the drive's own keys win when it sends R).
    static void AutoReload(Player p, Session s, Key[] keys, int frame)
    {
        if (p.Equipment?.Selected is GunInstance gun && gun.Rounds == 0 && !keys.Contains(Key.R))
            s.Send(keys.Append(Key.R).ToArray(), Vector2.zero, false, false);
    }

    static Light VisionLight(Player p) => p.transform.Find("VisionLight").GetComponent<Light>();

    static string LightInfo(Player p)
    {
        var l = VisionLight(p);
        return $"[vision {(l.isActiveAndEnabled ? "on" : "off")} int {l.intensity:F1} range {l.range:F1} shadows {l.shadows}]";
    }

    // ---------------- utils ----------------

    static StringBuilder Header(string what) => new StringBuilder($"== {what} {DateTime.Now:yyyy-MM-dd HH:mm} | {Info().Split('\n')[0]}\n" +
        "cols: frames, present stalls, dt med/p95/p99/max (stalls out) | game = PlayerLoop minus present wait med/p95/p99/max, RenderPlayModeViewCameras, EditorLoop " +
        "| GPU counter med/p95, FrameTiming gpu, render thread | counters (median) | cameras/frame\n");

    static string Finish(string what, StringBuilder sb)
    {
        string text = sb.ToString();
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"claude_perf_{what}.txt"), text);
        return text;
    }

    static Task NextFrame()
    {
        var tcs = new TaskCompletionSource<bool>();
        int start = Time.frameCount;
        void Tick()
        {
            if (Time.frameCount == start) return;
            EditorApplication.update -= Tick;
            tcs.TrySetResult(true);
        }
        EditorApplication.update += Tick;
        return tcs.Task;
    }

    static async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await NextFrame();
    }

    static async Task Seconds(float s)
    {
        float end = Time.realtimeSinceStartup + s;
        while (Time.realtimeSinceStartup < end) await NextFrame();
    }

    static float Median(IReadOnlyCollection<float> a) => Pct(a, 0.5f);
    static float Median(long[] a) => a.Length == 0 ? 0 : a.OrderBy(x => x).ElementAt(a.Length / 2);
    static float Max(IReadOnlyCollection<float> a) => a.Count == 0 ? 0 : a.Max();

    static float Pct(IReadOnlyCollection<float> a, float q)
    {
        if (a.Count == 0) return 0;
        var s = a.OrderBy(x => x).ToList();
        return s[Mathf.Clamp((int)(q * s.Count), 0, s.Count - 1)];
    }
}
