using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Inspect
{
    public static object Scene()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine($"scene {scene.path}");
        foreach (var root in scene.GetRootGameObjects())
            sb.AppendLine($"[{root.name}] children {root.transform.childCount}");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            sb.AppendLine($"light {Path(l.transform)} {l.type} int {l.intensity} range {l.range} col {l.color} shadows {l.shadows}");
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            sb.AppendLine($"volume {Path(v.transform)} global {v.isGlobal} prio {v.priority} weight {v.weight} profile {AssetDatabase.GetAssetPath(v.sharedProfile)}");
        sb.AppendLine($"sky {AssetDatabase.GetAssetPath(RenderSettings.skybox)} shader {RenderSettings.skybox?.shader.name} sun {RenderSettings.sun?.name}");
        return sb.ToString();
    }

    public static object Find(string nameContains)
    {
        var sb = new StringBuilder();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0).Take(60))
            sb.AppendLine($"{Path(t)} [{string.Join(",", t.GetComponents<Component>().Select(x => x.GetType().Name))}] {(PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject) is var p && p ? AssetDatabase.GetAssetPath(p) : "")}");
        return sb.ToString();
    }

    public static object Renderers(string path)
    {
        var sb = new StringBuilder();
        var go = GameObject.Find(path);
        if (!go) return "not found";
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            sb.AppendLine($"{Path(r.transform)} {r.GetType().Name} mats [{string.Join(", ", r.sharedMaterials.Select(m => m ? m.name + " (" + m.shader.name + ")" : "null"))}]");
        return sb.ToString();
    }

    // Unique materials under each child group of a root, with renderer counts and component types.
    public static object Materials(string path)
    {
        var sb = new StringBuilder();
        var go = GameObject.Find(path);
        if (!go) return "not found";
        foreach (Transform group in go.transform)
        {
            var mats = new Dictionary<Material, int>();
            foreach (var r in group.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m) mats[m] = mats.TryGetValue(m, out var n) ? n + 1 : 1;
            var types = group.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct();
            sb.AppendLine($"{group.name}: types [{string.Join(",", types)}]");
            foreach (var kv in mats)
                sb.AppendLine($"   {kv.Value}x {kv.Key.name} | {kv.Key.shader.name} | {AssetDatabase.GetAssetPath(kv.Key)} | fogColor {(kv.Key.HasProperty("_FogColor") ? kv.Key.GetColor("_FogColor").ToString() : "-")} base {(kv.Key.HasProperty("_BaseColor") ? kv.Key.GetColor("_BaseColor").ToString() : kv.Key.HasProperty("_Color") ? kv.Key.GetColor("_Color").ToString() : "-")}");
        }
        return sb.ToString();
    }

    public static object Particles(string path)
    {
        var sb = new StringBuilder();
        var go = GameObject.Find(path);
        if (!go) return "not found";
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true).Take(40))
        {
            var e = ps.emission;
            sb.AppendLine($"{Path(ps.transform)} rate {e.rateOverTimeMultiplier} dist {e.rateOverDistanceMultiplier} bursts {e.burstCount} loop {ps.main.loop} play {ps.main.playOnAwake} max {ps.main.maxParticles} rot {ps.transform.eulerAngles}");
        }
        return sb.ToString();
    }

    // Unlit particle renderers that no AtmosphereTint above them covers.
    public static object UntintedUnlit()
    {
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Exclude))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.shader.name.Contains("Unlit")) continue;
            if (r.GetComponentInParent<MB13.Aipocalypse.Core.EnvironmentSystem.AtmosphereTint>() != null) continue;
            sb.AppendLine($"{Path(r.transform)} | {m.name} | {m.shader.name} | pos {r.transform.position:F0}");
        }
        return sb.ToString();
    }

    // Particle renderers seen from a point within a bearing range.
    public static object ParticlesToward(float x, float z, float fromBearing, float toBearing)
    {
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Exclude))
        {
            var p = r.transform.position;
            float bearing = Mathf.Repeat(Mathf.Atan2(p.x - x, p.z - z) * Mathf.Rad2Deg, 360f);
            if (bearing < fromBearing || bearing > toBearing) continue;
            var m = r.sharedMaterial;
            sb.AppendLine($"{Path(r.transform)} | {(m ? m.name + " | " + m.shader.name : "-")} | bearing {bearing:F0} dist {Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.z)):F0}");
        }
        return sb.ToString();
    }

    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
}
