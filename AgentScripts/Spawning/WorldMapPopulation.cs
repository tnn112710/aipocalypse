// Open-world population of WorldMap (edit mode, WorldMap open):
//   unity command script run --file AgentScripts/Spawning/WorldMapPopulation.cs --entry WorldMapPopulation.Setup
// Adds the goblin camp on the Great Lake's west shore (a SpawnArea of 4 goblins, back 3 min after a kill, awake within 120 m)
// under "Spawn Areas", lists the rivers on the SpawnInstaller (fish in rivers) and saves the scene. Running it again updates
// the same objects.
using System.Collections.Generic;
using Aipocalypse.Core.SpawnSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RM = StylizedWater3.RiverModeler;

public static class WorldMapPopulation
{
    const string CardPath = "Assets/_Game/Content/Enemies/Goblin/Goblin_Card.asset";
    static readonly Vector2 CampXZ = new(490f, 480f);

    public static string Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "WorldMap") return "open WorldMap first";
        var installer = Object.FindAnyObjectByType<SpawnInstaller>();
        if (installer == null) return "no SpawnInstaller";

        var root = GameObject.Find("Spawn Areas") ?? new GameObject("Spawn Areas");
        Transform campTransform = root.transform.Find("Goblin Camp (Lake)");
        var camp = campTransform != null ? campTransform.gameObject : new GameObject("Goblin Camp (Lake)", typeof(SpawnZone), typeof(SpawnArea));
        camp.transform.SetParent(root.transform, false);
        Physics.Raycast(new Vector3(CampXZ.x, 500f, CampXZ.y), Vector3.down, out RaycastHit ground, 1000f, 1);
        camp.transform.SetPositionAndRotation(ground.point, Quaternion.identity);

        var zone = new SerializedObject(camp.GetComponent<SpawnZone>());
        zone.FindProperty("_id").stringValue = "Camp_Lake";
        zone.FindProperty("_minRadius").floatValue = 0f;
        zone.FindProperty("_maxRadius").floatValue = 15f;
        zone.FindProperty("_halfAngle").floatValue = 180f;
        zone.ApplyModifiedPropertiesWithoutUndo();

        var area = new SerializedObject(camp.GetComponent<SpawnArea>());
        area.FindProperty("_card").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SpawnCard>(CardPath);
        area.FindProperty("_capacity").intValue = 4;
        area.FindProperty("_groupSize").intValue = 4;
        area.FindProperty("_respawnDelay").floatValue = 180f;
        area.FindProperty("_activateRadius").floatValue = 120f;
        area.FindProperty("_deactivateRadius").floatValue = 160f;
        area.FindProperty("_prewarm").boolValue = true;
        area.ApplyModifiedPropertiesWithoutUndo();

        var rivers = new List<RM.RiverModeler>(Object.FindObjectsByType<RM.RiverModeler>(FindObjectsInactive.Exclude));
        rivers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var spawning = new SerializedObject(installer);
        SerializedProperty list = spawning.FindProperty("_rivers");
        list.arraySize = rivers.Count;
        for (int i = 0; i < rivers.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = rivers[i];
        spawning.ApplyModifiedPropertiesWithoutUndo();
        installer.CollectSpawnAreas();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return $"camp at {ground.point}, {rivers.Count} rivers ({string.Join(", ", rivers.ConvertAll(r => r.name))}), saved {saved}";
    }
}
