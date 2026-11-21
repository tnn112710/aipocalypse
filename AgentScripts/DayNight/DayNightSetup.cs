using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Editor.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Volume = UnityEngine.Rendering.Volume;

// Builds the day/night cycle: data assets (clock, looks, grades, sky, ambience, clock style), the scene wiring in
// World Map (director, reactors, scope fields) and the HUD clock. Every entry point is safe to re-run.
public static class DayNightSetup
{
    const string ClockPath = "Assets/_Game/Data/WorldTime/ClockSettings.asset";
    const string TodDir = "Assets/_Game/Data/Environment/TimeOfDay";
    const string ProfilePath = TodDir + "/TimeOfDay.asset";
    const string SkyPath = TodDir + "/TimeOfDay_Sky.mat";
    const string AmbiencePath = TodDir + "/TimeOfDay_Ambience.asset";
    const string BasePostPath = TodDir + "/Base_PostProcessing.asset";
    const string OldSkyPath = TodDir + "/Base_Skybox.mat";
    const string AudioLibPath = "Assets/_Game/Data/Audio/Ambience.asset";
    const string AudioDir = "Assets/_Game/Data/Audio/Ambience";
    const string ClockStylePath = "Assets/_Game/Data/UI/ClockStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string FontDir = "Assets/_Game/Data/UI/Fonts/";

    public static object All(bool resetLooks)
    {
        var a = Assets(resetLooks);
        var s = Scene();
        var h = Hud();
        return $"{a}\n{s}\n{h}";
    }

    // ---------------------------------------------------------------- assets

    public static object Assets(bool resetLooks)
    {
        Folder(System.IO.Path.GetDirectoryName(ClockPath));
        Folder(TodDir + "/Looks");
        Folder(TodDir + "/Grades");
        Folder(AudioDir);

        var clock = LoadOrCreate<ClockSettings>(ClockPath, out bool newClock);
        if (newClock)
        {
            clock.SecondsPerGameHour = 30f;
            clock.StartHour = 7f;
            EditorUtility.SetDirty(clock);
        }

        var looks = BuildLooks(resetLooks);
        var grades = BuildGrades(resetLooks);
        var profile = BuildProfile(looks, grades, resetLooks);
        var sky = BuildSky();
        var ambience = BuildAmbience(resetLooks);
        var style = BuildClockStyle();

        AssetDatabase.SaveAssets();
        return $"assets: clock {(newClock ? "new" : "kept")}, {looks.Count} looks, {grades.Count} grades, {profile.Keys.Count} keys, sky {sky.name}, ambience {ambience.Beds.Length} beds, style {style.name}";
    }

    static Dictionary<string, LightingPreset> BuildLooks(bool reset)
    {
        var looks = new Dictionary<string, LightingPreset>();

        LightingPreset Look(string name, Action<LightingPreset> fill)
        {
            var preset = LoadOrCreate<LightingPreset>($"{TodDir}/Looks/TOD_{name}.asset", out bool created);
            if (created || reset)
            {
                fill(preset);
                EditorUtility.SetDirty(preset);
            }
            looks[name] = preset;
            return preset;
        }

        // Today's World Map look (Atmosphere.cs), taken from the open scene so 09:00 matches it exactly.
        Light sun = RenderSettings.sun;
        var oldSky = AssetDatabase.LoadAssetAtPath<Material>(OldSkyPath);
        Color sunColor = sun != null && sun.useColorTemperature
            ? (sun.color.linear * Mathf.CorrelatedColorTemperatureToRGB(sun.colorTemperature)).gamma
            : sun != null ? sun.color : new Color(1f, 0.87f, 0.74f);
        sunColor.a = 1f;

        Look("Morning", p =>
        {
            p.SunColor = sunColor;
            p.SunIntensity = sun != null && sun.intensity > 0.5f ? sun.intensity : 2.8f;
            p.SunShadows = 1f;
            p.MoonIntensity = 0f;
            p.AmbientSky = Opaque(RenderSettings.ambientSkyColor);
            p.AmbientEquator = Opaque(RenderSettings.ambientEquatorColor);
            p.AmbientGround = Opaque(RenderSettings.ambientGroundColor);
            p.ReflectionIntensity = 1f;
            p.FogColor = Opaque(RenderSettings.fogColor);
            p.FogDensity = RenderSettings.fogDensity;
            p.SkyTint = oldSky != null ? Opaque(oldSky.GetColor("_Tint")) : new Color(0.462f, 0.481f, 0.502f);
            p.SkyExposure = oldSky != null ? oldSky.GetFloat("_Exposure") : 0.31f;
            p.NightSkyBlend = 0f;
            p.HorizonGlow = Color.black;
            p.SunDisc = 0.6f;
            p.Stars = 0f;
            p.MoonGlow = 0f;
            p.HazeColor = new Color(0.30f, 0.42f, 0.55f);
            p.UnlitTint = Color.white;
        });
        LightingPreset morning = looks["Morning"];

        // Everything else starts from the morning look and changes what that hour needs.
        // CopySerialized also copies the name, which must keep matching the file.
        void CopyLook(LightingPreset source, LightingPreset p)
        {
            string name = p.name;
            EditorUtility.CopySerialized(source, p);
            p.name = name;
        }

        void From(LightingPreset p) => CopyLook(morning, p);

        Look("Midnight", p =>
        {
            From(p);
            p.SunIntensity = 0f;
            p.MoonColor = new Color(0.55f, 0.66f, 1f);
            p.MoonIntensity = 0.75f;
            p.MoonShadows = 0.55f;
            p.AmbientSky = new Color(0.24f, 0.29f, 0.48f);
            p.AmbientEquator = new Color(0.17f, 0.21f, 0.34f);
            p.AmbientGround = new Color(0.09f, 0.1f, 0.14f);
            p.ReflectionIntensity = 0.2f;
            p.FogColor = new Color(0.06f, 0.08f, 0.15f);
            p.FogDensity = 0.0013f;
            p.SkyTint = new Color(0.05f, 0.06f, 0.1f);
            p.NightSkyTop = new Color(0.012f, 0.02f, 0.05f);
            p.NightSkyHorizon = new Color(0.05f, 0.08f, 0.15f);
            p.NightSkyBlend = 0.95f;
            p.SunDisc = 0f;
            p.Stars = 2.5f;
            p.MoonGlow = 1f;
            p.HazeColor = new Color(0.06f, 0.08f, 0.14f);
            p.UnlitTint = new Color(0.16f, 0.2f, 0.32f);
        });
        Look("Night", p =>
        {
            CopyLook(looks["Midnight"], p);
            p.MoonIntensity = 0.7f;
            p.AmbientSky = new Color(0.25f, 0.3f, 0.5f);
            p.AmbientEquator = new Color(0.18f, 0.22f, 0.35f);
            p.Stars = 2.2f;
        });
        Look("BlueHour", p =>
        {
            From(p);
            p.SunIntensity = 0f;
            p.MoonColor = new Color(0.55f, 0.66f, 1f);
            p.MoonIntensity = 0.4f;
            p.MoonShadows = 0.45f;
            p.AmbientSky = new Color(0.28f, 0.32f, 0.5f);
            p.AmbientEquator = new Color(0.18f, 0.2f, 0.3f);
            p.AmbientGround = new Color(0.09f, 0.1f, 0.13f);
            p.ReflectionIntensity = 0.3f;
            p.FogColor = new Color(0.14f, 0.17f, 0.28f);
            p.FogDensity = 0.0016f;
            p.SkyTint = new Color(0.12f, 0.13f, 0.19f);
            p.SkyExposure = morning.SkyExposure * 0.65f;
            p.NightSkyTop = new Color(0.025f, 0.04f, 0.09f);
            p.NightSkyHorizon = new Color(0.14f, 0.16f, 0.28f);
            p.NightSkyBlend = 0.8f;
            p.HorizonGlow = new Color(0.15f, 0.08f, 0.1f);
            p.SunDisc = 0f;
            p.Stars = 1f;
            p.MoonGlow = 0.6f;
            p.HazeColor = new Color(0.12f, 0.15f, 0.24f);
            p.UnlitTint = new Color(0.3f, 0.34f, 0.48f);
        });
        Look("Dawn", p =>
        {
            From(p);
            p.SunColor = new Color(1f, 0.55f, 0.4f);
            p.SunIntensity = 0.8f;
            p.MoonIntensity = 0.15f;
            p.AmbientSky = new Color(0.45f, 0.47f, 0.62f);
            p.AmbientEquator = new Color(0.5f, 0.42f, 0.44f);
            p.AmbientGround = new Color(0.25f, 0.22f, 0.22f);
            p.ReflectionIntensity = 0.6f;
            p.FogColor = new Color(0.55f, 0.5f, 0.58f);
            p.FogDensity = 0.0022f;
            p.SkyTint = new Color(0.32f, 0.28f, 0.34f);
            p.SkyExposure = morning.SkyExposure * 0.55f;
            p.NightSkyTop = new Color(0.06f, 0.08f, 0.17f);
            p.NightSkyHorizon = new Color(0.45f, 0.32f, 0.4f);
            p.NightSkyBlend = 0.35f;
            p.HorizonGlow = new Color(0.9f, 0.42f, 0.28f);
            p.SunDisc = 0.8f;
            p.Stars = 0.1f;
            p.MoonGlow = 0.2f;
            p.HazeColor = new Color(0.45f, 0.4f, 0.5f);
            p.UnlitTint = new Color(0.75f, 0.68f, 0.75f);
        });
        Look("Sunrise", p =>
        {
            From(p);
            p.SunColor = new Color(1f, 0.62f, 0.38f);
            p.SunIntensity = 2f;
            p.SunShadows = 0.9f;
            p.AmbientSky = new Color(0.58f, 0.6f, 0.72f);
            p.AmbientEquator = new Color(0.68f, 0.55f, 0.46f);
            p.AmbientGround = new Color(0.36f, 0.3f, 0.25f);
            p.FogColor = new Color(0.78f, 0.66f, 0.58f);
            p.FogDensity = 0.0018f;
            p.SkyTint = new Color(0.46f, 0.4f, 0.38f);
            p.SkyExposure = morning.SkyExposure * 0.75f;
            p.HorizonGlow = new Color(0.8f, 0.4f, 0.18f);
            p.SunDisc = 1f;
            p.HazeColor = new Color(0.55f, 0.46f, 0.45f);
            p.UnlitTint = new Color(1f, 0.88f, 0.8f);
        });
        Look("Noon", p =>
        {
            From(p);
            p.SunColor = new Color(1f, 0.95f, 0.88f);
            p.SunIntensity = 3f;
            p.AmbientSky = new Color(0.66f, 0.76f, 0.9f);
            p.AmbientEquator = new Color(0.6f, 0.62f, 0.55f);
            p.AmbientGround = new Color(0.44f, 0.41f, 0.33f);
            p.FogColor = new Color(0.6f, 0.78f, 0.96f);
            p.FogDensity = 0.001f;
            p.SkyTint = new Color(0.48f, 0.5f, 0.52f);
            p.SkyExposure = morning.SkyExposure * 1.06f;
            p.HazeColor = new Color(0.32f, 0.45f, 0.6f);
        });
        Look("Afternoon", p =>
        {
            From(p);
            p.SunColor = new Color(1f, 0.86f, 0.7f);
            p.AmbientEquator = new Color(0.6f, 0.58f, 0.5f);
            p.FogColor = new Color(0.62f, 0.75f, 0.9f);
            p.SkyTint = new Color(0.48f, 0.47f, 0.46f);
        });
        Look("GoldenHour", p =>
        {
            From(p);
            p.SunColor = new Color(1f, 0.7f, 0.42f);
            p.SunIntensity = 2.4f;
            p.AmbientSky = new Color(0.6f, 0.6f, 0.68f);
            p.AmbientEquator = new Color(0.68f, 0.55f, 0.42f);
            p.AmbientGround = new Color(0.38f, 0.3f, 0.22f);
            p.FogColor = new Color(0.85f, 0.7f, 0.55f);
            p.FogDensity = 0.0014f;
            p.SkyTint = new Color(0.5f, 0.42f, 0.36f);
            p.SkyExposure = morning.SkyExposure * 0.87f;
            p.HorizonGlow = new Color(0.6f, 0.3f, 0.12f);
            p.SunDisc = 1f;
            p.HazeColor = new Color(0.6f, 0.48f, 0.42f);
            p.UnlitTint = new Color(1f, 0.85f, 0.72f);
        });
        Look("Sunset", p =>
        {
            From(p);
            p.SunColor = new Color(1f, 0.48f, 0.25f);
            p.SunIntensity = 1.6f;
            p.SunShadows = 0.85f;
            p.MoonColor = new Color(0.55f, 0.66f, 1f);
            p.MoonIntensity = 0.1f;
            p.AmbientSky = new Color(0.5f, 0.45f, 0.58f);
            p.AmbientEquator = new Color(0.62f, 0.44f, 0.38f);
            p.AmbientGround = new Color(0.3f, 0.22f, 0.18f);
            p.ReflectionIntensity = 0.7f;
            p.FogColor = new Color(0.65f, 0.42f, 0.38f);
            p.FogDensity = 0.0018f;
            p.SkyTint = new Color(0.42f, 0.26f, 0.28f);
            // Dark enough to match the ground: the sun light is already at 0 on the horizon.
            p.SkyExposure = morning.SkyExposure * 0.21f;
            p.NightSkyTop = new Color(0.05f, 0.06f, 0.14f);
            p.NightSkyHorizon = new Color(0.32f, 0.16f, 0.14f);
            p.NightSkyBlend = 0.62f;
            p.HorizonGlow = new Color(0.9f, 0.35f, 0.14f);
            p.SunDisc = 1.2f;
            p.HazeColor = new Color(0.45f, 0.32f, 0.34f);
            p.UnlitTint = new Color(0.85f, 0.65f, 0.6f);
        });
        Look("Dusk", p =>
        {
            From(p);
            p.SunIntensity = 0f;
            p.MoonColor = new Color(0.55f, 0.66f, 1f);
            p.MoonIntensity = 0.35f;
            p.MoonShadows = 0.5f;
            p.AmbientSky = new Color(0.3f, 0.3f, 0.48f);
            p.AmbientEquator = new Color(0.26f, 0.22f, 0.3f);
            p.AmbientGround = new Color(0.1f, 0.09f, 0.11f);
            p.ReflectionIntensity = 0.35f;
            p.FogColor = new Color(0.18f, 0.17f, 0.28f);
            p.FogDensity = 0.0016f;
            p.SkyTint = new Color(0.1f, 0.09f, 0.14f);
            p.SkyExposure = morning.SkyExposure * 0.7f;
            p.NightSkyTop = new Color(0.025f, 0.035f, 0.09f);
            p.NightSkyHorizon = new Color(0.18f, 0.13f, 0.22f);
            p.NightSkyBlend = 0.8f;
            p.HorizonGlow = new Color(0.35f, 0.12f, 0.08f);
            p.SunDisc = 0f;
            p.Stars = 1.2f;
            p.MoonGlow = 0.5f;
            p.HazeColor = new Color(0.16f, 0.15f, 0.24f);
            p.UnlitTint = new Color(0.4f, 0.38f, 0.5f);
        });
        return looks;
    }

    static Dictionary<string, VolumeProfile> BuildGrades(bool reset)
    {
        var basePost = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BasePostPath);
        basePost.TryGet(out ColorAdjustments baseColor);
        basePost.TryGet(out WhiteBalance baseWhite);
        basePost.TryGet(out Bloom baseBloom);
        basePost.TryGet(out Vignette baseVignette);

        float exposure = Value(baseColor?.postExposure, 0f);
        float contrast = Value(baseColor?.contrast, 0f);
        float saturation = Value(baseColor?.saturation, 0f);
        Color filter = baseColor != null && baseColor.colorFilter.overrideState ? baseColor.colorFilter.value : Color.white;
        float temperature = Value(baseWhite?.temperature, 0f);
        float tint = Value(baseWhite?.tint, 0f);
        float bloom = Value(baseBloom?.intensity, 0f);
        float vignette = Value(baseVignette?.intensity, 0f);

        var grades = new Dictionary<string, VolumeProfile>();
        void Grade(string name, float dExposure, float dContrast, Color colorFilter, float dSaturation, float dTemperature, float dTint, float dBloom, float dVignette)
        {
            string path = $"{TodDir}/Grades/PP_{name}.asset";
            var profile = LoadOrCreate<VolumeProfile>(path, out bool created);
            grades[name] = profile;
            if (!created && !reset)
                return;

            foreach (VolumeComponent old in profile.components.ToList())
            {
                profile.components.Remove(old);
                Object.DestroyImmediate(old, true);
            }

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(exposure + dExposure);
            color.contrast.Override(Mathf.Clamp(contrast + dContrast, -100f, 100f));
            color.colorFilter.Override(filter * colorFilter);
            color.saturation.Override(Mathf.Clamp(saturation + dSaturation, -100f, 100f));
            var white = Add<WhiteBalance>(profile);
            white.temperature.Override(Mathf.Clamp(temperature + dTemperature, -100f, 100f));
            white.tint.Override(Mathf.Clamp(tint + dTint, -100f, 100f));
            Add<Bloom>(profile).intensity.Override(Mathf.Max(0f, bloom + dBloom));
            Add<Vignette>(profile).intensity.Override(Mathf.Clamp01(vignette + dVignette));
            MergeUnder(basePost, profile);
            EditorUtility.SetDirty(profile);
        }

        // Day = today's grade unchanged, so the morning still looks exactly like the current scene.
        Grade("Day", 0f, 0f, Color.white, 0f, 0f, 0f, 0f, 0f);
        Grade("Golden", 0f, 4f, new Color(1f, 0.95f, 0.88f), 6f, 12f, 0f, 0.1f, 0f);
        Grade("Dawn", 0.05f, 0f, new Color(0.98f, 0.94f, 1f), -4f, -4f, 6f, 0.05f, 0f);
        Grade("Night", 0.45f, 6f, new Color(0.82f, 0.88f, 1f), -22f, -22f, 4f, 0.25f, 0.1f);
        return grades;
    }

    static TimeOfDayProfile BuildProfile(Dictionary<string, LightingPreset> looks, Dictionary<string, VolumeProfile> grades, bool reset)
    {
        var profile = LoadOrCreate<TimeOfDayProfile>(ProfilePath, out bool created);
        if (!created && !reset && profile.Keys.Count > 0)
            return profile;

        TimeOfDayKey Key(string name, float hour, string look, string grade) => new()
        {
            Name = name,
            Hour = hour,
            Look = looks[look],
            Grade = grades[grade],
            Blend = KeyBlend.Smooth,
        };

        profile.Keys = new List<TimeOfDayKey>
        {
            Key("Midnight", 0f, "Midnight", "Night"),
            Key("Blue hour", 4.5f, "BlueHour", "Night"),
            Key("Dawn", 5.5f, "Dawn", "Dawn"),
            Key("Sunrise", 6.5f, "Sunrise", "Golden"),
            Key("Morning", 9f, "Morning", "Day"),
            Key("Noon", 12.5f, "Noon", "Day"),
            Key("Afternoon", 16f, "Afternoon", "Day"),
            Key("Golden hour", 17f, "GoldenHour", "Golden"),
            Key("Sunset", 18f, "Sunset", "Golden"),
            Key("Dusk", 19f, "Dusk", "Night"),
            Key("Night", 20.5f, "Night", "Night"),
        };
        profile.Path = new SunPath();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static Material BuildSky()
    {
        var shader = Shader.Find("Aipocalypse/Skybox/Time Of Day");
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        if (sky == null)
        {
            sky = new Material(shader);
            AssetDatabase.CreateAsset(sky, SkyPath);
        }
        sky.shader = shader;
        var old = AssetDatabase.LoadAssetAtPath<Material>(OldSkyPath);
        if (old != null)
        {
            sky.SetTexture("_MainTex", old.GetTexture("_MainTex"));
            sky.SetFloat("_Rotation", old.GetFloat("_Rotation"));
            sky.SetColor("_Tint", old.GetColor("_Tint"));
            sky.SetFloat("_Exposure", old.GetFloat("_Exposure"));
        }
        sky.SetFloat("_StarDensity", 140f);
        sky.SetFloat("_StarSize", 0.26f);
        EditorUtility.SetDirty(sky);
        return sky;
    }

    static TimeOfDayAmbience BuildAmbience(bool reset)
    {
        var library = LoadOrCreate<AudioAsset>(AudioLibPath, out bool newLibrary);
        if (newLibrary)
        {
            var so = new SerializedObject(library);
            so.FindProperty("<AssetName>k__BackingField").stringValue = "Ambience";
            so.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(AudioLibPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        const string Raygeas = "Assets/_StoreAssets/Env/Raygeas/";
        var birds = Entity(library, "Birds", true, 0.8f, 0, 0f, 0f,
            Raygeas + "Lakefell Village/Assets/Audio/Ambience/Ambience_Birds_1.wav",
            Raygeas + "Lakefell Village/Assets/Audio/Ambience/Ambience_Birds_2.wav");
        var wind = Entity(library, "Wind", true, 0.5f, 0, 0f, 0f,
            Raygeas + "Grasslands - Stylized Nature/Assets/Audio/Nature/Wind/Wind_Meadow_1.wav");
        var owl = Entity(library, "Owl", false, 0.7f, 2, 0.08f, 0.15f,
            Raygeas + "HIGHLANDS - Stylized Environment/Assets/Audio/Forest/Owls/Owl_1.wav",
            Raygeas + "HIGHLANDS - Stylized Environment/Assets/Audio/Forest/Owls/Owl_2.wav",
            Raygeas + "HIGHLANDS - Stylized Environment/Assets/Audio/Forest/Owls/Owl_3.wav");

        var ambience = LoadOrCreate<TimeOfDayAmbience>(AmbiencePath, out bool created);
        if (!created && !reset)
            return ambience;

        ambience.Beds = new[]
        {
            new TimeOfDayAmbience.Bed
            {
                Name = "Dawn chorus and day birds",
                Volume = Curve((0f, 0f), (4.5f, 0f), (5.5f, 0.9f), (6.5f, 1f), (8f, 0.55f), (12f, 0.35f), (17f, 0.4f), (19f, 0.25f), (20f, 0f), (24f, 0f)),
            },
            new TimeOfDayAmbience.Bed
            {
                Name = "Meadow wind",
                Volume = Curve((0f, 0.55f), (6f, 0.35f), (12f, 0.3f), (18f, 0.35f), (21f, 0.55f), (24f, 0.55f)),
            },
        };
        ambience.OneShots = new[]
        {
            new TimeOfDayAmbience.Calls
            {
                Name = "Owls",
                Hours = new TimeWindow(20.5f, 4.5f, 1f),
                Interval = new Vector2(18f, 45f),
                Volume = 0.6f,
            },
        };
        ambience.VolumeFade = 2f;
        EditorUtility.SetDirty(ambience);

        var so2 = new SerializedObject(ambience);
        so2.FindProperty("Beds.Array.data[0].Sound._entity").objectReferenceValue = birds;
        so2.FindProperty("Beds.Array.data[1].Sound._entity").objectReferenceValue = wind;
        so2.FindProperty("OneShots.Array.data[0].Sound._entity").objectReferenceValue = owl;
        so2.ApplyModifiedPropertiesWithoutUndo();
        return ambience;
    }

    static AudioEntity Entity(AudioAsset library, string name, bool loop, float volume, int playMode, float pitchRandom, float volumeRandom, params string[] clips)
    {
        string path = $"{AudioDir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.Ambience);
            AssetDatabase.CreateAsset(entity, path);
        }

        var so = new SerializedObject(entity);
        so.FindProperty("<Loop>k__BackingField").boolValue = loop;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("MulticlipsPlayMode").intValue = playMode;
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<VolumeRandomRange>k__BackingField").floatValue = volumeRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = (pitchRandom > 0f ? 1 : 0) | (volumeRandom > 0f ? 2 : 0);
        var list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]);
            if (clip == null)
                throw new Exception("missing clip " + clips[i]);
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue = clip;
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entity);
        return entity;
    }

    static ClockStyle BuildClockStyle()
    {
        var style = LoadOrCreate<ClockStyle>(ClockStylePath, out _);
        style.TimeFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "SairaHeader.asset");
        style.TimeMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontDir + "SairaHeader_Underlay.mat");
        style.LabelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "SairaBold.asset");
        style.LabelMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontDir + "SairaBold_Underlay.mat");
        style.LabelFontSize = 26f;
        // Layer Lab chamfered plate, as LayerLabSetup.
        style.BackingSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_StoreAssets/UI/Layer Lab/GUI Pro-SurvivalClean/ResourcesData/Sprites/Components/Frame_Custom/Frame_ItemFrame04_n_White3.png").OfType<Sprite>().First();
        style.BackingColor = new Color(0.047f, 0.094f, 0.157f, 0.82f);
        style.Phases[DayPhase.Morning] = new PhaseLabel(LocalizationKit.Ref(Loc.HudTable, "phase.morning"), new Color(1f, 0.86f, 0.45f, 1f));
        style.DayColor = new Color(0.97f, 0.97f, 0.95f, 1f);
        EditorUtility.SetDirty(style);
        return style;
    }

    // ---------------------------------------------------------------- scene (World Map)

    public static object Scene()
    {
        var scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        var environment = Root(scene, "Environment") ?? throw new Exception("no Environment root");

        // Director and its two grade volumes.
        var todTransform = environment.transform.Find("Time Of Day");
        var tod = todTransform != null ? todTransform.gameObject : new GameObject("Time Of Day");
        if (todTransform == null)
        {
            Undo.RegisterCreatedObjectUndo(tod, "Time Of Day");
            tod.transform.SetParent(environment.transform, false);
            tod.transform.SetAsFirstSibling();
        }
        var director = Get<EnvironmentDirector>(tod);
        var from = GradeVolume(tod.transform, "Grade From", 10f);
        var to = GradeVolume(tod.transform, "Grade To", 11f);
        RestGrades(from, to);
        RemoveBaseVolume(log);

        var so = new SerializedObject(director);
        so.FindProperty("_profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(ProfilePath);
        so.FindProperty("_light").objectReferenceValue = RenderSettings.sun;
        so.FindProperty("_gradeFrom").objectReferenceValue = from;
        so.FindProperty("_gradeTo").objectReferenceValue = to;
        so.FindProperty("_preview").boolValue = false;
        so.FindProperty("_previewHour").floatValue = 9f;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("director " + PathOf(tod.transform));

        // Keeps a Skybox Plus sky (SkyboxPlusSetup.Use switches between the two).
        if (RenderSettings.skybox == null || !RenderSettings.skybox.HasProperty("_MoonPhaseAngle"))
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        log.Add("skybox " + RenderSettings.skybox.name);

        // Atmosphere effects.
        string atmosphere = "Environment/Atmosphere/";
        Particles(atmosphere + "God Rays", ReactorSource.Daylight, default, 0f, 1f, alignToSun: true);
        Tint(atmosphere + "God Rays");
        Particles(atmosphere + "Mist", ReactorSource.Hours, new TimeWindow(4f, 9.5f, 1.5f), 1f, 2.6f);
        Tint(atmosphere + "Mist");
        Tint(atmosphere + "Swamp Fog");
        Tint(atmosphere + "Clouds");
        Particles(atmosphere + "Birds", ReactorSource.Hours, new TimeWindow(5f, 18f, 1f), 0f, 1f);
        Tint(atmosphere + "Birds");
        Particles(atmosphere + "Butterflies", ReactorSource.Hours, new TimeWindow(7.5f, 17f, 1f), 0f, 1f);
        Particles(atmosphere + "Seed Fluff", ReactorSource.Daylight, default, 0f, 1f);
        Particles(atmosphere + "Sunbeam Motes", ReactorSource.Daylight, default, 0f, 1f);
        Tint(atmosphere + "Sunbeam Motes");
        Particles(atmosphere + "Fireflies", ReactorSource.Hours, new TimeWindow(20f, 5f, 1f), 0f, 1f);

        // Boundary: haze banks are unlit; the HIGHLANDS / Grasslands mountain copies keep their own height haze.
        Tint("Environment/Boundary/Haze");
        Tint("Environment/Boundary/Peaks", new[] { "_FogColor" }, AtmosphereChannel.Haze, multiply: false);

        // Waterfall: the sheet, ripples, splash and mist are all unlit (the sheet keeps its colours in five properties).
        const string Komorebi = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Materials/";
        Tint("Environment/Landmarks/Waterfall",
            new[] { "_BaseColor", "_Color_1", "_Color_2", "_Foam_1_Color", "_Foam_1_Shadow_Color", "_Foam_2_Color" },
            AtmosphereChannel.UnlitTint, true,
            Mat("Assets/_Game/Environment/Water/Water_Waterfall.mat"),
            Mat(Komorebi + "Nature/Waterfall_Ripples_1.mat"),
            Mat(Komorebi + "Particle Systems/Waterfall_Splash_1.mat"),
            Mat(Komorebi + "Particle Systems/Fog_Particle.mat"));

        // Home: the windows glow from dusk to morning, and a warm light spills out of the door.
        var house = GameObject.Find("Environment/Landmarks/Home Base/House_1");
        var glass = house.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name == "Window_Glass");
        var windows = Get<TimeOfDayEmission>(house);
        var wso = new SerializedObject(windows);
        var mats = wso.FindProperty("_materials");
        mats.arraySize = 1;
        mats.GetArrayElementAtIndex(0).objectReferenceValue = glass;
        Window(wso, new TimeWindow(18f, 6.5f, 0.75f));
        wso.ApplyModifiedPropertiesWithoutUndo();
        log.Add("windows " + (glass != null ? glass.name : "NO GLASS"));

        var homeBase = house.transform.parent;
        var lightsTransform = homeBase.Find("Night Lights");
        var lights = lightsTransform != null ? lightsTransform.gameObject : new GameObject("Night Lights");
        lights.transform.SetParent(homeBase, false);
        var door = house.GetComponentsInChildren<Renderer>(true).First(r => r.name.StartsWith("Door_1"));
        Bounds houseBounds = door.bounds;
        foreach (Renderer r in house.GetComponentsInChildren<Renderer>(true))
            houseBounds.Encapsulate(r.bounds);
        Vector3 outward = door.bounds.center - houseBounds.center;
        outward.y = 0f;
        outward.Normalize();
        var doorLight = LightChild(lights.transform, "Door Light", door.bounds.center + outward * 1.6f + Vector3.up * 0.6f, new Color(1f, 0.68f, 0.38f), 2.5f, 8f);
        var lso = new SerializedObject(Get<TimeOfDayLight>(lights));
        Window(lso, new TimeWindow(18.25f, 6.25f, 0.75f));
        lso.ApplyModifiedPropertiesWithoutUndo();
        log.Add("door light at " + doorLight.transform.position.ToString("F1"));

        // Scope: the clock and environment installers (with the ambience) next to the GameLifetimeScope.
        var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        if (!scope.TryGetComponent(out WorldTimeInstaller clockInstaller))
            clockInstaller = scope.gameObject.AddComponent<WorldTimeInstaller>();
        var cso = new SerializedObject(clockInstaller);
        cso.FindProperty("_settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ClockSettings>(ClockPath);
        cso.ApplyModifiedPropertiesWithoutUndo();
        if (!scope.TryGetComponent(out EnvironmentInstaller environmentInstaller))
            environmentInstaller = scope.gameObject.AddComponent<EnvironmentInstaller>();
        var eso = new SerializedObject(environmentInstaller);
        eso.FindProperty("_director").objectReferenceValue = director;
        eso.FindProperty("_ambience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TimeOfDayAmbience>(AmbiencePath);
        eso.ApplyModifiedPropertiesWithoutUndo();

        director.CollectSceneReactors();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"reactors {new SerializedObject(director).FindProperty("_sceneReactors").arraySize}");
        return string.Join("\n", log);
    }

    // One volume at a time: every grade carries the base volume's fixed settings (tonemapping, bloom and vignette shape,
    // lift/gamma/gain), the scene's base volume goes, and the director turns Grade To off while it isn't blending.
    public static object SingleGrade()
    {
        var basePost = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BasePostPath);
        var profile = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(ProfilePath);
        var log = new List<string>();
        foreach (VolumeProfile grade in profile.Keys.Select(k => k.Grade).Where(g => g != null).Distinct())
        {
            MergeUnder(basePost, grade);
            EditorUtility.SetDirty(grade);
            AssetDatabase.SaveAssetIfDirty(grade);
            log.Add(grade.name + ": " + string.Join(", ", grade.components.Select(c => c.GetType().Name)));
        }

        var director = Object.FindAnyObjectByType<EnvironmentDirector>() ?? throw new Exception("no EnvironmentDirector");
        var so = new SerializedObject(director);
        RestGrades((Volume)so.FindProperty("_gradeFrom").objectReferenceValue, (Volume)so.FindProperty("_gradeTo").objectReferenceValue);
        RemoveBaseVolume(log);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        return string.Join("\n", log);
    }

    // Copies each base setting a grade doesn't override, so the grade alone renders what base + grade did.
    static void MergeUnder(VolumeProfile basePost, VolumeProfile grade)
    {
        if (basePost == null)
            return;
        foreach (VolumeComponent source in basePost.components.Where(c => c.active))
        {
            VolumeComponent target = grade.components.FirstOrDefault(c => c.GetType() == source.GetType());
            if (target == null)
            {
                target = grade.Add(source.GetType());
                target.name = source.GetType().Name;
                target.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(target, grade);
            }
            for (int i = 0; i < source.parameters.Count; i++)
            {
                VolumeParameter from = source.parameters[i];
                VolumeParameter to = target.parameters[i];
                if (!from.overrideState || to.overrideState)
                    continue;
                to.SetValue(from);
                to.overrideState = true;
            }
        }
    }

    // The saved scene shows the Day grade, so edit mode (no director running) still looks like the morning.
    static void RestGrades(Volume from, Volume to)
    {
        Undo.RecordObject(from, "Grades");
        Undo.RecordObject(to, "Grades");
        from.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TodDir + "/Grades/PP_Day.asset");
        from.weight = 1f;
        from.enabled = true;
        to.sharedProfile = null;
        to.weight = 0f;
        to.enabled = false;
    }

    static void RemoveBaseVolume(List<string> log)
    {
        var basePost = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BasePostPath);
        foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include).Where(v => v.sharedProfile == basePost))
        {
            log.Add("removed base volume " + volume.name);
            Undo.DestroyObjectImmediate(volume.GetComponents<Component>().Length <= 2 && volume.transform.childCount == 0 ? (Object)volume.gameObject : volume);
        }
    }

    static Volume GradeVolume(Transform parent, string name, float priority)
    {
        var t = parent.Find(name);
        var go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        var volume = Get<Volume>(go);
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 0f;
        volume.sharedProfile = null;
        return volume;
    }

    static void Particles(string path, ReactorSource source, TimeWindow hours, float off, float on, bool alignToSun = false)
    {
        var go = Find(path);
        var reactor = Get<TimeOfDayParticles>(go);
        var so = new SerializedObject(reactor);
        so.FindProperty("_source").enumValueIndex = (int)source;
        Window(so, hours);
        so.FindProperty("_offRate").floatValue = off;
        so.FindProperty("_onRate").floatValue = on;
        so.FindProperty("_alignToSun").boolValue = alignToSun;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Tint(string path) => Tint(path, new[] { "_BaseColor" }, AtmosphereChannel.UnlitTint, true);

    static void Tint(string path, string[] properties, AtmosphereChannel channel, bool multiply, params Material[] materials)
    {
        var go = Find(path);
        var tint = Get<AtmosphereTint>(go);
        var so = new SerializedObject(tint);
        var names = so.FindProperty("_properties");
        names.arraySize = properties.Length;
        for (int i = 0; i < properties.Length; i++)
            names.GetArrayElementAtIndex(i).stringValue = properties[i];
        so.FindProperty("_channel").enumValueIndex = (int)channel;
        so.FindProperty("_multiply").boolValue = multiply;
        so.FindProperty("_keepAlpha").boolValue = true;
        var list = so.FindProperty("_materials");
        list.arraySize = materials.Length;
        for (int i = 0; i < materials.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Window(SerializedObject so, TimeWindow window)
    {
        var hours = so.FindProperty("_hours");
        hours.FindPropertyRelative("On").floatValue = window.On;
        hours.FindPropertyRelative("Off").floatValue = window.Off;
        hours.FindPropertyRelative("Fade").floatValue = window.Fade;
    }

    static Light LightChild(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        var t = parent.Find(name);
        var go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var light = Get<Light>(go);
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    // ---------------------------------------------------------------- HUD clock

    public static object Hud()
    {
        var root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            // Top centre (the minimap holds the top-right corner); the anchor is made by MinimapSetup.
            var topCenter = root.transform.Find("SafeFrame/TopCenter") ?? throw new Exception("no SafeFrame/TopCenter: run MinimapSetup.Hud first");
            var existing = topCenter.Find("Clock");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            // Fixed rects, no layout groups: the digits are monospaced so the clock never changes size, and nothing
            // layout-driven ends up as overrides on the scene's HUD instance.
            var clock = UiObject("Clock", topCenter);
            Anchor(clock, new Vector2(296f, 76f));
            clock.anchorMin = clock.anchorMax = clock.pivot = new Vector2(0.5f, 1f);
            clock.gameObject.AddComponent<Canvas>();

            var content = UiObject("Content", clock);
            Stretch(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var backingRect = UiObject("Backing", content);
            Stretch(backingRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backing = backingRect.gameObject.AddComponent<Image>();
            backing.raycastTarget = false;

            var time = Text("Time", content, "<mspace=0.5em>07:00", TextAlignmentOptions.MidlineLeft);
            Stretch((RectTransform)time.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(16f, 0f), new Vector2(172f, 0f));
            var labels = UiObject("Labels", content);
            Stretch(labels, Vector2.zero, Vector2.one, new Vector2(182f, 8f), new Vector2(-10f, -8f));
            var phase = Text("Phase", labels, "Afternoon", TextAlignmentOptions.BottomLeft);
            Stretch((RectTransform)phase.transform, new Vector2(0f, 0.5f), Vector2.one, Vector2.zero, new Vector2(0f, 2f));
            var day = Text("Day", labels, "Day 1", TextAlignmentOptions.TopLeft);
            Stretch((RectTransform)day.transform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(0f, -2f), Vector2.zero);

            var view = clock.gameObject.AddComponent<ClockView>();
            var vso = new SerializedObject(view);
            vso.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ClockStyle>(ClockStylePath);
            vso.FindProperty("_content").objectReferenceValue = content.gameObject;
            vso.FindProperty("_backing").objectReferenceValue = backing;
            vso.FindProperty("_time").objectReferenceValue = time;
            vso.FindProperty("_phase").objectReferenceValue = phase;
            vso.FindProperty("_day").objectReferenceValue = day;
            vso.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyStyle();
            // Let TMP fill in what it computes on first enable (style hash, colour cache), so the prefab already holds it.
            var normal = TMP_Settings.defaultStyleSheet != null ? TMP_Settings.defaultStyleSheet.GetStyle("Normal") : null;
            foreach (var text in new[] { time, phase, day })
            {
                if (normal != null)
                    text.textStyle = normal;
                text.ForceMeshUpdate();
            }

            var hud = clock.gameObject.AddComponent<ClockHud>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("_view").objectReferenceValue = view;
            hso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return "hud clock at SafeFrame/TopCenter/Clock";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static RectTransform UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Anchor(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    // Puts the scene's HUD prefab instance overrides back to exactly what a reference copy of the scene has (e.g. the
    // committed one): editor layout passes write layout-driven values onto the instance, which is noise in a diff.
    public static object RestoreHudOverrides(string referenceScenePath)
    {
        string hudGuid = AssetDatabase.AssetPathToGUID(HudPath);
        string[] lines = File.ReadAllLines(referenceScenePath);
        int source = Array.FindIndex(lines, l => l.Contains("m_SourcePrefab: {fileID: 100100000, guid: " + hudGuid));
        int start = Array.FindLastIndex(lines, source, l => l.StartsWith("--- !u!1001"));
        var wanted = new List<(long target, string path, string value)>();
        for (int i = start; i < source; i++)
        {
            string line = lines[i].Trim();
            if (!line.StartsWith("- target: {fileID: "))
                continue;
            long target = long.Parse(line.Substring(19, line.IndexOf(',') - 19));
            string path = lines[i + 1].Trim().Substring("propertyPath: ".Length);
            string value = lines[i + 2].Trim().Substring("value:".Length).Trim();
            wanted.Add((target, path, value));
        }

        var byId = new Dictionary<long, Object>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(HudPath))
            if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long id))
                byId[id] = asset;

        var hud = SceneManager.GetActiveScene().GetRootGameObjects().First(g => PrefabUtility.GetCorrespondingObjectFromSource(g) is var p && p != null && AssetDatabase.GetAssetPath(p) == HudPath);
        var modifications = new List<PropertyModification>();
        foreach (var (target, path, value) in wanted)
        {
            if (!byId.TryGetValue(target, out Object targetObject))
                throw new Exception($"reference override target {target} not in {HudPath}");
            modifications.Add(new PropertyModification { target = targetObject, propertyPath = path, value = value });
        }

        int before = PrefabUtility.GetPropertyModifications(hud).Length;
        PrefabUtility.SetPropertyModifications(hud, modifications.ToArray());
        EditorSceneManager.MarkSceneDirty(hud.scene);
        EditorSceneManager.SaveScene(hud.scene);
        return $"HUD overrides {before} -> {PrefabUtility.GetPropertyModifications(hud).Length} (reference {wanted.Count})";
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, TextAlignmentOptions alignment)
    {
        var rect = UiObject(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    // ---------------------------------------------------------------- helpers

    // Not GetComponent() ?? AddComponent(): in the editor a missing component is a fake null that ?? doesn't see.
    static T Get<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;
        if (created)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var component = profile.Add<T>();
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static float Value(VolumeParameter<float> parameter, float fallback) => parameter != null && parameter.overrideState ? parameter.value : fallback;

    static AnimationCurve Curve(params (float t, float v)[] keys)
    {
        var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);
        return curve;
    }

    static Color Opaque(Color c) => new(c.r, c.g, c.b, 1f);

    static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new Exception("missing material " + path);

    static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

    static GameObject Find(string path) => GameObject.Find(path) ?? throw new Exception("not found: " + path);

    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

    static void Folder(string path)
    {
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path))
            return;
        Folder(System.IO.Path.GetDirectoryName(path));
        AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
    }
}
