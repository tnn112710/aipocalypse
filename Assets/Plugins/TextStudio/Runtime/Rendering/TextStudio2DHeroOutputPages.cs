using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    /// <summary>One owner's published Hero output: a slice of a shared page plus the valid rectangle it rendered.</summary>
    internal struct TextStudio2DHeroOutputLease
    {
        internal TextStudio2DHeroOutputPages.Page Page;
        internal int Slice;
        internal uint Generation;
        internal int Width;
        internal int Height;

        internal bool IsValid => Page != null && Page.Owns(Slice, Generation);
        internal Texture Texture => IsValid ? Page.Texture : null;
        /// <summary>Normalized extent of the valid rectangle inside the slice.</summary>
        internal Vector2 UvScale => IsValid ? new Vector2((float)Width / Page.Width, (float)Height / Page.Height) : Vector2.one;
    }

    /// <summary>
    /// Pools final Hero outputs in <see cref="TextureDimension.Tex2DArray"/> pages bucketed by resolution and format, so
    /// compatible outputs share one texture and one material: world outputs need no property block and Canvas outputs
    /// batch natively. Pages travel in material array properties because Canvas accepts only a 2D main texture. Each owner copies its completed composition into the origin of its own slice and presents the
    /// valid rectangle; scratch and history stay in <see cref="TextStudio2DHeroTargetPool"/>. Slices are single-sampled
    /// because Hero coverage is analytically antialiased. Pages have fixed capacity so slice handles stay stable, and a
    /// page is destroyed with its last slice. A lost page recreates its storage and reports it so owners re-render.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DHeroOutputPages
    {
        // Bucket edges keep sharing likely while bounding unused texels; sizes above the last edge are exact.
        static readonly int[] s_bucketEdges = { 64, 128, 192, 256, 384, 512, 640, 768, 896, 1024, 1280, 1536, 1792, 2048 };
        // A page reserves every slice up front; this bounds the unused capacity of one bucket.
        const long PageBudgetBytes = 4L * 1024L * 1024L;
        const int MaximumSlicesPerPage = 8;

        static TextStudio2DHeroOutputPages s_shared;
        readonly List<Page> _pages = new List<Page>();
        readonly Dictionary<(Page, Page), Material> _worldMaterials = new Dictionary<(Page, Page), Material>();
        readonly Dictionary<Page, Material> _canvasMaterials = new Dictionary<Page, Material>();

        internal static TextStudio2DHeroOutputPages Shared => s_shared ??= new TextStudio2DHeroOutputPages();

        internal int PageCount => _pages.Count;
        internal int SliceCount
        {
            get
            {
                int count = 0;
                foreach (Page page in _pages) count += page.UsedCount;
                return count;
            }
        }
        internal long ReservedBytes
        {
            get
            {
                long bytes = 0;
                foreach (Page page in _pages) bytes += page.ByteSize;
                return bytes;
            }
        }

        /// <summary>
        /// Region copies from a 2D target into an array slice need <see cref="CopyTextureSupport.DifferentTypes"/>; without
        /// it pages are exact-size and publication blits the whole slice, which keeps output faithful but rarely shared.
        /// </summary>
        static bool RegionCopies => TextStudio2DSliceCopy.CopiesAcrossTypes;

        internal static bool IsSupported => SystemInfo.supports2DArrayTextures;

        internal static int Bucket(int size)
        {
            if (!RegionCopies) return size;
            foreach (int edge in s_bucketEdges)
                if (size <= edge) return edge;
            return size;
        }

        /// <summary>
        /// Keeps <paramref name="lease"/> when its page still matches the requested bucket, otherwise moves it to a free
        /// slice of a matching page. On failure the previous lease stays untouched.
        /// </summary>
        internal bool TryAcquire(int width, int height, GraphicsFormat format, ref TextStudio2DHeroOutputLease lease)
        {
            if (!IsSupported || width <= 0 || height <= 0) return false;
            int bucketWidth = Bucket(width), bucketHeight = Bucket(height);
            if (lease.IsValid && lease.Page.Matches(bucketWidth, bucketHeight, format))
            {
                lease.Width = width;
                lease.Height = height;
                return true;
            }
            Page target = null;
            foreach (Page page in _pages)
            {
                if (page.Matches(bucketWidth, bucketHeight, format) && page.UsedCount < page.Capacity)
                {
                    target = page;
                    break;
                }
            }
            if (target == null && !TryCreatePage(bucketWidth, bucketHeight, format, out target)) return false;
            Release(ref lease);
            lease = target.Take(width, height);
            return true;
        }

        internal void Release(ref TextStudio2DHeroOutputLease lease)
        {
            if (lease.IsValid)
            {
                Page page = lease.Page;
                page.Return(lease.Slice);
                if (page.UsedCount == 0) DestroyPage(page);
            }
            lease = default;
        }

        /// <summary>Recreates lost page storage. Returns false when the slice content was lost and must be rendered again.</summary>
        internal static bool EnsureResident(in TextStudio2DHeroOutputLease lease)
        {
            if (!lease.IsValid) return false;
            RenderTexture texture = lease.Page.Texture;
            if (texture.IsCreated()) return true;
            texture.Create();
            lease.Page.ForgetWrittenExtents();
            return false;
        }

        /// <summary>Records the copy of a completed composition into the origin of the lease's slice.</summary>
        internal static void RecordPublish(CommandBuffer commands, RenderTexture source, in TextStudio2DHeroOutputLease lease)
        {
            if (RegionCopies)
            {
                // Output sampling filters one texel past the copied rectangle, so texels a larger earlier write left in the slice are cleared first.
                if (lease.Page.NeedsClear(lease.Slice, lease.Width, lease.Height))
                {
                    commands.SetRenderTarget(lease.Page.Texture, 0, CubemapFace.Unknown, lease.Slice);
                    commands.ClearRenderTarget(false, true, Color.clear);
                }
                commands.CopyTexture(source, 0, 0, 0, 0, lease.Width, lease.Height, lease.Page.Texture, lease.Slice, 0, 0, 0);
                lease.Page.MarkWritten(lease.Slice, lease.Width, lease.Height);
            }
            else
                TextStudio2DSliceCopy.RecordDrawSlice(commands, source, lease.Page.Texture, lease.Slice);
        }

        /// <summary>The shared world material presenting <paramref name="color"/> with alpha-clipped shadows from <paramref name="shadow"/>.</summary>
        internal Material GetWorldMaterial(Material template, in TextStudio2DHeroOutputLease color, in TextStudio2DHeroOutputLease shadow)
        {
            if (template == null || !color.IsValid) return null;
            Page shadowPage = shadow.IsValid ? shadow.Page : color.Page;
            if (_worldMaterials.TryGetValue((color.Page, shadowPage), out Material material) && material != null)
                return material;
            material = new Material(template)
            {
                name = "Text Studio Hero Output Page",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetTexture(TextStudio2DHeroOutputShader.Pages, color.Page.Texture);
            material.SetTexture(TextStudio2DHeroOutputShader.ShadowPages, shadowPage.Texture);
            _worldMaterials[(color.Page, shadowPage)] = material;
            return material;
        }

        /// <summary>The shared Canvas material presenting <paramref name="color"/>'s page; Canvas batches outputs by material.</summary>
        internal Material GetCanvasMaterial(Material template, in TextStudio2DHeroOutputLease color)
        {
            if (template == null || !color.IsValid) return null;
            if (_canvasMaterials.TryGetValue(color.Page, out Material material) && material != null)
                return material;
            material = new Material(template)
            {
                name = "Text Studio Hero Canvas Output Page",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetTexture(TextStudio2DHeroOutputShader.Pages, color.Page.Texture);
            _canvasMaterials[color.Page] = material;
            return material;
        }

        /// <summary>The GPU bytes a new page for an output of this size reserves, since a page reserves every slice up front.</summary>
        internal static long ReservationBytes(int width, int height, GraphicsFormat format)
        {
            long sliceBytes = SliceBytes(Bucket(width), Bucket(height), format);
            return sliceBytes * Capacity(sliceBytes);
        }

        static long SliceBytes(int width, int height, GraphicsFormat format) =>
            (long)width * height * Math.Max(1, (int)GraphicsFormatUtility.GetBlockSize(format));

        static int Capacity(long sliceBytes) => (int)Math.Clamp(PageBudgetBytes / Math.Max(1L, sliceBytes), 1L, MaximumSlicesPerPage);

        bool TryCreatePage(int width, int height, GraphicsFormat format, out Page page)
        {
            page = null;
            long sliceBytes = SliceBytes(width, height, format);
            int capacity = Capacity(sliceBytes);
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.HeroTargets, "Hero output page",
                    sliceBytes * capacity, 0, out TextStudioAppearanceMemoryLease memory))
                return false;
            RenderTexture texture = null;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, format, 0)
                {
                    dimension = TextureDimension.Tex2DArray,
                    volumeDepth = capacity,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    sRGB = false
                };
                texture = new RenderTexture(descriptor)
                {
                    name = "Text Studio Hero Output Page",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!texture.Create()) throw new InvalidOperationException("The Hero output page could not be created.");
            }
            catch
            {
                Destroy(texture);
                memory.Dispose();
                return false;
            }
            memory.Commit();
            page = new Page(width, height, format, capacity, texture, memory, sliceBytes * capacity);
            _pages.Add(page);
            return true;
        }

        void DestroyPage(Page page)
        {
            _pages.Remove(page);
            List<(Page, Page)> keys = null;
            foreach (KeyValuePair<(Page, Page), Material> entry in _worldMaterials)
                if (entry.Key.Item1 == page || entry.Key.Item2 == page)
                    (keys ??= new List<(Page, Page)>()).Add(entry.Key);
            if (keys != null)
            {
                foreach ((Page, Page) key in keys)
                {
                    Destroy(_worldMaterials[key]);
                    _worldMaterials.Remove(key);
                }
            }
            if (_canvasMaterials.TryGetValue(page, out Material canvasMaterial))
            {
                Destroy(canvasMaterial);
                _canvasMaterials.Remove(page);
            }
            page.Dispose();
        }

        static void Destroy(Object value)
        {
            if (value == null) return;
            TextStudio2DHeroSubmission.Flush();
            if (value is RenderTexture target) target.Release();
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void ResetRuntimeState()
        {
            if (s_shared == null) return;
            foreach (Material material in s_shared._worldMaterials.Values) Destroy(material);
            s_shared._worldMaterials.Clear();
            foreach (Material material in s_shared._canvasMaterials.Values) Destroy(material);
            s_shared._canvasMaterials.Clear();
            foreach (Page page in s_shared._pages) page.Dispose();
            s_shared._pages.Clear();
            s_shared = null;
        }

        internal sealed class Page
        {
            internal readonly int Width;
            internal readonly int Height;
            internal readonly GraphicsFormat Format;
            internal readonly int Capacity;
            internal readonly long ByteSize;
            internal RenderTexture Texture { get; private set; }
            internal int UsedCount { get; private set; }
            readonly uint[] _generations;
            readonly bool[] _used;
            readonly int[] _writtenWidths;
            readonly int[] _writtenHeights;
            TextStudioAppearanceMemoryLease _memory;

            internal Page(int width, int height, GraphicsFormat format, int capacity, RenderTexture texture,
                TextStudioAppearanceMemoryLease memory, long byteSize)
            {
                Width = width;
                Height = height;
                Format = format;
                Capacity = capacity;
                Texture = texture;
                ByteSize = byteSize;
                _memory = memory;
                _generations = new uint[capacity];
                _used = new bool[capacity];
                _writtenWidths = new int[capacity];
                _writtenHeights = new int[capacity];
                ForgetWrittenExtents();
            }

            /// <summary>True when earlier writes may have left texels outside a copy of this size.</summary>
            internal bool NeedsClear(int slice, int width, int height) =>
                _writtenWidths[slice] > width || _writtenHeights[slice] > height;

            internal void MarkWritten(int slice, int width, int height)
            {
                _writtenWidths[slice] = width;
                _writtenHeights[slice] = height;
            }

            /// <summary>New or recreated storage has undefined content.</summary>
            internal void ForgetWrittenExtents()
            {
                for (int slice = 0; slice < Capacity; slice++)
                    _writtenWidths[slice] = _writtenHeights[slice] = int.MaxValue;
            }

            internal bool Matches(int width, int height, GraphicsFormat format) =>
                Texture != null && Width == width && Height == height && Format == format;

            internal bool Owns(int slice, uint generation) =>
                Texture != null && slice >= 0 && slice < Capacity && _used[slice] && _generations[slice] == generation;

            internal TextStudio2DHeroOutputLease Take(int width, int height)
            {
                int slice = Array.IndexOf(_used, false);
                _used[slice] = true;
                uint generation = ++_generations[slice];
                if (generation == 0u) generation = _generations[slice] = 1u;
                UsedCount++;
                return new TextStudio2DHeroOutputLease { Page = this, Slice = slice, Generation = generation, Width = width, Height = height };
            }

            internal void Return(int slice)
            {
                _used[slice] = false;
                _generations[slice]++;
                UsedCount--;
            }

            internal void Dispose()
            {
                Destroy(Texture);
                Texture = null;
                _memory?.Dispose();
                _memory = null;
                UsedCount = 0;
            }
        }
    }

    /// <summary>Property identifiers of the Hero output shader.</summary>
    internal static class TextStudio2DHeroOutputShader
    {
        internal static readonly int Pages = Shader.PropertyToID("_TextStudioHeroOutputPages");
        internal static readonly int ShadowPages = Shader.PropertyToID("_TextStudioHeroShadowPages");
    }
}
