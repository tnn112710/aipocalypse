using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    internal readonly struct TextStudio2DSurfaceHandle
    {
        internal readonly int Slot;
        internal readonly uint Generation;
        internal readonly TextStudio2DSurfaceTable Owner;
        internal bool IsValid => Slot > 0 && Generation > 0;
        internal TextStudio2DSurfaceHandle(int slot, uint generation, TextStudio2DSurfaceTable owner) { Slot = slot; Generation = generation; Owner = owner; }
    }

    /// <summary>Bounded shared surface records. A renderer owns its handles and releases them with its retained geometry.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DSurfaceTable : IDisposable, ITextStudioAppearanceMemoryReclaimer
    {
        internal const int MaximumSurfaces = 1024;
        internal const int MaximumEdges = 65536;
        internal const int HeaderStride = 6;
        static TextStudio2DSurfaceTable s_shared;
        static readonly int s_headersProperty = Shader.PropertyToID("_TextStudio2DSurfaceHeaders");
        static readonly int s_edgesProperty = Shader.PropertyToID("_TextStudio2DSurfaceEdges");
        static readonly int s_inkSourcesProperty = Shader.PropertyToID("_TextStudio2DSurfaceInkSources");
        static readonly int s_enabledProperty = Shader.PropertyToID("_TextStudio2DSurfacesEnabled");
        readonly Entry[] _entries = new Entry[MaximumSurfaces];
        readonly Stack<int> _free = new Stack<int>(MaximumSurfaces);
        readonly TextStudioAppearanceBuffer<Vector4> _headerStorage =
            new TextStudioAppearanceBuffer<Vector4>(TextStudioAppearanceResourceKind.Geometry, "Surface headers");
        readonly TextStudioAppearanceBuffer<Vector4> _edgeStorage =
            new TextStudioAppearanceBuffer<Vector4>(TextStudioAppearanceResourceKind.Geometry, "Surface edges");
        readonly TextStudioAppearanceBuffer<Vector4> _inkStorage =
            new TextStudioAppearanceBuffer<Vector4>(TextStudioAppearanceResourceKind.Geometry, "Surface native ink sources");
        Vector4[] _inkRecords => _inkStorage.Data;
        Vector4[] _headers => _headerStorage.Data;
        Vector4[] _edges => _edgeStorage.Data;
        static readonly int s_headersData = Shader.PropertyToID("_TextStudio2DSurfaceHeadersData");
        static readonly int s_edgesData = Shader.PropertyToID("_TextStudio2DSurfaceEdgesData");
        static readonly int s_inkSourcesData = Shader.PropertyToID("_TextStudio2DSurfaceInkSourcesData");
        bool _preparing;
        internal bool MemoryBudgetExceeded { get; private set; }
        bool _dirty;
        bool _disposed;
        int _activeCount;
        int _activeEdges;
        int _activeInkRecords;
        internal int PublicationCount { get; private set; }
        internal static TextStudio2DSurfaceTable Shared => s_shared ??= new TextStudio2DSurfaceTable();
        internal int ActiveCount => _activeCount;
        internal long CpuBytes => (_headers.LongLength + _edges.LongLength + _inkRecords.LongLength) * 16;
        internal long GpuBytes => _headerStorage.GpuBytes + _edgeStorage.GpuBytes + _inkStorage.GpuBytes;

        internal TextStudio2DSurfaceTable()
        {
            for (int slot = MaximumSurfaces - 1; slot > 0; slot--) _free.Push(slot);
            TextStudioAppearanceMemory.RegisterIdleOwner(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ReleaseShared()
        {
            s_shared?.Dispose();
            s_shared = null;
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void OnExitingPlayMode() => ReleaseShared();
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading() => ReleaseShared();
#endif

        internal bool TryUpdate(ref TextStudio2DSurfaceHandle handle, TextStudio2DSurfaceGeometry geometry, Vector4 adjustmentMask)
        {
            _preparing = true;
            MemoryBudgetExceeded = false;
            try { return TryUpdateCore(ref handle, geometry, adjustmentMask); }
            finally
            {
                _preparing = false;
                _headerStorage.SetIdle(_activeCount == 0);
                _edgeStorage.SetIdle(_activeCount == 0);
                _inkStorage.SetIdle(_activeInkRecords == 0);
            }
        }

        bool TryUpdateCore(ref TextStudio2DSurfaceHandle handle, TextStudio2DSurfaceGeometry geometry, Vector4 adjustmentMask)
        {
            if (_disposed || geometry == null) return false;
            Entry entry = IsCurrent(handle) ? _entries[handle.Slot] : null;
            if (entry?.Geometry?.Fingerprint == geometry.Fingerprint && entry.AdjustmentMask == adjustmentMask) return true;
            int previousEdges = entry?.Geometry?.Edges.Length ?? 0;
            int previousInkRecords = (entry?.Geometry?.NativeInkSources.Length ?? 0) * TextStudio2DNativeInkSource.RecordStride;
            int newInkRecords = geometry.NativeInkSources.Length * TextStudio2DNativeInkSource.RecordStride;
            int requiredInkRecords = _activeInkRecords - previousInkRecords + newInkRecords;
            if (_activeEdges - previousEdges + geometry.Edges.Length > MaximumEdges || entry == null && _free.Count == 0) return false;
            int lastSlot = entry == null ? _free.Peek() : handle.Slot;
            for (int slot = MaximumSurfaces - 1; slot > lastSlot; slot--)
                if (_entries[slot]?.Active == true) { lastSlot = slot; break; }
            if (!_headerStorage.TryEnsureCapacity(Mathf.NextPowerOfTwo((lastSlot + 1) * HeaderStride)) ||
                !_edgeStorage.TryEnsureCapacity(Mathf.NextPowerOfTwo(Mathf.Max(1, _activeEdges - previousEdges + geometry.Edges.Length))) ||
                requiredInkRecords > 0 && !_inkStorage.TryEnsureCapacity(Mathf.NextPowerOfTwo(requiredInkRecords)))
            {
                MemoryBudgetExceeded = true;
                return false;
            }
            if (entry == null)
            {
                int slot = _free.Pop();
                entry = _entries[slot] ??= new Entry();
                entry.Generation = entry.Generation % 65535u + 1u;
                entry.Active = true;
                handle = new TextStudio2DSurfaceHandle(slot, entry.Generation, this);
                _activeCount++;
            }
            _activeEdges += geometry.Edges.Length - previousEdges;
            _activeInkRecords = requiredInkRecords;
            entry.Geometry = geometry;
            entry.AdjustmentMask = adjustmentMask;
            _dirty = true;
            return true;
        }

        internal void CollectActiveGeometriesForTests(List<TextStudio2DSurfaceGeometry> geometries)
        {
            geometries.Clear();
            for (int slot = 1; slot < MaximumSurfaces; slot++)
                if (_entries[slot]?.Active == true) geometries.Add(_entries[slot].Geometry);
        }

        internal bool IsCurrent(TextStudio2DSurfaceHandle handle) => !_disposed && handle.IsValid && handle.Slot < MaximumSurfaces &&
            ReferenceEquals(handle.Owner, this) &&
            _entries[handle.Slot] != null && _entries[handle.Slot].Active && _entries[handle.Slot].Generation == handle.Generation;

        internal void Release(ref TextStudio2DSurfaceHandle handle)
        {
            if (IsCurrent(handle))
            {
                Entry entry = _entries[handle.Slot];
                _activeEdges -= entry.Geometry?.Edges.Length ?? 0;
                _activeInkRecords -= (entry.Geometry?.NativeInkSources.Length ?? 0) * TextStudio2DNativeInkSource.RecordStride;
                _activeCount--;
                _headerStorage.SetIdle(_activeCount == 0);
                _edgeStorage.SetIdle(_activeCount == 0);
                _inkStorage.SetIdle(_activeInkRecords == 0);
                entry.Active = false;
                entry.Geometry = null;
                _free.Push(handle.Slot);
                _dirty = true;
            }
            handle = default;
        }

        internal void FlushAndBind()
        {
            if (_disposed) return;
            if (_activeCount == 0)
            {
                ReleaseBuffers();
                _headerStorage.Dispose();
                _edgeStorage.Dispose();
                _inkStorage.Dispose();
                _dirty = false;
                TextStudio2DGpuFallbackResources.RestoreSurfaceBindings();
                return;
            }
            if (_activeInkRecords == 0) _inkStorage.Dispose();
            if (_dirty)
            {
                int lastSlot = 0;
                for (int slot = 1; slot < MaximumSurfaces; slot++) if (_entries[slot]?.Active == true) lastSlot = slot;
                Array.Clear(_headers, 0, _headers.Length);
                int offset = 0;
                int inkOffset = 0;
                for (int slot = 1; slot <= lastSlot; slot++)
                {
                    Entry entry = _entries[slot];
                    if (entry?.Active != true) continue;
                    TextStudio2DSurfaceGeometry geometry = entry.Geometry;
                    int header = slot * HeaderStride;
                    _headers[header] = new Vector4(offset, geometry.Edges.Length, entry.Generation, geometry.PixelProjection.HasValue ? 1 : geometry.NativeInkSources.Length > 0 ? 2 : 0);
                    _headers[header + 1] = geometry.Bounds;
                    _headers[header + 2] = entry.AdjustmentMask;
                    // A static ink mask names its leading band edges for the lean line program of the decoration shader.
                    if (geometry.HasInkBand)
                        _headers[header + 3] = new Vector4(geometry.BandEdgeCount, geometry.InkBand.x, geometry.InkBand.y, 1);
                    if (geometry.PixelProjection.HasValue)
                    {
                        Matrix4x4 projection = geometry.PixelProjection.Value.Matrix;
                        _headers[header + 3] = projection.GetRow(0);
                        _headers[header + 4] = projection.GetRow(1);
                        _headers[header + 5] = projection.GetRow(3);
                    }
                    if (geometry.NativeInkSources.Length > 0)
                    {
                        _headers[header + 3] = new Vector4(inkOffset, geometry.NativeInkSources.Length, 0, 0);
                        foreach (TextStudio2DNativeInkSource source in geometry.NativeInkSources)
                        {
                            _inkRecords[inkOffset++] = source.Metadata;
                            _inkRecords[inkOffset++] = source.BottomCorners;
                            _inkRecords[inkOffset++] = source.TopCorners;
                            _inkRecords[inkOffset++] = source.FontPlane;
                        }
                    }
                    Array.Copy(geometry.Edges, 0, _edges, offset, geometry.Edges.Length);
                    offset += geometry.Edges.Length;
                }
                if (!Publish(_headerStorage) || !Publish(_edgeStorage) || _activeInkRecords > 0 && !Publish(_inkStorage))
                {
                    MemoryBudgetExceeded = true;
                    TextStudio2DGpuFallbackResources.RestoreSurfaceBindings();
                    return;
                }
                _dirty = false;
                PublicationCount++;
            }
            _headerStorage.Bind(s_headersProperty, s_headersData);
            _edgeStorage.Bind(s_edgesProperty, s_edgesData);
            if (_activeInkRecords > 0) _inkStorage.Bind(s_inkSourcesProperty, s_inkSourcesData);
            else TextStudio2DGpuFallbackResources.RestoreSurfaceInkBindings();
            Shader.SetGlobalFloat(s_enabledProperty, 1);
        }

        static bool Publish(TextStudioAppearanceBuffer<Vector4> storage)
        {
            if (!storage.TryEnsureGpuBuffer(out bool uploaded)) return false;
            if (!uploaded) storage.Upload(0, storage.Data.Length);
            return true;
        }

        public void ReclaimUnused(long gpuBytes, long cpuBytes)
        {
            if (_disposed || _preparing || _activeCount != 0) return;
            bool reclaimCpu = cpuBytes > 0 && CpuBytes > 0;
            bool reclaimGpu = gpuBytes > 0 && (_headerStorage.GpuBytes > 0 || _edgeStorage.GpuBytes > 0 || _inkStorage.GpuBytes > 0);
            if (!reclaimCpu && !reclaimGpu) return;
            ReleaseBuffers();
            if (reclaimCpu)
            {
                _headerStorage.Dispose();
                _edgeStorage.Dispose();
                _inkStorage.Dispose();
            }
            TextStudioAppearanceMemory.RecordEviction(TextStudioAppearanceResourceKind.Geometry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseBuffers();
            Array.Clear(_entries, 0, _entries.Length);
            TextStudioAppearanceMemory.UnregisterIdleOwner(this);
            _headerStorage.Dispose();
            _edgeStorage.Dispose();
            _inkStorage.Dispose();
            _free.Clear();
            _activeCount = 0;
            _activeEdges = 0;
            _activeInkRecords = 0;
            TextStudio2DGpuFallbackResources.RestoreSurfaceBindings();
        }

        void ReleaseBuffers()
        {
            _headerStorage.ReleaseGpu();
            _edgeStorage.ReleaseGpu();
            _inkStorage.ReleaseGpu();
            Shader.SetGlobalFloat(s_enabledProperty, 0);
            TextStudio2DGpuFallbackResources.RestoreSurfaceBindings();
        }

        sealed class Entry
        {
            internal bool Active;
            internal uint Generation;
            internal TextStudio2DSurfaceGeometry Geometry;
            internal Vector4 AdjustmentMask;
        }
    }
}
