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
//   All()      = Profiles + Audio + Settings + Rain + Scene
//   Profiles() = our COZY weather profile copies (no COZY audio / thunder / rain / inert FX)
//   Audio()    = the weather sounds in the Ambience library
//   Settings() = Data/Environment/Weather/Weather.asset (+ the lightning volume profile); keeps tuned values unless reset
//   Rain()     = Environment/Weather/WeatherRain.prefab: Unluck's Rain Multi + Rain Warpy + fake ground splashes, no collision
//   Scene()    = WorldMap wiring: Weather Wind (replaces the Raygeas wind drivers), Rain, Lightning Flash, installer fields
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
    const string UnluckDir = "Assets/_StoreAssets/VFX/Unluck Software/Stylized Weather/Prefabs/Particles Only/";
    const string RainPrefabDir = "Assets/_Game/Environment/Weather";
    const string RainPrefabPath = RainPrefabDir + "/WeatherRain.prefab";

    // FX COZY plays itself that we replace (audio, thunder) or that do nothing without their modules (precipitation and
    // climate need the Climate module, wind the Wind module, events have no listener).
    static readonly string[] StrippedFX = { "AudioFX", "ThunderFX", "PrecipitationFX", "ClimateFX", "WindFX", "EventFX" };
    // COZY's VFX Graph rain: ~18k drops (about 2 ms in the editor) that need its FX-blocking camera; WeatherRain draws the rain.
    static readonly string[] StrippedRain = { "Stylized Drizzle", "Stylized Light Rain", "Stylized Heavy Rain" };

    // Sky weather → the COZY profile copied.
    static readonly (SkyWeather sky, string source)[] Profiles_ =
    {
        (SkyWeather.Clear, "Clear"),
        (SkyWeather.PartlyCloudy, "Partly Cloudy"),
        (SkyWeather.Cloudy, "Mostly Cloudy"),
        (SkyWeather.Overcast, "Overcast"),
        (SkyWeather.LightRain, "Light Rain"),
        (SkyWeather.MediumRain, "Light Rain"),
        (SkyWeather.HeavyRain, "Thunder Storm"),
        (SkyWeather.Fog, "Dense Fog"),
    };

    public static string All(bool resetSettings = false) =>
        string.Join("\n", Profiles(), Audio(), Settings(resetSettings), Rain(), Scene());

    static string ProfilePath(SkyWeather sky) => $"{ProfileDir}/Weather_{sky}.asset";

    public static string Profiles()
    {
        EnsureFolder(ProfileDir);
        var log = new List<string>();
        foreach (var (sky, source) in Profiles_)
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
                if (StrippedFX.Contains(item.GetType().Name) || StrippedRain.Contains(item.name))
                {
                    removed.Add(item.name);
                    continue;
                }
                kept.Add(item);
            }
            fx.arraySize = kept.Count;
            for (int i = 0; i < kept.Count; i++)
                fx.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
            // Our weather never forecasts: drop the links to COZY's other profiles.
            so.FindProperty("forecastNext").arraySize = 0;
            so.FindProperty("description").stringValue = $"aipocalypse {sky}: copy of COZY's {source} without its rain, audio and thunder";
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
            settings.RainSlant = 0.6f;
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
                [SkyWeather.LightRain] = Sky(WindLevel.Breezy, 1.5f, 0.5f, "RainLight", 0.7f, rain: 0.2f),
                [SkyWeather.MediumRain] = Sky(WindLevel.Windy, 2f, 0.3f, "RainMedium", 0.8f, rain: 0.5f),
                [SkyWeather.HeavyRain] = Sky(WindLevel.Gale, 2.5f, 0.1f, "RainHeavy", 1f, true, 1f),
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
        // Rain amounts came after the first settings: fill them once while none is set.
        if (!settings.Skies.Values.Any(s => s.RainAmount > 0f))
        {
            settings.RainSlant = 0.6f;
            foreach (var (sky, amount) in new[] { (SkyWeather.LightRain, 0.2f), (SkyWeather.MediumRain, 0.5f), (SkyWeather.HeavyRain, 1f) })
                if (settings.Skies.TryGetValue(sky, out WeatherSettings.Sky rainy))
                    rainy.RainAmount = amount;
        }
        // The COZY profiles always point at the copies.
        foreach (var (sky, _) in Profiles_)
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

    static WeatherSettings.Sky Sky(WindLevel wind, float fog, float nature, string bed = null, float bedVolume = 1f, bool lightning = false,
        float rain = 0f) => new()
    {
        RainAmount = rain,
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

    // The rain prefab, built from Unluck Stylized Weather particles (vendor prefabs untouched): Rain Multi (multi-streak
    // sheets, the main rain), Rain Warpy (heavier rain only) and a fake-splash emitter made from Rain -C's splash sprite.
    // Nothing collides: the splashes are emitted on the ground around the camera. WeatherRain on the root drives them.
    public static string Rain()
    {
        EnsureFolder(RainPrefabDir);
        var root = new GameObject("WeatherRain");
        ParticleSystem multi = Copy(UnluckDir + "Rain Multi.prefab", root.transform);
        // Its sheets sink only ~11.4 m in their 2.5 s (the box sends them off at an angle, ~4.6 m/s down): spawned at the pack's
        // 17.9 m they never came below ~6.5 m over the camera (rain seen only looking up). At 6 m they span eye level.
        multi.transform.localPosition = new Vector3(0f, 6f, 0f);
        // Its material blends additively: the pack's dark teal (0.27, 0.38, 0.37) adds next to nothing over trees and grass,
        // so the rain showed only against the sky. A light grey like Rain Warpy's reads against both.
        ParticleSystem.MainModule multiMain = multi.main;
        multiMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.58f, 0.62f, 0.6f), new Color(0.72f, 0.75f, 0.8f, 0.6f));
        ParticleSystem warpy = Copy(UnluckDir + "Rain Warpy.prefab", root.transform);

        // Splashes: Rain -C's billboard splash (a sub-emitter there), turned into its own emitter on a flat disc.
        ParticleSystem rainC = Copy(UnluckDir + "Rain -C.prefab", root.transform);
        ParticleSystem splash = rainC.GetComponentsInChildren<ParticleSystem>(true)
            .First(ps => ps != rainC && ps.GetComponent<ParticleSystemRenderer>().renderMode == ParticleSystemRenderMode.Billboard);
        splash.transform.SetParent(root.transform, false);
        splash.name = "Splashes";
        Object.DestroyImmediate(rainC.gameObject);
        splash.transform.localPosition = Vector3.zero;
        splash.transform.localRotation = Quaternion.identity;
        ParticleSystem.MainModule splashMain = splash.main;
        splashMain.loop = true;
        splashMain.playOnAwake = false;
        splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
        splashMain.maxParticles = 300;
        splashMain.gravityModifier = 0f;
        splashMain.startSpeed = 0f;
        ParticleSystem.EmissionModule splashEmission = splash.emission;
        splashEmission.rateOverTime = 0f;
        splashEmission.SetBursts(Array.Empty<ParticleSystem.Burst>());
        ParticleSystem.ShapeModule disc = splash.shape;
        disc.enabled = true;
        disc.shapeType = ParticleSystemShapeType.Circle;
        disc.radius = 10f;
        disc.radiusThickness = 1f;
        disc.rotation = new Vector3(90f, 0f, 0f);
        disc.position = Vector3.zero;

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = false;
            ParticleSystem.SubEmittersModule sub = ps.subEmitters;
            sub.enabled = false;
        }

        var rain = root.AddComponent<WeatherRain>();
        var so = new SerializedObject(rain);
        SerializedProperty layers = so.FindProperty("_layers");
        var config = new (ParticleSystem system, float maxRate, float from, bool onGround)[]
        {
            (multi, 130f, 0f, false),
            (warpy, 80f, 0.5f, false),
            (splash, 120f, 0f, true),
        };
        layers.arraySize = config.Length;
        for (int i = 0; i < config.Length; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            layer.FindPropertyRelative("System").objectReferenceValue = config[i].system;
            layer.FindPropertyRelative("MaxRate").floatValue = config[i].maxRate;
            layer.FindPropertyRelative("From").floatValue = config[i].from;
            layer.FindPropertyRelative("OnGround").boolValue = config[i].onGround;
        }
        so.FindProperty("_ground").intValue = LayerMask.GetMask("Default", "Water");
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, RainPrefabPath);
        Object.DestroyImmediate(root);
        return "rain prefab " + RainPrefabPath + ": Rain Multi 130/s, Rain Warpy 80/s from amount 0.5, Splashes 120/s within 10 m";
    }

    // An unpacked copy of a vendor particle prefab under parent, without its scripts (the pack's wrapper moves particles
    // around a follow object; WeatherRain moves the rig instead). Keeps the prefab's own offset.
    static ParticleSystem Copy(string path, Transform parent)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception("missing " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (MonoBehaviour script in instance.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(script);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = source.transform.localPosition;
        return instance.GetComponent<ParticleSystem>();
    }

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

        // The rain: an instance of the rain prefab (WeatherRain on its root) as Environment/Weather/Rain.
        Transform oldRain = weatherRoot.transform.Find("Rain");
        WeatherRain rain = oldRain != null ? oldRain.GetComponent<WeatherRain>() : null;
        if (oldRain != null && (rain == null || !PrefabUtility.IsAnyPrefabInstanceRoot(oldRain.gameObject)))
        {
            Object.DestroyImmediate(oldRain.gameObject);
            rain = null;
        }
        if (rain == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RainPrefabPath) ?? throw new Exception("run Rain first");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, weatherRoot.transform);
            instance.name = "Rain";
            rain = instance.GetComponent<WeatherRain>();
        }

        // The installer on the scope.
        var installer = Object.FindObjectsByType<EnvironmentInstaller>(FindObjectsInactive.Include).FirstOrDefault()
                        ?? throw new Exception("no EnvironmentInstaller");
        var installerSo = new SerializedObject(installer);
        installerSo.FindProperty("_weather").objectReferenceValue = settings;
        installerSo.FindProperty("_wind").objectReferenceValue = wind;
        installerSo.FindProperty("_lightning").objectReferenceValue = flash;
        installerSo.FindProperty("_rain").objectReferenceValue = rain;
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
