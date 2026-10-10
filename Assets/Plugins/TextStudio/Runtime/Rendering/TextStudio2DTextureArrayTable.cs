using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    internal enum TextStudio2DTextureArrayStatus : byte
    {
        Ready = 0,
        InvalidTexture = 1,
        UnsupportedPlatform = 2,
        CapacityExhausted = 3,
        ResourceConflict = 4,
        UploadFailed = 5,
        MemoryBudgetExceeded = 7
    }

    internal readonly struct TextStudio2DTextureArrayHandle :
        IEquatable<TextStudio2DTextureArrayHandle>
    {
        internal readonly int Entry;
        internal readonly uint Generation;

        internal TextStudio2DTextureArrayHandle(int entry, uint generation)
        {
            Entry = entry;
            Generation = generation;
        }

        internal bool IsValid => Entry >= 0 && Generation != 0u;
        internal int Slice => Entry;

        public bool Equals(TextStudio2DTextureArrayHandle other)
        {
            return Entry == other.Entry && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudio2DTextureArrayHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Entry * 397 ^ (int)Generation;
            }
        }
    }

    internal readonly struct TextStudio2DTextureArrayStatistics
    {
        internal readonly int ActiveTextures;
        internal readonly int SliceCapacity;
        internal readonly int UploadCount;
        internal readonly int ResizeCount;
        internal readonly long EstimatedBytes;

        internal TextStudio2DTextureArrayStatistics(
            int activeTextures,
            int sliceCapacity,
            int uploadCount,
            int resizeCount,
            long estimatedBytes)
        {
            ActiveTextures = activeTextures;
            SliceCapacity = sliceCapacity;
            UploadCount = uploadCount;
            ResizeCount = resizeCount;
            EstimatedBytes = estimatedBytes;
        }
    }

    /// <summary>
    /// Shared canonical color-texture array for fused paints. Arbitrary source descriptors are
    /// resampled once on acquisition, while warmed rendering performs no copies or allocations.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DTextureArrayTable : IDisposable
    {
        internal const int CanonicalResolution = 512;
        internal const int InitialSliceCapacity = 4;
        internal const int MaximumTextures = 64;

        const string TextureProperty = "_TextStudio2DPaintTextures";
        const string EnabledProperty = "_TextStudio2DPaintTexturesEnabled";

        static readonly int s_textureProperty = Shader.PropertyToID(TextureProperty);
        static readonly int s_enabledProperty = Shader.PropertyToID(EnabledProperty);
        static readonly GraphicsFormat s_graphicsFormat =
            GraphicsFormat.R8G8B8A8_SRGB;

        static TextStudio2DTextureArrayTable s_shared;

        readonly Entry[] _entries = new Entry[MaximumTextures];
        readonly int[] _freeEntries = new int[MaximumTextures];
        readonly Dictionary<string, int> _entriesByResourceId =
            new Dictionary<string, int>(StringComparer.Ordinal);

        RenderTexture _textureArray;
        TextStudioAppearanceMemoryLease _memory;
        int _freeEntryCount;
        int _activeTextureCount;
        int _sliceCapacity;
        int _uploadCount;
        int _resizeCount;
        bool _disposed;
        bool _memoryBudgetBlocked;

        struct Entry
        {
            internal uint Generation;
            internal bool Active;
            internal string ResourceId;
            internal Texture2D Source;
            internal int SourceInstanceId;
            internal uint SourceUpdateCount;
            internal Hash128 ContentHash;
            internal int ReferenceCount;
            internal TextStudioBorrowedTextureLease Borrowed;
        }

        internal static TextStudio2DTextureArrayTable Shared =>
            s_shared ??= new TextStudio2DTextureArrayTable();

        // Devices without cross-type copies draw each slice instead (see TextStudio2DSliceCopy).
        internal static bool IsSupported => SystemInfo.supports2DArrayTextures;

        internal TextStudio2DTextureArrayStatistics Statistics =>
            new TextStudio2DTextureArrayStatistics(
                _activeTextureCount,
                _sliceCapacity,
                _uploadCount,
                _resizeCount,
                EstimateBytes(_sliceCapacity));

        TextStudio2DTextureArrayTable()
        {
            ResetEntries();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetOnSubsystemRegistration()
        {
            ResetRuntimeState();
        }

        internal bool TryAcquire(
            Texture2D texture,
            string resourceId,
            out TextStudio2DTextureArrayHandle handle,
            out TextStudio2DTextureArrayStatus status)
        {
            ThrowIfDisposed();
            _memoryBudgetBlocked = false;
            handle = default;
            if (texture == null || string.IsNullOrWhiteSpace(resourceId))
            {
                status = TextStudio2DTextureArrayStatus.InvalidTexture;
                return false;
            }
            if (!IsSupported)
            {
                status = TextStudio2DTextureArrayStatus.UnsupportedPlatform;
                return false;
            }

            if (_entriesByResourceId.TryGetValue(resourceId, out int existingIndex))
            {
                Entry existing = _entries[existingIndex];
                if (!existing.Active)
                {
                    _entriesByResourceId.Remove(resourceId);
                }
                else
                {
                    Hash128 contentHash = TextStudioTextureContent.ImportHash(texture);
                    bool sameSource = existing.SourceInstanceId == texture.GetStableId();
                    if (!sameSource)
                    {
                        status = TextStudio2DTextureArrayStatus.ResourceConflict;
                        return false;
                    }

                    if (sameSource &&
                        (existing.SourceUpdateCount != texture.updateCount ||
                         !existing.ContentHash.Equals(contentHash)))
                    {
                        if (!TryUpload(texture, existingIndex))
                        {
                            status = _memoryBudgetBlocked ? TextStudio2DTextureArrayStatus.MemoryBudgetExceeded : TextStudio2DTextureArrayStatus.UploadFailed;
                            return false;
                        }
                        existing.SourceUpdateCount = texture.updateCount;
                        existing.ContentHash = contentHash;
                    }
                    existing.ReferenceCount++;
                    TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.PaintTextures, true);
                    _entries[existingIndex] = existing;
                    handle = new TextStudio2DTextureArrayHandle(
                        existingIndex,
                        existing.Generation);
                    status = TextStudio2DTextureArrayStatus.Ready;
                    return true;
                }
            }

            if (_freeEntryCount <= 0)
            {
                status = TextStudio2DTextureArrayStatus.CapacityExhausted;
                return false;
            }

            int entryIndex = _freeEntries[--_freeEntryCount];
            TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.PaintTextures, false);
            if (!EnsureCapacity(entryIndex + 1) || !TryUpload(texture, entryIndex))
            {
                _freeEntries[_freeEntryCount++] = entryIndex;
                status = _memoryBudgetBlocked ? TextStudio2DTextureArrayStatus.MemoryBudgetExceeded : TextStudio2DTextureArrayStatus.UploadFailed;
                return false;
            }

            Entry entry = _entries[entryIndex];
            entry.Active = true;
            entry.ResourceId = resourceId;
            entry.Source = texture;
            entry.Borrowed = TextStudioAppearanceMemory.Borrow(texture);
            entry.SourceInstanceId = texture.GetStableId();
            entry.SourceUpdateCount = texture.updateCount;
            entry.ContentHash = TextStudioTextureContent.ImportHash(texture);
            entry.ReferenceCount = 1;
            _entries[entryIndex] = entry;
            _entriesByResourceId.Add(resourceId, entryIndex);
            _activeTextureCount++;
            handle = new TextStudio2DTextureArrayHandle(entryIndex, entry.Generation);
            status = TextStudio2DTextureArrayStatus.Ready;
            return true;
        }

        internal void Release(ref TextStudio2DTextureArrayHandle handle)
        {
            if (_disposed || !IsCurrent(handle))
            {
                handle = default;
                return;
            }

            int index = handle.Entry;
            Entry entry = _entries[index];
            entry.ReferenceCount--;
            if (entry.ReferenceCount > 0)
            {
                _entries[index] = entry;
                handle = default;
                return;
            }

            _entriesByResourceId.Remove(entry.ResourceId);
            entry.Borrowed?.Dispose();
            entry.Borrowed = null;
            entry.Active = false;
            entry.ResourceId = string.Empty;
            entry.Source = null;
            entry.SourceInstanceId = 0;
            entry.SourceUpdateCount = 0u;
            entry.ContentHash = default;
            entry.ReferenceCount = 0;
            entry.Generation++;
            if (entry.Generation == 0u)
                entry.Generation = 1u;
            _entries[index] = entry;
            _freeEntries[_freeEntryCount++] = index;
            _activeTextureCount--;
            handle = default;

            if (_activeTextureCount == 0)
            {
                ReleaseTextureArray();
                ResetEntries();
            }
        }

        internal bool IsCurrent(TextStudio2DTextureArrayHandle handle)
        {
            return handle.IsValid &&
                   handle.Entry < MaximumTextures &&
                   _entries[handle.Entry].Active &&
                   _entries[handle.Entry].Generation == handle.Generation;
        }

        internal void BindGlobal()
        {
            ThrowIfDisposed();
            if (_activeTextureCount <= 0 || _textureArray == null || !RestoreContents())
            {
                Shader.SetGlobalFloat(s_enabledProperty, 0f);
                return;
            }

            Shader.SetGlobalTexture(s_textureProperty, _textureArray);
            Shader.SetGlobalFloat(s_enabledProperty, 1f);
        }

        internal RenderTexture GetTextureArrayForTests()
        {
            return _textureArray;
        }

        bool EnsureCapacity(int requiredSlices)
        {
            if (_textureArray != null && !RestoreContents()) return false;
            if (_textureArray != null && requiredSlices <= _sliceCapacity)
                return true;
            if (requiredSlices > MaximumTextures)
                return false;

            int nextCapacity = Mathf.Min(
                MaximumTextures,
                Mathf.NextPowerOfTwo(Mathf.Max(InitialSliceCapacity, requiredSlices)));
            long bytes = EstimateBytes(nextCapacity);
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.PaintTextures, "Paint texture array",
                    bytes + EstimateBytes(1), 0, out TextStudioAppearanceMemoryLease memory)) { _memoryBudgetBlocked = true; return false; }
            RenderTexture replacement = null;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(CanonicalResolution, CanonicalResolution)
                {
                    graphicsFormat = s_graphicsFormat, depthStencilFormat = GraphicsFormat.None,
                    dimension = TextureDimension.Tex2DArray, volumeDepth = nextCapacity, msaaSamples = 1,
                    useMipMap = true, autoGenerateMips = false, sRGB = true,
                    mipCount = 1 + Mathf.FloorToInt(Mathf.Log(CanonicalResolution, 2))
                };
                replacement = new RenderTexture(descriptor)
                {
                    name = "Text Studio 2D Paint Textures " + nextCapacity,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Repeat,
                    anisoLevel = 4
                };
                if (!replacement.Create()) throw new InvalidOperationException("The paint texture array could not be created.");

                if (_textureArray != null && TextStudio2DSliceCopy.CopiesAcrossTypes)
                {
                    int mipCount = Mathf.Min(
                        _textureArray.mipmapCount,
                        replacement.mipmapCount);
                    for (int slice = 0; slice < _sliceCapacity; slice++)
                    {
                        for (int mip = 0; mip < mipCount; mip++)
                        {
                            Graphics.CopyTexture(
                                _textureArray,
                                slice,
                                mip,
                                replacement,
                                slice,
                                mip);
                        }
                    }
                }
            }
            catch
            {
                Destroy(replacement);
                memory.Dispose();
                return false;
            }

            bool redraw = _textureArray != null && !TextStudio2DSliceCopy.CopiesAcrossTypes;
            Destroy(_textureArray);
            _memory?.Dispose();
            _memory = memory;
            memory.Commit(bytes, 0);
            _textureArray = replacement;
            _sliceCapacity = nextCapacity;
            _resizeCount++;
            // Without cross-type copies the grown array is drawn again from the entries' sources.
            return !redraw || UploadActiveEntries();
        }

        bool RestoreContents()
        {
            if (_textureArray.IsCreated()) return true;
            if (!_textureArray.Create()) return false;
            if (UploadActiveEntries()) return true;
            _textureArray.Release();
            return false;
        }

        bool UploadActiveEntries()
        {
            bool drawn = !TextStudio2DSliceCopy.CopiesAcrossTypes;
            for (int index = 0; index < _entries.Length; index++)
            {
                if (!_entries[index].Active) continue;
                if (_entries[index].Source == null || !TryUpload(_entries[index].Source, index, !drawn)) return false;
            }
            if (drawn) _textureArray.GenerateMips();
            return true;
        }

        bool TryUpload(Texture2D source, int slice, bool generateDrawnMips = true)
        {
            if (_textureArray == null || slice < 0 || slice >= _sliceCapacity)
                return false;
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Preparation, "Paint texture upload",
                    EstimateBytes(1), 0, out TextStudioAppearanceMemoryLease memory)) { _memoryBudgetBlocked = true; return false; }

            if (!TextStudio2DSliceCopy.CopiesAcrossTypes)
            {
                // Drawing the slice resamples like the staging blit; the array's own mips then match the staged chain.
                try
                {
                    TextStudio2DSliceCopy.DrawSlice(source, _textureArray, slice);
                    if (generateDrawnMips) _textureArray.GenerateMips();
                    _uploadCount++;
                    TextStudioAppearanceMemory.RecordUpload(TextStudioAppearanceResourceKind.PaintTextures, EstimateBytes(1));
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    memory.Dispose();
                }
            }

            RenderTexture staging = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                    CanonicalResolution,
                    CanonicalResolution)
                {
                    graphicsFormat = s_graphicsFormat,
                    depthBufferBits = 0,
                    msaaSamples = 1,
                    volumeDepth = 1,
                    dimension = TextureDimension.Tex2D,
                    useMipMap = true,
                    autoGenerateMips = false
                };
                staging = new RenderTexture(descriptor)
                {
                    name = "Text Studio 2D Paint Upload",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Repeat
                };
                if (!staging.Create())
                    return false;

                Graphics.Blit(source, staging);
                staging.GenerateMips();
                int mipCount = Mathf.Min(
                    staging.mipmapCount,
                    _textureArray.mipmapCount);
                for (int mip = 0; mip < mipCount; mip++)
                {
                    Graphics.CopyTexture(
                        staging,
                        0,
                        mip,
                        _textureArray,
                        slice,
                        mip);
                }
                _uploadCount++;
                TextStudioAppearanceMemory.RecordUpload(TextStudioAppearanceResourceKind.PaintTextures, EstimateBytes(1));
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (staging != null)
                {
                    if (RenderTexture.active == staging)
                        RenderTexture.active = previousActive;
                    staging.Release();
                    Destroy(staging);
                }
                memory.Dispose();
            }
        }

        static long EstimateBytes(int sliceCapacity)
        {
            if (sliceCapacity <= 0)
                return 0L;
            return TextStudioAppearanceMemory.TextureBytes(CanonicalResolution, CanonicalResolution, sliceCapacity,
                1 + Mathf.FloorToInt(Mathf.Log(CanonicalResolution, 2)), s_graphicsFormat);
        }

        void ResetEntries()
        {
            _freeEntryCount = 0;
            for (int index = 0; index < MaximumTextures; index++)
            {
                Entry entry = _entries[index];
                if (entry.Generation == 0u)
                    entry.Generation = 1u;
                _entries[index] = entry;
                _freeEntries[_freeEntryCount++] = MaximumTextures - index - 1;
            }
        }

        void ReleaseTextureArray()
        {
            Destroy(_textureArray);
            _memory?.Dispose();
            _memory = null;
            _textureArray = null;
            _sliceCapacity = 0;
            Shader.SetGlobalTexture(s_textureProperty, null);
            Shader.SetGlobalFloat(s_enabledProperty, 0f);
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextStudio2DTextureArrayTable));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (int index = 0; index < _entries.Length; index++) _entries[index].Borrowed?.Dispose();
            ReleaseTextureArray();
            _entriesByResourceId.Clear();
            _activeTextureCount = 0;
        }

        static void Destroy(Object value)
        {
            if (value == null)
                return;
            TextStudio2DHeroSubmission.Flush();
            if (value is RenderTexture target) target.Release();
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }

        internal static void ResetRuntimeState()
        {
            s_shared?.Dispose();
            s_shared = null;
            Shader.SetGlobalTexture(s_textureProperty, null);
            Shader.SetGlobalFloat(s_enabledProperty, 0f);
        }
    }
}
