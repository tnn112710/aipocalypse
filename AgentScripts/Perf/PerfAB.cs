using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PerfAB
{
    static readonly float[] Yaws = { 0, 90, 180, 270 };

    public static async Task<string> Run(string outName = "ab", int frames = 40, int from = 0, int to = 99)
    {
        if (!EditorApplication.isPlaying) return "Not in play mode";
        var sb = new StringBuilder();
        var player = GameObject.Find("Player").transform;
        var startRot = player.rotation;
        var rp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var rpSo = new SerializedObject(rp);
        var rd = (UniversalRendererData)rpSo.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
        var terrain = Terrain.activeTerrain;
        var env = GameObject.Find("Environment");
        var atmosphere = env ? env.transform.Find("Atmosphere") : null;

        // snapshot for restore
        float shadowDist = rp.shadowDistance; int cascades = rp.shadowCascadeCount; Vector3 split4 = rp.cascade4Split; float split2 = rp.cascade2Split;
        int softQ = rpSo.FindProperty("m_SoftShadowQuality").intValue;
        var depthPriming = rd.depthPrimingMode;
        float lodBias = QualitySettings.lodBias;
        var features = rd.rendererFeatures.Where(f => f != null).ToDictionary(f => f, f => f.isActive);
        float treeDist = terrain.treeDistance, detailDist = terrain.detailObjectDistance, detailDensity = terrain.detailObjectDensity;
        var terrainShadows = terrain.shadowCastingMode;
        bool atmoActive = atmosphere && atmosphere.gameObject.activeSelf;

        void SetSoft(int q) { rpSo.Update(); rpSo.FindProperty("m_SoftShadowQuality").intValue = q; rpSo.ApplyModifiedPropertiesWithoutUndo(); }
        void Feature(string typeName, bool on) { foreach (var f in features.Keys) if (f.GetType().Name == typeName) f.SetActive(on); }

        var configs = new List<(string name, Action apply)>
        {
            ("baseline", () => { }),
            ("depthPriming=Disabled", () => rd.depthPrimingMode = DepthPrimingMode.Disabled),
            ("shadows 100m/4casc (doc)", () => { rp.shadowDistance = 100; rp.shadowCascadeCount = 4; rp.cascade4Split = new Vector3(0.05f, 0.16f, 0.42f); }),
            ("shadows 80m/4casc+softMed", () => { rp.shadowDistance = 80; rp.shadowCascadeCount = 4; rp.cascade4Split = new Vector3(0.06f, 0.2f, 0.5f); SetSoft(2); }),
            ("lodBias 0.6", () => QualitySettings.lodBias = 0.6f),
            ("terrain treeDist 400", () => terrain.treeDistance = 400),
            ("terrain detailDist 80", () => terrain.detailObjectDistance = 80),
            ("terrain detailDensity 0.6", () => terrain.detailObjectDensity = 0.6f),
            ("terrain no shadows", () => terrain.shadowCastingMode = ShadowCastingMode.Off),
            ("water feature off", () => Feature("StylizedWaterRenderFeature", false)),
            ("grass feature off", () => Feature("GrassRenderFeature", false)),
            ("decal feature off", () => Feature("DecalRendererFeature", false)),
            ("atmosphere FX off", () => { if (atmosphere) atmosphere.gameObject.SetActive(false); }),
            ("baseline again", () => { }),
        };

        void Restore()
        {
            rp.shadowDistance = shadowDist; rp.shadowCascadeCount = cascades; rp.cascade4Split = split4; rp.cascade2Split = split2; SetSoft(softQ);
            rd.depthPrimingMode = depthPriming;
            QualitySettings.lodBias = lodBias;
            foreach (var kv in features) kv.Key.SetActive(kv.Value);
            terrain.treeDistance = treeDist; terrain.detailObjectDistance = detailDist; terrain.detailObjectDensity = detailDensity; terrain.shadowCastingMode = terrainShadows;
            if (atmosphere) atmosphere.gameObject.SetActive(atmoActive);
        }

        sb.AppendLine($"pos {player.position} frames/view {frames}; columns per view: PlayerLoop ms / render(main) ms / GPU ms / draws / Mtris / shadowCasters");
        double baseLoop = 0, baseGpu = 0;
        try
        {
            foreach (var (name, apply) in configs.Skip(from).Take(to - from + 1).Prepend(configs[0]).Distinct())
            {
                Restore();
                apply();
                var row = new StringBuilder();
                double sumLoop = 0, sumGpu = 0, sumRender = 0;
                foreach (var yaw in Yaws)
                {
                    var m = await Measure(player, yaw, frames);
                    sumLoop += m.loop; sumGpu += m.gpu; sumRender += m.render;
                    row.Append($" | {yaw,3}: {m.loop,5:F1}/{m.render,5:F1}/{m.gpu,4:F1}/{m.draws,5:F0}/{m.tris / 1e6,4:F1}/{m.casters,4:F0}");
                }
                double loop = sumLoop / Yaws.Length, gpu = sumGpu / Yaws.Length, render = sumRender / Yaws.Length;
                if (name == "baseline") { baseLoop = loop; baseGpu = gpu; }
                sb.AppendLine($"{name,-28} avg loop {loop,5:F2} ({loop - baseLoop:+0.00;-0.00}) render {render,5:F2} gpu {gpu,4:F2} ({gpu - baseGpu:+0.00;-0.00}){row}");
            }
        }
        finally
        {
            Restore();
            player.rotation = startRot;
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "claude_perf_" + outName + ".txt"), sb.ToString());
        return sb.ToString();
    }

    struct M { public double loop, render, gpu, draws, tris, casters; }

    static async Task<M> Measure(Transform player, float yaw, int frames)
    {
        player.rotation = Quaternion.Euler(0, yaw, 0);
        await Frames(25);
        using var loop = ProfilerRecorder.StartNew(new ProfilerCategory("Internal"), "PlayerLoop", frames + 5);
        using var render = ProfilerRecorder.StartNew(new ProfilerCategory("Render"), "RenderPlayModeViewCameras", frames + 5);
        using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", frames + 5);
        using var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", frames + 5);
        using var casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count", frames + 5);
        var gpu = new List<double>();
        var t = new FrameTiming[1];
        int start = Time.frameCount;
        while (Time.frameCount - start < frames)
        {
            int f = Time.frameCount;
            while (Time.frameCount == f) await Task.Yield();
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, t) > 0 && t[0].gpuFrameTime > 0) gpu.Add(t[0].gpuFrameTime);
        }
        return new M
        {
            loop = Avg(loop) / 1e6, render = Avg(render) / 1e6, gpu = gpu.Count > 0 ? gpu.Average() : -1,
            draws = Avg(draws), tris = Avg(tris), casters = Avg(casters),
        };
    }

    static double Avg(ProfilerRecorder r)
    {
        int n = r.Count; if (n == 0) return -1;
        double s = 0; for (int i = 0; i < n; i++) s += r.GetSample(i).Value;
        return s / n;
    }

    static async Task Frames(int n)
    {
        int target = Time.frameCount + n;
        while (Time.frameCount < target) await Task.Yield();
    }
}
