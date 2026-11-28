using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The yard campfire (docs/technicals/CAMPFIRE.md). Night: the darker night looks and grades (user 2026-11-28: ambient × 0.3,
// moon × 0.5, -0.5 EV at Night / Midnight, half of it at Blue hour); re-run CozySetup.Atmosphere after it. Safe to re-run.
// Run: unity command script run --file AgentScripts/Campfire/CampfireSetup.cs --entry CampfireSetup.Night
public static class CampfireSetup
{
    const string TimeOfDayDir = "Assets/_Game/Data/Environment/TimeOfDay";
    const string ProfilePath = TimeOfDayDir + "/TimeOfDay.asset";
    const string NightGrade = TimeOfDayDir + "/Grades/PP_Night.asset";
    const string NightDarkGrade = TimeOfDayDir + "/Grades/PP_NightDark.asset";
    const string BlueHourDarkGrade = TimeOfDayDir + "/Grades/PP_BlueHourDark.asset";

    // Key name → (ambient scale, moon scale, grade path)
    static readonly (string key, float ambient, float moon, string grade)[] DarkKeys =
    {
        ("Midnight", 0.3f, 0.5f, NightDarkGrade),
        ("Night", 0.3f, 0.5f, NightDarkGrade),
        ("Blue hour", 0.65f, 0.75f, BlueHourDarkGrade),
    };

    public static string Night()
    {
        var log = new List<string>();
        var profile = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(ProfilePath) ?? throw new Exception("no " + ProfilePath);
        VolumeProfile nightDark = CloneGrade(NightDarkGrade, -0.5f, log);
        VolumeProfile blueDark = CloneGrade(BlueHourDarkGrade, -0.25f, log);
        for (int i = 0; i < profile.Keys.Count; i++)
        {
            TimeOfDayKey key = profile.Keys[i];
            int found = Array.FindIndex(DarkKeys, k => string.Equals(k.key, key.Name, StringComparison.OrdinalIgnoreCase));
            if (found < 0)
                continue;
            (string _, float ambient, float moon, string gradePath) = DarkKeys[found];
            VolumeProfile grade = gradePath == NightDarkGrade ? nightDark : blueDark;
            // The grade swap marks a key as done, so a re-run doesn't darken twice.
            if (key.Grade == grade)
            {
                log.Add($"{key.Name}: already dark");
                continue;
            }
            LightingPreset look = key.Look;
            look.AmbientSky *= ambient;
            look.AmbientEquator *= ambient;
            look.AmbientGround *= ambient;
            look.AmbientSky.a = look.AmbientEquator.a = look.AmbientGround.a = 1f;
            look.MoonIntensity *= moon;
            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssetIfDirty(look);
            key.Grade = grade;
            profile.Keys[i] = key;
            log.Add($"{key.Name}: {look.name} ambient × {ambient}, moon × {moon} → {look.MoonIntensity:0.###}; grade {grade.name}");
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        log.Add("now run CozySetup.Atmosphere to bake it into COZY");
        return string.Join("\n", log);
    }

    // ---------------------------------------------------------------- wood

    const string WoodDir = "Assets/_Game/Content/Items/Wood";
    const string WoodPath = WoodDir + "/Wood.asset";
    const string WoodPrefabPath = WoodDir + "/Wood.prefab";
    const string WoodIcon = "Assets/Synty/InterfaceApocalypseHUD/Sprites/Icons_Resources/ICON_SM_Prop_Wood_Stack_01.png";
    const string LogModel = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Camp/Log_1.prefab";
    const string CatalogPath = "Assets/_Game/Data/Equipment/ItemCatalog.asset";

    // The wood item: a log, 1 kg, 2 h of campfire fuel (user 2026-11-28), stacks to 20, carried in the backpack.
    public static string Wood()
    {
        var log = new List<string> { MB13.Aipocalypse.Editor.Localization.LocalizationCsv.ImportAll() };
        System.IO.Directory.CreateDirectory(WoodDir);
        var wood = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.EquipmentSystem.MaterialDefinition>(WoodPath);
        if (wood == null)
        {
            wood = ScriptableObject.CreateInstance<MB13.Aipocalypse.Core.EquipmentSystem.MaterialDefinition>();
            AssetDatabase.CreateAsset(wood, WoodPath);
        }
        var so = new SerializedObject(wood);
        MB13.Aipocalypse.Editor.Localization.LocalizationKit.Set(so.FindProperty("_displayName"), MB13.Aipocalypse.Core.LocalizationSystem.Loc.ItemsTable, "item.wood");
        so.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(WoodIcon) ?? throw new Exception("no icon " + WoodIcon);
        so.FindProperty("_maxStack").intValue = 20;
        so.FindProperty("_unlimitedStack").boolValue = false;
        so.FindProperty("_weight").floatValue = 1f;
        so.FindProperty("_value").intValue = 2;
        so.FindProperty("_fuelHours").floatValue = 2f;
        so.FindProperty("_prefab").objectReferenceValue = WoodPrefab(wood);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wood);
        AssetDatabase.SaveAssetIfDirty(wood);

        var catalog = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.EquipmentSystem.ItemCatalog>(CatalogPath);
        log.Add($"catalog: {catalog.Collect()} items");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        log.Add($"wood: {wood.FuelHours} h, {wood.Weight} kg, stack {wood.MaxStack}, prefab {wood.Prefab.name}");
        return string.Join("\n", log);
    }

    // Root WorldItem + trigger box (at least 30 cm), World/Model holding copies of the log's meshes.
    static MB13.Aipocalypse.Core.EquipmentSystem.WorldItem WoodPrefab(MB13.Aipocalypse.Core.EquipmentSystem.ItemDefinition wood)
    {
        var root = new GameObject("Wood");
        try
        {
            var world = new GameObject("World");
            world.transform.SetParent(root.transform, false);
            CopyModel(LogModel, world.transform, "Model", Vector3.zero, Vector3.zero, 0.55f);
            var bounds = MeshBounds(root.transform, world.transform);
            // Lying on the ground: the model's lowest point at the root.
            world.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = MeshBounds(root.transform, world.transform);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size + Vector3.one * 0.05f, Vector3.one * 0.3f);
            var item = root.AddComponent<MB13.Aipocalypse.Core.EquipmentSystem.WorldItem>();
            var so = new SerializedObject(item);
            so.FindProperty("_definition").objectReferenceValue = wood;
            so.FindProperty("_worldModel").objectReferenceValue = world;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, WoodPrefabPath).GetComponent<MB13.Aipocalypse.Core.EquipmentSystem.WorldItem>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static Transform CopyModel(string modelPath, Transform parent, string name, Vector3 position, Vector3 euler, float scale)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ?? throw new Exception("missing model " + modelPath);
        var model = new GameObject(name).transform;
        model.SetParent(parent, false);
        model.localPosition = position;
        model.localEulerAngles = euler;
        model.localScale = Vector3.one * scale;
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null)
                continue;
            Transform target = model;
            if (filter.transform != source.transform)
            {
                target = new GameObject(filter.name).transform;
                target.SetParent(model, false);
                Matrix4x4 local = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                target.localPosition = local.GetColumn(3);
                target.localRotation = local.rotation;
                target.localScale = local.lossyScale;
            }
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            target.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        return model;
    }

    // Bounds of every mesh under `of`, in `space`'s local coordinates.
    static Bounds MeshBounds(Transform space, Transform of)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in of.GetComponentsInChildren<MeshFilter>(true))
        {
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                    bounds = new Bounds(local, Vector3.zero);
                else
                    bounds.Encapsulate(local);
                any = true;
            }
        }
        return bounds;
    }

    // ---------------------------------------------------------------- settings, sound, prefabs

    const string SettingsPath = "Assets/_Game/Data/Campfire/CampfireSettings.asset";
    const string ContentDir = "Assets/_Game/Content/Structures/Campfire";
    const string CampfirePrefabPath = ContentDir + "/Campfire.prefab";
    const string PilePrefabPath = ContentDir + "/WoodPile.prefab";
    const string HL = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/";
    const string LV = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Prefabs/";
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";
    const string GaFire = "Assets/_StoreAssets/VFX/GabrielAguiarProductions/Prefabs/StylizedFire/vfx_Fire01_v1.prefab";
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string CampfireAudioDir = AudioDir + "/Campfire";
    const string Gm = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string PhWood = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Environment Sounds/Wood/";
    const string PhMisc = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Environment Sounds/Miscellaneous/";
    // The GA flame's HDR colours: its own (63, 9.3, 3.3) washed out to pale peach under the night grade; dimmer and warmer reads orange.
    static readonly Vector4 FlameColor = new(9f, 2.9f, 0.45f, 1f);
    static readonly Vector4 EmberColor = new(10f, 3.3f, 0.5f, 1f);
    static readonly Color LightColor = new(1f, 0.62f, 0.32f);
    // High enough that the yard is lit evenly instead of a hot spot (tuned from 22:00 captures, 2026-11-28).
    const float FillHeight = 6.5f;

    public static string All() => string.Join("\n", Settings(), Audio(), Prefabs());

    public static string Settings()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath)!);
        var settings = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>(SettingsPath);
        if (settings != null)
            return "settings: kept as tuned";
        settings = ScriptableObject.CreateInstance<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssetIfDirty(settings);
        return "settings: created " + SettingsPath;
    }

    // Library Campfire: the crackle loop (3D) and the light / add-wood / put-out one-shots (user approved the sounds, 2026-11-28).
    public static string Audio()
    {
        var log = new List<string>();
        System.IO.Directory.CreateDirectory(CampfireAudioDir);
        var library = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioAsset>(AudioDir + "/Campfire.asset");
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<Ami.BroAudio.Data.AudioAsset>();
            AssetDatabase.CreateAsset(library, AudioDir + "/Campfire.asset");
            var lso = new SerializedObject(library);
            lso.FindProperty("<AssetName>k__BackingField").stringValue = "Campfire";
            lso.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(AudioDir + "/Campfire.asset");
            lso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("library Campfire");
        }
        var near = Spatial(CampfireAudioDir + "/Campfire_Spatial.asset", 2f, 28f);
        Entity(library, "FireCrackle", true, 0.9f, 1f, 0.05f, near, Gm + "Explosion_Fire_Gas/fire_burning_flames_crackle_loop_01.wav");
        Entity(library, "FireIgnite", false, 0.8f, 1f, 0.05f, near, Gm + "Explosion_Fire_Gas/fire_start_light_flame_ignite_01.wav");
        // A log landing on the embers: PlaceHolder Inc wood hits, picked by their waveform (one hit, a short body), not by ear.
        Entity(library, "FireAddWood", false, 0.7f, 1f, 0.08f, near,
            PhWood + "Wood 11-1.wav", PhWood + "Wood 11-2.wav", PhWood + "Wood 11-3.wav", PhWood + "Wood 11-4.wav");
        // A dying fire: sizzles, not listened to by the agent.
        Entity(library, "FireSputter", false, 0.45f, 1f, 0.1f, near,
            PhMisc + "Sizzle 1-2.wav", PhMisc + "Sizzle 1-3.wav", PhMisc + "Sizzle 1-4.wav", PhMisc + "Sizzle 1-7.wav");
        Entity(library, "FirePutOut", false, 0.8f, 0.9f, 0.05f, near,
            Gm + "Explosion_Fire_Gas/fuse_stop_burn_out_bomb_dynamite_01.wav", Gm + "Explosion_Fire_Gas/fuse_stop_burn_out_bomb_dynamite_02.wav");
        AssetDatabase.SaveAssetIfDirty(library);
        log.Add("entities: FireCrackle (loop), FireIgnite, FireAddWood, FireSputter, FirePutOut");
        return string.Join("\n", log);
    }

    static Ami.BroAudio.Data.SpatialSetting Spatial(string path, float min, float max)
    {
        var spatial = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.SpatialSetting>(path);
        if (spatial == null)
        {
            spatial = ScriptableObject.CreateInstance<Ami.BroAudio.Data.SpatialSetting>();
            AssetDatabase.CreateAsset(spatial, path);
        }
        spatial.MinDistance = min;
        spatial.MaxDistance = max;
        spatial.RolloffMode = AudioRolloffMode.Logarithmic;
        spatial.DopplerLevel = 0f;
        EditorUtility.SetDirty(spatial);
        AssetDatabase.SaveAssetIfDirty(spatial);
        return spatial;
    }

    static Ami.BroAudio.Data.AudioEntity Entity(Ami.BroAudio.Data.AudioAsset library, string name, bool loop, float volume, float pitch, float pitchRandom,
        Ami.BroAudio.Data.SpatialSetting spatial, params string[] clips)
    {
        string path = $"{CampfireAudioDir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioEntity>(path);
        if (entity == null)
        {
            entity = Ami.BroAudio.Data.AudioEntity.CreateNewInstance(library, name, Ami.BroAudio.BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)Ami.BroAudio.BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = loop;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = pitch;
        so.FindProperty("MulticlipsPlayMode").intValue = clips.Length > 1 ? 3 : 0;   // Shuffle : Single
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
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

    static Ami.BroAudio.SoundID Sound(string name) =>
        new(AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioEntity>($"{CampfireAudioDir}/{name}.asset") ?? throw new Exception("run Audio first: no " + name));

    public static string Prefabs() => string.Join("\n", CampfirePrefab(), PilePrefab());

    // Writes the light / flame tuning into the existing settings asset and campfire prefab without rebuilding it (a rebuild
    // gives the prefab's objects new file ids, which drops the scene instance's SaveId value).
    public static string Tune()
    {
        var settings = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>(SettingsPath) ?? throw new Exception("run Settings first");
        var defaults = ScriptableObject.CreateInstance<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>();
        settings.CoreIntensity = defaults.CoreIntensity;
        settings.CoreRange = defaults.CoreRange;
        settings.FillIntensity = defaults.FillIntensity;
        settings.FillRange = defaults.FillRange;
        UnityEngine.Object.DestroyImmediate(defaults);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);

        GameObject root = PrefabUtility.LoadPrefabContents(CampfirePrefabPath);
        try
        {
            Transform core = root.transform.Find("Core Light"), fill = root.transform.Find("Fill Light");
            fill.localPosition = new Vector3(0f, FillHeight, 0f);
            core.GetComponent<Light>().range = settings.CoreRange;
            core.GetComponent<Light>().intensity = settings.CoreIntensity;
            fill.GetComponent<Light>().range = settings.FillRange;
            fill.GetComponent<Light>().intensity = settings.FillIntensity;
            var vfx = root.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            vfx.SetVector4("FlamesColor", FlameColor);
            vfx.SetVector4("FlameStaticColor", FlameColor);
            vfx.SetVector4("AmbersColor", EmberColor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(vfx);
            PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath, out bool saved);
            return $"settings core {settings.CoreIntensity}/{settings.CoreRange} m, fill {settings.FillIntensity}/{settings.FillRange} m at {FillHeight} m; flame {FlameColor}; prefab saved {saved}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Adds the logs in the pit and the fill light's shadows to the existing prefab, in place (keeps the scene's SaveId), and
    // writes the settings' new fields (FillFlicker, FuelBarRange) into the asset.
    public static string Pit()
    {
        var settings = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>(SettingsPath) ?? throw new Exception("run Settings first");
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        GameObject root = PrefabUtility.LoadPrefabContents(CampfirePrefabPath);
        try
        {
            Transform old = root.transform.Find("Logs");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            int logs = AddLogs(root);
            ShadowFill(root.transform.Find("Fill Light").GetComponent<Light>());
            PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath, out bool saved);
            return $"{logs} logs in the pit, fill light soft shadows; settings fill flicker {settings.FillFlicker}, fuel bar range {settings.FuelBarRange} m; prefab saved {saved}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The sounds (new add-wood clips, the sputter), then the wide catch for thrown wood and the sputter on the existing prefab,
    // in place (keeps the scene's SaveId).
    public static string Cues()
    {
        string audio = Audio();
        GameObject root = PrefabUtility.LoadPrefabContents(CampfirePrefabPath);
        try
        {
            AddCatch(root);
            var so = new SerializedObject(root.GetComponent<MB13.Aipocalypse.Core.CampfireSystem.Campfire>());
            SetSound(so, "_sputter", "FireSputter");
            SetSound(so, "_addWood", "FireAddWood");
            PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath, out bool saved);
            return audio + $"\ncatch box {CatchRadius * 2f} × {CatchHeight} m, sputter; prefab saved {saved}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A low, wide trigger on the root that catches thrown wood just before it lands (throws aimed at the pit from ~4.5 m land
    // ~1.4 m short of it). Low, so the interact ray reaches the fire from only a little farther (~3.9 m instead of ~3.5 m).
    // Not on Ignore Raycast: that layer ignores collisions with thrown items (Item) and Default, so it would catch nothing.
    static void AddCatch(GameObject root)
    {
        Transform child = root.transform.Find("Catch");
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        if (root.TryGetComponent(out Rigidbody body))
            UnityEngine.Object.DestroyImmediate(body);
        if (!root.TryGetComponent(out BoxCollider box))
            box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, CatchHeight * 0.5f, 0f);
        box.size = new Vector3(CatchRadius * 2f, CatchHeight, CatchRadius * 2f);
    }

    const float CatchRadius = 1.7f;
    const float CatchHeight = 0.8f;

    // Four HIGHLANDS logs laid as a star, inner ends raised on the embers; the Campfire shows one per quarter of fuel left.
    static int AddLogs(GameObject root)
    {
        var logs = new GameObject("Logs").transform;
        logs.SetParent(root.transform, false);
        var list = new GameObject[LogCount];
        for (int i = 0; i < LogCount; i++)
        {
            float angle = (i / (float)LogCount * 360f + 20f) * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * LogRadius + Vector3.up * 0.03f;
            GameObject log = Instance(LogModel, logs, "Log " + (i + 1), at, 0f, LogScale);
            log.transform.localRotation = Quaternion.LookRotation(-new Vector3(at.x, 0f, at.z)) * Quaternion.Euler(-LogTilt, 0f, 0f);
            list[i] = log;
        }
        var so = new SerializedObject(root.GetComponent<MB13.Aipocalypse.Core.CampfireSystem.Campfire>());
        SerializedProperty prop = so.FindProperty("_logs");
        prop.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return list.Length;
    }

    // The fill hangs high and reaches 42 m: unshadowed it lights the inside of the house through its walls.
    static void ShadowFill(Light fill)
    {
        fill.shadows = LightShadows.Soft;
        fill.shadowNearPlane = 0.3f;
        var data = fill.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>() ?? fill.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        var so = new SerializedObject(data);   // the property setter refuses outside Play
        so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    const int LogCount = 4;
    const float LogRadius = 0.42f;
    const float LogScale = 0.8f;
    const float LogTilt = 14f;

    // Root: Campfire, SafeLight, a solid capsule (nobody walks through it), a trigger that catches thrown wood, a NavmeshCut.
    // Children: the HIGHLANDS pit, a stone ring, the GA flame (warmed colours) under Flame, a core and a fill light.
    static string CampfirePrefab()
    {
        System.IO.Directory.CreateDirectory(ContentDir);
        var settings = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>(SettingsPath) ?? throw new Exception("run Settings first");
        var root = new GameObject("Campfire");
        try
        {
            Instance(HL + "Camp/Bonfire.prefab", root.transform, "Pit", new Vector3(0f, -0.02f, 0f), 35f, 1.3f);
            var stones = new GameObject("Stones").transform;
            stones.SetParent(root.transform, false);
            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * Mathf.PI * 2f + R(-0.12f, 0.12f);
                float radius = R(0.85f, 0.95f);
                Instance(KN + "Rocks/SmallStone_" + (1 + rng.Next(4)) + ".prefab", stones, "Stone",
                    new Vector3(Mathf.Cos(angle) * radius, -0.08f, Mathf.Sin(angle) * radius), R(0f, 360f), R(0.55f, 0.75f));
            }

            var flameRoot = new GameObject("Flame").transform;
            flameRoot.SetParent(root.transform, false);
            flameRoot.localPosition = new Vector3(0f, 0.05f, 0f);
            GameObject fire = Instance(GaFire, flameRoot, "Fire", Vector3.zero, 0f, 1f);
            var vfx = fire.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            vfx.SetVector4("FlamesColor", FlameColor);
            vfx.SetVector4("FlameStaticColor", FlameColor);
            vfx.SetVector4("AmbersColor", EmberColor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(vfx);

            Light core = PointLight(root.transform, "Core Light", 1f, settings.CoreIntensity, settings.CoreRange);
            Light fill = PointLight(root.transform, "Fill Light", FillHeight, settings.FillIntensity, settings.FillRange);
            ShadowFill(fill);

            var safe = root.AddComponent<MB13.Aipocalypse.Core.PlayerSystem.Needs.SafeLight>();
            var safeSo = new SerializedObject(safe);
            safeSo.FindProperty("_radius").floatValue = settings.SafeRadius;
            safeSo.ApplyModifiedPropertiesWithoutUndo();

            var solid = root.AddComponent<CapsuleCollider>();
            solid.center = new Vector3(0f, 0.5f, 0f);
            solid.radius = 0.85f;
            solid.height = 1f;
            var feed = root.AddComponent<SphereCollider>();
            feed.isTrigger = true;
            feed.center = new Vector3(0f, 0.6f, 0f);
            feed.radius = 1.05f;

            var cut = root.AddComponent<Pathfinding.NavmeshCut>();
            var cutSo = new SerializedObject(cut);
            cutSo.FindProperty("type").intValue = 1;            // circle
            cutSo.FindProperty("circleRadius").floatValue = 1f;
            cutSo.FindProperty("circleResolution").intValue = 8;
            cutSo.FindProperty("height").floatValue = 2f;
            cutSo.FindProperty("center").vector3Value = new Vector3(0f, 0.5f, 0f);
            cutSo.FindProperty("useRotationAndScale").boolValue = false;
            cutSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<MB13.Aipocalypse.Core.SaveSystem.SaveId>();   // the save finds it (empty on the asset)
            var campfire = root.AddComponent<MB13.Aipocalypse.Core.CampfireSystem.Campfire>();
            var so = new SerializedObject(campfire);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_safeLight").objectReferenceValue = safe;
            so.FindProperty("_core").objectReferenceValue = core;
            so.FindProperty("_fill").objectReferenceValue = fill;
            so.FindProperty("_flame").objectReferenceValue = vfx;
            so.FindProperty("_flameRoot").objectReferenceValue = flameRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetSound(so, "_crackle", "FireCrackle");
            SetSound(so, "_ignite", "FireIgnite");
            SetSound(so, "_addWood", "FireAddWood");
            SetSound(so, "_putOut", "FirePutOut");
            SetSound(so, "_sputter", "FireSputter");
            AddLogs(root);
            AddCatch(root);

            PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath);
            return $"prefab {CampfirePrefabPath}: pit, {stones.childCount} stones, GA fire, core {core.range} m / fill {fill.range} m, safe {settings.SafeRadius} m";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void SetSound(SerializedObject so, string field, string entity)
    {
        so.Update();
        var id = Sound(entity);
        so.FindProperty(field + "._entity").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioEntity>($"{CampfireAudioDir}/{entity}.asset");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Light PointLight(Transform parent, string name, float height, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = LightColor;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        return light;
    }

    // Root: WoodPile with a trigger box over the stack; Model: the Lakefell log stack (hidden while empty).
    static string PilePrefab()
    {
        var settings = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.CampfireSystem.CampfireSettings>(SettingsPath) ?? throw new Exception("run Settings first");
        var wood = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.EquipmentSystem.MaterialDefinition>(WoodPath) ?? throw new Exception("run Wood first");
        var root = new GameObject("WoodPile");
        try
        {
            GameObject model = Instance(LV + "Environment/Log_2.prefab", root.transform, "Model", Vector3.zero, 0f, 1f);
            Bounds bounds = MeshBounds(root.transform, model.transform);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size + Vector3.one * 0.1f, Vector3.one * 0.4f);
            root.AddComponent<MB13.Aipocalypse.Core.SaveSystem.SaveId>();
            var pile = root.AddComponent<MB13.Aipocalypse.Core.CampfireSystem.WoodPile>();
            var so = new SerializedObject(pile);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_wood").objectReferenceValue = wood;
            so.FindProperty("_model").objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PilePrefabPath);
            return $"prefab {PilePrefabPath}: box {box.size}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static GameObject Instance(string path, Transform parent, string name, Vector3 position, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception("missing prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    // ---------------------------------------------------------------- HUD

    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string StatBarPrefab = "Assets/_Game/_Core/UI/Prefabs/Widgets/StatBar.prefab";
    const string FuelStylePath = "Assets/_Game/Data/UI/CampfireFuel.asset";
    const string FireIconPath = "Assets/Synty/InterfaceApocalypseHUD/Sprites/Icons_Map/ICON_Apocalpyse_Map_Fire_01.png";

    // HUD.prefab → SafeFrame/BottomLeft/FireBar: the home fire's fuel bar (CampfireFuelHud), above the dog's bar.
    public static string Hud()
    {
        var stamina = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.UI.StatBarStyle>("Assets/_Game/Data/UI/StaminaBarStyle.asset") ?? throw new Exception("no stamina style");
        var style = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.UI.StatBarStyle>(FuelStylePath);
        if (style == null)
        {
            style = UnityEngine.Object.Instantiate(stamina);
            AssetDatabase.CreateAsset(style, FuelStylePath);
            style.FillColor = new Color(1f, 0.56f, 0.16f, 1f);
            style.WarningColor = new Color(0.9f, 0.25f, 0.15f, 1f);
        }
        style.IconSprite = AssetDatabase.LoadAllAssetsAtPath(FireIconPath).OfType<Sprite>().FirstOrDefault() ?? throw new Exception("no sprite in " + FireIconPath);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform corner = root.transform.Find("SafeFrame/BottomLeft") ?? throw new Exception("no SafeFrame/BottomLeft");
            Transform old = corner.Find("FireBar");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("FireBar", typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(corner, false);
            node.anchorMin = node.anchorMax = node.pivot = Vector2.zero;
            // Above the dog's bar (222 px, DogSetup.DogBar), the needs' rings and the effects row.
            node.anchoredPosition = new Vector2(0f, 262f);
            node.sizeDelta = new Vector2(260f, 34f);
            node.gameObject.AddComponent<Canvas>();
            var group = node.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var barObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StatBarPrefab), node);
            barObject.name = "Bar";
            var barRect = (RectTransform)barObject.transform;
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0f, 0.5f);
            barRect.anchoredPosition = Vector2.zero;
            var bar = barObject.GetComponent<MB13.Aipocalypse.Core.UI.StatBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("_style").objectReferenceValue = style;
            so.ApplyModifiedPropertiesWithoutUndo();
            bar.ApplyStyle();
            var hud = node.gameObject.AddComponent<MB13.Aipocalypse.Core.CampfireSystem.CampfireFuelHud>();
            hud.Setup(group, bar);
            EditorUtility.SetDirty(hud);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? "HUD.prefab: SafeFrame/BottomLeft/FireBar (" + FuelStylePath + ")" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- scene

    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    static readonly Vector2 HomeBase = new(267f, 282f);
    const float HomeYaw = 50f;
    // User 2026-11-28: the fire moves to the middle of the yard (was Yard(3.5, 6.5), off to the right). Same in WorldMapLayout.
    static readonly Vector2 FireAt = Yard(0f, 6.5f);

    static Vector2 Rot(Vector2 v, float yawDeg)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(a) + v.y * Mathf.Sin(a), -v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
    }

    static Vector2 Yard(float x, float z) => HomeBase + Rot(new Vector2(x, z), HomeYaw);

    // Replaces Home Base/Campfire in the open WorldMap with the campfire and wood pile prefabs at the yard's middle, with the
    // benches and seat logs around it; adds CampfireInstaller and re-collects the scene lists. Refuses a dirty scene.
    public static string Scene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            return $"open {ScenePath} first (active: {scene.path})";
        if (scene.isDirty)
            return "WorldMap has unsaved changes: save or revert them first";
        var terrain = Terrain.activeTerrain ?? throw new Exception("no terrain");
        float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;

        Transform node = GameObject.Find("Environment/Landmarks/Home Base/Campfire")?.transform ?? throw new Exception("no Home Base/Campfire");
        for (int i = node.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(node.GetChild(i).gameObject);

        GameObject Put(string path, Vector2 at, float yaw, float yOffset = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception("missing prefab " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node);
            go.transform.SetPositionAndRotation(new Vector3(at.x, Ground(at) + yOffset, at.y), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        GameObject fire = Put(CampfirePrefabPath, FireAt, 0f);
        foreach (float angle in new[] { HomeYaw + 160f, HomeYaw + 250f, HomeYaw + 355f })
        {
            float a = angle * Mathf.Deg2Rad;
            Put(LV + "Environment/Bench_1.prefab", FireAt + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 2.5f, angle + 90f);
        }
        Put(HL + "Camp/Log_1.prefab", FireAt + Rot(new Vector2(1.98f, 0.28f), HomeYaw), 40f);
        Put(HL + "Camp/Log_3.prefab", FireAt + Rot(new Vector2(1.98f, 0.71f), HomeYaw), 200f);
        GameObject pile = Put(PilePrefabPath, FireAt + Rot(new Vector2(3.9f, 0.2f), HomeYaw), HomeYaw + 30f);

        foreach (var id in node.GetComponentsInChildren<MB13.Aipocalypse.Core.SaveSystem.SaveId>())
            if (!id.HasId)
                id.Regenerate();
        var scope = UnityEngine.Object.FindAnyObjectByType<MB13.Aipocalypse.Core.Bootstrap.GameLifetimeScope>() ?? throw new Exception("no GameLifetimeScope");
        var installer = scope.GetComponent<MB13.Aipocalypse.Core.CampfireSystem.CampfireInstaller>()
            ?? Undo.AddComponent<MB13.Aipocalypse.Core.CampfireSystem.CampfireInstaller>(scope.gameObject);
        installer.CollectScene();
        scope.GetComponent<MB13.Aipocalypse.Core.PlayerSystem.Needs.NeedsInstaller>()?.CollectLights();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return $"campfire at {fire.transform.position:F1}, pile at {pile.transform.position:F1}; installer on {scope.name}; scene saved";
    }

    // ---------------------------------------------------------------- grades

    // A copy of PP_Night with its post exposure moved by `ev`; an existing copy is left as tuned.
    static VolumeProfile CloneGrade(string path, float ev, List<string> log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (existing != null)
            return existing;
        if (!AssetDatabase.CopyAsset(NightGrade, path))
            throw new Exception("couldn't copy " + NightGrade);
        var grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!grade.TryGet(out ColorAdjustments adjust))
            throw new Exception(NightGrade + " has no Color Adjustments");
        adjust.postExposure.Override(adjust.postExposure.value + ev);
        EditorUtility.SetDirty(adjust);
        EditorUtility.SetDirty(grade);
        AssetDatabase.SaveAssetIfDirty(grade);
        log.Add($"{grade.name}: post exposure {adjust.postExposure.value:0.##}");
        return grade;
    }
}
