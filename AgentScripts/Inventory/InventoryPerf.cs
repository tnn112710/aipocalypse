using System.IO;
using MB13.Aipocalypse.Editor.Build;
using UnityEditor;
using UnityEngine;

// Performance pass of the inventory and loot in a player build (EQUIPMENT.md → Performance):
// Build: a development build of WindowsDevelopment with the Pipeline server (so the CLI can drive the player), into
// _builds/. The config asset isn't touched: a copy carries the Pipeline flag.
// Run: unity command script run --file AgentScripts/Inventory/InventoryPerf.cs --entry InventoryPerf.Build --detach
public static class InventoryPerf
{
    const string ConfigPath = "Assets/_Game/Data/Build/WindowsDevelopment.asset";

    public static string Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<BuildConfig>(ConfigPath);
        if (source == null)
            return "missing " + ConfigPath;
        BuildConfig config = Object.Instantiate(source);
        config.name = source.name;
        config.Pipeline = true;
        string folder = AutoBuild.GetOutputFolder(config);
        BuildOutcome outcome = AutoBuild.Build(config, folder);
        Object.DestroyImmediate(config);
        File.WriteAllText(Path.Combine(AutoBuild.BuildsRoot, "inventory_perf_build.txt"), $"{outcome.Result} {folder}");
        return $"{outcome.Name}: {outcome.Result} → {folder}";
    }
}
