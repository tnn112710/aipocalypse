using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>One lazy source request in a sequential preparation batch. Source readers run on the main thread.</summary>
    public sealed class TextStudioFontAppearanceRequest
    {
        public string OutputPath { get; }
        public string SourceName { get; }
        public int FaceIndex { get; }
        public Object NativeFont { get; }
        internal readonly bool HasNativeFont;
        internal readonly Func<byte[]> ReadSource;
        internal readonly uint[] GlyphIds;
        internal readonly TextStudioFontAppearanceTierRequest[] Tiers;

        public TextStudioFontAppearanceRequest(Func<byte[]> readSource, int faceIndex, string outputPath,
            IEnumerable<uint> glyphIds, string sourceName = "Font", Object nativeFont = null,
            IReadOnlyList<TextStudioFontAppearanceTierRequest> tiers = null)
        {
            ReadSource = readSource ?? throw new ArgumentNullException(nameof(readSource));
            if (faceIndex < 0) throw new ArgumentOutOfRangeException(nameof(faceIndex));
            if (glyphIds == null) throw new ArgumentNullException(nameof(glyphIds));
            GlyphIds = glyphIds.Distinct().OrderBy(id => id).ToArray();
            Tiers = TextStudioFontAppearanceTierRequest.Snapshot(tiers);
            if (GlyphIds.Length == 0 || GlyphIds.Length > 65535) throw new ArgumentException("Prepare between 1 and 65,535 shaped glyph IDs per font; the texture budget still applies.", nameof(glyphIds));
            FaceIndex = faceIndex; OutputPath = outputPath; SourceName = sourceName;
            NativeFont = nativeFont; HasNativeFont = nativeFont != null;
        }
    }

    public static partial class TextStudioFontAppearancePreparation
    {
        static Batch s_batch;

        /// <summary>Prepares one source at a time, publishes each completed font, and stops on cancellation or failure.</summary>
        public static bool StartBatch(IEnumerable<TextStudioFontAppearanceRequest> requests, Action<TextStudioFontAppearanceData> completed = null)
        {
            if (IsBusy) { LastError = "A font preparation job is already running."; return false; }
            try
            {
                if (requests == null) throw new ArgumentNullException(nameof(requests));
                TextStudioFontAppearanceRequest[] items = requests.ToArray();
                if (items.Length == 0) throw new ArgumentException("No visible font glyphs were found in this text.");
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (TextStudioFontAppearanceRequest item in items)
                {
                    if (item == null) throw new ArgumentException("A font preparation request is missing.");
                    ValidateOutputPath(item.OutputPath);
                    if (!paths.Add(item.OutputPath)) throw new ArgumentException("Each font in a preparation batch needs a separate output asset.");
                }
                ResetProgress(items.Length);
                s_batch = new Batch(items, completed);
                if (StartNextBatchItem()) return true;
                s_batch = null;
                return false;
            }
            catch (Exception exception) { LastError = exception.Message; s_batch = null; return false; }
        }

        /// <summary>Collects actual primary/fallback glyphs and assigns completed resources to this text with Undo support.</summary>
        public static bool StartForText(TextStudioText hub, bool includeFontInventory = false, TMP_FontAsset onlyFont = null,
            string outputFolder = TextStudioAppearancePreparationProvider.GeneratedFolder, float pixelsPerEm = 256, float safeDistanceEm = .3f)
        {
            if (IsBusy) { LastError = "A font preparation job is already running."; return false; }
            try
            {
                List<TextStudioFontAppearanceRequest> requests = CreateTextRequests(hub, includeFontInventory, onlyFont, outputFolder, TextStudioFontAppearanceTierRequest.Adaptive(pixelsPerEm, safeDistanceEm));
                return StartBatch(requests, data =>
                {
                    if (hub == null) return;
                    Undo.RecordObject(hub, "Assign Prepared Font Appearance");
                    hub.SetAppearanceFont(data);
                    EditorUtility.SetDirty(hub);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hub);
                    if (!Application.isPlaying && hub.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(hub.gameObject.scene)) EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
                });
            }
            catch (Exception exception) { LastError = exception.Message; return false; }
        }

        internal static List<TextStudioFontAppearanceRequest> CreateTextRequests(TextStudioText hub, bool inventory, TMP_FontAsset onlyFont, string outputFolder, IReadOnlyList<TextStudioFontAppearanceTierRequest> tiers = null)
        {
            if (hub == null || hub.TMPText == null) throw new ArgumentException("Font preparation requires a supported text backend.");
            Dictionary<TMP_FontAsset, HashSet<uint>> fonts = TextStudioPreparationCoverage.Collect(hub, onlyFont);
            if (inventory && onlyFont != null && !fonts.ContainsKey(onlyFont)) fonts.Add(onlyFont, new HashSet<uint>());
            List<TextStudioFontAppearanceRequest> requests = new List<TextStudioFontAppearanceRequest>(fonts.Count);
            HashSet<string> plannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<TMP_FontAsset, HashSet<uint>> item in fonts)
            {
                TMP_FontAsset font = item.Key;
                TextStudioFontAppearanceData current = hub.FindAppearanceFont(font);
                string path = current != null ? AssetDatabase.GetAssetPath(current) : string.Empty;
                if (string.IsNullOrEmpty(path))
                {
                    string name = font.name;
                    foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
                    string stem = outputFolder.TrimEnd('/') + "/" + name + " Appearance";
                    path = ReserveOutputPath(stem, plannedPaths);
                }
                plannedPaths.Add(path);
                requests.Add(new TextStudioFontAppearanceRequest(() => ReadFontSource(font, current), ResolveFaceIndex(font), path,
                    inventory ? item.Value.Concat(font.glyphTable.Select(glyph => glyph.index)) : item.Value, font.name, font, tiers));
            }
            return requests;
        }

        internal static string ReserveOutputPath(string stem, HashSet<string> plannedPaths)
        {
            // GenerateUniqueAssetPath returns an empty path for an unimported parent folder on supported Editors.
            // Resolve planned and existing names without creating a directory before successful preparation.
            for (int suffix = 0; suffix < 4096; suffix++)
            {
                string candidate = stem + (suffix == 0 ? string.Empty : " " + suffix) + ".asset";
                ValidateOutputPath(candidate);
                if (plannedPaths.Contains(candidate) || File.Exists(candidate) || Directory.Exists(candidate) ||
                    !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(candidate, AssetPathToGUIDOptions.OnlyExistingAssets))) continue;
                return candidate;
            }
            throw new IOException("No available font appearance filename was found. Choose a different output folder.");
        }

        static byte[] ReadFontSource(TMP_FontAsset font, TextStudioFontAppearanceData fallback = null)
        {
            if (font == null) throw new InvalidOperationException("The font was removed before preparation.");
            string path = ResolveSourcePath(font);
            if (string.IsNullOrEmpty(path) && fallback != null && fallback.ValidateSource(out _)) return fallback.CopySourceBytes();
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("The original font file is unavailable. Restore its source asset before preparing appearance data.");
            return File.ReadAllBytes(path);
        }

        static void ResetProgress(int count)
        {
            CompletedFontCount = 0; RequestedFontCount = count;
            LastResult = null; LastError = string.Empty;
        }

        static void ValidateOutputPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                Path.GetExtension(path) != ".asset" || path.Split('/').Contains("..") || path.Contains('\\'))
                throw new ArgumentException("Choose an appearance data asset inside this project's Assets folder.");
        }

        static bool StartNextBatchItem()
        {
            TextStudioFontAppearanceRequest request = s_batch.Items[s_batch.Index];
            try
            {
                if (request.HasNativeFont && request.NativeFont == null) throw new InvalidOperationException("The font was removed before preparation.");
                return StartCore(request.ReadSource(), request.FaceIndex, request.OutputPath, request.GlyphIds,
                    request.SourceName, request.NativeFont, request.ReadSource, s_batch.Completed, request.Tiers);
            }
            catch (Exception exception) { LastError = exception.Message; return false; }
        }

        static void AdvanceBatch(bool succeeded)
        {
            if (s_batch == null) return;
            if (!succeeded || s_batch.Canceled)
            {
                if (s_batch.Canceled) LastError = "Font preparation was canceled.";
                s_batch = null;
                return;
            }
            if (++s_batch.Index == s_batch.Items.Length) { s_batch = null; return; }
            if (StartNextBatchItem()) return;
            s_batch = null;
            UnityEngine.Debug.LogError("[Text Studio] " + LastError);
        }

        sealed class Batch
        {
            internal readonly TextStudioFontAppearanceRequest[] Items;
            internal readonly Action<TextStudioFontAppearanceData> Completed;
            internal int Index;
            internal bool Canceled;
            internal Batch(TextStudioFontAppearanceRequest[] items, Action<TextStudioFontAppearanceData> completed)
            { Items = items; Completed = completed; }
        }
    }
}
