using System;
using System.Collections.Generic;
using System.Linq;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.Feedback;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.UI;
using Aipocalypse.Core.WeaponSystem;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Hit feel and raid banners, safe to re-run:
//   Blood    — blood material and Blood_Hit / Blood_Headshot / Blood_Kill effects (Content/Enemies/_Shared/Effects), set on
//              Data/Enemies/EnemyReactionSettings.asset.
//   Feedback — Player.prefab: LandedHitFeedback (under GunJamFeedback, for its FeedbackContext) with Stagger and HeadshotKill
//              MMF_Players (hit stop + camera impulse), copied from the jam's Punch player.
//   Deaths   — Goblin.prefab's directional death counts (_sideDeaths), matching Goblin.animspec.txt (rebuild the controller first).
//   Audio    — Raid_Start / Raid_Victory BroAudio entities (2D) in the Enemies library.
//   Banner   — Data/UI/RaidBannerStyle.asset and the RaidBanner node in HUD.prefab.
// Run: unity command script run --file AgentScripts/Enemies/HitJuiceSetup.cs --entry HitJuiceSetup.All
public static class HitJuiceSetup
{
    const string EffectsDir = "Assets/_Game/Content/Enemies/_Shared/Effects";
    const string BloodMaterialPath = EffectsDir + "/Blood.mat";
    const string ReactionPath = "Assets/_Game/Data/Enemies/EnemyReactionSettings.asset";
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string GoblinPath = "Assets/_Game/Content/Enemies/Goblin/Goblin.prefab";
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string EnemiesDir = AudioDir + "/Enemies";
    const string Interface = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Interface & Item Sounds Lite - Version 2/Miscellaneous/";
    const string StylePath = "Assets/_Game/Data/UI/RaidBannerStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string FontPath = "Assets/Synty/InterfaceApocalypseHUD/Fonts/SairaCondensed/Font_Apocalypse_Bold.asset";
    const string UrpParticles = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    static readonly Color BloodColor = new(0.55f, 0.04f, 0.03f, 1f);

    public static string All() => string.Join("\n", Blood(), Feedback(), Deaths(), Audio(), Banner());

    public static string Blood()
    {
        Folder(EffectsDir);
        if (AssetDatabase.LoadAssetAtPath<Material>(BloodMaterialPath) == null && !AssetDatabase.CopyAsset(UrpParticles, BloodMaterialPath))
            throw new Exception("couldn't copy " + UrpParticles);
        var material = AssetDatabase.LoadAssetAtPath<Material>(BloodMaterialPath);
        material.SetColor("_BaseColor", Color.white);
        Save(material);

        PooledEffect hit = Effect("Blood_Hit", material, 1f, droplets: 10, cone: 25f, speed: new Vector2(1.5f, 3.5f), mist: 0.55f, maxActive: 24);
        PooledEffect head = Effect("Blood_Headshot", material, 1.4f, droplets: 22, cone: 35f, speed: new Vector2(2.5f, 5.5f), mist: 0.8f, maxActive: 8);
        PooledEffect kill = Effect("Blood_Kill", material, 1.6f, droplets: 30, cone: 30f, speed: new Vector2(3f, 7f), mist: 1.1f, maxActive: 8);

        var reaction = AssetDatabase.LoadAssetAtPath<EnemyReactionSettings>(ReactionPath);
        reaction.HitEffect = hit;
        reaction.CriticalEffect = head;
        reaction.KillEffect = kill;
        Save(reaction);
        return $"blood: {BloodMaterialPath}, Blood_Hit / Blood_Headshot / Blood_Kill, set on {ReactionPath}";
    }

    // Droplets in a cone along +Z (the effect's facing) that fall, plus a mist puff that grows and fades.
    static PooledEffect Effect(string name, Material material, float scale, int droplets, float cone, Vector2 speed, float mist, int maxActive)
    {
        var root = new GameObject(name);
        try
        {
            var pooled = root.AddComponent<PooledEffect>();
            var so = new SerializedObject(pooled);
            so.FindProperty("_lifetime").floatValue = 1.2f;
            so.FindProperty("_maxActive").intValue = maxActive;
            so.ApplyModifiedPropertiesWithoutUndo();

            ParticleSystem drops = Particles("Droplets", root.transform, material);
            var main = drops.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f * scale, 0.14f * scale);
            main.gravityModifier = 1.6f;
            main.maxParticles = droplets * 2;
            var emission = drops.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)droplets) });
            var shape = drops.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = 0.03f;

            ParticleSystem puff = Particles("Mist", root.transform, material);
            var puffMain = puff.main;
            puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            puffMain.startSize = new ParticleSystem.MinMaxCurve(mist * 0.6f, mist);
            puffMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            puffMain.maxParticles = 6;
            var puffEmission = puff.emission;
            puffEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });
            var puffShape = puff.shape;
            puffShape.shapeType = ParticleSystemShapeType.Cone;
            puffShape.angle = cone * 0.6f;
            puffShape.radius = 0.02f;
            var grow = puff.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            string path = $"{EffectsDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved)
                throw new Exception("failed to save " + path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PooledEffect>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // A one-shot, world-space system that fades out; EffectPool plays it (PooledEffect.Restart).
    static ParticleSystem Particles(string name, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = BloodColor;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
    }

    public static string Feedback()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            Transform jam = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GunJamFeedback")
                ?? throw new Exception("no GunJamFeedback in " + PlayerPath);
            Transform punch = jam.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Punch")
                ?? throw new Exception("no Punch feedback to copy");
            Transform old = jam.Find("LandedHitFeedback");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("LandedHitFeedback");
            node.transform.SetParent(jam, false);
            var view = node.AddComponent<LandedHitFeedback>();
            MMF_Player stagger = CopyPunch(punch, node.transform, "Stagger", hitStop: 0.045f, kick: 0.35f);
            MMF_Player headshot = CopyPunch(punch, node.transform, "HeadshotKill", hitStop: 0.07f, kick: 0.6f);
            var so = new SerializedObject(view);
            so.FindProperty("_stagger").objectReferenceValue = stagger;
            so.FindProperty("_headshotKill").objectReferenceValue = headshot;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath, out bool saved);
            return saved ? "Player.prefab: LandedHitFeedback (Stagger, HeadshotKill)" : "failed to save " + PlayerPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The jam Punch player's impulse and hit stop, softer: `kick` scales its impulse velocity.
    static MMF_Player CopyPunch(Transform punch, Transform parent, string name, float hitStop, float kick)
    {
        GameObject copy = Object.Instantiate(punch.gameObject, parent);
        copy.name = name;
        var player = copy.GetComponent<MMF_Player>();
        for (int i = player.FeedbacksList.Count - 1; i >= 0; i--)
        {
            switch (player.FeedbacksList[i])
            {
                case MMF_CinemachineImpulse impulse:
                    impulse.Velocity *= kick;
                    impulse.Label = name + " kick";
                    break;
                case MMF_HitStop stop:
                    stop.Duration = hitStop;
                    stop.TimeScale = 0.05f;
                    break;
                default:
                    player.FeedbacksList.RemoveAt(i);
                    break;
            }
        }
        if (!player.FeedbacksList.Any(f => f is MMF_HitStop))
            player.FeedbacksList.Add(new MMF_HitStop { Duration = hitStop, TimeScale = 0.05f, Label = "Hit Stop" });
        return player;
    }

    public static string Deaths()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GoblinPath);
        try
        {
            var so = new SerializedObject(root.GetComponent<EnemyAnimator>());
            SerializedProperty sides = so.FindProperty("_sideDeaths");
            sides.arraySize = 4;
            int[] counts = { 3, 3, 1, 1 };
            for (int i = 0; i < counts.Length; i++)
                sides.GetArrayElementAtIndex(i).intValue = counts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GoblinPath, out bool saved);
            return saved ? "Goblin.prefab: side deaths 3 / 3 / 1 / 1" : "failed to save " + GoblinPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static string Audio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(AudioDir + "/Enemies.asset") ?? throw new Exception("missing Enemies library");
        Entity(library, "Raid_Start", 0.9f, Interface + "Gong Hit 1-1.wav");
        Entity(library, "Raid_Victory", 0.8f, Interface + "Victory Jingle 1-1.wav");
        Save(library);
        return "audio: Raid_Start (Gong Hit), Raid_Victory (Victory Jingle) in the Enemies library";
    }

    // A 2D one-shot SFX entity (no spatial setting: the banner sounds at the listener).
    static AudioEntity Entity(AudioAsset library, string name, float volume, string clipPath)
    {
        string path = $"{EnemiesDir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
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

    public static string Banner()
    {
        var style = AssetDatabase.LoadAssetAtPath<RaidBannerStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<RaidBannerStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }
        style.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing font " + FontPath);
        style.StartSound = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>(EnemiesDir + "/Raid_Start.asset"));
        style.VictorySound = new SoundID(AssetDatabase.LoadAssetAtPath<AudioEntity>(EnemiesDir + "/Raid_Victory.asset"));
        Save(style);

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find("RaidBanner");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            // Upper third of the screen, over the world but under the death screen (placed before it).
            RectTransform node = UiObject("RaidBanner", root.transform);
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
            return saved ? $"HUD.prefab: RaidBanner (child {node.GetSiblingIndex()}), {StylePath}" : "failed to save " + HudPath;
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

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
