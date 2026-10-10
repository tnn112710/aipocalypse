using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    /// <summary>Copies immutable prepared pages without resampling. Mapping entries own reference-counted leases.</summary>
    internal sealed class TextStudioPreparedFieldPages : IDisposable
    {
        internal const int PageSize = 1024;
        internal const int MaximumPages = 16;
        static readonly int s_textureProperty = Shader.PropertyToID("_TextStudioPreparedFields");
        readonly Page[] _pages = new Page[MaximumPages];
        RenderTexture _texture;
        TextStudioAppearanceMemoryLease _memory;
        bool _disposed;

        internal int ActivePages { get; private set; }
        internal long GpuBytes => (long)(_texture != null ? _texture.volumeDepth : 0) * PageSize * PageSize * 4;
        uint _pageAvailabilityRevision;
        internal uint AvailabilityRevision => unchecked(_pageAvailabilityRevision + TextStudioAppearanceMemory.AvailabilityRevision);
        internal RenderTexture Texture => _texture;
        // Devices without cross-type copies draw each page instead (see TextStudio2DSliceCopy).
        internal static bool IsSupported => SystemInfo.supports2DArrayTextures;

        internal sealed class Page
        {
            internal readonly TextStudioPreparedFieldPages Owner;
            internal readonly Texture2D Source;
            internal readonly int Slice;
            internal int References;
            internal readonly TextStudioBorrowedTextureLease Borrowed;
            internal Page(TextStudioPreparedFieldPages owner, Texture2D source, int slice)
            { Owner = owner; Source = source; Slice = slice; References = 1; Borrowed = TextStudioAppearanceMemory.Borrow(source); }
        }

        internal bool TryAcquire(Texture2D source, out Page lease)
        {
            lease = null;
            if (_disposed || !IsSupported || source == null || source.width > PageSize || source.height > PageSize)
                return false;
            int free = -1;
            for (int index = 0; index < MaximumPages; index++)
            {
                Page page = _pages[index];
                if (page == null) { if (free < 0) free = index; continue; }
                if (page.Source != source) continue;
                page.References++;
                TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.Fields, true);
                lease = page;
                return true;
            }
            if (free < 0) return false;
            TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.Fields, false);
            try
            {
                if (!EnsureCapacity(free + 1)) return false;
                TextStudio2DSliceCopy.CopyToSliceOrigin(source, _texture, free);
                TextStudioAppearanceMemory.RecordUpload(TextStudioAppearanceResourceKind.Fields, (long)source.width * source.height * 4);
                lease = new Page(this, source, free);
                _pages[free] = lease;
                ActivePages++;
                _pageAvailabilityRevision++;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[TextStudio] A prepared font page could not be uploaded. Its glyphs keep their native field. " + exception.Message);
                if (ActivePages == 0) ReleaseTexture();
                return false;
            }
        }

        internal void Release(Page lease)
        {
            if (_disposed || lease == null || !ReferenceEquals(lease.Owner, this) ||
                !ReferenceEquals(_pages[lease.Slice], lease)) return;
            if (--lease.References > 0) return;
            lease.Borrowed.Dispose();
            _pages[lease.Slice] = null;
            ActivePages--;
            _pageAvailabilityRevision++;
            if (ActivePages == 0) ReleaseTexture();
        }

        internal void Bind()
        {
            if (_texture != null && RestoreContents()) Shader.SetGlobalTexture(s_textureProperty, _texture);
            else TextStudio2DGpuFallbackResources.RestorePreparedFieldTexture();
        }

        bool RestoreContents()
        {
            if (_texture.IsCreated()) return true;
            if (!_texture.Create()) return false;
            for (int index = 0; index < _pages.Length; index++)
            {
                Page page = _pages[index];
                if (page == null || page.Source == null) continue;
                Texture2D source = page.Source;
                TextStudio2DSliceCopy.CopyToSliceOrigin(source, _texture, index);
                TextStudioAppearanceMemory.RecordUpload(TextStudioAppearanceResourceKind.Fields, (long)source.width * source.height * 4);
            }
            return true;
        }

        bool EnsureCapacity(int required)
        {
            if (_texture != null && _texture.volumeDepth >= required) return RestoreContents();
            int capacity = Mathf.NextPowerOfTwo(required);
            long bytes = (long)PageSize * PageSize * capacity * 4;
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Fields, "Prepared font field array",
                    bytes, 0, out TextStudioAppearanceMemoryLease memory)) return false;
            RenderTexture next = null;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(PageSize, PageSize)
                {
                    graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm, depthStencilFormat = GraphicsFormat.None,
                    dimension = TextureDimension.Tex2DArray, volumeDepth = capacity, msaaSamples = 1,
                    useMipMap = false, autoGenerateMips = false, sRGB = false
                };
                next = new RenderTexture(descriptor)
                {
                    name = "Text Studio Prepared Font Fields", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
                };
                if (!next.Create()) throw new InvalidOperationException("The prepared field array could not be created.");
                if (_texture != null)
                    for (int index = 0; index < _texture.volumeDepth; index++)
                    {
                        Page page = _pages[index];
                        if (page == null) continue;
                        // Without cross-type copies the grown array is drawn again from the pages' sources.
                        if (TextStudio2DSliceCopy.CopiesAcrossTypes) Graphics.CopyTexture(_texture, index, 0, next, index, 0);
                        else if (page.Source != null) TextStudio2DSliceCopy.CopyToSliceOrigin(page.Source, next, index);
                    }
            }
            catch { Destroy(next); memory.Dispose(); throw; }
            RenderTexture previous = _texture;
            _texture = next;
            Bind();
            Destroy(previous);
            _memory?.Dispose();
            _memory = memory;
            memory.Commit(bytes, 0);
            return true;
        }

        void ReleaseTexture()
        {
            RenderTexture previous = _texture;
            _texture = null;
            TextStudio2DGpuFallbackResources.RestorePreparedFieldTexture();
            Destroy(previous);
            _memory?.Dispose();
            _memory = null;
        }

        static void Destroy(RenderTexture texture)
        {
            if (texture == null) return;
            TextStudio2DHeroSubmission.Flush();
            texture.Release();
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = 0; index < _pages.Length; index++) _pages[index]?.Borrowed.Dispose();
            Array.Clear(_pages, 0, _pages.Length);
            ActivePages = 0;
            ReleaseTexture();
        }
    }
}
