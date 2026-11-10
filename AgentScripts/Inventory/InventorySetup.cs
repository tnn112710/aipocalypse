using System;
using System.Collections.Generic;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.Stats;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the inventory content: Tokens, ammo boxes, the medic box and the backpack (assets + world prefabs), item weights,
// the consumables (first aid kit, bandage, adrenaline and stamina shots; icons from the Kat Grabowska flat set), item
// values and rarities,
// the goblin loot table and lootable goblin bodies, the loot crate, the EquipmentConfig slots and keys, the inventory UI
// (style, ItemSlot prefab, InventoryScreen / LootMarkers / PickupFeed nodes in HUD.prefab), its sounds (Player library) and
// a test setup in the WorldMap yard. Safe to re-run: assets are updated in place, prefabs and HUD nodes rebuilt.
// Run: unity command script run --file AgentScripts/Inventory/InventorySetup.cs --entry InventorySetup.Build
public static class InventorySetup
{
    const string ItemsDir = "Assets/_Game/Content/Items";
    const string AmmoDir = "Assets/_Game/Content/Weapons/_Shared/Ammo";
    const string LootDir = "Assets/_Game/Data/Loot";
    const string CratePath = "Assets/_Game/Content/Props/LootCrate/LootCrate.prefab";
    const string ConfigPath = "Assets/_Game/Data/Equipment/EquipmentConfig.asset";
    const string TuningPath = "Assets/_Game/Data/Player/PlayerInteractionTuning.asset";
    const string StylePath = "Assets/_Game/Data/UI/InventoryStyle.asset";
    const string HotbarStylePath = "Assets/_Game/Data/UI/HotbarSlotStyle.asset";
    const string SlotPrefabPath = "Assets/_Game/_Core/UI/Prefabs/Widgets/ItemSlot.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string FontPath = "Assets/_Game/Data/UI/Fonts/SairaBold.asset";
    const string Icons = "Assets/Synty/InterfaceApocalypseHUD/Sprites/";
    const string Animpic = "Assets/_StoreAssets/Env/Animpic/";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string YardNode = "InventoryTest";
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string PlayerLib = AudioDir + "/Player.asset";
    const string PlayerAudioDir = AudioDir + "/Player";
    const string SurvivalItems = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Survival Game Sounds - Lite Edition/Objects and Items/";
    const string SurvivalMisc = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Survival Game Sounds - Lite Edition/Miscellaneous/";
    const string InterfaceMisc = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Interface & Item Sounds Lite - Version 2/Miscellaneous/";
    const string InterfaceUi = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Interface & Item Sounds Lite - Version 2/Interface/";

    public static string Build()
    {
        var log = new List<string> { Items(), Weights(), Consumables(), Values(), Loot(), Config(), Sounds(), Interface(), Yard(), Bake() };
        return string.Join("\n", log);
    }

    // ---- Items ----

    public static string Items()
    {
        var tokens = Asset<CurrencyDefinition>($"{ItemsDir}/Tokens/Tokens.asset");
        Define(tokens, "Tokens", Icons + "Icons_Resources/ICON_SM_Prop_Money_Strapped_04.png", maxStack: 1, weight: 0f, unlimited: true, autoCollect: true);
        SetPrefab(tokens, WorldPrefab($"{ItemsDir}/Tokens/Tokens.prefab", tokens,
            "Assets/Synty/PolygonPrototype/Prefabs/Icons/SM_Icon_Coin_03.prefab", 0.4f, new Vector3(0f, 0.13f, 0f), Vector3.zero));

        Ammo("PistolAmmo", Icons + "Icons_Ammo/ICON_SM_Wep_Pistol_Ammo_Box_01.png", 120, 0.012f, "Polygon-Mega Weapons Kit/Prefabs/SM_Ammo_Box_Closed.prefab", 0.22f);
        Ammo("RifleAmmo", Icons + "Icons_Ammo/ICON_SM_Prop_AmmoBox_04.png", 120, 0.016f, "Polygon-Mega Weapons Kit/Prefabs/SM_Ammo_Box_Large_Closed.prefab", 0.3f);
        Ammo("ShotgunShells", Icons + "Icons_Ammo/ICON_SM_Wep_Shotgun_Ammo_Box_01.png", 40, 0.04f, "Polygon-Mega Weapons Kit/Prefabs/SM_Ammo_Box_Wood_Closed.prefab", 0.3f);

        var backpack = Asset<BackpackDefinition>($"{ItemsDir}/Backpack/Backpack.asset");
        Define(backpack, "Backpack", Icons + "Icons_Resources/ICON_SM_Chr_Attach_Backpack_01.png", maxStack: 1, weight: 1f);
        var so = new SerializedObject(backpack);
        so.FindProperty("_extraSlots").intValue = 15;
        so.FindProperty("_extraWeight").floatValue = 15f;
        so.ApplyModifiedPropertiesWithoutUndo();
        SetPrefab(backpack, WorldPrefab($"{ItemsDir}/Backpack/Backpack.prefab", backpack, Animpic + "Polygon-Mega Survival/Prefabs/Backpack.prefab",
            1f, Vector3.zero, Vector3.zero));
        Save(tokens, backpack);
        return "items: Tokens, PistolAmmo/RifleAmmo/ShotgunShells boxes, Backpack (consumables: Consumables)";
    }

    static void Ammo(string name, string icon, int maxStack, float weight, string model, float scale)
    {
        var ammo = AssetDatabase.LoadAssetAtPath<AmmoType>($"{AmmoDir}/{name}.asset") ?? throw new Exception("missing ammo " + name);
        Define(ammo, null, icon, maxStack, weight);
        SetPrefab(ammo, WorldPrefab($"{AmmoDir}/{name}Box.prefab", ammo, Animpic + model, scale, Vector3.zero, Vector3.zero));
        Save(ammo);
    }

    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // `displayName` null keeps the current one.
    static void Define(ItemDefinition item, string displayName, string iconPath, int maxStack, float weight, bool unlimited = false, bool autoCollect = false)
    {
        var so = new SerializedObject(item);
        if (displayName != null)
            so.FindProperty("_displayName").stringValue = displayName;
        so.FindProperty("_icon").objectReferenceValue = Sprite(iconPath);
        so.FindProperty("_maxStack").intValue = maxStack;
        so.FindProperty("_unlimitedStack").boolValue = unlimited;
        so.FindProperty("_weight").floatValue = weight;
        so.FindProperty("_autoCollect").boolValue = autoCollect;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrefab(ItemDefinition item, WorldItem prefab)
    {
        var so = new SerializedObject(item);
        so.FindProperty("_prefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new Exception("missing sprite " + path);

    // A pickup: root WorldItem + trigger box, World/Model holding copies of the vendor model's meshes.
    static WorldItem WorldPrefab(string path, ItemDefinition item, string modelPath, float scale, Vector3 position, Vector3 euler)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            var world = new GameObject("World");
            world.transform.SetParent(root.transform, false);
            CopyModel(modelPath, world.transform, "Model", position, euler, scale);
            FitPickupBox(root, world.transform);
            var worldItem = root.AddComponent<WorldItem>();
            var so = new SerializedObject(worldItem);
            so.FindProperty("_definition").objectReferenceValue = item;
            so.FindProperty("_worldModel").objectReferenceValue = world;
            so.ApplyModifiedPropertiesWithoutUndo();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<WorldItem>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // A pickup that's also held in hand: HeldItem on the root with the first-person Model, the world model under World
    // (inactive in the asset, like the guns).
    static WorldItem HeldPrefab(string path, ItemDefinition item, string modelPath, Vector3 heldPosition, Vector3 heldEuler, float heldScale,
        Vector3 worldEuler, float worldScale = 1f)
    {
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            if (item is ConsumableDefinition)
                root.AddComponent<HeldConsumable>();
            else
                root.AddComponent<HeldItem>();
            Transform held = CopyModel(modelPath, root.transform, "Model", heldPosition, heldEuler, heldScale);
            foreach (Renderer r in held.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var world = new GameObject("World");
            world.transform.SetParent(root.transform, false);
            CopyModel(modelPath, world.transform, "Model", new Vector3(0f, 0.05f, 0f), worldEuler, worldScale);
            FitPickupBox(root, world.transform);
            world.SetActive(false);
            var worldItem = root.AddComponent<WorldItem>();
            var so = new SerializedObject(worldItem);
            so.FindProperty("_definition").objectReferenceValue = item;
            so.FindProperty("_worldModel").objectReferenceValue = world;
            so.ApplyModifiedPropertiesWithoutUndo();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<WorldItem>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Copies a vendor prefab's meshes (no colliders, no scripts) under `parent` as `name`, posed and scaled.
    static Transform CopyModel(string modelPath, Transform parent, string name, Vector3 position, Vector3 euler, float scale)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ?? throw new Exception("missing model " + modelPath);
        var model = new GameObject(name).transform;
        model.SetParent(parent, false);
        model.localPosition = position;
        model.localEulerAngles = euler;
        model.localScale = Vector3.one * scale;
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null)
                continue;
            Transform target = model;
            if (filter.transform != source.transform)
            {
                target = new GameObject(filter.name).transform;
                target.SetParent(model, false);
                Matrix4x4 local = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                target.localPosition = local.GetColumn(3);
                target.localRotation = local.rotation;
                target.localScale = local.lossyScale;
            }
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            target.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        return model;
    }

    // Trigger box around the world model, at least 30 cm a side so small items are easy to aim at.
    static void FitPickupBox(GameObject root, Transform world)
    {
        bool active = world.gameObject.activeSelf;
        world.gameObject.SetActive(true);
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (MeshFilter filter in world.GetComponentsInChildren<MeshFilter>(true))
        {
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = root.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                    bounds = new Bounds(local, Vector3.zero);
                else
                    bounds.Encapsulate(local);
                any = true;
            }
        }
        world.gameObject.SetActive(active);
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size + Vector3.one * 0.05f, Vector3.one * 0.3f);
    }

    // ---- Consumables: used by holding the attack button (HeldConsumable); effects through PlayerEffects ----

    const string Medical = Animpic + "Polygon-Mega Medical Kit/Prefab/";
    // Consumable icons: one set for all of them, from the UI icon packs (ASSETS.md → UI).
    const string FlatIcons = "Assets/_StoreAssets/UI/Survival Flat Icons by Kat Grabowska/250plus survival flat icons/";

    public static string Consumables()
    {
        var kit = Asset<ConsumableDefinition>($"{ItemsDir}/MedicBox/MedicBox.asset");
        Define(kit, "First Aid Kit", FlatIcons + "aidkit.png", maxStack: 1, weight: 0.5f);
        kit.SetUse(3f, uses: 5, moveSpeedScale: 0.5f);
        kit.SetEffects(heal: 20f, healOverTime: 0f, healSeconds: 10f, buffSeconds: 0f, null);
        SetPrefab(kit, HeldPrefab($"{ItemsDir}/MedicBox/MedicBox.prefab", kit, Medical + "SM_Med_Kit.prefab",
            heldPosition: new Vector3(0.1f, -0.02f, 0.28f), heldEuler: new Vector3(20f, 160f, 0f), heldScale: 0.45f,
            worldEuler: new Vector3(-90f, 0f, 0f)));

        var bandage = Asset<ConsumableDefinition>($"{ItemsDir}/Bandage/Bandage.asset");
        Define(bandage, "Bandage", FlatIcons + "bandage.png",
            maxStack: 5, weight: 0.05f);
        bandage.SetUse(1.5f, uses: 1, moveSpeedScale: 0.6f);
        bandage.SetEffects(heal: 0f, healOverTime: 15f, healSeconds: 10f, buffSeconds: 0f, null);
        SetPrefab(bandage, HeldPrefab($"{ItemsDir}/Bandage/Bandage.prefab", bandage, Medical + "SM_Bandage.prefab",
            heldPosition: new Vector3(0.09f, -0.05f, 0.27f), heldEuler: new Vector3(-60f, 160f, 0f), heldScale: 1f,
            worldEuler: new Vector3(-90f, 0f, 0f), worldScale: 1.4f));

        var adrenaline = Asset<ConsumableDefinition>($"{ItemsDir}/Adrenaline/Adrenaline.asset");
        Define(adrenaline, "Adrenaline", FlatIcons + "syringe1.png", maxStack: 3, weight: 0.2f);
        adrenaline.SetUse(1f, uses: 1, moveSpeedScale: 0.7f);
        adrenaline.SetEffects(0f, 0f, 10f, buffSeconds: 60f, new Dictionary<StatId, float> { { StatId.WalkSpeed, 1.25f }, { StatId.SprintSpeed, 1.25f } });
        SetPrefab(adrenaline, HeldPrefab($"{ItemsDir}/Adrenaline/Adrenaline.prefab", adrenaline, Medical + "SM_Injector_With_Adrenalin.prefab",
            heldPosition: new Vector3(0.1f, -0.06f, 0.26f), heldEuler: new Vector3(60f, 0f, -25f), heldScale: 1f,
            worldEuler: new Vector3(0f, 0f, 90f), worldScale: 1.4f));

        var stamina = Asset<ConsumableDefinition>($"{ItemsDir}/StaminaShot/StaminaShot.asset");
        Define(stamina, "Stamina Shot", FlatIcons + "syringe2.png", maxStack: 3, weight: 0.2f);
        stamina.SetUse(1f, uses: 1, moveSpeedScale: 0.7f);
        stamina.SetEffects(0f, 0f, 10f, buffSeconds: 60f,
            new Dictionary<StatId, float> { { StatId.StaminaRegen, 2f }, { StatId.SprintStaminaCost, 0.5f } });
        SetPrefab(stamina, HeldPrefab($"{ItemsDir}/StaminaShot/StaminaShot.prefab", stamina, Medical + "SM_Injector_With_Stimulant.prefab",
            heldPosition: new Vector3(0.1f, -0.06f, 0.26f), heldEuler: new Vector3(60f, 0f, -25f), heldScale: 1f,
            worldEuler: new Vector3(0f, 0f, 90f), worldScale: 1.4f));

        Save(kit, bandage, adrenaline, stamina);
        return "consumables: First Aid Kit (5 × 20 HP, 3 s), Bandage (15 HP over 10 s), Adrenaline (+25 % speed 60 s), Stamina Shot (regen ×2, sprint cost ½, 60 s)";
    }

    // ---- Values (Tokens per unit) and rarities ----

    public static string Values()
    {
        var values = new Dictionary<string, (int value, ItemRarity rarity)>
        {
            { "Tokens", (1, ItemRarity.Common) },
            { "PistolAmmo", (2, ItemRarity.Common) }, { "RifleAmmo", (3, ItemRarity.Common) }, { "ShotgunShells", (5, ItemRarity.Common) },
            { "MedicBox", (120, ItemRarity.Uncommon) }, { "Bandage", (15, ItemRarity.Common) },
            { "Adrenaline", (80, ItemRarity.Rare) }, { "StaminaShot", (60, ItemRarity.Uncommon) },
            { "Backpack", (150, ItemRarity.Uncommon) },
            { "Pistol", (300, ItemRarity.Uncommon) }, { "DoubleBarrel", (400, ItemRarity.Uncommon) }, { "PumpShotgun", (500, ItemRarity.Uncommon) },
            { "MP5", (600, ItemRarity.Rare) }, { "AK47", (900, ItemRarity.Rare) },
            { "FireAxe", (150, ItemRarity.Common) }, { "Hammer", (80, ItemRarity.Common) }, { "Lantern", (100, ItemRarity.Common) },
        };
        var done = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game/Content" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || !values.TryGetValue(item.name, out var entry))
                continue;
            var so = new SerializedObject(item);
            so.FindProperty("_value").intValue = entry.value;
            so.FindProperty("_rarity").enumValueIndex = (int)entry.rarity;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(item);
            done.Add($"{item.name} {entry.value} {entry.rarity}");
        }
        return "values: " + string.Join(", ", done);
    }

    // ---- Weights of the items that already existed ----

    public static string Weights()
    {
        var weights = new Dictionary<string, float>
        {
            { "AK47", 3.6f }, { "Pistol", 0.9f }, { "MP5", 2.5f }, { "DoubleBarrel", 3.2f }, { "PumpShotgun", 3.4f },
            { "FireAxe", 1.8f }, { "Hammer", 0.6f }, { "Lantern", 0.8f },
        };
        var done = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game/Content" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || !weights.TryGetValue(item.name, out float weight))
                continue;
            var so = new SerializedObject(item);
            so.FindProperty("_weight").floatValue = weight;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(item);
            done.Add($"{item.name} {weight} kg");
        }
        return "weights: " + string.Join(", ", done);
    }

    // ---- Loot: goblin table, lootable goblin bodies, the crate ----

    public static string Loot()
    {
        var tokens = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsDir}/Tokens/Tokens.asset");
        var pistol = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{AmmoDir}/PistolAmmo.asset");
        var rifle = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{AmmoDir}/RifleAmmo.asset");
        var shells = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{AmmoDir}/ShotgunShells.asset");
        var medic = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsDir}/MedicBox/MedicBox.asset");

        var table = Asset<LootTable>($"{LootDir}/GoblinLoot.asset");
        WeightedTable<LootDrop> drops = table.Drops;
        drops.Entries.Clear();
        drops.NothingWeight = 30f;
        drops.Count = new Vector2Int(1, 2);
        Drop(drops, tokens, 5, 20, 40f);
        Drop(drops, pistol, 6, 12, 20f);
        Drop(drops, rifle, 10, 20, 15f);
        Drop(drops, shells, 2, 4, 10f);
        Drop(drops, medic, 1, 1, 5f);
        Save(table);

        var bodies = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/_Game/Content/Enemies" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition == null || !definition.name.StartsWith("Goblin"))
                continue;
            definition.Loot = table;
            Save(definition);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var brain = asset != null ? asset.GetComponent<EnemyBrain>() : null;
            if (brain == null || brain.Definition == null || brain.Definition.Loot == null)
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                LootBody(root, brain.Definition.name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                bodies.Add(asset.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Crate(tokens, pistol, rifle, shells, medic);
        return $"loot: {LootDir}/GoblinLoot.asset; lootable bodies: {string.Join(", ", bodies)}; crate: {CratePath}";
    }

    static void Drop(WeightedTable<LootDrop> drops, ItemDefinition item, int min, int max, float weight) =>
        drops.Entries.Add(new WeightedEntry<LootDrop> { Value = new LootDrop { Item = item, Count = new Vector2Int(min, max) }, Weight = weight });

    // A LootContainer on the enemy root, and a trigger around where the body lies that the look ray finds it by (off
    // while alive: EnemyBody turns it on when the body holds loot).
    static void LootBody(GameObject root, string label)
    {
        Transform old = root.transform.Find("LootSearch");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var search = new GameObject("LootSearch") { layer = 0 };
        search.transform.SetParent(root.transform, false);
        var box = search.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.35f, 0f);
        box.size = new Vector3(1.6f, 0.7f, 1.6f);
        box.enabled = false;

        if (!root.TryGetComponent(out LootContainer loot))
            loot = root.AddComponent<LootContainer>();
        var so = new SerializedObject(loot);
        so.FindProperty("_label").stringValue = label;
        so.FindProperty("_capacity").intValue = 6;
        so.FindProperty("_contents").ClearArray();
        so.FindProperty("_hideWhenEmpty").boolValue = true;
        so.FindProperty("_searchCollider").objectReferenceValue = box;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Crate(ItemDefinition tokens, ItemDefinition pistol, ItemDefinition rifle, ItemDefinition shells, ItemDefinition medic)
    {
        var root = new GameObject("LootCrate");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Animpic + "POLY-SurvivalWorkshop/Prefabs/Interiors/SM_Wooden_Box_A.prefab"));
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = new Vector3(0.9f, 0.7f, 0.7f);
            // Saves find each placed crate by it (its id is generated per scene instance).
            root.AddComponent<MB13.Aipocalypse.Core.SaveSystem.SaveId>();
            var loot = root.AddComponent<LootContainer>();
            var so = new SerializedObject(loot);
            so.FindProperty("_label").stringValue = "Crate";
            so.FindProperty("_capacity").intValue = 8;
            SerializedProperty contents = so.FindProperty("_contents");
            var stacks = new (ItemDefinition item, int count)[] { (tokens, 35), (pistol, 24), (rifle, 30), (shells, 8), (medic, 1) };
            contents.arraySize = stacks.Length;
            for (int i = 0; i < stacks.Length; i++)
            {
                SerializedProperty element = contents.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Item").objectReferenceValue = stacks[i].item;
                element.FindPropertyRelative("Count").intValue = stacks[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CratePath)!);
            PrefabUtility.SaveAsPrefabAsset(root, CratePath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---- EquipmentConfig: 2 guns (1, 2), melee (V), quick slots 3-9; head, body, face, backpack, light ----

    public static string Config()
    {
        var config = AssetDatabase.LoadAssetAtPath<EquipmentConfig>(ConfigPath) ?? throw new Exception("missing " + ConfigPath);
        var so = new SerializedObject(config);
        SerializedProperty slots = so.FindProperty("_slots");
        var hotbar = new List<(string name, ItemCategory accepts, HotbarKey key)>
        {
            ("Firearm", ItemCategory.Gun, HotbarKey.Key1),
            ("Firearm", ItemCategory.Gun, HotbarKey.Key2),
            ("Melee", ItemCategory.Melee, HotbarKey.Melee),
        };
        for (var key = HotbarKey.Key3; key <= HotbarKey.Key9; key++)
            hotbar.Add(($"Quick {(int)key + 1}", ItemCategory.Consumable | ItemCategory.Tool, key));
        slots.arraySize = hotbar.Count;
        for (int i = 0; i < hotbar.Count; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Name").stringValue = hotbar[i].name;
            slot.FindPropertyRelative("Accepts").intValue = (int)hotbar[i].accepts;
            slot.FindPropertyRelative("Key").intValue = (int)hotbar[i].key;
            slot.FindPropertyRelative("StartingItem").objectReferenceValue = null;
        }

        var gearSlots = new (string name, ItemCategory accepts)[]
        {
            ("Head", ItemCategory.Head), ("Body", ItemCategory.Body), ("Face", ItemCategory.Face),
            ("Backpack", ItemCategory.Backpack), ("Light", ItemCategory.Lantern),
        };
        SerializedProperty gear = so.FindProperty("_gear");
        gear.arraySize = gearSlots.Length;
        for (int i = 0; i < gearSlots.Length; i++)
        {
            SerializedProperty slot = gear.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Name").stringValue = gearSlots[i].name;
            slot.FindPropertyRelative("Accepts").intValue = (int)gearSlots[i].accepts;
            slot.FindPropertyRelative("StartingItem").objectReferenceValue = null;
        }
        so.FindProperty("_backpackSlots").intValue = 10;
        so.FindProperty("_maxWeight").floatValue = 25f;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(config);

        var tuning = AssetDatabase.LoadAssetAtPath<PlayerInteractionTuning>(TuningPath) ?? throw new Exception("missing " + TuningPath);
        var tuningSo = new SerializedObject(tuning);
        tuningSo.FindProperty("_pickupMask").intValue = 1 << 0;
        tuningSo.ApplyModifiedPropertiesWithoutUndo();
        Save(tuning);
        return $"config: {hotbar.Count} hotbar slots (1, 2, V, 3-9), {gearSlots.Length} gear slots, 10 backpack slots, 25 kg";
    }

    // ---- Sounds: 2D one-shots in the Player library (the player's own inventory), set on InventoryStyle ----

    public static string Sounds()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(PlayerLib) ?? throw new Exception("missing " + PlayerLib);
        var style = Asset<InventoryStyle>(StylePath);
        style.OpenSound = Sound(library, "Inventory_Open", 0.6f, 0f, SurvivalItems + "Zipper 1-12.wav");
        style.CloseSound = Sound(library, "Inventory_Close", 0.5f, 0.05f, SurvivalItems + "Bag 5-8.wav");
        style.PlaceSound = Sound(library, "Item_Place", 0.45f, 0.08f, InterfaceMisc + "Bag Handle 1-9.wav", InterfaceMisc + "Bag Handle 1-11.wav");
        style.RefusedSound = Sound(library, "Inventory_Refused", 0.5f, 0f, InterfaceUi + "Negative Action 19-2.wav");
        style.RevealSound = Sound(library, "Loot_Reveal", 0.55f, 0.08f, SurvivalMisc + "Cloth Handle 1-3.wav", SurvivalMisc + "Cloth Foley 6.wav",
            SurvivalItems + "Bag 6-2.wav");
        style.PickupSound = Sound(library, "Item_Pickup", 0.7f, 0.08f, SurvivalMisc + "Cloth Grab 1-4.wav", SurvivalMisc + "Objects Small 1-5.wav",
            SurvivalMisc + "Objects Small 1-7.wav");
        style.TokensSound = Sound(library, "Tokens_Pickup", 0.6f, 0.06f, SurvivalItems + "Coins 1-1.wav", SurvivalItems + "Coins 4-01.wav",
            SurvivalItems + "Coins 4-08.wav");
        Save(style);
        return "sounds: Inventory_Open, Inventory_Close, Item_Place, Inventory_Refused, Loot_Reveal, Item_Pickup, Tokens_Pickup (Player library)";
    }

    // A 2D one-shot SFX entity with shuffled clips (no immediate repeat) and a random pitch range.
    static SoundID Sound(AudioAsset library, string name, float volume, float pitchRandom, params string[] clips)
    {
        string path = $"{PlayerAudioDir}/{name}.asset";
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
        so.FindProperty("<Pitch>k__BackingField").floatValue = 1f;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)(clips.Length > 1 ? MulticlipsPlayMode.Shuffle : MulticlipsPlayMode.Single);
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = null;
        var list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]) ?? throw new Exception("missing clip " + clips[i]);
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        return new SoundID(entity);
    }

    // ---- UI ----

    public static string Interface()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing font " + FontPath);
        var hotbarStyle = AssetDatabase.LoadAssetAtPath<HotbarSlotStyle>(HotbarStylePath);
        var style = Asset<InventoryStyle>(StylePath);
        style.Font = font;
        style.SlotSprite = hotbarStyle != null ? hotbarStyle.BackgroundSprite : null;
        style.HighlightSprite = hotbarStyle != null ? hotbarStyle.FrameSprite : null;
        style.PanelSprite = hotbarStyle != null ? hotbarStyle.BackgroundSprite : null;
        style.SetEmptyIcon(ItemCategory.Gun, Sprite(Icons + "Icons_Weapons/ICON_SM_Wep_AssaultRifle_01_Underlay.png"));
        style.SetEmptyIcon(ItemCategory.Melee, Sprite(Icons + "Icons_Weapons/ICON_SM_Wep_FireAxe_01_Underlay.png"));
        style.SetEmptyIcon(ItemCategory.Head, Sprite(Icons + "Icons_Inventory/ICON_Apocalpyse_Inventory_Helmets_01.png"));
        style.SetEmptyIcon(ItemCategory.Body, Sprite(Icons + "Icons_Inventory/ICON_Apocalpyse_Inventory_Armor_01.png"));
        style.SetEmptyIcon(ItemCategory.Face, Sprite(Icons + "Icons_Armor/ICON_SM_Chr_Attach_GasMask_01.png"));
        style.SetEmptyIcon(ItemCategory.Backpack, Sprite(Icons + "Icons_Inventory/ICON_Apocalpyse_Inventory_Backpack_01.png"));
        // Common items get no border: only what's worth noticing stands out.
        style.SetRarityColor(ItemRarity.Common, new Color(0.62f, 0.62f, 0.62f, 0f));
        style.RarityBorderAlpha = 0.7f;
        style.SetRarityColor(ItemRarity.Uncommon, new Color(0.42f, 0.82f, 0.38f, 1f));
        style.SetRarityColor(ItemRarity.Rare, new Color(0.32f, 0.62f, 1f, 1f));
        style.SetRarityColor(ItemRarity.Epic, new Color(0.72f, 0.42f, 1f, 1f));
        style.SetRarityColor(ItemRarity.Legendary, new Color(1f, 0.76f, 0.26f, 1f));
        Save(style);

        ItemSlotView slot = SlotPrefab(style, font);

        GameObject hud = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Screen(hud.transform, style, font, slot);
            Transform safe = hud.transform.Find("SafeFrame") ?? throw new Exception("HUD has no SafeFrame");
            // The search circle around the crosshair is gone: searching now reveals items inside the loot panel.
            Transform search = safe.Find("Center/LootSearch");
            if (search != null)
                Object.DestroyImmediate(search.gameObject);
            Feed(safe.Find("BottomRight") ?? throw new Exception("no SafeFrame/BottomRight"), font, style);
            Markers(hud.transform);
            ItemUse(safe.Find("Center") ?? throw new Exception("no SafeFrame/Center"), font);
            Effects(safe.Find("BottomLeft") ?? throw new Exception("no SafeFrame/BottomLeft"), font);
            PrefabUtility.SaveAsPrefabAsset(hud, HudPath, out bool saved);
            return saved ? $"ui: {StylePath}, {SlotPrefabPath}, HUD.prefab Inventory / LootMarkers / PickupFeed" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hud);
        }
    }

    // Layout groups and TMP compute some values only when the HUD wakes in a scene (driven rects, the 32-bit font colour).
    // Opens WorldMap clean, lets them compute, and applies just those values on the inventory nodes back to HUD.prefab, so
    // the scene's HUD instance shows no overrides and the scene isn't dirtied on open or after Play.
    public static string Bake() => BakeOpenScene(EditorSceneManager.OpenScene(WorldMapPath, OpenSceneMode.Single));

    // Bake on the scene as it is now (right after leaving Play, when the values above have been recomputed).
    public static string BakeCurrent() => BakeOpenScene(EditorSceneManager.GetActiveScene());

    static string BakeOpenScene(Scene scene)
    {
        var screen = Object.FindAnyObjectByType<InventoryScreen>(FindObjectsInactive.Include);
        if (screen == null)
            return "bake: no InventoryScreen in WorldMap";
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(screen.gameObject);
        var nodes = new List<Transform> { screen.transform };
        var markers = Object.FindAnyObjectByType<LootMarkerHud>(FindObjectsInactive.Include);
        var use = Object.FindAnyObjectByType<ItemUseHud>(FindObjectsInactive.Include);
        var effects = Object.FindAnyObjectByType<EffectsHud>(FindObjectsInactive.Include);
        if (use != null)
            nodes.Add(use.transform);
        if (effects != null)
            nodes.Add(effects.transform);
        var feed = Object.FindAnyObjectByType<PickupFeedHud>(FindObjectsInactive.Include);
        if (markers != null)
            nodes.Add(markers.transform);
        if (feed != null)
            nodes.Add(feed.transform);

        Canvas.ForceUpdateCanvases();
        foreach (Transform node in nodes)
        {
            foreach (LayoutGroup group in node.GetComponentsInChildren<LayoutGroup>(true))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            foreach (TMP_Text text in node.GetComponentsInChildren<TMP_Text>(true))
                text.ForceMeshUpdate(true, true);
        }

        int applied = 0;
        foreach (UnityEditor.SceneManagement.ObjectOverride change in PrefabUtility.GetObjectOverrides(root))
        {
            Object target = change.instanceObject;
            Transform owner = target is Component component ? component.transform : target is GameObject go ? go.transform : null;
            if (owner == null || !nodes.Exists(owner.IsChildOf))
                continue;
            change.Apply(HudPath, InteractionMode.AutomatedAction);
            applied++;
        }
        // Reopen from disk: drops the scene's in-memory noise instead of saving it.
        Scene reopened = EditorSceneManager.OpenScene(WorldMapPath, OpenSceneMode.Single);
        return $"bake: applied {applied} inventory overrides to HUD.prefab; WorldMap dirty after reopening = {reopened.isDirty}";
    }

    // 88x88 tile: background, silhouette, icon, name (bottom), count (top-right), key (top-left), highlight frame, caption
    // under the tile.
    static ItemSlotView SlotPrefab(InventoryStyle style, TMP_FontAsset font)
    {
        var root = new GameObject("ItemSlot", typeof(RectTransform));
        try
        {
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(88f, 88f);
            Image background = Img("Background", rect, Stretch(0f), raycast: true);
            Image empty = Img("EmptyIcon", rect, Stretch(16f));
            Image icon = Img("Icon", rect, Stretch(10f));
            icon.rectTransform.offsetMin = new Vector2(10f, 20f);
            TMP_Text itemName = Label("Name", rect, font, 13f, TextAlignmentOptions.Bottom);
            // Taller than the 13 pt font's ~20.5 px line: a shorter box truncates the whole name away.
            Place(itemName.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-6f, 22f));
            itemName.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text count = Label("Count", rect, font, 16f, TextAlignmentOptions.TopRight);
            Place(count.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -2f), new Vector2(-10f, 22f));
            TMP_Text key = Label("Key", rect, font, 15f, TextAlignmentOptions.TopLeft);
            Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -2f), new Vector2(30f, 20f));
            // Not yet revealed by a search: an "unknown" mark (an icon: the font has no '?') and the ring that fills while
            // it's being revealed.
            RectTransform hidden = Ui("Hidden", rect);
            Place(hidden, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image mark = Img("Mark", hidden, Stretch(0f));
            Place(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            mark.sprite = Sprite(Icons + "Icons_Map/ICON_Apocalpyse_Map_Unknown_01.png");
            mark.preserveAspect = true;
            mark.color = style.DimTextColor;
            Image ring = Img("Ring", hidden, Stretch(0f));
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            ring.sprite = Sprite(Icons + "HUD/SPR_HUD_Apocalypse_Ring_Medium_01_Clean.png");
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.enabled = false;
            hidden.gameObject.SetActive(false);
            // Under the hover highlight: the item's rarity colour (hidden when empty).
            Image rarity = Img("RarityBorder", rect, Stretch(-1f));
            Image highlight = Img("Highlight", rect, Stretch(-2f));
            TMP_Text caption = Label("Caption", rect, font, 15f, TextAlignmentOptions.Top);
            Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(20f, 22f));

            var view = root.AddComponent<ItemSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_emptyIcon").objectReferenceValue = empty;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_highlight").objectReferenceValue = highlight;
            so.FindProperty("_rarityBorder").objectReferenceValue = rarity;
            so.FindProperty("_name").objectReferenceValue = itemName;
            so.FindProperty("_count").objectReferenceValue = count;
            so.FindProperty("_key").objectReferenceValue = key;
            so.FindProperty("_caption").objectReferenceValue = caption;
            so.FindProperty("_hidden").objectReferenceValue = hidden.gameObject;
            so.FindProperty("_searchFill").objectReferenceValue = ring;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyStyle();
            return PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath).GetComponent<ItemSlotView>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void Screen(Transform hud, InventoryStyle style, TMP_FontAsset font, ItemSlotView slot)
    {
        Transform old = hud.Find("Inventory");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        RectTransform node = Ui("Inventory", hud);
        Place(node, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        node.SetAsLastSibling();
        var canvas = node.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        node.gameObject.AddComponent<GraphicRaycaster>();
        var group = node.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image backdrop = Img("Backdrop", node, Stretch(0f), raycast: true);
        backdrop.color = style.BackdropColor;

        // Left: header, equipment, backpack.
        RectTransform left = Panel("Left", node, style);
        // Fixed size, not stretched with the screen: a layout group under a screen-sized rect drives values that change with
        // the window, and the scene's HUD instance records them as overrides.
        Place(left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(540f, 800f));
        Column(left, 16, 10);
        RectTransform header = Ui("Header", left);
        Fixed(header, 58f);
        TMP_Text tokens = Label("Tokens", header, font, 26f, TextAlignmentOptions.TopLeft);
        Place(tokens.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f));
        TMP_Text tokensLabel = Label("TokensLabel", header, font, 14f, TextAlignmentOptions.BottomLeft);
        Place(tokensLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 20f));
        tokensLabel.text = "TOKENS";
        tokensLabel.color = style.DimTextColor;
        TMP_Text weight = Label("Weight", header, font, 16f, TextAlignmentOptions.TopRight);
        Place(weight.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 24f));
        Image weightBack = Img("WeightBar", header, Stretch(0f));
        Place(weightBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(0f, 10f));
        weightBack.color = new Color(1f, 1f, 1f, 0.15f);
        Image weightFill = Img("Fill", weightBack.rectTransform, Stretch(0f));
        weightFill.type = Image.Type.Filled;
        weightFill.fillMethod = Image.FillMethod.Horizontal;
        weightFill.sprite = WhiteSprite();
        weightFill.color = style.WeightBarColor;

        TMP_Text equipmentTitle = Label("EquipmentTitle", left, font, 20f, TextAlignmentOptions.Left);
        equipmentTitle.text = "EQUIPMENT";
        equipmentTitle.fontStyle = FontStyles.UpperCase;
        Fixed(equipmentTitle.rectTransform, 28f);
        RectTransform equipment = Grid("EquipmentGrid", left, new Vector2(10f, 30f));
        // Two rows of tiles plus the captions under the second row, with room before the backpack title.
        var equipmentHeight = equipment.gameObject.AddComponent<LayoutElement>();
        equipmentHeight.preferredHeight = equipmentHeight.minHeight = 88f * 2f + 30f + 40f;

        RectTransform backpackRow = Ui("BackpackRow", left);
        Fixed(backpackRow, 34f);
        TMP_Text backpackTitle = Label("BackpackTitle", backpackRow, font, 20f, TextAlignmentOptions.Left);
        backpackTitle.fontStyle = FontStyles.UpperCase;
        Place(backpackTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-110f, 0f));
        Button sort = Btn("Sort", backpackRow, font, style);
        Place((RectTransform)sort.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 32f));

        RectTransform scroll = Ui("BackpackScroll", left);
        scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = Ui("Viewport", scroll);
        Place(viewport, Vector2.zero, Vector2.one, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = new Color(0f, 0f, 0f, 0f);
        RectTransform backpack = Grid("Content", viewport, new Vector2(10f, 10f));
        Place(backpack, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        backpack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = backpack;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Right: loot.
        RectTransform loot = Panel("Loot", node, style);
        // Below the HUD clock (top-right).
        Place(loot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -180f), new Vector2(540f, 330f));
        Column(loot, 16, 10);
        RectTransform lootRow = Ui("LootRow", loot);
        Fixed(lootRow, 34f);
        TMP_Text lootTitle = Label("LootTitle", lootRow, font, 22f, TextAlignmentOptions.Left);
        lootTitle.fontStyle = FontStyles.UpperCase;
        Place(lootTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-130f, 0f));
        Button takeAll = Btn("Take all", lootRow, font, style);
        Place((RectTransform)takeAll.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 32f));
        RectTransform lootGrid = Grid("LootGrid", loot, new Vector2(10f, 10f));
        lootGrid.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        // Bottom: the quick slots (keys 3-9).
        RectTransform quick = Panel("Quick", node, style);
        Place(quick, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(760f, 140f));
        Column(quick, 12, 8);
        TMP_Text hint = Label("Hint", quick, font, 16f, TextAlignmentOptions.Center);
        hint.text = "Drag items into slots 3-9 to set up your hotbar";
        hint.color = style.DimTextColor;
        Fixed(hint.rectTransform, 22f);
        RectTransform quickRow = Ui("QuickRow", quick);
        Fixed(quickRow, 88f);
        var row = quickRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        TMP_Text message = Label("Message", node, font, 24f, TextAlignmentOptions.Center);
        Place(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 40f));
        message.enabled = false;

        ItemTooltipView tooltip = Tooltip(node, style, font);

        Image drag = Img("DragIcon", node, Stretch(0f));
        Place(drag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
        drag.preserveAspect = true;
        drag.enabled = false;

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.transform.SetParent(node, false);
        events.SetActive(false);

        var screen = node.gameObject.AddComponent<InventoryScreen>();
        var so = new SerializedObject(screen);
        Set(so, "_style", style);
        Set(so, "_group", group);
        Set(so, "_eventSystem", events);
        Set(so, "_slotPrefab", slot);
        Set(so, "_equipmentGrid", equipment);
        Set(so, "_backpackGrid", backpack);
        Set(so, "_lootGrid", lootGrid);
        Set(so, "_quickRow", quickRow);
        Set(so, "_lootPanel", loot.gameObject);
        Set(so, "_backpackTitle", backpackTitle);
        Set(so, "_lootTitle", lootTitle);
        Set(so, "_tokens", tokens);
        Set(so, "_weight", weight);
        Set(so, "_weightFill", weightFill);
        Set(so, "_message", message);
        Set(so, "_sortButton", sort);
        Set(so, "_takeAllButton", takeAll);
        Set(so, "_dragIcon", drag);
        Set(so, "_tooltip", tooltip);
        Set(so, "_backdrop", backdrop.gameObject);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static ItemTooltipView Tooltip(RectTransform parent, InventoryStyle style, TMP_FontAsset font)
    {
        RectTransform panel = Panel("Tooltip", parent, style);
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 70f));
        panel.GetComponent<Image>().raycastTarget = false;
        Column(panel, 10, 4);
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = Label("Title", panel, font, 20f, TextAlignmentOptions.Left);
        TMP_Text details = Label("Details", panel, font, 15f, TextAlignmentOptions.Left);
        details.color = style.DimTextColor;
        var view = panel.gameObject.AddComponent<ItemTooltipView>();
        var so = new SerializedObject(view);
        Set(so, "_panel", panel);
        Set(so, "_title", title);
        Set(so, "_details", details);
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
        return view;
    }

    // Loot markers: a full-screen node with its own canvas (markers move every frame without rebuilding the HUD's), under the
    // inventory screen; one inactive bag-icon template the view clones.
    static void Markers(Transform hud)
    {
        Transform old = hud.Find("LootMarkers");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        RectTransform node = Ui("LootMarkers", hud);
        Place(node, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Transform inventory = hud.Find("Inventory");
        node.SetSiblingIndex(inventory != null ? inventory.GetSiblingIndex() : hud.childCount - 1);
        node.gameObject.AddComponent<Canvas>();
        Image template = Img("Marker", node, Stretch(0f));
        Place(template.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(56f, 56f));
        template.sprite = Sprite(Icons + "Icons_Inventory/ICON_Apocalpyse_Inventory_Backpack_01.png");
        template.preserveAspect = true;
        // Reads against bright grass and sky.
        var shadow = template.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);
        template.gameObject.SetActive(false);
        var hud2 = node.gameObject.AddComponent<LootMarkerHud>();
        var so = new SerializedObject(hud2);
        Set(so, "_template", template);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Pickup lines bottom-right, newest at the bottom, the "Backpack full" warning above them.
    // The ring around the crosshair while a consumable is being used, its name (or "Health full") above it.
    static void ItemUse(Transform center, TMP_FontAsset font)
    {
        Transform old = center.Find("ItemUse");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        RectTransform node = Ui("ItemUse", center);
        Place(node, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        node.gameObject.AddComponent<Canvas>();
        RectTransform root = Ui("Root", node);
        Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
        Image ring = Img("Ring", root, Stretch(0f));
        ring.sprite = Sprite(Icons + "HUD/SPR_HUD_Apocalypse_Ring_Medium_01_Underlay.png");
        ring.color = new Color(0f, 0f, 0f, 0.45f);
        Image fill = Img("Fill", root, Stretch(0f));
        fill.sprite = Sprite(Icons + "HUD/SPR_HUD_Apocalypse_Ring_Medium_01_Clean.png");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.color = new Color(0.55f, 0.95f, 0.6f, 1f);
        TMP_Text label = Label("Label", root, font, 22f, TextAlignmentOptions.Center);
        Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(500f, 30f));
        root.gameObject.SetActive(false);
        var hud = node.gameObject.AddComponent<ItemUseHud>();
        var so = new SerializedObject(hud);
        Set(so, "_root", root.gameObject);
        Set(so, "_fill", fill);
        Set(so, "_label", label);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // A row of running effects (icon + seconds left) above the health and stamina bars.
    static void Effects(Transform corner, TMP_FontAsset font)
    {
        Transform old = corner.Find("Effects");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        RectTransform node = Ui("Effects", corner);
        Place(node, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 130f), new Vector2(420f, 64f));
        node.gameObject.AddComponent<Canvas>();
        var row = node.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childAlignment = TextAnchor.LowerLeft;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        Image template = Img("Effect", node, Stretch(0f));
        Place(template.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        template.preserveAspect = true;
        TMP_Text seconds = Label("Seconds", template.rectTransform, font, 16f, TextAlignmentOptions.Bottom);
        Place(seconds.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(20f, 20f));
        template.gameObject.SetActive(false);
        var hud = node.gameObject.AddComponent<EffectsHud>();
        var so = new SerializedObject(hud);
        Set(so, "_template", template);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Feed(Transform corner, TMP_FontAsset font, InventoryStyle style)
    {
        Transform old = corner.Find("PickupFeed");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        RectTransform node = Ui("PickupFeed", corner);
        Place(node, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 150f), new Vector2(460f, 220f));
        node.gameObject.AddComponent<Canvas>();
        const int Lines = 5;
        var lines = new TMP_Text[Lines];
        for (int i = 0; i < Lines; i++)
        {
            lines[i] = Label($"Line{i}", node, font, 22f, TextAlignmentOptions.Right);
            Place(lines[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, i * 32f), new Vector2(0f, 30f));
        }
        TMP_Text warning = Label("Warning", node, font, 24f, TextAlignmentOptions.Right);
        Place(warning.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, Lines * 32f + 8f), new Vector2(0f, 32f));
        var hud = node.gameObject.AddComponent<PickupFeedHud>();
        var so = new SerializedObject(hud);
        SerializedProperty array = so.FindProperty("_lines");
        array.arraySize = Lines;
        for (int i = 0; i < Lines; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
        Set(so, "_warning", warning);
        Set(so, "_style", style);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform Panel(string name, Transform parent, InventoryStyle style)
    {
        RectTransform rect = Ui(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.PanelSprite;
        image.type = StyleUtils.ImageTypeFor(style.PanelSprite);
        image.color = style.PanelColor;
        image.raycastTarget = true;
        return rect;
    }

    static void Column(RectTransform rect, int padding, float spacing)
    {
        var column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(padding, padding, padding, padding);
        column.spacing = spacing;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
    }

    static RectTransform Grid(string name, Transform parent, Vector2 spacing)
    {
        RectTransform rect = Ui(name, parent);
        var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(88f, 88f);
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperLeft;
        return rect;
    }

    static void Fixed(RectTransform rect, float height)
    {
        if (!rect.TryGetComponent(out LayoutElement element))
            element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }

    static Button Btn(string text, Transform parent, TMP_FontAsset font, InventoryStyle style)
    {
        RectTransform rect = Ui(text.Replace(" ", ""), parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.SlotSprite;
        image.type = StyleUtils.ImageTypeFor(style.SlotSprite);
        image.color = new Color(0.85f, 0.88f, 0.92f, 0.95f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = Label("Label", rect, font, 18f, TextAlignmentOptions.Center);
        label.text = text;
        label.color = new Color(0.1f, 0.12f, 0.15f, 1f);
        Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    static (Vector2 min, Vector2 max) Stretch(float inset) => (new Vector2(inset, inset), new Vector2(-inset, -inset));

    static Image Img(string name, Transform parent, (Vector2 min, Vector2 max) offsets, bool raycast = false)
    {
        RectTransform rect = Ui(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsets.min;
        rect.offsetMax = offsets.max;
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = raycast;
        return image;
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = Ui(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = string.Empty;
        // What TMP sets on its own when an instance wakes: set here, the scene's HUD instance shows no override for it.
        text.textStyle = TMP_Style.NormalStyle;
        return text;
    }

    static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Sprite _white;

    // A filled Image needs a sprite; Unity's built-in UISprite works.
    static Sprite WhiteSprite() => _white ??= AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    static void Set(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;

    // ---- Test setup in the WorldMap yard, beside the ammo crate ----

    public static string Yard()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != WorldMapPath)
            return "yard: skipped, WorldMap isn't the open scene";
        var crate = Object.FindAnyObjectByType<AmmoCrate>();
        if (crate == null)
            return "yard: skipped, no ammo crate to place beside";

        GameObject old = GameObject.Find(YardNode);
        if (old != null)
            Object.DestroyImmediate(old);
        var node = new GameObject(YardNode);
        ParentBeside(node, crate.gameObject);
        Transform anchor = crate.transform;
        Vector3 right = Vector3.ProjectOnPlane(anchor.right, Vector3.up).normalized;
        Vector3 forward = Vector3.Cross(right, Vector3.up);
        Vector3 start = anchor.position + right * 2f;

        var placements = new (string path, int count, float along, float across)[]
        {
            ($"{ItemsDir}/Backpack/Backpack.prefab", 1, 0f, 0f),
            ($"{ItemsDir}/Tokens/Tokens.prefab", 15, 1f, 0f),
            ($"{ItemsDir}/Tokens/Tokens.prefab", 40, 1.6f, 0.6f),
            ($"{ItemsDir}/Tokens/Tokens.prefab", 120, 2.2f, -0.4f),
            ($"{ItemsDir}/MedicBox/MedicBox.prefab", 1, 3f, 0f),
            ($"{ItemsDir}/MedicBox/MedicBox.prefab", 1, 3.6f, 0f),
            ($"{ItemsDir}/Bandage/Bandage.prefab", 3, 3f, 0.8f),
            ($"{ItemsDir}/Adrenaline/Adrenaline.prefab", 1, 3.6f, 0.8f),
            ($"{ItemsDir}/StaminaShot/StaminaShot.prefab", 1, 4.2f, 0.8f),
            ($"{AmmoDir}/PistolAmmoBox.prefab", 24, 4.4f, 0f),
            ($"{AmmoDir}/RifleAmmoBox.prefab", 30, 5.2f, 0f),
            ($"{AmmoDir}/ShotgunShellsBox.prefab", 8, 6f, 0f),
            (CratePath, 0, 7.4f, 0f),
        };
        var placed = new List<string>();
        foreach ((string path, int count, float along, float across) in placements)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception("missing " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
            Vector3 position = start + right * along + forward * across;
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;
            instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            if (count > 0 && instance.TryGetComponent(out WorldItem item))
            {
                var so = new SerializedObject(item);
                so.FindProperty("_count").intValue = count;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            placed.Add($"{instance.name}{(count > 1 ? " x" + count : "")}");
        }
        string injection = InjectCrates(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"yard: {YardNode} beside {crate.name}: {string.Join(", ", placed)}; {injection} (scene saved)";
    }

    // Lists the yard's loot crates in the scene scope's Auto Inject Game Objects, so they register for the HUD's loot markers.
    // Entries left missing by an earlier yard rebuild are dropped. Doesn't save.
    public static string InjectCrates(Scene scene)
    {
        GameLifetimeScope scope = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            scope = root.GetComponentInChildren<GameLifetimeScope>(true);
            if (scope != null)
                break;
        }
        GameObject yard = GameObject.Find(YardNode);
        if (scope == null || yard == null)
            return "no scene scope or yard node: crates not listed for injection";
        var fields = new SerializedObject(scope);
        SerializedProperty list = fields.FindProperty("autoInjectGameObjects");
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                list.DeleteArrayElementAtIndex(i);
        int added = 0;
        foreach (LootContainer container in yard.GetComponentsInChildren<LootContainer>(true))
        {
            bool listed = false;
            for (int i = 0; i < list.arraySize && !listed; i++)
                listed = list.GetArrayElementAtIndex(i).objectReferenceValue == container.gameObject;
            if (listed)
                continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = container.gameObject;
            added++;
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
        return $"{added} loot crate(s) added to {scope.name} Auto Inject Game Objects";
    }

    // InjectCrates on the open WorldMap, saved.
    public static string InjectYardCrates()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != WorldMapPath)
            return "skipped: WorldMap isn't the open scene";
        string result = InjectCrates(scene);
        EditorSceneManager.SaveScene(scene);
        return result + " (scene saved)";
    }

    // Same parent as the ammo crate, so the test items sit with the other yard props.
    static void ParentBeside(GameObject node, GameObject besides)
    {
        if (besides.transform.parent != null)
            node.transform.SetParent(besides.transform.parent, false);
    }

    static void Save(params Object[] assets)
    {
        foreach (Object asset in assets)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
