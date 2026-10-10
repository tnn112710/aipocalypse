using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace TextStudio
{
    internal readonly struct TextStudioWritingGpuHandle :
        IEquatable<TextStudioWritingGpuHandle>
    {
        internal readonly int Slot;
        internal readonly uint Generation;
        internal readonly uint TableGeneration;

        internal TextStudioWritingGpuHandle(
            int slot,
            uint generation,
            uint tableGeneration)
        {
            Slot = slot;
            Generation = generation;
            TableGeneration = tableGeneration;
        }

        internal bool IsValid =>
            Slot > 0 && Generation != 0u && TableGeneration != 0u;

        public bool Equals(TextStudioWritingGpuHandle other)
        {
            return Slot == other.Slot &&
                   Generation == other.Generation &&
                   TableGeneration == other.TableGeneration;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioWritingGpuHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Slot;
                hash = hash * 397 ^ (int)Generation;
                hash = hash * 397 ^ (int)TableGeneration;
                return hash;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudioGpuWritingSample
    {
        internal uint ProgressAndOpacity;
        internal uint SourceAndMetadata;

        internal bool Equals(in TextStudioGpuWritingSample other)
        {
            return ProgressAndOpacity == other.ProgressAndOpacity &&
                   SourceAndMetadata == other.SourceAndMetadata;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudioGpuWritingTransform
    {
        internal Vector4 MatrixRow0;
        internal Vector4 MatrixRow1;
        internal Vector4 MatrixRow2;
        internal Vector4 CornerBottomLeftTopLeft;
        internal Vector4 CornerTopRightBottomRight;
        internal Vector4 ColorMultiplierAndAlpha;
        internal Vector4 ColorAdditiveAndEffectScale;

        internal bool Equals(in TextStudioGpuWritingTransform other)
        {
            return MatrixRow0.Equals(other.MatrixRow0) &&
                   MatrixRow1.Equals(other.MatrixRow1) &&
                   MatrixRow2.Equals(other.MatrixRow2) &&
                   CornerBottomLeftTopLeft.Equals(other.CornerBottomLeftTopLeft) &&
                   CornerTopRightBottomRight.Equals(other.CornerTopRightBottomRight) &&
                   ColorMultiplierAndAlpha.Equals(other.ColorMultiplierAndAlpha) &&
                   ColorAdditiveAndEffectScale.Equals(other.ColorAdditiveAndEffectScale);
        }
    }

    internal static class TextStudioWritingGpuPacking
    {
        const uint SourceAlphaMask = 0x0fffu;
        const int ModeShift = 12;
        const int DirectionShift = 16;
        const int ReverseShift = 19;
        const int ActiveShift = 20;
        const int SoftnessShift = 21;
        const int BrushShift = 29;

        internal static TextStudioGpuWritingSample Pack(
            float progress,
            float opacity,
            float sourceAlpha,
            TextStudioWritingMode mode,
            TextStudioWritingDirection direction,
            bool reverse,
            float softness,
            TextStudioWritingBrush brush,
            bool active)
        {
            uint progress16 = PackUnorm(progress, ushort.MaxValue);
            uint opacity16 = PackUnorm(opacity, ushort.MaxValue);
            uint source12 = PackUnorm(sourceAlpha, SourceAlphaMask);
            uint metadata = source12 |
                            ((uint)mode & 0x0fu) << ModeShift |
                            ((uint)direction & 0x07u) << DirectionShift |
                            (reverse ? 1u : 0u) << ReverseShift |
                            (active ? 1u : 0u) << ActiveShift |
                            (PackUnorm(softness, byte.MaxValue) & 0xffu) <<
                            SoftnessShift |
                            ((uint)brush & 0x07u) << BrushShift;
            return new TextStudioGpuWritingSample
            {
                ProgressAndOpacity = progress16 | opacity16 << 16,
                SourceAndMetadata = metadata
            };
        }

        internal static float UnpackProgress(in TextStudioGpuWritingSample sample)
        {
            return (sample.ProgressAndOpacity & 0xffffu) / 65535f;
        }

        internal static float UnpackOpacity(in TextStudioGpuWritingSample sample)
        {
            return (sample.ProgressAndOpacity >> 16) / 65535f;
        }

        internal static float UnpackSourceAlpha(in TextStudioGpuWritingSample sample)
        {
            return (sample.SourceAndMetadata & SourceAlphaMask) /
                   (float)SourceAlphaMask;
        }

        internal static bool UnpackActive(in TextStudioGpuWritingSample sample)
        {
            return ((sample.SourceAndMetadata >> ActiveShift) & 1u) != 0u;
        }

        internal static TextStudioWritingBrush UnpackBrush(
            in TextStudioGpuWritingSample sample)
        {
            return (TextStudioWritingBrush)
                ((sample.SourceAndMetadata >> BrushShift) & 0x07u);
        }

        static uint PackUnorm(float value, uint maximum)
        {
            return (uint)Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(value) * maximum),
                0,
                (int)maximum);
        }
    }

    internal readonly struct TextStudioWritingGpuStatistics
    {
        internal readonly int SampleCapacity;
        internal readonly int TransformCapacity;
        internal readonly int ActiveOwners;
        internal readonly int ActiveSamples;
        internal readonly int ActiveTransforms;
        internal readonly int ReservedSamples;
        internal readonly int ReservedTransforms;
        internal readonly int SampleUploadCalls;
        internal readonly int TransformUploadCalls;
        internal readonly long UploadedBytes;

        internal TextStudioWritingGpuStatistics(
            int sampleCapacity,
            int transformCapacity,
            int activeOwners,
            int activeSamples,
            int activeTransforms,
            int reservedSamples,
            int reservedTransforms,
            int sampleUploadCalls,
            int transformUploadCalls,
            long uploadedBytes)
        {
            SampleCapacity = sampleCapacity;
            TransformCapacity = transformCapacity;
            ActiveOwners = activeOwners;
            ActiveSamples = activeSamples;
            ActiveTransforms = activeTransforms;
            ReservedSamples = reservedSamples;
            ReservedTransforms = reservedTransforms;
            SampleUploadCalls = sampleUploadCalls;
            TransformUploadCalls = transformUploadCalls;
            UploadedBytes = uploadedBytes;
        }
    }

    /// <summary>
    /// Shared generation-safe writing arena. Progress records and transforms use parallel ranges,
    /// allowing a progress-only frame to upload eight bytes per active sample while retained path
    /// vertices and unchanged glyph transforms remain untouched.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioWritingGpuTable : IDisposable, ITextStudioAppearanceMemoryReclaimer
    {
        internal const int MaximumOwners = 1024;
        internal const int InitialCapacity = 256;
        internal const int MaximumCapacity = 1 << 20;

        static readonly int s_samplesProperty =
            Shader.PropertyToID("_TextStudioWritingSamples");
        static readonly int s_transformsProperty =
            Shader.PropertyToID("_TextStudioWritingTransforms");
        static readonly int s_enabledProperty =
            Shader.PropertyToID("_TextStudioWritingEnabled");

        static TextStudioWritingGpuTable s_shared;
        static uint s_nextTableGeneration;

        struct SlotState
        {
            internal uint Generation;
            internal bool Active;
            internal int SampleOffset;
            internal int SampleCount;
            internal int SampleReserved;
            internal int TransformOffset;
            internal int TransformCount;
            internal int TransformReserved;
        }

        struct FreeRange
        {
            internal int Offset;
            internal int Count;

            internal FreeRange(int offset, int count)
            {
                Offset = offset;
                Count = count;
            }
        }

        struct DirtyRange
        {
            internal int Offset;
            internal int Count;

            internal DirtyRange(int offset, int count)
            {
                Offset = offset;
                Count = count;
            }
        }

        readonly SlotState[] _slots = new SlotState[MaximumOwners + 1];
        readonly int[] _freeSlots = new int[MaximumOwners];
        readonly List<FreeRange> _sampleFreeRanges =
            new List<FreeRange>(MaximumOwners * 2);
        readonly List<FreeRange> _transformFreeRanges =
            new List<FreeRange>(MaximumOwners * 2);
        readonly DirtyRange[] _sampleDirty = new DirtyRange[MaximumOwners * 2];
        readonly DirtyRange[] _transformDirty = new DirtyRange[MaximumOwners * 2];

        // Data textures carry each packed sample as four exact 16-bit halves in one texel.
        readonly TextStudioAppearanceBuffer<TextStudioGpuWritingSample> _sampleStorage =
            new TextStudioAppearanceBuffer<TextStudioGpuWritingSample>(TextStudioAppearanceResourceKind.Geometry, "Writing progress samples",
                1, EncodeSample);
        readonly TextStudioAppearanceBuffer<TextStudioGpuWritingTransform> _transformStorage =
            new TextStudioAppearanceBuffer<TextStudioGpuWritingTransform>(TextStudioAppearanceResourceKind.Geometry, "Writing glyph transforms");
        TextStudioGpuWritingSample[] _samples => _sampleStorage.Data;
        TextStudioGpuWritingTransform[] _transforms => _transformStorage.Data;
        static readonly int s_samplesData = Shader.PropertyToID("_TextStudioWritingSamplesData");
        static readonly int s_transformsData = Shader.PropertyToID("_TextStudioWritingTransformsData");
        bool _preparing;
        internal bool MemoryBudgetExceeded { get; private set; }
        int _freeSlotCount;
        int _sampleDirtyCount;
        int _transformDirtyCount;
        int _activeOwners;
        int _activeSamples;
        int _activeTransforms;
        int _reservedSamples;
        int _reservedTransforms;
        int _sampleUploadCalls;
        int _transformUploadCalls;
        long _uploadedBytes;
        int _lastBoundFrame = -1;
        bool _gpuCreationFailed;
        bool _disposed;
        readonly uint _tableGeneration;

        internal static TextStudioWritingGpuTable Shared =>
            s_shared ??= new TextStudioWritingGpuTable();

        internal static bool IsGpuSupported => TextStudio2DTransport.IsSupported;

        static void EncodeSample(in TextStudioGpuWritingSample sample, NativeArray<Vector4> texels, int texel) =>
            texels[texel] = new Vector4(sample.ProgressAndOpacity & 0xffffu, sample.ProgressAndOpacity >> 16,
                sample.SourceAndMetadata & 0xffffu, sample.SourceAndMetadata >> 16);

        internal TextStudioWritingGpuStatistics Statistics =>
            new TextStudioWritingGpuStatistics(
                _samples.Length,
                _transforms.Length,
                _activeOwners,
                _activeSamples,
                _activeTransforms,
                _reservedSamples,
                _reservedTransforms,
                _sampleUploadCalls,
                _transformUploadCalls,
                _uploadedBytes);

        internal TextStudioWritingGpuTable()
        {
            s_nextTableGeneration++;
            if (s_nextTableGeneration == 0u)
                s_nextTableGeneration = 1u;
            _tableGeneration = s_nextTableGeneration;
            TextStudioAppearanceMemory.RegisterIdleOwner(this);
            for (int slot = 1; slot <= MaximumOwners; slot++)
            {
                _slots[slot].Generation = 1u;
                _freeSlots[_freeSlotCount++] = MaximumOwners - slot + 1;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetShared()
        {
            s_shared?.Dispose();
            s_shared = null;
            Shader.SetGlobalFloat(s_enabledProperty, 0f);
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void OnExitingPlayMode() => ResetShared();
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading() => ResetShared();
#endif

        internal bool TryEnsureRanges(
            ref TextStudioWritingGpuHandle handle,
            int sampleCount,
            int transformCount,
            out int sampleOffset,
            out int transformOffset)
        {
            _preparing = true;
            MemoryBudgetExceeded = false;
            try { return TryEnsureRangesCore(ref handle, sampleCount, transformCount, out sampleOffset, out transformOffset); }
            finally
            {
                _preparing = false;
                _sampleStorage.SetIdle(_activeOwners == 0);
                _transformStorage.SetIdle(_activeOwners == 0);
            }
        }

        bool TryEnsureRangesCore(
            ref TextStudioWritingGpuHandle handle,
            int sampleCount,
            int transformCount,
            out int sampleOffset,
            out int transformOffset)
        {
            ThrowIfDisposed();
            sampleOffset = 0;
            transformOffset = 0;
            if (sampleCount <= 0)
            {
                Release(ref handle);
                return true;
            }
            if (sampleCount > MaximumCapacity ||
                transformCount < 0 ||
                transformCount > MaximumCapacity)
            {
                return false;
            }

            if ((_samples.Length == 0 && !Grow(_sampleFreeRanges, InitialCapacity, false)) ||
                (_transforms.Length == 0 && !Grow(_transformFreeRanges, InitialCapacity, true))) return false;
            if (!_sampleStorage.TryEnsureCapacity(_samples.Length) || !_transformStorage.TryEnsureCapacity(_transforms.Length))
            {
                MemoryBudgetExceeded = true;
                return false;
            }
            bool isNew = !IsCurrent(handle);
            int slot;
            if (isNew)
            {
                if (_freeSlotCount <= 0)
                    return false;
                slot = _freeSlots[--_freeSlotCount];
            }
            else
            {
                slot = handle.Slot;
                SlotState current = _slots[slot];
                if (current.SampleReserved >= sampleCount &&
                    current.TransformReserved >= transformCount)
                {
                    _activeSamples += sampleCount - current.SampleCount;
                    _activeTransforms += transformCount - current.TransformCount;
                    current.SampleCount = sampleCount;
                    current.TransformCount = transformCount;
                    _slots[slot] = current;
                    sampleOffset = current.SampleOffset;
                    transformOffset = current.TransformOffset;
                    return true;
                }
            }

            SlotState state = _slots[slot];
            bool allocateSamples = isNew || state.SampleReserved < sampleCount;
            bool allocateTransforms = isNew ||
                                      state.TransformReserved < transformCount;
            int nextSampleReserved = allocateSamples
                ? TextStudioBufferCapacity.ForCount(sampleCount)
                : state.SampleReserved;
            int nextTransformReserved = allocateTransforms && transformCount > 0
                ? TextStudioBufferCapacity.ForCount(transformCount)
                : state.TransformReserved;
            int nextSampleOffset = state.SampleOffset;
            int nextTransformOffset = state.TransformOffset;
            if (allocateSamples &&
                !TryAllocate(
                    _sampleFreeRanges,
                    nextSampleReserved,
                    false,
                    out nextSampleOffset))
            {
                if (isNew)
                    _freeSlots[_freeSlotCount++] = slot;
                return false;
            }
            if (allocateTransforms && transformCount > 0 &&
                !TryAllocate(
                    _transformFreeRanges,
                    nextTransformReserved,
                    true,
                    out nextTransformOffset))
            {
                if (allocateSamples)
                    FreeAndMerge(
                        _sampleFreeRanges,
                        nextSampleOffset,
                        nextSampleReserved);
                if (isNew)
                    _freeSlots[_freeSlotCount++] = slot;
                return false;
            }

            int previousSampleCount = isNew ? 0 : state.SampleCount;
            int previousTransformCount = isNew ? 0 : state.TransformCount;
            if (!isNew)
            {
                if (allocateSamples)
                {
                    Array.Clear(
                        _samples,
                        state.SampleOffset,
                        state.SampleReserved);
                    MarkDirty(
                        _sampleDirty,
                        ref _sampleDirtyCount,
                        state.SampleOffset,
                        state.SampleReserved);
                    FreeAndMerge(
                        _sampleFreeRanges,
                        state.SampleOffset,
                        state.SampleReserved);
                    _reservedSamples -= state.SampleReserved;
                }
                if (allocateTransforms && state.TransformReserved > 0)
                {
                    Array.Clear(
                        _transforms,
                        state.TransformOffset,
                        state.TransformReserved);
                    MarkDirty(
                        _transformDirty,
                        ref _transformDirtyCount,
                        state.TransformOffset,
                        state.TransformReserved);
                    FreeAndMerge(
                        _transformFreeRanges,
                        state.TransformOffset,
                        state.TransformReserved);
                    _reservedTransforms -= state.TransformReserved;
                }
            }
            else
            {
                state.Active = true;
                _activeOwners++;
                handle = new TextStudioWritingGpuHandle(
                    slot,
                    state.Generation,
                    _tableGeneration);
            }

            state.SampleOffset = nextSampleOffset;
            state.SampleCount = sampleCount;
            state.SampleReserved = nextSampleReserved;
            state.TransformOffset = transformCount > 0 ? nextTransformOffset : 0;
            state.TransformCount = transformCount;
            state.TransformReserved = transformCount > 0
                ? nextTransformReserved
                : 0;
            _slots[slot] = state;
            _activeSamples += sampleCount - previousSampleCount;
            _activeTransforms += transformCount - previousTransformCount;
            if (allocateSamples)
                _reservedSamples += nextSampleReserved;
            if (allocateTransforms && transformCount > 0)
                _reservedTransforms += nextTransformReserved;
            sampleOffset = nextSampleOffset;
            transformOffset = state.TransformOffset;
            return true;
        }

        internal TextStudioGpuWritingSample[] GetSampleBuffer(
            TextStudioWritingGpuHandle handle,
            out int offset,
            out int count)
        {
            if (!IsCurrent(handle))
            {
                offset = 0;
                count = 0;
                return null;
            }
            SlotState state = _slots[handle.Slot];
            offset = state.SampleOffset;
            count = state.SampleCount;
            return _samples;
        }

        internal TextStudioGpuWritingTransform[] GetTransformBuffer(
            TextStudioWritingGpuHandle handle,
            out int offset,
            out int count)
        {
            if (!IsCurrent(handle))
            {
                offset = 0;
                count = 0;
                return null;
            }
            SlotState state = _slots[handle.Slot];
            offset = state.TransformOffset;
            count = state.TransformCount;
            return _transforms;
        }

        internal void MarkSamplesDirty(
            TextStudioWritingGpuHandle handle,
            int localOffset,
            int count)
        {
            MarkHandleDirty(
                handle,
                localOffset,
                count,
                _sampleDirty,
                ref _sampleDirtyCount,
                false);
        }

        internal void MarkTransformsDirty(
            TextStudioWritingGpuHandle handle,
            int localOffset,
            int count)
        {
            MarkHandleDirty(
                handle,
                localOffset,
                count,
                _transformDirty,
                ref _transformDirtyCount,
                true);
        }

        internal bool IsCurrent(TextStudioWritingGpuHandle handle)
        {
            return handle.IsValid &&
                   handle.TableGeneration == _tableGeneration &&
                   handle.Slot <= MaximumOwners &&
                   _slots[handle.Slot].Active &&
                   _slots[handle.Slot].Generation == handle.Generation;
        }

        internal void Release(ref TextStudioWritingGpuHandle handle)
        {
            if (_disposed || !IsCurrent(handle))
            {
                handle = default;
                return;
            }

            int slot = handle.Slot;
            SlotState state = _slots[slot];
            Array.Clear(
                _samples,
                state.SampleOffset,
                state.SampleReserved);
            MarkDirty(
                _sampleDirty,
                ref _sampleDirtyCount,
                state.SampleOffset,
                state.SampleReserved);
            FreeAndMerge(
                _sampleFreeRanges,
                state.SampleOffset,
                state.SampleReserved);
            if (state.TransformReserved > 0)
            {
                Array.Clear(
                    _transforms,
                    state.TransformOffset,
                    state.TransformReserved);
                MarkDirty(
                    _transformDirty,
                    ref _transformDirtyCount,
                    state.TransformOffset,
                    state.TransformReserved);
                FreeAndMerge(
                    _transformFreeRanges,
                    state.TransformOffset,
                    state.TransformReserved);
            }
            _activeSamples -= state.SampleCount;
            _activeTransforms -= state.TransformCount;
            _reservedSamples -= state.SampleReserved;
            _reservedTransforms -= state.TransformReserved;
            _activeOwners--;
            if (_activeOwners <= 0)
            {
                _activeOwners = 0;
                Shader.SetGlobalFloat(s_enabledProperty, 0f);
                DisposeBuffers();
                _sampleStorage.SetIdle(true);
                _transformStorage.SetIdle(true);
            }
            state.Active = false;
            state.SampleOffset = 0;
            state.SampleCount = 0;
            state.SampleReserved = 0;
            state.TransformOffset = 0;
            state.TransformCount = 0;
            state.TransformReserved = 0;
            state.Generation++;
            if (state.Generation == 0u)
                state.Generation = 1u;
            _slots[slot] = state;
            _freeSlots[_freeSlotCount++] = slot;
            handle = default;
        }

        internal void FlushAndBind()
        {
            ThrowIfDisposed();
            if (_activeOwners <= 0 || !EnsureGpuBuffers())
            {
                Shader.SetGlobalFloat(s_enabledProperty, 0f);
                return;
            }

            bool uploaded = FlushDirty(
                _sampleStorage,
                _sampleDirty,
                ref _sampleDirtyCount,
                ref _sampleUploadCalls,
                Marshal.SizeOf<TextStudioGpuWritingSample>());
            uploaded |= FlushDirty(
                _transformStorage,
                _transformDirty,
                ref _transformDirtyCount,
                ref _transformUploadCalls,
                Marshal.SizeOf<TextStudioGpuWritingTransform>());
            int frame = Time.frameCount;
            if (_lastBoundFrame == frame && !uploaded)
                return;
            _sampleStorage.Bind(s_samplesProperty, s_samplesData);
            _transformStorage.Bind(s_transformsProperty, s_transformsData);
            Shader.SetGlobalFloat(s_enabledProperty, 1f);
            _lastBoundFrame = frame;
        }

        bool FlushDirty<T>(
            TextStudioAppearanceBuffer<T> storage,
            DirtyRange[] ranges,
            ref int rangeCount,
            ref int uploadCalls,
            int stride) where T : struct
        {
            bool uploaded = false;
            for (int index = 0; index < rangeCount; index++)
            {
                DirtyRange range = ranges[index];
                if (range.Count <= 0)
                    continue;
                storage.Upload(range.Offset, range.Count);
                uploadCalls++;
                _uploadedBytes += (long)range.Count * stride;
                uploaded = true;
            }
            rangeCount = 0;
            return uploaded;
        }

        bool EnsureGpuBuffers()
        {
            if (_gpuCreationFailed || !IsGpuSupported) return false;
            try
            {
                if (!_sampleStorage.TryEnsureGpuBuffer(out bool samplesUploaded) ||
                    !_transformStorage.TryEnsureGpuBuffer(out bool transformsUploaded)) return false;
                if (samplesUploaded)
                {
                    _uploadedBytes += (long)_samples.Length * Marshal.SizeOf<TextStudioGpuWritingSample>();
                    _sampleUploadCalls++;
                    _sampleDirtyCount = 0;
                    _lastBoundFrame = -1;
                }
                if (transformsUploaded)
                {
                    _uploadedBytes += (long)_transforms.Length * Marshal.SizeOf<TextStudioGpuWritingTransform>();
                    _transformUploadCalls++;
                    _transformDirtyCount = 0;
                    _lastBoundFrame = -1;
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[TextStudio] Writing GPU buffers could not be created: " + exception.Message);
                _gpuCreationFailed = true;
                return false;
            }
        }

        void MarkHandleDirty(
            TextStudioWritingGpuHandle handle,
            int localOffset,
            int count,
            DirtyRange[] ranges,
            ref int rangeCount,
            bool transform)
        {
            if (!IsCurrent(handle) || count <= 0)
                return;
            SlotState state = _slots[handle.Slot];
            int entryCount = transform
                ? state.TransformCount
                : state.SampleCount;
            int baseOffset = transform
                ? state.TransformOffset
                : state.SampleOffset;
            int local = Mathf.Clamp(localOffset, 0, entryCount);
            int resolvedCount = Mathf.Clamp(count, 0, entryCount - local);
            MarkDirty(
                ranges,
                ref rangeCount,
                baseOffset + local,
                resolvedCount);
        }

        static void MarkDirty(
            DirtyRange[] ranges,
            ref int rangeCount,
            int offset,
            int count)
        {
            if (count <= 0)
                return;
            int end = offset + count;
            for (int index = 0; index < rangeCount; index++)
            {
                DirtyRange current = ranges[index];
                int currentEnd = current.Offset + current.Count;
                if (end < current.Offset || offset > currentEnd)
                    continue;
                int mergedStart = Mathf.Min(offset, current.Offset);
                int mergedEnd = Mathf.Max(end, currentEnd);
                ranges[index] = new DirtyRange(
                    mergedStart,
                    mergedEnd - mergedStart);
                return;
            }
            if (rangeCount < ranges.Length)
                ranges[rangeCount++] = new DirtyRange(offset, count);
            else
            {
                int minimum = offset;
                int maximum = end;
                for (int index = 0; index < rangeCount; index++)
                {
                    minimum = Mathf.Min(minimum, ranges[index].Offset);
                    maximum = Mathf.Max(
                        maximum,
                        ranges[index].Offset + ranges[index].Count);
                }
                ranges[0] = new DirtyRange(minimum, maximum - minimum);
                rangeCount = 1;
            }
        }

        bool TryAllocate(
            List<FreeRange> freeRanges,
            int count,
            bool transform,
            out int offset)
        {
            for (int index = 0; index < freeRanges.Count; index++)
            {
                FreeRange range = freeRanges[index];
                if (range.Count < count)
                    continue;
                offset = range.Offset;
                if (range.Count == count)
                    freeRanges.RemoveAt(index);
                else
                    freeRanges[index] = new FreeRange(
                        range.Offset + count,
                        range.Count - count);
                return true;
            }

            if (!Grow(freeRanges, count, transform))
            {
                offset = 0;
                return false;
            }
            return TryAllocate(freeRanges, count, transform, out offset);
        }

        bool Grow(
            List<FreeRange> freeRanges,
            int minimumAdditional,
            bool transform)
        {
            int previous = transform
                ? _transforms.Length
                : _samples.Length;
            if (previous >= MaximumCapacity)
                return false;
            int required = Mathf.Min(
                MaximumCapacity,
                Mathf.Max(previous * 2, previous + minimumAdditional));
            int next = Mathf.NextPowerOfTwo(required);
            next = Mathf.Min(MaximumCapacity, Mathf.Max(previous + minimumAdditional, next));
            if (transform)
            {
                if (!_transformStorage.TryEnsureCapacity(next)) { MemoryBudgetExceeded = true; return false; }
            }
            else
            {
                if (!_sampleStorage.TryEnsureCapacity(next)) { MemoryBudgetExceeded = true; return false; }
            }
            freeRanges.Add(new FreeRange(previous, next - previous));
            MergeFreeRanges(freeRanges);
            return true;
        }

        static void FreeAndMerge(
            List<FreeRange> freeRanges,
            int offset,
            int count)
        {
            if (count <= 0)
                return;
            freeRanges.Add(new FreeRange(offset, count));
            MergeFreeRanges(freeRanges);
        }

        static void MergeFreeRanges(List<FreeRange> freeRanges)
        {
            freeRanges.Sort((first, second) => first.Offset.CompareTo(second.Offset));
            for (int index = freeRanges.Count - 1; index > 0; index--)
            {
                FreeRange previous = freeRanges[index - 1];
                FreeRange current = freeRanges[index];
                if (previous.Offset + previous.Count < current.Offset)
                    continue;
                int end = Mathf.Max(
                    previous.Offset + previous.Count,
                    current.Offset + current.Count);
                freeRanges[index - 1] = new FreeRange(
                    previous.Offset,
                    end - previous.Offset);
                freeRanges.RemoveAt(index);
            }
        }

        void DisposeBuffers()
        {
            DisposeSampleBuffer();
            DisposeTransformBuffer();
            TextStudio2DGpuFallbackResources.RestoreWritingBindings();
            _lastBoundFrame = -1;
        }

        void DisposeSampleBuffer()
        {
            _sampleStorage.ReleaseGpu();
            _lastBoundFrame = -1;
        }

        void DisposeTransformBuffer()
        {
            _transformStorage.ReleaseGpu();
            _lastBoundFrame = -1;
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextStudioWritingGpuTable));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            DisposeBuffers();
            TextStudioAppearanceMemory.UnregisterIdleOwner(this);
            _sampleStorage.Dispose();
            _transformStorage.Dispose();
            _disposed = true;
            Shader.SetGlobalFloat(s_enabledProperty, 0f);
        }

        public void ReclaimUnused(long gpuBytes, long cpuBytes)
        {
            if (_disposed || _preparing || _activeOwners != 0) return;
            bool reclaimCpu = cpuBytes > 0 && (_sampleStorage.CpuBytes > 0 || _transformStorage.CpuBytes > 0);
            bool reclaimGpu = gpuBytes > 0 && (_sampleStorage.GpuBytes > 0 || _transformStorage.GpuBytes > 0);
            if (!reclaimCpu && !reclaimGpu) return;
            DisposeBuffers();
            if (reclaimCpu)
            {
                _sampleStorage.Dispose();
                _transformStorage.Dispose();
                _sampleFreeRanges.Clear();
                _transformFreeRanges.Clear();
                _sampleDirtyCount = _transformDirtyCount = 0;
            }
            TextStudioAppearanceMemory.RecordEviction(TextStudioAppearanceResourceKind.Geometry);
        }

        internal static void ResetForTests()
        {
            ResetShared();
        }
    }
}
