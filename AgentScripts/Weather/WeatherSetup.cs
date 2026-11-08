using System;
using System.Collections.Generic;
using System.Linq;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnvironmentSystem;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using DistantLands.Cozy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Volume = UnityEngine.Rendering.Volume;

// Weather phase A setup (docs/technicals/WEATHER.md). Run with `unity command script run --file AgentScripts/Weather/WeatherSetup.cs
// --entry WeatherSetup.<Entry>`. Each step is safe to re-run and saves only what it wrote.
//   All()      = Profiles + Audio + Settings + Scene
//   Profiles() = our COZY weather profile copies (no COZY audio / thunder / inert FX)
//   Audio()    = the weather sounds in the Ambience library
//   Settings() = Data/Environment/Weather/Weather.asset (+ the lightning volume profile); keeps tuned values unless reset
//   Scene()    = WorldMap wiring: Weather Wind (replaces the Raygeas wind drivers), Lightning Flash, installer fields
public static class WeatherSetup
{
    const string CozyProfiles = "Packages/space.distantlands.cozyweather.core/Content/Profiles/";
    const string ProfileDir = "Assets/_Game/Data/Environment/Cozy/Weather";
    const string WeatherDir = "Assets/_Game/Data/Environment/Weather";
    const string SettingsPath = WeatherDir + "/Weather.asset";
    const string LightningProfilePath = WeatherDir + "/PP_Lightning.asset";
    const string AudioLibPath = "Assets/_Game/Data/Audio/Ambience.asset";
    const string AudioDir = "Assets/_Game/Data/Audio/Ambience";
    const string Ithappy = "Assets/_StoreAssets/Audio/ithappy/Nature_Sound_FX/Weather/";

    // FX COZY plays itself that we replace (audio, thunder) or that do nothing without their modules (precipitation and
    // climate need the Climate module, wind the Wind module, events have no listener).
    static readonly string[] StrippedFX = { "AudioFX", "ThunderFX", "PrecipitationFX", "ClimateFX", "WindFX", "EventFX" };

    // Sky weather → (COZY profile to copy, VFX Graph FX swaps old → new).
    static readonly (SkyWeather sky, string source, string swapFrom, string swapTo)[] Profiles_ =
    {
        (SkyWeather.Clear, "Clear", null, null),
        (SkyWeather.PartlyCloudy, "Partly Cloudy", null, null),
        (SkyWeather.Cloudy, "Mostly Cloudy", null, null),
        (SkyWeather.Overcast, "Overcast", null, null),
        (SkyWeather.LightRain, "Light Rain", "Stylized Light Rain", "Stylized Drizzle"),
        (SkyWeather.MediumRain, "Light Rain", null, null),
        (SkyWeather.HeavyRain, "Thunder Storm", null, null),
        (SkyWeather.Fog, "Dense Fog", null, null),
    };

    public static string All(bool resetSettings = false) =>
        string.Join("\n", Profiles(), Audio(), Settings(resetSettings), Scene());

    static string ProfilePath(SkyWeather sky) => $"{ProfileDir}/Weather_{sky}.asset";

    public static string Profiles()
    {
        EnsureFolder(ProfileDir);
        var log = new List<string>();
        foreach (var (sky, source, swapFrom, swapTo) in Profiles_)
        {
            string path = ProfilePath(sky);
            if (AssetDatabase.LoadAssetAtPath<WeatherProfile>(path) == null &&
                !AssetDatabase.CopyAsset($"{CozyProfiles}Weather Profiles/{source}.asset", path))
                throw new Exception("could not copy " + source);
            var profile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(path);
            var so = new SerializedObject(profile);
            SerializedProperty fx = so.FindProperty("FX");
            var kept = new List<Object>();
            var removed = new List<string>();
            for (int i = 0; i < fx.arraySize; i++)
            {
                Object item = fx.GetArrayElementAtIndex(i).objectReferenceValue;
                if (item == null)
                    continue;
                if (StrippedFX.Contains(item.GetType().Name))
                {
                    removed.Add(item.name);
                    continue;
                }
                if (swapFrom != null && item.name == swapFrom)
                {
                    item = AssetDatabase.LoadAssetAtPath<Object>($"{CozyProfiles}FX/VFX Graph FX/{swapTo}.asset")
                           ?? throw new Exception("missing FX " + swapTo);
                }
                kept.Add(item);
            }
            fx.arraySize = kept.Count;
            for (int i = 0; i < kept.Count; i++)
                fx.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
            // Our weather never forecasts: drop the links to COZY's other profiles.
            so.FindProperty("forecastNext").arraySize = 0;
            so.FindProperty("description").stringValue = $"aipocalypse {sky}: copy of COZY's {source}" + (swapTo != null ? $" with {swapTo}" : "");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            log.Add($"{sky}: {string.Join(", ", kept.Select(k => k.name))}" + (removed.Count > 0 ? $" (removed {string.Join(", ", removed)})" : ""));
        }
        return string.Join("\n", log);
    }

    public static string Audio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(AudioLibPath) ?? throw new Exception("missing " + AudioLibPath);
        Entity(library, "RainLight", true, 0.6f, 0f, Ithappy + "Drizzle_Loop_v1.wav");
        Entity(library, "RainMedium", true, 0.7f, 0f, Ithappy + "Light_Rain_Loop_v1.wav");
        Entity(library, "RainHeavy", true, 0.8f, 0f, Ithappy + "Heavy_Rain_Loop_v1.wav");
        Entity(library, "WindStrong", true, 0.6f, 0f, Ithappy + "Strong_Wind_Loop_v1.wav");
        Entity(library, "Thunder", false, 0.9f, 0.1f, Ithappy + "Close_Thunder_v1.wav", Ithappy + "Close_Thunder_v2.wav");
        return "audio: RainLight, RainMedium, RainHeavy, WindStrong, Thunder in " + AudioLibPath;
    }

    static AudioEntity Entity(AudioAsset library, string name, bool loop, float volume, float pitchRandom, params string[] clips)
    {
        string path = $"{AudioDir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.Ambience);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Loop>k__BackingField").boolValue = loop;
        so.FindProperty("MulticlipsPlayMode").intValue = clips.Length > 1 ? 2 : 0;
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        SerializedProperty list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]) ?? throw new Exception("missing clip " + clips[i]);
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue = clip;
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entity);
        AssetDatabase.SaveAssetIfDirty(entity);
        return entity;
    }

    static SoundID Sound(string name) =>
        new(AssetDatabase.LoadAssetAtPath<AudioEntity>($"{AudioDir}/{name}.asset") ?? throw new Exception("missing sound " + name + " (run Audio first)"));

    public static string Settings(bool reset = false)
    {
        EnsureFolder(WeatherDir);
        var settings = AssetDatabase.LoadAssetAtPath<WeatherSettings>(SettingsPath);
        bool created = settings == null;
        if (created)
        {
            settings = ScriptableObject.CreateInstance<WeatherSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        if (created || reset)
        {
            settings.Enabled = true;
            settings.StartSky = SkyWeather.PartlyCloudy;
            settings.TransitionSeconds = 20f;
            settings.RainSlant = 0.5f;
            settings.StrikeInterval = new Vector2(8f, 25f);
            settings.ThunderDelay = new Vector2(0.3f, 2.5f);
            settings.ThunderVolume = 1f;
            settings.Thunder = Sound("Thunder");
            settings.Skies = new Dictionary<SkyWeather, WeatherSettings.Sky>
            {
                [SkyWeather.Clear] = Sky(WindLevel.Calm, 1f, 1f),
                [SkyWeather.PartlyCloudy] = Sky(WindLevel.Breezy, 1f, 1f),
                [SkyWeather.Cloudy] = Sky(WindLevel.Breezy, 1.1f, 1f),
                [SkyWeather.Overcast] = Sky(WindLevel.Breezy, 1.3f, 0.8f),
                [SkyWeather.LightRain] = Sky(WindLevel.Breezy, 1.5f, 0.5f, "RainLight", 0.7f),
                [SkyWeather.MediumRain] = Sky(WindLevel.Windy, 2f, 0.3f, "RainMedium", 0.8f),
                [SkyWeather.HeavyRain] = Sky(WindLevel.Gale, 2.5f, 0.1f, "RainHeavy", 1f, true),
                [SkyWeather.Fog] = Sky(WindLevel.Calm, 4f, 0.7f),
            };
            settings.Winds = new Dictionary<WindLevel, WeatherSettings.Wind>
            {
                [WindLevel.Calm] = Wind(0.4f, 0.8f, 0.3f),
                [WindLevel.Breezy] = Wind(1f, 1f, 1f),
                [WindLevel.Windy] = Wind(1.8f, 1.3f, 1.8f, "WindStrong", 0.45f),
                [WindLevel.Gale] = Wind(2.8f, 1.6f, 3f, "WindStrong", 0.9f),
            };
        }
        // The COZY profiles always point at the copies.
        foreach (var (sky, _, _, _) in Profiles_)
            if (settings.Skies.TryGetValue(sky, out WeatherSettings.Sky entry))
                entry.Cozy = AssetDatabase.LoadAssetAtPath<WeatherProfile>(ProfilePath(sky)) ?? throw new Exception("missing profile copy for " + sky);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);

        var flash = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LightningProfilePath);
        if (flash == null)
        {
            flash = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(flash, LightningProfilePath);
            var adjustments = flash.Add<ColorAdjustments>();
            adjustments.name = nameof(ColorAdjustments);
            AssetDatabase.AddObjectToAsset(adjustments, flash);
            adjustments.postExposure.Override(1.6f);
            EditorUtility.SetDirty(adjustments);
            EditorUtility.SetDirty(flash);
            AssetDatabase.SaveAssetIfDirty(flash);
        }
        return $"settings {(created ? "created" : reset ? "reset" : "kept")} at {SettingsPath}; lightning profile {LightningProfilePath}";
    }

    static WeatherSettings.Sky Sky(WindLevel wind, float fog, float nature, string bed = null, float bedVolume = 1f, bool lightning = false) => new()
    {
        DefaultWind = wind,
        FogDensityScale = fog,
        NatureVolume = nature,
        Bed = bed != null ? Sound(bed) : default,
        BedVolume = bedVolume,
        Lightning = lightning,
    };

    static WeatherSettings.Wind Wind(float strength, float speed, float gust, string bed = null, float bedVolume = 1f) => new()
    {
        Strength = strength,
        Speed = speed,
        Gust = gust,
        Bed = bed != null ? Sound(bed) : default,
        BedVolume = bedVolume,
    };

    public static string Scene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "WorldMap")
            return "open WorldMap first (active: " + scene.name + ")";
        var settings = AssetDatabase.LoadAssetAtPath<WeatherSettings>(SettingsPath) ?? throw new Exception("run Settings first");
        GameObject windRoot = GameObject.Find("Environment/Wind") ?? throw new Exception("no Environment/Wind");
        var bk = windRoot.GetComponentInChildren<BKFoliageGlobals>(true);

        // The scene wind, at the Raygeas drivers' yaw, with their tuned values; then the drivers go off.
        Transform windTransform = windRoot.transform.Find("Weather Wind");
        GameObject windObject = windTransform != null ? windTransform.gameObject : new GameObject("Weather Wind");
        windObject.transform.SetParent(windRoot.transform, false);
        windObject.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        var zone = Get<WindZone>(windObject);
        zone.mode = WindZoneMode.Directional;
        zone.windMain = 1f;
        zone.windTurbulence = 1f;
        var grassWind = Get<sc.stylizedgrass.runtime.WindController>(windObject);
        grassWind.windZone = zone;
        var wind = Get<WeatherWind>(windObject);
        var windSo = new SerializedObject(wind);
        windSo.FindProperty("_bk").objectReferenceValue = bk;
        windSo.FindProperty("_zone").objectReferenceValue = zone;
        var drivers = new List<string>();
        foreach (MonoBehaviour driver in windRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string type = driver.GetType().FullName;
            if (type != "Raygeas.WindController" && type != "Highlands.WindSettings" && type != "Raygeas.WindSettings")
                continue;
            var driverSo = new SerializedObject(driver);
            void Copy(string from, string to)
            {
                SerializedProperty source = driverSo.FindProperty(from);
                if (source != null)
                    windSo.FindProperty(to).floatValue = source.floatValue;
            }
            if (type == "Raygeas.WindController")
            {
                Copy("treeWindForce", "_treeForce");
                Copy("treeWindSpeed", "_treeSpeed");
                Copy("treeWindWavesScale", "_treeWavesScale");
                Copy("treeWindFlowDensity", "_treeFlowDensity");
                Copy("grassWindForce", "_grassForce");
                Copy("grassWindSpeed", "_grassSpeed");
            }
            else
            {
                Copy("force", "_globalForce");
                Copy("speed", "_globalSpeed");
                Copy("wavesScale", "_globalWavesScale");
                Copy("flowDensity", "_globalFlowDensity");
            }
            if (driver.enabled)
            {
                driver.enabled = false;
                EditorUtility.SetDirty(driver);
            }
            drivers.Add($"{driver.name} ({type}) off");
        }
        windSo.ApplyModifiedPropertiesWithoutUndo();

        // The lightning flash: its own global volume above the grades.
        GameObject environment = GameObject.Find("Environment") ?? throw new Exception("no Environment");
        Transform flashTransform = environment.transform.Find("Weather/Lightning Flash");
        GameObject weatherRoot = environment.transform.Find("Weather")?.gameObject ?? new GameObject("Weather");
        weatherRoot.transform.SetParent(environment.transform, false);
        GameObject flashObject = flashTransform != null ? flashTransform.gameObject : new GameObject("Lightning Flash");
        flashObject.transform.SetParent(weatherRoot.transform, false);
        var volume = Get<Volume>(flashObject);
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LightningProfilePath);
        volume.enabled = false;
        var flash = Get<LightningFlash>(flashObject);

        // The installer on the scope.
        var installer = Object.FindObjectsByType<EnvironmentInstaller>(FindObjectsInactive.Include).FirstOrDefault()
                        ?? throw new Exception("no EnvironmentInstaller");
        var installerSo = new SerializedObject(installer);
        installerSo.FindProperty("_weather").objectReferenceValue = settings;
        installerSo.FindProperty("_wind").objectReferenceValue = wind;
        installerSo.FindProperty("_lightning").objectReferenceValue = flash;
        installerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(windObject);
        EditorUtility.SetDirty(flashObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"scene: {windObject.name} under Environment/Wind (bk {(bk != null)}), {string.Join(", ", drivers)}; Lightning Flash; installer wired; saved";
    }

    static T Get<T>(GameObject target) where T : Component => target.TryGetComponent(out T component) ? component : target.AddComponent<T>();

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path[..slash]);
        AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
    }
}
