using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-off: Standard -> URP/Lit for the Layer Lab Weapon100 pack materials.
public static class ConvertWeapon100ToUrp
{
    const string Root = "Assets/_StoreAssets/Weapons/Layer Lab/3D Props-Weapon100";

    public static string Run()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var log = new List<string>();
        int converted = 0, skipped = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == lit) { skipped++; continue; }
            var spec = m.shader.name == "Standard (Specular setup)";
            if (m.shader.name != "Standard" && !spec) { log.Add($"skip {path}: {m.shader.name}"); skipped++; continue; }

            var tex = m.GetTexture("_MainTex");
            var scale = m.GetTextureScale("_MainTex");
            var offset = m.GetTextureOffset("_MainTex");
            var color = m.GetColor("_Color");
            var metallic = spec ? 0f : m.GetFloat("_Metallic");
            var smooth = m.GetFloat("_Glossiness");
            var specCol = spec ? m.GetColor("_SpecColor") : Color.black;
            var specMap = spec ? m.GetTexture("_SpecGlossMap") : null;
            var bump = m.GetTexture("_BumpMap");
            var bumpScale = m.GetFloat("_BumpScale");
            var metalMap = spec ? null : m.GetTexture("_MetallicGlossMap");
            var occ = m.GetTexture("_OcclusionMap");
            var emisMap = m.GetTexture("_EmissionMap");
            var emisCol = m.GetColor("_EmissionColor");
            var emisOn = m.IsKeywordEnabled("_EMISSION") && (emisMap != null || emisCol.maxColorComponent > 0.001f);
            var mode = m.GetFloat("_Mode");

            m.shader = lit;
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (spec)
            {
                m.SetFloat("_WorkflowMode", 0f);
                m.SetColor("_SpecColor", specCol);
                if (specMap != null) { m.SetTexture("_SpecGlossMap", specMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                m.EnableKeyword("_SPECULAR_SETUP");
            }
            if (metalMap != null) { m.SetTexture("_MetallicGlossMap", metalMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (bump != null) { m.SetTexture("_BumpMap", bump); m.SetFloat("_BumpScale", bumpScale); m.EnableKeyword("_NORMALMAP"); }
            if (occ != null) { m.SetTexture("_OcclusionMap", occ); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (emisOn)
            {
                m.SetTexture("_EmissionMap", emisMap);
                m.SetColor("_EmissionColor", emisCol);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
            }
            m.DisableKeyword("_SPECGLOSSMAP");
            if (!spec) m.DisableKeyword("_SPECULAR_SETUP");
            if (mode > 0) log.Add($"{path}: Standard _Mode {mode} (not opaque) converted as opaque");

            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            converted++;
        }
        log.Insert(0, $"converted {converted}, skipped {skipped}");
        return string.Join("\n", log);
    }
}
