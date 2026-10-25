// Import presets for the clips our BroAudio entities use (edit mode, `unity command script run`). Safe to re-run.
// Each clip gets the preset for its length: Sfx (short, frequent), Medium (occasional), Stream (music, ambience beds).
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

public static class AudioImport
{
    public const string PresetFolder = "Assets/_Game/Settings/Audio";
    const string EntityFolder = "Assets/_Game/Data/Audio";
    const float SfxMaxSeconds = 2f;
    const float MediumMaxSeconds = 60f;

    sealed class Profile
    {
        public string Name;
        public AudioClipLoadType LoadType;
        public AudioCompressionFormat Format;
        public float Quality = 1f;
        public bool Mono, Preload = true;
        public string Path => $"{PresetFolder}/AudioImport_{Name}.preset";
    }

    static readonly Profile[] Profiles =
    {
        // Decompressed to PCM at load, so playing costs no decode; ADPCM keeps the build small and the load fast.
        new() { Name = "Sfx", LoadType = AudioClipLoadType.DecompressOnLoad, Format = AudioCompressionFormat.ADPCM, Mono = true },
        // Stays Vorbis in memory (~10x smaller than PCM) and decodes while playing.
        new() { Name = "Medium", LoadType = AudioClipLoadType.CompressedInMemory, Format = AudioCompressionFormat.Vorbis, Quality = 0.7f },
        new() { Name = "Stream", LoadType = AudioClipLoadType.Streaming, Format = AudioCompressionFormat.Vorbis, Quality = 0.6f, Preload = false },
    };

    static Profile For(AudioClip clip) => clip.length <= SfxMaxSeconds ? Profiles[0] : clip.length <= MediumMaxSeconds ? Profiles[1] : Profiles[2];

    // Writes the preset assets from the table above (overwrites them). The presets can also be applied by hand from the
    // importer's preset button; Apply uses whatever the assets hold, so tuning them in the Inspector is enough.
    public static string CreatePresets()
    {
        System.IO.Directory.CreateDirectory(PresetFolder);
        var source = EntityClips().First();
        const string temp = "Assets/_AudioImportTemp.wav";
        AssetDatabase.CopyAsset(source, temp);
        var sb = new StringBuilder();
        try
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(temp);
            foreach (var p in Profiles)
            {
                foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                    importer.ClearSampleSettingOverride(platform);
                importer.forceToMono = p.Mono;
                importer.loadInBackground = false;
                importer.ambisonic = false;
                var s = importer.defaultSampleSettings;
                s.loadType = p.LoadType;
                s.compressionFormat = p.Format;
                s.quality = p.Quality;
                s.preloadAudioData = p.Preload;
                s.sampleRateSetting = p.Format == AudioCompressionFormat.Vorbis ? AudioSampleRateSetting.PreserveSampleRate : AudioSampleRateSetting.OptimizeSampleRate;
                importer.defaultSampleSettings = s;
                var preset = new Preset(importer)
                {
                    excludedProperties = new[] { "m_UserData", "m_AssetBundleName", "m_AssetBundleVariant", "m_ExternalObjects" },
                };
                AssetDatabase.DeleteAsset(p.Path);
                AssetDatabase.CreateAsset(preset, p.Path);
                sb.AppendLine($"{p.Path}: {p.LoadType} {p.Format} q{p.Quality} mono {p.Mono} preload {p.Preload}");
            }
        }
        finally
        {
            AssetDatabase.DeleteAsset(temp);
        }
        return sb.ToString();
    }

    // Applies each entity clip's preset. Reports runtime memory (PCM when decompressed, imported size when compressed,
    // the stream buffer when streamed) and imported size before and after.
    public static string Apply(bool dryRun = false)
    {
        var presets = Profiles.ToDictionary(p => p, p => AssetDatabase.LoadAssetAtPath<Preset>(p.Path));
        if (presets.Values.Any(p => p == null)) return "presets missing: run AudioImport.CreatePresets first";
        var sb = new StringBuilder();
        var clips = EntityClips();
        long memBefore = 0, memAfter = 0, sizeBefore = 0, sizeAfter = 0;
        var counts = new Dictionary<string, int>();
        foreach (string path in clips)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var profile = For(clip);
            var preset = presets[profile];
            counts[profile.Name] = counts.GetValueOrDefault(profile.Name) + 1;
            long mem0 = Memory(importer, clip), size0 = ImportedSize(importer);
            memBefore += mem0; sizeBefore += size0;
            bool matches = preset.DataEquals(importer);
            if (dryRun || matches)
            {
                memAfter += mem0; sizeAfter += size0;
                if (!matches) sb.AppendLine($"  {profile.Name,-6} {clip.length,6:F1}s {clip.channels}ch  {Settings(importer)}  {path}");
                continue;
            }
            preset.ApplyTo(importer);
            importer.SaveAndReimport();
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            long mem1 = Memory(importer, clip), size1 = ImportedSize(importer);
            memAfter += mem1; sizeAfter += size1;
            sb.AppendLine($"  {profile.Name,-6} {clip.length,6:F1}s  mem {mem0 / 1024,5} -> {mem1 / 1024,5} KB  imported {size0 / 1024,5} -> {size1 / 1024,5} KB  {path}");
        }
        string totals = $"memory {memBefore / 1024} -> {memAfter / 1024} KB, imported {sizeBefore / 1024} -> {sizeAfter / 1024} KB";
        return $"{clips.Count} clips used by entities ({string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"))}), " +
               $"{(dryRun ? "would change" : "changed")} {sb.ToString().Split('\n').Length - 1}{(dryRun ? "" : $"; {totals}")}\n{sb}";
    }

    // Every clip under `folder` (vendor packs not used yet), reimported in batches; reports totals only.
    // `max` > 0 limits the run to the first clips, to time a sample before a full run.
    public static string ApplyFolder(string folder, bool dryRun = false, int max = 0)
    {
        var presets = Profiles.ToDictionary(p => p, p => AssetDatabase.LoadAssetAtPath<Preset>(p.Path));
        if (presets.Values.Any(p => p == null)) return "presets missing: run AudioImport.CreatePresets first";
        var all = AssetDatabase.FindAssets("t:AudioClip", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToList();
        var paths = max > 0 ? all.Take(max).ToList() : all;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var counts = new Dictionary<string, int>();
        var changed = new List<string>();
        long sizeBefore = 0;
        const int batch = 100;
        for (int start = 0; start < paths.Count; start += batch)
        {
            var chunk = paths.Skip(start).Take(batch).ToList();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in chunk)
                {
                    var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (importer == null || clip == null) continue;
                    var profile = For(clip);
                    counts[profile.Name] = counts.GetValueOrDefault(profile.Name) + 1;
                    sizeBefore += ImportedSize(importer);
                    if (presets[profile].DataEquals(importer)) continue;
                    changed.Add(path);
                    if (dryRun) continue;
                    presets[profile].ApplyTo(importer);
                    importer.SaveAndReimport(); // deferred to StopAssetEditing
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            EditorUtility.UnloadUnusedAssetsImmediate();
        }
        long sizeAfter = paths.Sum(p => AssetImporter.GetAtPath(p) is AudioImporter i ? ImportedSize(i) : 0);
        return $"{folder}: {paths.Count} of {all.Count} clips ({string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"))}), " +
               $"{(dryRun ? "would change" : "changed")} {changed.Count}; imported {sizeBefore / (1024 * 1024)} -> {sizeAfter / (1024 * 1024)} MB; {sw.Elapsed.TotalSeconds:F0} s";
    }

    static List<string> EntityClips() =>
        AssetDatabase.FindAssets("t:ScriptableObject", new[] { EntityFolder })
            .SelectMany(g => AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(g), false))
            .Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(AudioClip))
            .Distinct().OrderBy(p => p).ToList();

    static AudioImporterSampleSettings Effective(AudioImporter importer) =>
        importer.ContainsSampleSettingsOverride("Standalone") ? importer.GetOverrideSampleSettings("Standalone") : importer.defaultSampleSettings;

    static string Settings(AudioImporter importer)
    {
        var s = Effective(importer);
        return $"now {s.loadType} {s.compressionFormat} q{s.quality:F2} mono {importer.forceToMono} preload {s.preloadAudioData}";
    }

    static long Memory(AudioImporter importer, AudioClip clip) => Effective(importer).loadType switch
    {
        AudioClipLoadType.DecompressOnLoad => (long)clip.samples * clip.channels * 2,
        AudioClipLoadType.CompressedInMemory => ImportedSize(importer),
        _ => 200 * 1024, // the manual's per-stream overhead
    };

    // The imported size the Inspector shows (internal property).
    static long ImportedSize(AudioImporter importer) =>
        System.Convert.ToInt64(typeof(AudioImporter).GetProperty("compSize", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(importer) ?? 0);
}
