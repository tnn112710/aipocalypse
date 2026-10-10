using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    internal enum TextStudio2DHeroTargetStatus : byte
    {
        Ready = 0,
        InvalidSize = 1,
        TargetCapacityExceeded = 2,
        MemoryBudgetExceeded = 3,
        UnsupportedFormat = 4,
        CreationFailed = 5
    }

    internal readonly struct TextStudio2DHeroTargetLease :
        IEquatable<TextStudio2DHeroTargetLease>
    {
        internal readonly int Slot;
        internal readonly uint Generation;

        internal TextStudio2DHeroTargetLease(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }

        internal bool IsValid => Slot >= 0 && Generation != 0u;

        public bool Equals(TextStudio2DHeroTargetLease other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudio2DHeroTargetLease other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Slot * 397 ^ (int)Generation;
            }
        }
    }

    internal readonly struct TextStudio2DHeroTargetDescriptor :
        IEquatable<TextStudio2DHeroTargetDescriptor>
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly GraphicsFormat Format;
        internal readonly GraphicsFormat DepthStencilFormat;
        internal readonly long ByteSize;

        internal TextStudio2DHeroTargetDescriptor(
            int width,
            int height,
            GraphicsFormat format,
            long byteSize, GraphicsFormat depthStencilFormat = GraphicsFormat.None)
        {
            Width = width;
            Height = height;
            Format = format;
            DepthStencilFormat = depthStencilFormat;
            ByteSize = byteSize;
        }

        public bool Equals(TextStudio2DHeroTargetDescriptor other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   Format == other.Format && DepthStencilFormat == other.DepthStencilFormat;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudio2DHeroTargetDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width;
                hash = hash * 397 ^ Height;
                hash = hash * 397 ^ (int)Format;
                return hash * 397 ^ (int)DepthStencilFormat;
            }
        }
    }

    internal readonly struct TextStudio2DHeroTargetPoolStatistics
    {
        internal readonly int ActiveTargets;
        internal readonly int AllocatedTargets;
        internal readonly long AllocatedBytes;
        internal readonly long PeakAllocatedBytes;
        internal readonly int AcquisitionCount;
        internal readonly int ReuseCount;

        internal TextStudio2DHeroTargetPoolStatistics(
            int activeTargets,
            int allocatedTargets,
            long allocatedBytes,
            long peakAllocatedBytes,
            int acquisitionCount,
            int reuseCount)
        {
            ActiveTargets = activeTargets;
            AllocatedTargets = allocatedTargets;
            AllocatedBytes = allocatedBytes;
            PeakAllocatedBytes = peakAllocatedBytes;
            AcquisitionCount = acquisitionCount;
            ReuseCount = reuseCount;
        }
    }

    /// <summary>Process-wide, generation-safe, tightly quantized Path C surface pool.</summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DHeroTargetPool : IDisposable, ITextStudioAppearanceMemoryReclaimer
    {
        internal const int MaximumTargets = 48;
        internal const int MaximumDimension = 2048;
        internal const long MaximumAllocatedBytes = 192L * 1024L * 1024L;
        const int DimensionQuantum = 8;

        static TextStudio2DHeroTargetPool s_shared;

        readonly Entry[] _entries = new Entry[MaximumTargets];
        int _activeTargets;
        int _allocatedTargets;
        long _allocatedBytes;
        long _activeBytes;
        long _peakAllocatedBytes;
        int _acquisitionCount;
        int _reuseCount;
        ulong _useSequence;
        bool _disposed;

        struct Entry
        {
            internal uint Generation;
            internal bool Active;
            internal TextStudio2DHeroTargetDescriptor Descriptor;
            internal RenderTexture Texture;
            internal TextStudioAppearanceMemoryLease Memory;
            internal ulong LastUse;
        }

        internal static TextStudio2DHeroTargetPool Shared =>
            s_shared ??= new TextStudio2DHeroTargetPool();

        internal TextStudio2DHeroTargetPoolStatistics Statistics =>
            new TextStudio2DHeroTargetPoolStatistics(
                _activeTargets,
                _allocatedTargets,
                _allocatedBytes,
                _peakAllocatedBytes,
                _acquisitionCount,
                _reuseCount);

        internal long AvailableBytes => Math.Min(MaximumAllocatedBytes - _activeBytes,
            TextStudioAppearanceMemory.AvailableGpuBytes + _allocatedBytes - _activeBytes);

        internal long GetByteSize(TextStudio2DHeroTargetLease lease) =>
            IsCurrent(lease) ? _entries[lease.Slot].Descriptor.ByteSize : 0L;

        internal static bool TryFitCompositionBudget(ref int width, ref int height,
            TextStudio2DQualityTier quality, int fullResolutionCount, int blurCount, int blurFactor, long budget,
            int replacementCount = 0, long replacementBytes = 0L, int historyCount = 0, int historyCoverageCount = 0, int depthTargetCount = 0)
        {
            if (budget <= replacementBytes || fullResolutionCount <= 0) return false;
            blurFactor = Mathf.Max(1, blurFactor);
            while (true)
            {
                if (!TryResolveDescriptor(width, height, quality, out TextStudio2DHeroTargetDescriptor full, out _) ||
                    !TryResolveDescriptor(Mathf.Max(8, (width + blurFactor - 1) / blurFactor),
                        Mathf.Max(8, (height + blurFactor - 1) / blurFactor), quality,
                        out TextStudio2DHeroTargetDescriptor blur, out _)) return false;
                long depthBytes = 0;
                if (depthTargetCount > 0)
                {
                    if (!TryResolveDescriptor(width, height, quality, out TextStudio2DHeroTargetDescriptor withDepth, out _, withDepth: true)) return false;
                    depthBytes = (withDepth.ByteSize - full.ByteSize) * depthTargetCount;
                }
                long historyBytes = 0;
                if (historyCount > 0 || historyCoverageCount > 0)
                {
                    int historyWidth = Mathf.Max(8, (width + 1) / 2), historyHeight = Mathf.Max(8, (height + 1) / 2);
                    if (!TryResolveDescriptor(historyWidth, historyHeight, quality, out TextStudio2DHeroTargetDescriptor history, out _) ||
                        !TryResolveDescriptor(historyWidth, historyHeight, quality, out TextStudio2DHeroTargetDescriptor coverage, out _, true)) return false;
                    historyBytes = history.ByteSize * historyCount + coverage.ByteSize * historyCoverageCount;
                }
                // Completed compositions persist in output pages, whose bucketed slices can exceed the composition target.
                long outputBytes = TextStudio2DHeroOutputPages.IsSupported
                    ? Math.Max(full.ByteSize, TextStudio2DHeroOutputPages.ReservationBytes(full.Width, full.Height, full.Format))
                    : full.ByteSize;
                long bytes = full.ByteSize * fullResolutionCount + blur.ByteSize * blurCount + historyBytes + depthBytes +
                             Math.Max(replacementBytes, outputBytes * replacementCount + historyBytes);
                if (bytes <= budget) return true;
                if (width <= DimensionQuantum && height <= DimensionQuantum) return false;
                float scale = Mathf.Min(0.99f, Mathf.Sqrt(budget / (float)bytes));
                width = Mathf.Max(DimensionQuantum, Mathf.FloorToInt(width * scale / DimensionQuantum) * DimensionQuantum);
                height = Mathf.Max(DimensionQuantum, Mathf.FloorToInt(height * scale / DimensionQuantum) * DimensionQuantum);
            }
        }

        internal TextStudio2DHeroTargetPool()
        {
            TextStudioAppearanceMemory.RegisterIdleOwner(this);
            for (int index = 0; index < _entries.Length; index++)
                _entries[index].Generation = 1u;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void ResetRuntimeState()
        {
            s_shared?.Dispose();
            s_shared = null;
        }

        internal static void ResetForTests()
        {
            ResetRuntimeState();
        }

        internal bool TryAcquire(
            int width,
            int height,
            TextStudio2DQualityTier qualityTier,
            out TextStudio2DHeroTargetLease lease,
            out TextStudio2DHeroTargetStatus status,
            bool scalarCoverage = false, bool withDepth = false)
        {
            ThrowIfDisposed();
            lease = default;
            if (!TryResolveDescriptor(
                    width,
                    height,
                    qualityTier,
                    out TextStudio2DHeroTargetDescriptor descriptor,
                    out status, scalarCoverage, withDepth))
            {
                return false;
            }

            _acquisitionCount++;
            _useSequence++;
            for (int index = 0; index < _entries.Length; index++)
            {
                Entry entry = _entries[index];
                if (entry.Active ||
                    entry.Texture == null ||
                    !entry.Descriptor.Equals(descriptor))
                {
                    continue;
                }
                if (!entry.Texture.IsCreated() && !entry.Texture.Create())
                {
                    DestroyEntry(index);
                    continue;
                }
                entry.Active = true;
                entry.Memory.SetIdle(false);
                entry.LastUse = _useSequence;
                _entries[index] = entry;
                _activeTargets++;
                _activeBytes += entry.Descriptor.ByteSize;
                _reuseCount++;
                TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.HeroTargets, true);
                lease = new TextStudio2DHeroTargetLease(index, entry.Generation);
                status = TextStudio2DHeroTargetStatus.Ready;
                return true;
            }

            int slot = FindFreeOrEvictableSlot(descriptor.ByteSize, out status);
            if (slot < 0)
                return false;

            TextStudioAppearanceMemory.RecordCacheAccess(TextStudioAppearanceResourceKind.HeroTargets, false);
            if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.HeroTargets, "Hero render target",
                    descriptor.ByteSize, 0, out TextStudioAppearanceMemoryLease memory))
            {
                status = TextStudio2DHeroTargetStatus.MemoryBudgetExceeded;
                return false;
            }
            RenderTexture texture;
            try { texture = CreateTarget(in descriptor); }
            catch { memory.Dispose(); status = TextStudio2DHeroTargetStatus.CreationFailed; return false; }
            if (texture == null)
            {
                memory.Dispose();
                status = TextStudio2DHeroTargetStatus.CreationFailed;
                return false;
            }
            memory.Commit();

            Entry created = _entries[slot];
            created.Active = true;
            created.Descriptor = descriptor;
            created.Texture = texture;
            created.Memory = memory;
            created.LastUse = _useSequence;
            _entries[slot] = created;
            _activeTargets++;
            _activeBytes += descriptor.ByteSize;
            _allocatedTargets++;
            _allocatedBytes += descriptor.ByteSize;
            _peakAllocatedBytes = Math.Max(_peakAllocatedBytes, _allocatedBytes);
            lease = new TextStudio2DHeroTargetLease(slot, created.Generation);
            status = TextStudio2DHeroTargetStatus.Ready;
            return true;
        }

        internal RenderTexture GetTexture(TextStudio2DHeroTargetLease lease)
        {
            return IsCurrent(lease) ? _entries[lease.Slot].Texture : null;
        }

        internal bool IsCurrent(TextStudio2DHeroTargetLease lease)
        {
            return !_disposed &&
                   lease.IsValid &&
                   lease.Slot < _entries.Length &&
                   _entries[lease.Slot].Active &&
                   _entries[lease.Slot].Generation == lease.Generation;
        }

        internal void Release(ref TextStudio2DHeroTargetLease lease)
        {
            if (!IsCurrent(lease))
            {
                lease = default;
                return;
            }
            Entry entry = _entries[lease.Slot];
            entry.Active = false;
            entry.Memory.SetIdle(true);
            entry.LastUse = ++_useSequence;
            entry.Generation++;
            if (entry.Generation == 0u)
                entry.Generation = 1u;
            _entries[lease.Slot] = entry;
            _activeTargets--;
            _activeBytes -= entry.Descriptor.ByteSize;
            lease = default;
        }

        internal void TrimUnused(long targetBytes = 0L)
        {
            ThrowIfDisposed();
            long boundedTarget = Math.Max(0L, targetBytes);
            while (_allocatedBytes > boundedTarget)
            {
                int oldest = FindOldestInactiveSlot();
                if (oldest < 0)
                    break;
                DestroyEntry(oldest);
            }
        }

        void ITextStudioAppearanceMemoryReclaimer.ReclaimUnused(long gpuBytes, long cpuBytes)
        {
            if (!_disposed && gpuBytes > 0) TrimUnused(Math.Max(0, _allocatedBytes - gpuBytes));
        }

        internal static bool TryResolveDescriptor(
            int width,
            int height,
            TextStudio2DQualityTier qualityTier,
            out TextStudio2DHeroTargetDescriptor descriptor,
            out TextStudio2DHeroTargetStatus status,
            bool scalarCoverage = false, bool withDepth = false)
        {
            descriptor = default;
            if (width <= 0 || height <= 0)
            {
                status = TextStudio2DHeroTargetStatus.InvalidSize;
                return false;
            }

            int normalizedWidth = NormalizeDimension(width);
            int normalizedHeight = NormalizeDimension(height);
            GraphicsFormat preferred = scalarCoverage ? GraphicsFormat.R8_UNorm : qualityTier == TextStudio2DQualityTier.Hero
                ? GraphicsFormat.R16G16B16A16_SFloat
                : GraphicsFormat.R8G8B8A8_UNorm;
            GraphicsFormat format = SystemInfo.IsFormatSupported(
                preferred,
                GraphicsFormatUsage.Render)
                ? preferred
                : GraphicsFormat.R8G8B8A8_UNorm;
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
            {
                status = TextStudio2DHeroTargetStatus.UnsupportedFormat;
                return false;
            }

            GraphicsFormat depthFormat = GraphicsFormat.None;
            if (withDepth)
            {
                depthFormat = SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.D32_SFloat : GraphicsFormat.D24_UNorm_S8_UInt;
                if (!SystemInfo.IsFormatSupported(depthFormat, GraphicsFormatUsage.Render))
                {
                    status = TextStudio2DHeroTargetStatus.UnsupportedFormat;
                    return false;
                }
            }
            long bytesPerPixel = GraphicsFormatUtility.GetBlockSize(format) +
                                 (depthFormat != GraphicsFormat.None ? GraphicsFormatUtility.GetBlockSize(depthFormat) : 0);
            long byteSize = (long)normalizedWidth * normalizedHeight * bytesPerPixel;
            descriptor = new TextStudio2DHeroTargetDescriptor(
                normalizedWidth,
                normalizedHeight,
                format,
                byteSize, depthFormat);
            status = TextStudio2DHeroTargetStatus.Ready;
            return true;
        }

        static int NormalizeDimension(int value)
        {
            int clamped = Mathf.Clamp(value, DimensionQuantum, MaximumDimension);
            return Mathf.Min(
                MaximumDimension,
                (clamped + DimensionQuantum - 1) / DimensionQuantum * DimensionQuantum);
        }

        int FindFreeOrEvictableSlot(
            long requestedBytes,
            out TextStudio2DHeroTargetStatus status)
        {
            int empty = -1;
            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].Texture == null && !_entries[index].Active)
                {
                    empty = index;
                    break;
                }
            }

            while (_allocatedBytes + requestedBytes > MaximumAllocatedBytes)
            {
                int oldest = FindOldestInactiveSlot();
                if (oldest < 0)
                {
                    status = TextStudio2DHeroTargetStatus.MemoryBudgetExceeded;
                    return -1;
                }
                DestroyEntry(oldest);
                if (empty < 0)
                    empty = oldest;
            }

            if (empty >= 0)
            {
                status = TextStudio2DHeroTargetStatus.Ready;
                return empty;
            }

            int evictable = FindOldestInactiveSlot();
            if (evictable < 0)
            {
                status = TextStudio2DHeroTargetStatus.TargetCapacityExceeded;
                return -1;
            }
            DestroyEntry(evictable);
            status = TextStudio2DHeroTargetStatus.Ready;
            return evictable;
        }

        int FindOldestInactiveSlot()
        {
            int result = -1;
            ulong oldestUse = ulong.MaxValue;
            for (int index = 0; index < _entries.Length; index++)
            {
                Entry entry = _entries[index];
                if (entry.Active || entry.Texture == null || entry.LastUse >= oldestUse)
                    continue;
                oldestUse = entry.LastUse;
                result = index;
            }
            return result;
        }

        static RenderTexture CreateTarget(in TextStudio2DHeroTargetDescriptor descriptor)
        {
            RenderTextureDescriptor unityDescriptor = new RenderTextureDescriptor(
                descriptor.Width,
                descriptor.Height)
            {
                graphicsFormat = descriptor.Format,
                depthStencilFormat = descriptor.DepthStencilFormat,
                msaaSamples = 1,
                volumeDepth = 1,
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false,
                sRGB = false,
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D
            };
            RenderTexture texture = new RenderTexture(unityDescriptor)
            {
                name = "Text Studio Hero Surface " +
                       descriptor.Width + "x" + descriptor.Height,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                if (texture.Create()) return texture;
                Destroy(texture);
                return null;
            }
            catch { Destroy(texture); throw; }
        }

        void DestroyEntry(int slot)
        {
            Entry entry = _entries[slot];
            if (entry.Active)
                return;
            if (entry.Texture != null)
            {
                TextStudio2DHeroSubmission.Flush();
                _allocatedTargets--;
                _allocatedBytes -= entry.Descriptor.ByteSize;
                entry.Texture.Release();
                Destroy(entry.Texture);
                TextStudioAppearanceMemory.RecordEviction(TextStudioAppearanceResourceKind.HeroTargets);
            }
            entry.Memory?.Dispose();
            entry.Memory = null;
            entry.Texture = null;
            entry.Descriptor = default;
            entry.LastUse = 0u;
            entry.Generation++;
            if (entry.Generation == 0u)
                entry.Generation = 1u;
            _entries[slot] = entry;
        }

        static void Destroy(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            TextStudioAppearanceMemory.UnregisterIdleOwner(this);
            TextStudio2DHeroSubmission.Flush();
            for (int index = 0; index < _entries.Length; index++)
            {
                Entry entry = _entries[index];
                if (entry.Texture != null)
                {
                    entry.Texture.Release();
                    Destroy(entry.Texture);
                }
                entry.Memory?.Dispose();
                _entries[index] = default;
            }
            _activeTargets = 0;
            _allocatedTargets = 0;
            _allocatedBytes = 0L;
            _activeBytes = 0L;
            _disposed = true;
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextStudio2DHeroTargetPool));
        }
    }
}
