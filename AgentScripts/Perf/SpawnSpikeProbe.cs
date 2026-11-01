// Where the worst SpawnService.Spawn frames of a big crowd go (play mode on WorldMap):
//   unity command script run --file AgentScripts/Perf/SpawnSpikeProbe.cs --entry SpawnSpikeProbe.Run
// Prewarms `count` goblins, brings them in as `groups` groups round the player like SpawnPlayCheck.Crowd, and records per
// frame: the spawn marker, members woken, instances created (pool ran dry), GC collections and a few other markers. Prints
// the worst frames. Editor numbers: compare frames with each other, not with a build.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.SpawnSystem;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public static class SpawnSpikeProbe
{
    static readonly string[] Markers =
    {
        "Aipocalypse.SpawnService.Spawn", "Aipocalypse.SpawnService.Place", "GC.Collect", "Instantiate", "PlayerLoop",
        "Aipocalypse.PrefabPool.Trim",
    };

    sealed class Frame
    {
        public int Number;
        public int Woken;
        public int Created;
        public int Collections;
        public int Queued;
        public double[] Ms = new double[Markers.Length];
    }

    public static Task<string> Run(int count = 300, int groups = 10, int worst = 8)
    {
        if (!EditorApplication.isPlaying) return Task.FromResult("enter play mode first");
        Application.runInBackground = true;
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var commands = resolver.Resolve<CommandRegistry>();
        var spawns = resolver.Resolve<SpawnService>();
        var players = resolver.Resolve<PlayerRegistry>();
        var card = resolver.Resolve<EnemySystemSettings>().Cards[0];
        var recorders = Markers.Select(Recorder).ToArray();
        var frames = new List<Frame>();
        var handles = new List<SpawnHandle>();
        var tcs = new TaskCompletionSource<string>();
        var sb = new StringBuilder();
        int stage = 0, lastAlive = 0, lastMade = 0, lastGc = 0;
        float t0 = 0f;

        int Made() => card.Variants.Keys.Sum(p => spawns.Pool.Made(p));
        int Alive() => handles.Sum(h => h.Alive - h.Pending);

        void OnFrame()
        {
            try
            {
                switch (stage)
                {
                    case 0:
                        commands.Execute("cheat.god on");
                        commands.Execute("cheat.notarget on");
                        spawns.Prewarm(card, count);
                        stage = 1;
                        return;
                    case 1:
                        if (spawns.Pool.Warming) return;
                        var rules = new SpawnRules { MinPlayerDistance = 25f, MaxHiddenWait = 0f, SeenLock = 0f, Candidates = 12 };
                        rules.GroundMask = rules.SightBlockers = rules.ObstacleMask = resolver.Resolve<SpawnSystemSettings>().DefaultRules.GroundMask;
                        Vector3 at = players.Current.Transform.position;
                        for (int i = 0; i < groups; i++)
                            handles.Add(spawns.Spawn(SpawnRequest.Group(card, new RingArea(at, 30f, 110f), count / groups).WithRules(rules)));
                        lastMade = Made();
                        lastGc = GC.CollectionCount(0);
                        t0 = Time.realtimeSinceStartup;
                        stage = 2;
                        return;
                    case 2:
                        // Markers' LastValue is the previous frame: it belongs to the frame recorded last time.
                        if (frames.Count > 0)
                            for (int m = 0; m < Markers.Length; m++)
                                frames[^1].Ms[m] = recorders[m].Valid ? recorders[m].LastValue / 1e6 : -1;
                        int alive = Alive(), made = Made(), gc = GC.CollectionCount(0);
                        frames.Add(new Frame { Number = Time.frameCount, Woken = alive - lastAlive, Created = made - lastMade, Collections = gc - lastGc, Queued = spawns.Queued });
                        lastAlive = alive; lastMade = made; lastGc = gc;
                        if (handles.Sum(h => h.Pending) > 0 && Time.realtimeSinceStartup - t0 < 60f) return;
                        stage = 3;
                        return;
                    case 3:
                        for (int m = 0; m < Markers.Length; m++)
                            frames[^1].Ms[m] = recorders[m].Valid ? recorders[m].LastValue / 1e6 : -1;
                        Report();
                        return;
                }
            }
            catch (Exception e)
            {
                Application.onBeforeRender -= OnFrame;
                tcs.TrySetResult(sb + "\nerror: " + e);
            }
        }

        void Report()
        {
            Application.onBeforeRender -= OnFrame;
            sb.AppendLine($"{frames.Count} frames, {Alive()} alive, {frames.Sum(f => f.Created)} created mid-spawn, {frames.Sum(f => f.Collections)} GC");
            var spawn = frames.Select(f => f.Ms[0]).Where(v => v > 0).OrderBy(v => v).ToList();
            if (spawn.Count > 0)
                sb.AppendLine($"Spawn marker: med {spawn[spawn.Count / 2]:F2} p95 {spawn[(int)(spawn.Count * 0.95f)]:F2} max {spawn[^1]:F2} ms");
            sb.AppendLine("worst frames (frame: woken, created, gc, queued after | " + string.Join(", ", Markers) + " ms):");
            foreach (var f in frames.OrderByDescending(f => f.Ms[0]).Take(worst))
                sb.AppendLine($"  {f.Number}: woken {f.Woken}, created {f.Created}, gc {f.Collections}, queued {f.Queued} | " +
                    string.Join(", ", f.Ms.Select(v => v.ToString("F2"))));
            foreach (var h in handles) h.Despawn(DespawnMode.Now);
            commands.Execute("cheat.notarget off");
            commands.Execute("cheat.god off");
            foreach (var r in recorders) r.Dispose();
            tcs.TrySetResult(sb.ToString());
        }

        Application.onBeforeRender += OnFrame;
        return tcs.Task;
    }

    // Same crowd with the profiler recording (fresh play session for first-use costs): the worst frames' SpawnService.Spawn
    // sample broken down into its children, `depth` levels, samples of at least `minMs`.
    public static Task<string> Breakdown(int count = 300, int groups = 10, int worst = 3, int depth = 6, float minMs = 0.25f)
    {
        UnityEditorInternal.ProfilerDriver.ClearAllFrames();
        UnityEditorInternal.ProfilerDriver.profileEditor = false;
        UnityEditorInternal.ProfilerDriver.enabled = true;
        UnityEngine.Profiling.Profiler.enabled = true;
        return Run(count, groups, worst).ContinueWith(run =>
        {
            UnityEditorInternal.ProfilerDriver.enabled = false;
            var sb = new StringBuilder(run.Result);
            int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
            var found = new List<(int Frame, int Item, float Ms)>();
            for (int f = first; f <= last && f >= 0; f++)
            {
                using var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0,
                    UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime, false);
                if (!view.valid) continue;
                int item = Find(view, view.GetRootItemID(), Markers[0]);
                if (item >= 0) found.Add((f, item, view.GetItemColumnDataAsFloat(item, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime)));
            }
            sb.AppendLine($"profiled frames {first}..{last}, {found.Count} with the spawn marker");
            foreach (var (frame, item, ms) in found.OrderByDescending(x => x.Ms).Take(worst))
            {
                using var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                    UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime, false);
                sb.AppendLine($"frame {frame}: Spawn {ms:F2} ms");
                Dump(view, item, 1, depth, minMs, sb);
            }
            return sb.ToString();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    static int Find(UnityEditor.Profiling.HierarchyFrameDataView view, int item, string name)
    {
        if (view.GetItemName(item) == name) return item;
        var children = new List<int>();
        view.GetItemChildren(item, children);
        foreach (int child in children)
        {
            int hit = Find(view, child, name);
            if (hit >= 0) return hit;
        }
        return -1;
    }

    static void Dump(UnityEditor.Profiling.HierarchyFrameDataView view, int item, int level, int depth, float minMs, StringBuilder sb)
    {
        if (level > depth) return;
        var children = new List<int>();
        view.GetItemChildren(item, children);
        foreach (int child in children.OrderByDescending(c => view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime)))
        {
            float ms = view.GetItemColumnDataAsFloat(child, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime);
            if (ms < minMs) continue;
            sb.AppendLine($"{new string(' ', level * 2)}{view.GetItemName(child)} {ms:F2} ms (x{view.GetItemColumnDataAsFloat(child, UnityEditor.Profiling.HierarchyFrameDataView.columnCalls):F0})");
            Dump(view, child, level + 1, depth, minMs, sb);
        }
    }

    static ProfilerRecorder Recorder(string marker)
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (d.Name == marker)
                return ProfilerRecorder.StartNew(d.Category, d.Name, 4);
        }
        return ProfilerRecorder.StartNew(new ProfilerCategory("Internal"), marker, 4);
    }
}
