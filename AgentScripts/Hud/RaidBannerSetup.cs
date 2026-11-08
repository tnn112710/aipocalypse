using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.UI;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Builds the raid banner: its Raid_Start / Raid_Victory sounds (2D BroAudio entities in the Combat library, with the other
// HUD stingers), Data/UI/RaidBannerStyle.asset and the RaidBanner node in HUD.prefab (RaidBannerHud + Title / Subtitle).
// Safe to re-run: sounds and style are updated in place, the HUD node is rebuilt.
// Run: unity command script run --file AgentScripts/Hud/RaidBannerSetup.cs --entry RaidBannerSetup.Build
public static class RaidBannerSetup
{
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string CombatDir = AudioDir + "/Combat";
    const string Interface = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Interface & Item Sounds Lite - Version 2/Miscellaneous/";
    const string StylePath = "Assets/_Game/Data/UI/RaidBannerStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string NodeName = "RaidBanner";
    const string FontPath = "Assets/Synty/InterfaceApocalypseHUD/Fonts/SairaCondensed/Font_Apocalypse_Bold.asset";

    public static string Build()
    {
        var log = new List<string> { Audio() };
        log.Add(Banner());
        return string.Join("\n", log);
    }

    public static string Audio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(AudioDir + "/Combat.asset") ?? throw new Exception("missing Combat library");
        Entity(library, "Raid_Start", 0.9f, Interface + "Gong Hit 1-1.wav");
        Entity(library, "Raid_Victory", 0.8f, Interface + "Victory Jingle 1-1.wav");
        Save(library);
        return "audio: Raid_Start (Gong Hit), Raid_Victory (Victory Jingle) in the Combat library";
    }

    // A 2D one-shot SFX entity (no spatial setting: the banner sounds at the listener).
    static AudioEntity Entity(AudioAsset library, string name, float volume, string clipPath)
    {
        string path = $"{CombatDir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = 1f;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)MulticlipsPlayMode.Single;
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = 0f;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = 0;
        var clips = so.FindProperty("Clips");
        clips.arraySize = 1;
        SerializedProperty element = clips.GetArrayElementAtIndex(0);
        element.FindPropertyRelative("AudioClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) ?? throw new Exception("missing clip " + clipPath);
        element.FindPropertyRelative("Volume").floatValue = 1f;
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        return entity;
    }

    // The words keep the style's defaults (only characters the font's atlas already has: see RaidBannerStyle.Font).
    public static string Banner()
    {
        var style = AssetDatabase.LoadAssetAtPath<RaidBannerStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<RaidBannerStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }
        style.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing font " + FontPath);
        style.StartSound = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>(CombatDir + "/Raid_Start.asset"));
        style.VictorySound = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>(CombatDir + "/Raid_Victory.asset"));
        Save(style);

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find(NodeName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            // Upper third of the screen, over the world but under the death screen (placed before it).
            RectTransform node = UiObject(NodeName, root.transform);
            node.anchorMin = new Vector2(0f, 0.62f);
            node.anchorMax = new Vector2(1f, 0.82f);
            node.offsetMin = node.offsetMax = Vector2.zero;
            DeathScreenHud deathScreen = root.GetComponentInChildren<DeathScreenHud>(true);
            Transform death = deathScreen != null ? deathScreen.transform : null;
            while (death != null && death.parent != root.transform)
                death = death.parent;
            if (death != null)
                node.SetSiblingIndex(death.GetSiblingIndex());
            var group = node.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI title = Text("Title", node, style.Font, 96f, new Vector2(0f, 0.38f), new Vector2(1f, 1f));
            title.characterSpacing = 6f;
            TextMeshProUGUI subtitle = Text("Subtitle", node, style.Font, 36f, new Vector2(0f, 0f), new Vector2(1f, 0.38f));
            subtitle.color = new Color(1f, 1f, 1f, 0.85f);

            var hud = node.gameObject.AddComponent<RaidBannerHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_subtitle").objectReferenceValue = subtitle;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? $"HUD.prefab: {NodeName} (child {node.GetSiblingIndex()}), {StylePath}" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Vector2 min, Vector2 max)
    {
        RectTransform rect = UiObject(name, parent);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = name;
        return text;
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
