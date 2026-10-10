using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ImpossibleRobert.Common;
using TextStudio.Geometry;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>Owns one cancellable Editor preparation job. Native work and staging never run on the main thread.</summary>
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // The job is Editor-session state with explicit reload and quit ownership.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioFontAppearancePreparation
    {
        const string WindowsBakerGuid = "2aa9dc758c8b4eb3943909fcb39851fe";
        const string WindowsBakerHash = "fb57c34b04d2bce05bbcf6b3231f459922efc83f05ad64ef15d030b619378eb0";
        const int PageSize = 1024;
        const long MaximumTextureBytes = 64L * 1024 * 1024;
        static Job s_job;
        static WeakReference<TextStudioFontAppearanceData> s_lastResult;

        public static bool IsBusy => s_job != null || s_batch != null;
        public static int CompletedFontCount { get; private set; }
        public static int RequestedFontCount { get; private set; }
        public static string CurrentSourceName => s_job?.SourceName ?? string.Empty;
        public static string LastError { get; private set; } = string.Empty;
        public static TextStudioFontAppearanceData LastResult
        {
            get => s_lastResult != null && s_lastResult.TryGetTarget(out TextStudioFontAppearanceData data) ? data : null;
            private set => s_lastResult = value != null ? new WeakReference<TextStudioFontAppearanceData>(value) : null;
        }
        static float CurrentCompletion => s_job == null ? 0f : s_job.Total == 0 ? 0f : Mathf.Clamp01((float)s_job.Completed / s_job.Total);
        public static float Completion => !IsBusy ? 1f : Mathf.Clamp01((CompletedFontCount + CurrentCompletion) / Math.Max(1, RequestedFontCount));
        public static long EstimatedTextureBytes => s_job == null ? 0 : Interlocked.Read(ref s_job.PlannedBytes);

        static TextStudioFontAppearancePreparation()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static bool Start(TMP_FontAsset font, string outputPath, IEnumerable<uint> glyphIds = null,
            Action<TextStudioFontAppearanceData> completed = null, IReadOnlyList<TextStudioFontAppearanceTierRequest> tiers = null)
        {
            if (IsBusy) { LastError = "A font preparation job is already running."; return false; }
            try
            {
                if (font == null) throw new ArgumentNullException(nameof(font));
                string sourcePath = ResolveSourcePath(font);
                if (string.IsNullOrEmpty(sourcePath)) throw new InvalidOperationException("The original font file is unavailable. Restore its source asset before preparing appearance data.");
                return StartFromBytes(File.ReadAllBytes(sourcePath), ResolveFaceIndex(font), outputPath,
                    glyphIds ?? font.glyphTable.Select(glyph => glyph.index), font.name, font,
                    () => File.ReadAllBytes(ResolveSourcePath(font)), completed, tiers);
            }
            catch (Exception exception) { LastError = exception.Message; return false; }
        }

        /// <summary>Prepares caller-owned font bytes and shaped glyph IDs. The optional source reader runs on the main thread before publication to reject stale data.</summary>
        public static bool StartFromBytes(byte[] fontBytes, int faceIndex, string outputPath, IEnumerable<uint> glyphIds,
            string sourceName = "Font", Object nativeFont = null, Func<byte[]> readCurrentSource = null,
            Action<TextStudioFontAppearanceData> completed = null, IReadOnlyList<TextStudioFontAppearanceTierRequest> tiers = null)
        {
            if (IsBusy) { LastError = "A font preparation job is already running."; return false; }
            ResetProgress(1);
            return StartCore(fontBytes, faceIndex, outputPath, glyphIds, sourceName, nativeFont, readCurrentSource, completed, tiers);
        }

        static bool StartCore(byte[] fontBytes, int faceIndex, string outputPath, IEnumerable<uint> glyphIds,
            string sourceName, Object nativeFont, Func<byte[]> readCurrentSource, Action<TextStudioFontAppearanceData> completed,
            IReadOnlyList<TextStudioFontAppearanceTierRequest> tiers)
        {
            try
            {
                ValidateOutputPath(outputPath);
                TextStudioFontAppearanceTierRequest[] requestedTiers = TextStudioFontAppearanceTierRequest.Snapshot(tiers);
                if (fontBytes == null || fontBytes.Length == 0 || fontBytes.Length > 32 * 1024 * 1024)
                    throw new InvalidOperationException("The font source must fit within 32 MiB.");
                if (faceIndex < 0) throw new ArgumentOutOfRangeException(nameof(faceIndex));
                if (glyphIds == null) throw new ArgumentNullException(nameof(glyphIds));
                HashSet<uint> glyphs = new HashSet<uint>(glyphIds);
                TextStudioFontAppearanceData existing = AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(outputPath);
                if (existing != null && existing.FaceIndex == faceIndex && existing.SourceHash == Hash(fontBytes))
                    for (int tier = 0; tier < existing.TierCount; tier++)
                    {
                        TextStudioPreparedFieldTier previous = existing.GetTier(tier);
                        if (previous == null) continue;
                        for (int index = 0; index < previous.GlyphCount; index++) glyphs.Add(previous.GetGlyph(index).GlyphIndex);
                    }
                if (existing != null && existing.FaceIndex == faceIndex && existing.SourceHash == Hash(fontBytes))
                {
                    List<TextStudioFontAppearanceTierRequest> union = new List<TextStudioFontAppearanceTierRequest>(requestedTiers);
                    for (int tier = 0; tier < existing.TierCount; tier++)
                    {
                        TextStudioPreparedFieldTier previous = existing.GetTier(tier);
                        if (previous != null && !union.Any(request => request.PixelsPerEm == previous.PixelsPerEm && request.DistanceRangeEm == previous.DistanceRangeEm))
                            union.Add(new TextStudioFontAppearanceTierRequest(previous.PixelsPerEm, previous.DistanceRangeEm));
                    }
                    // Preserve every consumer's existing tier. Memory, rather than an arbitrary tier count,
                    // bounds the internal merged job; caller-supplied requests retain their public validation.
                    requestedTiers = union.OrderBy(tier => tier.PixelsPerEm).ThenBy(tier => tier.DistanceRangeEm).ToArray();
                }
                uint[] coverage = glyphs.OrderBy(value => value).ToArray();
                if (coverage.Length == 0 || coverage.Length > 65535) throw new InvalidOperationException("Prepare between 1 and 65,535 shaped glyph IDs per data asset. The texture budget still applies.");
                byte[] bytes = (byte[])fontBytes.Clone();
                string baker = ResolveBaker();
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TextStudioFieldPreparation"));
                string folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                Job job = new Job(nativeFont, faceIndex, sourceName, readCurrentSource, outputPath, folder, completed, requestedTiers);
                job.ProgressId = Progress.Start("Prepare Font Appearance", job.SourceName, Progress.Options.Managed);
                Progress.RegisterCancelCallback(job.ProgressId, () => { Cancel(); return true; });
                LastError = string.Empty;
                s_job = job;
                job.Task = Task.Run(() => Prepare(job, baker, bytes, coverage));
                EditorApplication.update += Update;
                return true;
            }
            catch (Exception exception) { LastError = exception.Message; return false; }
        }

        public static void Cancel()
        {
            if (s_batch != null) s_batch.Canceled = true;
            s_job?.Cancellation.Cancel();
        }

        internal static string ResolveSourcePath(TMP_FontAsset font)
        {
            string path = AssetDatabase.GetAssetPath(font.sourceFontFile);
            if (string.IsNullOrEmpty(path)) path = AssetDatabase.GUIDToAssetPath(font.creationSettings.sourceFontFileGUID);
            if (string.IsNullOrEmpty(path))
            {
                SerializedProperty source = new SerializedObject(font).FindProperty("m_SourceFontFileGUID");
                if (source != null) path = AssetDatabase.GUIDToAssetPath(source.stringValue);
            }
            return path;
        }

        internal static int ResolveFaceIndex(TMP_FontAsset font)
        {
            SerializedProperty face = new SerializedObject(font).FindProperty("m_FaceInfo.m_FaceIndex");
            return face != null ? face.intValue : font.creationSettings.faceIndex;
        }

        internal static string ResolveBaker()
        {
            string guid = WindowsBakerGuid, expectedHash = WindowsBakerHash;
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            if (Application.platform == RuntimePlatform.LinuxEditor)
            { guid = "f87eae60c3b248168b2e7811439a6076"; expectedHash = "1af38d6d34e1896034b4766f5e2447c077660bbcc7031dc8e41ad78e6e9545db"; }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                bool arm = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
                guid = arm ? "264d5f198a4a4662a0a0a4dd08daa385" : "c0c0be9c52be4bd7acf426d8d12eb74e";
                expectedHash = arm ? "d1a70f1e63dcb14db91fa8e2493ffd06a98204c2ab1bad8d28ce300eb3cac75d" : "21dfa11f59d2f0eebd387fa42f060141a5ac0aa43788e70b7d10da3a6704be42";
            }
            else if (!windows) throw new PlatformNotSupportedException("Font preparation requires a supported desktop Editor host.");
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (source == null) throw new InvalidOperationException("The bundled font appearance baker is missing. Reimport Text Studio's Editor files.");
            byte[] bytes = source.bytes;
            if (Hash(bytes) != expectedHash) throw new InvalidOperationException("The appearance baker does not match this version's pinned build.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TextStudioFieldPreparation/" + expectedHash));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, windows ? "TextStudioFieldBaker.exe" : "TextStudioFieldBaker");
            if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != expectedHash) File.WriteAllBytes(path, bytes);
            if (!windows && SetExecutablePermissions(path, 448) != 0)
                throw new IOException("The extracted font baker could not be made executable. Check permissions for this project's Library folder.");
            return path;
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        static extern int SetExecutablePermissions(string path, uint mode);

        internal static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        static BuildResult Prepare(Job job, string baker, byte[] bytes, uint[] coverage)
        {
            CancellationToken cancellation = job.Cancellation.Token;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                ICurveFontFace face = FontCurveSourceCache.GetOrCreate(bytes, job.FaceIndex);
                TextStudioFieldContourResolver contours = new TextStudioFieldContourResolver();
                List<FieldInput> inputs = new List<FieldInput>();
                TextStudioFontAppearanceTierRequest[] tiers = job.Tiers;
                long pixels = 0;
                for (int tier = 0; tier < tiers.Length; tier++)
                {
                    foreach (uint id in coverage)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (id >= face.GlyphCount) throw new InvalidDataException("The shaped glyph inventory does not belong to this font source.");
                        CurveGlyph glyph = face.GetGlyph((int)id);
                        if (glyph == null || glyph.ContourCount == 0) continue;
                        FieldInput input = CreateFieldInput(tier, id, glyph, tiers[tier].PixelsPerEm, tiers[tier].DistanceRangeEm, contours);
                        pixels += (long)input.Width * input.Height;
                        if (pixels * 4 > MaximumTextureBytes) throw new InvalidDataException("The requested font fields exceed 64 MiB. Reduce the declared glyph coverage.");
                        inputs.Add(input);
                    }
                }
                if (inputs.Count == 0) throw new InvalidDataException("The selected glyphs have no supported outline contours.");
                inputs = inputs.OrderBy(input => input.Tier).ThenByDescending(input => input.Height)
                    .ThenByDescending(input => input.Width).ThenBy(input => input.GlyphId).ToList();
                job.Total = inputs.Count;
                long plannedBytes = EstimatePackedBytes(inputs);
                if (plannedBytes > MaximumTextureBytes) throw new InvalidDataException("The requested atlas pages exceed 64 MiB. Reduce the declared glyph coverage.");
                Interlocked.Exchange(ref job.PlannedBytes, plannedBytes);
                List<FieldOutput> fields = new List<FieldOutput>(inputs.Count);
                string sourceHash = Hash(bytes);
                List<FieldInput> missing = new List<FieldInput>();
                foreach (FieldInput input in inputs)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (TryReadField(FieldCachePath(job, sourceHash, input), input, out FieldOutput cached))
                    { fields.Add(cached); Interlocked.Increment(ref job.Completed); }
                    else missing.Add(input);
                }
                for (int offset = 0; offset < missing.Count; offset += 4096)
                {
                    List<FieldInput> batch = missing.GetRange(offset, Math.Min(4096, missing.Count - offset));
                    foreach (FieldOutput field in BakeFields(baker, job.Folder, batch, cancellation, () => Interlocked.Increment(ref job.Completed)))
                    {
                        fields.Add(field);
                        WriteFieldCache(FieldCachePath(job, sourceHash, field.Input), field.Pixels);
                    }
                }
                return new BuildResult(bytes, sourceHash, fields.OrderBy(field => field.Input.Tier)
                    .ThenByDescending(field => field.Input.Height).ThenByDescending(field => field.Input.Width).ThenBy(field => field.Input.GlyphId).ToList());
            }
            finally
            {
                // This directory is created with a unique ID by Start and contains only this job's staging files.
                if (Directory.Exists(job.Folder)) Directory.Delete(job.Folder, true);
            }
        }

        static long EstimatePackedBytes(List<FieldInput> inputs)
        {
            long bytes = 0;
            int tier = -1, x = 1, y = 1, rowHeight = 0, usedWidth = 0, usedHeight = 0;
            void Flush()
            {
                if (usedWidth > 0) bytes += (long)Mathf.NextPowerOfTwo(usedWidth) * Mathf.NextPowerOfTwo(usedHeight) * 4;
                x = 1; y = 1; rowHeight = 0; usedWidth = 0; usedHeight = 0;
            }
            foreach (FieldInput input in inputs)
            {
                if (input.Tier != tier) { Flush(); tier = input.Tier; }
                if (x + input.Width + 1 > PageSize) { x = 1; y += rowHeight; rowHeight = 0; }
                if (y + input.Height + 1 > PageSize) Flush();
                usedWidth = Math.Max(usedWidth, x + input.Width + 1); usedHeight = Math.Max(usedHeight, y + input.Height + 1);
                x += input.Width + 2; rowHeight = Math.Max(rowHeight, input.Height + 2);
            }
            Flush();
            return bytes;
        }

        internal static void Update()
        {
            Job job = s_job;
            if (job == null) { EditorApplication.update -= Update; return; }
            Progress.Report(job.ProgressId, CurrentCompletion);
            if (!job.Task.IsCompleted) return;
            EditorApplication.update -= Update;
            bool succeeded = false;
            try
            {
                job.Cancellation.Token.ThrowIfCancellationRequested();
                BuildResult result = job.Task.GetAwaiter().GetResult();
                if (job.HasNativeFont && job.Font == null) throw new InvalidOperationException("The font was removed during preparation. No asset was written.");
                byte[] currentBytes = job.ReadCurrentSource?.Invoke();
                if (job.ReadCurrentSource != null && (currentBytes == null || Hash(currentBytes) != result.SourceHash)) throw new InvalidOperationException("The font changed during preparation. Prepare it again to use the new source.");
                LastResult = Save(job, result);
                CompletedFontCount++;
                LastError = string.Empty;
                foreach (TextStudioText hub in UnityUtils.FindAllIncludingInactive<TextStudioText>())
                    for (int index = 0; index < hub.AppearanceFontCount; index++)
                        if (hub.GetAppearanceFont(index) == LastResult) { hub.ScheduleTopologyRebuild(); break; }
                Progress.Finish(job.ProgressId);
                succeeded = true;
            }
            catch (OperationCanceledException) { LastError = "Font preparation was canceled."; Progress.Finish(job.ProgressId, Progress.Status.Canceled); }
            catch (Exception exception) { LastError = exception.Message; Progress.Finish(job.ProgressId, Progress.Status.Failed); UnityEngine.Debug.LogError("[Text Studio] " + LastError); }
            finally
            {
                s_job = null;
                job.Cancellation.Dispose();
            }

            // The finished job is released first, so a completion callback may start the next preparation.
            // Callback exceptions belong to the caller and never turn a saved preparation into a failure.
            try
            {
                if (succeeded) job.CompletedCallback?.Invoke(LastResult);
            }
            finally
            {
                AdvanceBatch(succeeded);
            }
        }

        internal static TextStudioFontAppearanceData SaveSourceOnly(TextStudioFontAppearanceRequest request)
        {
            byte[] bytes = request.ReadSource();
            if (bytes == null || bytes.Length == 0 || bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("The font source must fit within 32 MiB.");
            FontCurveSourceCache.GetOrCreate(bytes, request.FaceIndex);
            Job job = new Job(request.NativeFont, request.FaceIndex, request.SourceName, request.ReadSource, request.OutputPath, string.Empty, null, Array.Empty<TextStudioFontAppearanceTierRequest>());
            try { return Save(job, new BuildResult(bytes, Hash(bytes), new List<FieldOutput>())); }
            finally { job.Cancellation.Dispose(); }
        }

        static TextStudioFontAppearanceData Save(Job job, BuildResult result)
        {
            string path = job.OutputPath;
            TextStudioFontAppearanceData existing = AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(path);
            if (existing == null && AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("The output path belongs to a different asset.");
            TextStudioFontAppearanceData data = existing != null ? Object.Instantiate(existing) : ScriptableObject.CreateInstance<TextStudioFontAppearanceData>();
            TextStudioFontAppearanceData previous = existing != null ? Object.Instantiate(existing) : null;
            if (existing != null) data.name = existing.name;
            List<Texture2D> textures = new List<Texture2D>();
            bool created = false;
            bool published = false;
            try
            {
                List<TextStudioPreparedFieldTier> tiers = new List<TextStudioPreparedFieldTier>();
                long textureBytes = 0;
                foreach (IGrouping<int, FieldOutput> group in result.Fields.GroupBy(field => field.Input.Tier))
                {
                    List<TextStudioPreparedGlyphField> glyphs = new List<TextStudioPreparedGlyphField>();
                    List<Texture2D> pages = new List<Texture2D>();
                    byte[] pixels = new byte[PageSize * PageSize * 4];
                    int x = 1, y = 1, rowHeight = 0, usedWidth = 0, usedHeight = 0, firstPageGlyph = 0;
                    void Flush()
                    {
                        int width = Mathf.NextPowerOfTwo(usedWidth), height = Mathf.NextPowerOfTwo(usedHeight);
                        byte[] cropped = new byte[width * height * 4];
                        for (int row = 0; row < height; row++) Buffer.BlockCopy(pixels, row * PageSize * 4, cropped, row * width * 4, width * 4);
                        for (int index = firstPageGlyph; index < glyphs.Count; index++)
                        {
                            TextStudioPreparedGlyphField glyph = glyphs[index];
                            Rect uv = glyph.AtlasUv;
                            glyphs[index] = new TextStudioPreparedGlyphField(glyph.GlyphIndex, glyph.Page, glyph.EmBounds,
                                new Rect(uv.x * PageSize / width, uv.y * PageSize / height, uv.width * PageSize / width, uv.height * PageSize / height));
                        }
                        firstPageGlyph = glyphs.Count;
                        textureBytes += cropped.LongLength;
                        if (textureBytes > MaximumTextureBytes) throw new InvalidOperationException("The packed atlas pages exceed the 64 MiB budget. Reduce the declared glyph coverage.");
                        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                        { name = "Field " + group.Key + " Page " + pages.Count, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                        textures.Add(texture); pages.Add(texture);
                        texture.LoadRawTextureData(cropped); texture.Apply(false, true);
                        usedWidth = 0; usedHeight = 0;
                        Array.Clear(pixels, 0, pixels.Length);
                    }
                    FieldInput first = default;
                    foreach (FieldOutput field in group)
                    {
                        FieldInput input = field.Input; first = input;
                        if (x + input.Width + 1 > PageSize) { x = 1; y += rowHeight; rowHeight = 0; }
                        if (y + input.Height + 1 > PageSize) { Flush(); x = 1; y = 1; rowHeight = 0; }
                        for (int row = 0; row < input.Height; row++) Buffer.BlockCopy(field.Pixels, row * input.Width * 4, pixels, ((y + row) * PageSize + x) * 4, input.Width * 4);
                        glyphs.Add(new TextStudioPreparedGlyphField(input.GlyphId, pages.Count,
                            new Rect(input.Minimum, new Vector2(input.Width / input.Density, input.Height / input.Density)),
                            new Rect((float)x / PageSize, (float)y / PageSize, (float)input.Width / PageSize, (float)input.Height / PageSize)));
                        usedWidth = Math.Max(usedWidth, x + input.Width + 1); usedHeight = Math.Max(usedHeight, y + input.Height + 1);
                        x += input.Width + 2; rowHeight = Math.Max(rowHeight, input.Height + 2);
                    }
                    Flush();
                    tiers.Add(new TextStudioPreparedFieldTier(first.Density, first.Range, pages.ToArray(), glyphs.ToArray()));
                }
                data.Configure(job.Font, result.Bytes, job.FaceIndex, result.SourceHash, tiers.ToArray(), job.SourceName);
                if (!data.Validate(out string error)) throw new InvalidDataException(error);
                string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                EnsureAssetFolder(folder);
                if (existing == null) { data.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(data, path); created = true; }
                foreach (Texture2D texture in textures) AssetDatabase.AddObjectToAsset(texture, existing != null ? existing : data);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(data, existing);
                    existing.ClearRuntimeCaches();
                    Object.DestroyImmediate(data); data = existing;
                }
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                published = true;
                // Publish references before removing the old pages so a failed save can restore them.
                if (previous != null)
                {
                    for (int tier = 0; tier < previous.TierCount; tier++)
                    for (int page = 0; page < previous.GetTier(tier).PageCount; page++)
                    {
                        Texture2D oldPage = previous.GetTier(tier).GetPage(page);
                        if (oldPage != null && AssetDatabase.GetAssetPath(oldPage) == path) Object.DestroyImmediate(oldPage, true);
                    }
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
                return data;
            }
            catch (Exception exception)
            {
                if (published)
                {
                    UnityEngine.Debug.LogWarning("Font appearance data was saved, but unused atlas pages could not all be removed: " + exception.Message, data);
                    return data;
                }
                if (previous != null && existing != null)
                {
                    EditorUtility.CopySerialized(previous, existing);
                    existing.ClearRuntimeCaches();
                    EditorUtility.SetDirty(existing);
                }
                foreach (Texture2D texture in textures) if (texture != null) Object.DestroyImmediate(texture, AssetDatabase.Contains(texture));
                if (created) AssetDatabase.DeleteAsset(path);
                if (data != null && !AssetDatabase.Contains(data)) Object.DestroyImmediate(data);
                throw;
            }
            finally
            {
                if (previous != null) Object.DestroyImmediate(previous);
            }
        }

        static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureAssetFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Shutdown()
        {
            EditorApplication.update -= Update;
            LastResult = null;
            s_batch = null;
            Job job = s_job; s_job = null;
            if (job == null) return;
            job.Cancellation.Cancel();
            Progress.Finish(job.ProgressId, Progress.Status.Canceled);
            job.Task.ContinueWith(_ => job.Cancellation.Dispose(), TaskScheduler.Default);
        }

        sealed class Job
        {
            internal readonly Object Font;
            internal readonly bool HasNativeFont;
            internal readonly int FaceIndex;
            internal readonly TextStudioFontAppearanceTierRequest[] Tiers;
            internal readonly string SourceName;
            internal readonly Func<byte[]> ReadCurrentSource;
            internal readonly string OutputPath, Folder;
            internal readonly Action<TextStudioFontAppearanceData> CompletedCallback;
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal Task<BuildResult> Task;
            internal int ProgressId, Completed, Total;
            internal long PlannedBytes;
            internal Job(Object font, int faceIndex, string sourceName, Func<byte[]> readCurrentSource, string outputPath, string folder, Action<TextStudioFontAppearanceData> completed, TextStudioFontAppearanceTierRequest[] tiers)
            {
                Font = font; HasNativeFont = font != null; FaceIndex = faceIndex; Tiers = tiers;
                SourceName = string.IsNullOrEmpty(sourceName) ? "Font" : sourceName; ReadCurrentSource = readCurrentSource;
                OutputPath = outputPath; Folder = folder; CompletedCallback = completed;
            }
        }

        sealed class BuildResult
        {
            internal readonly byte[] Bytes;
            internal readonly string SourceHash;
            internal readonly List<FieldOutput> Fields;
            internal BuildResult(byte[] bytes, string sourceHash, List<FieldOutput> fields) { Bytes = bytes; SourceHash = sourceHash; Fields = fields; }
        }
    }
}
