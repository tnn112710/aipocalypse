// Builds the Goblin enemy kind from the WanderCapsule enemy contract: unity command script run --file
// AgentScripts/Characters/Goblin/BuildGoblin.cs --entry BuildGoblin.All. Needs the models (SidekickKit.Build) and the
// controller (CharacterKit.BuildLocomotionController, Goblin.animspec.txt) and the Melee subtree (TreeKit.Build of
// AgentScripts/Enemies/Melee.tree.txt) first. Re-running creates only what's missing
// and upgrades existing prefabs in place (LoadPrefabContents keeps file IDs): rebuilding Goblin.prefab would give its
// children new file IDs and break the variants' removed-Model overrides.
using System;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EnemySystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BuildGoblin
{
    const string Folder = "Assets/_Game/Content/Enemies/Goblin";
    const string Template = "Assets/_Game/Content/Enemies/WanderCapsule/WanderCapsule.prefab";
    const string AnimationSettings = "Assets/_Game/Data/Enemies/EnemyAnimationSettings.asset";
    const string ReactionSettings = "Assets/_Game/Data/Enemies/EnemyReactionSettings.asset";
    const string MeleeTree = "Assets/_Game/Content/Enemies/_Shared/Melee.asset";
    const string SwordModel = "Assets/_StoreAssets/Env/Animpic/Poly-Survival Melee Weapons/Prefab/SM_Handcrafted_Sword.prefab";
    const int Fidgets = 3;
    const int Deaths = 2;
    const int Attacks = 3;
    const int Celebrations = 4;
    // The sword: scaled to goblin size, held at this height up its handle (model units; the handle spans -0.2..0.14, the
    // blade's edges face its local +-Z), a little into the palm from the knuckles.
    const float SwordScale = 0.75f;
    const float SwordGripY = 0.05f;
    const float GripTowardsKnuckles = 0.7f;
    const float GripIntoPalm = 0.025f;
    // Head hitbox: a sphere this far above the head bone (the skull's middle), and its radius (m).
    const float HeadLift = 0.12f;
    const float HeadRadius = 0.2f;
    // Root capsule: legs and hips up to the hips bone + margin; the torso sphere on the upper chest.
    const float BodyRadius = 0.4f;
    const float HipsMargin = 0.2f;
    const float ChestRadius = 0.24f;
    // Goblin.prefab wears Goblin_01; each other look is a variant Goblin_NN.prefab.
    static readonly string[] VariantLooks = { "Goblin_02", "Goblin_03", "Goblin_04", "Goblin_05", "Goblin_06" };

    public static string All()
    {
        EnemyAnimationSettings animation = LoadOrCreate<EnemyAnimationSettings>(AnimationSettings, _ => { });
        EnemyReactionSettings reaction = LoadOrCreate<EnemyReactionSettings>(ReactionSettings, _ => { });
        WanderSettings wander = LoadOrCreate<WanderSettings>($"{Folder}/Goblin_Wander.asset", w =>
        {
            w.Radius = 10f;
            w.PauseDuration = 4.5f;
            w.TravelTimeout = 20f;
            w.DestinationAttempts = 12;
            w.MinTravelDistance = 2f;
        });
        MeleeSettings melee = LoadOrCreate<MeleeSettings>($"{Folder}/Goblin_Melee.asset", _ => { });
        EnemyDefinition definition = LoadOrCreate<EnemyDefinition>($"{Folder}/Goblin.asset", d =>
        {
            d.StopDistance = 0.3f;
            d.GrassBendRadius = 0.6f;
        });
        definition.SetBaseStat(EnemyStat.MaxHealth, 100f);
        // The walk clip's authored speed, so it plays at its own pace.
        definition.SetBaseStat(EnemyStat.MoveSpeed, 1.4f);
        definition.Wander = wander;
        definition.Melee = melee;
        definition.Animation = animation;
        definition.Reaction = reaction;
        EditorUtility.SetDirty(definition);
        foreach (ScriptableObject asset in new ScriptableObject[] { animation, reaction, wander, melee, definition })
            AssetDatabase.SaveAssetIfDirty(asset);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Folder}/Goblin.controller");
        if (controller == null)
            return "no Goblin.controller: build it from AgentScripts/Characters/Goblin/Goblin.animspec.txt first";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(MeleeTree) == null)
            return "no Melee subtree: build it from AgentScripts/Enemies/Melee.tree.txt (TreeKit.Build, aipocalypse-enemy-ai skill) first";
        GameObject sword = SwordPrefab();

        Scene active = SceneManager.GetActiveScene();
        Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temp);
        try
        {
            var results = new System.Collections.Generic.List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Goblin.prefab") == null)
                results.Add(BuildBase(definition, controller));
            results.Add(Upgrade($"{Folder}/Goblin.prefab", isBase: true, sword));
            foreach (string look in VariantLooks)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{look}.prefab") == null)
                    results.Add(BuildVariant(look, controller));
                results.Add(Upgrade($"{Folder}/{look}.prefab", isBase: false, sword));
            }
            return results.Count == 0 ? "nothing missing" : string.Join("; ", results);
        }
        finally
        {
            if (active.IsValid())
                SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(temp, true);
        }
    }

    // Goblin.prefab: the capsule's working enemy (FollowerEntity, mover, tree, brain, collider, layer) with the model instead.
    static string BuildBase(EnemyDefinition definition, RuntimeAnimatorController controller)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Template));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "Goblin";
        Transform capsule = root.transform.Find("Capsule");
        if (capsule != null)
            Object.DestroyImmediate(capsule.gameObject);

        Animator animator = AddModel(root, "Goblin_01", controller);
        var enemyAnimator = root.AddComponent<EnemyAnimator>();
        var serialized = new SerializedObject(enemyAnimator);
        serialized.FindProperty("_animator").objectReferenceValue = animator;
        serialized.FindProperty("_fidgets").intValue = Fidgets;
        serialized.FindProperty("_deaths").intValue = Deaths;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetVisual(root.AddComponent<EnemyBody>(), animator.transform);
        var brain = new SerializedObject(root.GetComponent<EnemyBrain>());
        brain.FindProperty("_definition").objectReferenceValue = definition;
        brain.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{Folder}/Goblin.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
        Object.DestroyImmediate(root);
        return saved ? path : "failed to save " + path;
    }

    // <look>.prefab: a variant of Goblin.prefab with another model; everything else follows the base.
    static string BuildVariant(string look, RuntimeAnimatorController controller)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Goblin.prefab"));
        root.name = look;
        GameObject oldModel = root.transform.Find("Model").gameObject;
        try
        {
            Object.DestroyImmediate(oldModel);
        }
        catch (Exception e)
        {
            Object.DestroyImmediate(root);
            return "variant: can't remove the base model from a prefab instance (" + e.Message + ")";
        }
        Animator animator = AddModel(root, look, controller);
        var serialized = new SerializedObject(root.GetComponent<EnemyAnimator>());
        serialized.FindProperty("_animator").objectReferenceValue = animator;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetVisual(root.GetComponent<EnemyBody>(), animator.transform);
        string path = $"{Folder}/{look}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
        Object.DestroyImmediate(root);
        return saved ? path : "failed to save " + path;
    }

    // Adds what later versions of the enemy contract need to an existing prefab, in place: EnemyBody sinking its own Model,
    // hit colliders, the sword in its model's hand; on the base, the death and attack counts and the Melee subtree.
    static string Upgrade(string path, bool isBase, GameObject sword)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform model = root.transform.Find("Model");
            var body = root.GetComponent<EnemyBody>();
            if (body == null)
            {
                if (!isBase)
                    return path + ": no EnemyBody (upgrade Goblin.prefab first)";
                body = root.AddComponent<EnemyBody>();
            }
            SetVisual(body, model);
            AddHeadHitbox(root, model);
            string held = HoldSword(model, sword);
            if (held != null)
                return path + ": " + held;
            if (isBase)
            {
                var serialized = new SerializedObject(root.GetComponent<EnemyAnimator>());
                serialized.FindProperty("_deaths").intValue = Deaths;
                serialized.FindProperty("_attacks").intValue = Attacks;
                serialized.FindProperty("_celebrations").intValue = Celebrations;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var tree = new SerializedObject(root.GetComponent<Opsive.BehaviorDesigner.Runtime.BehaviorTree>());
                tree.FindProperty("m_Subtree").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(MeleeTree);
                tree.FindProperty("m_GraphName").stringValue = "Melee";
                tree.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            return saved ? path + " upgraded" : "failed to save " + path;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Hit colliders that follow the pose (goblins hunch, so the head sits low and forward): the root capsule covers legs and
    // hips only, a sphere on the upper chest (halfway to the neck) the torso, and a critical sphere on the head bone.
    static void AddHeadHitbox(GameObject root, Transform model)
    {
        var animator = model.GetComponent<Animator>();
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

        var capsule = root.GetComponent<CapsuleCollider>();
        capsule.radius = BodyRadius;
        capsule.height = root.transform.InverseTransformPoint(hips.position).y + HipsMargin;
        capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);

        if (!chest.TryGetComponent(out SphereCollider torso))
            torso = chest.gameObject.AddComponent<SphereCollider>();
        torso.center = chest.InverseTransformPoint(Vector3.Lerp(chest.position, neck.position, 0.5f));
        torso.radius = ChestRadius / Mathf.Max(0.0001f, chest.lossyScale.x);

        if (!head.TryGetComponent(out SphereCollider sphere))
            sphere = head.gameObject.AddComponent<SphereCollider>();
        sphere.center = head.InverseTransformPoint(head.position + Vector3.up * HeadLift);
        sphere.radius = HeadRadius / Mathf.Max(0.0001f, head.lossyScale.x);
        if (!head.TryGetComponent(out Hitbox _))
            head.gameObject.AddComponent<Hitbox>();
    }

    // Goblin_Sword.prefab: an unpacked copy of the store model (store prefab untouched), no collider, so hits pass to the body.
    static GameObject SwordPrefab()
    {
        string path = $"{Folder}/Goblin_Sword.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SwordModel);
        var sword = new GameObject("Goblin_Sword");
        sword.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        sword.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(sword, path);
        Object.DestroyImmediate(sword);
        return saved;
    }

    // Puts (or re-places) the sword in the right hand, from the bind pose: the blade comes out of the fist on the thumb side
    // (little -> index knuckles), its edge leading along the fingers. Bone-relative, so it follows every pose.
    static string HoldSword(Transform model, GameObject swordPrefab)
    {
        var animator = model.GetComponent<Animator>();
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
            return "the avatar has no right hand finger bones to hold the sword";
        Transform sword = hand.Find("Sword");
        if (sword == null)
        {
            sword = ((GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, hand)).transform;
            sword.name = "Sword";
            sword.gameObject.layer = hand.gameObject.layer;
        }
        Vector3 up = (index.position - little.position).normalized;
        Vector3 fingers = Vector3.ProjectOnPlane(middle.position - hand.position, up).normalized;
        Vector3 palm = Vector3.Cross(fingers, up);
        Quaternion rotation = Quaternion.LookRotation(fingers, up);
        Vector3 grip = Vector3.Lerp(hand.position, middle.position, GripTowardsKnuckles) + palm * GripIntoPalm;
        sword.SetPositionAndRotation(grip - rotation * (Vector3.up * SwordGripY * SwordScale), rotation);
        sword.localScale = Vector3.one * (SwordScale / Mathf.Max(0.0001f, hand.lossyScale.x));
        return null;
    }

    static void SetVisual(EnemyBody body, Transform visual)
    {
        var serialized = new SerializedObject(body);
        serialized.FindProperty("_visual").objectReferenceValue = visual;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Animator AddModel(GameObject root, string model, RuntimeAnimatorController controller)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Model/{model}_Model.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        instance.name = "Model";
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        SetLayerRecursively(instance, root.layer);
        var animator = instance.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return animator;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    static T LoadOrCreate<T>(string path, Action<T> initialize) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        initialize(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
