using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Contact sheet + value stats for a folder of height stamps (renders through the GPU, textures aren't readable).
public static class StampSheet
{
    public static object Make(string folder, string outPng, int cell)
    {
        var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
        int cols = 6, rows = Mathf.CeilToInt(paths.Count / (float)cols);
        var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGB24, false);
        var stats = new StringBuilder();
        var rt = RenderTexture.GetTemporary(cell, cell, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        var read = new Texture2D(cell, cell, TextureFormat.RFloat, false, true);
        for (int i = 0; i < paths.Count; i++)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
            read.Apply();
            RenderTexture.active = null;
            var px = read.GetPixels().Select(c => c.r).ToArray();
            var sorted = px.OrderBy(v => v).ToArray();
            float P(float q) => sorted[Mathf.Clamp((int)(q * sorted.Length), 0, sorted.Length - 1)];
            // edge mean: how much the stamp fades to black at its border
            float edge = 0; int n = 0;
            for (int k = 0; k < cell; k++) { edge += px[k] + px[(cell - 1) * cell + k] + px[k * cell] + px[k * cell + cell - 1]; n += 4; }
            stats.AppendLine($"{i,2} {Path.GetFileNameWithoutExtension(paths[i]),-22} min {sorted[0]:0.00} p10 {P(.1f):0.00} p50 {P(.5f):0.00} p90 {P(.9f):0.00} max {sorted[^1]:0.00} edge {edge / n:0.00}");
            int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
            for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    float v = px[y * cell + x];
                    sheet.SetPixel(cx + x, cy + y, new Color(v, v, v));
                }
            // index marker
            for (int y = 0; y < 6; y++) for (int x = 0; x < 6 + i % 10 * 0; x++) sheet.SetPixel(cx + x, cy + cell - 1 - y, Color.red);
        }
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(outPng, sheet.EncodeToPNG());
        return stats.ToString();
    }
}
