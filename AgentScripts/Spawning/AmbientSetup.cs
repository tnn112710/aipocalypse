// Ambient flock content and WorldMap wiring (idempotent; rebuilds the content assets in place, keeping their GUIDs):
//   unity command script run --file AgentScripts/Spawning/AmbientSetup.cs --entry AmbientSetup.All
// Content/Ambient/FishSchool: members use the polyperfect chub's mesh as a static mesh (no Animator per fish).
// Content/Ambient/BirdFlock: blockout members (no bird pack is declared in ASSETS.md yet); swap Member for a real model.
// Each kind: member prefab, FlockDefinition, flock prefab (Spawnable + Flock) and SpawnCard, listed in SpawnSystemSettings.
// WorldMap: an AmbientInstaller and the flat water bodies on its SpawnInstaller. Saves only what it changed.
using System.Collections.Generic;
using Aipocalypse.Core.AmbientSystem;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.SpawnSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class AmbientSetup
{
    const string Root = "Assets/_Game/Content/Ambient";
    const string Fish = "Assets/_StoreAssets/Characters/polyperfect/Low Poly Animated Freshwater Fish/_Prefabs/Fish_Chub_Common.prefab";
    const string SettingsPath = "Assets/_Game/Data/Spawning/SpawnSystemSettings.asset";
    const string WorldMap = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";

    static readonly string[] WaterBodies =
    {
        "Environment/Water/Still Water/GreatLake", "Environment/Water/Still Water/NorthPond", "Environment/Water/Still Water/WestPond",
        "Environment/Water/Still Water/WaterfallPond", "Environment/Water/Still Water/Swamp", "Environment/Landmarks/Waterfall/Waterfall Source Pool",
    };

    public static string All()
    {
        var log = new List<string>();
        var settings = AssetDatabase.LoadAssetAtPath<SpawnSystemSettings>(SettingsPath);
        SpawnCard fish = FishSchool(log);
        SpawnCard birds = BirdFlock(log);
        foreach (SpawnCard card in new[] { fish, birds })
            if (!settings.Cards.Contains(card))
            {
                settings.Cards.Add(card);
                EditorUtility.SetDirty(settings);
                log.Add("listed " + card.name);
            }
        AssetDatabase.SaveAssetIfDirty(settings);
        WireScene(log);
        return string.Join("\n", log);
    }

    static SpawnCard FishSchool(List<string> log)
    {
        string dir = Folder("FishSchool");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Fish).GetComponentInChildren<SkinnedMeshRenderer>(true);
        var member = new GameObject("FishSchool_Member");
        member.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        var renderer = member.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        GameObject memberPrefab = SavePrefab(member, dir + "/FishSchool_Member.prefab", log);

        FlockDefinition flock = Asset<FlockDefinition>(dir + "/FishSchool.asset", log);
        flock.Member = memberPrefab;
        flock.Count = new Vector2Int(6, 12);
        flock.Speed = 1.2f;
        flock.Acceleration = 2f;
        flock.RoamRadius = 8f;
        flock.NeighbourRadius = 2f;
        flock.Separation = 0.5f;
        flock.SeparationWeight = 1.5f;
        flock.Alignment = 1f;
        flock.Cohesion = 0.8f;
        flock.Wander = 0.5f;
        flock.HomePull = 1f;
        flock.BandPull = 2f;
        flock.VerticalFreedom = 0.25f;
        flock.FleeRadius = 6f;
        flock.FleeWeight = 3f;
        flock.FleeSpeedMultiplier = 2.5f;
        Save(flock);

        Spawnable school = FlockPrefab("FishSchool", dir, flock, log);
        SpawnCard card = Asset<SpawnCard>(dir + "/FishSchool_Card.asset", log);
        card.ClearVariants();
        card.SetVariant(school, 1f);
        card.GroupSize = new Vector2Int(1, 1);
        card.Category = SpawnCategory.Fish;
        card.Placement = new SpawnPlacement { Medium = SpawnMedium.Water, Height = new Vector2(0.8f, 3f), Clearance = 0.3f };
        Save(card);
        return card;
    }

    static SpawnCard BirdFlock(List<string> log)
    {
        string dir = Folder("BirdFlock");
        var material = AssetDatabase.LoadAssetAtPath<Material>(dir + "/BirdFlock_Member.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, dir + "/BirdFlock_Member.mat");
        }
        material.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.14f));
        Save(material);
        var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        var member = new GameObject("BirdFlock_Member");
        Part(member.transform, "Body", sphere, material, new Vector3(0.14f, 0.12f, 0.4f));
        Part(member.transform, "Wings", sphere, material, new Vector3(0.9f, 0.03f, 0.16f));
        GameObject memberPrefab = SavePrefab(member, dir + "/BirdFlock_Member.prefab", log);

        FlockDefinition flock = Asset<FlockDefinition>(dir + "/BirdFlock.asset", log);
        flock.Member = memberPrefab;
        flock.Count = new Vector2Int(5, 10);
        flock.Speed = 7f;
        flock.Acceleration = 6f;
        flock.RoamRadius = 40f;
        flock.NeighbourRadius = 8f;
        flock.Separation = 2f;
        flock.SeparationWeight = 1.5f;
        flock.Alignment = 1f;
        flock.Cohesion = 0.6f;
        flock.Wander = 0.6f;
        flock.HomePull = 1f;
        flock.BandPull = 2f;
        flock.VerticalFreedom = 0.5f;
        flock.FleeRadius = 15f;
        flock.FleeWeight = 3f;
        flock.FleeSpeedMultiplier = 1.6f;
        Save(flock);

        Spawnable birds = FlockPrefab("BirdFlock", dir, flock, log);
        SpawnCard card = Asset<SpawnCard>(dir + "/BirdFlock_Card.asset", log);
        card.ClearVariants();
        card.SetVariant(birds, 1f);
        card.GroupSize = new Vector2Int(1, 1);
        card.Category = SpawnCategory.Bird;
        card.Placement = new SpawnPlacement { Medium = SpawnMedium.Air, Height = new Vector2(12f, 25f), Clearance = 1.5f };
        Save(card);
        return card;
    }

    static void WireScene(List<string> log)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WorldMap)
        {
            log.Add("WorldMap isn't the active scene; not wired");
            return;
        }
        var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        if (!scope.TryGetComponent(out AmbientInstaller _))
        {
            scope.gameObject.AddComponent<AmbientInstaller>();
            log.Add("AmbientInstaller added");
        }
        var installer = scope.GetComponent<SpawnInstaller>();
        var so = new SerializedObject(installer);
        SerializedProperty list = so.FindProperty("_waterBodies");
        var bodies = new List<Renderer>();
        foreach (string path in WaterBodies)
        {
            GameObject body = GameObject.Find(path);
            if (body != null && body.TryGetComponent(out Renderer renderer))
                bodies.Add(renderer);
            else
                log.Add("no water body at " + path);
        }
        list.arraySize = bodies.Count;
        for (int i = 0; i < bodies.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
        if (so.ApplyModifiedProperties())
            log.Add($"{bodies.Count} water bodies assigned");
        if (scene.isDirty)
        {
            EditorSceneManager.SaveScene(scene);
            log.Add("saved " + scene.path);
        }
    }

    static Spawnable FlockPrefab(string name, string dir, FlockDefinition flock, List<string> log)
    {
        var root = new GameObject(name, typeof(Spawnable), typeof(Flock));
        var so = new SerializedObject(root.GetComponent<Flock>());
        so.FindProperty("_definition").objectReferenceValue = flock;
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root, $"{dir}/{name}.prefab", log).GetComponent<Spawnable>();
    }

    static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 scale)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static GameObject SavePrefab(GameObject root, string path, List<string> log)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.Add("saved " + path);
        return prefab;
    }

    static T Asset<T>(string path, List<string> log) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        log.Add("created " + path);
        return asset;
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    static string Folder(string name)
    {
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/_Game/Content", "Ambient");
        if (!AssetDatabase.IsValidFolder($"{Root}/{name}"))
            AssetDatabase.CreateFolder(Root, name);
        return $"{Root}/{name}";
    }
}
