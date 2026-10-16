using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.UI;
using Aipocalypse.Core.QuickTimeSystem;

public static class RepairFeedbackSetup
{
    const string Preset = "Assets/_Game/Data/Weapons/RepairFeedback.asset";
    const string Hud = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";

    public static object Enhance()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RepairFeedbackSettings>(Preset);
        settings.SuccessDuration = 1f;
        settings.SuccessHold = 0.32f;
        settings.SuccessScale = 1.35f;
        settings.BurstScale = 2.1f;
        settings.MissDuration = 0.3f;
        settings.MissShakePixels = 12f;
        settings.SuccessVolume = 1f;
        settings.AccentVolume = 0.65f;
        settings.MissVolume = 0.5f;
        settings.SuccessSound = Clip("Assets/_Samples/Audio/Weapons/Sniper_1/Sniper_1 Bolt Push.wav");
        settings.SuccessAccent = Clip("Assets/_StoreAssets/Animals/Malbers Animations/Common/Audio/Collect.wav");
        settings.MissSound = Clip("Assets/_Samples/Audio/Weapons/Pistol_1/Pistol_1 Mag Out.wav");
        settings.WeaponKickDuration = 0.32f;
        settings.WeaponKickPosition = new Vector3(0f, 0.004f, -0.018f);
        settings.WeaponKickEuler = new Vector3(-2.3f, 0.5f, 0.9f);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        foreach (string name in new[] { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel" })
        {
            var challenge = AssetDatabase.LoadAssetAtPath<TimingQteChallenge>("Assets/_Game/Data/Weapons/" + name + "_RepairQte.asset");
            challenge.MissPauseDuration = 0.24f;
            challenge.RelocateZoneOnMiss = true;
            challenge.MinimumZoneShift = 0.16f;
            EditorUtility.SetDirty(challenge);
            AssetDatabase.SaveAssetIfDirty(challenge);
        }
        return Inspect();
    }

    public static object Apply()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RepairFeedbackSettings>(Preset);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RepairFeedbackSettings>();
            AssetDatabase.CreateAsset(settings, Preset);
            settings.SuccessSound = Clip("Assets/_Samples/Audio/Weapons/Sniper_1/Sniper_1 Bolt Push.wav");
            settings.NaturalSound = settings.SuccessSound;
            settings.MissSound = Clip("Assets/_Samples/Audio/Weapons/Pistol_1/Pistol_1 Mag Out.wav");
            settings.SuccessAccent = Clip("Assets/_StoreAssets/Animals/Malbers Animations/Common/Audio/Collect.wav");
            EditorUtility.SetDirty(settings);
        }
        foreach (string name in new[] { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<GunRepairProfile>("Assets/_Game/Data/Weapons/" + name + "_Repair.asset");
            if (profile.Feedback == null)
            {
                profile.Feedback = settings;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
        }
        var hud = PrefabUtility.LoadPrefabContents(Hud);
        try
        {
            var presenter = hud.GetComponentInChildren<RepairQteHud>(true);
            var content = presenter.transform.Find("Content");
            if (content == null) throw new InvalidOperationException("Repair content missing");
            var group = content.GetComponent<CanvasGroup>();
            if (group == null) group = content.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = false;
            var burst = content.Find("SuccessBurst")?.GetComponent<UnityEngine.UI.Image>();
            if (burst == null)
            {
                var go = new GameObject("SuccessBurst", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(content, false);
                go.transform.SetAsFirstSibling();
                burst = go.GetComponent<UnityEngine.UI.Image>();
            }
            var style = AssetDatabase.LoadAssetAtPath<QteStyle>("Assets/_Game/Data/UI/RepairQteStyle.asset");
            burst.sprite = style.Ring;
            burst.color = settings.SuccessColor;
            burst.raycastTarget = false;
            burst.rectTransform.anchorMin = burst.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            burst.rectTransform.anchoredPosition = Vector2.zero;
            burst.rectTransform.sizeDelta = new Vector2(126f, 126f);
            burst.gameObject.SetActive(false);
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_group").objectReferenceValue = group;
            serialized.FindProperty("_burst").objectReferenceValue = burst;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(hud, Hud);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        AssetDatabase.SaveAssetIfDirty(settings);
        return Inspect();
    }

    public static object Inspect()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RepairFeedbackSettings>(Preset);
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(Hud);
        var presenter = hud.GetComponentInChildren<RepairQteHud>(true);
        var serialized = new SerializedObject(presenter);
        if (serialized.FindProperty("_group").objectReferenceValue == null || serialized.FindProperty("_burst").objectReferenceValue == null)
            throw new InvalidOperationException("Feedback HUD is not wired");
        return new { preset = Preset, settings.SuccessDuration, settings.SuccessHold, settings.SuccessScale, settings.MissDuration, settings.MissShakePixels, settings.WeaponKickDuration,
            sounds = new[] { settings.SuccessSound, settings.SuccessAccent, settings.MissSound, settings.NaturalSound }
                .Select(c => new { c.name, c.length }).ToArray(),
            guns = new[] { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel" }.Select(name => new { name,
                feedback = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GunRepairProfile>("Assets/_Game/Data/Weapons/" + name + "_Repair.asset").Feedback) }).ToArray() };
    }

    static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? throw new InvalidOperationException("Missing sound: " + path);
}
