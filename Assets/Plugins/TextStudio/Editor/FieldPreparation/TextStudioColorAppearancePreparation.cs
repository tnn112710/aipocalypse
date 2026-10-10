using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>Prepares exact static image silhouettes sequentially, with bounded GPU readback and publication on the main thread.</summary>
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioColorAppearancePreparation
    {
        static Job s_job;
        public static bool IsBusy => s_job != null;
        public static float Completion => s_job != null ? (float)s_job.Index / s_job.Sources.Count : 1;
        public static string LastError { get; private set; } = string.Empty;
        public static int CompletedImageCount { get; private set; }

        static TextStudioColorAppearancePreparation()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
        }

        public static bool StartForText(TextStudioText hub, float pixelsPerEm = 256, float paddingEm = .5f,
            string outputFolder = TextStudioAppearancePreparationProvider.GeneratedFolder)
        {
            if (IsBusy) { LastError = "Image preparation is already running."; return false; }
            try
            {
                if (hub == null) throw new ArgumentNullException(nameof(hub));
                hub.ForceMeshRefresh(0);
                TextStudioReadOnlyBuffer<TextStudioColorSource> current = hub.ColorSources;
                if (current.Length == 0) throw new InvalidOperationException("This text has no visible native images to prepare.");
                List<TextStudioColorSource> sources = new List<TextStudioColorSource>(current.Length);
                for (int i = 0; i < current.Length; i++) sources.Add(current[i]);
                string path = string.Empty;
                for (int i = 0; i < hub.AppearanceImageCount; i++)
                {
                    TextStudioColorAppearanceData data = hub.GetAppearanceImages(i);
                    if (data == null) continue;
                    for (int field = 0; field < data.FieldCount; field++)
                        if (data.GetField(field)?.SourceTexture == sources[0].Texture)
                        { path = AssetDatabase.GetAssetPath(data); break; }
                    if (path.Length > 0) break;
                }
                if (path.Length == 0)
                {
                    string name = hub.name;
                    foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
                    string stem = outputFolder.TrimEnd('/') + "/" + name + " Images";
                    for (int i = 0; i < 10000; i++)
                    {
                        string candidate = stem + (i == 0 ? string.Empty : " " + i) + ".asset";
                        if (!File.Exists(candidate)) { path = candidate; break; }
                    }
                    if (path.Length == 0) throw new IOException("Choose another folder for prepared image data.");
                }
                return Start(sources, path, pixelsPerEm, paddingEm, data =>
                {
                    if (hub == null) return;
                    Undo.RecordObject(hub, "Assign Prepared Image Appearance");
                    hub.AddAppearanceImages(data);
                    EditorUtility.SetDirty(hub);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hub);
                    if (!Application.isPlaying && hub.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(hub.gameObject.scene))
                        EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
                });
            }
            catch (Exception exception) { LastError = exception.Message; return false; }
        }

        /// <summary>Prepares static source descriptors and preserves the previous coverage when replacing an existing data asset.</summary>
        public static bool Start(IEnumerable<TextStudioColorSource> sources, string outputPath, float pixelsPerEm = 256,
            float paddingEm = .5f, Action<TextStudioColorAppearanceData> completed = null, bool useCpu = false)
        {
            if (IsBusy) { LastError = "Image preparation is already running."; return false; }
            LastError = string.Empty; CompletedImageCount = 0;
            try
            {
                if (sources == null) throw new ArgumentNullException(nameof(sources));
                ValidateOutputPath(outputPath);
                if (!float.IsFinite(pixelsPerEm) || pixelsPerEm < 16 || pixelsPerEm > 1024 ||
                    !float.IsFinite(paddingEm) || paddingEm <= 0 || paddingEm > 16)
                    throw new ArgumentOutOfRangeException(nameof(pixelsPerEm), "Use 16 to 1024 pixels per em and more than 0 to 16 em of padding.");
                if (AssetDatabase.LoadMainAssetAtPath(outputPath) is Object existing && existing is not TextStudioColorAppearanceData)
                    throw new InvalidOperationException("The output path belongs to another asset type.");
                List<TextStudioColorSource> unique = new List<TextStudioColorSource>();
                HashSet<ulong> identities = new HashSet<ulong>();
                void Add(TextStudioColorSource source)
                {
                    if (source == null || source.IsEmpty || source.Texture is not Texture2D || source.Revision != 0 ||
                        !EditorUtility.IsPersistent(source.Texture))
                        throw new ArgumentException("Prepare images from saved Texture2D assets. Dynamic sources use runtime preparation.");
                    if (identities.Add(source.Identity)) unique.Add(source);
                    if (unique.Count > TextStudioColorFieldCache.MaximumSources) throw new ArgumentException("Prepare at most 128 unique image sources in one asset.");
                }
                foreach (TextStudioColorSource source in sources) Add(source);
                if (unique.Count == 0) throw new ArgumentException("No image sources were supplied.");
                TextStudioColorAppearanceData previous = AssetDatabase.LoadAssetAtPath<TextStudioColorAppearanceData>(outputPath);
                if (previous != null)
                    for (int i = 0; i < previous.FieldCount; i++)
                    {
                        TextStudioPreparedColorField field = previous.GetField(i);
                        Add(field.CreateSource());
                        pixelsPerEm = Mathf.Max(pixelsPerEm, field.RequestedPixelsPerEm);
                        paddingEm = Mathf.Max(paddingEm, field.PaddingEm);
                    }
                s_job = new Job(unique, outputPath, pixelsPerEm, paddingEm, completed) { UseCpu = useCpu };
                EditorApplication.update += Update;
                return true;
            }
            catch (Exception exception) { LastError = exception.Message; return false; }
        }

        static void ValidateOutputPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/../") || path.Contains("/./") || path.Contains('\\') ||
                !Path.GetFullPath(path).StartsWith(Path.GetFullPath("Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose an .asset output path inside Assets.", nameof(path));
        }

        public static void Cancel()
        {
            Job job = s_job; s_job = null;
            EditorApplication.update -= Update;
            job?.Dispose();
        }

        internal static void Update()
        {
            Job job = s_job;
            if (job == null) return;
            try
            {
                if (job.Error.Length > 0) throw new InvalidOperationException(job.Error);
                if (job.ExportPending) return;
                if (job.Index == job.Sources.Count)
                {
                    TextStudioColorAppearanceData data = job.Publish();
                    Action<TextStudioColorAppearanceData> completed = job.Completed;
                    CompletedImageCount = job.Index;
                    Cancel(); completed?.Invoke(data); return;
                }
                TextStudioColorSource source = job.Sources[job.Index];
                if (!job.IsCurrent(job.Index)) throw new InvalidOperationException("An image changed during preparation. Prepare its appearance data again.");
                if (job.UseCpu) { UpdateCpu(job); return; }
                if (job.Lease == null)
                {
                    job.Lease = job.Cache.Acquire(source, job.Density, job.Padding, out string error);
                    if (job.Lease == null) throw new InvalidOperationException(error);
                }
                bool ready = job.Cache.TryGet(job.Lease);
                if (job.Lease.Error.Length > 0) throw new InvalidOperationException(job.Lease.Error);
                if (!ready) return;
                if (job.Bytes + (long)job.Lease.Texture.width * job.Lease.Texture.height * 4 > TextStudioColorFieldCache.MaximumTextureBytes)
                    throw new InvalidOperationException("Prepared image fields exceed 64 MiB. Reduce density, padding, or source coverage.");
                job.ExportPending = true;
                job.Export = AsyncGPUReadback.Request(job.Lease.Texture, 0, TextureFormat.RFloat, request => job.ExportField(request));
            }
            catch (Exception exception) { LastError = exception.Message; Cancel(); }
        }

        sealed class Job : IDisposable
        {
            internal readonly List<TextStudioColorSource> Sources;
            internal readonly TextStudioColorFieldCache Cache = new TextStudioColorFieldCache();
            internal readonly Action<TextStudioColorAppearanceData> Completed;
            internal readonly float Density, Padding;
            readonly string _path;
            readonly uint[] _revisions;
            readonly Hash128[] _hashes;
            readonly List<TextStudioPreparedColorField> _fields = new List<TextStudioPreparedColorField>();
            internal TextStudioColorFieldCache.Entry Lease;
            internal int Index;
            internal long Bytes;
            internal string Error = string.Empty;
            internal AsyncGPUReadbackRequest Export;
            internal bool ExportPending;
            internal bool UseCpu;
            internal TextStudioAppearanceMemoryLease CpuMemory;
            internal System.Threading.Tasks.Task<CpuResult> CpuTask;
            internal readonly System.Threading.CancellationTokenSource CpuCancellation = new System.Threading.CancellationTokenSource();
            internal void AcceptCpuImport()
            {
                if (Sources[Index].Texture.imageContentsHash != _hashes[Index]) throw new InvalidOperationException("The source image changed during import.");
                _revisions[Index] = Sources[Index].Texture.updateCount;
            }
            internal void AddCpuField(TextStudioPreparedColorField field) { _fields.Add(field); Bytes += field.TextureBytes; Index++; }
            bool _disposed, _published;

            internal Job(List<TextStudioColorSource> sources, string path, float density, float padding, Action<TextStudioColorAppearanceData> completed)
            {
                Sources = sources; _path = path; Density = density; Padding = padding; Completed = completed;
                _revisions = new uint[sources.Count]; _hashes = new Hash128[sources.Count];
                for (int i = 0; i < sources.Count; i++) { _revisions[i] = sources[i].Texture.updateCount; _hashes[i] = sources[i].Texture.imageContentsHash; }
            }

            internal bool IsCurrent(int index) => Sources[index].Texture != null && EditorUtility.IsPersistent(Sources[index].Texture) &&
                Sources[index].Texture.updateCount == _revisions[index] && Sources[index].Texture.imageContentsHash == _hashes[index];

            internal void ExportField(AsyncGPUReadbackRequest request)
            {
                Texture2D texture = null;
                try
                {
                    if (_disposed) return;
                    if (request.hasError) throw new InvalidOperationException("The prepared image field could not be saved from the GPU.");
                    texture = new Texture2D(Lease.Texture.width, Lease.Texture.height, TextureFormat.RFloat, false, true)
                    { name = "Image Silhouette " + Index, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    texture.SetPixelData(request.GetData<float>(), 0); texture.Apply(false, true);
                    TextStudioPreparedColorField field = new TextStudioPreparedColorField(Sources[Index], texture, Lease.Bounds,
                        Lease.Density, Lease.PreparedDensityRequest, Lease.Padding, Lease.DistanceRange);
                    _fields.Add(field); Bytes += field.TextureBytes; texture = null; Index++;
                    Cache.Release(ref Lease);
                }
                catch (Exception exception) { Error = exception.Message; }
                finally { Object.DestroyImmediate(texture); ExportPending = false; }
            }

            internal TextStudioColorAppearanceData Publish()
            {
                for (int i = 0; i < Sources.Count; i++) if (!IsCurrent(i)) throw new InvalidOperationException("An image changed before publication. Prepare its appearance data again.");
                TextStudioColorAppearanceData existing = AssetDatabase.LoadAssetAtPath<TextStudioColorAppearanceData>(_path);
                TextStudioColorAppearanceData data = ScriptableObject.CreateInstance<TextStudioColorAppearanceData>();
                TextStudioColorAppearanceData backup = existing != null ? Object.Instantiate(existing) : null;
                List<Texture2D> previous = new List<Texture2D>();
                if (existing != null)
                    for (int i = 0; i < existing.FieldCount; i++) previous.Add(existing.GetField(i).Field);
                bool created = false;
                try
                {
                    data.name = existing != null ? existing.name : Path.GetFileNameWithoutExtension(_path);
                    data.SetFields(_fields.ToArray());
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    if (existing == null) { AssetDatabase.CreateAsset(data, _path); created = true; }
                    foreach (TextStudioPreparedColorField field in _fields) AssetDatabase.AddObjectToAsset(field.Field, existing != null ? existing : data);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(data, existing);
                        EditorUtility.SetDirty(existing);
                    }
                    AssetDatabase.SaveAssetIfDirty(existing != null ? existing : data);
                    _published = true;
                    // Retain the old textures until the replacement is saved, so a failed publication can roll back.
                    try
                    {
                        foreach (Texture2D texture in previous)
                            if (texture != null && AssetDatabase.GetAssetPath(texture) == _path) Object.DestroyImmediate(texture, true);
                        AssetDatabase.SaveAssetIfDirty(existing != null ? existing : data);
                    }
                    catch (Exception exception) { Debug.LogWarning("[Text Studio] Prepared image data was saved, but old texture cleanup failed: " + exception.Message); }
                    return existing != null ? existing : data;
                }
                catch
                {
                    if (created) AssetDatabase.DeleteAsset(_path);
                    else if (existing != null && backup != null)
                    {
                        EditorUtility.CopySerialized(backup, existing);
                        EditorUtility.SetDirty(existing);
                        foreach (TextStudioPreparedColorField field in _fields)
                            if (field.Field != null && AssetDatabase.GetAssetPath(field.Field) == _path) Object.DestroyImmediate(field.Field, true);
                        AssetDatabase.SaveAssetIfDirty(existing);
                    }
                    throw;
                }
                finally { if (!created) Object.DestroyImmediate(data); Object.DestroyImmediate(backup); }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                CpuCancellation.Cancel();
                // CPU tiles are capped at 1024 squared and rasterization checks cancellation per row.
                // Drain this pure worker before releasing its memory reservation or unloading the domain.
                try { CpuTask?.GetAwaiter().GetResult(); } catch (Exception) { }
                CpuCancellation.Dispose();
                CpuMemory?.Dispose(); CpuMemory = null;
                try { if (ExportPending) Export.WaitForCompletion(); }
                finally
                {
                    Cache.Dispose();
                    if (!_published) foreach (TextStudioPreparedColorField field in _fields)
                        if (field.Field != null) Object.DestroyImmediate(field.Field, true);
                }
            }
        }
    }
}
