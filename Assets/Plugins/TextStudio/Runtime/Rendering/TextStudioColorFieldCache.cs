using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using ImpossibleRobert.Common.Editor.Timing;
using ImpossibleRobert.Common.Timing;
#endif

namespace TextStudio
{
    /// <summary>Leased scalar fields for native color sources. At most two isolated tiles are processed concurrently.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioColorFieldCache : IDisposable
    {
        internal const int MaximumSources = 128;
        internal const int MaximumTileSize = 1024;
        internal const long MaximumTextureBytes = 64L * 1024 * 1024;
        const int MaximumJobs = 2;
        static TextStudioColorFieldCache s_shared;
        readonly Dictionary<(ulong, uint), Entry> _entries = new Dictionary<(ulong, uint), Entry>();
        readonly long _textureBudgetBytes;
        readonly List<Entry> _pendingReadbacks = new List<Entry>(MaximumJobs);
        Material _captureMaterial;
        int _running;
        long _reservedBytes;
        bool _disposed;
        uint _availabilityRevision;

        internal static TextStudioColorFieldCache Shared => s_shared ??= new TextStudioColorFieldCache();
        internal int SourceCount => _entries.Count;
        internal void CountSources(TextStudioReadOnlyBuffer<TextStudioColorSource> sources,
            out int ready, out int pending, out int borrowed)
        {
            ready = pending = borrowed = 0;
            for (int index = 0; index < sources.Length; index++)
            {
                TextStudioColorSource source = sources[index];
                if (source == null || source.Texture == null) continue;
                if (!_entries.TryGetValue((source.Identity, source.Texture.updateCount), out Entry entry)) continue;
                if (entry.Texture != null && entry.IsCurrent)
                {
                    ready++;
                    if (!entry.OwnsTexture) borrowed++;
                }
                if (entry.Running || entry.NeedsGeneration) pending++;
            }
        }
        internal int RunningJobs => _running;
        internal int PendingReadbacks => _pendingReadbacks.Count;
        internal long ReservedBytes => _reservedBytes;
        internal uint Revision { get; private set; }

        internal TextStudioColorFieldCache(long textureBudgetBytes = MaximumTextureBytes)
        {
            if (textureBudgetBytes <= 0 || textureBudgetBytes > MaximumTextureBytes) throw new ArgumentOutOfRangeException(nameof(textureBudgetBytes));
            _textureBudgetBytes = textureBudgetBytes;
        }

        internal sealed class Entry
        {
            internal readonly TextStudioColorFieldCache Owner;
            internal readonly TextStudioColorSource Source;
            internal readonly uint TextureRevision;
            internal int References;
            internal float RequestedDensity, RequestedPadding;
            internal float Density, Padding, DistanceRange;
            internal float PreparedDensityRequest;
            internal Rect Bounds;
            internal Texture2D Texture;
            internal bool OwnsTexture;
            internal bool Running;
            internal bool ReadbackPending;
            internal AsyncGPUReadbackRequest Readback;
            internal RenderTexture CaptureTarget;
            internal Mesh CaptureMesh;
            internal bool BudgetBlocked;
            internal uint BlockedRevision;
            internal uint BlockedMemoryRevision;
            internal TextStudioAppearanceMemoryLease PublishedMemory, PendingMemory, WorkingMemory;
            internal TextStudioBorrowedTextureLease BorrowedSource, BorrowedField;
            internal string Error = string.Empty;
            internal long ReservedBytes;
            internal bool IsCurrent => Source.Texture != null && Source.Texture.updateCount == TextureRevision;
            internal bool NeedsGeneration => (Error.Length == 0 || BudgetBlocked && (BlockedRevision != Owner._availabilityRevision ||
                BlockedMemoryRevision != TextStudioAppearanceMemory.AvailabilityRevision)) && IsCurrent &&
                (Texture == null || RequestedDensity > PreparedDensityRequest || RequestedPadding > Padding);
            internal Entry(TextStudioColorFieldCache owner, TextStudioColorSource source)
            { Owner = owner; Source = source; TextureRevision = source.Texture.updateCount; References = 1; BorrowedSource = TextStudioAppearanceMemory.Borrow(source.Texture); }
        }

        internal Entry Acquire(TextStudioColorSource source, float pixelsPerEm, float paddingEm, out string error, TextStudioPreparedColorField prepared = null)
        {
            error = string.Empty;
            if (_disposed) throw new ObjectDisposedException(nameof(TextStudioColorFieldCache));
            if (source == null || source.Texture == null || source.IsEmpty) { error = "The color source has no drawable geometry."; return null; }
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RFloat))
            { error = "Color silhouettes require scalar float textures."; return null; }
            if (!float.IsFinite(pixelsPerEm) || pixelsPerEm <= 0 || !float.IsFinite(paddingEm) || paddingEm < 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerEm));
            (ulong, uint) key = (source.Identity, source.Texture.updateCount);
            bool cacheHit = _entries.TryGetValue(key, out Entry entry);
            TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.Fields, cacheHit);
            if (cacheHit) entry.References++;
            else
            {
                if (_entries.Count >= MaximumSources) { error = "The active color silhouette source budget is full."; return null; }
                entry = new Entry(this, source);
                _entries.Add(key, entry);
            }
            if (entry.Texture == null && !entry.Running && prepared != null && prepared.Matches(source) && _reservedBytes - entry.ReservedBytes + prepared.TextureBytes <= _textureBudgetBytes)
            {
                _reservedBytes -= entry.ReservedBytes;
                entry.Texture = prepared.Field;
                entry.OwnsTexture = false;
                entry.BorrowedField = TextStudioAppearanceMemory.Borrow(prepared.Field);
                entry.Bounds = prepared.Bounds;
                entry.Density = prepared.PixelsPerEm;
                entry.PreparedDensityRequest = prepared.RequestedPixelsPerEm;
                entry.Padding = prepared.PaddingEm;
                entry.DistanceRange = prepared.DistanceRangeEm;
                entry.ReservedBytes = prepared.TextureBytes;
                _reservedBytes += entry.ReservedBytes;
                entry.Error = string.Empty; entry.BudgetBlocked = false;
                Revision++;
            }
            Request(entry, pixelsPerEm, paddingEm);
            return entry;
        }

        internal void Request(Entry entry, float pixelsPerEm, float paddingEm)
        {
            if (entry == null || entry.Owner != this || entry.References <= 0) return;
            if (!float.IsFinite(pixelsPerEm) || pixelsPerEm <= 0 || !float.IsFinite(paddingEm) || paddingEm < 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerEm));
            // Coalesce all consumers before the first poll starts work. Hold the largest live request until release.
            entry.RequestedDensity = Mathf.Max(entry.RequestedDensity, Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Clamp(pixelsPerEm, 16, 1024))));
            entry.RequestedPadding = Mathf.Max(entry.RequestedPadding, Mathf.Ceil(Mathf.Clamp(paddingEm, 0, 16) * 32) / 32);
        }

        internal bool TryGet(Entry entry)
        {
            Pump();
            return entry != null && entry.Owner == this && entry.References > 0 && entry.IsCurrent && entry.Texture != null;
        }

        internal void Release(ref Entry entry)
        {
            Entry released = entry;
            entry = null;
            if (released == null || released.Owner != this || released.References <= 0) return;
            if (--released.References > 0) return;
            _entries.Remove((released.Source.Identity, released.TextureRevision));
            if (released.OwnsTexture) Destroy(released.Texture);
            released.PublishedMemory?.Dispose(); released.PublishedMemory = null;
            released.BorrowedField?.Dispose(); released.BorrowedField = null;
            released.Texture = null;
            if (!released.Running) { FreeReservation(released); released.BorrowedSource?.Dispose(); released.BorrowedSource = null; }
            Revision++;
        }

        void Pump()
        {
            if (_disposed || _running >= MaximumJobs) return;
            foreach (Entry entry in _entries.Values)
            {
                if (_running >= MaximumJobs) break;
                if (!entry.Running && entry.NeedsGeneration) Start(entry);
            }
        }

        void Start(Entry entry)
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
            { entry.Error = "Prepare image silhouettes in the Editor for this device. Runtime preparation requires asynchronous texture readback."; return; }
            float density = entry.RequestedDensity;
            float padding = Mathf.Max(entry.RequestedPadding, 2 / density);
            Rect bounds = entry.Source.Bounds;
            bounds = Rect.MinMaxRect(bounds.xMin - padding, bounds.yMin - padding, bounds.xMax + padding, bounds.yMax + padding);
            float scale = Mathf.Min(density, MaximumTileSize / Mathf.Max(bounds.width, bounds.height));
            int width = Mathf.Clamp(Mathf.CeilToInt(bounds.width * scale), 4, MaximumTileSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(bounds.height * scale), 4, MaximumTileSize);
            long bytes = (long)width * height * 4;
            // Reserve both an old published tile and its replacement while an upgrade is in flight.
            long previousBytes = entry.Texture != null ? (long)entry.Texture.width * entry.Texture.height * 4 : 0;
            long reservation = previousBytes + bytes;
            if (_reservedBytes - entry.ReservedBytes + reservation > _textureBudgetBytes)
            {
                entry.Error = "The active color silhouette texture budget is full.";
                entry.BudgetBlocked = true;
                entry.BlockedRevision = _availabilityRevision;
                entry.BlockedMemoryRevision = TextStudioAppearanceMemory.AvailabilityRevision;
                return;
            }
            // Count the readback, detached alpha, both distance planes, line workspace, and upload staging before admission.
            long workingBytes = (long)width * height * 13 + (long)Math.Max(width, height) * 16 + 4;
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Fields, "Color silhouette replacement", bytes, 0, out entry.PendingMemory))
            { BlockForAppearanceBudget(entry); return; }
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Preparation, "Color silhouette preparation", bytes, workingBytes, out entry.WorkingMemory))
            {
                entry.PendingMemory.Dispose(); entry.PendingMemory = null;
                BlockForAppearanceBudget(entry); return;
            }
            entry.Error = string.Empty;
            entry.BudgetBlocked = false;
            _reservedBytes += reservation - entry.ReservedBytes;
            entry.ReservedBytes = reservation;
            RenderTexture target = null;
            Mesh mesh = null;
            CommandBuffer commands = null;
            try
            {
                if (_captureMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>("TextStudioColorSilhouetteCapture");
                    if (shader == null || !shader.isSupported) throw new InvalidOperationException("The color silhouette capture shader is unavailable.");
                    _captureMaterial = new Material(shader) { name = "Text Studio Color Silhouette Capture", hideFlags = HideFlags.HideAndDontSave };
                }
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "Text Studio Color Silhouette Readback", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                target.Create();
                mesh = entry.Source.CreateCaptureMesh();
                commands = new CommandBuffer { name = "Text Studio Isolated Color Silhouette" };
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(false, true, Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax, -1, 1));
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                properties.SetTexture("_MainTex", entry.Source.Texture);
                commands.DrawMesh(mesh, Matrix4x4.identity, _captureMaterial, 0, 0, properties);
                Graphics.ExecuteCommandBuffer(commands);
                entry.Running = true;
                _running++;
                entry.CaptureTarget = target;
                entry.CaptureMesh = mesh;
                target = null;
                mesh = null;
                entry.ReadbackPending = true;
                _pendingReadbacks.Add(entry);
                float distanceRange = Mathf.Max(padding, Mathf.Max(entry.Source.Bounds.width, entry.Source.Bounds.height), .01f);
                entry.Readback = AsyncGPUReadback.Request(entry.CaptureTarget, 0, TextureFormat.RGBA32, request =>
                {
                    byte[] alpha = null;
                    try
                    {
                        if (_disposed || entry.References <= 0 || !entry.IsCurrent) return;
                        if (request.hasError) { entry.Error = "The color silhouette texture could not be read back."; return; }
                        Unity.Collections.NativeArray<Color32> pixels = request.GetData<Color32>();
                        alpha = new byte[width * height];
                        for (int index = 0; index < alpha.Length; index++) alpha[index] = pixels[index].a;
                    }
                    catch (Exception exception) { entry.Error = exception.Message; }
                    finally
                    {
                        ReleaseCapture(entry);
                        if (alpha == null) Finish(entry);
                    }
                    if (alpha != null) TransformAsync(entry, alpha, width, height, bounds, density, padding, distanceRange);
                });
            }
            catch (Exception exception)
            {
                ReleaseCapture(entry);
                entry.Error = exception.Message;
                if (entry.Running) Finish(entry);
                else Finish(entry);
            }
            finally
            {
                commands?.Dispose();
                if (target != null) { target.Release(); Destroy(target); }
                Destroy(mesh);
            }
        }

        void ReleaseCapture(Entry entry)
        {
            if (entry.CaptureTarget != null) entry.CaptureTarget.Release();
            Destroy(entry.CaptureTarget);
            Destroy(entry.CaptureMesh);
            entry.CaptureTarget = null;
            entry.CaptureMesh = null;
            entry.ReadbackPending = false;
            _pendingReadbacks.Remove(entry);
            if (entry.WorkingMemory != null) entry.WorkingMemory.Reduce(0, entry.WorkingMemory.Cpu);
        }

        void BlockForAppearanceBudget(Entry entry)
        {
            entry.Error = TextStudioAppearanceMemory.LastRejection.ToString();
            entry.BudgetBlocked = true;
            entry.BlockedRevision = _availabilityRevision;
            entry.BlockedMemoryRevision = TextStudioAppearanceMemory.AvailabilityRevision;
        }

        async void TransformAsync(Entry entry, byte[] alpha, int width, int height, Rect bounds, float density, float padding, float range)
        {
            try
            {
                float threshold = entry.Source.AlphaThreshold;
#if UNITY_EDITOR
                // Readback stops requesting Editor player-loop updates before this managed worker finishes.
                // Keep its main-thread continuation runnable until the owned work is complete.
                EditorTickBridge.Subscribe(WakeWorkerContinuation);
#endif
                float[] distances = await Task.Run(() => TextStudioSilhouetteDistanceTransform.Generate(
                    alpha, width, height, bounds.width / width, bounds.height / height, threshold, range));
                if (_disposed || entry.References <= 0 || !entry.IsCurrent) return;
                Texture2D texture = new Texture2D(width, height, TextureFormat.RFloat, false, true)
                { name = "Text Studio Color Silhouette", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                try { texture.SetPixelData(distances, 0); texture.Apply(false, true); }
                catch { Destroy(texture); throw; }
                if (entry.OwnsTexture) Destroy(entry.Texture);
                entry.PublishedMemory?.Dispose();
                entry.BorrowedField?.Dispose(); entry.BorrowedField = null;
                entry.PendingMemory.Commit();
                entry.PublishedMemory = entry.PendingMemory; entry.PendingMemory = null;
                TextStudioAppearanceMemory.RecordUpload(TextStudioAppearanceResourceKind.Fields, (long)width * height * 4);
                entry.Texture = texture;
                entry.OwnsTexture = true;
                entry.Bounds = bounds;
                entry.Density = Mathf.Min(width / bounds.width, height / bounds.height);
                entry.PreparedDensityRequest = density;
                entry.Padding = padding;
                entry.DistanceRange = range;
                long bytes = (long)width * height * 4;
                _reservedBytes -= entry.ReservedBytes - bytes;
                if (entry.ReservedBytes > bytes) _availabilityRevision++;
                entry.ReservedBytes = bytes;
                Revision++;
            }
            catch (Exception exception) { entry.Error = exception.Message; }
            finally { Finish(entry); }
        }
#if UNITY_EDITOR
        void WakeWorkerContinuation() => HostTime.RequestEditorFrame(null, false, false);
#endif

        void Finish(Entry entry)
        {
            entry.PendingMemory?.Dispose(); entry.PendingMemory = null;
            entry.WorkingMemory?.Dispose(); entry.WorkingMemory = null;
            if (entry.Running) { entry.Running = false; _running--; }
#if UNITY_EDITOR
            if (_running == 0 || _disposed) EditorTickBridge.Unsubscribe(WakeWorkerContinuation);
#endif
            if (_disposed || entry.References <= 0 || entry.Texture == null) FreeReservation(entry);
            else
            {
                long publishedBytes = (long)entry.Texture.width * entry.Texture.height * 4;
                if (entry.ReservedBytes != publishedBytes) _availabilityRevision++;
                _reservedBytes += publishedBytes - entry.ReservedBytes;
                entry.ReservedBytes = publishedBytes;
            }
            if (_disposed || entry.References <= 0) { entry.BorrowedSource?.Dispose(); entry.BorrowedSource = null; }
            Revision++;
            // The next consumer poll starts queued or enlarged requests. Do not recurse from callbacks into enumeration.
        }

        void FreeReservation(Entry entry)
        {
            if (entry.ReservedBytes > 0) _availabilityRevision++;
            _reservedBytes -= entry.ReservedBytes;
            entry.ReservedBytes = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if UNITY_EDITOR
            EditorTickBridge.Unsubscribe(WakeWorkerContinuation);
#endif
            foreach (Entry entry in _entries.Values)
            {
                entry.References = 0;
                if (entry.OwnsTexture) Destroy(entry.Texture);
                entry.PublishedMemory?.Dispose(); entry.PublishedMemory = null;
                entry.BorrowedField?.Dispose(); entry.BorrowedField = null;
                entry.Texture = null;
                FreeReservation(entry);
                if (!entry.Running) { entry.BorrowedSource?.Dispose(); entry.BorrowedSource = null; }
            }
            _entries.Clear();
            // Shutdown drains only this owner's (at most two) requests before domain unload can discard their callbacks.
            while (_pendingReadbacks.Count > 0)
            {
                Entry entry = _pendingReadbacks[_pendingReadbacks.Count - 1];
                try { entry.Readback.WaitForCompletion(); }
                finally
                {
                    if (entry.ReadbackPending) { ReleaseCapture(entry); Finish(entry); }
                }
            }
            Destroy(_captureMaterial); _captureMaterial = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetRuntimeState() { s_shared?.Dispose(); s_shared = null; }
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void OnExitingPlayMode() => ResetRuntimeState();
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading() => ResetRuntimeState();
#endif
        static void Destroy(Object value)
        {
            if (value == null) return;
            TextStudio2DHeroSubmission.Flush();
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
