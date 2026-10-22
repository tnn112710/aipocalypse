using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PrefabInfo
{
    // Components per child: lights, particle systems, audio, scripts (to vet props before placing them in a game scene).
    public static object Components(string paths)
    {
        var sb = new StringBuilder();
        foreach (var path in paths.Split(';'))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { sb.AppendLine("MISSING " + path); continue; }
            sb.AppendLine("== " + System.IO.Path.GetFileNameWithoutExtension(path));
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { sb.AppendLine("   <missing script>"); continue; }
                if (c is Transform || c is MeshFilter || c is MeshRenderer || c is LODGroup || c is MeshCollider || c is BoxCollider || c is CapsuleCollider || c is SphereCollider) continue;
                string extra = c is Light l ? $" {l.type} color {l.color} intensity {l.intensity} range {l.range} shadows {l.shadows} mode {l.lightmapBakeType}" : c is ParticleSystem ps ? $" max {ps.main.maxParticles} loop {ps.main.loop}" : c is AudioSource a ? $" clip {(a.clip ? a.clip.name : "none")} loop {a.loop}" : "";
                sb.AppendLine($"   {c.transform.name}: {c.GetType().FullName}{extra}");
            }
        }
        return sb.ToString();
    }
}
