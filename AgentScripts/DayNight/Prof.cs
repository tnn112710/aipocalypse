using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;

// Records Play-mode frames with the editor profiler and reports where the slow ones spend their time.
public static class Prof
{
    public static object Start()
    {
        var settings = System.Type.GetType("UnityEditor.Profiling.ProfilerUserSettings, UnityEditor");
        settings?.GetProperty("frameCount")?.SetValue(null, 2000);
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        return "recording";
    }

    public static object StartGpu()
    {
        ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, true);
        return Start();
    }

    public static object Mark() => ProfilerDriver.lastFrameIndex;

    // Median CPU and GPU frame time over a frame range.
    public static object Window(int from, int to)
    {
        var cpu = new List<float>();
        var gpu = new List<float>();
        var allocs = new Dictionary<string, float>();
        var children = new List<int>();
        for (int f = from; f <= to; f++)
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
                if (view.valid)
                {
                    cpu.Add(view.frameTimeMs);
                    gpu.Add(view.frameGpuTimeMs);
                    // GC.Alloc samples, charged to their parent marker (outside the editor loop).
                    var stack = new Stack<(int id, string parent)>();
                    stack.Push((view.GetRootItemID(), ""));
                    while (stack.Count > 0)
                    {
                        var (id, parent) = stack.Pop();
                        string name = view.GetItemName(id);
                        if (name == "EditorLoop")
                            continue;
                        if (name == "GC.Alloc")
                            allocs[parent] = allocs.GetValueOrDefault(parent) + view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
                        children.Clear();
                        view.GetItemChildren(id, children);
                        foreach (int child in children)
                            stack.Push((child, name));
                    }
                }
        int n = System.Math.Max(1, cpu.Count);
        string topAllocs = string.Join("; ", allocs.OrderByDescending(p => p.Value).Take(5).Select(p => $"{p.Key} {p.Value / n:F0} B/frame"));
        return $"{cpu.Count} frames  cpu median {Median(cpu):F1} ms  max {cpu.DefaultIfEmpty().Max():F1}  >25ms {cpu.Count(c => c > 25f)}  gpu median {Median(gpu):F1} ms  gpu p90 {P90(gpu):F1}  GC {allocs.Values.Sum() / n:F0} B/frame: {topAllocs}";
    }

    // Markers whose average self time grew most from range B (baseline) to range A, on one thread.
    public static object Compare(int aFrom, int aTo, int bFrom, int bTo, string thread, int top)
    {
        var a = Average(Enumerable.Range(aFrom, aTo - aFrom + 1).ToList(), thread);
        var b = Average(Enumerable.Range(bFrom, bTo - bFrom + 1).ToList(), thread);
        var text = new StringBuilder($"{thread}: marker  A ms (B ms)\n");
        foreach (var pair in a.Where(p => p.Key != "EditorLoop" && p.Key != "RenderLoop").OrderByDescending(p => p.Value - b.GetValueOrDefault(p.Key)).Take(top))
            text.AppendLine($"{pair.Key}  {pair.Value:F3}  ({b.GetValueOrDefault(pair.Key):F3})");
        return text.ToString();
    }

    // The worst frames in a range and their heaviest main-thread markers (self time), editor loop excluded.
    public static object Spikes(int from, int to, int count, int top)
    {
        var frames = new List<(int frame, float ms)>();
        for (int f = from; f <= to; f++)
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
                if (view.valid)
                    frames.Add((f, view.frameTimeMs));
        var text = new StringBuilder();
        foreach (var (frame, ms) in frames.OrderByDescending(f => f.ms).Take(count))
        {
            var markers = Average(new List<int> { frame }, "Main Thread");
            text.Append($"frame {frame} {ms:F1} ms: ");
            foreach (var pair in markers.Where(p => p.Key != "EditorLoop").OrderByDescending(p => p.Value).Take(top))
                text.Append($"{pair.Key} {pair.Value:F2}; ");
            text.AppendLine();
        }
        return text.ToString();
    }

    static float P90(List<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0f : sorted[(int)(sorted.Count * 0.9f)];
    }

    public static object Stop()
    {
        ProfilerDriver.enabled = false;
        return $"frames {ProfilerDriver.firstFrameIndex}..{ProfilerDriver.lastFrameIndex}";
    }

    // Frame-time series (averaged per bucket), then the top self-time markers of the slowest frames vs the median frames.
    public static object Report(int bucket, int top)
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        var times = new List<(int frame, float ms)>();
        for (int f = first; f <= last; f++)
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
                if (view.valid)
                    times.Add((f, view.frameTimeMs));

        var text = new StringBuilder();
        text.AppendLine($"{times.Count} frames, median {Median(times.Select(t => t.ms))} ms");
        for (int i = 0; i < times.Count; i += bucket)
        {
            var slice = times.Skip(i).Take(bucket).ToList();
            text.Append($"{slice.Average(t => t.ms):F1}/{slice.Max(t => t.ms):F0} ");
        }
        text.AppendLine();

        float median = Median(times.Select(t => t.ms));
        var slow = times.Where(t => t.ms > median * 1.5f).Select(t => t.frame).ToList();
        var normal = times.Where(t => t.ms <= median * 1.1f).Select(t => t.frame).ToList();
        text.AppendLine($"slow frames (>1.5x median): {slow.Count}, normal: {normal.Count}");

        foreach (string thread in new[] { "Main Thread", "Render Thread" })
        {
            var slowMarkers = Average(slow, thread);
            var normalMarkers = Average(normal, thread);
            text.AppendLine($"-- {thread}: marker  slow-avg ms  (normal-avg ms)");
            foreach (var pair in slowMarkers.OrderByDescending(p => p.Value - normalMarkers.GetValueOrDefault(p.Key)).Take(top))
                text.AppendLine($"{pair.Key}  {pair.Value:F2}  ({normalMarkers.GetValueOrDefault(pair.Key):F2})");
        }
        return text.ToString();
    }

    static Dictionary<string, float> Average(List<int> frames, string threadName)
    {
        var sums = new Dictionary<string, float>();
        if (frames.Count == 0)
            return sums;
        var children = new List<int>();
        foreach (int f in frames)
        {
            int threadIndex = ThreadIndex(f, threadName);
            if (threadIndex < 0)
                continue;
            using var view = ProfilerDriver.GetHierarchyFrameDataView(f, threadIndex, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
            if (!view.valid)
                continue;
            var stack = new Stack<int>();
            stack.Push(view.GetRootItemID());
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                string name = view.GetItemName(id);
                float self = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                sums[name] = sums.GetValueOrDefault(name) + self;
                children.Clear();
                view.GetItemChildren(id, children);
                foreach (int child in children)
                    stack.Push(child);
            }
        }
        return sums.ToDictionary(p => p.Key, p => p.Value / frames.Count);
    }

    static int ThreadIndex(int frame, string name)
    {
        for (int i = 0; i < 64; i++)
            using (var raw = ProfilerDriver.GetRawFrameDataView(frame, i))
            {
                if (!raw.valid)
                    return -1;
                if (raw.threadName == name)
                    return i;
            }
        return -1;
    }

    static float Median(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0f : sorted[sorted.Count / 2];
    }
}
