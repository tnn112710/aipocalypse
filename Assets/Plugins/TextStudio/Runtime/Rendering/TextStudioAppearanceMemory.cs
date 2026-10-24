using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace TextStudio
{
    /// <summary>Classifies appearance memory allocations as source fields, paint textures, geometry, tables, hero targets, or preparation data.</summary>
    public enum TextStudioAppearanceResourceKind
    {
        Fields,
        PaintTextures,
        Geometry,
        Tables,
        HeroTargets,
        Preparation
    }

    /// <summary>Application-wide envelopes for owned appearance payloads, including pending replacements.</summary>
    public readonly struct TextStudioAppearanceMemoryBudget
    {
        public long GpuBytes { get; }
        public long CpuBytes { get; }
        public static TextStudioAppearanceMemoryBudget UI => new TextStudioAppearanceMemoryBudget(64L << 20, 32L << 20);
        public static TextStudioAppearanceMemoryBudget Titles => new TextStudioAppearanceMemoryBudget(192L << 20, 64L << 20);

        /// <summary>Creates an owned-appearance payload budget requiring positive GPU and CPU byte limits.</summary>
        public TextStudioAppearanceMemoryBudget(long gpuBytes, long cpuBytes)
        {
            if (gpuBytes <= 0 || cpuBytes <= 0) throw new ArgumentOutOfRangeException(nameof(gpuBytes));
            GpuBytes = gpuBytes;
            CpuBytes = cpuBytes;
        }
    }

    /// <summary>A snapshot of owned, pending, idle, peak, and borrowed appearance payloads with allocation, upload, cache, and eviction counters.</summary>
    public readonly struct TextStudioAppearanceMemorySnapshot
    {
        public long OwnedGpuBytes { get; }
        public long OwnedCpuBytes { get; }
        public long PendingGpuBytes { get; }
        public long PendingCpuBytes { get; }
        public long IdleGpuBytes { get; }
        public long IdleCpuBytes { get; }
        public long PeakGpuBytes { get; }
        public long PeakCpuBytes { get; }
        public long BorrowedTextureBytes { get; }
        public int BorrowedTextureCount { get; }
        public int ActiveAllocations { get; }
        public int IdleAllocations { get; }
        public int PendingAllocations { get; }
        public long UploadedBytes { get; }
        public long CacheHits { get; }
        public long CacheMisses { get; }
        public long Evictions { get; }

        internal TextStudioAppearanceMemorySnapshot(in TextStudioAppearanceMemoryCounters counters,
            long borrowedBytes = 0, int borrowedCount = 0)
        {
            OwnedGpuBytes = counters.Gpu; OwnedCpuBytes = counters.Cpu;
            PendingGpuBytes = counters.PendingGpu; PendingCpuBytes = counters.PendingCpu;
            IdleGpuBytes = counters.IdleGpu; IdleCpuBytes = counters.IdleCpu;
            PeakGpuBytes = counters.PeakGpu; PeakCpuBytes = counters.PeakCpu;
            ActiveAllocations = counters.Active; IdleAllocations = counters.Idle; PendingAllocations = counters.Pending;
            BorrowedTextureBytes = borrowedBytes; BorrowedTextureCount = borrowedCount;
            UploadedBytes = counters.Uploaded; CacheHits = counters.Hits; CacheMisses = counters.Misses; Evictions = counters.Evictions;
        }
    }

    /// <summary>Details of a rejected appearance reservation, including requested GPU and CPU bytes and the available budget.</summary>
    public readonly struct TextStudioAppearanceMemoryRejection
    {
        public TextStudioAppearanceResourceKind Kind { get; }
        public string Resource { get; }
        public long RequestedGpuBytes { get; }
        public long RequestedCpuBytes { get; }
        public long AvailableGpuBytes { get; }
        public long AvailableCpuBytes { get; }
        public bool HasValue => Resource != null;

        internal TextStudioAppearanceMemoryRejection(TextStudioAppearanceResourceKind kind, string resource,
            long gpu, long cpu, long availableGpu, long availableCpu)
        {
            Kind = kind; Resource = resource; RequestedGpuBytes = gpu; RequestedCpuBytes = cpu;
            AvailableGpuBytes = availableGpu; AvailableCpuBytes = availableCpu;
        }

        public override string ToString() => HasValue
            ? $"{Resource} needs {RequestedGpuBytes:N0} GPU bytes and {RequestedCpuBytes:N0} CPU bytes; the appearance budget has {AvailableGpuBytes:N0} GPU bytes and {AvailableCpuBytes:N0} CPU bytes available."
            : string.Empty;
    }

    /// <summary>Accounts payload capacity, not Unity/driver object overhead or the process managed heap. Borrowed textures are reported once and are never owned or destroyed here.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioAppearanceMemory
    {
        // The ledger follows explicit resource-owner lifetimes, including pending workers during session transitions.
        static readonly TextStudioAppearanceMemoryAccountant s_accountant = new TextStudioAppearanceMemoryAccountant(TextStudioAppearanceMemoryBudget.Titles);
        static readonly List<ITextStudioAppearanceMemoryReclaimer> s_idleOwners = new List<ITextStudioAppearanceMemoryReclaimer>();
        public static TextStudioAppearanceMemoryBudget Budget => s_accountant.Budget;
        public static TextStudioAppearanceMemorySnapshot Statistics => s_accountant.Statistics;
        public static TextStudioAppearanceMemoryRejection LastRejection => s_accountant.LastRejection;
        /// <summary>Returns appearance payload and activity statistics for the supplied resource category.</summary>
        public static TextStudioAppearanceMemorySnapshot GetStatistics(TextStudioAppearanceResourceKind kind) => s_accountant.GetStatistics(kind);
        /// <summary>Attempts to set the owned-payload budget, reclaiming unused resources when current allocations initially exceed the requested limits.</summary>
        public static bool TrySetBudget(TextStudioAppearanceMemoryBudget budget)
        {
            if (s_accountant.TrySetBudget(budget)) return true;
            TextStudioAppearanceMemorySnapshot stats = s_accountant.Statistics;
            ReclaimUnused(Math.Max(0, stats.OwnedGpuBytes - budget.GpuBytes), Math.Max(0, stats.OwnedCpuBytes - budget.CpuBytes));
            return s_accountant.TrySetBudget(budget);
        }
        /// <summary>Resets peak usage to current ownership, clears activity counters and the last rejection, and retains existing allocations.</summary>
        public static void ResetStatistics() => s_accountant.ResetStatistics();
        internal static long AvailableGpuBytes => s_accountant.AvailableGpuBytes;
        internal static uint AvailabilityRevision => s_accountant.AvailabilityRevision;
        internal static bool TryReserve(TextStudioAppearanceResourceKind kind, string resource, long gpuBytes, long cpuBytes,
            out TextStudioAppearanceMemoryLease lease)
        {
            if (s_accountant.TryReserve(kind, resource, gpuBytes, cpuBytes, out lease)) return true;
            TextStudioAppearanceMemorySnapshot stats = s_accountant.Statistics;
            ReclaimUnused(Math.Max(0, gpuBytes - Math.Max(0, Budget.GpuBytes - stats.OwnedGpuBytes)),
                Math.Max(0, cpuBytes - Math.Max(0, Budget.CpuBytes - stats.OwnedCpuBytes)));
            return s_accountant.TryReserve(kind, resource, gpuBytes, cpuBytes, out lease);
        }
        static void ReclaimUnused(long gpuNeeded, long cpuNeeded)
        {
            for (int index = 0; index < s_idleOwners.Count; index++)
            {
                if (gpuNeeded <= 0 && cpuNeeded <= 0) break;
                TextStudioAppearanceMemorySnapshot before = s_accountant.Statistics;
                s_idleOwners[index].ReclaimUnused(gpuNeeded, cpuNeeded);
                TextStudioAppearanceMemorySnapshot after = s_accountant.Statistics;
                gpuNeeded = Math.Max(0, gpuNeeded - (before.OwnedGpuBytes - after.OwnedGpuBytes));
                cpuNeeded = Math.Max(0, cpuNeeded - (before.OwnedCpuBytes - after.OwnedCpuBytes));
            }
        }
        internal static void RegisterIdleOwner(ITextStudioAppearanceMemoryReclaimer owner) => s_idleOwners.Add(owner);
        internal static void UnregisterIdleOwner(ITextStudioAppearanceMemoryReclaimer owner) => s_idleOwners.Remove(owner);
        internal static void RecordUpload(TextStudioAppearanceResourceKind kind, long bytes) => s_accountant.RecordUpload(kind, bytes);
        internal static void RecordCacheAccess(TextStudioAppearanceResourceKind kind, bool hit) => s_accountant.RecordCacheAccess(kind, hit);
        internal static void RecordEviction(TextStudioAppearanceResourceKind kind) => s_accountant.RecordEviction(kind);
        internal static TextStudioBorrowedTextureLease Borrow(Texture texture) => texture == null ? null : s_accountant.Borrow(texture.GetStableId(), TextureBytes(texture));

        internal static long TextureBytes(Texture texture)
        {
            if (texture == null || texture.graphicsFormat == GraphicsFormat.None) return 0;
            int slices = texture is Texture2DArray array ? array.depth : texture is Cubemap ? 6 : 1;
            return TextureBytes(texture.width, texture.height, slices, texture.mipmapCount, texture.graphicsFormat);
        }

        internal static long TextureBytes(int width, int height, int slices, int mipCount, GraphicsFormat format)
        {
            long blockWidth = GraphicsFormatUtility.GetBlockWidth(format);
            long blockHeight = GraphicsFormatUtility.GetBlockHeight(format);
            long blockBytes = GraphicsFormatUtility.GetBlockSize(format);
            if (width <= 0 || height <= 0 || slices <= 0 || blockWidth <= 0 || blockHeight <= 0) return 0;
            long bytes = 0;
            for (int mip = 0; mip < Math.Max(1, mipCount); mip++)
            {
                bytes = checked(bytes + ((width + blockWidth - 1) / blockWidth) * ((height + blockHeight - 1) / blockHeight) * blockBytes * slices);
                width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
            }
            return bytes;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetSession()
        {
            s_accountant.ResetSession();
        }
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnEnteringPlayMode]
        static void OnEnteringPlayMode() => ResetSession();
        [UnityEngine.OnExitingPlayMode]
        static void OnExitingPlayMode() => ResetSession();
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading() => ResetSession();
#endif
    }

    internal struct TextStudioAppearanceMemoryCounters
    {
        internal long Gpu, Cpu, PendingGpu, PendingCpu, IdleGpu, IdleCpu, PeakGpu, PeakCpu, Uploaded, Hits, Misses, Evictions;
        internal int Active, Idle, Pending;
        internal void Add(long gpu, long cpu, TextStudioAppearanceMemoryState state, int count)
        {
            Gpu += gpu; Cpu += cpu;
            if (state == TextStudioAppearanceMemoryState.Pending) { PendingGpu += gpu; PendingCpu += cpu; Pending += count; }
            else if (state == TextStudioAppearanceMemoryState.Idle) { IdleGpu += gpu; IdleCpu += cpu; Idle += count; }
            else Active += count;
            PeakGpu = Math.Max(PeakGpu, Gpu); PeakCpu = Math.Max(PeakCpu, Cpu);
        }
        internal void ResetStatistics()
        {
            PeakGpu = Gpu; PeakCpu = Cpu; Uploaded = Hits = Misses = Evictions = 0;
        }
    }

    internal enum TextStudioAppearanceMemoryState { Pending, Active, Idle, Released }

    internal interface ITextStudioAppearanceMemoryReclaimer
    {
        void ReclaimUnused(long gpuBytes, long cpuBytes);
    }

    internal sealed class TextStudioAppearanceMemoryLease : IDisposable
    {
        internal readonly TextStudioAppearanceMemoryAccountant Owner;
        internal readonly TextStudioAppearanceResourceKind Kind;
        internal long Gpu, Cpu;
        internal TextStudioAppearanceMemoryState State;
        internal TextStudioAppearanceMemoryLease(TextStudioAppearanceMemoryAccountant owner, TextStudioAppearanceResourceKind kind, long gpu, long cpu)
        { Owner = owner; Kind = kind; Gpu = gpu; Cpu = cpu; State = TextStudioAppearanceMemoryState.Pending; }
        internal void Commit() => Owner.Commit(this, Gpu, Cpu);
        internal void Commit(long gpuBytes, long cpuBytes) => Owner.Commit(this, gpuBytes, cpuBytes);
        internal void Reduce(long gpuBytes, long cpuBytes) => Owner.Commit(this, gpuBytes, cpuBytes, false);
        internal void SetIdle(bool idle) => Owner.SetIdle(this, idle);
        public void Dispose() => Owner.Release(this);
    }

    internal sealed class TextStudioBorrowedTextureLease : IDisposable
    {
        TextStudioAppearanceMemoryAccountant _owner;
        internal readonly int Identity;
        internal TextStudioBorrowedTextureLease(TextStudioAppearanceMemoryAccountant owner, int identity) { _owner = owner; Identity = identity; }
        public void Dispose()
        {
            TextStudioAppearanceMemoryAccountant owner = System.Threading.Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseBorrow(Identity);
        }
    }

    internal sealed class TextStudioAppearanceMemoryAccountant
    {
        readonly object _gate = new object();
        readonly TextStudioAppearanceMemoryCounters[] _categories = new TextStudioAppearanceMemoryCounters[6];
        readonly Dictionary<int, (int references, long bytes)> _borrowed = new Dictionary<int, (int, long)>();
        TextStudioAppearanceMemoryCounters _total;
        TextStudioAppearanceMemoryBudget _budget;
        TextStudioAppearanceMemoryRejection _lastRejection;
        long _borrowedBytes;
        uint _availabilityRevision;
        internal TextStudioAppearanceMemoryAccountant(TextStudioAppearanceMemoryBudget budget)
        {
            ValidateBudget(budget); _budget = budget;
        }
        internal TextStudioAppearanceMemoryBudget Budget { get { lock (_gate) return _budget; } }
        internal TextStudioAppearanceMemorySnapshot Statistics { get { lock (_gate) return new TextStudioAppearanceMemorySnapshot(in _total, _borrowedBytes, _borrowed.Count); } }
        internal TextStudioAppearanceMemoryRejection LastRejection { get { lock (_gate) return _lastRejection; } }
        internal long AvailableGpuBytes { get { lock (_gate) return Math.Max(0, _budget.GpuBytes - _total.Gpu); } }
        internal uint AvailabilityRevision { get { lock (_gate) return _availabilityRevision; } }
        internal TextStudioAppearanceMemorySnapshot GetStatistics(TextStudioAppearanceResourceKind kind)
        {
            ValidateKind(kind);
            lock (_gate) return new TextStudioAppearanceMemorySnapshot(in _categories[(int)kind]);
        }

        internal bool TrySetBudget(TextStudioAppearanceMemoryBudget budget)
        {
            ValidateBudget(budget);
            lock (_gate)
            {
                if (budget.GpuBytes < _total.Gpu || budget.CpuBytes < _total.Cpu) return false;
                _budget = budget; _availabilityRevision++; return true;
            }
        }

        internal bool TryReserve(TextStudioAppearanceResourceKind kind, string resource, long gpu, long cpu, out TextStudioAppearanceMemoryLease lease)
        {
            ValidateKind(kind);
            if (gpu < 0 || cpu < 0 || gpu == 0 && cpu == 0) throw new ArgumentOutOfRangeException(nameof(gpu));
            if (string.IsNullOrEmpty(resource)) throw new ArgumentException("A resource name is required.", nameof(resource));
            lease = null;
            lock (_gate)
            {
                long availableGpu = Math.Max(0, _budget.GpuBytes - _total.Gpu);
                long availableCpu = Math.Max(0, _budget.CpuBytes - _total.Cpu);
                if (gpu > availableGpu || cpu > availableCpu)
                {
                    _lastRejection = new TextStudioAppearanceMemoryRejection(kind, resource, gpu, cpu, availableGpu, availableCpu);
                    return false;
                }
                lease = new TextStudioAppearanceMemoryLease(this, kind, gpu, cpu);
                Add(lease, 1); return true;
            }
        }

        internal void Commit(TextStudioAppearanceMemoryLease lease, long gpu, long cpu, bool publish = true)
        {
            lock (_gate)
            {
                if (lease.State == TextStudioAppearanceMemoryState.Released) return;
                if (gpu < 0 || cpu < 0 || gpu > lease.Gpu || cpu > lease.Cpu) throw new ArgumentOutOfRangeException(nameof(gpu));
                Add(lease, -1);
                if (gpu < lease.Gpu || cpu < lease.Cpu) _availabilityRevision++;
                lease.Gpu = gpu; lease.Cpu = cpu;
                if (publish) lease.State = TextStudioAppearanceMemoryState.Active;
                Add(lease, 1);
            }
        }

        internal void SetIdle(TextStudioAppearanceMemoryLease lease, bool idle)
        {
            lock (_gate)
            {
                if (lease.State == TextStudioAppearanceMemoryState.Released || lease.State == TextStudioAppearanceMemoryState.Pending) return;
                TextStudioAppearanceMemoryState state = idle ? TextStudioAppearanceMemoryState.Idle : TextStudioAppearanceMemoryState.Active;
                if (lease.State == state) return;
                Add(lease, -1); lease.State = state; Add(lease, 1);
                if (idle) _availabilityRevision++;
            }
        }

        internal void Release(TextStudioAppearanceMemoryLease lease)
        {
            lock (_gate)
            {
                if (lease.State == TextStudioAppearanceMemoryState.Released) return;
                Add(lease, -1); lease.State = TextStudioAppearanceMemoryState.Released; _availabilityRevision++;
            }
        }

        void Add(TextStudioAppearanceMemoryLease lease, int direction)
        {
            _total.Add(direction * lease.Gpu, direction * lease.Cpu, lease.State, direction);
            _categories[(int)lease.Kind].Add(direction * lease.Gpu, direction * lease.Cpu, lease.State, direction);
        }

        internal TextStudioBorrowedTextureLease Borrow(int identity, long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            lock (_gate)
            {
                _borrowed.TryGetValue(identity, out (int references, long bytes) entry);
                long currentBytes = Math.Max(entry.bytes, bytes);
                _borrowedBytes += currentBytes - entry.bytes;
                _borrowed[identity] = (entry.references + 1, currentBytes);
                return new TextStudioBorrowedTextureLease(this, identity);
            }
        }

        internal void ReleaseBorrow(int identity)
        {
            lock (_gate)
            {
                if (!_borrowed.TryGetValue(identity, out (int references, long bytes) entry)) return;
                if (entry.references == 1) { _borrowed.Remove(identity); _borrowedBytes -= entry.bytes; }
                else _borrowed[identity] = (entry.references - 1, entry.bytes);
            }
        }

        internal void RecordUpload(TextStudioAppearanceResourceKind kind, long bytes)
        {
            ValidateKind(kind);
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            lock (_gate) { _total.Uploaded += bytes; _categories[(int)kind].Uploaded += bytes; }
        }
        internal void RecordCacheAccess(TextStudioAppearanceResourceKind kind, bool hit)
        {
            ValidateKind(kind);
            lock (_gate)
            {
                if (hit) { _total.Hits++; _categories[(int)kind].Hits++; }
                else { _total.Misses++; _categories[(int)kind].Misses++; }
            }
        }
        internal void RecordEviction(TextStudioAppearanceResourceKind kind)
        {
            ValidateKind(kind);
            lock (_gate) { _total.Evictions++; _categories[(int)kind].Evictions++; }
        }
        internal void ResetStatistics()
        {
            lock (_gate)
            {
                _total.ResetStatistics();
                for (int index = 0; index < _categories.Length; index++) _categories[index].ResetStatistics();
                _lastRejection = default;
            }
        }
        internal void ResetSession()
        {
            lock (_gate)
            {
                _budget = TextStudioAppearanceMemoryBudget.Titles;
                ResetStatistics(); _availabilityRevision++;
            }
        }
        static void ValidateBudget(TextStudioAppearanceMemoryBudget budget)
        {
            if (budget.GpuBytes <= 0 || budget.CpuBytes <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
        }
        static void ValidateKind(TextStudioAppearanceResourceKind kind)
        {
            if (kind < TextStudioAppearanceResourceKind.Fields || kind > TextStudioAppearanceResourceKind.Preparation) throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
