// Project settings from the 2026-10-25 perf pass (edit mode, run with `unity command script run`). Safe to re-run.
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class PerfSettings
{
    public static string Apply()
    {
        // Mip streaming on with zero streaming textures only costs TextureStreamingManager.UpdateRenderers (~0.5 ms/frame).
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize; i++)
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("streamingMipmapsActive").boolValue = false;
        quality.ApplyModifiedPropertiesWithoutUndo();

        // A* compiles out AstarPath.OnGUI, the only OnGUI left in builds (IMGUI runs and allocates every frame for it).
        var standalone = NamedBuildTarget.Standalone;
        var defines = PlayerSettings.GetScriptingDefineSymbols(standalone).Split(';').Where(d => d.Length > 0).ToList();
        if (!defines.Contains("ASTAR_NO_GUI"))
        {
            defines.Add("ASTAR_NO_GUI");
            PlayerSettings.SetScriptingDefineSymbols(standalone, string.Join(";", defines));
        }

        AssetDatabase.SaveAssets();
        return State();
    }

    public static string State() =>
        $"streamingMipmapsActive {QualitySettings.streamingMipmapsActive} (level {QualitySettings.names[QualitySettings.GetQualityLevel()]}) | " +
        $"Standalone defines {PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone)}";
}
