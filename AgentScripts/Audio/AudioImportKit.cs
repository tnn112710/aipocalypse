// Length-based AudioClip import presets for any Unity 6 project. Run with the Unity CLI (com.unity.pipeline):
//   unity command script run --file "<abs path>/AudioImportKit.cs" --entry AudioImportKit.<Entry> --args '[...]'
// or copy it into an Editor folder (with templates/AudioImportHooks.cs for import-time presets and menu items).
// Entries: CreatePresets, Audit, Apply. Scope = ';'-separated folders or clip paths (every AudioClip inside), or
// "used:<folder or asset>" for the clips those assets (scenes, prefabs, ScriptableObjects, audio libraries) depend on.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

public static class AudioImportKit
{
    public const string DefaultPresetFolder = "Assets/Settings/AudioImport";
    const string ConfigName = "AudioImport.json";
    const int Batch = 100;

    [Serializable]
    public sealed class Config
    {
        [Tooltip("Clips up to this length (s) get the Sfx preset.")]
        public float sfxMaxSeconds = 2f;
        [Tooltip("Clips up to this length (s) get the Medium preset; longer ones Stream.")]
        public float mediumMaxSeconds = 60f;
        [Tooltip("A clip this close (s) to a threshold keeps the neighbouring preset it already has, so re-encoding (which can shift the length by a few samples) never flips it back and forth.")]
        public float boundarySeconds = 0.15f;
    }

    sealed class Profile
    {
        public string Name;
        public AudioClipLoadType LoadType;
        public AudioCompressionFormat Format;
        public float Quality = 1f;
        public bool Mono, Preload = true;
        public string PathIn(string folder) => $"{folder}/AudioImport_{Name}.preset";
    }

    // Defaults the presets are created from; after that the preset assets are the source of truth (tune them in the Inspector).
    static readonly Profile[] Profiles =
    {
        // PCM in memory after load: no decode when played. ADPCM keeps the build small and decompresses fast at load.
        new() { Name = "Sfx", LoadType = AudioClipLoadType.DecompressOnLoad, Format = AudioCompressionFormat.ADPCM, Mono = true },
        // Stays Vorbis in memory (~10x smaller than PCM), decoded while playing.
        new() { Name = "Medium", LoadType = AudioClipLoadType.CompressedInMemory, Format = AudioCompressionFormat.Vorbis, Quality = 0.7f },
        // Read from disk while playing: ~200 KB per playing instance whatever the length.
        new() { Name = "Stream", LoadType = AudioClipLoadType.Streaming, Format = AudioCompressionFormat.Vorbis, Quality = 0.6f, Preload = false },
    };

    // ---------------- entries ----------------

    // Creates the three presets and AudioImport.json in `presetFolder`. Existing ones are kept unless `overwrite`.
    public static string CreatePresets(string presetFolder = DefaultPresetFolder, bool overwrite = false)
    {
        Directory.CreateDirectory(presetFolder);
        var sb = new StringBuilder();
        string configPath = $"{presetFolder}/{ConfigName}";
        if (overwrite || !File.Exists(configPath))
        {
            File.WriteAllText(configPath, JsonUtility.ToJson(new Config(), true));
            AssetDatabase.ImportAsset(configPath);
            sb.AppendLine($"{configPath}: thresholds {JsonUtility.ToJson(new Config())}");
        }
        var todo = Profiles.Where(p => overwrite || AssetDatabase.LoadAssetAtPath<Preset>(p.PathIn(presetFolder)) == null).ToArray();
        if (todo.Length == 0) return sb.Append("presets exist (overwrite=true to rebuild)").ToString();

        string source = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        if (source == null) return "no AudioClip in the project to build the presets from";
        string temp = "Assets/_AudioImportKitTemp" + Path.GetExtension(source);
        AssetDatabase.CopyAsset(source, temp);
        try
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(temp);
            foreach (var p in todo)
            {
                foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL", "PS4", "PS5", "XboxOne", "GameCoreXboxSeries", "Switch" })
                    importer.ClearSampleSettingOverride(platform);
                importer.forceToMono = p.Mono;
                importer.loadInBackground = false;
                importer.ambisonic = false;
                var s = importer.defaultSampleSettings;
                s.loadType = p.LoadType;
                s.compressionFormat = p.Format;
                s.quality = p.Quality;
                s.preloadAudioData = p.Preload;
                // Sample rate settings only apply to PCM / ADPCM; Optimize picks the rate from the highest frequency present.
                s.sampleRateSetting = p.Format == AudioCompressionFormat.Vorbis ? AudioSampleRateSetting.PreserveSampleRate : AudioSampleRateSetting.OptimizeSampleRate;
                importer.defaultSampleSettings = s;
                var preset = new Preset(importer)
                {
                    // Per-asset data a preset must never overwrite.
                    excludedProperties = new[] { "m_UserData", "m_AssetBundleName", "m_AssetBundleVariant", "m_ExternalObjects" },
                };
                AssetDatabase.DeleteAsset(p.PathIn(presetFolder));
                AssetDatabase.CreateAsset(preset, p.PathIn(presetFolder));
                sb.AppendLine($"{p.PathIn(presetFolder)}: {p.LoadType} {p.Format} q{p.Quality} mono {p.Mono} preload {p.Preload}");
            }
        }
        finally
        {
            AssetDatabase.DeleteAsset(temp);
        }
        return sb.ToString();
    }

    // Read-only report: settings mix, estimated runtime memory and imported size, and the usual problems.
    public static string Audit(string scope, string presetFolder = DefaultPresetFolder, int listMax = 40)
    {
        var paths = Resolve(scope);
        var presets = LoadPresets(presetFolder);
        var cfg = LoadConfig(presetFolder);
        var mix = new Dictionary<string, (int n, long mem, long size)>();
        var flags = new Dictionary<string, List<string>>();
        void Flag(string what, string path)
        {
            if (!flags.TryGetValue(what, out var l)) flags[what] = l = new List<string>();
            l.Add(path);
        }
        long memTotal = 0, sizeTotal = 0;
        int mismatched = 0;
        ForEachBatch(paths, false, (path, importer, clip) =>
        {
            var s = Effective(importer);
            long mem = Memory(s.loadType, importer, clip), size = ImportedSize(importer);
            memTotal += mem; sizeTotal += size;
            string key = $"{s.loadType} {s.compressionFormat}{(s.compressionFormat == AudioCompressionFormat.Vorbis ? $" q{s.quality:0.##}" : "")} {(importer.forceToMono ? "mono" : clip.channels + "ch")}{(s.preloadAudioData ? "" : " no-preload")}{(importer.loadInBackground ? " bg" : "")}";
            var e = mix.GetValueOrDefault(key);
            mix[key] = (e.n + 1, e.mem + mem, e.size + size);
            if (s.loadType == AudioClipLoadType.DecompressOnLoad && mem > 1024 * 1024) Flag("Decompress On Load over 1 MB in memory", path);
            if (s.loadType != AudioClipLoadType.Streaming && !s.preloadAudioData) Flag("Preload off (loads on first play: a main-thread hitch)", path);
            if (s.loadType == AudioClipLoadType.Streaming && clip.length < 10f) Flag("Streaming a short clip (~200 KB and a stream open per play)", path);
            if (s.loadType != AudioClipLoadType.Streaming && clip.length > cfg.mediumMaxSeconds) Flag($"Long clip (> {cfg.mediumMaxSeconds} s) not streamed", path);
            if (clip.channels > 1 && clip.length <= cfg.sfxMaxSeconds && !importer.forceToMono) Flag("Short stereo clip (mono halves memory; stereo is lost on 3D sources anyway)", path);
            if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) Flag("MP3 source (lossy, re-encoded again on import)", path);
            if (importer.ContainsSampleSettingsOverride("Standalone")) Flag("Has a Standalone override (presets replace it)", path);
            if (presets != null && !presets[Pick(clip.length, importer, presets, cfg)].DataEquals(importer)) mismatched++;
        });
        var sb = new StringBuilder();
        sb.AppendLine($"{paths.Count} clips; est. memory {Mb(memTotal)}, imported {Mb(sizeTotal)}" +
                      (presets == null ? $"; no presets in {presetFolder}" : $"; {mismatched} don't match their preset"));
        foreach (var kv in mix.OrderByDescending(kv => kv.Value.mem))
            sb.AppendLine($"  {kv.Value.n,5} x {kv.Key,-44} memory {Mb(kv.Value.mem),9}  imported {Mb(kv.Value.size),9}");
        foreach (var kv in flags)
        {
            sb.AppendLine($"! {kv.Key}: {kv.Value.Count}");
            foreach (var p in kv.Value.Take(listMax)) sb.AppendLine($"    {p}");
            if (kv.Value.Count > listMax) sb.AppendLine($"    … {kv.Value.Count - listMax} more");
        }
        return sb.ToString();
    }

    // Gives each clip in `scope` the preset for its length (Preset.ApplyTo + reimport, batched). Clips that already match
    // are skipped, so re-running is free. `max` > 0 limits the run (to time a sample); `verbose` lists each change.
    public static string Apply(string scope, bool dryRun = false, string presetFolder = DefaultPresetFolder, int max = 0, bool verbose = false)
    {
        var presets = LoadPresets(presetFolder);
        if (presets == null) return $"no presets in {presetFolder}: run CreatePresets first";
        var cfg = LoadConfig(presetFolder);
        var all = Resolve(scope);
        var paths = max > 0 ? all.Take(max).ToList() : all;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var counts = new Dictionary<string, int>();
        var lines = new StringBuilder();
        int changed = 0;
        long memBefore = 0, sizeBefore = 0;
        ForEachBatch(paths, !dryRun, (path, importer, clip) =>
        {
            var profile = Pick(clip.length, importer, presets, cfg);
            counts[profile.Name] = counts.GetValueOrDefault(profile.Name) + 1;
            memBefore += Memory(Effective(importer).loadType, importer, clip);
            sizeBefore += ImportedSize(importer);
            if (presets[profile].DataEquals(importer)) return;
            changed++;
            if (verbose && changed <= 500) lines.AppendLine($"  {profile.Name,-6} {clip.length,7:F2}s {clip.channels}ch  was {Describe(importer)}  {path}");
            if (dryRun) return;
            presets[profile].ApplyTo(importer);
            importer.SaveAndReimport(); // deferred to the batch's StopAssetEditing
        });
        string after = "";
        if (!dryRun && changed > 0)
        {
            long memAfter = 0, sizeAfter = 0;
            ForEachBatch(paths, false, (_, importer, clip) =>
            {
                memAfter += Memory(Effective(importer).loadType, importer, clip);
                sizeAfter += ImportedSize(importer);
            });
            after = $"; memory {Mb(memBefore)} -> {Mb(memAfter)}, imported {Mb(sizeBefore)} -> {Mb(sizeAfter)}";
        }
        return $"{paths.Count} of {all.Count} clips ({string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"))}), " +
               $"{(dryRun ? "would change" : "changed")} {changed}{after}; {sw.Elapsed.TotalSeconds:F0} s\n{lines}";
    }

    // ---------------- helpers ----------------

    // Below a threshold → that preset; but near one, a clip that already has the neighbouring preset keeps it.
    static Profile Pick(float length, AudioImporter importer, Dictionary<Profile, Preset> presets, Config cfg)
    {
        var p = length <= cfg.sfxMaxSeconds ? Profiles[0] : length <= cfg.mediumMaxSeconds ? Profiles[1] : Profiles[2];
        bool nearSfx = Mathf.Abs(length - cfg.sfxMaxSeconds) < cfg.boundarySeconds;
        bool nearMedium = Mathf.Abs(length - cfg.mediumMaxSeconds) < cfg.boundarySeconds;
        if (nearSfx && p != Profiles[2] && presets[Profiles[p == Profiles[0] ? 1 : 0]].DataEquals(importer)) return Profiles[p == Profiles[0] ? 1 : 0];
        if (nearMedium && p != Profiles[0] && presets[Profiles[p == Profiles[1] ? 2 : 1]].DataEquals(importer)) return Profiles[p == Profiles[1] ? 2 : 1];
        return p;
    }

    public static List<string> Resolve(string scope)
    {
        var result = new HashSet<string>();
        foreach (var raw in scope.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (raw.StartsWith("used:"))
            {
                string root = raw.Substring(5).Trim();
                var roots = AssetDatabase.IsValidFolder(root)
                    ? AssetDatabase.FindAssets("", new[] { root }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)).ToArray()
                    : new[] { root };
                foreach (var p in AssetDatabase.GetDependencies(roots, true))
                    if (AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(AudioClip)) result.Add(p);
            }
            else if (AssetDatabase.IsValidFolder(raw))
            {
                foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { raw })) result.Add(AssetDatabase.GUIDToAssetPath(g));
            }
            else if (AssetDatabase.GetMainAssetTypeAtPath(raw) == typeof(AudioClip))
            {
                result.Add(raw);
            }
        }
        return result.Where(p => p.StartsWith("Assets/")).OrderBy(p => p).ToList(); // packages are read-only
    }

    static void ForEachBatch(List<string> paths, bool editing, Action<string, AudioImporter, AudioClip> each)
    {
        for (int start = 0; start < paths.Count; start += Batch)
        {
            if (editing) AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths.Skip(start).Take(Batch))
                {
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (importer != null && clip != null) each(path, importer, clip);
                }
            }
            finally
            {
                if (editing) AssetDatabase.StopAssetEditing();
            }
            if (paths.Count > Batch) EditorUtility.UnloadUnusedAssetsImmediate();
        }
    }

    static Dictionary<Profile, Preset> LoadPresets(string folder)
    {
        var d = Profiles.ToDictionary(p => p, p => AssetDatabase.LoadAssetAtPath<Preset>(p.PathIn(folder)));
        return d.Values.Any(p => p == null) ? null : d;
    }

    static Config LoadConfig(string folder)
    {
        string path = $"{folder}/{ConfigName}";
        return File.Exists(path) ? JsonUtility.FromJson<Config>(File.ReadAllText(path)) : new Config();
    }

    static AudioImporterSampleSettings Effective(AudioImporter importer) =>
        importer.ContainsSampleSettingsOverride("Standalone") ? importer.GetOverrideSampleSettings("Standalone") : importer.defaultSampleSettings;

    static string Describe(AudioImporter importer)
    {
        var s = Effective(importer);
        return $"{s.loadType} {s.compressionFormat} q{s.quality:0.##} mono {importer.forceToMono} preload {s.preloadAudioData}";
    }

    static long Memory(AudioClipLoadType type, AudioImporter importer, AudioClip clip) => type switch
    {
        AudioClipLoadType.DecompressOnLoad => (long)clip.samples * clip.channels * 2,
        AudioClipLoadType.CompressedInMemory => ImportedSize(importer),
        _ => 200 * 1024, // the manual's per-stream overhead
    };

    // The imported size the Inspector shows (internal property).
    static long ImportedSize(AudioImporter importer) =>
        Convert.ToInt64(typeof(AudioImporter).GetProperty("compSize", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(importer) ?? 0);

    static string Mb(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024f * 1024f):F1} MB" : $"{bytes / 1024} KB";
}
