using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

// Our own static TMP fonts, so Play never rewrites a font asset: a dynamic atlas grows as new text appears and the editor
// saves it (git noise, and vendor files changed). Builds Data/UI/Fonts/SairaHeader / SairaBold / SairaBody from the Synty
// Saira Condensed TTFs (ExtraBold / SemiBold / Regular, as Synty's own font assets) with Synty's settings and their
// underlay presets, makes DeathScreenFont static, then points every _Game prefab and asset from the Synty fonts to ours.
// Text outside `Characters` shows as a missing glyph: add it there and re-run. Safe to re-run (assets and GUIDs kept).
public static class FontSetup
{
    const string Dir = "Assets/_Game/Data/UI/Fonts";
    const string Synty = "Assets/Synty/InterfaceApocalypseHUD/Fonts/SairaCondensed/";
    const string DeathFontPath = "Assets/_Game/Data/UI/DeathScreen/DeathScreenFont.asset";
    const int SamplingSize = 90;
    const int Padding = 9;
    const int AtlasSize = 1024;

    // Printable ASCII and the punctuation the game's text uses.
    static readonly string Characters =
        new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + "·…—–‘’“”°×•";

    static readonly (string synty, string ttf, string ours)[] Fonts =
    {
        ("Font_Apocalypse_Header", "SairaCondensed-ExtraBold.ttf", "SairaHeader"),
        ("Font_Apocalypse_Bold", "SairaCondensed-SemiBold.ttf", "SairaBold"),
        ("Font_Apocalypse_Body", "SairaCondensed-Regular.ttf", "SairaBody"),
    };

    public static string All()
    {
        var log = new List<string>();
        var map = new Dictionary<Object, Object>();
        Build(map, log);
        DeathFont(log);
        Remap(map, log);
        return string.Join("\n", log);
    }

    static void Build(Dictionary<Object, Object> map, List<string> log)
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Data/UI", "Fonts");
        foreach ((string synty, string ttf, string ours) in Fonts)
        {
            var source = Load<Font>(Synty + ttf);
            string path = $"{Dir}/{ours}.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
                    AtlasPopulationMode.Dynamic, false);
                font.name = ours;
                AssetDatabase.CreateAsset(font, path);
                font.atlasTextures[0].name = ours + " Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                font.material.name = ours + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }
            log.Add(Fill(font));

            var syntyFont = Load<TMP_FontAsset>(Synty + synty + ".asset");
            map[syntyFont] = font;
            map[syntyFont.material] = font.material;
            var syntyUnderlay = AssetDatabase.LoadAssetAtPath<Material>(Synty + synty + "_Underlay.mat");
            if (syntyUnderlay != null)
                map[syntyUnderlay] = Preset(syntyUnderlay, font, $"{Dir}/{ours}_Underlay.mat", log);
        }
    }

    // Empties the atlas, adds `Characters`, then freezes it.
    static string Fill(TMP_FontAsset font)
    {
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.ClearFontAssetData(true);
        font.TryAddCharacters(Characters, out string missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        foreach (Texture2D atlas in font.atlasTextures)
            EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssetIfDirty(font);
        return $"{font.name}: static, {font.characterTable.Count} characters" +
               (string.IsNullOrEmpty(missing) ? string.Empty : $", not in the font: {missing}");
    }

    // A copy of a Synty material preset (underlay look) that draws our atlas.
    static Material Preset(Material synty, TMP_FontAsset font, string path, List<string> log)
    {
        var copy = new Material(synty);
        copy.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
        copy.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
        copy.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
        copy.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            log.Add("created " + path);
            return copy;
        }
        existing.CopyPropertiesFromMaterial(copy);
        Object.DestroyImmediate(copy);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }

    static void DeathFont(List<string> log) => log.Add(Fill(Load<TMP_FontAsset>(DeathFontPath)));

    // Every object reference under Assets/_Game (prefabs, ScriptableObjects; scenes are rebuilt by their setup scripts).
    static void Remap(Dictionary<Object, Object> map, List<string> log)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.GetDependencies(path, false).Any(d => d.StartsWith(Synty)))
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = root.GetComponentsInChildren<Component>(true).Where(c => c != null).Sum(c => Swap(c, map));
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"{path}: {changed}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Game" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is TMP_FontAsset || !AssetDatabase.GetDependencies(path, false).Any(d => d.StartsWith(Synty)))
                continue;
            int changed = Swap(asset, map);
            if (changed > 0)
            {
                AssetDatabase.SaveAssetIfDirty(asset);
                log.Add($"{path}: {changed}");
            }
        }
    }

    static int Swap(Object target, Dictionary<Object, Object> map)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.GetIterator();
        int changed = 0;
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                map.TryGetValue(property.objectReferenceValue, out Object replacement))
            {
                property.objectReferenceValue = replacement;
                changed++;
            }
        if (changed > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        return changed;
    }

    static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception($"missing {typeof(T).Name} {path}");
}
