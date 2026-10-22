using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class Previews
{
    static readonly string[] Paths =
    {
        "Assets/_StoreAssets/Env/Animpic/Polygon-Mega Survival/Prefabs/Lantern.prefab",
        "Assets/_StoreAssets/Env/Animpic/Polygon-Mega Survival/Prefabs/New/SM_Kerosene_Lamp.prefab",
        "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Prefabs/Environment/Lamp_1.prefab",
        "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Witches House/Lamp.prefab",
        "Assets/_StoreAssets/Env/SICS/Toon Fantasy Nature/Prefabs/Props/TFF_Gas_Lamp_01A.prefab",
        "Assets/_StoreAssets/Env/SICS/Toon Series/Toon Farm Pack/Prefabs/Props/Exterior Props/TFP_Gas_Lamp_01A.prefab",
        "Assets/_StoreAssets/Env/SICS/Toon Series/Toon Enchanted Meadow/Prefabs/Props/Exterior Props/TEM_Lamp_01A.prefab",
    };

    public static async Task<string> Save(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        for (int i = 0; i < Paths.Length; i++)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[i]);
            if (go == null) { sb.AppendLine("missing " + Paths[i]); continue; }
            Texture2D tex = null;
            for (int tries = 0; tries < 100 && tex == null; tries++)
            {
                tex = AssetPreview.GetAssetPreview(go);
                if (tex == null) await Task.Delay(50);
            }
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            string mats = "";
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    mats += (m != null ? m.shader.name : "null") + "; ";
            sb.AppendLine($"{i}: {go.name} size={bounds.size} center={bounds.center} lights={go.GetComponentsInChildren<Light>(true).Length} shaders={mats}");
            if (tex == null) { sb.AppendLine("  no preview"); continue; }
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.SetPixels(tex.GetPixels());
            File.WriteAllBytes(Path.Combine(outDir, $"{i}_{go.name}.png"), copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
        }
        return sb.ToString();
    }
}
