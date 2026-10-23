// Play-mode frame-time probe for map scenes (run via `unity command run_script` while in play mode).
// Teleports the player to each view, lets it settle, then samples frame, CPU and GPU times and draw calls.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PerfProbe
{
    // views: "name:x,z,yaw;name:x,z,yaw;..." (player stands on the ground at x, z facing yaw)
    public static async Task<object> Run(string views, int frames)
    {
        if (!Application.isPlaying) return "enter play mode first";
        var player = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Player");
        var cc = player.GetComponent<CharacterController>();
        var terrain = Terrain.activeTerrain;
        var report = new List<string>();
        foreach (var v in views.Split(';'))
        {
            var parts = v.Split(':'); var n = parts[1].Split(',').Select(float.Parse).ToArray();
            if (cc) cc.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(n[0], terrain.SampleHeight(new Vector3(n[0], 0, n[1])) + 0.1f, n[1]), Quaternion.Euler(0, n[2], 0));
            if (cc) cc.enabled = true;
            await Wait(2.0f);
            var dt = new List<float>(); var cpu = new List<double>(); var gpu = new List<double>(); var batches = new List<int>();
            var timing = new FrameTiming[1];
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            for (int i = 0; i < frames; i++)
            {
                await NextFrame();
                dt.Add(Time.unscaledDeltaTime * 1000f);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0) { cpu.Add(timing[0].cpuFrameTime); gpu.Add(timing[0].gpuFrameTime); }
                batches.Add((int)draws.LastValue);
            }
            float Q(List<float> a, float q) { var s = a.OrderBy(x => x).ToList(); return s[Mathf.Clamp((int)(q * s.Count), 0, s.Count - 1)]; }
            double M(List<double> a) => a.Count == 0 ? 0 : a.OrderBy(x => x).ElementAt(a.Count / 2);
            float med = Q(dt, 0.5f);
            report.Add($"{parts[0]}: {1000f / med:0} fps (median {med:0.0} ms, p95 {Q(dt, 0.95f):0.0} ms) cpu {M(cpu):0.0} gpu {M(gpu):0.0} ms, draws {batches.OrderBy(x => x).ElementAt(batches.Count / 2)}");
        }
        return report;
    }

    static Task NextFrame()
    {
        var tcs = new TaskCompletionSource<bool>();
        int start = Time.frameCount;
        void Tick() { if (Time.frameCount > start) { EditorApplication.update -= Tick; tcs.TrySetResult(true); } }
        EditorApplication.update += Tick;
        return tcs.Task;
    }

    static async Task Wait(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) await NextFrame();
    }
}
