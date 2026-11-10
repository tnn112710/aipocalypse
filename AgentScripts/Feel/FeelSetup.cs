using System;
using System.Collections.Generic;
using System.Linq;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SurfaceSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the content of the shooting-feel pass. Safe to re-run: assets are updated in place, prefab nodes rebuilt.
//   Audio    new BroAudio entities (Gamemaster Audio clips): gun shots and tails, last-rounds click, a Surfaces library
//            (footsteps and bullet impacts per surface, flesh impact), the heartbeat
//   Surfaces Data/Surfaces/SurfaceSettings.asset (rules, looks, tinted impact effects) on the root scope prefab
//   Guns     sounds, field-of-view punch, shell prefabs and ejection ports on the gun prefabs
//   Player   Kill and MeleeHit feedback players under LandedHitFeedback
//   Hud      the LowHealth vignette node in HUD.prefab + Data/UI/LowHealthStyle.asset
//   Axe      cleave (3 targets) and a strike trail on the fire axe
// Run: unity command script run --file AgentScripts/Feel/FeelSetup.cs --entry FeelSetup.All
public static class FeelSetup
{
    const string G = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string GunClips = G + "Guns_Weapons/Guns/";
    const string BulletClips = G + "Guns_Weapons/Bullets/";
    const string StepClips = G + "Footsteps/";
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string GunsDir = AudioDir + "/Guns";
    const string PlayerDir = AudioDir + "/Player";
    const string SurfacesDir = AudioDir + "/Surfaces";
    const string Weapons = "Assets/_Game/Content/Weapons/";
    const string Effects = Weapons + "_Shared/Effects/";
    const string SurfacePath = "Assets/_Game/Data/Surfaces/SurfaceSettings.asset";
    const string RootPath = "Assets/_Game/_Core/_Bootstrap/ProjectLifetimeScope.prefab";
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string LowHealthPath = "Assets/_Game/Data/UI/LowHealthStyle.asset";

    public static string All() => string.Join("\n", Audio(), Surfaces(), Guns(), Player(), Hud(), Axe());

    // ---- Audio ----

    public static string Audio()
    {
        var log = new List<string>();
        AudioAsset guns = Load<AudioAsset>(AudioDir + "/Guns.asset");
        Finish(Entity(guns, GunsDir, "Fire_Smg", 0.8f, 1f, 0.04f, Range(GunClips + "gun_submachine_auto_shot_", 6)), null, log);
        Finish(Entity(guns, GunsDir, "Fire_Shotgun", 0.9f, 1f, 0.04f, Range(GunClips + "gun_shotgun_shot_", 4)), null, log);
        Finish(Entity(guns, GunsDir, "FireTail_Rifle", 0.55f, 1f, 0.03f, GunClips + "gun_semi_auto_rifle_shot_00_tail_only_01.wav"), null, log);
        Finish(Entity(guns, GunsDir, "FireTail_Smg", 0.5f, 1f, 0.03f, GunClips + "gun_submachine_auto_shot_00_tail_only_01.wav"), null, log);
        Finish(Entity(guns, GunsDir, "LowAmmo_Click", 0.6f, 1.15f, 0.03f, Range(GunClips + "gun_pistol_safety_switch_", 2)), null, log);

        AudioAsset player = Load<AudioAsset>(AudioDir + "/Player.asset");
        Finish(Entity(player, PlayerDir, "Heartbeat", 0.7f, 0.75f, 0f, BulletClips + "bullet_impact_body_thump_03.wav"), null, log);

        Folder(SurfacesDir);
        AudioAsset surfaces = Library(AudioDir + "/Surfaces.asset", "Surfaces", log);
        SpatialSetting spatial = Spatial(SurfacesDir + "/Impact_Spatial.asset", 2f, 60f);
        foreach ((string kind, string step, int steps, string impact) in SurfaceClips)
        {
            if (step != null)
                Finish(Entity(surfaces, SurfacesDir, "Footstep_" + kind, 0.8f, 1f, 0.06f, Range(StepClips + step, steps)), null, log);
            if (impact != null)
                Finish(Entity(surfaces, SurfacesDir, "Impact_" + kind, 0.85f, 1f, 0.08f, Range(BulletClips + impact, 8)), spatial, log);
        }
        Finish(Entity(surfaces, SurfacesDir, "Impact_Flesh", 0.8f, 1f, 0.08f, Range(BulletClips + "bullet_impact_body_flesh_", 8)), spatial, log);
        Save(guns);
        Save(player);
        Save(surfaces);
        return "audio: " + log.Count + " entities\n  " + string.Join("\n  ", log);
    }

    static readonly (string kind, string step, int steps, string impact)[] SurfaceClips =
    {
        ("Grass", "footstep_grass_walk_", 10, "bullet_impact_grass_"),
        ("Dirt", "footstep_dirt_walk_run_", 12, "bullet_impact_dirt_"),
        ("Stone", "footstep_concrete_walk_", 12, "bullet_impact_rock_"),
        ("Wood", "footstep_wood_walk_", 12, "bullet_impact_wood_light_"),
        ("Sand", "footstep_sand_walk_", 12, "bullet_impact_gravel_"),
        ("Mud", "footstep_mud_walk_", 9, "bullet_impact_mud_"),
        ("Metal", null, 0, "bullet_impact_concrete_brick_"),
    };

    // ---- Surfaces ----

    static readonly (string keyword, SurfaceKind kind)[] Rules =
    {
        ("Foundation", SurfaceKind.Stone), ("Fireplace", SurfaceKind.Stone), ("Stone", SurfaceKind.Stone), ("Rock", SurfaceKind.Stone),
        ("Boulder", SurfaceKind.Stone), ("Clif", SurfaceKind.Stone), ("Pebbles", SurfaceKind.Stone),
        ("Metal", SurfaceKind.Metal), ("Lamp", SurfaceKind.Metal),
        ("Fence", SurfaceKind.Wood), ("Wall", SurfaceKind.Wood), ("Roof", SurfaceKind.Wood), ("Floor", SurfaceKind.Wood),
        ("Beam", SurfaceKind.Wood), ("Log", SurfaceKind.Wood), ("Bench", SurfaceKind.Wood), ("Barrel", SurfaceKind.Wood),
        ("Box", SurfaceKind.Wood), ("Crate", SurfaceKind.Wood), ("Cart", SurfaceKind.Wood), ("Door", SurfaceKind.Wood),
        ("Stairs", SurfaceKind.Wood), ("Rails", SurfaceKind.Wood), ("Post", SurfaceKind.Wood), ("Gable", SurfaceKind.Wood),
        ("Extension", SurfaceKind.Wood), ("Tree", SurfaceKind.Wood), ("Bark", SurfaceKind.Wood), ("Birch", SurfaceKind.Wood),
        ("Broadleaf", SurfaceKind.Wood), ("Sakura", SurfaceKind.Wood), ("Bush", SurfaceKind.Wood), ("Wood", SurfaceKind.Wood),
        ("Table", SurfaceKind.Wood), ("Bridge", SurfaceKind.Wood), ("Plank", SurfaceKind.Wood), ("Bed", SurfaceKind.Wood),
        ("Sand", SurfaceKind.Sand), ("Mud", SurfaceKind.Mud), ("Dirt", SurfaceKind.Dirt), ("Grass", SurfaceKind.Grass),
    };

    // Impact dust per surface: the shared bullet impact with its particles tinted (the dark mark keeps its colour).
    static readonly (SurfaceKind kind, Color tint)[] Tints =
    {
        (SurfaceKind.Grass, new Color(0.62f, 0.72f, 0.38f)), (SurfaceKind.Dirt, new Color(0.72f, 0.52f, 0.36f)),
        (SurfaceKind.Wood, new Color(0.95f, 0.74f, 0.46f)), (SurfaceKind.Stone, new Color(0.9f, 0.9f, 0.86f)),
        (SurfaceKind.Sand, new Color(1f, 0.88f, 0.6f)), (SurfaceKind.Mud, new Color(0.5f, 0.38f, 0.28f)),
    };

    public static string Surfaces()
    {
        Folder("Assets/_Game/Data/Surfaces");
        var settings = AssetDatabase.LoadAssetAtPath<SurfaceSettings>(SurfacePath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<SurfaceSettings>();
            AssetDatabase.CreateAsset(settings, SurfacePath);
        }
        settings.Rules = Rules.Select(r => new SurfaceRule { Keyword = r.keyword, Kind = r.kind }).ToList();
        settings.Looks = new Dictionary<SurfaceKind, SurfaceLook>();
        foreach (SurfaceKind kind in Enum.GetValues(typeof(SurfaceKind)))
        {
            settings.Looks[kind] = new SurfaceLook
            {
                Footstep = Id(SurfacesDir, "Footstep_" + kind),
                BulletImpact = Id(SurfacesDir, "Impact_" + kind),
                ImpactEffect = TintedImpact(kind),
            };
        }
        settings.FleshImpact = Id(SurfacesDir, "Impact_Flesh");
        Save(settings);

        var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPath);
        var scope = root.GetComponent<MB13.Aipocalypse.Core.Bootstrap.ProjectLifetimeScope>();
        var so = new SerializedObject(scope);
        so.FindProperty("_surfaceSettings").objectReferenceValue = settings;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(root);
        return $"surfaces: {settings.Rules.Count} rules, {settings.Looks.Count} looks, on the root scope";
    }

    static PooledEffect TintedImpact(SurfaceKind kind)
    {
        int index = Array.FindIndex(Tints, t => t.kind == kind);
        if (index < 0)
            return null;
        string path = $"{Effects}BulletImpact_{kind}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            AssetDatabase.CopyAsset(Effects + "BulletImpact.prefab", path);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Effects + "BulletImpact.prefab");
            ParticleSystem[] sources = source.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem[] systems = contents.GetComponentsInChildren<ParticleSystem>(true);
            Color tint = Tints[index].tint;
            for (int i = 0; i < systems.Length && i < sources.Length; i++)
            {
                if (systems[i].name == "BlackMark")
                    continue;
                // From the source every run, so a re-run never tints twice. Its orange becomes the tint at the same brightness.
                ParticleSystem.MinMaxGradient from = sources[i].main.startColor;
                ParticleSystem.MainModule main = systems[i].main;
                main.startColor = from.mode == ParticleSystemGradientMode.TwoColors
                    ? new ParticleSystem.MinMaxGradient(Tinted(from.colorMin, tint), Tinted(from.colorMax, tint))
                    : new ParticleSystem.MinMaxGradient(Tinted(from.color, tint));
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        return AssetDatabase.LoadAssetAtPath<PooledEffect>(path);
    }

    static Color Tinted(Color from, Color tint)
    {
        float brightness = Mathf.Max(from.r, from.g, from.b);
        return new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, from.a);
    }

    // ---- Guns ----

    public static string Guns()
    {
        var log = new List<string>();
        PooledEffect rifleShell = Shell("Shell_Rifle", "Shell_Brass", new Color(0.85f, 0.62f, 0.22f), 0.9f, new Vector3(0.007f, 0.013f, 0.007f));
        PooledEffect pistolShell = Shell("Shell_Pistol", "Shell_Brass", new Color(0.85f, 0.62f, 0.22f), 0.9f, new Vector3(0.006f, 0.007f, 0.006f));
        PooledEffect shotgunShell = Shell("Shell_Shotgun", "Shell_Red", new Color(0.7f, 0.1f, 0.08f), 0.1f, new Vector3(0.012f, 0.018f, 0.012f));

        Gun("AK47", "Bolt", rifleShell, false, 0.008f, 0.03f, null, "FireTail_Rifle", log);
        Gun("MP5", "Bolt", pistolShell, false, 0.006f, 0.025f, "Fire_Smg", "FireTail_Smg", log);
        Gun("Pistol", "Slide", pistolShell, false, 0.012f, 0.03f, null, null, log);
        Gun("PumpShotgun", "Bolt", shotgunShell, true, 0.03f, 0.04f, "Fire_Shotgun", null, log);
        Gun("DoubleBarrel", null, null, false, 0.03f, 0.045f, "Fire_Shotgun", null, log);
        return "guns:\n  " + string.Join("\n  ", log);
    }

    // A spent case: a small cylinder that falls and bounces on the Debris layer (like dropped magazines), pooled.
    static PooledEffect Shell(string name, string materialName, Color color, float metallic, Vector3 scale)
    {
        string materialPath = $"{Effects}{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.55f);
        Save(material);

        string path = $"{Effects}{name}.prefab";
        var magazine = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons + "AK47/AK47_Magazine.prefab");
        var root = new GameObject(name) { layer = magazine.layer };
        try
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            model.name = "Model";
            model.layer = root.layer;
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = scale;
            Object.DestroyImmediate(model.GetComponent<Collider>());
            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = scale.x * 0.5f;
            collider.height = scale.y * 2f;
            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.02f;
            body.angularDamping = 0.5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var effect = root.AddComponent<PooledEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_maxActive").intValue = 24;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        return AssetDatabase.LoadAssetAtPath<PooledEffect>(path);
    }

    static void Gun(string name, string portPart, PooledEffect shell, bool onCycle, float fovKick, float fovMax, string fire, string tail,
        List<string> log)
    {
        var definition = Load<GunDefinition>($"{Weapons}{name}/{name}.asset");
        var so = new SerializedObject(definition);
        so.FindProperty("_ejection.Shell").objectReferenceValue = shell;
        so.FindProperty("_ejection.OnCycle").boolValue = onCycle;
        so.FindProperty("_recoil.FovKick").floatValue = fovKick;
        so.FindProperty("_recoil.FovKickMax").floatValue = fovMax;
        so.ApplyModifiedPropertiesWithoutUndo();
        GunSounds sounds = definition.Sounds;
        if (fire != null)
            sounds.Fire = Id(GunsDir, fire);
        sounds.FireTail = tail != null ? Id(GunsDir, tail) : default;
        sounds.LowAmmo = Id(GunsDir, "LowAmmo_Click");
        Save(definition);

        string prefabPath = $"{Weapons}{name}/{name}.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform weapon = contents.GetComponentsInChildren<Transform>(true).First(t => t.name == "Weapon");
            Transform old = weapon.Find("EjectionPort");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Transform port = null;
            if (portPart != null)
            {
                Transform part = weapon.Find(portPart) ?? throw new Exception($"{name}: no {portPart} under Weapon");
                port = new GameObject("EjectionPort") { layer = weapon.gameObject.layer }.transform;
                port.SetParent(weapon, false);
                // On the right side of the action; x (red) points out of the port.
                port.localPosition = part.localPosition + new Vector3(0.025f, 0.01f, 0f);
                port.localRotation = Quaternion.identity;
            }
            var held = contents.GetComponent<HeldGun>();
            var heldSo = new SerializedObject(held);
            heldSo.FindProperty("_ejectionPort").objectReferenceValue = port;
            heldSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        log.Add($"{name}: fov {fovKick}/{fovMax}, fire {fire ?? "kept"}, tail {tail ?? "none"}, shell {(shell != null ? shell.name : "none")}{(onCycle ? " (on pump)" : "")}");
    }

    // ---- Player: the light feedback players ----

    public static string Player()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var feedback = contents.GetComponentInChildren<LandedHitFeedback>(true);
            Transform parent = feedback.transform;
            Transform template = parent.Find("HeadshotKill") ?? throw new Exception("no HeadshotKill player to copy");
            MMF_Player kill = Copy(template, "Kill", 0.4f, 0f, 0f);
            MMF_Player melee = Copy(template, "MeleeHit", 0.55f, 0.045f, 0.1f);
            var so = new SerializedObject(feedback);
            so.FindProperty("_kill").objectReferenceValue = kill;
            so.FindProperty("_meleeHit").objectReferenceValue = melee;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath);
            return "player: Kill (impulse x0.4, no hit stop) and MeleeHit (impulse x0.55, 45 ms hit stop at 0.1) under LandedHitFeedback";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // A copy of a feedback player with its impulse scaled; hitStop 0 removes the hit stop.
    static MMF_Player Copy(Transform template, string name, float impulseScale, float hitStop, float timeScale)
    {
        Transform old = template.parent.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        GameObject copy = Object.Instantiate(template.gameObject, template.parent);
        copy.name = name;
        var player = copy.GetComponent<MMF_Player>();
        for (int i = player.FeedbacksList.Count - 1; i >= 0; i--)
        {
            MMF_Feedback item = player.FeedbacksList[i];
            if (item is MoreMountains.FeedbacksForThirdParty.MMF_CinemachineImpulse impulse)
            {
                impulse.Velocity *= impulseScale;
                impulse.Label = name + " kick";
            }
            else if (item is MB13.Aipocalypse.Core.Feedback.MMF_HitStop stop)
            {
                if (hitStop <= 0f)
                    player.FeedbacksList.RemoveAt(i);
                else
                {
                    stop.Duration = hitStop;
                    stop.TimeScale = timeScale;
                }
            }
        }
        EditorUtility.SetDirty(player);
        return player;
    }

    // ---- HUD: low health ----

    public static string Hud()
    {
        var style = AssetDatabase.LoadAssetAtPath<LowHealthStyle>(LowHealthPath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<LowHealthStyle>();
            AssetDatabase.CreateAsset(style, LowHealthPath);
        }
        style.Heartbeat = Id(PlayerDir, "Heartbeat");
        Save(style);

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find("LowHealth");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("LowHealth", typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)node.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            // Under the widgets, over the hurt flash; fades with the HUD on death.
            rect.SetSiblingIndex(1);
            node.AddComponent<Canvas>();
            var image = node.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            var hud = node.AddComponent<LowHealthHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_vignette").objectReferenceValue = image;
            so.FindProperty("_style").objectReferenceValue = style;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return "HUD.prefab: LowHealth (child 1, own canvas, RawImage + LowHealthHud)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- Fire axe ----

    public static string Axe()
    {
        var definition = Load<MeleeDefinition>(Weapons + "FireAxe/FireAxe.asset");
        var so = new SerializedObject(definition);
        so.FindProperty("_swing.MaxTargets").intValue = 3;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(definition);

        string materialPath = Effects + "MeleeTrail.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Load<Material>(Effects + "Tracer_Line.mat"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
        Save(material);

        string prefabPath = Weapons + "FireAxe/FireAxe.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform model = contents.transform.Find("PosePivot/Model");
            Transform old = model.Find("Trail");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("Trail") { layer = model.gameObject.layer };
            node.transform.SetParent(model, false);
            // The axe head: top of the model, on the blade side.
            node.transform.localPosition = new Vector3(0f, 0.74f, -0.24f);
            var trail = node.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.02f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.sharedMaterial = material;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            trail.alignment = LineAlignment.View;
            var held = contents.GetComponent<HeldMelee>();
            var heldSo = new SerializedObject(held);
            heldSo.FindProperty("_trail").objectReferenceValue = trail;
            heldSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        return "fire axe: MaxTargets 3, strike trail on the head";
    }

    // ---- helpers ----

    static SoundID Id(string dir, string entity)
    {
        var asset = AssetDatabase.LoadAssetAtPath<AudioEntity>($"{dir}/{entity}.asset");
        return asset != null ? new SoundID(asset) : default;
    }

    static string[] Range(string prefix, int count)
    {
        var paths = new string[count];
        for (int i = 0; i < count; i++)
            paths[i] = $"{prefix}{i + 1:00}.wav";
        return paths;
    }

    static AudioAsset Library(string path, string name, List<string> log)
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(path);
        if (library != null)
            return library;
        library = ScriptableObject.CreateInstance<AudioAsset>();
        AssetDatabase.CreateAsset(library, path);
        var so = new SerializedObject(library);
        so.FindProperty("<AssetName>k__BackingField").stringValue = name;
        so.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(path);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("library " + path);
        return library;
    }

    // A one-shot SFX entity with shuffled clips, a base pitch and a random pitch range.
    static AudioEntity Entity(AudioAsset library, string dir, string name, float volume, float pitch, float pitchRandom, params string[] clips)
    {
        string path = $"{dir}/{name}.asset";
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
        so.FindProperty("<Pitch>k__BackingField").floatValue = pitch;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)(clips.Length > 1 ? MulticlipsPlayMode.Shuffle : MulticlipsPlayMode.Single);
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
        return entity;
    }

    static void Finish(AudioEntity entity, SpatialSetting spatial, List<string> log)
    {
        var so = new SerializedObject(entity);
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        log.Add(entity.name);
    }

    static SpatialSetting Spatial(string path, float min, float max)
    {
        var spatial = AssetDatabase.LoadAssetAtPath<SpatialSetting>(path);
        if (spatial == null)
        {
            spatial = ScriptableObject.CreateInstance<SpatialSetting>();
            AssetDatabase.CreateAsset(spatial, path);
        }
        spatial.MinDistance = min;
        spatial.MaxDistance = max;
        spatial.RolloffMode = AudioRolloffMode.Logarithmic;
        spatial.DopplerLevel = 0f;
        Save(spatial);
        return spatial;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("missing " + path);

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
