// Reports size, LODs, colliders and shaders of prefabs (run via `unity command run_script`, editor only).
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MeasurePrefabs
{
    // folders: ';'-separated asset folders; filter: substring of the prefab path ("" = all).
    public static object Run(string folders, string filter)
    {
        var sb = new StringBuilder();
        var guids = AssetDatabase.FindAssets("t:Prefab", folders.Split(';'));
        foreach (var path in guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains(filter)).OrderBy(p => p))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var b = new Bounds(); bool any = false;
            int tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>();
                var lod = root.GetComponent<LODGroup>();
                // only LOD0 / non-LOD renderers count for the size
                if (lod != null && lod.GetLODs().Length > 0 && !lod.GetLODs()[0].renderers.Contains(r)) continue;
                var m = mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                foreach (var c in Corners(mb))
                {
                    var w = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
                }
                tris += mf.sharedMesh.triangles.Length / 3;
            }
            var lods = root.GetComponent<LODGroup>();
            var cols = root.GetComponentsInChildren<Collider>(true).Select(c => c.GetType().Name).Distinct();
            var shaders = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.shader.name).Distinct();
            sb.AppendLine($"{path.Replace("Assets/_StoreAssets/Env/", "")} | size {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0} | min.y {b.min.y:0.0} center {b.center.x:0.0},{b.center.z:0.0} | lod0 tris {tris} | LODs {(lods ? lods.lodCount : 0)} | colliders {string.Join(",", cols)} | scale {root.transform.localScale.x:0.##} | {string.Join(", ", shaders)}");
        }
        return sb.ToString();
    }

    static Vector3[] Corners(Bounds b)
    {
        var c = new Vector3[8]; int i = 0;
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            c[i++] = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
        return c;
    }
    // Temporary preview instance under "__Preview" (not saved; Clear removes it).
    public static object Preview(string path, float x, float y, float z, float scale, float rotY)
    {
        var parent = GameObject.Find("__Preview") ?? new GameObject("__Preview");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.transform);
        go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0, rotY, 0));
        go.transform.localScale = Vector3.one * scale;
        return go.name;
    }

    // Many previews at once: "path|scale|x|y|z|rotY;..."
    public static object Lineup(string specs)
    {
        int n = 0;
        foreach (var spec in specs.Split(';'))
        {
            var f = spec.Split('|');
            if (f.Length < 6) continue;
            Preview(f[0], float.Parse(f[2]), float.Parse(f[3]), float.Parse(f[4]), float.Parse(f[1]), float.Parse(f[5]));
            n++;
        }
        return n + " placed";
    }

    public static object SetActive(string path, bool on)
    {
        var parts = path.Split('/');
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        var t = root == null ? null : parts.Length == 1 ? root.transform : root.transform.Find(string.Join("/", parts.Skip(1)));
        if (t == null) return "missing " + path;
        t.gameObject.SetActive(on);
        return path + " active=" + on;
    }

    public static object Clear()
    {
        var parent = GameObject.Find("__Preview");
        if (parent != null) Object.DestroyImmediate(parent);
        return "cleared";
    }
}
