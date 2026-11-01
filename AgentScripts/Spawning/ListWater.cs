using System.Text;
using UnityEngine;

public static class ListWater
{
    // Renderers on the Water layer (4) in the open scene: path, bounds size and top.
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (r.gameObject.layer != 4) continue;
            string path = r.name;
            for (var p = r.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
            var b = r.bounds;
            sb.AppendLine($"{path} | size {b.size:F1} | top {b.max.y:F2}");
        }
        return sb.ToString();
    }
}
