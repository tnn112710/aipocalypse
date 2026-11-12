using System;
using System.Collections.Generic;
using System.Linq;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using Pathfinding;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the farm animals: the Animals BroAudio library, Data/Animals/AnimalSettings, the cow and sheep (definition,
// animator controller, model and entity prefabs from polyperfect Low Poly Animated Animals), the pen pieces (rail, post,
// gate, bell from Lakefell parts), and in WorldMap the "Home Pen" root (pen in the yard's west corner, pasture in front of
// the home gate, 2 cows and 4 sheep) plus the AnimalInstaller. Safe to re-run: animals already in the scene are kept.
// The tree is built apart: TreeKit.Build AgentScripts/Animals/Animal.tree.txt.
// Run: unity command script run --file AgentScripts/Animals/AnimalSetup.cs --entry AnimalSetup.All
public static class AnimalSetup
{
    const string PP = "Assets/_StoreAssets/Characters/polyperfect/Low Poly Animated Animals/";
    const string GM = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string LV = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Prefabs/";
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string AnimalAudioDir = AudioDir + "/Animals";
    const string DefenseAudioDir = AudioDir + "/Defense";
    const string SettingsPath = "Assets/_Game/Data/Animals/AnimalSettings.asset";
    const string ContentDir = "Assets/_Game/Content/Animals";
    const string TreePath = ContentDir + "/_Shared/Animal.asset";
    const string PenDir = "Assets/_Game/Content/Structures/Pen";
    const string TokensPath = "Assets/_Game/Content/Items/Tokens/Tokens.asset";
    const string TokensPrefabPath = "Assets/_Game/Content/Items/Tokens/Tokens.prefab";
    const string Template = "Assets/_Game/Content/Enemies/WanderCapsule/WanderCapsule.prefab";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string AnimalLayer = "Animal";
    const string PenRootName = "Home Pen";

    // Pen: this far along the two fence sides that meet in the yard's west corner (whole rail slots).
    const float SlotWidth = 2.25f;
    const int SlotsAlongBack = 4;       // along side 0 (towards the south corner): the side with the gate
    const int SlotsAlongSide = 5;       // along side 3 (towards the north corner)
    const int GateSlot = 1;             // counted from the big fence
    const float RailMeshLength = 1.606f;    // Extension_1_Fence
    const float AgentRadius = 0.5f;         // the WorldMap bake's agent radius
    const float CutOverlap = 0.3f;
    const float PastureDistance = 32f;      // from the raid spawn zone's origin (the home gate), along its facing
    const float PastureRadius = 16f;

    sealed class Kind
    {
        public string Name;
        public string[] Looks;              // vendor prefab names; the first is <Name>.prefab, the rest <Name>_<Look>.prefab
        public string ClipFbx;
        public string ClipPrefix;
        public string IdleClip = "Idle";
        public float ModelScale = 1f;
        public float AuthoredWalk, AuthoredRun;
        public float AgentRadius, AgentHeight;
        public float BodyRadius, BodyLength, BodyHeight;
        public float MarkerHeight;
        public Action<AnimalDefinition> Tune;
    }

    static readonly Kind Cow = new()
    {
        Name = "Cow",
        Looks = new[] { "Cow_White", "Cow_Brown" },
        ClipFbx = PP + "Meshes/Animals/Cow/SKM_Cow_Animations.fbx",
        ClipPrefix = "Cow_",
        IdleClip = "Idle1",
        ModelScale = 0.8f,
        AuthoredWalk = 1f,
        AuthoredRun = 4f,
        AgentRadius = 0.6f,
        AgentHeight = 1.5f,
        BodyRadius = 0.42f,
        BodyLength = 2.3f,
        BodyHeight = 0.85f,
        MarkerHeight = 2f,
        Tune = d =>
        {
            d.DisplayName = "Cow";
            d.WalkSpeed = 0.9f;
            d.HurrySpeed = 3f;
            d.StopDistance = 0.5f;
            d.GrassBendRadius = 1.2f;
            d.GrazeRadius = 10f;
            d.GrazeTime = new Vector2(5f, 12f);
            d.FollowsLeader = false;
            d.TokensPerDayPenned = 20f;
            d.TokensPerDayRoaming = 40f;
            d.HoldDays = 2f;
            d.CollectAt = 5;
            d.Call = Id(AnimalAudioDir, "Cow_Call");
            d.Hurried = Id(AnimalAudioDir, "Cow_Hurried");
            d.CallInterval = new Vector2(15f, 40f);
        },
    };

    static readonly Kind Sheep = new()
    {
        Name = "Sheep",
        Looks = new[] { "Sheep_Wool" },
        ClipFbx = PP + "Meshes/Animals/Sheep/SKM_Sheep_Wool_Animations.fbx",
        ClipPrefix = "Sheep_Wool_",
        AuthoredWalk = 0.8f,
        AuthoredRun = 2.8f,
        AgentRadius = 0.4f,
        AgentHeight = 1.1f,
        BodyRadius = 0.28f,
        BodyLength = 1.2f,
        BodyHeight = 0.55f,
        MarkerHeight = 1.6f,
        Tune = d =>
        {
            d.DisplayName = "Sheep";
            d.WalkSpeed = 1.1f;
            d.HurrySpeed = 3.2f;
            d.StopDistance = 0.4f;
            d.GrassBendRadius = 0.7f;
            d.GrazeRadius = 8f;
            d.GrazeTime = new Vector2(4f, 9f);
            d.FollowsLeader = true;
            d.FlockRadius = 5f;
            d.TokensPerDayPenned = 6f;
            d.TokensPerDayRoaming = 12f;
            d.HoldDays = 2f;
            d.CollectAt = 2;
            d.Call = Id(AnimalAudioDir, "Sheep_Call");
            d.Hurried = Id(AnimalAudioDir, "Sheep_Hurried");
            d.CallInterval = new Vector2(10f, 30f);
        },
    };

    public static string All() => string.Join("\n", Audio(), Content(), Pen(), World());

    // ---- Audio ----

    public static string Audio()
    {
        var log = new List<string>();
        Folder(AnimalAudioDir);
        AudioAsset library = Library(AudioDir + "/Animals.asset", "Animals", log);
        SpatialSetting near = Spatial(AnimalAudioDir + "/Animal_Spatial.asset", 2f, 45f);
        SpatialSetting far = Spatial(AnimalAudioDir + "/Bell_Spatial.asset", 5f, 120f);
        string voices = GM + "Animal_Impersonations/";
        Finish(Entity(library, AnimalAudioDir, "Cow_Call", 0.6f, 1f, 0.06f, Range(voices + "cow_moo_", 4)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Cow_Hurried", 0.75f, 1.05f, 0.06f, Range(voices + "cow_2_moo_", 6)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Sheep_Call", 0.55f, 1f, 0.08f, Range(voices + "sheep_2_baa_calm_", 5)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Sheep_Hurried", 0.7f, 1f, 0.08f, Range(voices + "sheep_2_baa_high_", 3)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Pen_Bell", 0.9f, 1f, 0.02f, Range(GM + "Miscellaneous/Bell/bell_med_ringing_", 3)), far, log);
        Finish(Entity(library, AnimalAudioDir, "Animal_Collect", 0.5f, 1f, 0.08f,
            Range(GM + "Collectibles_Items_Powerup/chime_tinkle_wood_bell_positive_", 4)), near, log);
        Save(library);
        return "audio: " + string.Join(", ", log);
    }

    // ---- Settings, definitions, controllers, prefabs ----

    public static string Content()
    {
        var log = new List<string>();
        Folder("Assets/_Game/Data/Animals");
        var settings = AssetDatabase.LoadAssetAtPath<AnimalSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AnimalSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            log.Add("settings created");
        }
        settings.Tokens = Load<CurrencyDefinition>(TokensPath);
        settings.BellSound = Id(AnimalAudioDir, "Pen_Bell");
        settings.CollectSound = Id(AnimalAudioDir, "Animal_Collect");
        settings.GateOpenSound = Id(DefenseAudioDir, "Gate_Open");
        settings.GateCloseSound = Id(DefenseAudioDir, "Gate_Close");
        Save(settings);

        foreach (Kind kind in new[] { Cow, Sheep })
        {
            string dir = $"{ContentDir}/{kind.Name}";
            Folder(dir + "/Model");
            string definitionPath = $"{dir}/{kind.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<AnimalDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<AnimalDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            kind.Tune(definition);
            Save(definition);

            AnimatorController controller = Controller(kind, definition, $"{dir}/{kind.Name}.controller");
            for (int i = 0; i < kind.Looks.Length; i++)
            {
                string look = kind.Looks[i];
                GameObject model = Model(PP + $"Prefabs/Animals/{look}.prefab", $"{dir}/Model/{look}.prefab");
                string entityName = i == 0 ? kind.Name : look;
                log.Add(Entity(kind, definition, controller, model, $"{dir}/{entityName}.prefab"));
            }
        }
        return "content: " + string.Join(", ", log);
    }

    // Speed blends idle → walk → run at the definition's speeds (the clips retimed to them); Eat and Sleep are states.
    static AnimatorController Controller(Kind kind, AnimalDefinition definition, string path)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(kind.ClipFbx).OfType<AnimationClip>().ToDictionary(c => c.name);
        AnimationClip Clip(string name) => clips.TryGetValue(kind.ClipPrefix + name, out AnimationClip clip) ? clip : throw new Exception($"no clip {kind.ClipPrefix}{name} in {kind.ClipFbx}");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        else
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
                controller.RemoveParameter(parameter);
            AnimatorStateMachine old = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition transition in old.anyStateTransitions)
                old.RemoveAnyStateTransition(transition);
            foreach (ChildAnimatorState state in old.states)
                old.RemoveState(state.state);
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is BlendTree)
                    Object.DestroyImmediate(sub, true);
        }
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Eat", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Sleep", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(Clip(kind.IdleClip), 0f);
        tree.AddChild(Clip("Walk"), definition.WalkSpeed);
        tree.AddChild(Clip("Run"), definition.HurrySpeed);
        ChildMotion[] children = tree.children;
        children[1].timeScale = definition.WalkSpeed / kind.AuthoredWalk;
        children[2].timeScale = definition.HurrySpeed / kind.AuthoredRun;
        tree.children = children;

        AnimatorState locomotion = machine.AddState("Locomotion");
        locomotion.motion = tree;
        machine.defaultState = locomotion;
        AnimatorState eat = machine.AddState("Eat");
        eat.motion = Clip("Eat");
        AnimatorState sleep = machine.AddState("Sleep");
        sleep.motion = Clip("Sleep");
        AnimatorState wake = machine.AddState("WakeUp");
        wake.motion = Clip("Sleep_Stand");

        AnimatorStateTransition toEat = Transition(locomotion.AddTransition(eat), 0.4f);
        toEat.AddCondition(AnimatorConditionMode.If, 0f, "Eat");
        toEat.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        Transition(eat.AddTransition(locomotion), 0.35f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Eat");
        Transition(eat.AddTransition(locomotion), 0.25f).AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
        AnimatorStateTransition toSleep = Transition(machine.AddAnyStateTransition(sleep), 0.8f);
        toSleep.canTransitionToSelf = false;
        toSleep.AddCondition(AnimatorConditionMode.If, 0f, "Sleep");
        Transition(sleep.AddTransition(wake), 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sleep");
        AnimatorStateTransition stood = wake.AddTransition(locomotion);
        stood.hasExitTime = true;
        stood.exitTime = 0.85f;
        stood.duration = 0.25f;
        stood.hasFixedDuration = true;
        Save(controller);
        return controller;
    }

    static AnimatorStateTransition Transition(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        return transition;
    }

    // A first-party copy of the vendor prefab with only the look left: its wander script, CharacterController, NavMeshAgent
    // and AudioSource removed, one Animator on the root with no controller.
    static GameObject Model(string vendorPath, string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(vendorPath);
        try
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component is CharacterController or AudioSource or UnityEngine.AI.NavMeshAgent or Collider or MonoBehaviour)
                    Object.DestroyImmediate(component);
            Animator rootAnimator = root.GetComponent<Animator>();
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                if (animator != rootAnimator)
                    Object.DestroyImmediate(animator);
            rootAnimator.runtimeAnimatorController = null;
            rootAnimator.applyRootMotion = false;
            rootAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = false;
            SetLayer(root, LayerMask.NameToLayer(AnimalLayer));
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The entity: the WanderCapsule's legs and tree (FollowerEntity, EnemyMover, Manual BehaviorTree) with the Animal on
    // top, the model nested as Model, a body collider to look at, the coin marker and a SaveId. An existing prefab is
    // updated in place.
    static string Entity(Kind kind, AnimalDefinition definition, AnimatorController controller, GameObject model, string path)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = PrefabUtility.LoadPrefabContents(exists ? path : Template);
        try
        {
            int layer = LayerMask.NameToLayer(AnimalLayer);
            if (!exists)
            {
                Object.DestroyImmediate(root.GetComponent<EnemyBody>());
                Object.DestroyImmediate(root.GetComponent<EnemyBrain>());
                Object.DestroyImmediate(root.GetComponent<Spawnable>());
                Object.DestroyImmediate(root.transform.Find("Capsule").gameObject);
            }
            root.name = System.IO.Path.GetFileNameWithoutExtension(path);
            root.layer = layer;

            var body = root.GetComponent<CapsuleCollider>();
            body.direction = 2;
            body.radius = kind.BodyRadius;
            body.height = kind.BodyLength;
            body.center = new Vector3(0f, kind.BodyHeight, 0f);

            var agent = new SerializedObject(root.GetComponent<FollowerEntity>());
            agent.FindProperty("shape.radius").floatValue = kind.AgentRadius;
            agent.FindProperty("shape.height").floatValue = kind.AgentHeight;
            agent.FindProperty("movement.debugFlags").intValue = 0;
            agent.ApplyModifiedPropertiesWithoutUndo();

            var tree = new SerializedObject(root.GetComponent<Opsive.BehaviorDesigner.Runtime.BehaviorTree>());
            tree.FindProperty("m_Subtree").objectReferenceValue = Load<ScriptableObject>(TreePath);
            tree.ApplyModifiedPropertiesWithoutUndo();

            Transform modelRoot = root.transform.Find("Model");
            if (modelRoot != null && PrefabUtility.GetCorrespondingObjectFromSource(modelRoot.gameObject) != model)
            {
                Object.DestroyImmediate(modelRoot.gameObject);
                modelRoot = null;
            }
            if (modelRoot == null)
            {
                modelRoot = ((GameObject)PrefabUtility.InstantiatePrefab(model, root.transform)).transform;
                modelRoot.name = "Model";
            }
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one * kind.ModelScale;
            Animator animator = modelRoot.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Transform marker = root.transform.Find("CoinMarker");
            if (marker == null)
            {
                marker = new GameObject("CoinMarker").transform;
                marker.SetParent(root.transform, false);
                marker.gameObject.AddComponent<MeshFilter>();
                marker.gameObject.AddComponent<MeshRenderer>();
            }
            Transform coin = Load<GameObject>(TokensPrefabPath).transform.Find("World/Model");
            marker.GetComponent<MeshFilter>().sharedMesh = coin.GetComponent<MeshFilter>().sharedMesh;
            var markerRenderer = marker.GetComponent<MeshRenderer>();
            markerRenderer.sharedMaterials = coin.GetComponent<MeshRenderer>().sharedMaterials;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker.localPosition = new Vector3(0f, kind.MarkerHeight, 0f);
            marker.localRotation = coin.localRotation;
            marker.localScale = coin.localScale * 1.4f;
            marker.gameObject.layer = 0;
            // Off until it holds Tokens (Animal shows it).
            marker.gameObject.SetActive(false);

            if (!root.TryGetComponent(out Animal animal))
                animal = root.AddComponent<Animal>();
            animal.Setup(definition, animator, marker.gameObject);
            if (!root.TryGetComponent(out SaveId _))
                root.AddComponent<SaveId>();

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            return saved ? System.IO.Path.GetFileName(path) : "FAILED " + path;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- Pen pieces ----

    public static string Pen()
    {
        Folder(PenDir);
        string brassPath = PenDir + "/Bell_Brass.mat";
        var brass = AssetDatabase.LoadAssetAtPath<Material>(brassPath);
        if (brass == null)
        {
            brass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(brass, brassPath);
        }
        brass.SetColor("_BaseColor", new Color(0.83f, 0.6f, 0.2f));
        brass.SetFloat("_Metallic", 0.35f);
        brass.SetFloat("_Smoothness", 0.6f);
        Save(brass);

        Build(PenDir + "/PenPost.prefab", root => Post(root.transform, 0f, 0.85f));

        Build(PenDir + "/PenRail.prefab", root =>
        {
            Rail(root.transform, 0f, SlotWidth - 0.1f);
            Cut(root, SlotWidth);
        });

        Build(PenDir + "/PenGate.prefab", root =>
        {
            float half = SlotWidth * 0.5f;
            Post(root.transform, -half, 1.05f).name = "Post_Left";
            Post(root.transform, half, 1.05f).name = "Post_Right";
            Transform hinge = Child(root.transform, "Hinge");
            hinge.localPosition = new Vector3(-(half - 0.12f), 0f, 0f);
            float leafLength = SlotWidth - 0.36f;
            Rail(Child(hinge, "Leaf"), leafLength * 0.5f, leafLength);
            NavmeshCut cut = Cut(root, SlotWidth);
            // Looking into the open doorway still finds the gate ("Close Pen").
            var doorway = new GameObject("Doorway");
            doorway.transform.SetParent(root.transform, false);
            var trigger = doorway.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.7f, 0f);
            trigger.size = new Vector3(SlotWidth - 0.4f, 1.4f, 0.6f);
            doorway.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
            root.AddComponent<PenGate>().Setup(hinge, cut);
        });

        Build(PenDir + "/PenBell.prefab", root =>
        {
            Post(root.transform, 0f, 1.4f);
            const float top = 1.75f;
            GameObject arm = Primitive(PrimitiveType.Cube, root.transform, "Arm", new Vector3(0.28f, top, 0f), new Vector3(0.6f, 0.07f, 0.07f), brass, false);
            arm.GetComponent<MeshRenderer>().sharedMaterial = Load<GameObject>(LV + "Environment/FenceEnd_1.prefab").GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Transform bell = Child(root.transform, "Bell");
            bell.localPosition = new Vector3(0.5f, top - 0.04f, 0f);
            Primitive(PrimitiveType.Sphere, bell, "Dome", new Vector3(0f, -0.2f, 0f), new Vector3(0.34f, 0.38f, 0.34f), brass, false);
            Primitive(PrimitiveType.Cylinder, bell, "Rim", new Vector3(0f, -0.38f, 0f), new Vector3(0.42f, 0.03f, 0.42f), brass, false);
            Primitive(PrimitiveType.Sphere, bell, "Clapper", new Vector3(0f, -0.45f, 0f), new Vector3(0.09f, 0.09f, 0.09f), brass, false);
            // One box to look at: the bell and the top of its post.
            var reach = root.AddComponent<BoxCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(0.3f, top - 0.3f, 0f);
            reach.size = new Vector3(1f, 1f, 0.6f);
            root.AddComponent<AnimalBell>().Setup(bell);
        });
        return "pen: PenPost, PenRail, PenGate, PenBell";
    }

    // FenceEnd_1 is 1.35 m tall at scale 1.
    static GameObject Post(Transform parent, float x, float heightScale)
    {
        GameObject post = Nest(LV + "Environment/FenceEnd_1.prefab", parent);
        post.name = "Post";
        post.transform.localPosition = new Vector3(x, -0.05f, 0f);
        post.transform.localScale = new Vector3(1.3f, heightScale, 1.3f);
        ExcludeFromNavmesh(post);
        return post;
    }

    // One row of Lakefell's two-rail planks, `length` long, centred `x` along its parent; shots and looks pass between
    // the planks (a mesh collider on them instead of the vendor's box).
    static void Rail(Transform parent, float x, float length)
    {
        GameObject rail = Nest(LV + "Modules/Extension_1_Fence.prefab", parent);
        rail.name = "Rails";
        rail.transform.localPosition = new Vector3(x, 0.06f, 0f);
        rail.transform.localScale = new Vector3(length / RailMeshLength, 1.1f, 1.6f);
        foreach (BoxCollider box in rail.GetComponents<BoxCollider>())
            box.enabled = false;
        MeshFilter planks = rail.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.EndsWith("_LOD0"));
        if (!planks.TryGetComponent(out MeshCollider mesh))
            mesh = planks.gameObject.AddComponent<MeshCollider>();
        mesh.sharedMesh = planks.sharedMesh;
        ExcludeFromNavmesh(rail);
    }

    // As the home fence's cut: A* widens it by the agent radius, so it is shortened by nearly that at both ends and
    // neighbouring cuts still overlap.
    static NavmeshCut Cut(GameObject root, float width)
    {
        var cut = root.AddComponent<NavmeshCut>();
        cut.type = NavmeshCut.MeshType.Box;
        cut.rectangleSize = new Vector2(width - 2f * AgentRadius + CutOverlap, 0.3f);
        cut.height = 3f;
        cut.center = new Vector3(0f, 0.9f, 0f);
        cut.useRotationAndScale = true;
        cut.radiusExpansionMode = NavmeshCut.RadiusExpansionMode.ExpandByAgentRadius;
        return cut;
    }

    static void ExcludeFromNavmesh(GameObject model)
    {
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.TryGetComponent(out RecastNavmeshModifier modifier))
                modifier = collider.gameObject.AddComponent<RecastNavmeshModifier>();
            modifier.includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
            modifier.dynamic = true;
        }
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            if (!renderer.GetComponent<Collider>() && !renderer.TryGetComponent(out RecastNavmeshModifier _))
                renderer.gameObject.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
    }

    static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!keepCollider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        go.AddComponent<RecastNavmeshModifier>().includeInScan = RecastNavmeshModifier.ScanInclusion.AlwaysExclude;
        return go;
    }

    // Rebuilds a prefab made only of parts this script creates (nothing references its children by file id).
    static void Build(string path, Action<GameObject> fill)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            fill(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---- WorldMap ----

    public static string World()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WorldMapPath)
        {
            if (scene.isDirty)
                throw new Exception($"{scene.path} has unsaved changes; save or discard them first");
            scene = EditorSceneManager.OpenScene(WorldMapPath, OpenSceneMode.Single);
        }
        var log = new List<string>();

        // The yard's west corner, from the fence segments either side of it: `side` runs along side 3 towards the north
        // corner, `back` along side 0 towards the south corner.
        Vector3 Segment(string name) => (GameObject.Find("Environment/Landmarks/Home Base/Fence/" + name) ?? throw new Exception("no fence segment " + name)).transform.position;
        Vector3 side = (Segment("Fence 3-10") - Segment("Fence 3-11")).normalized;
        Vector3 back = (Segment("Fence 0-01") - Segment("Fence 0-00")).normalized;
        float half = SlotWidth * 0.5f;
        Vector3 corner = (Segment("Fence 3-11") - side * half + Segment("Fence 0-00") - back * half) * 0.5f;
        float lengthSide = SlotsAlongSide * SlotWidth;
        float lengthBack = SlotsAlongBack * SlotWidth;

        GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == PenRootName);
        if (root == null)
            root = new GameObject(PenRootName);
        Transform fence = root.transform.Find("Fence");
        if (fence != null)
            Object.DestroyImmediate(fence.gameObject);
        fence = Child(root.transform, "Fence");

        // The front run (parallel to `back`, `lengthSide` up the side): rails, the gate in GateSlot; then the run back to
        // the fence (parallel to `side`). +Z of every piece points out of the pen.
        Quaternion frontRotation = Quaternion.LookRotation(side, Vector3.up);
        Quaternion flankRotation = Quaternion.LookRotation(back, Vector3.up);
        Vector3 frontStart = corner + side * lengthSide;
        Vector3 flankStart = corner + back * lengthBack;
        PenGate gate = null;
        for (int i = 0; i < SlotsAlongBack; i++)
        {
            Vector3 position = Ground(frontStart + back * ((i + 0.5f) * SlotWidth));
            GameObject piece = Place(i == GateSlot ? "PenGate" : "PenRail", fence, position, frontRotation);
            if (i == GateSlot)
                gate = piece.GetComponent<PenGate>();
            else if (i != GateSlot + 1)
                Place("PenPost", fence, Ground(frontStart + back * (i * SlotWidth)), frontRotation);
        }
        for (int i = 0; i < SlotsAlongSide; i++)
        {
            Place("PenRail", fence, Ground(flankStart + side * ((i + 0.5f) * SlotWidth)), flankRotation);
            Place("PenPost", fence, Ground(flankStart + side * (i * SlotWidth)), flankRotation);
        }
        Place("PenPost", fence, Ground(frontStart + back * lengthBack), frontRotation);

        Vector3 center = Ground(corner + side * (lengthSide * 0.5f) + back * (lengthBack * 0.5f));
        Transform penTransform = root.transform.Find("Pen") ?? Child(root.transform, "Pen");
        penTransform.SetPositionAndRotation(center, frontRotation);
        Transform wait = penTransform.Find("WaitPoint") ?? Child(penTransform, "WaitPoint");
        wait.position = gate.transform.position + side * 1.8f;
        if (!penTransform.TryGetComponent(out AnimalPen pen))
            pen = penTransform.gameObject.AddComponent<AnimalPen>();
        pen.Setup(new Vector2(lengthBack - 0.6f, lengthSide - 0.6f), gate, wait);
        EditorUtility.SetDirty(pen);

        Transform bell = root.transform.Find("PenBell");
        if (bell != null)
            Object.DestroyImmediate(bell.gameObject);
        // Outside the front run, on the far side of the gate from the big fence.
        Place("PenBell", root.transform, Ground(frontStart + back * ((GateSlot + 1) * SlotWidth + 0.45f) + side * 0.55f), frontRotation);

        // The pasture: on the meadow in front of the home gate.
        GameObject zone = GameObject.Find("Raid/Spawn Zone (Front)") ?? throw new Exception("no Raid/Spawn Zone (Front): where is the home gate?");
        Transform pastureTransform = root.transform.Find("Pasture") ?? Child(root.transform, "Pasture");
        pastureTransform.position = Ground(zone.transform.position + zone.transform.forward * PastureDistance);
        if (!pastureTransform.TryGetComponent(out AnimalPasture pasture))
            pasture = pastureTransform.gameObject.AddComponent<AnimalPasture>();
        pasture.Setup(PastureRadius);
        EditorUtility.SetDirty(pasture);

        // The herd: kept when it's already there (their SaveIds are in saves).
        Transform herd = root.transform.Find("Animals") ?? Child(root.transform, "Animals");
        if (herd.childCount == 0)
        {
            (string prefab, Vector2 at, float yaw, float scale)[] animals =
            {
                ("Cow/Cow", new Vector2(-1.8f, 2.6f), 200f, 1f),
                ("Cow/Cow_Brown", new Vector2(1.6f, -2.2f), 70f, 0.95f),
                ("Sheep/Sheep", new Vector2(1.9f, 2.4f), 150f, 1f),
                ("Sheep/Sheep", new Vector2(-2.2f, -0.6f), 20f, 0.92f),
                ("Sheep/Sheep", new Vector2(0.2f, -3.6f), 310f, 1.05f),
                ("Sheep/Sheep", new Vector2(-1.2f, -2.9f), 250f, 0.88f),
            };
            foreach ((string prefab, Vector2 at, float yaw, float scale) in animals)
            {
                var asset = Load<GameObject>($"{ContentDir}/{prefab}.prefab");
                var animal = (GameObject)PrefabUtility.InstantiatePrefab(asset, herd);
                animal.transform.SetPositionAndRotation(Ground(penTransform.TransformPoint(new Vector3(at.x, 0f, at.y))), Quaternion.Euler(0f, yaw, 0f));
                animal.transform.localScale = Vector3.one * scale;
                animal.GetComponent<SaveId>().Regenerate();
            }
            for (int i = 0; i < herd.childCount; i++)
                herd.GetChild(i).name = $"{herd.GetChild(i).name} {i + 1}";
            log.Add($"{herd.childCount} animals placed");
        }
        else
            log.Add($"{herd.childCount} animals kept");

        GameLifetimeScope scope = Object.FindAnyObjectByType<GameLifetimeScope>() ?? throw new Exception("WorldMap has no GameLifetimeScope");
        if (!scope.TryGetComponent(out AnimalInstaller installer))
        {
            installer = scope.gameObject.AddComponent<AnimalInstaller>();
            log.Add("AnimalInstaller added");
        }
        var serialized = new SerializedObject(installer);
        serialized.FindProperty("_settings").objectReferenceValue = Load<AnimalSettings>(SettingsPath);
        serialized.FindProperty("_pen").objectReferenceValue = pen;
        serialized.FindProperty("_pasture").objectReferenceValue = pasture;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        installer.CollectSceneAnimals();
        log.Add(installer.Problem == null ? "installer ready" : "installer problem: " + installer.Problem);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"scene: pen centre {center}, gate {gate.transform.position}, pasture {pasture.Center} r{PastureRadius}; " + string.Join(", ", log);
    }

    static GameObject Place(string prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{PenDir}/{prefab}.prefab"), parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    static Vector3 Ground(Vector3 point)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    // ---- helpers ----

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Nest(string prefabPath, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(prefabPath), parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    static void SetLayer(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

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
