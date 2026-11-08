// Sets the SpawnSystem up in the project (idempotent):
//   unity command script run --file AgentScripts/Spawning/SpawnSystemSetup.cs --entry SpawnSystemSetup.All
// Spawnable on every enemy prefab root (base prefabs first, variants inherit), the goblin card's variants as Spawnables
// (the field was retyped from EnemyBrain), Data/Spawning/SpawnSystemSettings.asset, and a SpawnInstaller on WorldMap's
// GameLifetimeScope. Saves only what it changed.
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.RaidSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpawnSystemSetup
{
    const string Enemies = "Assets/_Game/Content/Enemies";
    const string GoblinCard = Enemies + "/Goblin/Goblin_Card.asset";
    const string SettingsPath = "Assets/_Game/Data/Spawning/SpawnSystemSettings.asset";
    const string RaidSettingsPath = "Assets/_Game/Data/Raids/RaidSettings.asset";
    const string SpawnableGuid = "b7cb4f394e93ca9468af7ef50526fa39";
    const string WorldMap = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";

    public static string All()
    {
        var log = new List<string>();
        AddSpawnables(log);
        MigrateCards(log, false);
        var settings = CreateSettings(log);
        WireScene(settings, log);
        return string.Join("\n", log);
    }

    // Spawnable on each prefab whose root has an EnemyBrain, written to disk: base prefabs first, then the variants are
    // reloaded so they inherit it. (EnemyBrain's RequireComponent adds a missing one in memory only, when an old prefab loads.)
    public static void AddSpawnables(List<string> log)
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Enemies }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).TryGetComponent(out EnemyBrain _))
            .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant ? 1 : 0)
            .ToList();
        foreach (string path in paths)
        {
            if (System.IO.File.ReadAllText(path).Contains(SpawnableGuid))
                continue;
            if (PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Variant)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                int count = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponents<Spawnable>().Length;
                log.Add($"variant reloaded: {path} ({count} Spawnable)");
                continue;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!root.TryGetComponent(out Spawnable _))
                    root.AddComponent<Spawnable>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add("Spawnable saved: " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    // The goblin looks, weight 1 each (as before the retype), when the card lost them.
    public static string FixCards()
    {
        var log = new List<string>();
        MigrateCards(log, true);
        return string.Join("\n", log);
    }

    public static void MigrateCards(List<string> log, bool force)
    {
        var card = AssetDatabase.LoadAssetAtPath<EnemySpawnCard>(GoblinCard);
        if (!force && card.Variants.Count > 0 && card.Variants.Keys.All(k => k != null))
        {
            log.Add("card already migrated: " + card.Variants.Count);
            return;
        }
        card.ClearVariants();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Enemies + "/Goblin" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab.TryGetComponent(out EnemyBrain _) && prefab.TryGetComponent(out Spawnable spawnable))
                card.SetVariant(spawnable, 1f);
        }
        card.Category = SpawnCategory.Enemy;
        EditorUtility.SetDirty(card);
        AssetDatabase.SaveAssetIfDirty(card);
        log.Add($"card variants: {card.Variants.Count}");
    }

    public static SpawnSystemSettings CreateSettings(List<string> log)
    {
        var settings = AssetDatabase.LoadAssetAtPath<SpawnSystemSettings>(SettingsPath);
        if (settings != null)
            return settings;
        System.IO.Directory.CreateDirectory("Assets/_Game/Data/Spawning");
        settings = ScriptableObject.CreateInstance<SpawnSystemSettings>();
        var raid = AssetDatabase.LoadAssetAtPath<RaidSettings>(RaidSettingsPath);
        if (raid != null)
        {
            settings.DefaultRules.GroundMask = raid.Spawning.GroundMask;
            settings.DefaultRules.SightBlockers = raid.Spawning.SightBlockers;
            settings.DefaultRules.ObstacleMask = raid.Spawning.GroundMask;
            settings.DefaultRules.MinPlayerDistance = 10f;
        }
        settings.Cards.Add(AssetDatabase.LoadAssetAtPath<EnemySpawnCard>(GoblinCard));
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssetIfDirty(settings);
        log.Add("created " + SettingsPath);
        return settings;
    }

    public static void WireScene(SpawnSystemSettings settings, List<string> log)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WorldMap)
        {
            log.Add("WorldMap isn't the active scene; not wired");
            return;
        }
        var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        if (!scope.TryGetComponent(out SpawnInstaller installer))
        {
            installer = scope.gameObject.AddComponent<SpawnInstaller>();
            log.Add("SpawnInstaller added to " + scope.name);
        }
        var so = new SerializedObject(installer);
        so.FindProperty("_settings").objectReferenceValue = settings;
        if (so.ApplyModifiedProperties())
            log.Add("settings assigned");
        if (scene.isDirty)
        {
            EditorSceneManager.SaveScene(scene);
            log.Add("saved " + scene.path);
        }
    }
}
