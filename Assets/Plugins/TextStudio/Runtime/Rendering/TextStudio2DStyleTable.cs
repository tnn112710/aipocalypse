using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TextStudio
{
    internal enum TextStudio2DFieldProgramStatus : byte
    {
        PassThrough = 0,
        Ready = 1,
        InvalidStyle = 2,
        UnsupportedExecutionPath = 3,
        UnsupportedPaint = 4,
        UnsupportedBlend = 5,
        UnsupportedBounds = 6,
        CapacityExhausted = 7,
        GpuUnsupported = 8,
        UnsupportedMapping = 9,
        UnsupportedTexturePlatform = 10,
        TextureCapacityExhausted = 11,
        TextureResourceConflict = 12,
        TextureUploadFailed = 13,
        MemoryBudgetExceeded = 14
    }

    internal readonly struct TextStudio2DStyleTableHandle : IEquatable<TextStudio2DStyleTableHandle>
    {
        internal readonly int Slot;
        internal readonly uint Generation;

        internal TextStudio2DStyleTableHandle(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }

        internal bool IsValid => Slot > 0 && Generation != 0u;

        public bool Equals(TextStudio2DStyleTableHandle other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudio2DStyleTableHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Slot * 397 ^ (int)Generation;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudio2DGpuStyleHeader
    {
        internal Vector4 Metadata;
        internal Vector4 BoundsExpansion;
        /// <summary>Operation and gradient-stop record bases of the interned program this text references.</summary>
        internal Vector4 Program;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudio2DGpuOperation
    {
        internal Vector4 LayerMetadata;
        internal Vector4 ExecutionMetadata;
        internal Vector4 PaintMetadata;
        internal Vector4 Color;
        internal Vector4 PaintTransform0;
        internal Vector4 PaintTransform1;
        internal Vector4 Parameters0;
        internal Vector4 Parameters1;
        internal Vector4 Parameters2;
        internal Vector4 Parameters3;
        internal Vector4 Parameters4;
        internal Vector4 Parameters5;
        internal Vector4 Parameters6;
        internal Vector4 Parameters7;
        internal Vector4 Parameters8;
        internal Vector4 Parameters9;
        internal Vector4 Parameters10;
        internal Vector4 Parameters11;
        internal Vector4 Parameters12;
        internal Vector4 Parameters13;
        internal Vector4 AuxiliaryColor0;
        internal Vector4 AuxiliaryColor1;
        internal Vector4 AuxiliaryColor2;
        internal Vector4 AuxiliaryColor3;
        internal Vector4 AuxiliaryColor4;
        internal Vector4 AuxiliaryColor5;
        internal Vector4 SelectorMetadata;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudio2DGpuGradientStop
    {
        internal Vector4 Metadata;
        internal Vector4 Color;
    }

    internal readonly struct TextStudio2DStyleTableStatistics
    {
        internal readonly int Capacity;
        internal readonly int ActiveSlots;
        internal readonly int PendingDirtySlots;
        internal readonly int PendingUniformSlots;
        internal readonly int UploadCalls;
        internal readonly long UploadedBytes;
        internal readonly int UniformUploadCalls;
        internal readonly long UniformUploadedBytes;
        /// <summary>Distinct interned programs. Texts with the same compiled style share one program.</summary>
        internal readonly int ProgramCount;
        internal readonly int OperationRecords;
        internal readonly int OperationCapacity;

        internal TextStudio2DStyleTableStatistics(
            int capacity,
            int activeSlots,
            int pendingDirtySlots,
            int pendingUniformSlots,
            int uploadCalls,
            long uploadedBytes,
            int uniformUploadCalls,
            long uniformUploadedBytes,
            int programCount,
            int operationRecords,
            int operationCapacity)
        {
            Capacity = capacity;
            ActiveSlots = activeSlots;
            PendingDirtySlots = pendingDirtySlots;
            PendingUniformSlots = pendingUniformSlots;
            UploadCalls = uploadCalls;
            UploadedBytes = uploadedBytes;
            UniformUploadCalls = uniformUploadCalls;
            UniformUploadedBytes = uniformUploadedBytes;
            ProgramCount = programCount;
            OperationRecords = operationRecords;
            OperationCapacity = operationCapacity;
        }
    }

    /// <summary>
    /// Process-wide appearance program table shared by direct, retained and Hero execution. Each active text owns one
    /// stable instance slot whose header references an interned, immutable program range, so texts with the same compiled
    /// style share one copy of its operation and gradient records. Parameter changes patch records without changing
    /// vertex data or material identity; ranged storage grows under the appearance memory budget up to the compiler's
    /// operation limit instead of reserving a fixed per-text stride.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DStyleTable : IDisposable, ITextStudioAppearanceMemoryReclaimer
    {
        internal const int SchemaVersion = TextStudio2DGpuStylePacking.SchemaVersion;
        internal const int MaximumStyles = 1024;
        /// <summary>Operations one fused primitive evaluates. Larger programs draw as ordered spans, never as one wider loop.</summary>
        internal const int MaximumFusedOperations = 8;
        /// <summary>The compiler's safety limit for one program, including flattened surface treatments.</summary>
        internal const int MaximumProgramOperations = 512;
        /// <summary>Stops one gradient paint evaluates. A program may hold any number of gradient paints.</summary>
        internal const int MaximumGradientStops = 16;

        static readonly int s_headersProperty =
            Shader.PropertyToID("_TextStudio2DStyleHeaders");
        static readonly int s_operationsProperty =
            Shader.PropertyToID("_TextStudio2DStyleOperations");
        static readonly int s_gradientStopsProperty =
            Shader.PropertyToID("_TextStudio2DGradientStops");
        static readonly int s_textUniformsProperty =
            Shader.PropertyToID("_TextStudio2DTextUniforms");
        static readonly int s_headersData = Shader.PropertyToID("_TextStudio2DStyleHeadersData");
        static readonly int s_operationsData = Shader.PropertyToID("_TextStudio2DStyleOperationsData");
        static readonly int s_gradientStopsData = Shader.PropertyToID("_TextStudio2DGradientStopsData");
        static readonly int s_textUniformsData = Shader.PropertyToID("_TextStudio2DTextUniformsData");
        static readonly int s_visualModifiersEnabledProperty =
            Shader.PropertyToID("_TextStudio2DVisualModifiersEnabled");
        static readonly int s_enabledProperty =
            Shader.PropertyToID("_TextStudio2DStyleTableEnabled");

        static TextStudio2DStyleTable s_shared;

        readonly TextStudioAppearanceBuffer<TextStudio2DGpuStyleHeader> _headerStorage =
            new TextStudioAppearanceBuffer<TextStudio2DGpuStyleHeader>(TextStudioAppearanceResourceKind.Tables, "Style instance headers");
        readonly TextStudioAppearanceBuffer<TextStudio2DGpuTextUniform> _uniformStorage =
            new TextStudioAppearanceBuffer<TextStudio2DGpuTextUniform>(TextStudioAppearanceResourceKind.Tables, "Style text clocks");
        readonly TextStudioAppearanceBuffer<TextStudio2DGpuOperation> _operationStorage =
            new TextStudioAppearanceBuffer<TextStudio2DGpuOperation>(TextStudioAppearanceResourceKind.Tables, "Style program operations");
        readonly TextStudioAppearanceBuffer<TextStudio2DGpuGradientStop> _stopStorage =
            new TextStudioAppearanceBuffer<TextStudio2DGpuGradientStop>(TextStudioAppearanceResourceKind.Tables, "Style program gradient stops");
        readonly TextStudioRangeAllocator _operationRanges = new TextStudioRangeAllocator();
        readonly TextStudioRangeAllocator _stopRanges = new TextStudioRangeAllocator();
        readonly Dictionary<TextStudioStackFingerprint, Program> _programs = new Dictionary<TextStudioStackFingerprint, Program>();
        readonly List<Vector2Int> _dirtyOperationRanges = new List<Vector2Int>();
        readonly List<Vector2Int> _dirtyStopRanges = new List<Vector2Int>();
        readonly SlotState[] _slots;
        readonly Program[] _slotPrograms;
        readonly int[] _freeSlots;
        readonly bool[] _dirtyFlags;
        readonly int[] _dirtySlots;
        readonly bool[] _uniformDirtyFlags;
        readonly int[] _uniformDirtySlots;

        int _freeCount;
        int _dirtyCount;
        int _uniformDirtyCount;
        int _activeCount;
        int _uploadCalls;
        long _uploadedBytes;
        int _uniformUploadCalls;
        long _uniformUploadedBytes;
        int _activeTextureReferenceCount;
        int _lastBoundFrame = -1;
        bool _gpuCreationFailed;
        bool _disposed;

        struct SlotState
        {
            internal uint Generation;
            internal bool Active;
        }

        sealed class Program
        {
            internal TextStudioStackFingerprint Fingerprint;
            internal int OperationStart, OperationCount, StopStart, StopCount, References;
            internal TextStudio2DTextureArrayHandle[] Textures = Array.Empty<TextStudio2DTextureArrayHandle>();
        }

        internal static TextStudio2DStyleTable Shared =>
            s_shared ??= new TextStudio2DStyleTable();

        internal static bool IsGpuSupported => TextStudio2DTransport.IsSupported;

        internal TextStudio2DStyleTableStatistics Statistics =>
            new TextStudio2DStyleTableStatistics(
                MaximumStyles - 1,
                _activeCount,
                _dirtyCount,
                _uniformDirtyCount,
                _uploadCalls,
                _uploadedBytes,
                _uniformUploadCalls,
                _uniformUploadedBytes,
                _programs.Count,
                _operationRanges.UsedCount,
                _operationRanges.Capacity);

        TextStudio2DStyleTable()
        {
            TextStudioAppearanceMemory.RegisterIdleOwner(this);
            _slots = new SlotState[MaximumStyles];
            _slotPrograms = new Program[MaximumStyles];
            _freeSlots = new int[MaximumStyles - 1];
            _dirtyFlags = new bool[MaximumStyles];
            _dirtySlots = new int[MaximumStyles];
            _uniformDirtyFlags = new bool[MaximumStyles];
            _uniformDirtySlots = new int[MaximumStyles];

            for (int slot = 1; slot < MaximumStyles; slot++)
            {
                _slots[slot].Generation = 1u;
                _freeSlots[_freeCount++] = MaximumStyles - slot;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void ResetSharedTable()
        {
            s_shared?.Dispose();
            s_shared = null;
            Shader.SetGlobalFloat(s_enabledProperty, 0f);
            Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 0f);
        }

        /// <summary>
        /// Binds <paramref name="style"/> to this text's slot. Any valid program is stored, including Hero-only and
        /// retained operations; route admission is decided by the renderer, not by storage.
        /// </summary>
        internal bool TryUpdate(
            ref TextStudio2DStyleTableHandle handle,
            CompiledTextStudio2DStyle style,
            out TextStudio2DFieldProgramStatus status)
        {
            ThrowIfDisposed();
            status = ValidateStorage(style);
            if (status != TextStudio2DFieldProgramStatus.Ready)
            {
                Release(ref handle);
                return status == TextStudio2DFieldProgramStatus.PassThrough;
            }

            if (IsCurrent(handle) &&
                _slotPrograms[handle.Slot].Fingerprint.Equals(style.Fingerprint))
            {
                return true;
            }

            if (!IsCurrent(handle) && _freeCount <= 0)
            {
                status = TextStudio2DFieldProgramStatus.CapacityExhausted;
                return false;
            }
            if (!_headerStorage.TryEnsureCapacity(MaximumStyles) || !_uniformStorage.TryEnsureCapacity(MaximumStyles))
            {
                status = TextStudio2DFieldProgramStatus.MemoryBudgetExceeded;
                return false;
            }
            if (!TryAcquireProgram(style, out Program program, out status))
            {
                Release(ref handle);
                return false;
            }

            if (!IsCurrent(handle))
            {
                int slot = _freeSlots[--_freeCount];
                SlotState state = _slots[slot];
                state.Active = true;
                _slots[slot] = state;
                _activeCount++;
                SetIdle(false);
                handle = new TextStudio2DStyleTableHandle(slot, state.Generation);
            }

            Program previous = _slotPrograms[handle.Slot];
            _slotPrograms[handle.Slot] = program;
            TextStudio2DGpuStyleHeader header = TextStudio2DGpuStylePacking.PackHeader(style);
            header.Program = new Vector4(program.OperationStart, program.StopStart, 0f, 0f);
            _headerStorage.Data[handle.Slot] = header;
            ReleaseProgram(previous);
            MarkDirty(handle.Slot);
            return true;
        }

        internal void Release(ref TextStudio2DStyleTableHandle handle)
        {
            if (_disposed || !IsCurrent(handle))
            {
                handle = default;
                return;
            }

            int slot = handle.Slot;
            _headerStorage.Data[slot] = default;
            _uniformStorage.Data[slot] = default;
            ReleaseProgram(_slotPrograms[slot]);
            _slotPrograms[slot] = null;

            SlotState state = _slots[slot];
            state.Active = false;
            state.Generation++;
            if (state.Generation == 0u)
                state.Generation = 1u;
            _slots[slot] = state;
            _freeSlots[_freeCount++] = slot;
            _activeCount--;
            MarkDirty(slot);
            MarkUniformDirty(slot);
            if (_activeCount == 0)
                SetIdle(true);
            handle = default;
        }

        internal void FlushAndBind()
        {
            ThrowIfDisposed();
            if (_activeCount == 0 || _gpuCreationFailed || !IsGpuSupported)
            {
                Shader.SetGlobalFloat(s_enabledProperty, 0f);
                Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 0f);
                return;
            }

            bool uploaded;
            try
            {
                uploaded = Publish(_headerStorage, _dirtySlots, _dirtyCount, false);
                uploaded |= Publish(_uniformStorage, _uniformDirtySlots, _uniformDirtyCount, true);
                uploaded |= Publish(_operationStorage, _dirtyOperationRanges);
                uploaded |= Publish(_stopStorage, _dirtyStopRanges);
            }
            catch (Exception exception)
            {
                _gpuCreationFailed = true;
                ReleaseGpu();
                Debug.LogWarning(
                    "[TextStudio] The shared 2D style table could not be created. " +
                    "Appearance styles will use the platform fallback. " + exception.Message);
                Shader.SetGlobalFloat(s_enabledProperty, 0f);
                Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 0f);
                return;
            }
            if (!_headerStorage.HasGpuStorage || !_uniformStorage.HasGpuStorage ||
                !_operationStorage.HasGpuStorage || !_stopStorage.HasGpuStorage)
            {
                Shader.SetGlobalFloat(s_enabledProperty, 0f);
                Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 0f);
                return;
            }
            ClearDirtyList();
            ClearUniformDirtyList();
            _dirtyOperationRanges.Clear();
            _dirtyStopRanges.Clear();

            if (_activeTextureReferenceCount > 0)
                TextStudio2DTextureArrayTable.Shared.BindGlobal();
            // A fallback reset disables the table within the frame; rebind rather than trusting the frame marker.
            int frame = Time.frameCount;
            if (_lastBoundFrame == frame && !uploaded && Shader.GetGlobalFloat(s_enabledProperty) > .5f)
                return;
            // Data textures apply their pending texel edits here, once per frame.

            _headerStorage.Bind(s_headersProperty, s_headersData);
            _operationStorage.Bind(s_operationsProperty, s_operationsData);
            _stopStorage.Bind(s_gradientStopsProperty, s_gradientStopsData);
            _uniformStorage.Bind(s_textUniformsProperty, s_textUniformsData);
            Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 1f);
            Shader.SetGlobalFloat(s_enabledProperty, 1f);
            _lastBoundFrame = frame;
        }

        internal bool IsCurrent(TextStudio2DStyleTableHandle handle)
        {
            return handle.IsValid &&
                   handle.Slot < MaximumStyles &&
                   _slots[handle.Slot].Active &&
                   _slots[handle.Slot].Generation == handle.Generation;
        }

        internal TextStudio2DGpuStyleHeader GetHeaderForTests(int slot)
        {
            return _headerStorage.Data[slot];
        }

        internal TextStudio2DGpuOperation GetOperationForTests(int slot, int operation)
        {
            Program program = _slotPrograms[slot];
            return program != null && operation < program.OperationCount
                ? _operationStorage.Data[program.OperationStart + operation]
                : default;
        }

        internal TextStudio2DGpuGradientStop GetGradientStopForTests(int slot, int stop)
        {
            Program program = _slotPrograms[slot];
            return program != null && stop < program.StopCount
                ? _stopStorage.Data[program.StopStart + stop]
                : default;
        }

        internal TextStudio2DGpuTextUniform GetTextUniformForTests(int slot)
        {
            return _uniformStorage.Data[slot];
        }

        internal bool HasVisualModifiers(TextStudio2DStyleTableHandle handle)
        {
            return IsCurrent(handle) &&
                   ((uint)Mathf.RoundToInt(_headerStorage.Data[handle.Slot].Metadata.w) &
                    (uint)TextStudio2DShaderFeatures.AnimatedEffect) != 0u;
        }

        internal bool TryUpdateVisualUniform(
            TextStudio2DStyleTableHandle handle,
            in TextStudio2DVisualModifierUniformState state)
        {
            ThrowIfDisposed();
            if (!HasVisualModifiers(handle))
                return false;

            TextStudio2DGpuTextUniform packed =
                TextStudio2DVisualModifierPacking.PackUniform(in state);
            if (_uniformStorage.Data[handle.Slot].Equals(in packed))
                return true;

            _uniformStorage.Data[handle.Slot] = packed;
            MarkUniformDirty(handle.Slot);
            return true;
        }

        /// <summary>Storage admission shared by every execution route.</summary>
        internal static TextStudio2DFieldProgramStatus ValidateStorage(
            CompiledTextStudio2DStyle style)
        {
            if (style == null || style.IsPassThrough)
                return TextStudio2DFieldProgramStatus.PassThrough;
            if (!style.IsValid || style.OperationCount > MaximumProgramOperations)
                return TextStudio2DFieldProgramStatus.InvalidStyle;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (!TextStudio2DGpuStylePacking.OperationUsesPaint(operation.Kind))
                    continue;
                if (operation.PaintIndex < 0 || operation.PaintIndex >= style.PaintCount)
                    return TextStudio2DFieldProgramStatus.UnsupportedPaint;
                TextStudio2DCompiledPaint paint = style.GetPaint(operation.PaintIndex);
                if (!IsSupportedPaint(paint.Kind))
                    return TextStudio2DFieldProgramStatus.UnsupportedPaint;
                if (paint.GradientStopCount > MaximumGradientStops)
                    return TextStudio2DFieldProgramStatus.InvalidStyle;
            }
            return TextStudio2DFieldProgramStatus.Ready;
        }

        /// <summary>Admission of the direct field route: every operation must run as a field or retained primitive.</summary>
        internal static TextStudio2DFieldProgramStatus Validate(
            CompiledTextStudio2DStyle style)
        {
            TextStudio2DFieldProgramStatus storage = ValidateStorage(style);
            if (storage != TextStudio2DFieldProgramStatus.Ready)
                return storage;
            if (!TextStudio2DFieldPadding.TryResolveFused(style, out _))
                return TextStudio2DFieldProgramStatus.UnsupportedBounds;

            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                bool supportedField =
                    operation.ExecutionPath == TextStudio2DExecutionPath.FusedField &&
                    IsSupportedFieldOperation(operation.Kind);
                bool supportedRetained =
                    operation.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry &&
                    (operation.Kind == TextStudio2DLayerKind.Decoration ||
                     operation.Kind == TextStudio2DLayerKind.Stroke ||
                     operation.Kind == TextStudio2DLayerKind.FauxDepth ||
                     operation.SurfaceOwnerOperationIndex >= 0);
                if (!supportedField && !supportedRetained)
                {
                    return TextStudio2DFieldProgramStatus.UnsupportedExecutionPath;
                }
                if (operation.SurfaceOwnerOperationIndex < 0 && operation.BlendMode != TextStudio2DBlendMode.Normal)
                    return TextStudio2DFieldProgramStatus.UnsupportedBlend;
                if (operation.Kind == TextStudio2DLayerKind.AnimatedEffect &&
                    (!TextStudioVisualPrimitiveCatalog.TryGet(
                         operation.AnimatedEffect.Kind,
                         out TextStudioVisualPrimitiveContract primitive) ||
                     primitive.RequiresPostProcess))
                {
                    return TextStudio2DFieldProgramStatus.UnsupportedExecutionPath;
                }
            }

            return TextStudio2DFieldProgramStatus.Ready;
        }

        static bool IsSupportedPaint(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.Solid ||
                   kind == TextStudio2DPaintKind.LinearGradient ||
                   kind == TextStudio2DPaintKind.RadialGradient ||
                   kind == TextStudio2DPaintKind.ConicGradient ||
                   kind == TextStudio2DPaintKind.FourCornerGradient ||
                   kind == TextStudio2DPaintKind.Texture ||
                   kind == TextStudio2DPaintKind.Pattern ||
                   kind == TextStudio2DPaintKind.ProceduralNoise ||
                   kind == TextStudio2DPaintKind.DistanceGradient;
        }

        static bool IsSupportedFieldOperation(TextStudio2DLayerKind kind)
        {
            return kind == TextStudio2DLayerKind.Fill ||
                   kind == TextStudio2DLayerKind.Stroke ||
                   kind == TextStudio2DLayerKind.InnerShadow ||
                   kind == TextStudio2DLayerKind.OuterShadow ||
                   kind == TextStudio2DLayerKind.InnerGlow ||
                   kind == TextStudio2DLayerKind.OuterGlow ||
                   kind == TextStudio2DLayerKind.Bevel ||
                   kind == TextStudio2DLayerKind.FauxDepth ||
                   kind == TextStudio2DLayerKind.MaterialResponse ||
                   kind == TextStudio2DLayerKind.GraphicEffect ||
                   kind == TextStudio2DLayerKind.AnimatedEffect ||
                   kind == TextStudio2DLayerKind.ColorAdjustment;
        }

        bool TryAcquireProgram(
            CompiledTextStudio2DStyle style,
            out Program program,
            out TextStudio2DFieldProgramStatus status)
        {
            status = TextStudio2DFieldProgramStatus.Ready;
            if (_programs.TryGetValue(style.Fingerprint, out program))
            {
                program.References++;
                return true;
            }

            int operationCount = style.OperationCount;
            int stopCount = style.GradientStopCount;
            TextStudio2DTextureArrayHandle[] textures = new TextStudio2DTextureArrayHandle[operationCount];
            if (!TryAcquireTextureHandles(style, textures, out status))
                return false;
            if (!TryAllocate(_operationRanges, _operationStorage, operationCount, out int operationStart))
            {
                ReleaseTextureHandles(textures);
                status = TextStudio2DFieldProgramStatus.MemoryBudgetExceeded;
                return false;
            }
            if (!TryAllocate(_stopRanges, _stopStorage, stopCount, out int stopStart))
            {
                _operationRanges.Free(operationStart, operationCount);
                ReleaseTextureHandles(textures);
                status = TextStudio2DFieldProgramStatus.MemoryBudgetExceeded;
                return false;
            }

            TextStudio2DGpuOperation[] operations = _operationStorage.Data;
            for (int index = 0; index < operationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                operations[operationStart + index] = TextStudio2DGpuStylePacking.PackOperation(
                    style,
                    index,
                    textures[index].IsValid ? textures[index].Slice : -1,
                    TextStudio2DVisualModifierPacking.ResolveOperationFlags(in operation));
            }
            TextStudio2DGpuGradientStop[] stops = _stopStorage.Data;
            for (int index = 0; index < stopCount; index++)
                stops[stopStart + index] = TextStudio2DGpuStylePacking.PackGradient(style.GetGradientStop(index));
            for (int index = 0; index < textures.Length; index++)
                if (textures[index].IsValid) _activeTextureReferenceCount++;
            if (operationCount > 0) _dirtyOperationRanges.Add(new Vector2Int(operationStart, operationCount));
            if (stopCount > 0) _dirtyStopRanges.Add(new Vector2Int(stopStart, stopCount));

            program = new Program
            {
                Fingerprint = style.Fingerprint,
                OperationStart = operationStart,
                OperationCount = operationCount,
                StopStart = stopStart,
                StopCount = stopCount,
                References = 1,
                Textures = textures
            };
            _programs.Add(style.Fingerprint, program);
            return true;
        }

        void ReleaseProgram(Program program)
        {
            if (program == null || --program.References > 0)
                return;
            _programs.Remove(program.Fingerprint);
            Array.Clear(_operationStorage.Data, program.OperationStart, program.OperationCount);
            Array.Clear(_stopStorage.Data, program.StopStart, program.StopCount);
            _operationRanges.Free(program.OperationStart, program.OperationCount);
            _stopRanges.Free(program.StopStart, program.StopCount);
            for (int index = 0; index < program.Textures.Length; index++)
                if (program.Textures[index].IsValid) _activeTextureReferenceCount--;
            ReleaseTextureHandles(program.Textures);
        }

        /// <summary>Allocates a record range, growing the shared payload by powers of two when fragmentation or demand requires it.</summary>
        static bool TryAllocate<T>(
            TextStudioRangeAllocator ranges,
            TextStudioAppearanceBuffer<T> storage,
            int count,
            out int start) where T : struct
        {
            start = 0;
            if (count <= 0)
                return true;
            if (ranges.TryAllocate(count, out start))
                return true;
            int capacity = TextStudioBufferCapacity.ForCount(Math.Max(ranges.Capacity * 2, ranges.Capacity + count));
            if (!storage.TryEnsureCapacity(capacity))
                return false;
            ranges.Grow(storage.Data.Length);
            return ranges.TryAllocate(count, out start);
        }

        static bool TryAcquireTextureHandles(
            CompiledTextStudio2DStyle style,
            TextStudio2DTextureArrayHandle[] textures,
            out TextStudio2DFieldProgramStatus status)
        {
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (!TextStudio2DGpuStylePacking.OperationUsesPaint(operation.Kind) || operation.PaintIndex < 0)
                    continue;
                TextStudio2DCompiledPaint paint = style.GetPaint(operation.PaintIndex);
                if (paint.Kind != TextStudio2DPaintKind.Texture &&
                    paint.Kind != TextStudio2DPaintKind.Pattern)
                {
                    continue;
                }

                if (!TextStudio2DTextureArrayTable.Shared.TryAcquire(
                        paint.Texture,
                        paint.ResourceId,
                        out textures[index],
                        out TextStudio2DTextureArrayStatus textureStatus))
                {
                    ReleaseTextureHandles(textures);
                    status = ResolveTextureFailure(textureStatus);
                    return false;
                }
            }

            status = TextStudio2DFieldProgramStatus.Ready;
            return true;
        }

        static void ReleaseTextureHandles(TextStudio2DTextureArrayHandle[] textures)
        {
            for (int index = 0; index < textures.Length; index++)
            {
                ref TextStudio2DTextureArrayHandle handle = ref textures[index];
                if (handle.IsValid)
                    TextStudio2DTextureArrayTable.Shared.Release(ref handle);
            }
        }

        static TextStudio2DFieldProgramStatus ResolveTextureFailure(
            TextStudio2DTextureArrayStatus status)
        {
            return status switch
            {
                TextStudio2DTextureArrayStatus.MemoryBudgetExceeded =>
                    TextStudio2DFieldProgramStatus.MemoryBudgetExceeded,
                TextStudio2DTextureArrayStatus.UnsupportedPlatform =>
                    TextStudio2DFieldProgramStatus.UnsupportedTexturePlatform,
                TextStudio2DTextureArrayStatus.CapacityExhausted =>
                    TextStudio2DFieldProgramStatus.TextureCapacityExhausted,
                TextStudio2DTextureArrayStatus.ResourceConflict =>
                    TextStudio2DFieldProgramStatus.TextureResourceConflict,
                _ => TextStudio2DFieldProgramStatus.TextureUploadFailed
            };
        }

        void MarkDirty(int slot)
        {
            if (_dirtyFlags[slot])
                return;
            _dirtyFlags[slot] = true;
            int insertionIndex = _dirtyCount;
            while (insertionIndex > 0 &&
                   _dirtySlots[insertionIndex - 1] > slot)
            {
                _dirtySlots[insertionIndex] = _dirtySlots[insertionIndex - 1];
                insertionIndex--;
            }
            _dirtySlots[insertionIndex] = slot;
            _dirtyCount++;
        }

        void MarkUniformDirty(int slot)
        {
            if (_uniformDirtyFlags[slot])
                return;
            _uniformDirtyFlags[slot] = true;
            int insertionIndex = _uniformDirtyCount;
            while (insertionIndex > 0 &&
                   _uniformDirtySlots[insertionIndex - 1] > slot)
            {
                _uniformDirtySlots[insertionIndex] =
                    _uniformDirtySlots[insertionIndex - 1];
                insertionIndex--;
            }
            _uniformDirtySlots[insertionIndex] = slot;
            _uniformDirtyCount++;
        }

        /// <summary>Uploads sorted dirty slots as coalesced ranges. A recreated buffer already holds the full mirror.</summary>
        bool Publish<T>(TextStudioAppearanceBuffer<T> storage, int[] dirtySlots, int dirtyCount, bool uniform) where T : struct
        {
            if (!storage.TryEnsureGpuBuffer(out bool created))
                return false;
            int stride = Marshal.SizeOf<T>();
            if (created)
            {
                RecordUpload(storage.Data.Length, stride, uniform);
                return true;
            }
            if (dirtyCount == 0)
                return false;
            int rangeStart = dirtySlots[0];
            int rangeEnd = rangeStart;
            for (int index = 1; index <= dirtyCount; index++)
            {
                if (index < dirtyCount && dirtySlots[index] == rangeEnd + 1)
                {
                    rangeEnd = dirtySlots[index];
                    continue;
                }
                storage.Upload(rangeStart, rangeEnd - rangeStart + 1);
                RecordUpload(rangeEnd - rangeStart + 1, stride, uniform);
                if (index < dirtyCount)
                    rangeStart = rangeEnd = dirtySlots[index];
            }
            return true;
        }

        bool Publish<T>(TextStudioAppearanceBuffer<T> storage, List<Vector2Int> dirtyRanges) where T : struct
        {
            if (storage.Data.Length == 0 && !storage.TryEnsureCapacity(1))
                return false;
            if (!storage.TryEnsureGpuBuffer(out bool created))
                return false;
            int stride = Marshal.SizeOf<T>();
            if (created)
            {
                RecordUpload(storage.Data.Length, stride, false);
                return true;
            }
            if (dirtyRanges.Count == 0)
                return false;
            dirtyRanges.Sort((left, right) => left.x.CompareTo(right.x));
            int start = dirtyRanges[0].x;
            int end = start + dirtyRanges[0].y;
            for (int index = 1; index <= dirtyRanges.Count; index++)
            {
                if (index < dirtyRanges.Count && dirtyRanges[index].x <= end)
                {
                    end = Math.Max(end, dirtyRanges[index].x + dirtyRanges[index].y);
                    continue;
                }
                storage.Upload(start, end - start);
                RecordUpload(end - start, stride, false);
                if (index < dirtyRanges.Count)
                {
                    start = dirtyRanges[index].x;
                    end = start + dirtyRanges[index].y;
                }
            }
            return true;
        }

        void ClearDirtyList()
        {
            for (int index = 0; index < _dirtyCount; index++)
                _dirtyFlags[_dirtySlots[index]] = false;
            _dirtyCount = 0;
        }

        void ClearUniformDirtyList()
        {
            for (int index = 0; index < _uniformDirtyCount; index++)
            {
                _uniformDirtyFlags[_uniformDirtySlots[index]] = false;
            }
            _uniformDirtyCount = 0;
        }

        void RecordUpload(int count, int stride, bool uniform)
        {
            _uploadCalls++;
            _uploadedBytes += (long)count * stride;
            if (!uniform)
                return;
            _uniformUploadCalls++;
            _uniformUploadedBytes += (long)count * stride;
        }

        void SetIdle(bool idle)
        {
            _headerStorage.SetIdle(idle);
            _uniformStorage.SetIdle(idle);
            _operationStorage.SetIdle(idle);
            _stopStorage.SetIdle(idle);
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextStudio2DStyleTable));
        }

        void ITextStudioAppearanceMemoryReclaimer.ReclaimUnused(long gpuBytes, long cpuBytes)
        {
            if (_disposed || _activeCount > 0) return;
            long ownedCpu = _headerStorage.CpuBytes + _uniformStorage.CpuBytes + _operationStorage.CpuBytes + _stopStorage.CpuBytes;
            long ownedGpu = _headerStorage.GpuBytes + _uniformStorage.GpuBytes + _operationStorage.GpuBytes + _stopStorage.GpuBytes;
            bool reclaimCpu = cpuBytes > 0 && ownedCpu > 0;
            bool reclaimGpu = gpuBytes > 0 && ownedGpu > 0;
            if (!reclaimCpu && !reclaimGpu) return;
            if (reclaimCpu) DisposePayloads();
            else ReleaseGpu();
            TextStudioAppearanceMemory.RecordEviction(TextStudioAppearanceResourceKind.Tables);
            _lastBoundFrame = -1;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            foreach (Program program in _programs.Values)
                ReleaseTextureHandles(program.Textures);
            _programs.Clear();
            _disposed = true;
            TextStudioAppearanceMemory.UnregisterIdleOwner(this);
            DisposePayloads();
            Shader.SetGlobalFloat(s_enabledProperty, 0f);
            Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 0f);
        }

        void DisposePayloads()
        {
            _headerStorage.Dispose();
            _uniformStorage.Dispose();
            _operationStorage.Dispose();
            _stopStorage.Dispose();
            _operationRanges.Clear();
            _stopRanges.Clear();
            _dirtyOperationRanges.Clear();
            _dirtyStopRanges.Clear();
            TextStudio2DGpuFallbackResources.RestoreStyleBindings();
        }

        void ReleaseGpu()
        {
            _headerStorage.ReleaseGpu();
            _uniformStorage.ReleaseGpu();
            _operationStorage.ReleaseGpu();
            _stopStorage.ReleaseGpu();
            TextStudio2DGpuFallbackResources.RestoreStyleBindings();
        }

        internal static void ResetForTests()
        {
            ResetSharedTable();
            TextStudio2DTextureArrayTable.ResetRuntimeState();
        }
    }
}
