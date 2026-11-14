using System;
using System.Linq;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEngine;

// Where gun parts come from in play (user, 2026-11-14: goblins and crates): every part joins the goblins' loot table at a
// small weight by rarity, with the two new ammo types; a crate table (Data/Loot/CrateLoot.asset) rolls one part (or
// nothing) on top of a crate's fixed contents, and the loot crate prefab uses it. Weights are first guesses.
// InventorySetup.Loot rebuilds GoblinLoot from scratch: re-run this after it.
// Run: unity command script run --file AgentScripts/Weapons/WeaponLootSetup.cs --entry WeaponLootSetup.Apply
public static class WeaponLootSetup
{
    const string GoblinLoot = "Assets/_Game/Data/Loot/GoblinLoot.asset";
    const string CrateLoot = "Assets/_Game/Data/Loot/CrateLoot.asset";
    const string CratePrefab = "Assets/_Game/Content/Props/LootCrate/LootCrate.prefab";
    const string Parts = "Assets/_Game/Content/Weapons/_Shared/Attachments";
    const string AmmoDir = "Assets/_Game/Content/Weapons/_Shared/Ammo/";

    // A pick's weight per part rarity; goblin loot's other entries add up to 120 (30 of it nothing), so all parts
    // together are about 8 % of a pick.
    static float Weight(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1f,
        ItemRarity.Uncommon => 0.7f,
        ItemRarity.Rare => 0.4f,
        _ => 0.15f,
    };

    public static string Apply()
    {
        AttachmentDefinition[] parts = AssetDatabase.FindAssets("t:AttachmentDefinition", new[] { Parts })
            .Select(guid => AssetDatabase.LoadAssetAtPath<AttachmentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(part => part != null).OrderBy(part => part.name).ToArray();
        var heavy = AssetDatabase.LoadAssetAtPath<AmmoType>(AmmoDir + "HeavyAmmo.asset") ?? throw new Exception("run GunBlockoutSetup.Ammo first");
        var magnum = AssetDatabase.LoadAssetAtPath<AmmoType>(AmmoDir + "MagnumAmmo.asset") ?? throw new Exception("run GunBlockoutSetup.Ammo first");

        var goblin = AssetDatabase.LoadAssetAtPath<LootTable>(GoblinLoot) ?? throw new Exception("missing " + GoblinLoot);
        WeightedTable<LootDrop> drops = goblin.Drops;
        drops.Entries.RemoveAll(e => e.Value?.Item is AttachmentDefinition || e.Value?.Item == heavy || e.Value?.Item == magnum);
        Add(drops, heavy, 6, 12, 8f);
        Add(drops, magnum, 4, 8, 4f);
        foreach (AttachmentDefinition part in parts)
            Add(drops, part, 1, 1, Weight(part.Rarity));
        Save(goblin);

        var crate = AssetDatabase.LoadAssetAtPath<LootTable>(CrateLoot);
        if (crate == null)
        {
            crate = ScriptableObject.CreateInstance<LootTable>();
            AssetDatabase.CreateAsset(crate, CrateLoot);
        }
        WeightedTable<LootDrop> crateDrops = crate.Drops;
        crateDrops.Entries.Clear();
        crateDrops.Count = Vector2Int.one;
        crateDrops.NothingWeight = 2f;
        foreach (AttachmentDefinition part in parts)
            Add(crateDrops, part, 1, 1, Weight(part.Rarity));
        Save(crate);

        GameObject root = PrefabUtility.LoadPrefabContents(CratePrefab);
        try
        {
            var container = new SerializedObject(root.GetComponent<LootContainer>());
            container.FindProperty("_table").objectReferenceValue = crate;
            container.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CratePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        float partsWeight = parts.Sum(p => Weight(p.Rarity));
        float total = drops.NothingWeight + drops.Entries.Sum(e => e.Weight);
        return $"GoblinLoot: {parts.Length} parts ({partsWeight / total:P0} of a pick), heavy and magnum rounds; CrateLoot: one part " +
               $"({partsWeight / (partsWeight + crateDrops.NothingWeight):P0}) on LootCrate.prefab";
    }

    static void Add(WeightedTable<LootDrop> drops, ItemDefinition item, int min, int max, float weight) =>
        drops.Entries.Add(new WeightedEntry<LootDrop> { Value = new LootDrop { Item = item, Count = new Vector2Int(min, max) }, Weight = weight });

    static void Save(ScriptableObject asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
