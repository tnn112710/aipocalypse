// Import settings for the clips our BroAudio entities use (edit mode, `unity command script run`). Safe to re-run.
// Short clips preload with the scene, so their first play doesn't load from disk (~11 ms); long beds stream.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AudioPreload
{
    const float StreamAboveSeconds = 60f;

    public static string Apply(bool dryRun = false)
    {
        var sb = new StringBuilder();
        var clips = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Game/Data/Audio" })
            .SelectMany(g => AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(g), false))
            .Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(AudioClip))
            .Distinct().OrderBy(p => p).ToList();
        int changed = 0;
        foreach (string path in clips)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            bool stream = clip.length > StreamAboveSeconds;
            // Compares against the .meta on disk: importer objects keep in-memory edits between runs.
            string meta = System.IO.File.ReadAllText(path + ".meta");
            bool wrong = meta.Contains(stream ? "preloadAudioData: 1" : "preloadAudioData: 0") || (stream ? !meta.Contains("loadType: 2") : meta.Contains("loadType: 2"));
            if (!wrong) continue;
            changed++;
            sb.AppendLine($"  {(stream ? "stream " : "preload")} {clip.length,6:F1}s  {path}");
            if (dryRun) continue;
            importer.defaultSampleSettings = Fixed(importer.defaultSampleSettings, stream);
            foreach (string platform in new[] { "Standalone" })
                if (importer.ContainsSampleSettingsOverride(platform))
                    importer.SetOverrideSampleSettings(platform, Fixed(importer.GetOverrideSampleSettings(platform), stream));
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
        return $"{clips.Count} clips used by entities, {changed} {(dryRun ? "would change" : "changed")}\n{sb}";
    }

    static AudioImporterSampleSettings Fixed(AudioImporterSampleSettings s, bool stream)
    {
        s.loadType = stream ? AudioClipLoadType.Streaming : s.loadType == AudioClipLoadType.Streaming ? AudioClipLoadType.DecompressOnLoad : s.loadType;
        s.preloadAudioData = !stream;
        return s;
    }
}
