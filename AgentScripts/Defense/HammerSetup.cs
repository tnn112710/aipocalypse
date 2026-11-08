using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The repair hammer as a hotbar item, and what goes with it: Content/Tools/Hammer (item + one prefab, moved out of the
// Player's old built-in RepairHammer, keeping its poses and fitted grip), a fourth hotbar slot that takes tools, the HUD's
// [R] line, the repair highlight look (Data/Defense) and, in WorldMap, the highlight style and a hammer on the yard table.
// Safe to re-run. Run: unity command script run --file AgentScripts/Defense/HammerSetup.cs --entry HammerSetup.All
public static class HammerSetup
{
    const string Dir = "Assets/_Game/Content/Tools/Hammer";
    const string PrefabPath = Dir + "/Hammer.prefab";
    const string ItemPath = Dir + "/Hammer.asset";
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string ConfigPath = "Assets/_Game/Data/Equipment/EquipmentConfig.asset";
    const string IconPath = "Assets/Synty/InterfaceApocalypseHUD/Sprites/Icons_Weapons/ICON_SM_Wep_Hammer_01.png";
    const string HammerMeshPath = "Assets/_StoreAssets/Env/Animpic/Polygon-Mega Survival Tools/Prefabs/SM_Hammer.prefab";
    const string HighlightDir = "Assets/_Game/Data/Defense";
    const string XrayShader = "Aipocalypse/Ghosts/GhostXray";
    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    // After the ghosts (3100, 3101): drawn over them.
    const int HighlightQueue = 3102;
    const int SlotCount = 4;
    // As big as in the hand.
    const float WorldScale = 1.3f;
    // Metres from the pistol, along the guns' length.
    const float HammerFromPistol = -0.3f;
    // Metres toward the shotguns, clear of the axe's head.
    const float HammerAwayFromAxe = 0.14f;

    public static string All() => string.Join("\n", Item(), Player(), Config(), Hud(), Highlight(), Scene());

    // ---------------- item ----------------

    public static string Item()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Content/Tools"))
            AssetDatabase.CreateFolder("Assets/_Game/Content", "Tools");
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Content/Tools", "Hammer");

        var item = AssetDatabase.LoadAssetAtPath<RepairToolDefinition>(ItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<RepairToolDefinition>();
            AssetDatabase.CreateAsset(item, ItemPath);
        }

        GameObject source = HeldSource(out GameObject playerContents);
        GameObject root;
        try
        {
            root = Object.Instantiate(source);
        }
        finally
        {
            if (playerContents != null)
                PrefabUtility.UnloadPrefabContents(playerContents);
        }

        try
        {
            root.name = "Hammer";
            root.SetActive(true);
            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform hidden = root.transform.Find("Poses/Hidden");
            if (hidden != null)
                Object.DestroyImmediate(hidden.gameObject);
            Transform old = root.transform.Find("World");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var tool = root.GetComponent<RepairTool>();
            tool.Setup(root.transform.Find("PosePivot"), root.transform.Find("Poses/Idle"), root.transform.Find("Poses/WindUp"),
                root.transform.Find("Poses/Strike"));
            if (!root.TryGetComponent(out ItemHands _))
                throw new Exception("the hammer has no ItemHands");

            Transform world = WorldModel(root.transform, out Bounds bounds);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 0;

            if (!root.TryGetComponent(out BoxCollider box))
                box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = bounds.center;
            box.size = bounds.size + new Vector3(0.06f, 0.1f, 0.06f);

            if (!root.TryGetComponent(out WorldItem worldItem))
                worldItem = root.AddComponent<WorldItem>();
            var wso = new SerializedObject(worldItem);
            wso.FindProperty("_definition").objectReferenceValue = item;
            wso.FindProperty("_worldModel").objectReferenceValue = world.gameObject;
            wso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
                return "failed to save " + PrefabPath;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        var iso = new SerializedObject(item);
        iso.FindProperty("_displayName").stringValue = "Hammer";
        iso.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        iso.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldItem>(PrefabPath);
        iso.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(item);
        return $"{ItemPath} + {PrefabPath} (icon {(item.Icon != null ? "set" : "MISSING")})";
    }

    // The old built-in RepairHammer under the Player's held items, or, once that's gone, the hammer prefab itself.
    static GameObject HeldSource(out GameObject playerContents)
    {
        playerContents = null;
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
            return existing;
        playerContents = PrefabUtility.LoadPrefabContents(PlayerPath);
        Transform held = playerContents.transform.Find("CameraRoot/CameraPitch/HeldItems/RepairHammer");
        return held != null ? held.gameObject : throw new Exception("no RepairHammer on the Player and no " + PrefabPath);
    }

    // World > Model: the hammer lying on its side, handle along X, resting on the root's ground. Bounds in the root's space.
    static Transform WorldModel(Transform root, out Bounds bounds)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(HammerMeshPath) ?? throw new Exception("missing " + HammerMeshPath);
        Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
        var world = new GameObject("World");
        world.transform.SetParent(root, false);
        var model = new GameObject("Model");
        model.transform.SetParent(world.transform, false);
        model.AddComponent<MeshFilter>().sharedMesh = mesh;
        model.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;

        Quaternion rotation = Quaternion.Euler(0f, 0f, 90f);
        model.transform.localScale = Vector3.one * WorldScale;
        Bounds turned = Turned(new Bounds(mesh.bounds.center * WorldScale, mesh.bounds.size * WorldScale), rotation);
        // Centred over the root, its lowest point on the ground.
        Vector3 offset = new(-turned.center.x, -turned.min.y, -turned.center.z);
        model.transform.SetLocalPositionAndRotation(offset, rotation);
        bounds = new Bounds(turned.center + offset, turned.size);
        world.SetActive(false);
        return world.transform;
    }

    static Bounds Turned(Bounds bounds, Quaternion rotation)
    {
        var result = new Bounds(rotation * bounds.center, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(rotation * corner);
        }
        return result;
    }

    // ---------------- player: the built-in hammer goes ----------------

    public static string Player()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            return "Player: run Item first";
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            Transform held = root.transform.Find("CameraRoot/CameraPitch/HeldItems/RepairHammer");
            if (held == null)
                return "Player.prefab: no built-in RepairHammer (already removed)";
            Object.DestroyImmediate(held.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath, out bool saved);
            return saved ? "Player.prefab: built-in RepairHammer removed" : "failed to save " + PlayerPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------- hotbar: four slots, all take weapons and tools ----------------

    public static string Config()
    {
        var config = AssetDatabase.LoadAssetAtPath<EquipmentConfig>(ConfigPath);
        var so = new SerializedObject(config);
        SerializedProperty slots = so.FindProperty("_slots");
        slots.arraySize = Mathf.Max(slots.arraySize, SlotCount);
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Name").stringValue = $"Slot {i + 1}";
            slot.FindPropertyRelative("Accepts").intValue = (int)(ItemCategory.Gun | ItemCategory.Melee | ItemCategory.Tool);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(config);
        return $"EquipmentConfig: {slots.arraySize} slots, each Gun | Melee | Tool";
    }

    // ---------------- HUD: the [R] line above the [E] one ----------------

    public static string Hud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform interact = root.transform.Find("SafeFrame/Center/Interact") ?? throw new Exception("no SafeFrame/Center/Interact");
            Transform prompt = interact.Find("InteractPrompt");
            Transform old = interact.Find("ToolPrompt");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            GameObject tool = Object.Instantiate(prompt.gameObject, interact, false);
            tool.name = "ToolPrompt";
            ((RectTransform)tool.transform).anchoredPosition = new Vector2(0f, 46f);

            var hud = interact.GetComponent<InteractPromptHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_toolView").objectReferenceValue = tool.GetComponent<InteractPromptView>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? "HUD.prefab: ToolPrompt above InteractPrompt" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------- repair highlight look ----------------

    public static string Highlight()
    {
        Shader shader = Shader.Find(XrayShader) ?? throw new Exception(XrayShader + " not found: build GhostXray.shadergraph first");
        if (!AssetDatabase.IsValidFolder(HighlightDir))
            AssetDatabase.CreateFolder("Assets/_Game/Data", "Defense");
        string materialPath = HighlightDir + "/RepairHighlight.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        // User Override, so URP's material validation keeps the queue (GhostSetup).
        material.SetFloat("_QueueControl", 1f);
        material.renderQueue = HighlightQueue;
        material.SetFloat("_BandStrength", 0.15f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);

        string stylePath = HighlightDir + "/RepairHighlightStyle.asset";
        var style = AssetDatabase.LoadAssetAtPath<RepairHighlightStyle>(stylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<RepairHighlightStyle>();
            AssetDatabase.CreateAsset(style, stylePath);
        }
        style.Look = material;
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        return $"{stylePath} → {materialPath}";
    }

    // ---------------- WorldMap: highlight style, hammer on the table ----------------

    public static string Scene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            return "open " + ScenePath + " first";
        var log = new List<string>();

        DefenseInstaller installer = Object.FindAnyObjectByType<DefenseInstaller>(FindObjectsInactive.Include);
        if (installer != null)
        {
            var so = new SerializedObject(installer);
            so.FindProperty("_repairHighlight").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RepairHighlightStyle>(HighlightDir + "/RepairHighlightStyle.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("DefenseInstaller: repair highlight style set");
        }

        // Beside the weapons on the yard table, at the end of their row.
        WorldItem[] pickups = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include);
        WorldItem axe = pickups.FirstOrDefault(p => p.name == "FireAxe");
        WorldItem pistol = pickups.FirstOrDefault(p => p.name == "Pistol");
        if (axe == null || pistol == null)
            return string.Join("\n", log.Append("no FireAxe / Pistol pickup to place the hammer by"));
        WorldItem stale = pickups.FirstOrDefault(p => p.name == "Hammer");
        if (stale != null)
            Object.DestroyImmediate(stale.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var hammer = (GameObject)PrefabUtility.InstantiatePrefab(prefab, axe.transform.parent);
        hammer.name = "Hammer";
        // The guns lie side by side across the table; the short pistol leaves room beside it, along the guns' length.
        Vector3 row = axe.transform.position - pistol.transform.position;
        row.y = 0f;
        Vector3 along = Vector3.Cross(Vector3.up, row).normalized;
        Vector3 position = pistol.transform.position + along * HammerFromPistol - row.normalized * HammerAwayFromAxe;
        position.y = axe.transform.position.y;
        hammer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(row.normalized, Vector3.up));
        // As in play: only the world model shows.
        foreach (Transform child in hammer.transform)
            child.gameObject.SetActive(child.name == "World");
        log.Add($"Hammer placed at {position}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("WorldMap saved");
        return string.Join("\n", log);
    }
}
