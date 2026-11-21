using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.UI;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Editor.Localization;
using Pathfinding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Builds the fence and gate content (DEFENSE.md): the Defense sounds, the wood chip / dust effects, the barrier definitions
// (wood fence, its spiked tier with thorns, wood gates), the look and segment prefabs, the breach alert in HUD.prefab, and the goblins'
// fence-breaking data. Every entry is safe to re-run. Placing them in WorldMap is the generator's (Landmarks.Fence); the
// repair hammer is HammerSetup's.
// Run: unity command script run --file AgentScripts/Defense/DefenseSetup.cs --entry DefenseSetup.All
public static class DefenseSetup
{
    const string LV = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Prefabs/";
    const string PHE = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Environment Sounds/";
    const string GM = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string Structures = "Assets/_Game/Content/Structures";
    const string FenceDir = Structures + "/WoodFence";
    const string GateDir = Structures + "/WoodGate";
    const string FxDir = Structures + "/_Shared/Effects";
    const string MatDir = Structures + "/_Shared/Materials";
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string DefenseLib = AudioDir + "/Defense.asset";
    const string DefenseAudio = AudioDir + "/Defense";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string RendererPath = "Assets/_Game/Settings/PC_Renderer.asset";
    const string PromptStylePath = "Assets/_Game/Data/UI/InteractPromptStyle.asset";
    const string BreachStylePath = "Assets/_Game/Data/UI/BreachAlertStyle.asset";
    const string SystemSettings = "Assets/_Game/Data/Enemies/EnemySystemSettings.asset";
    const string GoblinDefinition = "Assets/_Game/Content/Enemies/Goblin/Goblin.asset";
    const string GoblinMelee = "Assets/_Game/Content/Enemies/Goblin/Goblin_Melee.asset";

    // Metres. A segment fills one panel slot (the generator scales X to the real slot); panels are raised to about 2 m.
    public const float PanelWidth = 2.25f;
    const float PanelHeightScale = 1.35f;
    const float BlockerHeight = 2.4f;
    const float BlockerDepth = 0.3f;
    const float AgentRadius = 0.5f;           // the WorldMap bake's agent radius (Navigation.Bake)
    const float CutOverlap = 0.3f;
    const float GatePostScale = 2f;            // FenceEnd_1: 1.35 m tall at 1 → 2.7 m
    const float GatePostWidth = 1.9f;
    const float LeafMeshLength = 1.606f;       // Extension_1_Fence
    const float LeafRowHeight = 0.93f;         // two rails stacked: 0.85 m mesh × 1.1
    const string IgnoreRaycast = "Ignore Raycast";
    const string DoorwayName = "Doorway";
    const string GapName = "Gap";

    public static object All() => string.Join("\n", Audio(), Effects(), Content(), Hud(), EnemyData(), LocalizationKit.ApplyLabels());

    // ---------------- sounds ----------------

    public static string Audio()
    {
        var log = new List<string>();
        Folder(DefenseAudio);
        AudioAsset library = Library(DefenseLib, "Defense", log);
        SpatialSetting spatial = Spatial(DefenseAudio + "/Defense_Spatial.asset", 2f, 45f);
        AudioEntity e;
        e = Entity(library, "Fence_Hit", 0.85f, 1f, 0.1f, Numbered(PHE + "Wood/Wood Chop 1-", 1, 9));
        Finish(e, spatial, Group(DefenseAudio + "/Fence_Hit_Group.asset", 4), log);
        e = Entity(library, "Fence_Break", 1f, 1f, 0.06f, PHE + "Wood/Wood 11-1.wav", PHE + "Wood/Wood 11-3.wav", PHE + "Wood/Wood 16-1.wav");
        Finish(e, spatial, null, log);
        e = Entity(library, "Fence_Repair", 0.75f, 1f, 0.08f, Numbered(PHE + "Crafting/Nail Wood 1-", 6, 10));
        Finish(e, spatial, Group(DefenseAudio + "/Fence_Repair_Group.asset", 2), log);
        e = Entity(library, "Gate_Open", 0.7f, 1f, 0.06f, GM + "Doors/door_old_wooden_squeak_02.wav", GM + "Doors/door_old_wooden_squeak_04.wav",
            GM + "Doors/door_old_wooden_squeak_07.wav");
        Finish(e, spatial, null, log);
        e = Entity(library, "Gate_Close", 0.85f, 1f, 0.05f, Enumerable.Range(1, 5).Select(i => $"{GM}Doors/door_old_wooden_close_thud_{i:00}.wav").ToArray());
        Finish(e, spatial, null, log);
        Save(library);
        return string.Join("\n", log);
    }

    // ---------------- effects ----------------

    public static string Effects()
    {
        Folder(FxDir);
        Folder(MatDir);
        Material chip = ParticleMaterial(MatDir + "/WoodChip.mat", null, transparent: false);
        Material dust = ParticleMaterial(MatDir + "/WoodDust.mat",
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_StoreAssets/VFX/GabrielAguiarProductions/Textures/Circle001.png"), transparent: true);
        Material plank = LitParticleMaterial(MatDir + "/WoodPlank.mat", new Color(0.66f, 0.43f, 0.26f));
        WoodEffect(FxDir + "/WoodHit.prefab", chip, dust, 9, 0.9f, 1.4f);
        WoodBreakEffect(FxDir + "/WoodBreak.prefab", plank, chip, dust);
        return $"effects: {FxDir}/WoodHit, WoodBreak";
    }

    // A panel bursting: planks that tumble and bounce on the ground, a spray of splinters and a dust cloud, all emitted
    // from a 1 m wide box that BarrierSegment stretches to the barrier's width (Shape scaling), centred 0.8 m up.
    static void WoodBreakEffect(string path, Material plank, Material chip, Material dust)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            var pooled = root.AddComponent<PooledEffect>();
            var so = new SerializedObject(pooled);
            so.FindProperty("_lifetime").floatValue = 4f;
            so.FindProperty("_maxActive").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();
            var box = new Vector3(1f, 1.4f, 0.15f);

            ParticleSystem planks = Particles("Planks", root.transform, plank);
            var main = planks.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.75f, 0.75f), Color.white);
            main.gravityModifier = 1.3f;
            var emission = planks.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            Box(planks, box, 0.6f);
            // Spins in the air and settles once down (world collision has no rolling friction).
            var spin = planks.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            var settle = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 1f), new Keyframe(0.2f, 0f), new Keyframe(1f, 0f));
            spin.x = new ParticleSystem.MinMaxCurve(10f, settle);
            spin.y = new ParticleSystem.MinMaxCurve(4f, settle);
            spin.z = new ParticleSystem.MinMaxCurve(8f, settle);
            Shrink(planks, 0.85f);
            var collision = planks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            // High raycasts; Medium's voxel cache let them fall through the terrain.
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.collidesWith = LayerMask.GetMask("Default");
            collision.dampen = 0.55f;
            collision.bounce = 0.3f;
            collision.radiusScale = 0.3f;
            var renderer = planks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            ParticleSystem splinters = Particles("Splinters", root.transform, chip);
            main = splinters.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.3f, 0.17f), new Color(0.72f, 0.55f, 0.34f));
            main.gravityModifier = 1.6f;
            emission = splinters.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            Box(splinters, box, 0.7f);
            var rotation = splinters.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-10f, 10f);

            ParticleSystem cloud = Particles("Dust", root.transform, dust);
            main = cloud.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.56f, 0.48f, 0.38f);
            main.gravityModifier = -0.02f;
            emission = cloud.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10), new ParticleSystem.Burst(0.08f, 6) });
            Box(cloud, new Vector3(1f, 1.4f, 0.4f), 0.3f);
            var drag = cloud.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.5f;
            var size = cloud.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.8f));
            var color = cloud.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Emits from the box's volume, flung out both ways across the barrier (local Z) with some spread.
    static void Box(ParticleSystem particles, Vector3 scale, float spread)
    {
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = scale;
        shape.randomDirectionAmount = spread;
    }

    // Full size, then shrinking away over the last part of its life.
    static void Shrink(ParticleSystem particles, float from)
    {
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(from, 1f), new Keyframe(1f, 0f)));
    }

    static Material LitParticleMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Smoothness", 0.1f);
        Save(material);
        return material;
    }

    // Brown chips thrown out with gravity, and a soft grey puff.
    static void WoodEffect(string path, Material chip, Material dust, int chips, float dustSize, float lifetime)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            var pooled = root.AddComponent<PooledEffect>();
            var so = new SerializedObject(pooled);
            so.FindProperty("_lifetime").floatValue = lifetime;
            so.ApplyModifiedPropertiesWithoutUndo();

            ParticleSystem chipsPs = Particles("Chips", root.transform, chip);
            var main = chipsPs.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.3f, 0.17f), new Color(0.7f, 0.52f, 0.32f));
            main.gravityModifier = 1.6f;
            var emission = chipsPs.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)chips) });
            var shape = chipsPs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.05f;
            var rotation = chipsPs.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            ParticleSystem dustPs = Particles("Dust", root.transform, dust);
            main = dustPs.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustSize * 0.4f, dustSize * 0.7f);
            main.startColor = new Color(0.62f, 0.56f, 0.48f, 0.45f);
            main.gravityModifier = -0.05f;
            emission = dustPs.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(3, chips / 4)) });
            shape = dustPs.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = dustSize * 0.15f;
            var size = dustPs.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
            var color = dustPs.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static ParticleSystem Particles(string name, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    static Material ParticleMaterial(string path, Texture2D texture, bool transparent)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", transparent ? 1f : 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
        material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        if (transparent)
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        else
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
        Save(material);
        return material;
    }

    // ---------------- definitions and prefabs ----------------

    public static string Content()
    {
        var log = new List<string>();
        Folder(FenceDir);
        Folder(GateDir);
        var hit = AssetDatabase.LoadAssetAtPath<PooledEffect>(FxDir + "/WoodHit.prefab");
        var broke = AssetDatabase.LoadAssetAtPath<PooledEffect>(FxDir + "/WoodBreak.prefab");

        BarrierLook fenceLook = FenceLook(FenceDir + "/WoodFence_Look.prefab");
        BarrierLook gateLook = GateLook(GateDir + "/WoodGate_Look.prefab", 1);
        BarrierLook doubleLook = GateLook(GateDir + "/WoodGate_Double_Look.prefab", 2);

        var thorns = Asset<ThornsEffect>(FenceDir + "/WoodFence_Thorns.asset");
        thorns.Damage = 8f;
        Save(thorns);

        var spiked = Asset<BarrierDefinition>(FenceDir + "/WoodFence_Spiked.asset");
        Define(spiked, "barrier.spiked_fence", 450f, 30f, fenceLook, hit, broke, "Fence");
        spiked.OnStruck = new List<BarrierEffect> { thorns };
        spiked.UpgradesTo = null;
        Save(spiked);

        var fence = Asset<BarrierDefinition>(FenceDir + "/WoodFence.asset");
        Define(fence, "barrier.fence", 300f, 30f, fenceLook, hit, broke, "Fence");
        fence.OnStruck = new List<BarrierEffect>();
        fence.UpgradesTo = spiked;
        Save(fence);

        var gate = Asset<BarrierDefinition>(GateDir + "/WoodGate.asset");
        Define(gate, "barrier.gate", 500f, 40f, gateLook, hit, broke, "Gate");
        gate.OnStruck = new List<BarrierEffect>();
        gate.UpgradesTo = null;
        Save(gate);

        var wide = Asset<BarrierDefinition>(GateDir + "/WoodGate_Double.asset");
        Define(wide, "barrier.gate", 700f, 50f, doubleLook, hit, broke, "Gate");
        wide.OnStruck = new List<BarrierEffect>();
        wide.UpgradesTo = null;
        Save(wide);

        Segment(FenceDir + "/WoodFence.prefab", fence, fenceLook, isGate: false);
        Segment(GateDir + "/WoodGate.prefab", gate, gateLook, isGate: true);
        Segment(GateDir + "/WoodGate_Double.prefab", wide, doubleLook, isGate: true, slots: 2);
        log.Add("definitions: WoodFence (300, → Spiked 450 + thorns 8), WoodGate (500), WoodGate_Double (700); prefabs WoodFence, WoodGate (1 slot), WoodGate_Double (2 slots)");
        return string.Join("\n", log);
    }

    // `nameKey`: its name in the Interaction table.
    static void Define(BarrierDefinition definition, string nameKey, float health, float repair, BarrierLook look, PooledEffect hit, PooledEffect broke,
        string kind)
    {
        definition.DisplayName = LocalizationKit.Ref(Loc.InteractionTable, nameKey);
        definition.MaxHealth = health;
        definition.DamageScale = new Dictionary<DamageType, float>();
        definition.RepairPerKnock = repair;
        definition.StandAt = 0.3f;
        definition.Look = look;
        definition.HitEffect = hit;
        definition.BreakEffect = broke;
        definition.RepairEffect = hit;
        definition.HitSound = Id("Fence_Hit");
        definition.BreakSound = Id("Fence_Break");
        definition.RepairSound = Id("Fence_Repair");
        definition.OpenSound = kind == "Gate" ? Id("Gate_Open") : default;
        definition.CloseSound = kind == "Gate" ? Id("Gate_Close") : default;
    }

    // Pivot at the base, local X along the fence; the three Lakefell panels as variants, raised to about 2 m.
    static BarrierLook FenceLook(string path)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            Transform pivot = Child(root.transform, "Pivot");
            Transform shake = Child(pivot, "Shake");
            var variants = new List<GameObject>();
            var solid = new List<Collider>();
            for (int i = 1; i <= 3; i++)
            {
                GameObject panel = Nest(LV + $"Environment/Fence_{i}.prefab", shake);
                panel.transform.localScale = new Vector3(1f, PanelHeightScale, 1f);
                ExcludeFromNavmesh(panel, solid);
                variants.Add(panel);
            }
            var look = root.AddComponent<BarrierLook>();
            var so = new SerializedObject(look);
            SetArray(so.FindProperty("_pivots"), new[] { pivot });
            SetArray(so.FindProperty("_shakes"), new[] { shake });
            SetArray(so.FindProperty("_variants"), variants);
            SetArray(so.FindProperty("_solid"), solid);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debris(look);
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BarrierLook>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Adds (or rebuilds) the debris on the existing fence look prefab, without touching the rest of the content.
    public static string FenceDebris()
    {
        string path = FenceDir + "/WoodFence_Look.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Debris(root.GetComponent<BarrierLook>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return "fence debris: a few loose planks on " + path;
    }

    // What lies in the gap while a panel is down (start of game, or after it fell): a few loose planks on the ground, no
    // colliders, outside the pivot so the model can be hidden on its own. Local X runs along the fence.
    static void Debris(BarrierLook look)
    {
        Transform old = look.transform.Find("Debris");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        Transform debris = Child(look.transform, "Debris");
        Material wood = DebrisMaterial();
        // Only a hint (user, 2026-11-22): small, but propped up enough to peek out of the knee-high lawn. A short rail with
        // one end up, a board leaning across the line, a snapped stub and a loose board.
        (Vector3 at, Vector3 size, Vector3 euler)[] planks =
        {
            (new Vector3(-0.1f, 0.12f, 0.04f), new Vector3(0.8f, 0.06f, 0.1f), new Vector3(0f, 6f, 13f)),
            (new Vector3(0.45f, 0.15f, -0.1f), new Vector3(0.11f, 0.4f, 0.04f), new Vector3(-50f, 12f, 8f)),
            (new Vector3(-0.42f, 0.1f, 0.02f), new Vector3(0.1f, 0.22f, 0.04f), new Vector3(-8f, 0f, -6f)),
            (new Vector3(0.2f, 0.05f, 0.22f), new Vector3(0.45f, 0.05f, 0.11f), new Vector3(0f, -22f, 0f)),
        };
        for (int i = 0; i < planks.Length; i++)
        {
            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = $"Plank_{i}";
            Object.DestroyImmediate(plank.GetComponent<Collider>());
            plank.transform.SetParent(debris, false);
            plank.transform.SetLocalPositionAndRotation(planks[i].at, Quaternion.Euler(planks[i].euler));
            plank.transform.localScale = planks[i].size;
            plank.GetComponent<MeshRenderer>().sharedMaterial = wood;
        }
        debris.gameObject.SetActive(false);
        var so = new SerializedObject(look);
        so.FindProperty("_debris").objectReferenceValue = debris.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Plain lit wood in the break effect's plank colour (WoodPlank.mat is a particle material).
    static Material DebrisMaterial()
    {
        string path = MatDir + "/FenceDebris.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", new Color(0.6f, 0.39f, 0.24f));
        material.SetFloat("_Smoothness", 0.1f);
        Save(material);
        return material;
    }

    // A taller frame than the fence (2.7 m posts and a beam across the top) over `slots` panel slots, round two-rail plank
    // leaves: one hinged at -X, or for a double gate (two slots) two meeting in the middle, each hinged on its own post.
    static BarrierLook GateLook(string path, int slots)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            float half = PanelWidth * slots * 0.5f;
            var solid = new List<Collider>();
            Transform frame = Child(root.transform, "Frame");
            foreach (float x in new[] { -half, half })
            {
                GameObject post = Nest(LV + "Environment/FenceEnd_1.prefab", frame);
                post.name = x < 0f ? "Post_Left" : "Post_Right";
                post.transform.localPosition = new Vector3(x, -0.05f, 0f);
                post.transform.localScale = new Vector3(GatePostWidth, GatePostScale, GatePostWidth);
                ExcludeFromNavmesh(post, null);
            }
            GameObject beam = Nest(LV + "Environment/FenceEnd_1.prefab", frame);
            beam.name = "Beam";
            beam.transform.localPosition = new Vector3(-half - 0.25f, 2.5f, 0f);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            beam.transform.localScale = new Vector3(GatePostWidth * 0.85f, (2f * half + 0.5f) / 1.35f, GatePostWidth * 0.85f);
            ExcludeFromNavmesh(beam, null);

            int leaves = slots >= 2 ? 2 : 1;
            float leafLength = 2f * half / leaves - 0.12f - (leaves == 2 ? 0.02f : 0.12f);
            var hinges = new List<Transform>();
            var pivots = new List<Transform>();
            var shakes = new List<Transform>();
            for (int l = 0; l < leaves; l++)
            {
                // The left leaf runs along +X from its hinge, the right one back along -X.
                float along = l == 0 ? 1f : -1f;
                Transform hinge = Child(root.transform, leaves == 1 ? "Hinge" : l == 0 ? "Hinge_Left" : "Hinge_Right");
                hinge.localPosition = new Vector3(-along * (half - 0.12f), 0.06f, 0f);
                Transform pivot = Child(hinge, "Leaf");
                Transform shake = Child(pivot, "Shake");
                for (int row = 0; row < 2; row++)
                {
                    GameObject rail = Nest(LV + "Modules/Extension_1_Fence.prefab", shake);
                    rail.name = row == 0 ? "Rails_Low" : "Rails_High";
                    rail.transform.localPosition = new Vector3(along * leafLength * 0.5f, row * LeafRowHeight, 0f);
                    rail.transform.localScale = new Vector3(leafLength / LeafMeshLength, 1.1f, 1.6f);
                    PlankColliders(rail);
                    ExcludeFromNavmesh(rail, solid);
                }
                hinges.Add(hinge);
                pivots.Add(pivot);
                shakes.Add(shake);
            }
            var look = root.AddComponent<BarrierLook>();
            var so = new SerializedObject(look);
            SetArray(so.FindProperty("_pivots"), pivots);
            SetArray(so.FindProperty("_shakes"), shakes);
            SetArray(so.FindProperty("_hinges"), hinges);
            so.FindProperty("_flipVariants").boolValue = false;
            SetArray(so.FindProperty("_solid"), solid);
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BarrierLook>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Root: local X along the fence, Z across it. A navmesh cut and a tall climb blocker cover its `slots` panel slots.
    static void Segment(string path, BarrierDefinition definition, BarrierLook look, bool isGate, int slots = 1)
    {
        float width = PanelWidth * slots;
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            var lookInstance = (GameObject)PrefabUtility.InstantiatePrefab(look.gameObject, root.transform);
            lookInstance.name = look.name;

            var blockerGo = new GameObject("Blocker") { layer = LayerMask.NameToLayer(IgnoreRaycast) };
            blockerGo.transform.SetParent(root.transform, false);
            var blocker = blockerGo.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, BlockerHeight * 0.5f, 0f);
            blocker.size = new Vector3(width, BlockerHeight, BlockerDepth);

            var cut = root.AddComponent<NavmeshCut>();
            cut.type = NavmeshCut.MeshType.Box;
            // A* widens the cut by the agent radius all round: shortened by nearly that at both ends, neighbouring cuts still
            // overlap by CutOverlap (cuts that only touch leak at their corners) and a broken panel leaves most of its width open.
            cut.rectangleSize = new Vector2(width - 2f * AgentRadius + CutOverlap, BlockerDepth);
            cut.height = 3f;
            cut.center = new Vector3(0f, 0.9f, 0f);
            cut.useRotationAndScale = true;
            cut.radiusExpansionMode = NavmeshCut.RadiusExpansionMode.ExpandByAgentRadius;

            ReachTrigger(root.transform, isGate ? DoorwayName : GapName, width);

            var segment = root.AddComponent<BarrierSegment>();
            segment.Setup(definition, root.transform, lookInstance.GetComponent<BarrierLook>(), cut, blocker);
            if (isGate)
                root.AddComponent<FenceGate>();
            // Saves find each placed segment by it (its id is generated per scene instance).
            root.AddComponent<MB13.Aipocalypse.Core.SaveSystem.SaveId>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Looking into an open doorway or a broken segment's gap (its colliders are off) still finds the segment ("Close Gate",
    // "Rebuild Fence"); bullets and bodies ignore triggers.
    static void ReachTrigger(Transform root, string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, 0f);
        trigger.size = new Vector3(width - 0.4f, 2f, 0.6f);
        go.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
    }

    // Adds the Gap trigger to an existing WoodFence.prefab without rebuilding it (Content would, and the scene's fence after it).
    public static string FenceGap()
    {
        string path = FenceDir + "/WoodFence.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find(GapName) != null)
                return "WoodFence: Gap already there";
            ReachTrigger(root.transform, GapName, PanelWidth);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "WoodFence: Gap trigger added";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Lakefell's leaf has one solid box over its planks, which would stop bullets in the gaps: off, and a mesh collider on the
    // LOD0 planks instead, so shots and the interact cast go between them (the closed gate is still blocked by its Blocker).
    static void PlankColliders(GameObject rail)
    {
        foreach (BoxCollider box in rail.GetComponents<BoxCollider>())
            box.enabled = false;
        MeshFilter planks = rail.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.EndsWith("_LOD0"));
        if (!planks.TryGetComponent(out MeshCollider mesh))
            mesh = planks.gameObject.AddComponent<MeshCollider>();
        mesh.sharedMesh = planks.sharedMesh;
    }

    // The fence's own colliders stay out of the baked navmesh (the segment's cut stands for it, and goes when it breaks).
    static void ExcludeFromNavmesh(GameObject model, List<Collider> solid)
    {
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.TryGetComponent(out RecastNavmeshModifier modifier))
                modifier = collider.gameObject.AddComponent<RecastNavmeshModifier>();
            modifier.includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
            modifier.dynamic = true;
            if (collider.enabled)
                solid?.Add(collider);
        }
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            if (!renderer.GetComponent<Collider>() && !renderer.TryGetComponent(out RecastNavmeshModifier _))
                renderer.gameObject.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
    }

    // ---------------- HUD ----------------

    public static string Hud()
    {
        var style = Asset<BreachAlertStyle>(BreachStylePath);
        Save(style);
        var font = AssetDatabase.LoadAssetAtPath<InteractPromptStyle>(PromptStylePath)?.Font;
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find("BreachAlert");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("BreachAlert", typeof(RectTransform));
            node.layer = root.layer;
            var rect = (RectTransform)node.transform;
            rect.SetParent(root.transform, false);
            // Under the raid banner's band, above the crosshair.
            rect.anchorMin = new Vector2(0f, 0.56f);
            rect.anchorMax = new Vector2(1f, 0.62f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Transform banner = root.transform.Find("RaidBanner");
            if (banner != null)
                rect.SetSiblingIndex(banner.GetSiblingIndex() + 1);
            var group = node.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = root.layer;
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = 40f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = style.FenceText.Text() + style.BehindSuffix.Text();

            var hud = node.AddComponent<BreachAlertHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? $"HUD.prefab: BreachAlert, {BreachStylePath}" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------- WorldMap ----------------

    // DefenseInstaller on the scene scope (lists the barriers the generator placed), and the front raid zone's path check moved
    // out past the gate, then the scene saved. Run after the generator's Landmarks.Fence and Navigation.Bake.
    public static string Scene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scope = Object.FindObjectsByType<MB13.Aipocalypse.Core.Bootstrap.GameLifetimeScope>(FindObjectsInactive.Include)
            .FirstOrDefault(s => s.gameObject.scene == scene) ?? throw new Exception("no GameLifetimeScope in " + scene.path);
        if (!scope.TryGetComponent(out DefenseInstaller installer))
            installer = scope.gameObject.AddComponent<DefenseInstaller>();
        installer.CollectSceneBarriers();

        var log = new List<string>();
        foreach (var zone in Object.FindObjectsByType<MB13.Aipocalypse.Core.SpawnSystem.SpawnZone>(FindObjectsInactive.Include))
        {
            if (zone.gameObject.scene != scene || zone.Id != "Front")
                continue;
            var so = new SerializedObject(zone);
            so.FindProperty("_originOffset").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add($"zone {zone.name}: origin 3 m out, at {zone.Origin:F1}");
        }
        int barriers = new SerializedObject(installer).FindProperty("_sceneBarriers").arraySize;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        log.Add($"DefenseInstaller on {scope.name}: {barriers} barriers; scene {(saved ? "saved" : "NOT saved")}");
        return string.Join("\n", log);
    }

    // ---------------- enemies ----------------

    public static string EnemyData()
    {
        var settings = Load<EnemySystemSettings>(SystemSettings);
        settings.SetAttackersPerTarget(TargetKind.Fence, 3);
        Save(settings);
        var goblin = Load<EnemyDefinition>(GoblinDefinition);
        goblin.SetTargetWeight(TargetKind.Fence, 1f);
        Save(goblin);
        // Writes the new BreachCandidates field (5) into the YAML.
        Save(Load<MeleeSettings>(GoblinMelee));
        return "enemies: 3 attackers per fence segment, goblins break fences (weakest of the 5 nearest)";
    }

    // ---------------- helpers ----------------

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform Marker(Transform parent, string name, Vector3 position, Vector3 euler)
    {
        Transform marker = Child(parent, name);
        marker.SetLocalPositionAndRotation(position, Quaternion.Euler(euler));
        return marker;
    }

    static GameObject Nest(string prefabPath, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new Exception("missing " + prefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    static void SetArray<T>(SerializedProperty array, IReadOnlyList<T> items) where T : Object
    {
        array.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("missing " + path);

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static SoundID Id(string entity) => new(AssetDatabase.LoadAssetAtPath<AudioEntity>($"{DefenseAudio}/{entity}.asset"));

    static string[] Numbered(string prefix, int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(i => $"{prefix}{i}.wav").ToArray();

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
        log.Add("created library " + path);
        return library;
    }

    // A one-shot SFX entity with shuffled clips, a base pitch and a random pitch range.
    static AudioEntity Entity(AudioAsset library, string name, float volume, float pitch, float pitchRandom, params string[] clips)
    {
        string path = $"{DefenseAudio}/{name}.asset";
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
        var list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]) ?? throw new Exception("missing clip " + clips[i]);
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue = clip;
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return entity;
    }

    static void Finish(AudioEntity entity, SpatialSetting spatial, PlaybackGroup group, List<string> log)
    {
        var so = new SerializedObject(entity);
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
        so.FindProperty("_group").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        log.Add($"entity {AssetDatabase.GetAssetPath(entity)}");
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

    static PlaybackGroup Group(string path, int maxPlaying)
    {
        var group = AssetDatabase.LoadAssetAtPath<DefaultPlaybackGroup>(path);
        if (group == null)
        {
            group = ScriptableObject.CreateInstance<DefaultPlaybackGroup>();
            AssetDatabase.CreateAsset(group, path);
        }
        var so = new SerializedObject(group);
        so.FindProperty("_maxPlayableCount.Value").intValue = maxPlaying;
        so.FindProperty("_maxPlayableCount._isOverride").boolValue = true;
        so.FindProperty("_logCombFilteringWarning").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(group);
        return group;
    }
}
