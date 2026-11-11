using System;
using System.Collections.Generic;
using System.Linq;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.RaidSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Builds the two ranged goblin kinds: the spear thrower (GoblinSpear) and the archer (GoblinArcher). Safe to re-run.
// First, once (and again after their specs change):
//   Folders                                                     this script's Folders entry
//   CharacterKit.BuildLocomotionController                      AgentScripts/Characters/Goblin/GoblinSpear.animspec.txt, GoblinArcher.animspec.txt
//   TreeKit.Build                                               AgentScripts/Enemies/Ranged.tree.txt
// Then All: sounds (Enemies library), projectile and held-prop prefabs, the landing marker, projectile / ranged / enemy
// definitions, spawn cards (listed in EnemySystemSettings and SpawnSystemSettings), the two enemy prefabs (variants of
// Goblin_02 and Goblin_04) and Raid (adds them to Data/Raids/Night_Default; RaidNightSetup.Raid rewrites that plan without
// them, so run Raid again after it).
// Run: unity command script run --file AgentScripts/Enemies/RangedGoblinSetup.cs --entry RangedGoblinSetup.All
public static class RangedGoblinSetup
{
    const string Enemies = "Assets/_Game/Content/Enemies/";
    const string Shared = Enemies + "_Shared/Projectiles/";
    const string AudioDir = "Assets/_Game/Data/Audio/Enemies";
    const string G = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string BowPack = "Assets/Synty/AnimationBowCombat/";
    const string TracerMaterial = "Assets/_Game/Content/Weapons/_Shared/Effects/Tracer_Line.mat";
    const string RangedTree = Enemies + "_Shared/Ranged.asset";

    public static string Folders()
    {
        Folder(Enemies + "GoblinSpear");
        Folder(Enemies + "GoblinArcher");
        Folder(Enemies + "_Shared/Projectiles");
        return "folders ready";
    }

    public static string All()
    {
        Folders();
        var log = new List<string>();
        Sounds(log);
        Material trail = TrailMaterial();
        PooledEffect marker = LandingMarker(log);

        // --- Spear thrower ---
        GameObject spearModel = SpearModel();
        PooledEffect spearFlight = Flight("Spear", spearModel, trail, 0.16f, 0.35f, new Color(1f, 0.45f, 0.15f));
        ProjectileDefinition spear = Projectile("Spear", spearFlight, marker, 0.14f, "FlyBy_Spear", 6f);
        RangedSettings spearRanged = Ranged("GoblinSpear", spear, r =>
        {
            r.StandOff = new Vector2(9f, 14f);
            r.MaxRange = 19f;
            r.MinRange = 4f;
            // The throw clip is 2.4 s; the arm comes through a little before its middle.
            r.AttackDuration = 2.4f;
            r.ReleaseTime = 1.1f;
            r.Cooldown = new Vector2(2.2f, 4f);
            r.StraightSpeed = 22f;
            r.LobAngle = new Vector2(30f, 50f);
            r.MaxLobSpeed = 26f;
            r.Spread = 2.5f;
            r.Lead = new Vector2(0.3f, 0.9f);
            r.WindUp = Id("Goblin_Attack");
            r.Launch = Id("Ranged_SpearThrow");
        });
        EnemyDefinition spearKind = Kind("GoblinSpear", spearRanged, 90f, 22f);
        string spearPrefab = Prefab("GoblinSpear", "Goblin_02", spearKind, model => HoldSpear(model, spearModel), log);
        Card("GoblinSpear", spearPrefab, new Vector2Int(1, 2), log);

        // --- Archer ---
        Material bowMaterial = BowMaterial();
        GameObject arrowModel = ArrowModel(bowMaterial);
        GameObject bowModel = BowModel(bowMaterial);
        PooledEffect arrowFlight = Flight("Arrow", arrowModel, trail, 0.1f, 0.3f, new Color(1f, 0.75f, 0.2f));
        ProjectileDefinition arrow = Projectile("Arrow", arrowFlight, marker, 0.08f, "FlyBy_Arrow", 5f);
        RangedSettings archerRanged = Ranged("GoblinArcher", arrow, r =>
        {
            r.StandOff = new Vector2(16f, 24f);
            r.MaxRange = 32f;
            r.MinRange = 6f;
            // The draw clip is 0.63 s: the arrow leaves as it ends.
            r.AttackDuration = 1.1f;
            r.ReleaseTime = 0.62f;
            r.Cooldown = new Vector2(1.6f, 3.2f);
            r.StraightSpeed = 32f;
            r.LobAngle = new Vector2(28f, 46f);
            r.MaxLobSpeed = 34f;
            r.Spread = 2f;
            r.Lead = new Vector2(0.4f, 1f);
            r.WindUp = Id("Ranged_BowDraw");
            r.Launch = Id("Ranged_BowShoot");
        });
        EnemyDefinition archerKind = Kind("GoblinArcher", archerRanged, 70f, 12f);
        string archerPrefab = Prefab("GoblinArcher", "Goblin_04", archerKind, model => HoldBow(model, bowModel), log);
        Card("GoblinArcher", archerPrefab, new Vector2Int(1, 2), log);

        log.Add(Raid());
        return string.Join("\n", log);
    }

    // ---- sounds ----

    static void Sounds(List<string> log)
    {
        var library = Load<AudioAsset>("Assets/_Game/Data/Audio/Enemies.asset");
        var near = Load<SpatialSetting>(AudioDir + "/Enemy_Spatial.asset");
        SpatialSetting pass = Spatial(AudioDir + "/FlyBy_Spatial.asset", 2f, 30f);
        Entity(library, "Ranged_BowDraw", 0.8f, 1f, 0.05f, near, Range(G + "Guns_Weapons/Bow_Arrow/bow_crossbow_arrow_draw_stretch1_", 6));
        Entity(library, "Ranged_BowShoot", 0.9f, 1f, 0.05f, near, Range(G + "Guns_Weapons/Bow_Arrow/bow_crossbow_arrow_shoot_type1_", 6));
        Entity(library, "Ranged_SpearThrow", 0.9f, 0.9f, 0.06f, near, Range(G + "Whooshes/whoosh_swish_med_fast_", 4));
        Entity(library, "FlyBy_Arrow", 1f, 1.15f, 0.08f, pass, Range(G + "Whooshes/whoosh_swish_high_fast_", 4));
        Entity(library, "FlyBy_Spear", 1f, 0.95f, 0.06f, pass, Range(G + "Whooshes/whoosh_swish_high_big_", 5));
        Entity(library, "Projectile_Impact", 0.85f, 1f, 0.08f, near, Range(G + "Guns_Weapons/Bullets/bullet_impact_wood_heavy_", 8));
        Entity(library, "Projectile_Hit", 0.9f, 1f, 0.06f, near, Range(G + "Guns_Weapons/Bullets/bullet_impact_body_flesh_", 8));
        Save(library);
        log.Add("sounds: Ranged_BowDraw, Ranged_BowShoot, Ranged_SpearThrow, FlyBy_Arrow, FlyBy_Spear, Projectile_Impact, Projectile_Hit");
    }

    // ---- models and effects ----

    // The projectiles' bright trail: the tracer's material, so it blooms and reads at night.
    static Material TrailMaterial()
    {
        string path = Shared + "ProjectileTrail.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Load<Material>(TracerMaterial));
            AssetDatabase.CreateAsset(material, path);
        }
        Save(material);
        return material;
    }

    static Material Lit(string name, Color color, float smoothness)
    {
        string path = Shared + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        Save(material);
        return material;
    }

    // A hand-made spear, built from primitives (no pack spear is declared for use): a shaft with a stone head, its tip at
    // +Z, held 40% up the shaft at the origin.
    static GameObject SpearModel()
    {
        string path = Shared + "Spear_Model.prefab";
        Material wood = Lit("Spear_Wood", new Color(0.42f, 0.28f, 0.16f), 0.2f);
        Material stone = Lit("Spear_Stone", new Color(0.55f, 0.56f, 0.6f), 0.35f);
        var root = new GameObject("Spear_Model");
        try
        {
            const float length = 1.5f;
            const float grip = 0.4f;
            GameObject shaft = Primitive(PrimitiveType.Cylinder, "Shaft", root.transform, wood);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.028f, length * 0.5f, 0.028f);
            shaft.transform.localPosition = new Vector3(0f, 0f, length * (0.5f - grip));
            GameObject head = Primitive(PrimitiveType.Cube, "Head", root.transform, stone);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            head.transform.localScale = new Vector3(0.05f, 0.05f, 0.22f);
            head.transform.localPosition = new Vector3(0f, 0f, length * (1f - grip) + 0.08f);
            GameObject binding = Primitive(PrimitiveType.Cylinder, "Binding", root.transform, Lit("Spear_Cord", new Color(0.7f, 0.2f, 0.12f), 0.1f));
            binding.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            binding.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            binding.transform.localPosition = new Vector3(0f, 0f, length * (1f - grip) - 0.05f);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // The bow pack's materials are Built-in Standard (pink in URP): a URP copy with its texture.
    static Material BowMaterial()
    {
        Material material = Lit("BowCombat_URP", Color.white, 0.2f);
        var source = AssetDatabase.LoadAssetAtPath<Material>(BowPack + "Materials/BowCombat_Texture_01.mat");
        Texture texture = source != null ? source.mainTexture : null;
        if (texture != null)
            material.SetTexture("_BaseMap", texture);
        Save(material);
        return material;
    }

    // An arrow from the bow pack (its mesh only), its tip at +Z, centred.
    static GameObject ArrowModel(Material material) =>
        MeshModel("Arrow_Model", BowPack + "Models/Arrows/Wep_Arrow_Makeshift_01.fbx", material, 0.85f, true);

    // The unrigged recurve bow (no Animator: the string stays still), limbs along Z, centred on the grip.
    static GameObject BowModel(Material material) =>
        MeshModel("Bow_Model", BowPack + "Models/Bows/Unrigged/Wep_RecurveBow_01_Unrigged.fbx", material, 0.95f, false);

    // A copy of a model's first mesh under a root, turned so its longest side lies along +Z and scaled to `length` metres.
    static GameObject MeshModel(string name, string fbx, Material material, float length, bool centre)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) ?? throw new Exception("missing " + fbx);
        MeshFilter filter = source.GetComponentsInChildren<MeshFilter>(true).OrderByDescending(f => f.sharedMesh.bounds.size.magnitude).First();
        Mesh mesh = filter.sharedMesh;
        Vector3 size = mesh.bounds.size;
        var root = new GameObject(name);
        try
        {
            var model = new GameObject("Mesh");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, mesh.subMeshCount)).ToArray();
            Quaternion rotation = size.x >= size.y && size.x >= size.z ? Quaternion.Euler(0f, -90f, 0f)
                : size.y >= size.z ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            float scale = length / Mathf.Max(size.x, size.y, size.z);
            model.transform.localRotation = rotation;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = centre ? -(rotation * mesh.bounds.center) * scale : Vector3.zero;
            return PrefabUtility.SaveAsPrefabAsset(root, Shared + name + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // What flies: the model with a bright trail from its tail and a glowing bead at its head, pooled, returned by ProjectileSystem.
    static PooledEffect Flight(string name, GameObject modelPrefab, Material trail, float width, float time, Color color)
    {
        string path = Shared + name + ".prefab";
        var root = new GameObject(name);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
            model.name = "Model";
            foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;

            var tail = new GameObject("Trail");
            tail.transform.SetParent(root.transform, false);
            tail.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            var renderer = tail.AddComponent<TrailRenderer>();
            renderer.time = time;
            renderer.minVertexDistance = 0.05f;
            renderer.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
            // HDR: the head of the trail blooms.
            Color hot = color * 3f;
            hot.a = 1f;
            renderer.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(hot, 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            renderer.sharedMaterial = trail;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = LineAlignment.View;

            var effect = root.AddComponent<PooledEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_lifetime").floatValue = 0f;
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

    // A red ring on the ground where a lob will land: a quad with a ring texture made here.
    static PooledEffect LandingMarker(List<string> log)
    {
        string texturePath = Shared + "LandingRing.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2((x - half) / half, (y - half) / half).magnitude;
                    // A bright rim, a faint fill and a dot in the middle.
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);
                    float fill = d < 0.86f ? 0.12f : 0f;
                    float dot = Mathf.Clamp01(1f - d / 0.12f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(rim, dot) + fill)));
                }
            }
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            log.Add("made " + texturePath);
        }

        string materialPath = Shared + "LandingMarker.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", new Color(3f, 0.35f, 0.2f, 0.85f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        Save(material);

        string path = Shared + "LandingMarker.prefab";
        var root = new GameObject("LandingMarker");
        try
        {
            GameObject quad = Primitive(PrimitiveType.Quad, "Ring", root.transform, material);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            quad.transform.localScale = Vector3.one * 1.6f;
            var effect = root.AddComponent<PooledEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_lifetime").floatValue = 0f;
            so.FindProperty("_maxActive").intValue = 16;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        return AssetDatabase.LoadAssetAtPath<PooledEffect>(path);
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    // ---- data ----

    static ProjectileDefinition Projectile(string name, PooledEffect visual, PooledEffect marker, float radius, string flyBy, float flyByRadius)
    {
        var definition = LoadOrCreate<ProjectileDefinition>($"{Shared}{name}_Projectile.asset");
        definition.Radius = radius;
        definition.Visual = visual;
        definition.LandingMarker = marker;
        definition.FlyBy = Id(flyBy);
        definition.FlyByRadius = flyByRadius;
        definition.Impact = Id("Projectile_Impact");
        definition.Hit = Id("Projectile_Hit");
        // Everything but other enemies, loose debris, items and what the player holds.
        definition.HitLayers = ~LayerMask.GetMask("Enemy", "Debris", "Item", "HeldWeapon", "Ignore Raycast", "UI", "TransparentFX");
        Save(definition);
        return definition;
    }

    static RangedSettings Ranged(string kind, ProjectileDefinition projectile, Action<RangedSettings> tune)
    {
        var settings = LoadOrCreate<RangedSettings>($"{Enemies}{kind}/{kind}_Ranged.asset");
        settings.Projectile = projectile;
        settings.Obstacles = ~LayerMask.GetMask("Enemy", "Debris", "Item", "HeldWeapon", "Ignore Raycast", "UI", "TransparentFX");
        tune(settings);
        Save(settings);
        return settings;
    }

    // The goblin's definition with its own stats and ranged settings; it hunts the player only (it never breaks fences).
    static EnemyDefinition Kind(string kind, RangedSettings ranged, float health, float damage)
    {
        string path = $"{Enemies}{kind}/{kind}.asset";
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        if (definition == null)
        {
            AssetDatabase.CopyAsset(Enemies + "Goblin/Goblin.asset", path);
            definition = Load<EnemyDefinition>(path);
        }
        definition.Ranged = ranged;
        definition.SetBaseStat(EnemyStat.MaxHealth, health);
        definition.SetBaseStat(EnemyStat.AttackDamage, damage);
        var so = new SerializedObject(definition);
        SerializedProperty targets = so.FindProperty("_targets");
        targets.arraySize = 1;
        targets.GetArrayElementAtIndex(0).FindPropertyRelative("key").intValue = (int)TargetKind.Player;
        targets.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(definition);
        return definition;
    }

    static void Card(string kind, string prefabPath, Vector2Int group, List<string> log)
    {
        string path = $"{Enemies}{kind}/{kind}_Card.asset";
        var card = AssetDatabase.LoadAssetAtPath<EnemySpawnCard>(path);
        if (card == null)
        {
            AssetDatabase.CopyAsset(Enemies + "Goblin/Goblin_Card.asset", path);
            card = Load<EnemySpawnCard>(path);
        }
        var so = new SerializedObject(card);
        SerializedProperty variants = so.FindProperty("_variants");
        variants.arraySize = 1;
        variants.GetArrayElementAtIndex(0).FindPropertyRelative("key").objectReferenceValue =
            Load<GameObject>(prefabPath).GetComponent<Spawnable>();
        variants.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = 1f;
        so.FindProperty("GroupSize").vector2IntValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(card);

        foreach (string settingsPath in new[] { "Assets/_Game/Data/Enemies/EnemySystemSettings.asset", "Assets/_Game/Data/Spawning/SpawnSystemSettings.asset" })
        {
            Object settings = Load<ScriptableObject>(settingsPath);
            var list = new SerializedObject(settings);
            SerializedProperty cards = list.FindProperty("Cards");
            bool listed = false;
            for (int i = 0; i < cards.arraySize; i++)
                listed |= cards.GetArrayElementAtIndex(i).objectReferenceValue == card;
            if (!listed)
            {
                cards.arraySize++;
                cards.GetArrayElementAtIndex(cards.arraySize - 1).objectReferenceValue = card;
                list.ApplyModifiedPropertiesWithoutUndo();
                Save(settings);
            }
        }
        log.Add($"{kind}: card (groups of {group.x}-{group.y}), listed for enemy.spawn / spawn.card");
    }

    // ---- prefabs ----

    // A variant of a goblin look: its own controller, definition and tree, the sword put away, the ranged weapon in hand.
    static string Prefab(string kind, string look, EnemyDefinition definition, Func<Transform, (Transform launch, GameObject held)> arm, List<string> log)
    {
        string path = $"{Enemies}{kind}/{kind}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            var source = Load<GameObject>($"{Enemies}Goblin/{look}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Enemies}{kind}/{kind}.controller")
            ?? throw new Exception($"no {kind}.controller: build it from AgentScripts/Characters/Goblin/{kind}.animspec.txt first");
        var tree = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RangedTree)
            ?? throw new Exception("no Ranged.asset: TreeKit.Build AgentScripts/Enemies/Ranged.tree.txt first");

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var enemyAnimator = root.GetComponent<EnemyAnimator>();
            Animator animator = enemyAnimator.Animator;
            animator.runtimeAnimatorController = controller;
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform sword = rightHand != null ? rightHand.Find("Sword") : null;
            if (sword != null)
                sword.gameObject.SetActive(false);
            (Transform launch, GameObject held) = arm(animator.transform);

            var so = new SerializedObject(enemyAnimator);
            so.FindProperty("_attacks").intValue = 1;
            so.FindProperty("_launchPoint").objectReferenceValue = launch;
            so.FindProperty("_heldProjectile").objectReferenceValue = held;
            so.ApplyModifiedPropertiesWithoutUndo();

            var brain = new SerializedObject(root.GetComponent<EnemyBrain>());
            brain.FindProperty("_definition").objectReferenceValue = definition;
            brain.ApplyModifiedPropertiesWithoutUndo();

            var behavior = new SerializedObject(root.GetComponent<Opsive.BehaviorDesigner.Runtime.BehaviorTree>());
            behavior.FindProperty("m_Subtree").objectReferenceValue = tree;
            behavior.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        log.Add($"{kind}: {path} (variant of {look})");
        return path;
    }

    // The hand's frame from the bind pose, as BuildGoblin.HoldSword: `up` runs from the little finger to the index (out of
    // the fist on the thumb side), `fingers` along the fingers.
    static bool HandFrame(Animator animator, bool right, out Transform hand, out Vector3 up, out Vector3 fingers, out Vector3 grip)
    {
        hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform index = animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform middle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        Transform little = animator.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        up = fingers = grip = default;
        if (hand == null || index == null || middle == null || little == null)
            return false;
        up = (index.position - little.position).normalized;
        fingers = Vector3.ProjectOnPlane(middle.position - hand.position, up).normalized;
        Vector3 palm = Vector3.Cross(fingers, up) * (right ? 1f : -1f);
        grip = Vector3.Lerp(hand.position, middle.position, 0.6f) + palm * 0.02f;
        return true;
    }

    static Transform Held(Transform hand, string name, GameObject prefab)
    {
        Transform old = hand.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        Transform held = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, hand)).transform;
        held.name = name;
        foreach (Transform t in held.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = hand.gameObject.layer;
        return held;
    }

    // The spear in the right fist, its head out of the little-finger side (an overhand throwing grip); thrown from the hand.
    static (Transform, GameObject) HoldSpear(Transform model, GameObject spearPrefab)
    {
        var animator = model.GetComponent<Animator>();
        if (!HandFrame(animator, true, out Transform hand, out Vector3 up, out Vector3 fingers, out Vector3 grip))
            throw new Exception("the avatar has no right hand finger bones to hold the spear");
        Transform spear = Held(hand, "Spear", spearPrefab);
        spear.SetPositionAndRotation(grip, Quaternion.LookRotation(-up, fingers));
        spear.localScale = Vector3.one * (0.8f / Mathf.Max(0.0001f, hand.lossyScale.x));
        return (hand, spear.gameObject);
    }

    // The bow in the left fist, limbs along the fist; arrows leave from it.
    static (Transform, GameObject) HoldBow(Transform model, GameObject bowPrefab)
    {
        var animator = model.GetComponent<Animator>();
        if (!HandFrame(animator, false, out Transform hand, out Vector3 up, out Vector3 fingers, out Vector3 grip))
            throw new Exception("the avatar has no left hand finger bones to hold the bow");
        Transform bow = Held(hand, "Bow", bowPrefab);
        bow.SetPositionAndRotation(grip, Quaternion.LookRotation(up, fingers));
        bow.localScale = Vector3.one * (1f / Mathf.Max(0.0001f, hand.lossyScale.x));
        return (hand, null);
    }

    // ---- raid ----

    // Adds the ranged kinds to the nightly plan: a spear pair in wave 2, a spear and an archer group in wave 3.
    public static string Raid()
    {
        var plan = Load<RaidPlan>("Assets/_Game/Data/Raids/Night_Default.asset");
        var spear = Load<EnemySpawnCard>(Enemies + "GoblinSpear/GoblinSpear_Card.asset");
        var archer = Load<EnemySpawnCard>(Enemies + "GoblinArcher/GoblinArcher_Card.asset");
        var so = new SerializedObject(plan);
        SerializedProperty waves = so.FindProperty("Waves");
        var added = new List<string>();
        void Add(int wave, EnemySpawnCard card, int count)
        {
            if (wave >= waves.arraySize)
                return;
            SerializedProperty entries = waves.GetArrayElementAtIndex(wave).FindPropertyRelative("Entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Card").objectReferenceValue == card)
                    return;
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("Card").objectReferenceValue = card;
            entry.FindPropertyRelative("Count").intValue = count;
            added.Add($"wave {wave + 1}: {count} {card.name}");
        }
        Add(1, spear, 2);
        Add(2, spear, 2);
        Add(2, archer, 2);
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(plan);
        return "raid plan: " + (added.Count > 0 ? string.Join(", ", added) : "already has them");
    }

    // ---- helpers ----

    static SoundID Id(string entity)
    {
        var asset = AssetDatabase.LoadAssetAtPath<AudioEntity>($"{AudioDir}/{entity}.asset");
        return asset != null ? new SoundID(asset) : default;
    }

    static string[] Range(string prefix, int count)
    {
        var paths = new string[count];
        for (int i = 0; i < count; i++)
            paths[i] = $"{prefix}{i + 1:00}.wav";
        return paths;
    }

    static void Entity(AudioAsset library, string name, float volume, float pitch, float pitchRandom, SpatialSetting spatial, params string[] clips)
    {
        string path = $"{AudioDir}/{name}.asset";
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
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
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

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void Folder(string path)
    {
        path = path.TrimEnd('/');
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
