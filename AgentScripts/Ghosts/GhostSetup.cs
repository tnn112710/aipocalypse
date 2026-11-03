using Aipocalypse.Core.Ghosts;
using Aipocalypse.Core.PlayerSystem;
using UnityEditor;
using UnityEngine;

// Builds the ghost look (GhostStyle in Data/Ghosts: a Valid and an Invalid material on Aipocalypse/Ghosts/Ghost, and its
// depth-only pass) and gives it to the player's interaction tuning. The graph itself: GhostGraph.json through ShaderGraphKit.
// Safe to re-run. Run: unity command script run --file AgentScripts/Ghosts/GhostSetup.cs --entry GhostSetup.All
public static class GhostSetup
{
    const string Dir = "Assets/_Game/Data/Ghosts";
    const string ShaderName = "Aipocalypse/Ghosts/Ghost";
    const string InteractionTuningPath = "Assets/_Game/Data/Player/PlayerInteractionTuning.asset";
    // After every other transparent; the depth pass just before the looks, so only the nearest surfaces get colour.
    const int DepthQueue = 3100;
    const int LookQueue = 3101;

    public static string All()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            return $"{ShaderName} not found: build _Core/_Common/Ghosts/Shaders/Ghost.shadergraph first";
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Data", "Ghosts");

        Material valid = Material("Ghost_Valid", shader, LookQueue, new Color(0.2f, 1f, 0.3f, 0.32f), new Color(0.45f, 2f, 0.55f, 0.7f));
        Material invalid = Material("Ghost_Invalid", shader, LookQueue, new Color(1f, 0.22f, 0.18f, 0.32f), new Color(2f, 0.45f, 0.35f, 0.7f));
        Material depth = Material("Ghost_Depth", shader, DepthQueue, Color.clear, Color.clear);

        string stylePath = Dir + "/GhostStyle.asset";
        var style = AssetDatabase.LoadAssetAtPath<GhostStyle>(stylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<GhostStyle>();
            AssetDatabase.CreateAsset(style, stylePath);
        }
        style.Valid = valid;
        style.Invalid = invalid;
        style.DepthOnly = depth;
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);

        var tuning = AssetDatabase.LoadAssetAtPath<PlayerInteractionTuning>(InteractionTuningPath);
        var so = new SerializedObject(tuning);
        so.FindProperty("_ghost").objectReferenceValue = style;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(tuning);
        return "GhostStyle (Valid, Invalid, Depth) → PlayerInteractionTuning.Ghost";
    }

    static Material Material(string name, Shader shader, int queue, Color fill, Color rim)
    {
        string path = $"{Dir}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Color", fill);
        material.SetColor("_RimColor", rim);
        material.renderQueue = queue;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }
}
