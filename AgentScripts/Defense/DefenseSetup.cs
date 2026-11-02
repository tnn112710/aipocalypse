using System;
using System.Collections.Generic;
using System.Linq;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.DefenseSystem;
using Aipocalypse.Core.Effects;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.UI;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using HighlightPlus;
using Pathfinding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Builds the fence and gate content (DEFENSE.md): the Defense sounds, the wood chip / dust effects, the barrier definitions
// (wood fence, its spiked tier with thorns, wood gate), the look and segment prefabs, the repair hammer and the interact
// outline on the Player prefab (+ Highlight Plus' renderer feature), the breach alert in HUD.prefab, and the goblins'
// fence-breaking data. Every entry is safe to re-run. Placing them in WorldMap is the generator's (Landmarks.Fence).
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
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string RendererPath = "Assets/_Game/Settings/PC_Renderer.asset";
    const string HammerPath = "Assets/_StoreAssets/Env/Animpic/Polygon-Mega Survival Tools/Prefabs/SM_Hammer.prefab";
    const string GripPosePath = "Assets/_Game/Data/Hands/Poses/Grip.asset";
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

    public static object All() => string.Join("\n", Audio(), Effects(), Content(), Player(), Hud(), EnemyData());

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
        WoodEffect(FxDir + "/WoodHit.prefab", chip, dust, 9, 0.9f, 1.4f);
        WoodEffect(FxDir + "/WoodBreak.prefab", chip, dust, 28, 2.2f, 2.5f);
        return $"effects: {FxDir}/WoodHit, WoodBreak";
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
        Define(spiked, "Spiked fence", 450f, 30f, fenceLook, hit, broke, "Fence");
        spiked.OnStruck = new List<BarrierEffect> { thorns };
        spiked.UpgradesTo = null;
        Save(spiked);

        var fence = Asset<BarrierDefinition>(FenceDir + "/WoodFence.asset");
        Define(fence, "Fence", 300f, 30f, fenceLook, hit, broke, "Fence");
        fence.OnStruck = new List<BarrierEffect>();
        fence.UpgradesTo = spiked;
        Save(fence);

        var gate = Asset<BarrierDefinition>(GateDir + "/WoodGate.asset");
        Define(gate, "Gate", 500f, 40f, gateLook, hit, broke, "Gate");
        gate.OnStruck = new List<BarrierEffect>();
        gate.UpgradesTo = null;
        Save(gate);

        var wide = Asset<BarrierDefinition>(GateDir + "/WoodGate_Double.asset");
        Define(wide, "Gate", 700f, 50f, doubleLook, hit, broke, "Gate");
        wide.OnStruck = new List<BarrierEffect>();
        wide.UpgradesTo = null;
        Save(wide);

        Segment(FenceDir + "/WoodFence.prefab", fence, fenceLook, isGate: false);
        Segment(GateDir + "/WoodGate.prefab", gate, gateLook, isGate: true);
        Segment(GateDir + "/WoodGate_Double.prefab", wide, doubleLook, isGate: true, slots: 2);
        log.Add("definitions: WoodFence (300, → Spiked 450 + thorns 8), WoodGate (500), WoodGate_Double (700); prefabs WoodFence, WoodGate (1 slot), WoodGate_Double (2 slots)");
        return string.Join("\n", log);
    }

    static void Define(BarrierDefinition definition, string name, float health, float repair, BarrierLook look, PooledEffect hit, PooledEffect broke,
        string kind)
    {
        definition.DisplayName = name;
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
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BarrierLook>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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

            if (isGate)
            {
                // Looking into the open doorway still finds the gate ("Close Gate"); bullets and bodies ignore triggers.
                var doorway = new GameObject("Doorway");
                doorway.transform.SetParent(root.transform, false);
                var trigger = doorway.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 1f, 0f);
                trigger.size = new Vector3(width - 0.4f, 2f, 0.6f);
                doorway.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
            }

            var segment = root.AddComponent<BarrierSegment>();
            segment.Setup(definition, root.transform, lookInstance.GetComponent<BarrierLook>(), cut, blocker);
            if (isGate)
                root.AddComponent<FenceGate>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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
            solid?.Add(collider);
        }
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            if (!renderer.GetComponent<Collider>() && !renderer.TryGetComponent(out RecastNavmeshModifier _))
                renderer.gameObject.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
    }

    // ---------------- player: repair hammer and interact outline ----------------

    public static string Player()
    {
        var log = new List<string>();
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var player = root.GetComponent<Player>();
            Transform hand = root.transform.Find("CameraRoot/CameraPitch/HeldItems/Hand") ?? throw new Exception("no HeldItems/Hand on the Player");
            Transform stale = hand.Find("RepairHammer");
            if (stale != null)
                Object.DestroyImmediate(stale.gameObject);
            RepairTool tool = Hammer(hand);
            HighlightEffect highlight = Highlight(root.transform);
            var so = new SerializedObject(player);
            so.FindProperty("_repairTool").objectReferenceValue = tool;
            so.FindProperty("_interactHighlight").objectReferenceValue = highlight;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath, out bool saved);
            log.Add(saved ? "Player.prefab: RepairHammer under HeldItems, Interact Highlight" : "failed to save " + PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Bake the grip in the saved prefab, so the fit lands on the asset.
        GameObject again = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            HandGrip grip = again.GetComponentsInChildren<HandGrip>(true).FirstOrDefault(g => g.name == "Grip_R" && g.GetComponentInParent<RepairTool>(true) != null);
            if (grip != null)
            {
                Type fit = Type.GetType("Aipocalypse.Core.HandSystem.HandFit, Aipocalypse.Core");
                try
                {
                    fit?.GetMethod("Bake")?.Invoke(null, new object[] { grip });
                    log.Add("hammer grip baked");
                }
                catch (Exception e)
                {
                    log.Add("hammer grip NOT baked: " + (e.InnerException ?? e).Message);
                }
                PrefabUtility.SaveAsPrefabAsset(again, PlayerPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(again);
        }
        log.Add(Renderer());
        return string.Join("\n", log);
    }

    // RepairHammer: pose markers + PosePivot → Model (the hammer mesh, no collider) → Grip_R. Held right-handed, head up.
    // Beside the hand node at its rest pose, not under it: the hand is lowered out of view while the held item is put away.
    static RepairTool Hammer(Transform hand)
    {
        Transform old = hand.parent.Find("RepairHammer");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        int layer = hand.gameObject.layer;
        var root = new GameObject("RepairHammer") { layer = layer };
        root.transform.SetParent(hand.parent, false);
        root.transform.SetLocalPositionAndRotation(hand.localPosition, hand.localRotation);

        Transform poses = Child(root.transform, "Poses");
        Transform hidden = Marker(poses, "Hidden", new Vector3(0.05f, -0.45f, -0.1f), new Vector3(40f, 0f, 0f));
        Transform idle = Marker(poses, "Idle", new Vector3(0f, -0.02f, 0f), new Vector3(-15f, -10f, 8f));
        Transform windUp = Marker(poses, "WindUp", new Vector3(0.02f, 0.1f, -0.06f), new Vector3(-55f, -12f, 12f));
        Transform strike = Marker(poses, "Strike", new Vector3(-0.04f, -0.12f, 0.16f), new Vector3(55f, -8f, 4f));

        Transform pivot = Child(root.transform, "PosePivot");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(HammerPath) ?? throw new Exception("missing " + HammerPath);
        var model = new GameObject("Model") { layer = layer };
        model.transform.SetParent(pivot, false);
        model.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        model.transform.localScale = Vector3.one * 1.3f;
        model.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var renderer = model.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        var gripGo = new GameObject("Grip_R") { layer = layer };
        gripGo.transform.SetParent(model.transform, false);
        // Lower handle, Z up the handle (little finger → index).
        float handleY = source.GetComponent<MeshFilter>().sharedMesh.bounds.min.y + 0.07f;
        gripGo.transform.localPosition = new Vector3(0f, handleY, 0f);
        gripGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var grip = gripGo.AddComponent<HandGrip>();
        var gso = new SerializedObject(grip);
        gso.FindProperty("_side").intValue = (int)HandSide.Right;
        gso.FindProperty("_pose").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HandPose>(GripPosePath);
        gso.ApplyModifiedPropertiesWithoutUndo();

        var hands = root.AddComponent<ItemHands>();
        var hso = new SerializedObject(hands);
        hso.FindProperty("_right").objectReferenceValue = grip;
        hso.FindProperty("_left").objectReferenceValue = null;
        hso.FindProperty("_handScale").floatValue = 1.12f;
        hso.ApplyModifiedPropertiesWithoutUndo();

        var tool = root.AddComponent<RepairTool>();
        tool.Setup(pivot, hidden, idle, windUp, strike, hands);
        pivot.SetLocalPositionAndRotation(idle.localPosition, idle.localRotation);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
        root.SetActive(false);
        return tool;
    }

    // One outline effect on the player, moved to whatever is in focus: warm white, thin, no glow, fades quickly.
    static HighlightEffect Highlight(Transform player)
    {
        Transform old = player.Find("Interact Highlight");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Interact Highlight");
        go.transform.SetParent(player, false);
        var effect = go.AddComponent<HighlightEffect>();
        effect.effectGroup = TargetOptions.Children;
        effect.outline = 1f;
        effect.outlineColor = new Color(1f, 0.86f, 0.62f, 1f);
        effect.outlineWidth = 0.6f;
        effect.outlineQuality = HighlightPlus.QualityLevel.High;
        effect.constantWidth = true;
        effect.glow = 0f;
        effect.overlay = 0f;
        effect.innerGlow = 0f;
        effect.seeThrough = SeeThroughMode.Never;
        effect.fadeInDuration = 0.1f;
        effect.fadeOutDuration = 0.15f;
        return effect;
    }

    static string Renderer()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath) ?? throw new Exception("missing " + RendererPath);
        if (data.rendererFeatures.Any(f => f is HighlightPlusRenderPassFeature))
            return "PC_Renderer: Highlight Plus feature already there";
        var feature = ScriptableObject.CreateInstance<HighlightPlusRenderPassFeature>();
        feature.name = "HighlightPlus";
        AssetDatabase.AddObjectToAsset(feature, data);
        var so = new SerializedObject(data);
        SerializedProperty features = so.FindProperty("m_RendererFeatures");
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(data);
        return "PC_Renderer: Highlight Plus feature added";
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
            text.text = style.FenceText + style.BehindSuffix;

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
        var scope = Object.FindObjectsByType<Aipocalypse.Core.Bootstrap.GameLifetimeScope>(FindObjectsInactive.Include)
            .FirstOrDefault(s => s.gameObject.scene == scene) ?? throw new Exception("no GameLifetimeScope in " + scene.path);
        if (!scope.TryGetComponent(out DefenseInstaller installer))
            installer = scope.gameObject.AddComponent<DefenseInstaller>();
        installer.CollectSceneBarriers();

        var log = new List<string>();
        foreach (var zone in Object.FindObjectsByType<Aipocalypse.Core.SpawnSystem.SpawnZone>(FindObjectsInactive.Include))
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
