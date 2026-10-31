using System;
using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.EnvironmentSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Night highlight (DAYNIGHT.md → Enemies at night): the "Enemy" rendering layer, the settings asset, enemies joining the layer,
// and the scene wiring (reactor + light under Time Of Day, installer field).
// Every entry point is safe to re-run.
public static class NightHighlightSetup
{
    const string LayerName = "Enemy";
    const string SettingsPath = "Assets/_Game/Data/Environment/NightHighlight.asset";
    const string EnemySettingsPath = "Assets/_Game/Data/Enemies/EnemySystemSettings.asset";

    public static object All() => string.Join("\n", Assets(), Scene());

    public static string Assets()
    {
        var log = new List<string>();
        RenderingLayerMask mask = EnsureLayer(log);

        var settings = AssetDatabase.LoadAssetAtPath<NightHighlightSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<NightHighlightSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            log.Add("created " + SettingsPath);
        }
        settings.Layer = mask;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);

        var enemies = AssetDatabase.LoadAssetAtPath<EnemySystemSettings>(EnemySettingsPath);
        enemies.RenderingLayer = mask;
        EditorUtility.SetDirty(enemies);
        AssetDatabase.SaveAssetIfDirty(enemies);
        log.Add($"layer {LayerName} = bits {mask.value} on the highlight and enemy settings");
        return string.Join("\n", log);
    }

    // Appends the layer to Tags and Layers → Rendering Layers if it isn't there.
    static RenderingLayerMask EnsureLayer(List<string> log)
    {
        int index = RenderingLayerMask.NameToRenderingLayer(LayerName);
        if (index < 0)
        {
            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(tagManager);
            SerializedProperty layers = so.FindProperty("m_RenderingLayers");
            index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            layers.GetArrayElementAtIndex(index).stringValue = LayerName;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(tagManager);
            log.Add($"added rendering layer {index} {LayerName}");
        }
        return RenderingLayerMask.GetMask(LayerName);
    }

    public static string Scene()
    {
        Scene scene = SceneManager.GetActiveScene();
        var director = Object.FindAnyObjectByType<EnvironmentDirector>() ?? throw new Exception("no EnvironmentDirector");
        var settings = AssetDatabase.LoadAssetAtPath<NightHighlightSettings>(SettingsPath);

        Transform parent = director.transform;
        Transform existing = parent.Find("Night Highlight");
        GameObject root = existing != null ? existing.gameObject : new GameObject("Night Highlight");
        root.transform.SetParent(parent, false);
        if (!root.TryGetComponent(out NightHighlight highlight))
            highlight = root.AddComponent<NightHighlight>();

        Transform lightTransform = root.transform.Find("Enemy Light");
        GameObject lightObject = lightTransform != null ? lightTransform.gameObject : new GameObject("Enemy Light");
        lightObject.transform.SetParent(root.transform, false);
        if (!lightObject.TryGetComponent(out Light light))
            light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.intensity = 0f;
        light.enabled = false;
        if (!lightObject.TryGetComponent(out UniversalAdditionalLightData lightData))
            lightData = lightObject.AddComponent<UniversalAdditionalLightData>();
        lightData.renderingLayers = settings.Layer;

        var so = new SerializedObject(highlight);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_light").objectReferenceValue = light;
        so.FindProperty("_source").enumValueIndex = (int)ReactorSource.Darkness;
        so.FindProperty("_response").floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        if (!scope.TryGetComponent(out EnvironmentInstaller installer))
            throw new Exception("no EnvironmentInstaller; run DayNightSetup.Scene first");
        var iso = new SerializedObject(installer);
        iso.FindProperty("_nightHighlight").objectReferenceValue = highlight;
        iso.ApplyModifiedPropertiesWithoutUndo();

        director.CollectSceneReactors();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"scene: {root.name} under {parent.name}, light layers {lightData.renderingLayers.value}, installer wired";
    }
}
