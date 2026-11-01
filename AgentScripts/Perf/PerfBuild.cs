using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

// A development player of WorldMap for profiling outside the editor (the scene list is passed here, the build profiles are
// left alone). Run it with Unity's profiler command-line arguments and the dev console's -console commands, e.g. a night
// fight on autopilot streamed to a .raw capture:
//   Builds/Perf/aipocalypse.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
//     -profiler-enable -profiler-log-file <path>.raw -profiler-capture-frame-count 3000 -profiler-maxusedmemory 268435456
//     -console "app.background on; cheat.god on; cheat.infammo on; cheat.take ak47; clock.set 21; raid.start; cheat.autopilot on"
// then read it in the editor with PerfSuite.Capture(<path>.raw). Builds/ is gitignored.
// Run: unity command script run --file AgentScripts/Perf/PerfBuild.cs --entry PerfBuild.Build --timeout 3600
public static class PerfBuild
{
    const string Scene = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string Output = "Builds/Perf/aipocalypse.exe";

    public static string Build()
    {
        var watch = Stopwatch.StartNew();
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        });
        BuildSummary summary = report.summary;
        string errors = string.Join("\n", report.steps.SelectMany(s => s.messages)
            .Where(m => m.type is UnityEngine.LogType.Error or UnityEngine.LogType.Exception).Select(m => m.content).Take(10));
        return $"{summary.result} in {watch.Elapsed.TotalMinutes:F1} min, {summary.totalSize / 1048576} MB, {summary.totalErrors} errors, " +
               $"backend {PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone)} -> {Path.GetFullPath(Output)}\n{errors}";
    }
}
