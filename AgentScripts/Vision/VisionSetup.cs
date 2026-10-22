using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Player vision light + belt lantern. Run with `unity command run_script --file AgentScripts/Vision/VisionSetup.cs --entry VisionSetup.<Entry>`.
public static class VisionSetup
{
    const string TuningPath = "Assets/_Game/Data/Player/PlayerTuning.asset";
    const string VisionTuningPath = "Assets/_Game/Data/Player/PlayerVisionTuning.asset";
    const string LanternPath = "Assets/_Game/Data/Items/Lantern.asset";
    const string LanternPrefabPath = "Assets/_Game/_Core/EquipmentSystem/Prefabs/Lantern.prefab";
    const string PlayerPrefabPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    // URP Lit like the scene; the toon-shaded lanterns turned saturated blue under the night light.
    const string LanternModelPath = "Assets/_StoreAssets/Env/Animpic/Polygon-Mega Survival/Prefabs/New/SM_Kerosene_Lamp.prefab";
    const float LanternScale = 1f;

    // Vision tuning (linked from PlayerTuning), the Lantern item and its pickup prefab. Keeps existing assets' values.
    public static string Assets()
    {
        var vision = LoadOrCreate<PlayerVisionTuning>(VisionTuningPath);
        var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
        var tuningSo = new SerializedObject(tuning);
        tuningSo.FindProperty("<Vision>k__BackingField").objectReferenceValue = vision;
        tuningSo.ApplyModifiedPropertiesWithoutUndo();

        var lantern = LoadOrCreate<LanternDefinition>(LanternPath);
        WorldItem prefab = BuildLanternPrefab(lantern);
        var lanternSo = new SerializedObject(lantern);
        if (string.IsNullOrEmpty(lanternSo.FindProperty("<DisplayName>k__BackingField").stringValue))
            lanternSo.FindProperty("<DisplayName>k__BackingField").stringValue = "Lantern";
        lanternSo.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab;
        lanternSo.ApplyModifiedPropertiesWithoutUndo();

        // Not SaveAssets: it would also write whatever else is dirty in the editor (MicroVerse terrain data).
        EditorUtility.SetDirty(tuning);
        EditorUtility.SetDirty(lantern);
        AssetDatabase.SaveAssetIfDirty(tuning);
        AssetDatabase.SaveAssetIfDirty(lantern);
        AssetDatabase.SaveAssetIfDirty(vision);
        return $"vision {VisionTuningPath}, lantern {LanternPath}, prefab {LanternPrefabPath}";
    }

    // Puts PlayerVisionTuning back to the script's defaults and re-saves the Lantern item (drops renamed fields).
    public static string Defaults()
    {
        var vision = AssetDatabase.LoadAssetAtPath<PlayerVisionTuning>(VisionTuningPath);
        var fresh = ScriptableObject.CreateInstance<PlayerVisionTuning>();
        EditorUtility.CopySerialized(fresh, vision);
        Object.DestroyImmediate(fresh);
        vision.name = System.IO.Path.GetFileNameWithoutExtension(VisionTuningPath);
        EditorUtility.SetDirty(vision);
        AssetDatabase.SaveAssetIfDirty(vision);

        var lantern = AssetDatabase.LoadAssetAtPath<LanternDefinition>(LanternPath);
        EditorUtility.SetDirty(lantern);
        AssetDatabase.SaveAssetIfDirty(lantern);
        return "defaults applied";
    }

    // VisionLight child on Player.prefab: an off point light + a Darkness weight, wired to Player.
    public static string Player()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var player = root.GetComponent<Player>();
            Transform child = root.transform.Find("VisionLight");
            if (child == null)
            {
                child = new GameObject("VisionLight").transform;
                child.SetParent(root.transform, false);
            }
            child.localPosition = new Vector3(0f, 3f, 0f);
            child.localRotation = Quaternion.identity;

            if (!child.TryGetComponent(out Light light))
                light = child.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.shadows = LightShadows.None;
            light.intensity = 4f;
            light.range = 8f;
            light.color = new Color(0.78f, 0.85f, 1f);
            light.enabled = false;
            light.GetUniversalAdditionalLightData();

            if (!child.TryGetComponent(out TimeOfDayWeight darkness))
                darkness = child.gameObject.AddComponent<TimeOfDayWeight>();
            var darknessSo = new SerializedObject(darkness);
            darknessSo.FindProperty("_source").intValue = (int)ReactorSource.Darkness;
            darknessSo.FindProperty("_response").floatValue = 1.5f;
            darknessSo.ApplyModifiedPropertiesWithoutUndo();

            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("_visionLight").objectReferenceValue = light;
            playerSo.FindProperty("_darkness").objectReferenceValue = darkness;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return "VisionLight added to " + PlayerPrefabPath;
    }

    // A lantern pickup in the open scene at `position` (not saved).
    public static string Place(float x, float y, float z)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(x, y, z);
        Undo.RegisterCreatedObjectUndo(instance, "Place lantern");
        EditorSceneManager.MarkSceneDirty(instance.scene);
        return $"{instance.name} at {instance.transform.position} in {instance.scene.path}";
    }

    static WorldItem BuildLanternPrefab(LanternDefinition definition)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(LanternModelPath);
        Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;

        var root = new GameObject("Lantern");
        try
        {
            var world = new GameObject("World");
            world.transform.SetParent(root.transform, false);
            var model = new GameObject("Model");
            model.transform.SetParent(world.transform, false);
            model.transform.localScale = Vector3.one * LanternScale;
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;

            // A little larger than the model, so the look ray finds it easily.
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = mesh.bounds.size * LanternScale + Vector3.one * 0.08f;
            box.center = mesh.bounds.center * LanternScale;

            var worldItem = root.AddComponent<WorldItem>();
            var so = new SerializedObject(worldItem);
            so.FindProperty("_definition").objectReferenceValue = definition;
            so.FindProperty("_worldModel").objectReferenceValue = world;
            so.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LanternPrefabPath));
            return PrefabUtility.SaveAsPrefabAsset(root, LanternPrefabPath).GetComponent<WorldItem>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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
}
