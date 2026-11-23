using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.RobotSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// Builds the service robots: the Robot layer, Data/Robots (settings, the Shop and Upgrades services), the Rob15 model,
// face set and animator controller from the GrigoriyArx Droll Robots pack, and two tinted body materials.
// The tree is built apart: TreeKit.Build AgentScripts/Robots/Robot.tree.txt.
// Run: unity command script run --file AgentScripts/Robots/RobotSetup.cs --entry RobotSetup.<Step>
public static class RobotSetup
{
    public const string Layer = "Robot";
    const string Vendor = "Assets/_StoreAssets/Characters/GrigoriyArx/DrollRobots/";
    const string VendorPrefab = Vendor + "Prefabs/Robots/Rob15.prefab";
    const string VendorFbx = Vendor + "Art/Meshes/Robots/Rob15Emo06/Rob15.fbx";
    const string VendorBody = Vendor + "Art/Materials/Robots/Rob15/Rob15Body.mat";
    const string DataDir = "Assets/_Game/Data/Robots";
    const string SettingsPath = DataDir + "/RobotSettings.asset";
    const string ServicesDir = DataDir + "/Services";
    const string ContentDir = "Assets/_Game/Content/Robots";
    const string ModelDir = ContentDir + "/Rob15";
    const string ModelPath = ModelDir + "/Model/Rob15_Model.prefab";
    const string ControllerPath = ModelDir + "/Rob15.controller";
    const string FacePath = ModelDir + "/Rob15_Face.asset";
    const string MaterialsDir = ModelDir + "/Materials";
    // Model scale for a 1.3 m robot (the vendor model is 1.76 m).
    public const float ModelScale = 0.74f;

    public static readonly (string Name, Color Tint)[] Tints =
    {
        ("Rob15Body_Orange", new Color(1f, 0.72f, 0.45f)),
        ("Rob15Body_Mint", new Color(0.62f, 0.95f, 0.84f)),
    };

    public static string All() => string.Join("\n", RobotLayer(), Data(), Model(), Controller());

    // ---- Shared settings: the whistle carries through walls to robots; robots are metal to bullets ----

    const string InteractionTuningPath = "Assets/_Game/Data/Player/PlayerInteractionTuning.asset";
    const string SurfaceSettingsPath = "Assets/_Game/Data/Surfaces/SurfaceSettings.asset";

    public static string Shared()
    {
        var tuning = Load<MB13.Aipocalypse.Core.PlayerSystem.PlayerInteractionTuning>(InteractionTuningPath);
        var so = new SerializedObject(tuning);
        so.FindProperty("_whistleHeardMask").intValue = 1 << LayerMask.NameToLayer(Layer);
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(tuning);

        var surfaces = Load<MB13.Aipocalypse.Core.SurfaceSystem.SurfaceSettings>(SurfaceSettingsPath);
        // First, so no later keyword in a robot's name or its parents' wins.
        if (!surfaces.Rules.Any(r => r.Keyword == "Robot"))
            surfaces.Rules.Insert(0, new MB13.Aipocalypse.Core.SurfaceSystem.SurfaceRule { Keyword = "Robot", Kind = MB13.Aipocalypse.Core.SurfaceSystem.SurfaceKind.Metal });
        Save(surfaces);
        var metal = surfaces.Look(MB13.Aipocalypse.Core.SurfaceSystem.SurfaceKind.Metal);
        return $"shared: whistle heard mask {so.FindProperty("_whistleHeardMask").intValue}, surface rule Robot = Metal, metal look {(metal != null ? "set" : "MISSING")}";
    }

    // ---- Layer ----

    public static string RobotLayer()
    {
        if (LayerMask.NameToLayer(Layer) >= 0)
            return "layer: " + LayerMask.NameToLayer(Layer);
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tags.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue))
                continue;
            slot.stringValue = Layer;
            tags.ApplyModifiedPropertiesWithoutUndo();
            return "layer: added " + i;
        }
        throw new Exception("no free layer");
    }

    // ---- Settings, services, face set ----

    public static string Data()
    {
        Folder(ServicesDir);
        Folder(ModelDir + "/Model");
        var settings = LoadOrCreate<RobotSettings>(SettingsPath);
        settings.PatVerb = Loc.Interaction("verb.robot_pat");
        settings.CallVerb = Loc.Interaction("verb.robot_call");
        Save(settings);
        // Job icons: placeholder map glyphs from Synty Interface Apocalypse HUD.
        Service("Shop", "verb.robot_shop", "robot.shop", RobotMenu.Shop, MapIcons + "ICON_Apocalpyse_Map_GunShop_01.png");
        Service("Upgrades", "verb.robot_upgrades", "robot.upgrades", RobotMenu.Upgrades, MapIcons + "ICON_Apocalpyse_Map_Star_01.png");

        // Emotions4.png: a strip of 10 eye frames over 10 mouth frames. Eyes: 0 square, 1 happy arcs, 2 closed (u u),
        // 3 lines, 4 rings, 5 crosses, 6 > <, 7 angry, 8 keyholes, 9 hearts. Mouths: 0 small smile, 1 big open smile,
        // 2 frown, 3 line, 4 "O", 5 tongue, 6 wavy, 7 zigzag teeth, 8 small wave, 9 open round.
        var face = LoadOrCreate<RobotFaceSet>(FacePath);
        face.EyesRenderer = "Rob15_Eyes";
        face.MouthRenderer = "Rob15_mouth";
        face.Columns = 10;
        // The scrolling speech line the vendor's TextureScroller drove (3 lines stacked, the middle one shown).
        face.SpeechRenderer = "Rob15_mouthSpeech";
        face.SpeechScroll = 0.2f;
        face.SpeechTracks = 3;
        face.SpeechTrack = 1;
        face.Frames = new Dictionary<RobotExpression, RobotFaceSet.Frame>
        {
            { RobotExpression.Neutral, Frame(0, 0) },
            { RobotExpression.Happy, Frame(1, 1) },
            { RobotExpression.Blink, Frame(3, 0) },
            { RobotExpression.Sleepy, Frame(2, 3) },
            { RobotExpression.Surprised, Frame(4, 4) },
            { RobotExpression.Sad, Frame(3, 2) },
            { RobotExpression.Angry, Frame(7, 7) },
            { RobotExpression.Talk, Frame(0, 9) },
            { RobotExpression.Worried, Frame(8, 6) },
            { RobotExpression.Dizzy, Frame(5, 6) },
            { RobotExpression.Love, Frame(9, 1) },
            { RobotExpression.Excited, Frame(6, 1) },
            { RobotExpression.Yawn, Frame(2, 4) },
        };
        Save(face);
        return "data: settings, Shop, Upgrades, face set";
    }

    static RobotFaceSet.Frame Frame(int eyes, int mouth) => new() { Eyes = eyes, Mouth = mouth };

    const string MapIcons = "Assets/Synty/InterfaceApocalypseHUD/Sprites/Icons_Map/";

    static void Service(string name, string verbKey, string nameKey, RobotMenu menu, string icon)
    {
        var service = LoadOrCreate<RobotService>($"{ServicesDir}/{name}.asset");
        service.Menu = menu;
        service.Icon = AssetDatabase.LoadAllAssetsAtPath(icon).OfType<Sprite>().FirstOrDefault() ?? throw new Exception("no sprite " + icon);
        service.Verb = Loc.Interaction(verbKey);
        service.DisplayName = Loc.Creatures(nameKey);
        Save(service);
    }

    // ---- Model and tints ----

    // A first-party copy of the vendor robot without its demo scripts, character controller and push collider; the
    // Animator stays on the root with no controller (the entity assigns one), root motion off, animated off screen too.
    // Less gloss than the vendor's 0.5: the robots looked like wet plastic, worst under lights at night.
    const float BodySmoothness = 0.3f;
    const float GlassSmoothness = 0.15f;
    const string EyesMeshPath = ModelDir + "/Model/Rob15_Eyes_Joined.asset";

    // Our copies of the vendor's glass and face materials: no highlights or reflections on the face (only the emoji glow),
    // a matte-ish glass.
    static readonly (string Vendor, string Copy, bool Screen)[] FaceMaterials =
    {
        ("Rob15Glass", "Rob15Glass_Matte", false),
        ("Rob15Emo_eyes", "Rob15Emo_eyes_Screen", true),
        ("Rob15Emo_mouth", "Rob15Emo_mouth_Screen", true),
        ("Rob15Emo_speak", "Rob15Emo_speak_Screen", true),
    };

    public static string Model()
    {
        Folder(ModelDir + "/Model");
        Folder(MaterialsDir);
        var copies = new Dictionary<Material, Material>();
        foreach ((string vendor, string copyName, bool screen) in FaceMaterials)
        {
            var source = Load<Material>($"{Vendor}Art/Materials/Robots/Rob15/{vendor}.mat");
            string path = $"{MaterialsDir}/{copyName}.mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(source);
                AssetDatabase.CreateAsset(copy, path);
            }
            copy.CopyPropertiesFromMaterial(source);
            copy.SetFloat("_Smoothness", screen ? 0f : GlassSmoothness);
            copy.SetFloat("_SpecularHighlights", 0f);
            copy.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            if (screen)
            {
                copy.SetFloat("_EnvironmentReflections", 0f);
                copy.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            Save(copy);
            copies[source] = copy;
        }
        Mesh eyes = JoinedEyes();

        GameObject root = PrefabUtility.LoadPrefabContents(VendorPrefab);
        try
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            Transform push = root.transform.Find("PushCollider");
            if (push != null)
                Object.DestroyImmediate(push.gameObject);
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component is MonoBehaviour)
                    Object.DestroyImmediate(component);
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component is CharacterController or Collider or Rigidbody or AudioSource)
                    Object.DestroyImmediate(component);
            Animator animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            // Always: the head turn goes on top of the animated pose, which a culled Animator stops rewriting (two robots).
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = false;
            int layer = LayerMask.NameToLayer(Layer);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && copies.TryGetValue(materials[i], out Material copy))
                        materials[i] = copy;
                renderer.sharedMaterials = materials;
                if (renderer.name == "Rob15_Eyes" && renderer is SkinnedMeshRenderer skin)
                    skin.sharedMesh = eyes;
                else if (renderer.name == "Rob15_Eyes" && renderer.TryGetComponent(out MeshFilter filter))
                    filter.sharedMesh = eyes;
            }
            root.name = "Rob15_Model";
            PrefabUtility.SaveAsPrefabAsset(root, ModelPath, out bool saved);
            if (!saved)
                throw new Exception("couldn't save " + ModelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var body = Load<Material>(VendorBody);
        foreach ((string name, Color tint) in Tints)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(body);
                AssetDatabase.CreateAsset(material, path);
            }
            material.CopyPropertiesFromMaterial(body);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", BodySmoothness);
            Save(material);
        }
        return "model: " + ModelPath + ", tints " + string.Join(", ", Tints.Select(t => t.Name)) + ", face " + string.Join(", ", copies.Values.Select(m => m.name)) + ", eyes " + eyes.name;
    }

    // The vendor's eye screen is two quads with a 2.4 cm gap down the middle (a dark line, emoji cut in two): a copy with
    // the inner edges moved to the centre, meeting at the same texture column.
    static Mesh JoinedEyes()
    {
        Mesh source = AssetDatabase.LoadAllAssetsAtPath(VendorFbx).OfType<Mesh>().First(m => m.name == "Rob15_Eyes");
        Mesh joined = Object.Instantiate(source);
        joined.name = "Rob15_Eyes_Joined";
        Vector3[] vertices = joined.vertices;
        Vector2[] uv = joined.uv;
        var inner = Enumerable.Range(0, vertices.Length).Where(i => Mathf.Abs(vertices[i].x) < 0.05f).ToArray();
        float middle = inner.Average(i => uv[i].x);
        foreach (int i in inner)
        {
            vertices[i].x = 0f;
            uv[i].x = middle;
        }
        joined.vertices = vertices;
        joined.uv = uv;
        joined.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(EyesMeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(joined, EyesMeshPath);
            return joined;
        }
        EditorUtility.CopySerialized(joined, existing);
        Object.DestroyImmediate(joined);
        Save(existing);
        return existing;
    }

    public static string BodyMaterialPath(int tint) => $"{MaterialsDir}/{Tints[tint].Name}.mat";
    public static string VendorBodyPath => VendorBody;

    // ---- Animator controller ----

    // Parameters: Speed (m/s), Turn (−1..1), Emote + EmoteIndex (RobotEmote), Hit + HitIndex (0 front, 1 left, 2 right),
    // Standby. Locomotion blends idle, walk, run and the turns on (Turn, Speed); emotes and hits play from any state and
    // go back to Locomotion; Standby is the idle played slowly while the bool is on.
    public static string Controller()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(VendorFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))
            .ToDictionary(c => c.name);
        AnimationClip Clip(string name) => clips.TryGetValue("Rob15_" + name, out AnimationClip clip) ? clip : throw new Exception("no clip Rob15_" + name);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            controller.RemoveParameter(parameter);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
        controller.AddParameter("Emote", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("EmoteIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("HitIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Knock", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Down", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Standby", AnimatorControllerParameterType.Bool);
        // Never set by code: the lying states sample the fall at this normalized time (its last frame).
        controller.AddParameter(new AnimatorControllerParameter { name = "LieTime", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states.ToArray())
            machine.RemoveState(child.state);
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
            machine.RemoveAnyStateTransition(transition);
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>().ToArray())
            AssetDatabase.RemoveObjectFromAsset(sub);

        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.FreeformCartesian2D, blendParameter = "Turn", blendParameterY = "Speed", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, controller);
        float walk = 0.84f * ModelScale;
        float run = 1.75f * ModelScale;
        tree.AddChild(Clip("idle"), new Vector2(0f, 0f));
        // The walk again at a crawl, played slower: the long idle only mixes in near a standstill (a blend syncs its clips'
        // cycles, so the idle dragged the walking legs slower than the body moved, at bedtime pace and every start and stop).
        tree.AddChild(Clip("walk"), new Vector2(0f, walk * SlowStep));
        ChildMotion[] slow = tree.children;
        slow[^1].timeScale = SlowStep;
        tree.children = slow;
        tree.AddChild(Clip("walk"), new Vector2(0f, walk));
        tree.AddChild(Clip("run"), new Vector2(0f, run));
        tree.AddChild(Clip("turn90Left"), new Vector2(-1f, 0f));
        tree.AddChild(Clip("turn90Right"), new Vector2(1f, 0f));
        AnimatorState locomotion = machine.AddState("Locomotion", new Vector3(300f, 0f));
        locomotion.motion = tree;
        machine.defaultState = locomotion;

        AnimatorState standby = machine.AddState("Standby", new Vector3(300f, 120f));
        standby.motion = Clip("idle");
        standby.speed = 0.4f;
        Quick(locomotion.AddTransition(standby), 0.6f).AddCondition(AnimatorConditionMode.If, 0f, "Standby");
        Quick(standby.AddTransition(locomotion), 0.4f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Standby");

        var emotes = new (RobotEmote Emote, string Clip, float Exit)[]
        {
            (RobotEmote.Hello, "EmoHello", 0.9f),
            (RobotEmote.Talk, "EmoTalk1", 0.9f),
            (RobotEmote.ThumbsUp, "EmoThumbsUp", 0.9f),
            (RobotEmote.Laugh, "EmoLaught", 0.9f),
            (RobotEmote.DontKnow, "EmoDontKnow", 0.9f),
            (RobotEmote.No, "EmoNo", 0.9f),
            (RobotEmote.Win, "EmoWin", 0.9f),
            (RobotEmote.Dance, "Dance1b", 2.9f),
            (RobotEmote.Jump, "Jump", 0.9f),
            (RobotEmote.Cry, "EmoCry", 0.92f),
            (RobotEmote.LookAround, "EmoLooking for", 0.92f),
            (RobotEmote.Brave, "BattleIdle", 0.95f),
        };
        for (int i = 0; i < emotes.Length; i++)
        {
            AnimatorState state = machine.AddState("Emote_" + (int)emotes[i].Emote, new Vector3(600f, i * 50f));
            state.motion = Clip(emotes[i].Clip);
            AnimatorStateTransition enter = Quick(machine.AddAnyStateTransition(state), 0.2f);
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Emote");
            enter.AddCondition(AnimatorConditionMode.Equals, (int)emotes[i].Emote, "EmoteIndex");
            if (emotes[i].Emote != RobotEmote.Brave)
            {
                Exit(state.AddTransition(locomotion), emotes[i].Exit, 0.25f);
                continue;
            }
            // Brave: the boxing stance, then a double punch.
            AnimatorState punch = machine.AddState("Emote_" + (int)RobotEmote.Brave + "_Punch", new Vector3(850f, i * 50f));
            punch.motion = Clip("BattleDoublePunch");
            Exit(state.AddTransition(punch), emotes[i].Exit, 0.1f);
            Exit(punch.AddTransition(locomotion), 0.9f, 0.25f);
        }

        // Knocked over (Knock, HitIndex 0 on its back, 1 on its face): the fall, then lying on the fall's last frame while
        // Down, then getting up. Hits and emotes are held off by the robot while it's down.
        string[,] falls = { { "fallBack", "riseBack" }, { "fallFront", "riseFront" } };
        for (int i = 0; i < 2; i++)
        {
            AnimatorState fall = machine.AddState("Fall_" + i, new Vector3(0f, 200f + i * 120f));
            fall.motion = Clip(falls[i, 0]);
            AnimatorStateTransition enter = Quick(machine.AddAnyStateTransition(fall), 0.1f);
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Knock");
            enter.AddCondition(AnimatorConditionMode.Equals, i, "HitIndex");
            AnimatorState lie = machine.AddState("Lie_" + i, new Vector3(0f, 240f + i * 120f));
            lie.motion = Clip(falls[i, 0]);
            // Held on the last frame by motion time: cycle offset (with speed 0) only works on looping clips, and the
            // falls don't loop, so it showed the first, standing frame.
            lie.timeParameterActive = true;
            lie.timeParameter = "LieTime";
            Exit(fall.AddTransition(lie), 0.98f, 0.02f);
            AnimatorState rise = machine.AddState("Rise_" + i, new Vector3(0f, 280f + i * 120f));
            rise.motion = Clip(falls[i, 1]);
            Quick(lie.AddTransition(rise), 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
            Exit(rise.AddTransition(locomotion), 0.92f, 0.2f);
        }

        string[] hits = { "getHitFront", "getHitLeft", "getHitRight" };
        for (int i = 0; i < hits.Length; i++)
        {
            AnimatorState state = machine.AddState("Hit_" + i, new Vector3(0f, i * 50f));
            state.motion = Clip(hits[i]);
            AnimatorStateTransition enter = Quick(machine.AddAnyStateTransition(state), 0.08f);
            enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            enter.AddCondition(AnimatorConditionMode.Equals, i, "HitIndex");
            Exit(state.AddTransition(locomotion), 0.9f, 0.2f);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return "controller: " + ControllerPath;
    }

    const float SlowStep = 0.3f;

    static AnimatorStateTransition Quick(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.hasFixedDuration = true;
        return transition;
    }

    static void Exit(AnimatorStateTransition transition, float exitTime, float duration)
    {
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.hasFixedDuration = true;
    }

    // ---- Entity prefabs ----

    const string Template = "Assets/_Game/Content/Enemies/WanderCapsule/WanderCapsule.prefab";
    const string TreePath = ContentDir + "/_Shared/Robot.asset";
    const string BasePath = ContentDir + "/Robot.prefab";
    const float TurnSpeed = 220f;
    const float OnSpotTurnSpeed = 160f;
    const float TurnSmoothing = 0.15f;
    const float Radius = 0.35f;
    const float Height = 1.3f;

    public static readonly (string Name, string Service, int Tint)[] Variants =
    {
        ("Robot_Shop", "Shop", 0),
        ("Robot_Upgrades", "Upgrades", 1),
    };

    // The base entity: the WanderCapsule's legs and tree (FollowerEntity, EnemyMover, Manual BehaviorTree) with the Robot
    // on top, the model nested as Model at 1.3 m, a body capsule to look at, whistle and shoot, the face and its night glow.
    // Then one variant per service with its own body tint. Existing prefabs are updated in place.
    public static string Prefabs()
    {
        var log = new List<string> { Base() };
        foreach ((string name, string service, int tint) in Variants)
            log.Add(Variant(name, service, tint));
        return "prefabs: " + string.Join(", ", log);
    }

    static string Base()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath) != null;
        GameObject root = PrefabUtility.LoadPrefabContents(exists ? BasePath : Template);
        try
        {
            if (!exists)
            {
                Object.DestroyImmediate(root.GetComponent<MB13.Aipocalypse.Core.EnemySystem.EnemyBody>());
                Object.DestroyImmediate(root.GetComponent<MB13.Aipocalypse.Core.EnemySystem.EnemyBrain>());
                Object.DestroyImmediate(root.GetComponent<MB13.Aipocalypse.Core.SpawnSystem.Spawnable>());
                Object.DestroyImmediate(root.transform.Find("Capsule").gameObject);
            }
            root.name = "Robot";
            int layer = LayerMask.NameToLayer(Layer);
            root.layer = layer;

            var body = root.GetComponent<CapsuleCollider>();
            body.direction = 1;
            body.radius = Radius;
            body.height = Height;
            body.center = new Vector3(0f, Height * 0.5f, 0f);

            var agent = new SerializedObject(root.GetComponent<Pathfinding.FollowerEntity>());
            agent.FindProperty("shape.radius").floatValue = Radius;
            agent.FindProperty("shape.height").floatValue = Height;
            agent.FindProperty("movement.debugFlags").intValue = 0;
            agent.FindProperty("movement.follower.rotationSpeed").floatValue = TurnSpeed;
            agent.FindProperty("movement.follower.maxRotationSpeed").floatValue = TurnSpeed * 1.5f;
            agent.FindProperty("movement.follower.maxOnSpotRotationSpeed").floatValue = OnSpotTurnSpeed;
            agent.FindProperty("movement.follower.allowRotatingOnSpotBacking").boolValue = false;
            agent.FindProperty("movement.rotationSmoothing").floatValue = TurnSmoothing;
            agent.ApplyModifiedPropertiesWithoutUndo();

            var tree = new SerializedObject(root.GetComponent<Opsive.BehaviorDesigner.Runtime.BehaviorTree>());
            tree.FindProperty("m_Subtree").objectReferenceValue = Load<ScriptableObject>(TreePath);
            tree.ApplyModifiedPropertiesWithoutUndo();

            var model = Load<GameObject>(ModelPath);
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
            modelRoot.localScale = Vector3.one * ModelScale;
            var animator = modelRoot.GetComponent<Animator>();
            animator.runtimeAnimatorController = Load<AnimatorController>(ControllerPath);
            Transform head = modelRoot.GetComponentsInChildren<Transform>(true).First(t => t.name == "head");

            if (!root.TryGetComponent(out RobotFace face))
                face = root.AddComponent<RobotFace>();
            face.Setup(Load<RobotFaceSet>(FacePath));
            if (!root.TryGetComponent(out RobotFaceGlow glow))
                glow = root.AddComponent<RobotFaceGlow>();
            glow.Setup(face);
            var glowSo = new SerializedObject(glow);
            glowSo.FindProperty("_source").intValue = (int)MB13.Aipocalypse.Core.EnvironmentSystem.ReactorSource.Darkness;
            glowSo.ApplyModifiedPropertiesWithoutUndo();
            NightLight(root.transform, modelRoot);

            if (!root.TryGetComponent(out RobotGroundLegs _))
                root.AddComponent<RobotGroundLegs>();
            if (!root.TryGetComponent(out Robot robot))
                robot = root.AddComponent<Robot>();
            robot.Setup(robot.Role, face, animator, head);

            PrefabUtility.SaveAsPrefabAsset(root, BasePath, out bool saved);
            return saved ? "Robot" : "FAILED " + BasePath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A small spot light that fades in at night (TimeOfDayLight on Darkness), in the screen's colour: from the chest, down
    // and forward, it lights a patch of ground in front and never the robot (its face and glass would glare).
    const float NightLightHeight = 0.85f;
    const float NightLightForward = 0.3f;
    const float NightLightPitch = 60f;
    const float NightLightAngle = 100f;
    const float NightLightRange = 5f;
    const float NightLightIntensity = 2f;

    static void NightLight(Transform root, Transform model)
    {
        Transform child = root.Find("Night Light");
        if (child == null)
        {
            child = new GameObject("Night Light").transform;
            child.SetParent(root, false);
        }
        child.gameObject.layer = root.gameObject.layer;
        child.localPosition = new Vector3(0f, NightLightHeight, NightLightForward);
        child.localRotation = Quaternion.Euler(NightLightPitch, 0f, 0f);
        if (!child.TryGetComponent(out Light light))
            light = child.gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = NightLightAngle;
        light.innerSpotAngle = NightLightAngle * 0.5f;
        light.range = NightLightRange;
        light.intensity = NightLightIntensity;
        light.shadows = LightShadows.None;
        light.color = ScreenColour(model);
        // URP adds this to every light it renders; on the prefab, so scene instances don't each get it as an override.
        if (!child.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalLightData _))
            child.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        if (!child.TryGetComponent(out MB13.Aipocalypse.Core.EnvironmentSystem.TimeOfDayLight fade))
            fade = child.gameObject.AddComponent<MB13.Aipocalypse.Core.EnvironmentSystem.TimeOfDayLight>();
        var so = new SerializedObject(fade);
        so.FindProperty("_source").intValue = (int)MB13.Aipocalypse.Core.EnvironmentSystem.ReactorSource.Darkness;
        SerializedProperty lights = so.FindProperty("_lights");
        lights.arraySize = 1;
        lights.GetArrayElementAtIndex(0).objectReferenceValue = light;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The eyes' emission colour at full brightness, half mixed with white (a pure green light would dye the yard).
    static Color ScreenColour(Transform model)
    {
        var face = Load<RobotFaceSet>(FacePath);
        Renderer eyes = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == face.EyesRenderer);
        Material material = eyes != null ? eyes.sharedMaterial : null;
        Color colour = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.white;
        float max = Mathf.Max(colour.r, colour.g, colour.b);
        return max > 0.01f ? Color.Lerp(Color.white, new Color(colour.r / max, colour.g / max, colour.b / max), 0.5f) : Color.white;
    }

    // A prefab variant of the base: its service, and the vendor body material swapped for its tint on every renderer.
    static string Variant(string name, string service, int tint)
    {
        string path = $"{ContentDir}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BasePath), scene);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = name;
            var robot = root.GetComponent<Robot>();
            robot.Setup(Load<RobotService>($"{ServicesDir}/{service}.asset"), robot.Face, robot.Animator, robot.Head);
            var vendorBody = Load<Material>(VendorBody);
            var tinted = Load<Material>(BodyMaterialPath(tint));
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == vendorBody || Tints.Any(t => materials[i] != null && materials[i].name == t.Name))
                    {
                        materials[i] = tinted;
                        changed = true;
                    }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            return saved ? name : "FAILED " + path;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- WorldMap ----

    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string RootName = "Home Robots";
    const string FencePath = "Environment/Landmarks/Home Base/Fence";
    const string HousePath = "Environment/Landmarks/Home Base/House_1";
    const float YardYaw = 50f;
    static readonly Vector3 YardHalfSize = new(17f, 2f, 13.5f);
    // In the house's frame (its door is at z = 3, facing +z; the bed at x = −4): the robot stands across the room from the
    // bed, facing the door; the doorway stays clear.
    static readonly Vector3 HouseSpotLocal = new(1.8f, 1f, -0.6f);
    static readonly Bounds HouseDoorway = new(new Vector3(-1.8f, 0f, 3.4f), new Vector3(2.4f, 4f, 2.4f));
    const float HouseRadius = 1.6f;
    // The robots-only way through the door: the floor inside (the navmesh ends about 1 m short of the door), the sill,
    // and the walkable patch outside beside the flower pots (the ground right in front of the door is blocked by them).
    static readonly Vector3 DoorInsideLocal = new(-1.6f, 1f, 1.9f);
    static readonly Vector3 DoorSillLocal = new(-1.6f, 1f, 2.95f);
    static readonly Vector3 DoorOutsideLocal = new(-2.6f, 0f, 4.5f);
    // The interior (and the sill) tagged robots-only, so no other walker plans into the house.
    static readonly Vector3[] InteriorLocal = { new(-4.6f, 1f, -2.5f), new(4.5f, 1f, -2.5f), new(4.5f, 1f, 3.4f), new(-4.6f, 1f, 3.4f) };
    // Where the garden robot hides during a raid: inside, by the wall left of the door.
    static readonly Vector3 GardenShelterLocal = new(-0.4f, 1f, -1.3f);
    // In the yard's frame (gate at z = +13.5, the herd's corner at x = −12.5): east of the house, facing the gate side.
    static readonly Vector3 GardenSpotLocal = new(8.5f, 0f, 4f);
    const float GardenRadius = 4f;

    // The "Home Robots" root in WorldMap: the yard box, a garden spot and a house spot, Robot_Shop in the garden,
    // Robot_Upgrades in the house, a camera point inside the house for checks, and the RobotInstaller on the scope.
    // Re-running keeps what's there and re-places it.
    public static string World()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != WorldMapPath)
        {
            if (scene.isDirty)
                throw new Exception($"{scene.path} has unsaved changes; save or discard them first");
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(WorldMapPath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
        Transform fence = (GameObject.Find(FencePath) ?? throw new Exception("no home fence")).transform;
        Vector3 yardCenter = Vector3.zero;
        Collider[] fenceColliders = fence.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in fenceColliders)
            yardCenter += collider.bounds.center;
        yardCenter /= fenceColliders.Length;
        Quaternion yardTurn = Quaternion.Euler(0f, YardYaw, 0f);
        Transform house = (GameObject.Find(HousePath) ?? throw new Exception("no house")).transform;

        GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName) ?? new GameObject(RootName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            return child;
        }

        Transform yardObject = Child(root.transform, "Yard");
        yardObject.SetPositionAndRotation(Ground(yardCenter), yardTurn);
        RobotYard yard = yardObject.GetComponent<RobotYard>() ?? yardObject.gameObject.AddComponent<RobotYard>();
        yard.Setup(YardHalfSize);

        Transform garden = Child(root.transform, "Spot_Garden");
        garden.SetPositionAndRotation(Ground(yardCenter + yardTurn * GardenSpotLocal), yardTurn);
        RobotSpot gardenSpot = garden.GetComponent<RobotSpot>() ?? garden.gameObject.AddComponent<RobotSpot>();
        Transform shelter = Child(root.transform, "Shelter_Garden");
        shelter.SetPositionAndRotation(Floor(house.TransformPoint(GardenShelterLocal)), house.rotation);
        gardenSpot.Setup(GardenRadius, Array.Empty<Bounds>(), shelter);

        Transform houseSpotObject = Child(root.transform, "Spot_House");
        Vector3 inside = house.TransformPoint(HouseSpotLocal);
        houseSpotObject.SetPositionAndRotation(Floor(inside), house.rotation);
        RobotSpot houseSpot = houseSpotObject.GetComponent<RobotSpot>() ?? houseSpotObject.gameObject.AddComponent<RobotSpot>();
        // The doorway box, from the house's frame into the spot's.
        Vector3 doorway = houseSpotObject.InverseTransformPoint(house.TransformPoint(HouseDoorway.center));
        houseSpot.Setup(HouseRadius, new[] { new Bounds(new Vector3(doorway.x, 0f, doorway.z), HouseDoorway.size) });

        string door = Door(root.transform, house, Child);

        Transform camera = Child(root.transform, "RobotCam_House");
        camera.position = house.TransformPoint(new Vector3(-3.2f, 2.6f, -2.4f));
        camera.rotation = Quaternion.LookRotation(houseSpotObject.position + Vector3.up * 0.7f - camera.position);

        Transform robots = Child(root.transform, "Robots");
        Robot shop = PlaceRobot(robots, "Robot_Shop", gardenSpot);
        Robot upgrades = PlaceRobot(robots, "Robot_Upgrades", houseSpot);

        var scope = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.Bootstrap.GameLifetimeScope>() ?? throw new Exception("WorldMap has no GameLifetimeScope");
        var log = new List<string>();
        if (!scope.TryGetComponent(out RobotInstaller installer))
        {
            installer = scope.gameObject.AddComponent<RobotInstaller>();
            log.Add("RobotInstaller added");
        }
        installer.Setup(Load<RobotSettings>(SettingsPath), yard);
        EditorUtility.SetDirty(installer);
        installer.CollectSceneRobots();
        log.Add(installer.Problem == null ? "installer ready" : "installer problem: " + installer.Problem);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return $"scene: yard {yard.transform.position}, garden {garden.position}, house {houseSpotObject.position}, {shop.name}, {upgrades.name}; {door}; " + string.Join(", ", log);
    }

    // The robot-only door: a NodeLink2 (tag RobotsOnly) from the floor inside to the ground outside, hopped by RobotDoorHop
    // over the sill; a GraphUpdateScene tags the interior RobotsOnly; AstarPath names the tag.
    static string Door(Transform root, Transform house, Func<Transform, string, Transform> child)
    {
        Transform link = child(root, "DoorLink_House");
        link.SetPositionAndRotation(Floor(house.TransformPoint(DoorInsideLocal)), house.rotation);
        Transform sill = child(link, "Sill");
        sill.position = Floor(house.TransformPoint(DoorSillLocal));
        Transform outside = child(link, "Outside");
        outside.position = Ground(house.TransformPoint(DoorOutsideLocal));
        var nodeLink = link.GetComponent<Pathfinding.NodeLink2>() ?? link.gameObject.AddComponent<Pathfinding.NodeLink2>();
        nodeLink.end = outside;
        nodeLink.oneWay = false;
        nodeLink.costFactor = 1f;
        nodeLink.pathfindingTag = MB13.Aipocalypse.Core.EnemySystem.NavTags.RobotsOnly;
        EditorUtility.SetDirty(nodeLink);
        var hop = link.GetComponent<RobotDoorHop>() ?? link.gameObject.AddComponent<RobotDoorHop>();
        hop.Setup(sill);
        EditorUtility.SetDirty(hop);

        Transform interior = child(root, "RobotsOnly_House");
        interior.SetPositionAndRotation(house.position, house.rotation);
        var tagged = interior.GetComponent<Pathfinding.GraphUpdateScene>() ?? interior.gameObject.AddComponent<Pathfinding.GraphUpdateScene>();
        tagged.points = InteriorLocal;
        tagged.convex = true;
        tagged.minBoundsHeight = 3f;
        tagged.updatePhysics = false;
        tagged.modifyWalkability = false;
        tagged.modifyTag = true;
        tagged.setTag = MB13.Aipocalypse.Core.EnemySystem.NavTags.RobotsOnly;
        tagged.applyOnStart = true;
        tagged.applyOnScan = true;
        EditorUtility.SetDirty(tagged);

        var astar = Object.FindAnyObjectByType<AstarPath>() ?? throw new Exception("no AstarPath");
        var astarSo = new SerializedObject(astar);
        SerializedProperty names = astarSo.FindProperty("tagNames");
        if (names.arraySize < 32)
        {
            int old = names.arraySize;
            names.arraySize = 32;
            for (int i = old; i < 32; i++)
                names.GetArrayElementAtIndex(i).stringValue = i == 0 ? "Basic Ground" : i.ToString();
        }
        names.GetArrayElementAtIndex(MB13.Aipocalypse.Core.EnemySystem.NavTags.RobotsOnly).stringValue = "Robots only";
        astarSo.ApplyModifiedPropertiesWithoutUndo();
        return $"door link {link.position} → sill {sill.position} → {outside.position}";
    }

    static Robot PlaceRobot(Transform parent, string name, RobotSpot spot)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{ContentDir}/{name}.prefab"), parent);
        go.name = name;
        go.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
        var robot = go.GetComponent<Robot>();
        robot.SetSpot(spot);
        EditorUtility.SetDirty(robot);
        PrefabUtility.RecordPrefabInstancePropertyModifications(robot);
        return robot;
    }

    // What the ground casts hit: not the robots themselves (re-running would put a spot on its robot's head).
    static int GroundMask => Physics.DefaultRaycastLayers & ~(1 << LayerMask.NameToLayer(Layer));

    // The ground under a point outside (terrain or props), from high above.
    static Vector3 Ground(Vector3 point) =>
        Physics.Raycast(point + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, GroundMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : point;

    // The floor under a point inside the house (cast from just above it, under the roof).
    static Vector3 Floor(Vector3 point) =>
        Physics.Raycast(point + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, GroundMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : point;

    // ---- HUD: the [Q] Call line under the [C] Collect one ----

    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";

    public static string Hud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform interact = root.transform.Find("SafeFrame/Center/Interact") ?? throw new Exception("no SafeFrame/Center/Interact");
            Transform prompt = interact.Find("InteractPrompt") ?? throw new Exception("no InteractPrompt");
            Transform old = interact.Find("WhistlePrompt");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            GameObject whistle = Object.Instantiate(prompt.gameObject, interact, false);
            whistle.name = "WhistlePrompt";
            ((RectTransform)whistle.transform).anchoredPosition = new Vector2(0f, -92f);
            var hud = interact.GetComponent<MB13.Aipocalypse.Core.UI.InteractPromptHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_whistleView").objectReferenceValue = whistle.GetComponent<MB13.Aipocalypse.Core.UI.InteractPromptView>();
            so.ApplyModifiedPropertiesWithoutUndo();
            NameTag(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? "HUD.prefab: WhistlePrompt under CollectPrompt, NameTag" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    const string TagFontPath = "Assets/_Game/Data/UI/Fonts/SairaBold.asset";

    // The name tag over an aimed-at robot: the job icon over its name, on its own canvas (it moves every frame), no layout
    // groups (they'd leave overrides on the scene's HUD).
    static void NameTag(Transform hud)
    {
        Transform old = hud.Find("NameTag");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var node = new GameObject("NameTag", typeof(RectTransform)).GetComponent<RectTransform>();
        node.SetParent(hud, false);
        node.anchorMin = node.anchorMax = Vector2.zero;
        node.pivot = new Vector2(0.5f, 0f);
        node.sizeDelta = new Vector2(320f, 96f);
        node.gameObject.AddComponent<Canvas>();
        var group = node.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // The job icon on a dark disc, so a pale glyph reads against the sky.
        var disc = new GameObject("IconDisc", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        disc.rectTransform.SetParent(node, false);
        disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        disc.rectTransform.pivot = new Vector2(0.5f, 0f);
        disc.rectTransform.anchoredPosition = new Vector2(0f, 34f);
        disc.rectTransform.sizeDelta = new Vector2(60f, 60f);
        disc.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        disc.color = new Color(0f, 0f, 0f, 0.6f);
        disc.raycastTarget = false;

        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        icon.rectTransform.SetParent(node, false);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(0f, 64f);
        icon.rectTransform.sizeDelta = new Vector2(42f, 42f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = new Color(1f, 0.86f, 0.6f, 1f);
        var iconShadow = icon.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        iconShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        iconShadow.effectDistance = new Vector2(2f, -2f);

        // A soft dark band behind the name so it reads against sky and grass (TMP outlines would edit the shared font material).
        var band = new GameObject("Band", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        band.rectTransform.SetParent(node, false);
        band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        band.rectTransform.pivot = new Vector2(0.5f, 0f);
        band.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        band.rectTransform.sizeDelta = new Vector2(150f, 32f);
        band.color = new Color(0f, 0f, 0f, 0.6f);
        band.raycastTarget = false;

        var name = new GameObject("Name", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        name.rectTransform.SetParent(node, false);
        name.rectTransform.anchorMin = new Vector2(0f, 0f);
        name.rectTransform.anchorMax = new Vector2(1f, 0f);
        name.rectTransform.pivot = new Vector2(0.5f, 0f);
        name.rectTransform.anchoredPosition = Vector2.zero;
        name.rectTransform.sizeDelta = new Vector2(0f, 28f);
        name.font = Load<TMPro.TMP_FontAsset>(TagFontPath);
        name.fontSize = 26f;
        name.alignment = TMPro.TextAlignmentOptions.Center;
        name.color = Color.white;
        name.raycastTarget = false;
        name.text = "";

        var view = node.gameObject.AddComponent<MB13.Aipocalypse.Core.UI.NameTagHud>();
        view.Setup(group, icon, name);
        EditorUtility.SetDirty(view);
    }

    // ---- Audio ----

    const string AudioDir = "Assets/_Game/Data/Audio";
    const string RobotAudioDir = AudioDir + "/Robots";
    const string PlayerAudioDir = AudioDir + "/Player";
    const string PlayerSoundsPath = "Assets/_Game/Data/Player/PlayerSoundTuning.asset";
    const string Droid = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/Retro_Classic/Robot/retro_robot_driod_beeps_";
    const string Blip = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/Retro_Classic/Menu_UI_Beeps/retro_blip_beep_";
    const string Whistle = "Assets/_StoreAssets/Characters/Malbers Animations/Common/Audio/Whistle/Whistle.wav";

    // Placeholder beeps (Gamemaster droid beeps, split by moment, pitched for mood) and the player's whistle (Malbers).
    public static string Audio()
    {
        var log = new List<string>();
        Folder(RobotAudioDir);
        Ami.BroAudio.Data.AudioAsset robots = Library(AudioDir + "/Robots.asset", "Robots", log);
        Ami.BroAudio.Data.SpatialSetting near = Spatial(RobotAudioDir + "/Robot_Spatial.asset", 1.5f, 30f);
        var settings = Load<RobotSettings>(SettingsPath);
        var sounds = new (RobotSound Sound, string Name, float Volume, float Pitch, string[] Clips)[]
        {
            (RobotSound.Greet, "Robot_Greet", 0.55f, 1f, Clips(Droid, 1, 2, 3, 4)),
            (RobotSound.Happy, "Robot_Happy", 0.55f, 1.1f, Clips(Droid, 5, 6, 7)),
            (RobotSound.Called, "Robot_Called", 0.6f, 1.2f, Clips(Droid, 8, 9)),
            (RobotSound.Chirp, "Robot_Chirp", 0.5f, 1.15f, Clips(Droid, 10, 11, 12)),
            (RobotSound.Shrug, "Robot_Shrug", 0.5f, 0.8f, Clips(Droid, 13, 14)),
            (RobotSound.Nervous, "Robot_Nervous", 0.55f, 1.3f, Clips(Droid, 15, 16)),
            (RobotSound.Hit, "Robot_Hit", 0.65f, 1.4f, Clips(Droid, 17)),
            (RobotSound.Wake, "Robot_Wake", 0.4f, 1f, Clips(Blip, 1, 2, 3)),
        };
        settings.Sounds = new Dictionary<RobotSound, Ami.BroAudio.SoundID>();
        foreach ((RobotSound sound, string name, float volume, float pitch, string[] clips) in sounds)
        {
            Ami.BroAudio.Data.AudioEntity entity = Entity(robots, RobotAudioDir, name, volume, pitch, 0.06f, clips);
            Finish(entity, near, log);
            settings.Sounds[sound] = new Ami.BroAudio.SoundID(entity);
        }
        Save(settings);
        Save(robots);

        var player = Load<Ami.BroAudio.Data.AudioAsset>(AudioDir + "/Player.asset");
        Ami.BroAudio.Data.AudioEntity whistle = Entity(player, PlayerAudioDir, "Player_Whistle", 0.8f, 1f, 0.04f, Whistle);
        Finish(whistle, null, log);
        Save(player);
        var playerSounds = new SerializedObject(Load<Object>(PlayerSoundsPath));
        SerializedProperty whistleId = playerSounds.FindProperty("_whistleSound");
        whistleId.boxedValue = new Ami.BroAudio.SoundID(whistle);
        playerSounds.ApplyModifiedPropertiesWithoutUndo();
        Save(playerSounds.targetObject);
        return "audio: " + string.Join(", ", log);
    }

    static string[] Clips(string prefix, params int[] numbers) => numbers.Select(n => $"{prefix}{n:00}.wav").ToArray();

    static Ami.BroAudio.Data.AudioAsset Library(string path, string name, List<string> log)
    {
        var library = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioAsset>(path);
        if (library != null)
            return library;
        library = ScriptableObject.CreateInstance<Ami.BroAudio.Data.AudioAsset>();
        AssetDatabase.CreateAsset(library, path);
        var so = new SerializedObject(library);
        so.FindProperty("<AssetName>k__BackingField").stringValue = name;
        so.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(path);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("library " + path);
        return library;
    }

    // A one-shot SFX entity with shuffled clips, a base pitch and a random pitch range (as AnimalSetup builds them).
    static Ami.BroAudio.Data.AudioEntity Entity(Ami.BroAudio.Data.AudioAsset library, string dir, string name, float volume, float pitch, float pitchRandom, params string[] clips)
    {
        string path = $"{dir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioEntity>(path);
        if (entity == null)
        {
            entity = Ami.BroAudio.Data.AudioEntity.CreateNewInstance(library, name, Ami.BroAudio.BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)Ami.BroAudio.BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = pitch;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)(clips.Length > 1 ? Ami.BroAudio.Data.MulticlipsPlayMode.Shuffle : Ami.BroAudio.Data.MulticlipsPlayMode.Single);
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

    static void Finish(Ami.BroAudio.Data.AudioEntity entity, Ami.BroAudio.Data.SpatialSetting spatial, List<string> log)
    {
        var so = new SerializedObject(entity);
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        log.Add(entity.name);
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
        Save(spatial);
        return spatial;
    }

    // ---- Helpers ----

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
