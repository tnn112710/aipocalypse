using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Screen looks (docs/technicals/NEEDS.md → Sanity, PLUGINS.md → SC Screen Effects): six volume profiles (the three sanity looks,
// URP-only and SC Screen Effects), the two VolumeLookBackendAssets, ScreenLookSettings (Backend = SC, the user's pick), SC's
// two renderer features on PC_Renderer, and ScreenLookInstaller on the open WorldMap's scope. Safe to re-run: profiles are
// rewritten in place, features and the installer are added once. SC types are reached by name, so only this file and the SC
// profiles know the pack.
// Run: unity command script run --file AgentScripts/Graphics/ScreenLookSetup.cs --entry ScreenLookSetup.Build
public static class ScreenLookSetup
{
    const string Dir = "Assets/_Game/Data/Graphics/ScreenLooks";
    const string RendererPath = "Assets/_Game/Settings/PC_Renderer.asset";
    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string ScAssembly = "sc.screeneffects.runtime";
    static readonly string[] ScFeatures = { "sc.screeneffects.runtime.ColorSplitRenderer", "sc.screeneffects.runtime.DoubleVisionRenderer" };

    public static string Build()
    {
        var log = new List<string>();
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Graphics"))
            AssetDatabase.CreateFolder("Assets/_Game/Data", "Graphics");
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Data/Graphics", "ScreenLooks");

        var urp = Backend("UrpLooks", false);
        var sc = Backend("ScLooks", true);
        log.Add("backends: UrpLooks, ScLooks (3 profiles each)");

        string settingsPath = $"{Dir}/ScreenLookSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<ScreenLookSettings>(settingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ScreenLookSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }
        settings.Backend = sc;
        Save(settings);
        log.Add("ScreenLookSettings: Backend = ScLooks");
        log.Add(Features());
        log.Add(Installer(settings));
        return string.Join("\n", log);
    }

    static VolumeLookBackendAsset Backend(string name, bool withSc)
    {
        string path = $"{Dir}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<VolumeLookBackendAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<VolumeLookBackendAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }
        string suffix = withSc ? "SC" : "URP";
        asset.Looks = new Dictionary<ScreenLookId, VolumeLook>
        {
            { ScreenLookId.SanityUneasy, new VolumeLook { Profile = Profile($"Look_SanityUneasy_{suffix}", Uneasy) } },
            { ScreenLookId.SanityLow, new VolumeLook { Profile = Profile($"Look_SanityLow_{suffix}", p => Low(p, withSc)), PulseRate = withSc ? 0.35f : 0f, PulseDepth = withSc ? 0.25f : 0f } },
            { ScreenLookId.SanityCritical, new VolumeLook { Profile = Profile($"Look_SanityCritical_{suffix}", p => Critical(p, withSc)), PulseRate = 0.5f, PulseDepth = 0.4f } },
        };
        Save(asset);
        return asset;
    }

    // Darker edges, a little colour gone.
    static void Uneasy(VolumeProfile profile)
    {
        Vignette(profile, 0.3f);
        Saturation(profile, -15f);
    }

    static void Low(VolumeProfile profile, bool withSc)
    {
        Vignette(profile, 0.38f);
        Saturation(profile, -40f);
        Override<FilmGrain>(profile).intensity.Override(0.3f);
        if (withSc)
            Sc(profile, "ColorSplit", ("intensity", 1f), ("offset", 0.4f));
    }

    static void Critical(VolumeProfile profile, bool withSc)
    {
        Vignette(profile, 0.45f);
        Saturation(profile, -70f);
        Override<FilmGrain>(profile).intensity.Override(0.45f);
        Override<ChromaticAberration>(profile).intensity.Override(withSc ? 0.3f : 0.6f);
        if (!withSc)
            return;
        Sc(profile, "ColorSplit", ("intensity", 1f), ("offset", 0.9f));
        Sc(profile, "DoubleVision", ("intensity", 0.6f), ("angle", 45f), ("edgeMask", 0.4f));
    }

    static void Vignette(VolumeProfile profile, float intensity)
    {
        var vignette = Override<Vignette>(profile);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(intensity);
        vignette.smoothness.Override(0.45f);
    }

    static void Saturation(VolumeProfile profile, float saturation) => Override<ColorAdjustments>(profile).saturation.Override(saturation);

    static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>();
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;
        return component;
    }

    // An SC Screen Effects override by type name, its shader serialized (SerializeShader, as SC's own Reset does) and the
    // listed float parameters overridden.
    static void Sc(VolumeProfile profile, string type, params (string Field, float Value)[] values)
    {
        Type effect = Type.GetType($"sc.screeneffects.runtime.{type}, {ScAssembly}") ?? throw new Exception($"SC Screen Effects has no {type}");
        VolumeComponent component = profile.components.Find(c => c != null && c.GetType() == effect);
        if (component == null)
        {
            component = profile.Add(effect);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;
        effect.GetMethod("SerializeShader", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(component, null);
        foreach ((string field, float value) in values)
        {
            var parameter = effect.GetField(field)?.GetValue(component) as VolumeParameter<float>
                ?? throw new Exception($"{type} has no float parameter {field}");
            parameter.Override(value);
        }
        EditorUtility.SetDirty(component);
    }

    static VolumeProfile Profile(string name, Action<VolumeProfile> fill)
    {
        string path = $"{Dir}/{name}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        fill(profile);
        Save(profile);
        return profile;
    }

    // SC's renderer feature for each SC effect a profile uses, once.
    static string Features()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath) ?? throw new Exception("missing " + RendererPath);
        var added = new List<string>();
        foreach (string typeName in ScFeatures)
        {
            Type type = Type.GetType($"{typeName}, {ScAssembly}") ?? throw new Exception("missing " + typeName);
            if (data.rendererFeatures.Exists(f => f != null && f.GetType() == type))
                continue;
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = type.Name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            added.Add(type.Name);
        }
        if (added.Count == 0)
            return "PC_Renderer: SC features already there";
        data.SetDirty();
        Save(data);
        return "PC_Renderer: added " + string.Join(", ", added);
    }

    static string Installer(ScreenLookSettings settings)
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
        var installer = scope.GetComponent<ScreenLookInstaller>() ?? Undo.AddComponent<ScreenLookInstaller>(scope.gameObject);
        var so = new SerializedObject(installer);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (wasDirty)
            return "ScreenLookInstaller set; the scene had other unsaved changes, so it is NOT saved";
        EditorSceneManager.SaveScene(scene);
        return "ScreenLookInstaller on the scope, scene saved";
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
