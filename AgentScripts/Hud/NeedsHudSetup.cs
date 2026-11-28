using System;
using System.Collections.Generic;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the needs' HUD (docs/technicals/NEEDS.md, UI.md): the body sounds (2D entities in the Player library, found in the
// imported packs), Data/UI/<Need>RingStyle assets and the Needs row in HUD.prefab (SafeFrame/BottomLeft above Vitals, with
// Effects and DogBar moved up by the row; a CanvasGroup on Vitals for sanity's flicker), and sanity's sounds on
// PlayerNeedsSettings.SanityEffects (drone, whispers, the low-health heartbeat), the overloaded sound on InventoryStyle. Safe to re-run: sounds and styles are updated in place, the row is rebuilt, the
// others move only once.
// Run: unity command script run --file AgentScripts/Hud/NeedsHudSetup.cs --entry NeedsHudSetup.Build
// Then the import presets: AgentScripts/Audio/AudioImportKit.cs Apply ["used:Assets/_Game/Data/Audio", false, "Assets/_Game/Settings/Audio"].
public static class NeedsHudSetup
{
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string UiDir = "Assets/_Game/Data/UI";
    const string PlayerAudioDir = "Assets/_Game/Data/Audio/Player";
    const string Gamemaster = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string Synty = "Assets/Synty/InterfaceApocalypseHUD/Sprites/";
    const string RingSprite = Synty + "HUD/SPR_HUD_Apocalypse_Ring_Medium_01_Clean.png";
    const string HungerIcon = Synty + "Icons_Status/ICON_Apocalpyse_Status_Hunger_01.png";
    const string ThirstIcon = Synty + "Icons_Status/ICON_Apocalpyse_Status_Thirst_01.png";
    const string SanityIcon = "Assets/_StoreAssets/UI/Layer Lab/2D Icons-SimplePictoPack/Icons/PNG/512/Pictoicon_brain.png";
    const string NodeName = "Needs";
    const float RingSize = 56f;
    const float RingGap = 10f;
    const float RowY = 96f;
    // Effects and DogBar sit this much higher than before the row existed (Effects 130 → 162, DogBar 190 → 222).
    const float Lift = 32f;

    public static string Build()
    {
        var log = new List<string> { Audio() };
        log.Add(SanityAudio());
        log.Add(LoadAudio());
        log.Add(Styles());
        log.Add(Hud());
        return string.Join("\n", log);
    }

    // Body sounds from the imported packs; a need with no match gets none (no stomach growl in any pack).
    public static string Audio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>("Assets/_Game/Data/Audio/Player.asset") ?? throw new Exception("missing Player library");
        var coughs = new List<string>();
        for (int i = 1; i <= 15; i++)
        {
            string path = $"{Gamemaster}Voice/Human Male A/voice_male_coughing_{i:00}.wav";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
                coughs.Add(path);
        }
        if (coughs.Count == 0)
            return "audio: no voice_male_coughing clips found, Thirst stays silent";
        Entity(library, "Thirst_Cough", 0.7f, coughs, false);
        Save(library);
        return $"audio: Thirst_Cough ({coughs.Count} clips, random) in the Player library";
    }

    // Sanity: a low drone loop and ghostly whispers (Gamemaster, not listened to by the agent: check by ear), the heartbeat
    // the low-health warning already uses.
    public static string SanityAudio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>("Assets/_Game/Data/Audio/Player.asset") ?? throw new Exception("missing Player library");
        string drone = Gamemaster + "Cinematic Sounds/cinematic_LowDrone1.wav";
        var whispers = new List<string>();
        for (int i = 1; i <= 12; i++)
        {
            string path = $"{Gamemaster}Voice/Ghost/ghost_witch_voice_hiss_{i:00}.wav";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
                whispers.Add(path);
        }
        Entity(library, "Sanity_Drone", 0.6f, new List<string> { drone }, true);
        Entity(library, "Sanity_Whisper", 0.5f, whispers, false);
        Save(library);

        var settings = AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>("Assets/_Game/Data/Player/PlayerNeedsSettings.asset")
            ?? throw new Exception("missing PlayerNeedsSettings (run NeedsSetup.Settings)");
        settings.SanityEffects.Drone = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>($"{PlayerAudioDir}/Sanity_Drone.asset"));
        settings.SanityEffects.Whisper = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>($"{PlayerAudioDir}/Sanity_Whisper.asset"));
        var heartbeat = AssetDatabase.LoadAssetAtPath<AudioEntity>($"{PlayerAudioDir}/Heartbeat.asset");
        if (heartbeat != null)
            settings.SanityEffects.Heartbeat = new SoundID(heartbeat);
        Save(settings);
        return $"audio: Sanity_Drone (loop), Sanity_Whisper ({whispers.Count} clips), heartbeat {(heartbeat != null ? "reused" : "missing")}";
    }

    // Stepping out of every safe light at night: a cold gust of wind (PlaceHolder Inc Environment Sounds, picked by name and
    // length, not listened to by the agent: check by ear), on SanityEffects.LeftLightSound.
    public static string LeftLightAudio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>("Assets/_Game/Data/Audio/Player.asset") ?? throw new Exception("missing Player library");
        const string wind = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Environment Sounds/Wind/";
        var gusts = new List<string> { wind + "Wind Gust 2-5.wav", wind + "Wind Gust 1-5.wav", wind + "Wind Gust 2-1.wav" };
        Entity(library, "Sanity_LeftLight", 0.7f, gusts, false);
        Save(library);
        var settings = AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>("Assets/_Game/Data/Player/PlayerNeedsSettings.asset")
            ?? throw new Exception("missing PlayerNeedsSettings (run NeedsSetup.Settings)");
        settings.SanityEffects.LeftLightSound = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>($"{PlayerAudioDir}/Sanity_LeftLight.asset"));
        Save(settings);
        return $"audio: Sanity_LeftLight ({gusts.Count} wind gusts, random) on SanityEffects.LeftLightSound";
    }

    // Carried weight reaching Max: a heavy shuffle of gear (Gamemaster foley), on InventoryStyle.OverloadedSound for the
    // pickup feed's 'Too heavy' line.
    public static string LoadAudio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>("Assets/_Game/Data/Audio/Player.asset") ?? throw new Exception("missing Player library");
        var clips = new List<string>();
        for (int i = 1; i <= 8; i++)
        {
            string path = $"{Gamemaster}Foley/foley_soldier_gear_equipment_metal_cloth_heavy_movement_strong_{i:00}.wav";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
                clips.Add(path);
        }
        if (clips.Count == 0)
            return "audio: no heavy_movement_strong clips found, overloading stays silent";
        Entity(library, "Load_Overloaded", 0.8f, clips, false);
        Save(library);
        var style = AssetDatabase.LoadAssetAtPath<InventoryStyle>("Assets/_Game/Data/UI/InventoryStyle.asset") ?? throw new Exception("missing InventoryStyle");
        style.OverloadedSound = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>($"{PlayerAudioDir}/Load_Overloaded.asset"));
        Save(style);
        return $"audio: Load_Overloaded ({clips.Count} clips, random) on InventoryStyle.OverloadedSound";
    }

    public static string Styles()
    {
        var ring = Sprite(RingSprite);
        var cough = AssetDatabase.LoadAssetAtPath<AudioEntity>($"{PlayerAudioDir}/Thirst_Cough.asset");
        Style("HungerRingStyle", Sprite(HungerIcon), ring, default);
        Style("ThirstRingStyle", Sprite(ThirstIcon), ring, cough != null ? new SoundID(cough) : default);
        Style("SanityRingStyle", Sprite(SanityIcon), ring, default);
        return "styles: Hunger / Thirst / SanityRingStyle";
    }

    public static string Hud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform corner = root.transform.Find("SafeFrame/BottomLeft") ?? throw new Exception("no SafeFrame/BottomLeft in HUD.prefab");
            Transform old = corner.Find(NodeName);
            bool first = old == null;
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform row = UiObject(NodeName, corner);
            row.anchorMin = row.anchorMax = row.pivot = Vector2.zero;
            row.anchoredPosition = new Vector2(0f, RowY);
            row.sizeDelta = new Vector2(3f * RingSize + 2f * RingGap, RingSize);
            // Own canvas: the rings change alpha every frame while fading.
            row.gameObject.AddComponent<Canvas>();
            NeedRing hunger = Ring("Hunger", row, 0, "HungerRingStyle");
            NeedRing thirst = Ring("Thirst", row, 1, "ThirstRingStyle");
            NeedRing sanity = Ring("Sanity", row, 2, "SanityRingStyle");

            var hud = row.gameObject.AddComponent<PlayerNeedsHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_hunger").objectReferenceValue = hunger;
            so.FindProperty("_thirst").objectReferenceValue = thirst;
            so.FindProperty("_sanity").objectReferenceValue = sanity;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Sanity's flicker fades the health and stamina bars through this.
            Transform vitals = corner.Find("Vitals");
            if (vitals != null && vitals.TryGetComponent(out PlayerVitalsHud vitalsHud))
            {
                if (!vitals.TryGetComponent(out CanvasGroup group))
                    group = vitals.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                var vitalsSo = new SerializedObject(vitalsHud);
                vitalsSo.FindProperty("_group").objectReferenceValue = group;
                vitalsSo.ApplyModifiedPropertiesWithoutUndo();
            }

            string moved = "";
            if (first)
                moved = Raise(corner, "Effects") + Raise(corner, "DogBar");
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? $"HUD.prefab: {NodeName} row at y {RowY}{moved}" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string Raise(Transform corner, string name)
    {
        var rect = corner.Find(name) as RectTransform;
        if (rect == null)
            return $", no {name}";
        rect.anchoredPosition += new Vector2(0f, Lift);
        return $", {name} → y {rect.anchoredPosition.y}";
    }

    static NeedRing Ring(string name, RectTransform row, int index, string styleName)
    {
        RectTransform node = UiObject(name, row);
        node.anchorMin = node.anchorMax = node.pivot = Vector2.zero;
        node.anchoredPosition = new Vector2(index * (RingSize + RingGap), 0f);
        node.sizeDelta = new Vector2(RingSize, RingSize);
        var group = node.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform body = Stretch(UiObject("Body", node));
        body.pivot = new Vector2(0.5f, 0.5f);
        Image track = Stretch(UiObject("Track", body)).gameObject.AddComponent<Image>();
        Image fill = Stretch(UiObject("Fill", body)).gameObject.AddComponent<Image>();
        RectTransform iconRect = UiObject("Icon", body);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(RingSize * 0.52f, RingSize * 0.52f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        foreach (Image image in new[] { track, fill, icon })
            image.raycastTarget = false;

        var ring = node.gameObject.AddComponent<NeedRing>();
        var so = new SerializedObject(ring);
        so.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NeedRingStyle>($"{UiDir}/{styleName}.asset");
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_body").objectReferenceValue = body;
        so.FindProperty("_track").objectReferenceValue = track;
        so.FindProperty("_fill").objectReferenceValue = fill;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.ApplyModifiedPropertiesWithoutUndo();
        ring.ApplyStyle();
        return ring;
    }

    static void Style(string name, Sprite icon, Sprite ring, SoundID sound)
    {
        string path = $"{UiDir}/{name}.asset";
        var style = AssetDatabase.LoadAssetAtPath<NeedRingStyle>(path);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<NeedRingStyle>();
            AssetDatabase.CreateAsset(style, path);
        }
        style.Icon = icon;
        style.RingSprite = ring;
        // The empty part: the same ring, dim.
        style.TrackSprite = ring;
        style.TrackColor = new Color(1f, 1f, 1f, 0.22f);
        style.TierSound = sound;
        Save(style);
    }

    // A 2D SFX entity (a one-shot picking one of its clips at random each play, or a loop).
    static void Entity(AudioAsset library, string name, float volume, List<string> clipPaths, bool loop)
    {
        string path = $"{PlayerAudioDir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = loop;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = 1f;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)MulticlipsPlayMode.Random;
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = 0f;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = 0;
        SerializedProperty clips = so.FindProperty("Clips");
        clips.arraySize = clipPaths.Count;
        for (int i = 0; i < clipPaths.Count; i++)
        {
            SerializedProperty element = clips.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPaths[i]);
            element.FindPropertyRelative("Volume").floatValue = 1f;
            element.FindPropertyRelative("Weight").intValue = 1;
        }
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
    }

    static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new Exception("missing sprite " + path);

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static RectTransform UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
