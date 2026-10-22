using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
public static class BoundsProbe
{
    // Real renderer bounds of prefabs (instantiated off-map, then destroyed).
    public static object Run(string paths)
    {
        var sb = new StringBuilder();
        foreach (var path in paths.Split(';'))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("MISSING " + path); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(-5000, 0, -5000);
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) { sb.AppendLine("no renderers " + path); Object.DestroyImmediate(go); continue; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var mats = rs.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.shader.name).Distinct();
            sb.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: size {b.size.ToString("0.0")} min.y {(b.min.y):0.0} centerOff {(b.center - go.transform.position).ToString("0.0")} renderers {rs.Length} cols {go.GetComponentsInChildren<Collider>(true).Length} shaders {string.Join(",", mats)}");
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }
}
