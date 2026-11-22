using System;
using System.Collections.Generic;
using System.Linq;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using MB13.Aipocalypse.Editor.Localization;
using Pathfinding;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the farm animals: the Animals BroAudio library, Data/Animals/AnimalSettings and its "+N Tokens" number, the cow,
// sheep, chicken (hen and rooster) and pig (definition, animator controller, model and entity prefabs from the SICS Toon
// Farm Pack, on its own toon material), the pen pieces (rail, post, gate, bell from Lakefell parts), and in WorldMap
// the "Home Pen" root (the herd's home in the yard's west corner with no pen fence, the bell by the home gate, pasture in
// front of it, 2 cows, 6 sheep, a rooster, 3 hens, 2 pigs) plus the AnimalInstaller. Safe to re-run: animals already in the scene are kept, missing ones from the list are added.
// The tree is built apart: TreeKit.Build AgentScripts/Animals/Animal.tree.txt.
// Run: unity command script run --file AgentScripts/Animals/AnimalSetup.cs --entry AnimalSetup.All
public static class AnimalSetup
{
    const string Tfp = "Assets/_StoreAssets/Env/SICS/Toon Series/Toon Farm Pack/";
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
    const string DamageNumberPath = "Assets/_Game/_Core/UI/Prefabs/Widgets/DamageNumber.prefab";
    const string TokenNumberPath = ContentDir + "/_Shared/TokenNumber.prefab";
    const string Template = "Assets/_Game/Content/Enemies/WanderCapsule/WanderCapsule.prefab";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string AnimalLayer = "Animal";
    const string PenRootName = "Home Pen";

    // Pen: this far along the two fence sides that meet in the yard's west corner (whole rail slots).
    const float SlotWidth = 2.25f;
    const int SlotsAlongBack = 4;       // along side 0 (towards the south corner)
    const int SlotsAlongSide = 8;       // along side 3 (towards the north corner); the cart stands just past it
    const float RailMeshLength = 1.606f;    // Extension_1_Fence
    const float AgentRadius = 0.5f;         // the WorldMap bake's agent radius
    const float CutOverlap = 0.3f;
    const float PastureDistance = 32f;      // from the raid spawn zone's origin (the home gate), along its facing
    const float PastureRadius = 16f;
    const float TurnSpeed = 180f;           // deg/s, the animals' FollowerEntity
    const float OnSpotTurnSpeed = 120f;     // deg/s, turning to face something while standing
    const float TurnSmoothing = 0.2f;       // s, visual only

    sealed class Kind
    {
        public string Name;
        // Model name (Model/<Name>.prefab) and Toon Farm Pack prefab under Prefabs/Animals/. The first look's entity is
        // <Kind>.prefab, the others' <Name>.prefab.
        public (string Name, string Vendor)[] Looks;
        public string ClipFbx;              // under the pack's Models/
        public string ClipPrefix;
        // Looks whose clips live in another file (the rooster's): look -> (clip file, clip prefix); same clip names.
        public Dictionary<string, (string Fbx, string Prefix)> LookClips = new();
        public string IdleClip = "Idle_01", WalkClip = "Walk", RunClip = "Trot", EatClip = "Eating_01";
        public string SleepClip = "Sleep";  // null: dozes standing, the idle slowed down
        public string LieDownClip, WakeUpClip;  // optional: into and out of Sleep, else a cross-fade
        public float ModelScale = 1f;
        // Blend shape weights (0-100) set on the model over the vendor's, by blend shape name.
        public Dictionary<string, float> BlendShapes = new();
        // Ground speed of the walk and run clips at model scale 1, measured by AgentScripts/Animals/AnimalStrideProbe (the
        // planted feet's backward speed); the clips play at speed / (authored * ModelScale), so the feet stay planted.
        public float AuthoredWalk, AuthoredRun;
        // Spare idle clips (Idle parameter 1..n).
        public string[] Fidgets = Array.Empty<string>();
        public float AgentRadius, AgentHeight;
        public float BodyRadius, BodyLength, BodyHeight;
        public float MarkerHeight;
        public Action<AnimalDefinition> Tune;
    }

    static readonly Kind Cow = new()
    {
        Name = "Cow",
        Looks = new[] { ("Cow_White", "Cows/TFP_Cow_01E"), ("Cow_Brown", "Cows/TFP_Cow_01A") },
        ClipFbx = "TFP_Cow 01_Anim_Source.fbx",
        ClipPrefix = "RIG_Cow_01_",
        SleepClip = null,
        ModelScale = 0.7f,
        AuthoredWalk = 1.23f,
        AuthoredRun = 5.36f,        // a long-striding trot
        Fidgets = new[] { "Idle_02", "Idle_03", "Idle_04", "Idle_05", "Idle_06" },
        AgentRadius = 0.6f,
        AgentHeight = 1.5f,
        BodyRadius = 0.42f,
        BodyLength = 2.3f,
        BodyHeight = 0.85f,
        MarkerHeight = 2f,
        Tune = d =>
        {
            d.DisplayName = LocalizationKit.Ref(Loc.CreaturesTable, "animal.cow");
            d.WalkSpeed = 0.85f;
            d.HurrySpeed = 1.4f;
            d.FleeSpeed = 3f;
            d.IdleChance = 0.3f;
            d.IdleTime = new Vector2(4f, 9f);
            d.DriftChance = 0.6f;
            d.DriftDistance = new Vector2(1f, 2.5f);
            d.DartChance = 0f;
            d.ScareScale = 0.8f;
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
            d.Startled = Id(AnimalAudioDir, "Cow_Hurried");
            d.CallInterval = new Vector2(15f, 40f);
            d.MarkerDropAsleep = 0.4f;     // sleeps standing
        },
    };

    static readonly Kind Sheep = new()
    {
        Name = "Sheep",
        Looks = new[] { ("Sheep_Wool", "Sheep/TFP_Sheep_01A") },
        ClipFbx = "TFP_Sheep 01_Anim_Source.fbx",
        ClipPrefix = "RIG_Sheep_01_",
        WalkClip = "Walk.001",
        BlendShapes = { ["Wool Size"] = 100f },     // the pack's sheep: 35
        ModelScale = 0.85f,
        AuthoredWalk = 0.48f,
        AuthoredRun = 1.27f,        // a trot: no run clip, so fleeing plays it fast
        Fidgets = new[] { "Idle_02", "Idle_03", "Idle_04", "Idle_05" },
        AgentRadius = 0.4f,
        AgentHeight = 1.1f,
        BodyRadius = 0.28f,
        BodyLength = 1.2f,
        BodyHeight = 0.55f,
        MarkerHeight = 1.6f,
        Tune = d =>
        {
            d.DisplayName = LocalizationKit.Ref(Loc.CreaturesTable, "animal.sheep");
            d.WalkSpeed = 0.52f;
            d.HurrySpeed = 0.9f;
            d.FleeSpeed = 3f;
            d.IdleChance = 0.25f;
            d.IdleTime = new Vector2(3f, 8f);
            d.DriftChance = 0.7f;
            d.DriftDistance = new Vector2(0.6f, 1.6f);
            d.DartChance = 0f;
            d.ScareScale = 1f;
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
            d.Startled = Id(AnimalAudioDir, "Sheep_Hurried");
            d.CallInterval = new Vector2(10f, 30f);
            d.MarkerDropAsleep = 0.5f;
        },
    };

    // Hens follow the first chicken out on the pasture: the rooster, placed first.
    static readonly Kind Chicken = new()
    {
        Name = "Chicken",
        Looks = new[] { ("Hen", "Chicken/TFP_Chicken_01A"), ("Rooster", "Chicken/TFP_Rooster_01A") },
        ClipFbx = "TFP_Chicken_01.fbx",
        ClipPrefix = "RIG_TFP_Chicken_01_",
        LookClips = { ["Rooster"] = ("TFP_Rooster_01.fbx", "RIG_TFP_Rooster_01_") },
        RunClip = "Run",
        EatClip = "Eat",
        AuthoredWalk = 0.25f,
        AuthoredRun = 1f,
        Fidgets = new[] { "Idle_02", "WingFlap" },
        AgentRadius = 0.25f,
        AgentHeight = 0.7f,
        BodyRadius = 0.2f,
        BodyLength = 0.6f,
        BodyHeight = 0.4f,
        MarkerHeight = 1.3f,
        Tune = d =>
        {
            d.DisplayName = LocalizationKit.Ref(Loc.CreaturesTable, "animal.chicken");
            d.WalkSpeed = 0.35f;
            d.HurrySpeed = 1.1f;
            d.FleeSpeed = 1.6f;
            d.IdleChance = 0.2f;
            d.IdleTime = new Vector2(1.5f, 4f);
            d.DriftChance = 0.8f;
            d.DriftDistance = new Vector2(0.5f, 1.5f);
            d.DartChance = 0.35f;
            d.ScareScale = 1.5f;
            d.StopDistance = 0.25f;
            d.GrassBendRadius = 0.35f;
            d.GrazeRadius = 6f;
            d.MinTravel = 1f;
            d.GrazeTime = new Vector2(1.5f, 4f);   // pecks
            d.FollowsLeader = true;
            d.FlockRadius = 4f;
            d.TokensPerDayPenned = 3f;
            d.TokensPerDayRoaming = 6f;
            d.HoldDays = 2f;
            d.CollectAt = 2;
            d.Call = Id(AnimalAudioDir, "Chicken_Call");
            d.Hurried = Id(AnimalAudioDir, "Chicken_Hurried");
            d.Startled = Id(AnimalAudioDir, "Chicken_Startled");
            d.CallInterval = new Vector2(8f, 25f);
            d.MarkerDropAsleep = 0.25f;
        },
    };

    static readonly Kind Pig = new()
    {
        Name = "Pig",
        Looks = new[] { ("Pig", "Pigs/TFP_Pig_01A") },
        ClipFbx = "TFP_Pig_01_Anim_Source.fbx",
        ClipPrefix = "RIG_TFP_Pig_01_",
        EatClip = "Eat",
        LieDownClip = "IdleToGround",
        WakeUpClip = "GroundToIdle",
        ModelScale = 0.85f,
        AuthoredWalk = 0.81f,
        AuthoredRun = 1.57f,        // a trot
        Fidgets = new[] { "Idle_02", "Idle_03" },
        AgentRadius = 0.45f,
        AgentHeight = 1f,
        BodyRadius = 0.3f,
        BodyLength = 1.5f,
        BodyHeight = 0.5f,
        MarkerHeight = 1.5f,
        Tune = d =>
        {
            d.DisplayName = LocalizationKit.Ref(Loc.CreaturesTable, "animal.pig");
            d.WalkSpeed = 0.75f;
            d.HurrySpeed = 1.25f;
            d.FleeSpeed = 2.6f;
            d.IdleChance = 0.3f;
            d.IdleTime = new Vector2(4f, 10f);
            d.DriftChance = 0.6f;
            d.DriftDistance = new Vector2(0.8f, 2f);
            d.DartChance = 0f;
            d.ScareScale = 1f;
            d.StopDistance = 0.4f;
            d.GrassBendRadius = 0.8f;
            d.GrazeRadius = 8f;
            d.GrazeTime = new Vector2(5f, 12f);
            d.FollowsLeader = false;
            d.TokensPerDayPenned = 12f;
            d.TokensPerDayRoaming = 24f;
            d.HoldDays = 2f;
            d.CollectAt = 4;
            d.Call = Id(AnimalAudioDir, "Pig_Call");
            d.Hurried = Id(AnimalAudioDir, "Pig_Hurried");
            d.Startled = Id(AnimalAudioDir, "Pig_Startled");
            d.CallInterval = new Vector2(12f, 35f);
            d.MarkerDropAsleep = 0.4f;
        },
    };

    static readonly Kind[] Kinds = { Cow, Sheep, Chicken, Pig };

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
        Finish(Entity(library, AnimalAudioDir, "Chicken_Call", 0.45f, 1f, 0.08f, Range(voices + "chicken_2_cluck_bwak_soft_", 11)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Chicken_Hurried", 0.55f, 1f, 0.08f, Range(voices + "chicken_2_bwak_", 10)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Chicken_Startled", 0.6f, 1f, 0.06f, Range(voices + "chicken_cluck_scream_", 5)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Pig_Call", 0.5f, 1f, 0.06f, Range(voices + "pig_2_hog_single_", 8)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Pig_Hurried", 0.6f, 1f, 0.06f, Range(voices + "pig_2_hog_seq_", 11)), near, log);
        Finish(Entity(library, AnimalAudioDir, "Pig_Startled", 0.6f, 1f, 0.06f, Range(voices + "pig_scream_short_", 5)), near, log);
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
        settings.TokenNumber = TokenNumber();
        settings.DuskHours = 1.5f;
        Save(settings);

        foreach (Kind kind in Kinds)
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
            definition.Fidgets = kind.Fidgets.Length;
            Save(definition);

            AnimatorController shared = Controller(definition, kind.ClipFbx, kind.ClipPrefix, kind, $"{dir}/{kind.Name}.controller");
            for (int i = 0; i < kind.Looks.Length; i++)
            {
                (string look, string vendor) = kind.Looks[i];
                (GameObject model, bool reshaped) = Model(Tfp + $"Prefabs/Animals/{vendor}.prefab", $"{dir}/Model/{look}.prefab", kind.BlendShapes);
                string entityName = i == 0 ? kind.Name : look;
                AnimatorController controller = kind.LookClips.TryGetValue(look, out var clips)
                    ? Controller(definition, clips.Fbx, clips.Prefix, kind, $"{dir}/{entityName}.controller")
                    : shared;
                log.Add(Entity(kind, definition, controller, model, reshaped, $"{dir}/{entityName}.prefab"));
            }
        }
        return "content: " + string.Join(", ", log);
    }

    // "+12 Tokens" over an animal handing its Tokens over: a gold copy of the HUD's damage number (its own pool).
    static DamageNumbersPro.DamageNumber TokenNumber()
    {
        Folder(ContentDir + "/_Shared");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TokenNumberPath) == null && !AssetDatabase.CopyAsset(DamageNumberPath, TokenNumberPath))
            throw new Exception("couldn't copy " + DamageNumberPath);
        var prefab = Load<GameObject>(TokenNumberPath);
        var number = prefab.GetComponent<DamageNumbersPro.DamageNumber>();
        var so = new SerializedObject(number);
        var gold = new Color(1f, 0.82f, 0.25f, 1f);
        foreach (string settings in new[] { "numberSettings", "leftTextSettings", "rightTextSettings" })
        {
            so.FindProperty(settings + ".customColor").boolValue = true;
            so.FindProperty(settings + ".color").colorValue = gold;
        }
        so.FindProperty("enableLeftText").boolValue = true;
        so.FindProperty("leftText").stringValue = "+";
        so.FindProperty("enableRightText").boolValue = true;
        so.FindProperty("rightText").stringValue = " Tokens";
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
        return number;
    }

    // Stand (the idle clip) is home: Eat and the spare idles (Idle = 1..n) branch from it. Locomotion blends idle → walk →
    // run on Speed (walking, then fleeing speed; the clips retimed so the feet stay planted). Sleep from any awake state,
    // through the lie-down clip where the kind has one, and back to Stand through the wake-up clip or a cross-fade.
    static AnimatorController Controller(AnimalDefinition definition, string fbx, string prefix, Kind kind, string path)
    {
        const float SetOff = 0.12f, Stopped = 0.06f, Moving = 0.25f;
        var clips = AssetDatabase.LoadAllAssetsAtPath(Tfp + "Models/" + fbx).OfType<AnimationClip>().ToDictionary(c => c.name);
        string animations = System.IO.Path.GetDirectoryName(path).Replace('\\', '/') + "/Animations";
        AnimationClip Clip(string name) => clips.TryGetValue(prefix + name, out AnimationClip clip) ? clip : throw new Exception($"no clip {prefix}{name} in {fbx}");
        AnimationClip Loop(string name) => Looping(Clip(name), animations, prefix);

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
        controller.AddParameter("Idle", AnimatorControllerParameterType.Int);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        float scale = kind.ModelScale;
        AnimationClip idle = Loop(kind.IdleClip);

        var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0f);
        tree.AddChild(Loop(kind.WalkClip), definition.WalkSpeed);
        tree.AddChild(Loop(kind.RunClip), definition.FleeSpeed);
        ChildMotion[] children = tree.children;
        children[1].timeScale = definition.WalkSpeed / (kind.AuthoredWalk * scale);
        children[2].timeScale = definition.FleeSpeed / (kind.AuthoredRun * scale);
        tree.children = children;

        AnimatorState stand = machine.AddState("Stand");
        stand.motion = idle;
        machine.defaultState = stand;
        AnimatorState locomotion = machine.AddState("Locomotion");
        locomotion.motion = tree;
        Transition(stand.AddTransition(locomotion), 0.2f).AddCondition(AnimatorConditionMode.Greater, SetOff, "Speed");
        Transition(locomotion.AddTransition(stand), 0.25f).AddCondition(AnimatorConditionMode.Less, Stopped, "Speed");
        var awake = new List<AnimatorState> { stand, locomotion };

        AnimatorState eat = machine.AddState("Eat");
        eat.motion = Loop(kind.EatClip);
        AnimatorStateTransition toEat = Transition(stand.AddTransition(eat), 0.4f);
        toEat.AddCondition(AnimatorConditionMode.If, 0f, "Eat");
        toEat.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        Transition(eat.AddTransition(stand), 0.35f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Eat");
        Transition(eat.AddTransition(locomotion), 0.25f).AddCondition(AnimatorConditionMode.Greater, Moving, "Speed");
        awake.Add(eat);

        for (int i = 0; i < kind.Fidgets.Length; i++)
        {
            int number = i + 1;
            AnimatorState fidget = machine.AddState("Idle " + number);
            fidget.motion = Loop(kind.Fidgets[i]);
            AnimatorStateTransition into = Transition(stand.AddTransition(fidget), 0.4f);
            into.AddCondition(AnimatorConditionMode.Equals, number, "Idle");
            into.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            Transition(fidget.AddTransition(stand), 0.4f).AddCondition(AnimatorConditionMode.NotEqual, number, "Idle");
            Transition(fidget.AddTransition(locomotion), 0.25f).AddCondition(AnimatorConditionMode.Greater, Moving, "Speed");
            awake.Add(fidget);
        }

        AnimatorState sleep = machine.AddState("Sleep");
        if (kind.SleepClip != null)
            sleep.motion = Loop(kind.SleepClip);
        else
        {
            sleep.motion = idle;
            sleep.speed = 0.4f;
        }
        AnimatorState fallAsleep = sleep;
        if (kind.LieDownClip != null)
        {
            fallAsleep = machine.AddState("LieDown");
            fallAsleep.motion = Clip(kind.LieDownClip);
            Exit(fallAsleep.AddTransition(sleep), 0.95f, 0.2f);
        }
        // From each awake state, not Any State: a lie-down state would otherwise restart itself while Sleep stays on.
        foreach (AnimatorState state in awake)
            Transition(state.AddTransition(fallAsleep), fallAsleep == sleep ? 0.8f : 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Sleep");
        if (kind.WakeUpClip != null)
        {
            AnimatorState wake = machine.AddState("WakeUp");
            wake.motion = Clip(kind.WakeUpClip);
            Transition(sleep.AddTransition(wake), 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sleep");
            if (fallAsleep != sleep)
                Transition(fallAsleep.AddTransition(wake), 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sleep");
            Exit(wake.AddTransition(stand), 0.9f, 0.25f);
        }
        else
        {
            Transition(sleep.AddTransition(stand), 0.6f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sleep");
            if (fallAsleep != sleep)
                Transition(fallAsleep.AddTransition(stand), 0.4f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sleep");
        }
        Save(controller);
        return controller;
    }

    // The vendor clip itself when it loops; otherwise a looping first-party copy in <Kind>/Animations/ (the sheep and
    // rooster clips import as one-shots). A copy is rewritten in place, keeping its GUID.
    static AnimationClip Looping(AnimationClip clip, string dir, string prefix)
    {
        if (clip.isLooping)
            return clip;
        Folder(dir);
        string name = clip.name.Substring(prefix.Length).Split('.')[0];     // "Walk.001" -> "Walk"
        if (prefix.Contains("Rooster"))
            name = "Rooster_" + name;
        string path = $"{dir}/{name}.anim";
        AnimationClip copy = Object.Instantiate(clip);
        copy.name = name;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }
        EditorUtility.CopySerialized(copy, existing);
        Object.DestroyImmediate(copy);
        Save(existing);
        return existing;
    }

    static void Exit(AnimatorStateTransition transition, float exitTime, float duration)
    {
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.hasFixedDuration = true;
    }

    static AnimatorStateTransition Transition(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        return transition;
    }

    // A first-party copy of the vendor prefab with only the look left: the pack's switched-off look meshes and any vendor
    // components removed, its toon material kept, one Animator on the root with no controller. Reshaped: the saved prefab's
    // top-level children changed (another vendor model), so the entity nests it afresh instead of keeping stale overrides.
    static (GameObject Model, bool Reshaped) Model(string vendorPath, string path, Dictionary<string, float> blendShapes)
    {
        var before = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject root = PrefabUtility.LoadPrefabContents(vendorPath);
        try
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (!child.gameObject.activeSelf)
                    Object.DestroyImmediate(child.gameObject);
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach ((string shape, float weight) in blendShapes)
                {
                    int index = skin.sharedMesh.GetBlendShapeIndex(shape);
                    if (index >= 0)
                        skin.SetBlendShapeWeight(index, weight);
                }
            bool reshaped = before == null || !ChildNames(before.transform).SequenceEqual(ChildNames(root.transform));
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
            return (PrefabUtility.SaveAsPrefabAsset(root, path), reshaped);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static IEnumerable<string> ChildNames(Transform t) => t.Cast<Transform>().Select(c => c.name);

    // The entity: the WanderCapsule's legs and tree (FollowerEntity, EnemyMover, Manual BehaviorTree) with the Animal on
    // top, the model nested as Model, a body collider to look at, the coin marker and a SaveId. An existing prefab is
    // updated in place.
    static string Entity(Kind kind, AnimalDefinition definition, AnimatorController controller, GameObject model, bool reshaped, string path)
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
            // Calmer turning than an enemy's 600 deg/s, smoothed: a nudge from a neighbour no longer snaps the body round.
            agent.FindProperty("movement.follower.rotationSpeed").floatValue = TurnSpeed;
            agent.FindProperty("movement.follower.maxRotationSpeed").floatValue = TurnSpeed * 1.5f;
            agent.FindProperty("movement.follower.maxOnSpotRotationSpeed").floatValue = OnSpotTurnSpeed;
            // Big turns while walking are arcs, not a spin on the spot with the feet skating.
            agent.FindProperty("movement.follower.allowRotatingOnSpotBacking").boolValue = false;
            agent.FindProperty("movement.rotationSmoothing").floatValue = TurnSmoothing;
            agent.ApplyModifiedPropertiesWithoutUndo();

            var tree = new SerializedObject(root.GetComponent<Opsive.BehaviorDesigner.Runtime.BehaviorTree>());
            tree.FindProperty("m_Subtree").objectReferenceValue = Load<ScriptableObject>(TreePath);
            tree.ApplyModifiedPropertiesWithoutUndo();

            Transform modelRoot = root.transform.Find("Model");
            if (modelRoot != null && (reshaped || PrefabUtility.GetCorrespondingObjectFromSource(modelRoot.gameObject) != model))
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
        // No pen fence (user, 2026-11-22): the herd lives loose in the yard's west corner, in the same box as the old pen,
        // and the bell by the home gate lets it out and calls it home. PenRail / PenGate stay as content for other scenes.
        Transform fence = root.transform.Find("Fence");
        if (fence != null)
            Object.DestroyImmediate(fence.gameObject);

        Transform homeGate = (GameObject.Find("Environment/Landmarks/Home Base/Fence/Gate") ?? throw new Exception("no home gate")).transform;
        Transform homeFence = homeGate.parent;
        Vector3 yard = Vector3.zero;
        int segments = 0;
        foreach (Transform child in homeFence)
            if (child.name.StartsWith("Fence "))
            {
                yard += child.position;
                segments++;
            }
        yard /= segments;
        Vector3 inward = Vector3.ProjectOnPlane(yard - homeGate.position, Vector3.up).normalized;
        Vector3 along = Vector3.ProjectOnPlane(homeGate.right, Vector3.up).normalized;

        Quaternion frontRotation = Quaternion.LookRotation(side, Vector3.up);
        Vector3 center = Ground(corner + side * (lengthSide * 0.5f) + back * (lengthBack * 0.5f));
        Transform penTransform = root.transform.Find("Pen") ?? Child(root.transform, "Pen");
        penTransform.SetPositionAndRotation(center, frontRotation);
        // Where a called animal waits when the yard can't be reached: just outside the home gate.
        Transform wait = penTransform.Find("WaitPoint") ?? Child(penTransform, "WaitPoint");
        wait.position = Ground(homeGate.position - inward * 2.5f);
        if (!penTransform.TryGetComponent(out AnimalPen pen))
            pen = penTransform.gameObject.AddComponent<AnimalPen>();
        pen.Setup(new Vector2(lengthBack - 0.6f, lengthSide - 0.6f), null, wait);
        EditorUtility.SetDirty(pen);

        Transform bell = root.transform.Find("PenBell");
        if (bell != null)
            Object.DestroyImmediate(bell.gameObject);
        // Inside the yard, beside the double gate (4.5 m wide), facing out.
        Place("PenBell", root.transform, Ground(homeGate.position + along * (SlotWidth + 1f) + inward * 0.9f), Quaternion.LookRotation(-inward, Vector3.up));

        // The pasture: on the meadow in front of the home gate.
        GameObject zone = GameObject.Find("Raid/Spawn Zone (Front)") ?? throw new Exception("no Raid/Spawn Zone (Front): where is the home gate?");
        Transform pastureTransform = root.transform.Find("Pasture") ?? Child(root.transform, "Pasture");
        pastureTransform.position = Ground(zone.transform.position + zone.transform.forward * PastureDistance);
        if (!pastureTransform.TryGetComponent(out AnimalPasture pasture))
            pasture = pastureTransform.gameObject.AddComponent<AnimalPasture>();
        pasture.Setup(PastureRadius);
        EditorUtility.SetDirty(pasture);

        // The herd: those already there are kept (their SaveIds are in saves); the rest of the list is added after them, in
        // pen space (z along the side, away from the corner). The first six stood round the old, shorter pen's middle.
        Transform herd = root.transform.Find("Animals") ?? Child(root.transform, "Animals");
        (string prefab, Vector2 at, float yaw, float scale)[] animals =
        {
            ("Cow/Cow", new Vector2(-1.8f, -0.8f), 200f, 1f),
            ("Cow/Cow_Brown", new Vector2(1.6f, -5.6f), 70f, 0.95f),
            ("Sheep/Sheep", new Vector2(1.9f, -1f), 150f, 1f),
            ("Sheep/Sheep", new Vector2(-2.2f, -4f), 20f, 0.92f),
            ("Sheep/Sheep", new Vector2(0.2f, -7f), 310f, 1.05f),
            ("Sheep/Sheep", new Vector2(-1.2f, -6.3f), 250f, 0.88f),
            ("Sheep/Sheep", new Vector2(2.6f, 1.8f), 110f, 0.97f),
            ("Sheep/Sheep", new Vector2(0.6f, 2.6f), 300f, 1.02f),
            ("Chicken/Rooster", new Vector2(-2.4f, 4.6f), 40f, 1f),
            ("Chicken/Chicken", new Vector2(-1.5f, 5.4f), 160f, 1f),
            ("Chicken/Chicken", new Vector2(-3f, 6f), 260f, 0.95f),
            ("Chicken/Chicken", new Vector2(-1.9f, 6.6f), 20f, 1.05f),
            ("Pig/Pig", new Vector2(2.2f, 5.6f), 230f, 1f),
            ("Pig/Pig", new Vector2(1f, 7.2f), 80f, 0.92f),
        };
        int kept = herd.childCount;
        for (int i = kept; i < animals.Length; i++)
        {
            (string prefab, Vector2 at, float yaw, float scale) = animals[i];
            var asset = Load<GameObject>($"{ContentDir}/{prefab}.prefab");
            var animal = (GameObject)PrefabUtility.InstantiatePrefab(asset, herd);
            animal.transform.SetPositionAndRotation(Ground(penTransform.TransformPoint(new Vector3(at.x, 0f, at.y))), Quaternion.Euler(0f, yaw, 0f));
            animal.transform.localScale = Vector3.one * scale;
            animal.GetComponent<SaveId>().Regenerate();
            animal.name = $"{animal.name} {i + 1}";
        }
        log.Add($"{kept} animals kept, {Mathf.Max(0, animals.Length - kept)} placed");

        // The vendor cow once stood by hand next to the pen; it is not part of the system.
        GameObject stray = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Cow_White");
        if (stray != null)
        {
            Object.DestroyImmediate(stray);
            log.Add("stray Cow_White removed");
        }

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
        return $"scene: herd home centre {center}, bell by the home gate {homeGate.position}, pasture {pasture.Center} r{PastureRadius}; " + string.Join(", ", log);
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
