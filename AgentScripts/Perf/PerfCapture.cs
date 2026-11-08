using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class PerfCapture
{
    sealed class Phase
    {
        public string Name;
        public int FirstFrame, LastFrame;
        public List<float> Dt = new(), Gpu = new(), CpuMain = new(), CpuRender = new();
        public Dictionary<string, List<long>> Counters = new();
        public string Report;
        public float YawStart, YawEnd;
    }

    static readonly (ProfilerCategory cat, string name)[] CounterDefs =
    {
        (ProfilerCategory.Render, "Batches Count"),
        (ProfilerCategory.Render, "SetPass Calls Count"),
        (ProfilerCategory.Render, "Draw Calls Count"),
        (ProfilerCategory.Render, "Triangles Count"),
        (ProfilerCategory.Render, "Vertices Count"),
        (ProfilerCategory.Render, "Shadow Casters Count"),
        (ProfilerCategory.Memory, "GC Allocated In Frame"),
        (ProfilerCategory.Memory, "GC Allocation In Frame Count"),
    };

    public static async Task<string> Run(string outName = "cap", float seconds = 5f, float lookPixelsPerFrame = 12f)
    {
        if (!EditorApplication.isPlaying) return "Not in play mode";
        var sb = new StringBuilder();
        sb.AppendLine($"Screen {Screen.width}x{Screen.height} sceneViews={SceneView.sceneViews.Count} focused={UnityEditorInternal.InternalEditorUtility.isApplicationActive} vsync={QualitySettings.vSyncCount}");

        var settings = InputSystem.settings;
        var prevBg = settings.backgroundBehavior;
        var prevEd = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var mouse = InputSystem.AddDevice<Mouse>("PerfMouse");

        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        FrameTimingManager.IsFeatureEnabled();

        var phases = new List<Phase>();
        try
        {
            await RunPhase(null, 1.5f, false, false, 0, mouse); // warm-up
            foreach (var (name, look, fire) in new[] { ("idle", false, false), ("look", true, false), ("fire", false, true), ("look+fire", true, true) })
            {
                var p = new Phase { Name = name };
                await RunPhase(p, seconds, look, fire, lookPixelsPerFrame, mouse);
                p.Report = Analyze(p);
                phases.Add(p);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                await Task.Delay(500);
            }
        }
        finally
        {
            ProfilerDriver.enabled = false;
            InputSystem.RemoveDevice(mouse);
            settings.backgroundBehavior = prevBg;
            settings.editorInputBehaviorInPlayMode = prevEd;
        }

        foreach (var p in phases) sb.AppendLine(p.Report);
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "claude_perf_" + outName + ".txt"), sb.ToString());
        return sb.ToString();
    }

    static async Task RunPhase(Phase p, float seconds, bool look, bool fire, float px, Mouse mouse)
    {
        var recs = p == null ? null : CounterDefs.Select(c => ProfilerRecorder.StartNew(c.cat, c.name)).ToArray();
        float end = Time.realtimeSinceStartup + seconds;
        int lastFrame = Time.frameCount;
        var player = GameObject.Find("Player");
        if (p != null) { p.FirstFrame = ProfilerDriver.lastFrameIndex + 1; p.YawStart = player ? player.transform.eulerAngles.y : -1; }
        var timings = new FrameTiming[1];
        while (Time.realtimeSinceStartup < end)
        {
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                var state = new MouseState();
                if (look) state.delta = new Vector2(px, Mathf.Sin(Time.realtimeSinceStartup * 2f) * px * 0.3f);
                if (fire) state = state.WithButton(MouseButton.Left, (lastFrame / 4) % 2 == 0);
                InputSystem.QueueStateEvent(mouse, state);

                if (p != null)
                {
                    p.Dt.Add(Time.unscaledDeltaTime * 1000f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        p.Gpu.Add((float)timings[0].gpuFrameTime);
                        p.CpuMain.Add((float)timings[0].cpuMainThreadFrameTime);
                        p.CpuRender.Add((float)timings[0].cpuRenderThreadFrameTime);
                    }
                    for (int i = 0; i < recs.Length; i++)
                    {
                        if (!p.Counters.TryGetValue(CounterDefs[i].name, out var l)) p.Counters[CounterDefs[i].name] = l = new List<long>();
                        if (recs[i].Valid) l.Add(recs[i].LastValue);
                    }
                }
            }
            await Task.Yield();
        }
        if (p != null)
        {
            p.LastFrame = ProfilerDriver.lastFrameIndex;
            p.YawEnd = player ? player.transform.eulerAngles.y : -1;
            foreach (var r in recs) r.Dispose();
        }
    }

    static string Analyze(Phase p)
    {
        var sb = new StringBuilder();
        string Stat(List<float> l) => l.Count == 0 ? "n/a" : $"avg {l.Average():F1} p95 {Pct(l, .95f):F1} max {l.Max():F1}";
        sb.AppendLine($"=== {p.Name}: {p.Dt.Count} frames, dt(ms) {Stat(p.Dt)} -> {1000f / p.Dt.Average():F0} fps");
        sb.AppendLine($"  FTM cpuMain {Stat(p.CpuMain)} | cpuRender {Stat(p.CpuRender)} | gpu {Stat(p.Gpu)}");
        sb.AppendLine("  " + string.Join(" | ", p.Counters.Select(kv => kv.Value.Count == 0 ? $"{kv.Key}=n/a" : $"{kv.Key.Replace(" Count", "")} avg {kv.Value.Average():F0} max {kv.Value.Max()}")));

        int first = Mathf.Max(p.FirstFrame, ProfilerDriver.firstFrameIndex), last = p.LastFrame;
        if (last < first) { sb.AppendLine("  no profiler frames"); return sb.ToString(); }
        int n = last - first + 1;

        // find render thread index
        int renderThread = -1;
        for (int t = 1; t < 64; t++)
        {
            using var raw = ProfilerDriver.GetRawFrameDataView(first, t);
            if (raw == null || !raw.valid) break;
            if (raw.threadName == "Render Thread") { renderThread = t; break; }
        }

        sb.AppendLine($"  yaw {p.YawStart:F0}->{p.YawEnd:F0}");
        var tree = new Dictionary<string, double>();
        var order = new List<string>();
        var frameTotals = new List<(int f, double ms)>();
        for (int f = first; f <= last; f++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
            if (view == null || !view.valid) continue;
            frameTotals.Add((f, view.frameTimeMs));
            Tree(view, view.GetRootItemID(), "", 0, tree, order);
        }
        sb.AppendLine("  [Main tree] total ms/frame (>=0.25):");
        foreach (var k in order.Where(k => tree[k] / n >= 0.25))
        {
            int d = k.Count(c => c == '');
            sb.AppendLine($"    {tree[k] / n,6:F2} {new string(' ', d * 2)}{k.Substring(k.LastIndexOf('') + 1)}");
        }
        sb.AppendLine("  [Spikes] worst frames:");
        foreach (var (f, ms) in frameTotals.OrderByDescending(x => x.ms).Take(4))
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
            var self = new Dictionary<string, double>(); var tot = new Dictionary<string, double>(); var cl = new Dictionary<string, double>(); var g = new Dictionary<string, double>();
            Walk(view, view.GetRootItemID(), "", 0, 0, self, tot, cl, g);
            sb.AppendLine($"    frame {f} {ms:F1}ms: " + string.Join("; ", self.OrderByDescending(x => x.Value).Take(7).Select(x => $"{x.Key.Replace("UnityEngine.CoreModule.dll!", "")} {x.Value:F1}")));
        }

        foreach (var (thread, label, depthTotals) in new[] { (0, "Main", 0), (renderThread, "Render", 0) })
        {
            if (thread < 0) continue;
            var self = new Dictionary<string, double>();
            var total = new Dictionary<string, double>();
            var calls = new Dictionary<string, double>();
            var gc = new Dictionary<string, double>();
            for (int f = first; f <= last; f++)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, thread, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                if (view == null || !view.valid) continue;
                Walk(view, view.GetRootItemID(), "", 0, depthTotals, self, total, calls, gc);
            }
            sb.AppendLine($"  [{label} thread] top self time (ms/frame):");
            foreach (var kv in self.OrderByDescending(k => k.Value).Take(label == "Main" ? 28 : 12))
                sb.AppendLine($"    {kv.Value / n,7:F2}  calls {calls[kv.Key] / n,6:F0}  {kv.Key}");
            if (label == "Main")
            {
                sb.AppendLine("  [Main] GC alloc by marker (bytes/frame, >256):");
                foreach (var kv in gc.Where(k => k.Value / n > 256).OrderByDescending(k => k.Value).Take(15))
                    sb.AppendLine($"    {kv.Value / n,9:F0}  {kv.Key}");
            }
        }
        return sb.ToString();
    }

    static void Tree(HierarchyFrameDataView v, int id, string path, int depth, Dictionary<string, double> tree, List<string> order)
    {
        if (depth > 16) return;
        var kids = new List<int>();
        v.GetItemChildren(id, kids);
        foreach (var c in kids)
        {
            double t = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
            string name = v.GetItemName(c).Replace("UnityEngine.CoreModule.dll!", "").Replace("MB13.Aipocalypse.Core.dll!", "");
            string p = path.Length == 0 ? name : path + "" + name;
            if (!tree.ContainsKey(p)) { tree[p] = 0; int i = order.FindLastIndex(x => x == path || x.StartsWith(path + "")); order.Insert(path.Length == 0 ? order.Count : i + 1, p); }
            tree[p] += t;
            if (t > 0.05) Tree(v, c, p, depth + 1, tree, order);
        }
    }

    static void Walk(HierarchyFrameDataView v, int id, string path, int depth, int maxPathDepth,
        Dictionary<string, double> self, Dictionary<string, double> total, Dictionary<string, double> calls, Dictionary<string, double> gc)
    {
        var kids = new List<int>();
        v.GetItemChildren(id, kids);
        foreach (var c in kids)
        {
            string name = v.GetItemName(c);
            double s = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnSelfTime);
            double t = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
            double cl = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnCalls);
            double g = v.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory);
            self[name] = (self.TryGetValue(name, out var a) ? a : 0) + s;
            calls[name] = (calls.TryGetValue(name, out var b) ? b : 0) + cl;
            if (g > 0) gc[name] = (gc.TryGetValue(name, out var d) ? d : 0) + g;
            string p = path.Length == 0 ? name : path + " > " + name;
            if (depth < maxPathDepth) total[p] = (total.TryGetValue(p, out var e) ? e : 0) + t;
            Walk(v, c, p, depth + 1, maxPathDepth, self, total, calls, gc);
        }
    }

    static float Pct(List<float> l, float q)
    {
        var s = l.OrderBy(x => x).ToList();
        return s[Mathf.Clamp((int)(q * s.Count), 0, s.Count - 1)];
    }
}
