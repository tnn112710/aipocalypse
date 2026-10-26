// Builds the Goblin enemy kind from the WanderCapsule enemy contract: unity command script run --file
// AgentScripts/Characters/Goblin/BuildGoblin.cs --entry BuildGoblin.All. Needs the models (SidekickKit.Build) and the
// controller (CharacterKit.BuildLocomotionController, Goblin.animspec.txt) first. Re-running creates only what's missing:
// rebuilding Goblin.prefab would give its children new file IDs and break the variants' removed-Model overrides.
using System;
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
    const int Fidgets = 3;
    // Goblin.prefab wears Goblin_01; each other look is a variant Goblin_NN.prefab.
    static readonly string[] VariantLooks = { "Goblin_02", "Goblin_03", "Goblin_04", "Goblin_05", "Goblin_06" };

    public static string All()
    {
        EnemyAnimationSettings animation = LoadOrCreate<EnemyAnimationSettings>(AnimationSettings, _ => { });
        WanderSettings wander = LoadOrCreate<WanderSettings>($"{Folder}/Goblin_Wander.asset", w =>
        {
            w.Radius = 10f;
            w.PauseDuration = 4.5f;
            w.TravelTimeout = 20f;
            w.DestinationAttempts = 12;
            w.MinTravelDistance = 2f;
        });
        EnemyDefinition definition = LoadOrCreate<EnemyDefinition>($"{Folder}/Goblin.asset", d =>
        {
            d.MoveSpeed = 1.4f;
            d.StopDistance = 0.3f;
            d.GrassBendRadius = 0.6f;
        });
        definition.Wander = wander;
        definition.Animation = animation;
        EditorUtility.SetDirty(definition);
        foreach (ScriptableObject asset in new ScriptableObject[] { animation, wander, definition })
            AssetDatabase.SaveAssetIfDirty(asset);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Folder}/Goblin.controller");
        if (controller == null)
            return "no Goblin.controller: build it from AgentScripts/Characters/Goblin/Goblin.animspec.txt first";

        Scene active = SceneManager.GetActiveScene();
        Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temp);
        try
        {
            var results = new System.Collections.Generic.List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Goblin.prefab") == null)
                results.Add(BuildBase(definition, controller));
            foreach (string look in VariantLooks)
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{look}.prefab") == null)
                    results.Add(BuildVariant(look, controller));
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
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
        string path = $"{Folder}/{look}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
        Object.DestroyImmediate(root);
        return saved ? path : "failed to save " + path;
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
