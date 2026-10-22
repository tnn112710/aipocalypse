using System.Text;
using UnityEditor;
using UnityEngine;

public static class Inspect
{
    public static string Hierarchy(string path)
    {
        var sb = new StringBuilder();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) return "missing " + path;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            sb.Append(t.name).Append(" active=").Append(t.gameObject.activeSelf).Append(" pos=").Append(t.localPosition).Append(" rot=").Append(t.localEulerAngles).Append(" scale=").Append(t.localScale).Append(" layer=").Append(t.gameObject.layer);
            foreach (Component c in t.GetComponents<Component>())
                sb.Append(" [").Append(c.GetType().Name).Append(']');
            sb.AppendLine();
            if (t.TryGetComponent(out Renderer r))
            {
                sb.Append("    bounds=").Append(r.bounds.size).Append(" center=").Append(r.bounds.center).Append(" shadows=").Append(r.shadowCastingMode);
                foreach (Material m in r.sharedMaterials)
                    sb.Append(" mat=").Append(m != null ? m.name + "(" + m.shader.name + ")" : "null");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }
}
