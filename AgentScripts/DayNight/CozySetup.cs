using System;
using System.Collections.Generic;
using System.Linq;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.WorldTimeSystem;
using DistantLands.Cozy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// COZY 4 on the world map: COZY draws the sky, clouds and sun / moon lights at the director's look hour (DAYNIGHT.md → COZY).
// Atmosphere() bakes our time-of-day looks into a COZY atmosphere profile; Scene() builds and wires COZY in the open scene;
// Use(bool) switches between COZY and the director's own light and sky; Fog(mode) picks the fog. Saves only what it changes.
public static class CozySetup
{
    const string DataDir = "Assets/_Game/Data/Environment/Cozy";
    const string ProfilePath = DataDir + "/Aipocalypse_Atmosphere.asset";
    const string BaseProfilePath = "Packages/space.distantlands.cozyweather.core/Content/Profiles/Atmosphere Profiles/Balanced.asset";
    const string StartingWeatherPath = "Packages/space.distantlands.cozyweather.core/Content/Profiles/Weather Profiles/Partly Cloudy.asset";
    const string TimeOfDayPath = "Assets/_Game/Data/Environment/TimeOfDay/TimeOfDay.asset";
    const string ClockSettingsPath = "Assets/_Game/Data/WorldTime/ClockSettings.asset";
    const string CozyName = "COZY Weather";
    const string FogDir = "Assets/_Game/Data/Environment/Fog";
    const string StylePath = DataDir + "/Aipocalypse_Style.asset";
    const string RendererPath = "Assets/_Game/Settings/PC_Renderer.asset";
    const string Vf2PresetPath = "Assets/_StoreAssets/Rendering/Kronnect/VolumetricFog2/Demo/Presets/Fog.asset";
    const string DynamicFogPresetPath = "Assets/_StoreAssets/Rendering/Kronnect/DynamicFogURP/Demo/Fog Presets/FogLight.asset";
    // Samples per day for the baked keys (every 30 game minutes).
    const int Samples = 48;
    // A look's Stars value → COZY star intensity.
    const float StarScale = 0.6f;
    // A look's Stars value → constellation line and galaxy glow intensity.
    const float ConstellationScale = 0.12f;
    const float GalaxyScale = 1f;
    // Turns COZY's star dome (stars, constellations, galaxy) so the Milky Way arches overhead around midnight; it turns daily
    // only, so every night looks the same (with yearly turning, our day count would drift it).
    const float StarDomeOffset = 0.575f;
    static readonly Color ConstellationColor = new(0.6f, 0.75f, 1f);

    // Bakes the looks into COZY overrides: sun and moon colour / intensity (with our horizon fade at COZY's sun height) /
    // shadow strength, the trilight ambient, sky horizon and fog colours from the look's fog, the night sky (NightSky), and a
    // moon that opposes the sun on our moon cycle. Everything else comes from the base profile (Balanced).
    public static object Atmosphere()
    {
        var tod = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(TimeOfDayPath);
        if (tod == null)
            return "no " + TimeOfDayPath;
        if (!AssetDatabase.IsValidFolder(DataDir))
            AssetDatabase.CreateFolder("Assets/_Game/Data/Environment", "Cozy");
        var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(ProfilePath);
        if (profile == null)
        {
            if (!AssetDatabase.CopyAsset(BaseProfilePath, ProfilePath))
                return "copy failed: " + BaseProfilePath;
            profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(ProfilePath);
            profile.Locked = false;
        }

        // Keys sit on COZY's clock; each samples our look at the matching hour of our day (CozyBridge.CozyHour).
        SunPath path = tod.Path;
        var hours = Enumerable.Range(0, Samples).Select(i => i * 24f / Samples).ToArray();
        var states = hours.Select(h => tod.Evaluate(CozyBridge.OurHour(h, path.Sunrise, path.Sunset), out AtmosphereState s, out _) ? s : default).ToArray();
        float pitch = CozyPreferences.SkyPitch;
        float rotation = CozyPreferences.SkyRotation;
        var sunHeight = hours.Select(h => Elevation(SunToward(h, pitch, rotation))).ToArray();

        AtmosphereProperty<ColorValue> Colors(Func<AtmosphereState, Color> pick) =>
            new(hours.Select((h, i) => Key<ColorValue>(pick(states[i]), h)).ToArray());
        AtmosphereProperty<FloatValue> Floats(Func<int, float> pick) =>
            new(hours.Select((h, i) => Key<FloatValue>(pick(i), h)).ToArray());
        float Fade(float elevation) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elevation / path.HorizonFade));

        var overrides = profile.overrides ??= new List<AtmosphereOverride>();
        overrides.RemoveAll(o => o is LightingSunOverride or LightingMoonOverride or LightingAmbientOverride or SkyGradientOverride
            or FogGradientOverride or SkyMoonDiskOverride);

        overrides.Add(new LightingSunOverride
        {
            lightColor = Colors(s => s.SunColor),
            lightIntensity = Floats(i => states[i].SunIntensity * Fade(sunHeight[i])),
            shadowIntensity = Floats(i => states[i].SunShadows),
            shadowType = Constant(new EnumValue<LightShadows>(LightShadows.Soft)),
        });
        // The moon opposes the sun; the bridge scales it by the moon phase at runtime.
        overrides.Add(new LightingMoonOverride
        {
            lightColor = Colors(s => s.MoonColor),
            lightIntensity = Floats(i => states[i].MoonIntensity * Fade(-sunHeight[i])),
            shadowIntensity = Floats(i => states[i].MoonShadows),
            shadowType = Constant(new EnumValue<LightShadows>(LightShadows.Soft)),
        });
        overrides.Add(new LightingAmbientOverride
        {
            ambientMode = Constant(new EnumValue<AmbientLightingType>(AmbientLightingType.Gradient)),
            zenithColor = Colors(s => s.AmbientSky),
            horizonColor = Colors(s => s.AmbientEquator),
            groundColor = Colors(s => s.AmbientGround),
            intensity = Constant<FloatValue>(1f),
        });
        // Sky: COZY's own zenith by day (evaluated from the base profile), darkening to our night sky by the look's Night Sky
        // Blend; the horizon is the look's fog colour (so Unity fog and COZY fog meet it), easing toward our night horizon.
        var model = new AtmosphereModel();
        model.UpdateModel(AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(BaseProfilePath));
        var cozyZenith = hours.Select(h =>
        {
            var parameters = new AtmosphereParameters();
            model.ApplyToParameters(parameters, h / 24f);
            return (Color)parameters.skyGradientZenithColor;
        }).ToArray();
        overrides.Add(new SkyGradientOverride
        {
            zenithColor = new(hours.Select((h, i) => Key<ColorValue>(Color.Lerp(cozyZenith[i], states[i].NightSkyTop, states[i].NightSkyBlend), h)).ToArray()),
            horizonColor = Colors(s => Color.Lerp(s.FogColor, s.NightSkyHorizon, s.NightSkyBlend * 0.5f)),
        });
        var fog = Colors(s => s.FogColor);
        overrides.Add(new FogGradientOverride { color1 = fog, color2 = fog, color3 = fog, color4 = fog, color5 = fog });

        AddNightSky(overrides, Floats, i => states[i].Stars);

        var clock = AssetDatabase.LoadAssetAtPath<ClockSettings>(ClockSettingsPath);
        overrides.Add(new SkyMoonDiskOverride
        {
            placementMode = Constant(new EnumValue<MoonPlacementMode>(MoonPlacementMode.AlwaysOpposeSun)),
            phaseMode = Constant(new EnumValue<MoonPhaseMode>(MoonPhaseMode.Linear)),
            orbitPeriod = Constant<FloatValue>(clock != null ? clock.MoonCycleDays : 8f),
        });

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        foreach (var module in Object.FindObjectsByType<CozyAtmosphereModule>(FindObjectsInactive.Include))
            module.UpdateModel();
        return $"baked {Samples} keys into {ProfilePath} (pitch {pitch}, rotation {rotation})";
    }

    // Re-bakes only the night sky (stars, constellations, galaxy) into the profile; the other overrides stay as they are.
    public static object NightSky()
    {
        var tod = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(TimeOfDayPath);
        var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(ProfilePath);
        if (tod == null || profile == null)
            return "missing " + (tod == null ? TimeOfDayPath : ProfilePath);
        SunPath path = tod.Path;
        var hours = Enumerable.Range(0, Samples).Select(i => i * 24f / Samples).ToArray();
        var stars = hours.Select(h => tod.Evaluate(CozyBridge.OurHour(h, path.Sunrise, path.Sunset), out AtmosphereState s, out _) ? s.Stars : 0f).ToArray();
        AddNightSky(profile.overrides ??= new List<AtmosphereOverride>(),
            pick => new(hours.Select((h, i) => Key<FloatValue>(pick(i), h)).ToArray()), i => stars[i]);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        foreach (var module in Object.FindObjectsByType<CozyAtmosphereModule>(FindObjectsInactive.Include))
            module.UpdateModel();
        return $"night sky baked into {ProfilePath} (midnight stars {stars[0]:0.##})";
    }

    // COZY's own stars (its constellation and galaxy cubemaps are aligned to them; our star map is mirrored against them) as
    // bright as the looks' Stars ask (tuned for the old sky, ~2.5 at midnight), with constellation lines (shimmering one at a
    // time) and the galaxy fading with them. Replaces those three overrides.
    static void AddNightSky(List<AtmosphereOverride> overrides, Func<Func<int, float>, AtmosphereProperty<FloatValue>> floats,
        Func<int, float> stars)
    {
        overrides.RemoveAll(o => o is SkyStarsOverride or SkyConstellationsOverride or SkyGalaxyOverride);
        overrides.Add(new SkyStarsOverride
        {
            intensity = floats(i => stars(i) * StarScale),
            twinkleAmount = Constant<FloatValue>(0.4f),
            mode = Constant(new EnumValue<StarDomePlacementMode>(StarDomePlacementMode.RotateDaily)),
            rotationOffset = Constant<FloatValue>(StarDomeOffset),
        });
        overrides.Add(new SkyConstellationsOverride
        {
            color = Constant(new ColorValue(ConstellationColor)),
            intensity = floats(i => stars(i) * ConstellationScale),
            shimmerAmount = Constant<FloatValue>(0.5f),
            shimmerSpeed = Constant<FloatValue>(1f),
        });
        overrides.Add(new SkyGalaxyOverride
        {
            primaryColor = Constant(new ColorValue(new Color(0.6f, 0.45f, 1f))),
            secondaryColor = Constant(new ColorValue(new Color(0.35f, 0.8f, 1f))),
            tertiaryColor = Constant(new ColorValue(new Color(0.9f, 0.4f, 0.8f))),
            intensity = floats(i => stars(i) * GalaxyScale),
        });
    }

    // Builds COZY in the open scene (once) and wires it: Atmosphere + Time + Weather modules, sun and moon lights copied from
    // the director's light (shadows, cookie, cloud mover), our profile, COZY time paused, scripted weather starting Partly Cloudy,
    // the bridge on the director. Fits COZY's sun path to ours, turns FX blocking off and the renderer feature on. Saves the scene.
    public static object Scene()
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>(FindObjectsInactive.Include);
        if (director == null)
            return "no EnvironmentDirector in the open scene";
        var directorSo = new SerializedObject(director);
        var oldLight = directorSo.FindProperty("_light").objectReferenceValue as Light;
        if (oldLight == null)
            return "the director has no light to copy";

        string fit = FitSunPath();
        object baked = Atmosphere();
        var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(ProfilePath);

        var cozy = Object.FindAnyObjectByType<CozyWeather>(FindObjectsInactive.Include);
        if (cozy == null)
        {
            var root = new GameObject(CozyName);
            Undo.RegisterCreatedObjectUndo(root, "Create COZY");
            root.transform.SetParent(director.transform, false);
            cozy = root.AddComponent<CozyWeather>();
            CopyLight(oldLight, AddChild(root, "Sun Light").AddComponent<Light>(), true);
            CopyLight(oldLight, AddChild(root, "Moon Light").AddComponent<Light>(), false);
            AddFlare(root, "Sun Flare");
            AddFlare(root, "Moon Flare");
            AddChild(root, "Wind Zone").AddComponent<WindZone>();
            cozy.AddModule<CozyAtmosphereModule>();
            cozy.AddModule<CozyTimeModule>();
            cozy.AddModule<CozyWeatherModule>();
        }

        var atmosphere = (CozyAtmosphereModule)cozy.Atmosphere;
        atmosphere.AtmosphereProfile = profile;
        EditorUtility.SetDirty(atmosphere);
        var time = (CozyTimeModule)cozy.Time;
        time.PauseTime = true;
        time.CurrentTime = 9f / 24f;
        EditorUtility.SetDirty(time);
        var weather = (CozyWeatherModule)cozy.Weather;
        var weatherSo = new SerializedObject(weather);
        weatherSo.FindProperty("weatherChangeMode").enumValueIndex = (int)WeatherChangeMode.ChangeViaScripting;
        weatherSo.FindProperty("weatherInitializationBehavior").enumValueIndex = (int)WeatherInitializationBehavior.SetWeather;
        weatherSo.FindProperty("startingWeather").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeatherProfile>(StartingWeatherPath);
        weatherSo.ApplyModifiedPropertiesWithoutUndo();

        var bridge = cozy.GetComponent<CozyBridge>() ?? cozy.gameObject.AddComponent<CozyBridge>();
        var bridgeSo = new SerializedObject(bridge);
        bridgeSo.FindProperty("_cozy").objectReferenceValue = cozy;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();

        string use = Use(true);
        var prefs = new SerializedObject(CozyPreferences.Instance);
        prefs.FindProperty("renderFXBlocking").boolValue = false;
        prefs.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(CozyPreferences.Instance);

        var scene = director.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"COZY wired in {scene.name}: {fit}; {baked}; {use}";
    }

    // Our COZY style (Data/Environment/Cozy/Aipocalypse_Style.asset): the vendor Desktop style without screen-space sunshafts
    // (0.5 ms GPU; the scene has particle god rays), first in COZY's style list because COZY doesn't save which style is
    // selected and always starts on the first. Fog at half resolution. Measured in DAYNIGHT.md → COZY → Cost.
    public static object Style()
    {
        var prefs = new SerializedObject(CozyPreferences.Instance);
        var styles = prefs.FindProperty("styles");
        CozyStyle desktop = null;
        for (int i = 0; i < styles.arraySize; i++)
            if (styles.GetArrayElementAtIndex(i).objectReferenceValue is CozyStyle s && s.name == "Desktop")
                desktop = s;
        if (desktop == null)
            return "no Desktop style in COZY Settings";

        var ours = AssetDatabase.LoadAssetAtPath<CozyStyle>(StylePath);
        if (ours == null)
        {
            ours = ScriptableObject.CreateInstance<CozyStyle>();
            EditorUtility.CopySerialized(desktop, ours);
            ours.name = "Aipocalypse";
            AssetDatabase.CreateAsset(ours, StylePath);
        }
        var style = new SerializedObject(ours);
        style.FindProperty("useSunshafts").boolValue = false;
        style.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(ours);

        var list = Enumerable.Range(0, styles.arraySize).Select(i => styles.GetArrayElementAtIndex(i).objectReferenceValue as CozyStyle)
            .Where(s => s != null && s != ours).Prepend(ours).ToArray();
        styles.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++)
            styles.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        prefs.FindProperty("fogDownsampling").enumValueIndex = 1;
        prefs.ApplyModifiedPropertiesWithoutUndo();
        CozyPreferences.SelectStyle(0);
        AssetDatabase.SaveAssetIfDirty(CozyPreferences.Instance);
        Object.FindAnyObjectByType<CozyWeather>()?.ResetQuality();
        return $"styles {string.Join(", ", list.Select(s => s.name))}; current {CozyPreferences.CurrentStyle.name}, fog {CozyPreferences.FogDownsampling}";
    }

    // true: COZY draws (bridge on the director, COZY active, the director's light off, renderer feature on).
    // false: the director's own light and sky again (COZY inactive, feature off). Saves the renderer, not the scene.
    public static string Use(bool cozy)
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>(FindObjectsInactive.Include);
        var weather = Object.FindAnyObjectByType<CozyWeather>(FindObjectsInactive.Include);
        if (director == null || weather == null)
            return "no director or COZY in the open scene";
        var so = new SerializedObject(director);
        so.FindProperty("_cozy").objectReferenceValue = cozy ? weather.GetComponent<CozyBridge>() : null;
        var oldLight = so.FindProperty("_light").objectReferenceValue as Light;
        so.ApplyModifiedPropertiesWithoutUndo();
        weather.gameObject.SetActive(cozy);
        if (oldLight != null)
        {
            oldLight.enabled = !cozy;
            if (!cozy)
                RenderSettings.sun = oldLight;
        }
        string feature = SetFeature(cozy);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        return $"cozy {cozy}, {feature}";
    }

    // The two Kronnect fog modes for comparison: a Volumetric Fog 2 volume and a Dynamic Fog volume over the whole map
    // (vendor presets copied to Data/Environment/Fog, tinted with the day fog colour), both inactive until the director's
    // fog mode picks them, plus VF2's renderer feature on PC_Renderer. Their colours don't follow the time of day.
    public static object FogModes()
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>(FindObjectsInactive.Include);
        if (director == null)
            return "no EnvironmentDirector";
        if (!AssetDatabase.IsValidFolder(FogDir))
            AssetDatabase.CreateFolder("Assets/_Game/Data/Environment", "Fog");
        var parent = director.transform.Find("Fog Modes") ?? AddChild(director.gameObject, "Fog Modes").transform;
        Color dayFog = RenderSettings.fogColor;

        var vfProfile = CopyAsset<VolumetricFogAndMist2.VolumetricFogProfile>(Vf2PresetPath, FogDir + "/VF2_Fog.asset");
        Tint(vfProfile, "albedo", dayFog);
        var vf = parent.Find("Volumetric Fog 2")?.gameObject;
        if (vf == null)
        {
            vf = VolumetricFogAndMist2.VolumetricFogManager.CreateFogVolume("Volumetric Fog 2");
            vf.transform.SetParent(parent, false);
        }
        vf.GetComponent<VolumetricFogAndMist2.VolumetricFog>().profile = vfProfile;
        FitToMap(vf.transform);
        vf.SetActive(false);
        var vfManager = VolumetricFogAndMist2.VolumetricFogManager.instance;
        if (vfManager != null)
            vfManager.transform.SetParent(parent, false);

        var dfProfile = CopyAsset<DynamicFogAndMist2.DynamicFogProfile>(DynamicFogPresetPath, FogDir + "/DynamicFog_Light.asset");
        Tint(dfProfile, "color", dayFog);
        var df = parent.Find("Dynamic Fog")?.gameObject;
        if (df == null)
        {
            df = DynamicFogAndMist2.DynamicFogManager.CreateFogVolume("Dynamic Fog");
            df.transform.SetParent(parent, false);
        }
        df.GetComponent<DynamicFogAndMist2.DynamicFog>().profile = dfProfile;
        FitToMap(df.transform);
        df.SetActive(false);
        var dfManager = DynamicFogAndMist2.DynamicFogManager.instance;
        if (dfManager != null)
            dfManager.transform.SetParent(parent, false);

        var so = new SerializedObject(director);
        so.FindProperty("_volumetricFog").objectReferenceValue = vf;
        so.FindProperty("_dynamicFog").objectReferenceValue = df;
        so.ApplyModifiedPropertiesWithoutUndo();
        string feature = AddRendererFeature<VolumetricFogAndMist2.VolumetricFogRenderFeature>(RendererPath);

        var scene = director.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"fog modes ready: VF2 {AssetDatabase.GetAssetPath(vfProfile)}, Dynamic Fog {AssetDatabase.GetAssetPath(dfProfile)}; {feature}";
    }

    static void FitToMap(Transform volume)
    {
        // The world map: 1000 × 300 × 1000 m at the origin (ENVIRONMENT.md).
        volume.position = new Vector3(500f, 120f, 500f);
        volume.localScale = new Vector3(1600f, 300f, 1600f);
    }

    static T CopyAsset<T>(string source, string target) where T : Object
    {
        if (AssetDatabase.LoadAssetAtPath<T>(target) == null)
            AssetDatabase.CopyAsset(source, target);
        return AssetDatabase.LoadAssetAtPath<T>(target);
    }

    static void Tint(Object asset, string property, Color color)
    {
        var so = new SerializedObject(asset);
        var p = so.FindProperty(property);
        if (p == null || p.propertyType != SerializedPropertyType.Color)
            return;
        p.colorValue = new Color(color.r, color.g, color.b, p.colorValue.a);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    // Adds a renderer feature the way URP's renderer editor does (feature list + local id map); saves the renderer.
    static string AddRendererFeature<T>(string rendererPath) where T : ScriptableRendererFeature
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (data.rendererFeatures.Any(f => f is T))
            return typeof(T).Name + " already on " + data.name;
        var feature = ScriptableObject.CreateInstance<T>();
        feature.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(feature, data);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
        var so = new SerializedObject(data);
        var features = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        return typeof(T).Name + " added to " + data.name;
    }

    // Unity / Cozy / VolumetricFog2 / DynamicFog on the director (live in Play mode; saved in edit mode).
    public static string Fog(string mode)
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>(FindObjectsInactive.Include);
        if (director == null || !Enum.TryParse(mode, true, out EnvironmentFog fog))
            return $"no director, or unknown mode '{mode}' ({string.Join(", ", Enum.GetNames(typeof(EnvironmentFog)))})";
        var so = new SerializedObject(director);
        so.FindProperty("_fog").enumValueIndex = (int)fog;
        so.ApplyModifiedProperties();
        EnvironmentDirector.MarkLooksEdited();
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        return $"fog {fog}; Unity fog {RenderSettings.fog}";
    }

    public static string Status()
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>(FindObjectsInactive.Include);
        var cozy = Object.FindAnyObjectByType<CozyWeather>(FindObjectsInactive.Include);
        if (director == null)
            return "no director";
        var so = new SerializedObject(director);
        var atmosphere = cozy != null ? cozy.Atmosphere as CozyAtmosphereModule : null;
        return $"cozy {(so.FindProperty("_cozy").objectReferenceValue != null)} active {(cozy != null && cozy.gameObject.activeInHierarchy)} " +
               $"fog {(EnvironmentFog)so.FindProperty("_fog").enumValueIndex} unityFog {RenderSettings.fog} | look {director.LookHour:F2} sun {director.SunDirection:F2} " +
               $"elev {director.SunElevation:F1} daylight {director.Daylight:F2} | RenderSettings.sun {(RenderSettings.sun ? RenderSettings.sun.name : "-")} " +
               $"int {(RenderSettings.sun ? RenderSettings.sun.intensity : 0f):F2} | cozy sun {(atmosphere?.SunLight != null ? $"{atmosphere.SunLight.enabled} {atmosphere.SunLight.intensity:F2}" : "-")} " +
               $"moon {(atmosphere?.MoonLight != null ? $"{atmosphere.MoonLight.enabled} {atmosphere.MoonLight.intensity:F2}" : "-")} | skybox {(RenderSettings.skybox ? RenderSettings.skybox.name : "none")}";
    }

    // Toward the sun at an hour, the way COZY places it (CozyAtmosphereModule.GetSunBase), for given sky pitch and rotation.
    static Vector3 SunToward(float hour, float pitch, float rotation)
    {
        Quaternion sky = Quaternion.AngleAxis(rotation, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.forward);
        return -(sky * Quaternion.AngleAxis(hour / 24f * 360f - 90f, Vector3.right) * Vector3.forward);
    }

    static float Elevation(Vector3 direction) => Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;

    // COZY's sun runs a tilted circle (up 06:00, down 18:00 on its clock): pitch sets the noon height, rotation the bearing.
    // Matches our SunPath's noon elevation and its 09:00 bearing (at CozyHour(9)). Project-global COZY settings, saved.
    static string FitSunPath()
    {
        var path = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(TimeOfDayPath).Path;
        float pitch = 90f - path.NoonElevation;
        Vector3 target = path.Sun(9f, out _);
        float targetBearing = Mathf.Atan2(target.x, target.z) * Mathf.Rad2Deg;
        float nine = CozyBridge.CozyHour(9f, path.Sunrise, path.Sunset);
        float best = 0f;
        float bestError = float.MaxValue;
        for (float rotation = 0f; rotation < 360f; rotation += 0.25f)
        {
            Vector3 sun = SunToward(nine, pitch, rotation);
            float error = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(sun.x, sun.z) * Mathf.Rad2Deg, targetBearing));
            if (error < bestError)
            {
                bestError = error;
                best = rotation;
            }
        }
        var prefs = new SerializedObject(CozyPreferences.Instance);
        prefs.FindProperty("skyPitch").floatValue = pitch;
        prefs.FindProperty("skyRotation").floatValue = best;
        prefs.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(CozyPreferences.Instance);
        Vector3 fitted = SunToward(nine, pitch, best);
        return $"sky pitch {pitch:F1} rotation {best:F2} (09:00 bearing {Mathf.Atan2(fitted.x, fitted.z) * Mathf.Rad2Deg:F1} vs {targetBearing:F1}, " +
               $"elevation {Elevation(fitted):F1} vs {Elevation(target):F1})";
    }

    static string SetFeature(bool active)
    {
        var changed = new List<string>();
        foreach (var data in AssetDatabase.FindAssets("t:UniversalRendererData").Select(AssetDatabase.GUIDToAssetPath)
                     .Select(AssetDatabase.LoadAssetAtPath<UniversalRendererData>).Where(d => d != null))
        {
            foreach (var feature in data.rendererFeatures.Where(f => f is CozyRendererFeature && f.isActive != active))
            {
                feature.SetActive(active);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
                changed.Add(data.name);
            }
            AssetDatabase.SaveAssetIfDirty(data);
        }
        return changed.Count > 0 ? $"feature {(active ? "on" : "off")} in {string.Join(", ", changed)}" : "feature unchanged";
    }

    static GameObject AddChild(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    static void AddFlare(GameObject root, string name)
    {
        var go = AddChild(root, name);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.bounceIntensity = 0f;
        light.enabled = false;
        var flare = go.AddComponent<LensFlareComponentSRP>();
        flare.enabled = false;
        flare.lightOverride = light;
        flare.useOcclusion = true;
    }

    // Shadows, bias, layers and URP data from the director's light; the sun also keeps its cloud cookie and the cloud mover.
    static void CopyLight(Light source, Light target, bool sun)
    {
        EditorUtility.CopySerialized(source, target);
        target.type = LightType.Directional;
        target.bounceIntensity = 0f;
        target.enabled = sun;
        if (source.TryGetComponent(out UniversalAdditionalLightData sourceData))
        {
            if (!target.TryGetComponent(out UniversalAdditionalLightData targetData))
                targetData = target.gameObject.AddComponent<UniversalAdditionalLightData>();
            EditorUtility.CopySerialized(sourceData, targetData);
        }
        if (!sun)
        {
            target.cookie = null;
            return;
        }
        var mover = source.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "CloudMover");
        if (mover != null)
            EditorUtility.CopySerialized(mover, target.gameObject.AddComponent(mover.GetType()));
    }

    static AtmosphereProperty<T>.AtmosphereGradientKey Key<T>(T value, float hour) where T : IInterpolatedValue<T> =>
        new(value, hour / 24f);

    static AtmosphereProperty<T> Constant<T>(T value) where T : IInterpolatedValue<T>
    {
        // COZY's value constructor also turns interpolation on with one default key, which would evaluate to zero.
        var property = new AtmosphereProperty<T>(value) { interpolateValue = false };
        return property;
    }
}
