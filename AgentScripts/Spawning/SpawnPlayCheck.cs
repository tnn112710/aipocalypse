// Play-mode checks of the SpawnSystem on WorldMap (enter play mode first):
//   unity command script run --file AgentScripts/Spawning/SpawnPlayCheck.cs --entry SpawnPlayCheck.Raid
// Raid: waits for the raid prewarm, starts tonight's raid, follows it for `seconds` (spawned, alive, where they stand,
// whether any spawned in view) and stops it. Console: spawns `count` of a card in a formation through spawn.card.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.RaidSystem;
using Aipocalypse.Core.SpawnSystem;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public static class SpawnPlayCheck
{
    public static Task<string> Raid(float seconds = 25f) => Drive(sb => RaidSteps(sb, seconds));

    public static Task<string> Console(string line = "spawn.card goblin 6 wedge", float seconds = 4f) =>
        Drive(sb => ConsoleSteps(sb, line, seconds));

    // Flocks: takes the player to the lake view (PerfSuite's lake_view), tests water and air placement, spawns fish schools and
    // bird flocks near and reports whether their members stay in their bands and round their homes.
    public static Task<string> Flocks(float seconds = 6f) => Drive(sb => FlockSteps(sb, seconds));

    static IEnumerable<float> FlockSteps(StringBuilder sb, float seconds)
    {
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var commands = resolver.Resolve<CommandRegistry>();
        var players = resolver.Resolve<PlayerRegistry>();
        var flocks = resolver.Resolve<Aipocalypse.Core.AmbientSystem.FlockSystem>();
        var player = (Component)players.Current;
        Physics.Raycast(new Vector3(526f, 500f, 196f), Vector3.down, out RaycastHit ground, 1000f, 1);
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.SetPositionAndRotation(ground.point + Vector3.up * 0.2f, Quaternion.Euler(0f, 100f, 0f));
        if (cc) cc.enabled = true;
        commands.Execute("cheat.god on");
        yield return 1f;
        foreach (string line in new[] { "spawn.test water 60 150", "spawn.test air 40 120", "spawn.near fishschool 3 150", "spawn.near birdflock 2 120" })
            sb.AppendLine($"{line}: {commands.Execute(line).Message}");
        yield return seconds;
        sb.AppendLine($"flocks moving: {flocks.Count}");
        foreach (var flock in Object.FindObjectsByType<Aipocalypse.Core.AmbientSystem.Flock>(FindObjectsInactive.Exclude))
        {
            float low = float.MaxValue, high = float.MinValue, far = 0f;
            for (int i = 0; i < flock.Count; i++)
            {
                Vector3 p = flock.MemberPosition(i);
                low = Mathf.Min(low, p.y);
                high = Mathf.Max(high, p.y);
                far = Mathf.Max(far, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(flock.Home.x, flock.Home.z)));
            }
            sb.AppendLine($"  {flock.name}: {flock.Count} members, band {flock.Floor:F1}–{flock.Ceiling:F1}, members {low:F1}–{high:F1}, " +
                $"farthest {far:F1} m from home (roam {flock.Definition.RoamRadius}), {Vector3.Distance(player.transform.position, flock.Home):F0} m from the player");
        }
        sb.AppendLine(commands.Execute("spawn.stats").Message);
        sb.AppendLine("spawn.clear: " + commands.Execute("spawn.clear").Message);
        commands.Execute("cheat.god off");
    }

    // Crowd: prewarms `count` goblins, brings them in through SpawnService in groups round the player (they can't target the
    // player: cheat.notarget), then compares 4 s of steady frames with far enemies thinking slower (FarThinkDistance) and not.
    // Frame numbers are PlayerLoop (editor) and the profiler markers of the spawn path and the trees.
    public static Task<string> Crowd(int count = 300, int groups = 10) => Drive(sb => CrowdSteps(sb, count, groups));

    static IEnumerable<float> CrowdSteps(StringBuilder sb, int count, int groups)
    {
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var commands = resolver.Resolve<CommandRegistry>();
        var spawns = resolver.Resolve<SpawnService>();
        var players = resolver.Resolve<PlayerRegistry>();
        var enemySettings = resolver.Resolve<EnemySystemSettings>();
        var card = enemySettings.Cards[0];
        commands.Execute("cheat.god on");
        commands.Execute("cheat.notarget on");
        var loop = Recorder("PlayerLoop");
        var think = Recorder("Aipocalypse.EnemyManager.Think");
        var spawn = Recorder("Aipocalypse.SpawnService.Spawn");
        var prewarm = Recorder("Aipocalypse.PrefabPool.Prewarm");

        float t0 = Time.realtimeSinceStartup;
        Restart(loop, prewarm);
        spawns.Prewarm(card, count);
        while (spawns.Pool.Warming) yield return 0f;
        sb.AppendLine($"prewarm {count}: {Time.realtimeSinceStartup - t0:F1} s, PlayerLoop {Stats(loop)}, PrefabPool.Prewarm {Stats(prewarm)}");

        var rules = new SpawnRules { MinPlayerDistance = 25f, MaxHiddenWait = 0f, SeenLock = 0f, Candidates = 12 };
        rules.GroundMask = rules.SightBlockers = rules.ObstacleMask = resolver.Resolve<SpawnSystemSettings>().DefaultRules.GroundMask;
        var handles = new List<SpawnHandle>();
        Vector3 at = players.Current.Transform.position;
        t0 = Time.realtimeSinceStartup;
        Restart(loop, spawn);
        for (int i = 0; i < groups; i++)
            handles.Add(spawns.Spawn(SpawnRequest.Group(card, new RingArea(at, 30f, 110f), count / groups).WithRules(rules)));
        int pending = int.MaxValue;
        while (pending > 0 && Time.realtimeSinceStartup - t0 < 60f)
        {
            pending = 0;
            foreach (var h in handles) pending += h.Pending;
            yield return 0f;
        }
        int alive = 0;
        foreach (var h in handles) alive += h.Alive;
        sb.AppendLine($"arrival of {alive}: {Time.realtimeSinceStartup - t0:F1} s, PlayerLoop {Stats(loop)}, SpawnService.Spawn {Stats(spawn)}");
        yield return 2f;

        float far = enemySettings.FarThinkDistance;
        Restart(loop, think);
        yield return 4f;
        sb.AppendLine($"steady, far enemies slower ({far} m): PlayerLoop {Stats(loop)}, EnemyManager.Think {Stats(think)}");
        enemySettings.FarThinkDistance = 0f;
        Restart(loop, think);
        yield return 4f;
        enemySettings.FarThinkDistance = far;
        sb.AppendLine($"steady, all at full rate: PlayerLoop {Stats(loop)}, EnemyManager.Think {Stats(think)}");

        foreach (var h in handles) h.Despawn(DespawnMode.Now);
        commands.Execute("cheat.notarget off");
        commands.Execute("cheat.god off");
        foreach (var r in new[] { loop, think, spawn, prewarm }) r.Dispose();
    }

    static Unity.Profiling.ProfilerRecorder Recorder(string marker)
    {
        var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
            if (d.Name == marker)
                return Unity.Profiling.ProfilerRecorder.StartNew(d.Category, d.Name, 20000);
        }
        return Unity.Profiling.ProfilerRecorder.StartNew(new Unity.Profiling.ProfilerCategory("Internal"), marker, 20000);
    }

    static void Restart(params Unity.Profiling.ProfilerRecorder[] recorders)
    {
        foreach (var r in recorders)
        {
            if (!r.Valid) continue;
            r.Reset();
            r.Start();
        }
    }

    static string Stats(Unity.Profiling.ProfilerRecorder r)
    {
        if (!r.Valid || r.Count == 0) return "n/a";
        var v = new List<double>();
        for (int i = 0; i < r.Count; i++) v.Add(r.GetSample(i).Value / 1e6);
        v.Sort();
        return $"med {v[v.Count / 2]:F2} p95 {v[(int)(v.Count * 0.95f)]:F2} max {v[^1]:F2} ms ({v.Count} frames)";
    }

    static Task<string> Drive(Func<StringBuilder, IEnumerable<float>> steps)
    {
        if (!EditorApplication.isPlaying) return Task.FromResult("enter play mode first");
        Application.runInBackground = true;
        var sb = new StringBuilder();
        var tcs = new TaskCompletionSource<string>();
        var it = steps(sb).GetEnumerator();
        double wakeAt = 0;
        void Tick()
        {
            if (EditorApplication.timeSinceStartup < wakeAt) return;
            try
            {
                if (!it.MoveNext()) { EditorApplication.update -= Tick; tcs.TrySetResult(sb.ToString()); return; }
                wakeAt = EditorApplication.timeSinceStartup + it.Current;
            }
            catch (Exception e)
            {
                EditorApplication.update -= Tick;
                tcs.TrySetResult(sb + "\nerror: " + e);
            }
        }
        EditorApplication.update += Tick;
        return tcs.Task;
    }

    // yield = seconds to wait (real time)
    static IEnumerable<float> RaidSteps(StringBuilder sb, float seconds)
    {
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var commands = resolver.Resolve<CommandRegistry>();
        var spawns = resolver.Resolve<SpawnService>();
        var raid = resolver.Resolve<RaidDirector>();
        var players = resolver.Resolve<PlayerRegistry>();
        var visibility = resolver.Resolve<SpawnVisibility>();
        var card = resolver.Resolve<EnemySystemSettings>().Cards[0];
        for (int i = 0; i < 120 && spawns.Pool.Warming; i++) yield return 0.25f;
        sb.AppendLine("pool: " + string.Join(", ", card.Variants.Keys.Select(p => $"{p.name} made {spawns.Pool.Made(p)} free {spawns.Pool.Free(p)}")));
        commands.Execute("cheat.god on");
        sb.AppendLine("raid.start: " + commands.Execute("raid.start").Message);
        var seen = new HashSet<EnemyBrain>();
        int spawnedInView = 0;
        var registry = resolver.Resolve<EnemyRegistry>();
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
        {
            for (int i = 0; i < registry.Count; i++)
            {
                EnemyBrain e = registry[i];
                if (!seen.Add(e)) continue;
                if (visibility.IsSeen(e.transform, resolver.Resolve<SpawnSystemSettings>().DefaultRules)) spawnedInView++;
                float d = Vector3.Distance(players.Current.Transform.position, e.transform.position);
                sb.AppendLine($"  t+{seconds - (until - Time.realtimeSinceStartup):F1}s {e.name} at {d:F0} m, appearing {e.Appearing}");
            }
            yield return 0.1f;
        }
        sb.AppendLine($"raid: {commands.Execute("raid.status").Message}");
        sb.AppendLine($"spawn: {commands.Execute("spawn.stats").Message}");
        sb.AppendLine($"seen at spawn: {spawnedInView} of {seen.Count}");
        sb.AppendLine("raid.stop: " + commands.Execute("raid.stop").Message);
        yield return 1f;
        sb.AppendLine($"after stop: {commands.Execute("spawn.stats").Message}; registry {registry.Count}");
        commands.Execute("cheat.god off");
    }

    static IEnumerable<float> ConsoleSteps(StringBuilder sb, string line, float seconds)
    {
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var commands = resolver.Resolve<CommandRegistry>();
        var players = resolver.Resolve<PlayerRegistry>();
        commands.Execute("cheat.god on");
        commands.Execute("cheat.notarget on");
        sb.AppendLine($"{line}: {commands.Execute(line).Message}");
        yield return seconds;
        var registry = resolver.Resolve<EnemyRegistry>();
        Transform body = players.Current.Transform;
        for (int i = 0; i < registry.Count; i++)
        {
            Vector3 local = body.InverseTransformPoint(registry[i].transform.position);
            sb.AppendLine($"  {registry[i].name} local ({local.x:F1}, {local.z:F1})");
        }
        sb.AppendLine(commands.Execute("spawn.stats").Message);
    }
}
