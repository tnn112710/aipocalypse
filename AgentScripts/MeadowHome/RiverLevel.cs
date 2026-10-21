using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Keeps hand-made render settings (shadows off on rocks) across a partial rebuild of the water-dependent groups.
public static class RiverLevel
{
    static readonly string[] Roots = { "Dressing/Bank Rocks", "Dressing/Home Pond", "Landmarks/Waterfall" };

    static Transform Env() => SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Environment").transform;

    // Subgroup key: the first two levels under a root ("Dressing/Home Pond/In Water").
    static string Key(string root, Transform rootT, Transform t)
    {
        var chain = new List<string>();
        for (var c = t; c != null && c != rootT; c = c.parent) chain.Add(c.name);
        chain.Reverse();
        return root + "/" + string.Join("/", chain.Take(Mathf.Min(2, chain.Count - 1)));
    }

    public static object Snapshot(string path)
    {
        var env = Env();
        var stats = new Dictionary<string, int[]>();
        foreach (var root in Roots)
        {
            var rt = env.Find(root);
            if (rt == null) continue;
            foreach (var r in rt.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var k = Key(root, rt, r.transform);
                if (!stats.TryGetValue(k, out var c)) stats[k] = c = new int[2];
                c[r.shadowCastingMode == ShadowCastingMode.Off ? 0 : 1]++;
            }
        }
        File.WriteAllLines(path, stats.Select(s => $"{s.Key}\t{s.Value[0]}\t{s.Value[1]}"));
        return stats.ToDictionary(s => s.Key, s => $"off {s.Value[0]} on {s.Value[1]}");
    }

    // Turns shadows off in every subgroup that was mostly shadowless in the snapshot.
    public static object Apply(string path)
    {
        var off = File.ReadAllLines(path).Select(l => l.Split('\t')).Where(f => int.Parse(f[1]) > int.Parse(f[2])).Select(f => f[0]).ToHashSet();
        var env = Env();
        var changed = new Dictionary<string, int>();
        foreach (var root in Roots)
        {
            var rt = env.Find(root);
            if (rt == null) continue;
            foreach (var r in rt.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var k = Key(root, rt, r.transform);
                if (!off.Contains(k) || r.shadowCastingMode == ShadowCastingMode.Off) continue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                changed[k] = changed.TryGetValue(k, out var n) ? n + 1 : 1;
            }
        }
        if (changed.Count > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return changed;
    }

    // River knots (x, z) and the home pond centre, matching MeadowHomeLayout.
    static readonly Vector2[] RiverXZ = { new(264,402), new(277,397), new(292,384), new(312,362), new(348,339), new(386,321), new(430,303), new(468,289), new(490,284), new(508,283), new(524,285), new(542,289) };
    static readonly Vector2 PondCentre = new(232, 425);

    static float RiverDistance(Vector2 p)
    {
        float d = float.MaxValue;
        for (int i = 1; i < RiverXZ.Length; i++)
        {
            var a = RiverXZ[i - 1]; var ab = RiverXZ[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            d = Mathf.Min(d, Vector2.Distance(p, a + t * ab));
        }
        return Mathf.Min(d, Vector2.Distance(p, PondCentre) - 30f);
    }

    // Moves the river mist from the old sloped surface down to the new level.
    public static object MoveMist(float level)
    {
        var mist = Env().GetComponentsInChildren<Transform>(true).Where(t => t.name == "Mist").SelectMany(t => t.Cast<Transform>()).ToList();
        var moved = new List<string>();
        foreach (var t in mist)
        {
            var p = t.position;
            if (p.y < level + 0.2f || p.y > 23.5f || RiverDistance(new Vector2(p.x, p.z)) > 20f) continue;
            UnityEditor.Undo.RecordObject(t, "Mist");
            t.position = new Vector3(p.x, level + 0.35f, p.z);
            moved.Add($"{t.name} ({p.x:0},{p.z:0}) {p.y:0.00} -> {level + 0.35f:0.00}");
        }
        return new { total = mist.Count, moved };
    }

    // Objects near the river or pond (outside the rebuilt groups) whose base now floats above the terrain.
    public static object Floating(float within, float above)
    {
        var terrain = Terrain.activeTerrain;
        var env = Env();
        var skip = Roots.Select(r => env.Find(r)).Where(t => t != null).ToHashSet();
        var hits = new List<string>();
        foreach (var r in env.GetComponentsInChildren<Renderer>())
        {
            var t = r.transform;
            if (r is ParticleSystemRenderer || skip.Any(s => t.IsChildOf(s))) continue;
            var lod = t.GetComponentInParent<LODGroup>();
            var root = lod != null ? lod.transform : t;
            var b = r.bounds;
            var c = new Vector2(b.center.x, b.center.z);
            if (RiverDistance(c) > within) continue;
            float h = terrain.SampleHeight(b.center) + terrain.transform.position.y;
            if (b.min.y - h > above) hits.Add($"{Path(root)} min {b.min.y:0.0} ground {h:0.0}");
        }
        return hits.Distinct().Take(200).ToList();
    }

    // Lowers placed objects (outermost prefab instances outside the rebuilt groups, water and stamps) by how much the
    // ground under their pivot dropped between the old terrain and the current one. apply=false only reports.
    public static object Settle(string oldTerrainPath, float within, float minDrop, bool apply)
    {
        var old = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainData>(oldTerrainPath);
        var terrain = Terrain.activeTerrain;
        var size = terrain.terrainData.size;
        var tp = terrain.transform.position;
        var env = Env();
        var skip = Roots.Select(r => env.Find(r)).Append(env.Find("Water")).Where(t => t != null).ToHashSet();
        var report = new Dictionary<string, List<string>>();
        int count = 0;
        foreach (var t in env.GetComponentsInChildren<Transform>(true))
        {
            if (!UnityEditor.PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) || skip.Any(s => t.IsChildOf(s))) continue;
            var parentPath = Path(t.parent);
            if (parentPath.EndsWith("/Mist") || parentPath.EndsWith("/Lilies") || parentPath.Contains("/River Bridge")) continue;   // on the water, or moved by its banks
            var p = t.position;
            if (RiverDistance(new Vector2(p.x, p.z)) > within) continue;
            float u = (p.x - tp.x) / size.x, v = (p.z - tp.z) / size.z;
            if (u < 0 || u > 1 || v < 0 || v > 1) continue;
            float drop = old.GetInterpolatedHeight(u, v) - terrain.terrainData.GetInterpolatedHeight(u, v);
            if (drop < minDrop) continue;
            var group = parentPath;
            if (!report.TryGetValue(group, out var list)) report[group] = list = new List<string>();
            list.Add($"{t.name} ({p.x:0},{p.z:0}) y {p.y:0.00} drop {drop:0.00}");
            count++;
            if (!apply) continue;
            UnityEditor.Undo.RecordObject(t, "Settle");
            t.position = p - new Vector3(0, drop, 0);
        }
        if (apply && count > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return new { count, groups = report.ToDictionary(g => g.Key, g => g.Value.Count > 12 ? g.Value.Take(12).Append($"... {g.Value.Count} total").ToList() : g.Value) };
    }

    // The bridge rests on its banks: lower it by the smaller drop at its two span ends.
    public static object SettleBridge(string oldTerrainPath, bool apply)
    {
        var old = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainData>(oldTerrainPath);
        var cur = Terrain.activeTerrain.terrainData;
        var bridge = Env().Find("Landmarks/River Bridge").Cast<Transform>().First(t => t.name == "Bridge");
        var b = bridge.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        float half = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(bridge.forward.x), 0, Mathf.Abs(bridge.forward.z)));
        float Drop(Vector3 p) => old.GetInterpolatedHeight(p.x / 1000f, p.z / 1000f) - cur.GetInterpolatedHeight(p.x / 1000f, p.z / 1000f);
        var a1 = bridge.position + bridge.forward * half; var a2 = bridge.position - bridge.forward * half;
        float drop = Mathf.Min(Drop(a1), Drop(a2));
        float bankA = cur.GetInterpolatedHeight(a1.x / 1000f, a1.z / 1000f), bankB = cur.GetInterpolatedHeight(a2.x / 1000f, a2.z / 1000f);
        if (apply && drop > 0.05f)
        {
            UnityEditor.Undo.RecordObject(bridge, "Settle bridge");
            bridge.position -= new Vector3(0, drop, 0);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        return $"half span {half:0.0} drops {Drop(a1):0.00}/{Drop(a2):0.00} banks now {bankA:0.0}/{bankB:0.0} bridge y {bridge.position.y:0.00}";
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
