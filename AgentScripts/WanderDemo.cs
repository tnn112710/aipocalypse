using System;
using System.Linq;
using System.Reflection;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnemySystem;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
using Opsive.BehaviorDesigner.Runtime.Tasks.Events;
using Opsive.GraphDesigner.Runtime;
using Pathfinding;
using Pathfinding.RVO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WanderDemo
{
    const string DataFolder = "Assets/_Game/Data/Enemies";

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (GameObject.Find("Wander Enemies") != null) throw new InvalidOperationException("Demo already exists.");
        var player = GameObject.Find("Player");
        var terrain = Terrain.activeTerrain;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
        {
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { enemyLayer = i; break; }
            if (enemyLayer < 0) throw new InvalidOperationException("No free Enemy layer.");
            layers.GetArrayElementAtIndex(enemyLayer).stringValue = "Enemy";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
        var center = player.transform.position + player.transform.forward * 10f;
        center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
        EnsureFolder(DataFolder);
        EnsureFolder("Assets/_Game/Prefabs");
        var settings = ScriptableObject.CreateInstance<WanderSettings>();
        AssetDatabase.CreateAsset(settings, DataFolder + "/WanderSettings.asset");

        var navigationObject = new GameObject("Navigation");
        var navigation = navigationObject.AddComponent<AstarPath>();
        navigationObject.AddComponent<RVOSimulator>();
        var graph = navigation.data.AddGraph(typeof(GridGraph)) as GridGraph;
        graph.center = center;
        graph.SetDimensions(64, 64, 0.75f);
        graph.maxSlope = 45f;
        graph.collision.heightCheck = true;
        graph.collision.fromHeight = 200f;
        graph.collision.heightMask = 1 << terrain.gameObject.layer;
        graph.collision.collisionCheck = true;
        graph.collision.mask = 1 << terrain.gameObject.layer;
        graph.collision.diameter = 1.4f;
        graph.collision.collisionOffset = 0.6f;
        graph.collision.height = 0.8f;
        navigation.Scan();
        navigation.data.SetData(navigation.data.SerializeGraphs());
        PrefabUtility.SaveAsPrefabAssetAndConnect(navigationObject, "Assets/_Game/Prefabs/Navigation.prefab", InteractionMode.AutomatedAction);
        navigation.Scan();
        graph = (GridGraph)navigation.data.graphs[0];

        var subtree = ScriptableObject.CreateInstance<Subtree>();
        subtree.Name = "Wander";
        var repeat = new Repeater { Index = 0, ParentIndex = ushort.MaxValue, SiblingIndex = ushort.MaxValue, RepeatForever = true };
        var sequence = new Sequence { Index = 1, ParentIndex = 0, SiblingIndex = ushort.MaxValue };
        var choose = new ChooseWanderDestination { Index = 2, ParentIndex = 1, SiblingIndex = 3 };
        var walk = new WalkToWanderDestination { Index = 3, ParentIndex = 1, SiblingIndex = 4 };
        var pause = new PauseWander { Index = 4, ParentIndex = 1, SiblingIndex = ushort.MaxValue };
        subtree.EventNodes = new IEventNode[] { new Start { ConnectedIndex = 0 } };
        subtree.TreeLogicNodes = new ITreeLogicNode[] { repeat, sequence, choose, walk, pause };
        subtree.EventNodeProperties = new[] { new NodeProperties { Position = new Vector2(0, 0) } };
        subtree.LogicNodeProperties = new[] {
            Props("Repeat forever", 0, 100), Props("Wander", 0, 220),
            Props("Choose reachable point", -250, 350), Props("Walk to point", 0, 350), Props("Pause", 250, 350)
        };
        subtree.Serialize();
        AssetDatabase.CreateAsset(subtree, DataFolder + "/Wander.asset");

        var group = new GameObject("Wander Enemies");
        var brains = new EnemyBrain[2];
        for (int i = 0; i < 2; i++)
        {
            var desired = center + new Vector3(i == 0 ? -3 : 3, 0, 0);
            var nearest = navigation.GetNearest(desired, NearestNodeConstraint.Walkable);
            if (nearest.node == null) throw new InvalidOperationException("No walkable spawn point.");
            var agentObject = new GameObject("Wander Capsule " + (i + 1));
            agentObject.layer = enemyLayer;
            agentObject.SetActive(false);
            agentObject.transform.SetParent(group.transform);
            agentObject.transform.position = nearest.position;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Capsule";
            visual.layer = enemyLayer;
            visual.transform.SetParent(agentObject.transform, false);
            visual.transform.localPosition = Vector3.up;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", i == 0 ? new Color(0.1f, 0.65f, 1f) : new Color(1f, 0.35f, 0.08f));
            AssetDatabase.CreateAsset(material, DataFolder + "/WanderCapsule" + (i + 1) + ".mat");
            visual.GetComponent<Renderer>().sharedMaterial = material;
            var collider = agentObject.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.5f;
            var follower = agentObject.AddComponent<FollowerEntity>();
            follower.height = 2f;
            follower.radius = 0.5f;
            follower.enableLocalAvoidance = true;
            follower.groundMask = 1 << terrain.gameObject.layer;
            var mover = agentObject.AddComponent<WanderMover>();
            var moverSO = new SerializedObject(mover);
            moverSO.FindProperty("_settings").objectReferenceValue = settings;
            moverSO.ApplyModifiedPropertiesWithoutUndo();
            var tree = agentObject.AddComponent<BehaviorTree>();
            tree.StartWhenEnabled = false;
            tree.UpdateMode = UpdateMode.Manual;
            tree.Subgraph = subtree;
            tree.Name = "Wander";
            brains[i] = agentObject.AddComponent<EnemyBrain>();
            agentObject.SetActive(true);
        }
        var scope = new GameObject("GameLifetimeScope").AddComponent<GameLifetimeScope>();
        var so = new SerializedObject(scope);
        so.FindProperty("_navigation").objectReferenceValue = navigation;
        so.FindProperty("_wanderSettings").objectReferenceValue = settings;
        var enemies = so.FindProperty("_enemies");
        enemies.arraySize = brains.Length;
        for (int i = 0; i < brains.Length; i++) enemies.GetArrayElementAtIndex(i).objectReferenceValue = brains[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(navigation);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = group;
        SceneView.lastActiveSceneView.LookAtDirect(center + Vector3.up, Quaternion.Euler(38, 0, 0), 22f);
        return new { scene = EditorSceneManager.GetActiveScene().path, center = center.ToString(), agents = brains.Select(b => b.transform.position.ToString()).ToArray(), walkableNodes = graph.nodes.Count(n => n.Walkable) };
    }

    static LogicNodeProperties Props(string label, float x, float y) => new LogicNodeProperties { DisplayName = label, Position = new Vector2(x, y) };

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    public static object Inspect() => UnityEngine.Object.FindObjectsByType<EnemyBrain>().Select(b => new {
        b.name, position = b.transform.position.ToString(), active = b.Tree != null && b.Tree.IsActive(),
        status = b.Tree == null ? "uninitialized" : b.Tree.Status.ToString(),
        destination = b.GetComponent<FollowerEntity>().destination.ToString(),
        choices = b.GetComponent<WanderMover>().DestinationsChosen,
        speed = b.GetComponent<FollowerEntity>().velocity.magnitude,
        stopped = b.GetComponent<FollowerEntity>().isStopped,
        paused = EditorApplication.isPaused, timeScale = Time.timeScale, time = Time.time,
        nodes = b.Tree == null ? null : Enumerable.Range(0, 5).Select(i => b.Tree.GetRuntimeNodeStatus(i).ToString()).ToArray()
    }).ToArray();

}

