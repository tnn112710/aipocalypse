using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Survival needs (docs/technicals/NEEDS.md): the settings asset (Settings) and wiring it in (Wire: PlayerTuning's needs
// field, NeedsInstaller on the open WorldMap's GameLifetimeScope; the campfire's SafeLight is on its prefab, CampfireSetup). Safe to re-run: an existing asset is left as tuned;
// ResetSettings writes the design's starting values (the class's field defaults) again. Wire saves the open scene only
// when it was clean before.
// Run: unity command script run --file AgentScripts/Needs/NeedsSetup.cs --entry NeedsSetup.Settings (then NeedsSetup.Wire)
public static class NeedsSetup
{
    const string SettingsPath = "Assets/_Game/Data/Player/PlayerNeedsSettings.asset";
    const string TuningPath = "Assets/_Game/Data/Player/PlayerTuning.asset";
    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";

    public static string Settings() => WriteSettings(false);

    public static string ResetSettings() => WriteSettings(true);

    static string WriteSettings(bool reset)
    {
        var log = new List<string>();
        var asset = AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>(SettingsPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<PlayerNeedsSettings>();
            AssetDatabase.CreateAsset(asset, SettingsPath);
            log.Add($"created {SettingsPath}");
        }
        else if (reset)
        {
            var defaults = ScriptableObject.CreateInstance<PlayerNeedsSettings>();
            EditorUtility.CopySerialized(defaults, asset);
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(asset);
            log.Add($"reset {SettingsPath} to the starting values");
        }
        else
            log.Add($"kept {SettingsPath}");
        AssetDatabase.SaveAssetIfDirty(asset);
        return string.Join("\n", log);
    }

    public static string Wire()
    {
        var log = new List<string>();
        var settings = AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>(SettingsPath);
        var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
        if (settings == null || tuning == null)
            return $"missing {SettingsPath} or {TuningPath}: run Settings first";
        var serialized = new SerializedObject(tuning);
        SerializedProperty needs = serialized.FindProperty("_needs");
        if (needs.objectReferenceValue != settings)
        {
            needs.objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(tuning);
            log.Add("PlayerTuning: needs settings assigned");
        }
        else
            log.Add("PlayerTuning: already wired");
        log.Add(AddInstaller<NeedsInstaller>());
        return string.Join("\n", log);
    }

    // The yard campfire's SafeLight now comes with its prefab (Content/Structures/Campfire, CampfireSetup; CAMPFIRE.md).
    public static string Campfire() => "the campfire prefab carries its SafeLight: run AgentScripts/Campfire/CampfireSetup.cs (Scene)";

    // Adds a scene installer to the open WorldMap's GameLifetimeScope and saves the scene if it was clean before.
    internal static string AddInstaller<T>() where T : SceneInstaller
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            return $"open {ScenePath} first (active: {scene.path})";
        bool wasDirty = scene.isDirty;
        GameLifetimeScope scope = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            scope = root.GetComponentInChildren<GameLifetimeScope>(true);
            if (scope != null)
                break;
        }
        if (scope == null)
            return "no GameLifetimeScope in the scene";
        if (scope.GetComponent<T>() != null)
            return $"{typeof(T).Name}: already on {scope.name}";
        Undo.AddComponent<T>(scope.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (wasDirty)
            return $"{typeof(T).Name}: added to {scope.name}; the scene had other unsaved changes, so it is NOT saved";
        EditorSceneManager.SaveScene(scene);
        return $"{typeof(T).Name}: added to {scope.name}, scene saved";
    }
}
