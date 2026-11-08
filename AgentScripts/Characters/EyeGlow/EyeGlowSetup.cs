using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.EnemySystem;
using UnityEditor;
using UnityEngine;

// Glowing enemy eyes (EnemyEyes). Run with `unity command script run --file AgentScripts/Characters/EyeGlow/EyeGlowSetup.cs`:
//   EyeGlowSetup.Assets()   the goblin eye material and style, keeping existing ones
//   EyeGlowSetup.Goblins()  adds EnemyEyes to Goblin.prefab and places eyes on it and every variant (re-run after a model change)
//   EyeGlowSetup.Report()   each enemy prefab's eyes: style, glow, eye positions relative to the root
// The graph itself is rebuilt from EnemyEyeGlow.ops.json with ShaderGraphKit.Apply on a fresh Unlit graph.
public static class EyeGlowSetup
{
    const string Shader = "Aipocalypse/Enemies/EnemyEyeGlow";
    const string MaterialPath = "Assets/_Game/Content/Enemies/_Shared/EyeGlow/EyeGlow_Goblin.mat";
    const string StylePath = "Assets/_Game/Data/Enemies/EyeGlow/GoblinEyes.asset";
    const string GoblinFolder = "Assets/_Game/Content/Enemies/Goblin/";
    static readonly Color GoblinColor = new(4f, 2.1f, 0.3f, 1f);
    const float GoblinMinAngle = 0.005f;
    const float GoblinEyeBoneForward = 0.045f;

    public static object Assets()
    {
        var log = new List<string>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            EnsureFolder(MaterialPath);
            material = new Material(UnityEngine.Shader.Find(Shader));
            material.SetColor("_Color", GoblinColor);
            material.SetFloat("_MinAngle", GoblinMinAngle);
            AssetDatabase.CreateAsset(material, MaterialPath);
            log.Add("created " + MaterialPath);
        }
        var style = AssetDatabase.LoadAssetAtPath<EyeGlowStyle>(StylePath);
        if (style == null)
        {
            EnsureFolder(StylePath);
            style = ScriptableObject.CreateInstance<EyeGlowStyle>();
            style.Material = material;
            style.EyeBoneForward = GoblinEyeBoneForward; // clears Goblin_04's goggles
            AssetDatabase.CreateAsset(style, StylePath);
            log.Add("created " + StylePath);
        }
        return log.Count == 0 ? "assets exist" : string.Join("\n", log);
    }

    public static object Goblins()
    {
        var style = AssetDatabase.LoadAssetAtPath<EyeGlowStyle>(StylePath);
        if (style == null)
            return "run Assets() first";
        var log = new List<string>();
        // The base first: its variants inherit the component and override only the eyes.
        string basePath = GoblinFolder + "Goblin.prefab";
        foreach (string path in new[] { basePath }.Concat(Variants(basePath)))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var eyes = root.GetComponent<EnemyEyes>();
                if (eyes == null)
                    eyes = root.AddComponent<EnemyEyes>();
                var so = new SerializedObject(eyes);
                if (so.FindProperty("_style").objectReferenceValue == null)
                {
                    so.FindProperty("_style").objectReferenceValue = style;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                log.Add(eyes.Place());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return string.Join("\n", log);
    }

    public static object Report()
    {
        var lines = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab.GetComponent<EnemyBody>() == null)
                continue;
            var eyes = prefab.GetComponent<EnemyEyes>();
            if (eyes == null)
            {
                lines.Add($"{prefab.name}: no EnemyEyes");
                continue;
            }
            string at = string.Join(" ", eyes.Eyes.Select(e => e == null ? "missing" :
                $"{e.transform.parent.name}:{prefab.transform.InverseTransformPoint(e.transform.position):F3}"));
            lines.Add($"{prefab.name}: glow {eyes.Glow}, style {(eyes.Style != null ? eyes.Style.name : "none")}, {at}");
        }
        return string.Join("\n", lines);
    }

    static IEnumerable<string> Variants(string basePath)
    {
        var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        return AssetDatabase.FindAssets("t:Prefab", new[] { GoblinFolder.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p != basePath && PrefabUtility.GetCorrespondingObjectFromSource(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == baseAsset)
            .OrderBy(p => p);
    }

    static void EnsureFolder(string assetPath)
    {
        string folder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
