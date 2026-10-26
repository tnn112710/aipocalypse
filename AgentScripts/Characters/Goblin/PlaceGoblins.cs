// Places the ten goblins in WorldMap's Enemies group outside the home gate (where the wander capsules stood, now removed)
// and saves the scene, which makes EnemyInstaller collect them. Re-running moves the existing ones instead of adding more.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlaceGoblins
{
    const string Folder = "Assets/_Game/Content/Enemies/Goblin/";
    static readonly (string name, string prefab, Vector2 xz, float yaw)[] Goblins =
    {
        ("Goblin 1", "Goblin", new Vector2(271f, 312f), 140f),
        ("Goblin 2", "Goblin_02", new Vector2(287f, 309f), 220f),
        ("Goblin 3", "Goblin_03", new Vector2(277f, 306f), 20f),
        ("Goblin 4", "Goblin_04", new Vector2(282f, 303f), 300f),
        ("Goblin 5", "Goblin_05", new Vector2(279f, 317f), 180f),
        ("Goblin 6", "Goblin_06", new Vector2(266f, 305f), 90f),
        // Looks repeat from here.
        ("Goblin 7", "Goblin", new Vector2(285f, 298f), 250f),
        ("Goblin 8", "Goblin_02", new Vector2(268f, 319f), 120f),
        ("Goblin 9", "Goblin_03", new Vector2(291f, 316f), 160f),
        ("Goblin 10", "Goblin_04", new Vector2(273f, 297f), 30f),
    };
    static readonly string[] Removed = { "Wander Capsule 1", "Wander Capsule 2" };

    public static string Run()
    {
        var parent = GameObject.Find("Enemies");
        if (parent == null)
            return "no Enemies root (is WorldMap open?)";
        string result = "";
        foreach (string name in Removed)
        {
            Transform old = parent.transform.Find(name);
            if (old == null)
                continue;
            Object.DestroyImmediate(old.gameObject);
            result += "removed " + name + "; ";
        }
        foreach (var goblin in Goblins)
            result += Place(parent.transform, goblin.name, Folder + goblin.prefab + ".prefab", goblin.xz, goblin.yaw) + "; ";
        EditorSceneManager.SaveScene(parent.scene);
        return result;
    }

    static string Place(Transform parent, string name, string prefabPath, Vector2 xz, float yaw)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), parent);
        go.name = name;
        Terrain terrain = Terrain.activeTerrain;
        var position = new Vector3(xz.x, 0f, xz.y);
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return name + " " + position.ToString("F1");
    }
}
