// Renders farm animal models into one PNG grid, offscreen (no scene touched), to compare looks and materials:
//   Compare(png, material): the Toon Farm Pack's own toon material above the given material on the same models, under a
//     high sun (rows 1-2) and a low evening sun (rows 3-4).
//   Looks(png, folder): every prefab in a Toon Farm Pack animal folder ("Cows", "Sheep"…), in file order, 6 per row.
//   unity command script run --file AgentScripts/Animals/AnimalLookSheet.cs --entry AnimalLookSheet.Looks --args '["<png>","Cows"]'
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AnimalLookSheet
{
    const string Tfp = "Assets/_StoreAssets/Env/SICS/Toon Series/Toon Farm Pack/Prefabs/Animals/";
    const int Cell = 384;

    static readonly string[] Herd =
    {
        "Cows/TFP_Cow_01A", "Sheep/TFP_Sheep_01A", "Pigs/TFP_Pig_01A", "Chicken/TFP_Chicken_01A", "Chicken/TFP_Rooster_01A",
    };

    public static string Compare(string png, string materialPath)
    {
        var other = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (other == null)
            return "no material at " + materialPath;
        var cells = new List<Color32[]>();
        foreach (bool evening in new[] { false, true })
            foreach (Material material in new[] { null, other })
                foreach (string animal in Herd)
                    cells.Add(Render(Tfp + animal + ".prefab", material, evening));
        Save(png, cells, Herd.Length);
        return $"ok {png}: rows toon day, {other.name} day, toon evening, {other.name} evening; columns {string.Join(", ", Herd.Select(Path.GetFileName))}";
    }

    public static string Looks(string png, string folder)
    {
        string[] prefabs = Directory.GetFiles(Tfp + folder, "*.prefab").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
        Save(png, prefabs.Select(p => Render(p, null, false)).ToList(), 6);
        return $"ok {png}: {string.Join(", ", prefabs.Select(Path.GetFileNameWithoutExtension))}";
    }

    // Poses: each spec "<prefab path>|<clip file>#<clip name>|<time s>", 6 per row, e.g. to see what a clip shows.
    public static string Poses(string png, string[] specs)
    {
        var cells = new List<Color32[]>();
        foreach (string spec in specs)
        {
            string[] parts = spec.Split('|');
            string[] clipParts = parts[1].Split('#');
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipParts[0]).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipParts[1]);
            if (clip == null)
                return "no clip " + parts[1];
            cells.Add(Render(parts[0], null, false, clip, float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
        }
        Save(png, cells, 6);
        return "ok " + png;
    }

    static Color32[] Render(string prefabPath, Material material, bool evening, AnimationClip clip = null, float time = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var preview = new PreviewRenderUtility();
        try
        {
            GameObject instance = preview.InstantiatePrefabInScene(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, -35f, 0f));
            if (clip != null)
                clip.SampleAnimation(instance.GetComponentInChildren<Animator>().gameObject, time);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (material != null)
                foreach (Renderer r in renderers)
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);

            Camera camera = preview.camera;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = evening ? new Color(0.36f, 0.3f, 0.32f) : new Color(0.55f, 0.66f, 0.78f);
            float radius = Mathf.Max(bounds.extents.magnitude * 0.6f, 0.3f);
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = bounds.center + new Vector3(0f, radius * 0.35f, distance);
            camera.transform.LookAt(bounds.center);

            Light sun = preview.lights[0];
            sun.intensity = evening ? 0.9f : 1.4f;
            sun.color = evening ? new Color(1f, 0.62f, 0.4f) : new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = evening ? Quaternion.Euler(12f, 120f, 0f) : Quaternion.Euler(50f, 150f, 0f);
            preview.lights[1].intensity = 0.25f;
            preview.ambientColor = evening ? new Color(0.25f, 0.22f, 0.3f) : new Color(0.45f, 0.5f, 0.58f);

            preview.BeginStaticPreview(new Rect(0, 0, Cell, Cell));
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            Color32[] pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            return pixels;
        }
        finally
        {
            preview.Cleanup();
        }
    }

    static void Save(string png, List<Color32[]> cells, int columns)
    {
        int rows = (cells.Count + columns - 1) / columns;
        var sheet = new Texture2D(columns * Cell, rows * Cell, TextureFormat.RGBA32, false);
        sheet.SetPixels32(Enumerable.Repeat(new Color32(20, 20, 20, 255), columns * Cell * rows * Cell).ToArray());
        for (int i = 0; i < cells.Count; i++)
            // Texture rows go bottom-up: the first cell is top-left.
            sheet.SetPixels32(i % columns * Cell, (rows - 1 - i / columns) * Cell, Cell, Cell, cells[i]);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(png)));
        File.WriteAllBytes(png, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
    }
}
