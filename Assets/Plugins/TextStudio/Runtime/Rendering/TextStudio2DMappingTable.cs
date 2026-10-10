using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudio2DGpuMappingHeader
    {
        internal Vector4 Metadata;
        internal Vector4 ObjectBounds;
        /// <summary>x: per-operation range row base; y: selection words (64 operations each) per glyph row.</summary>
        internal Vector4 Program;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudio2DGpuGlyphMapping
    {
        internal Vector4 WordBounds;
        internal Vector4 LineBounds;
        internal Vector4 PreparedTransform;
        internal Vector4 PreparedBounds;
        internal Vector4 PreparedDecode;
        internal Vector4 SourceMetadata;
        internal Vector4 AppearanceBounds;
        internal Vector4 AppearanceMetadata;
    }

    /// <summary>
    /// Publishes exact semantic paint frames without adding mapping data to TMP materials or
    /// duplicating it per vertex. Layout changes rebuild compact per-text rows; steady animation
    /// keeps the buffers untouched.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DMappingTable : IDisposable
    {
        static TextStudio2DMappingTable s_shared;

        readonly MappingDomain _domain;
        readonly TextStudioPreparedFieldPages _pages = new TextStudioPreparedFieldPages();
        bool _disposed;

        internal static TextStudio2DMappingTable Shared =>
            s_shared ??= new TextStudio2DMappingTable();
        internal int PreparedPageCount => _pages.ActivePages;
        internal string GetMemoryWarning(int slot) => _domain.GetMemoryWarning(slot);
        internal bool CanRetryMemory(int slot) => _domain.CanRetryMemory(slot);
        internal long PreparedGpuBytes => _pages.GpuBytes;
        internal Vector4 GetPreparedQuality(int slot, int glyphIndex) => _domain.GetPreparedQuality(slot, glyphIndex);
        internal Vector4 GetPreparedDemand(int slot) => _domain.GetPreparedDemand(slot);
        internal bool CanRetryPreparedUpgrade(int slot) => _domain.CanRetryPreparedUpgrade(slot);

        internal TextStudio2DMappingTable()
        {
            _domain = new MappingDomain(
                _pages,
                TextStudio2DStyleTable.MaximumStyles,
                "_TextStudio2DMappingHeaders",
                "_TextStudio2DGlyphMappings",
                "_TextStudio2DRangeMappings",
                "_TextStudio2DMappingTableEnabled");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetRuntimeState()
        {
            s_shared?.Dispose();
            s_shared = null;
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void OnExitingPlayMode() => ResetRuntimeState();
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading() => ResetRuntimeState();
#endif

        /// <summary>Publishes the mapping rows of the program bound to <paramref name="handle"/>, for any execution route.</summary>
        internal bool Update(
            TextStudio2DStyleTableHandle handle,
            in TextStudioFrame frame,
            TMP_Text text,
            CompiledTextStudio2DStyle style,
            TextStudio2DBoundStyle binding = null,
            TextStudioPreparedFieldDemand demand = null,
            TextStudio2DExternalGlyphSource external = null)
        {
            ThrowIfDisposed();
            if (!handle.IsValid)
                return true;
            return _domain.Update(
                handle.Slot,
                handle.Generation,
                in frame,
                text,
                style, binding, demand, external);
        }

        internal void Release(TextStudio2DStyleTableHandle handle)
        {
            if (!_disposed && handle.IsValid)
                _domain.Release(handle.Slot, handle.Generation);
        }

        internal void FlushAndBind()
        {
            ThrowIfDisposed();
            _domain.FlushAndBind();
            _pages.Bind();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _domain.Dispose();
            _pages.Dispose();
        }

        internal static void ResetForTests()
        {
            ResetRuntimeState();
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextStudio2DMappingTable));
        }

        internal sealed class MappingDomain : IDisposable, ITextStudioAppearanceMemoryReclaimer
        {
            readonly TextStudioPreparedFieldPages _pages;
            readonly int _maximumStyles;
            readonly int _headersProperty;
            readonly int _glyphsProperty;
            readonly int _rangesProperty;
            readonly int _enabledProperty;
            readonly int _headersData;
            readonly int _glyphsData;
            readonly int _rangesData;
            readonly SlotEntry[] _slots;
            readonly TextStudioAppearanceBuffer<TextStudio2DGpuMappingHeader> _headerStorage;
            readonly TextStudioAppearanceBuffer<TextStudio2DGpuGlyphMapping> _glyphStorage;
            readonly TextStudioAppearanceBuffer<Vector4> _rangeStorage;
            readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
            TextStudio2DGpuMappingHeader[] _headers => _headerStorage.Data;
            Vector4[] _ranges => _rangeStorage.Data;
            TextStudio2DGpuGlyphMapping[] _packedGlyphs => _glyphStorage.Data;
            int _activeCount;
            bool _preparing;
            bool _published;
            bool _dirty = true;
            bool _disposed;

            internal MappingDomain(
                TextStudioPreparedFieldPages pages,
                int maximumStyles,
                string headersProperty,
                string glyphsProperty,
                string rangesProperty,
                string enabledProperty)
            {
                _pages = pages;
                _maximumStyles = maximumStyles;
                _headersProperty = Shader.PropertyToID(headersProperty);
                _glyphsProperty = Shader.PropertyToID(glyphsProperty);
                _rangesProperty = Shader.PropertyToID(rangesProperty);
                _enabledProperty = Shader.PropertyToID(enabledProperty);
                _headersData = Shader.PropertyToID(headersProperty + "Data");
                _glyphsData = Shader.PropertyToID(glyphsProperty + "Data");
                _rangesData = Shader.PropertyToID(rangesProperty + "Data");
                _slots = new SlotEntry[maximumStyles];
                _headerStorage = new TextStudioAppearanceBuffer<TextStudio2DGpuMappingHeader>(TextStudioAppearanceResourceKind.Tables, "Appearance mapping headers");
                _glyphStorage = new TextStudioAppearanceBuffer<TextStudio2DGpuGlyphMapping>(TextStudioAppearanceResourceKind.Tables, "Appearance glyph mappings");
                _rangeStorage = new TextStudioAppearanceBuffer<Vector4>(TextStudioAppearanceResourceKind.Tables, "Appearance range mappings");
                TextStudioAppearanceMemory.RegisterIdleOwner(this);
            }

            internal bool Update(
                int slot,
                uint generation,
                in TextStudioFrame frame,
                TMP_Text text,
                CompiledTextStudio2DStyle style,
                TextStudio2DBoundStyle binding,
                TextStudioPreparedFieldDemand demand,
                TextStudio2DExternalGlyphSource external = null)
            {
                _preparing = true;
                _published = false;
                try { return UpdateCore(slot, generation, in frame, text, style, binding, demand, external); }
                finally
                {
                    _preparing = false;
                    SetIdle(_activeCount == 0);
                    SlotEntry entry = slot > 0 && slot < _slots.Length ? _slots[slot] : null;
                    if (_published && entry != null) entry.PageAvailabilityRevision = _pages.AvailabilityRevision;
                    if (!string.IsNullOrEmpty(entry?.MemoryWarning)) entry.FailedAvailability = TextStudioAppearanceMemory.AvailabilityRevision;
                }
            }

            bool UpdateCore(
                int slot,
                uint generation,
                in TextStudioFrame frame,
                TMP_Text text,
                CompiledTextStudio2DStyle style,
                TextStudio2DBoundStyle binding,
                TextStudioPreparedFieldDemand demand,
                TextStudio2DExternalGlyphSource external)
            {
                // External glyphs read their field through mapping rows, so they always publish rows.
                if (_disposed ||
                    slot <= 0 ||
                    slot >= _maximumStyles ||
                    style == null ||
                    external == null &&
                    frame.AppearanceBinding == null && !UsesExtendedMapping(style) && (style.Requirements & TextStudio2DStyleRequirements.GlyphMajorPainterOrder) == 0 &&
                    frame.VisualSnapshot.RenderGlyphCount == frame.VisualSnapshot.GlyphCount &&
                    (frame.VisualSnapshot.FontSourceCount == 0 ||
                        style.SourceFieldRequest.Quality == TextStudio2DSourceFieldQuality.Native))
                {
                    Release(slot, generation);
                    return true;
                }

                TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
                if (!snapshot.IsCreated || snapshot.GlyphCount <= 0 || snapshot.RenderGlyphCount <= 0)
                {
                    Release(slot, generation);
                    return true;
                }

                SlotEntry entry = _slots[slot] ??= new SlotEntry();
                bool needsFields = snapshot.FontSourceCount > 0 && style.SourceFieldRequest.Quality != TextStudio2DSourceFieldQuality.Native;
                ulong qualityFingerprint = 0;
                if (needsFields)
                {
                    if (demand != null) entry.Demand = demand;
                    else
                    {
                        entry.Demand ??= new TextStudioPreparedFieldDemand();
                        entry.Demand.Update(in frame, text, style);
                    }
                    qualityFingerprint = entry.Demand.Fingerprint;
                }
                if (external != null)
                {
                    TextStudioDeterministicHash64 externalHash = default;
                    externalHash.Add(qualityFingerprint); externalHash.Add(external.FieldRevision); externalHash.Add(external.Count);
                    qualityFingerprint = externalHash.Value;
                }
                Rect rect = text != null && text.rectTransform != null
                    ? text.rectTransform.rect
                    : new Rect(
                        snapshot.SourceBounds.x,
                        snapshot.SourceBounds.y,
                        snapshot.SourceBounds.z - snapshot.SourceBounds.x,
                        snapshot.SourceBounds.w - snapshot.SourceBounds.y);
                Vector4 objectBounds = new Vector4(
                    rect.xMin,
                    rect.yMin,
                    rect.xMax,
                    rect.yMax);
                ulong mappingFingerprint = MappingFingerprint(style);
                ulong membershipFingerprint = MembershipFingerprint(style, in frame, _selectorPass);
                if (frame.AppearanceBinding != null && frame.AppearanceProgramIndex >= 0)
                {
                    TextStudioDeterministicHash64 appearanceHash = default;
                    appearanceHash.Add(membershipFingerprint); appearanceHash.Add(frame.AppearanceBinding.GetProgramRevision(frame.AppearanceProgramIndex));
                    appearanceHash.Add(frame.AppearanceProgramIndex);
                    membershipFingerprint = appearanceHash.Value;
                }
                TextStudioDeterministicHash64 fontHash = default;
                for (int index = 0; index < snapshot.FontSourceCount; index++)
                {
                    TextStudioVisualFontSource source = snapshot.FontSources[index];
                    fontHash.Add(source.FontIdentity);
                    fontHash.Add(source.Revision);
                }
                bool sameMappingInputs = entry.Active &&
                                  entry.Generation == generation &&
                                  entry.LayoutVersion == snapshot.Version.Layout &&
                                  entry.GeometryVersion == snapshot.Version.Geometry &&
                                  entry.AtlasVersion == snapshot.Version.Atlas &&
                                  entry.MaterialsVersion == snapshot.Version.Materials &&
                                  entry.FontFingerprint == fontHash.Value &&
                                  entry.PlanVersion == frame.Plan.SourceVersion &&
                                  ReferenceEquals(entry.AppearanceBindingIdentity, frame.AppearanceBinding) &&
                                  ReferenceEquals(entry.AppearancePlanIdentity, frame.AppearanceBinding?.Plan) &&
                                  entry.AppearanceProgramIndex == frame.AppearanceProgramIndex &&
                                  entry.RenderGlyphCount == snapshot.RenderGlyphCount &&
                                  entry.SourceBounds.Equals(snapshot.SourceBounds) &&
                                  entry.StyleFingerprint.Equals(style.Fingerprint) &&
                                  entry.MappingFingerprint == mappingFingerprint &&
                                  entry.ObjectBounds.Equals(objectBounds) &&
                                  entry.QualityFingerprint == qualityFingerprint &&
                                  !(entry.Fields?.HasPendingUpgrade == true && entry.PageAvailabilityRevision != _pages.AvailabilityRevision);
                if (sameMappingInputs && entry.MembershipFingerprint == membershipFingerprint && string.IsNullOrEmpty(entry.MemoryWarning)) return true;
                if (sameMappingInputs && string.IsNullOrEmpty(entry.MemoryWarning) &&
                    CanRefreshAppearanceSelection(entry, in frame, style) &&
                    (binding != null || entry.FallbackBinding != null))
                {
                    // Only selector membership changed. Keep this slot's committed geometry, prepared fields,
                    // and exact-sized payloads; structural or failed updates still use the transaction below.
                    TextStudio2DBoundStyle resolvedBinding = binding ?? entry.FallbackBinding;
                    resolvedBinding.Bind(style);
                    Array.Clear(entry.RangeBounds, 0, entry.RangeBounds.Length);
                    Array.Clear(entry.MatchFrames, 0, entry.MatchFrames.Length);
                    Array.Clear(entry.SelectionMasks, 0, entry.SelectionMasks.Length);
                    BuildSelectionMappings(entry, in frame, style, resolvedBinding, true);
                    ExpandAppearanceSelectionMappings(entry, in frame, style, resolvedBinding);
                    entry.MembershipFingerprint = membershipFingerprint;
                    _dirty = true;
                    _published = true;
                    return true;
                }
                int glyphCount = frame.AppearanceBinding != null && frame.AppearanceProgramIndex >= 0
                    ? frame.AppearanceBinding.GetProgramGlyphs(frame.AppearanceProgramIndex).Count : snapshot.RenderGlyphCount;
                int matchCount = CountMatchFrames(style, glyphCount, out bool usesAdjustments);
                int selectionCount = usesAdjustments ? glyphCount * SelectionWords(style) : 0;
                int wordCount = ResolveSemanticCount(in snapshot, true);
                int lineCount = ResolveSemanticCount(in snapshot, false);
                TextStudioDeterministicHash64 attempt = default;
                attempt.Add(generation); attempt.Add(snapshot.Version.Layout); attempt.Add(snapshot.Version.Geometry);
                attempt.Add(snapshot.Version.Atlas); attempt.Add(snapshot.Version.Materials); attempt.Add(fontHash.Value);
                attempt.Add(frame.Plan.SourceVersion); attempt.Add(mappingFingerprint); attempt.Add(membershipFingerprint);
                attempt.Add(objectBounds.x); attempt.Add(objectBounds.y); attempt.Add(objectBounds.z); attempt.Add(objectBounds.w);
                attempt.Add(qualityFingerprint);
                // Only resource admission failures are cached. Include every payload/workspace size:
                // replacement plans can reuse local revision numbers while requiring fewer rows.
                attempt.Add(glyphCount); attempt.Add(matchCount); attempt.Add(selectionCount);
                attempt.Add(snapshot.RenderGlyphCount); attempt.Add(wordCount); attempt.Add(lineCount);
                attempt.Add(frame.AppearanceBinding != null);
                attempt.Add(style.Fingerprint.Topology); attempt.Add(style.Fingerprint.Parameters);
                attempt.Add(style.Fingerprint.RenderState); attempt.Add(style.Fingerprint.Bounds); attempt.Add(style.Fingerprint.Complete);
                if (!string.IsNullOrEmpty(entry.MemoryWarning) && entry.FailedFingerprint == attempt.Value &&
                    entry.FailedAvailability == TextStudioAppearanceMemory.AvailabilityRevision) return false;

                long payloadBytes = (long)glyphCount * Marshal.SizeOf<TextStudio2DGpuGlyphMapping>() +
                    ((long)style.OperationCount + matchCount + selectionCount + 1) * 16;
                long workspaceBytes = ((long)wordCount + lineCount + snapshot.RenderGlyphCount) * Marshal.SizeOf<BoundsAccumulator>();
                if (frame.AppearanceBinding != null)
                {
                    payloadBytes += (long)glyphCount * sizeof(int) * 2;
                    workspaceBytes += (long)snapshot.RenderGlyphCount * Marshal.SizeOf<TextStudio2DGpuGlyphMapping>() +
                        ((long)CountMatchFrames(style, snapshot.RenderGlyphCount, out _) +
                            (usesAdjustments ? snapshot.RenderGlyphCount * SelectionWords(style) : 0)) * 16;
                }
                TextStudioAppearanceMemoryLease payload = null, workspace = null;
                SlotEntry candidate = null;
                try
                {
                    if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Tables,
                            "Per-text appearance mappings", 0, payloadBytes, out payload) ||
                        !TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Preparation,
                            "Appearance mapping bounds", 0, workspaceBytes, out workspace) ||
                        !TryEnsurePackedCapacity(slot, glyphCount, style.OperationCount + matchCount + selectionCount + 1))
                    {
                        entry.Generation = generation;
                        entry.MemoryWarning = TextStudioAppearanceMemory.LastRejection.ToString();
                        entry.FailedFingerprint = attempt.Value;
                        return false;
                    }
                    candidate = new SlotEntry { Memory = payload, Demand = entry.Demand };
                    payload = null;
                    candidate.Active = true;
                    candidate.Generation = generation;
                    candidate.LayoutVersion = snapshot.Version.Layout;
                    candidate.GeometryVersion = snapshot.Version.Geometry;
                    candidate.AtlasVersion = snapshot.Version.Atlas;
                    candidate.MaterialsVersion = snapshot.Version.Materials;
                    candidate.FontFingerprint = fontHash.Value;
                    candidate.PlanVersion = frame.Plan.SourceVersion;
                    candidate.StyleFingerprint = style.Fingerprint;
                    candidate.AppearancePlanIdentity = frame.AppearanceBinding?.Plan;
                    candidate.AppearanceBindingIdentity = frame.AppearanceBinding;
                    candidate.AppearanceProgramIndex = frame.AppearanceProgramIndex;
                    candidate.RenderGlyphCount = snapshot.RenderGlyphCount;
                    candidate.MappingFingerprint = mappingFingerprint;
                    candidate.MembershipFingerprint = membershipFingerprint;
                    candidate.ObjectBounds = objectBounds;
                    candidate.SourceBounds = snapshot.SourceBounds;
                    candidate.QualityFingerprint = qualityFingerprint;
                    candidate.External = external;
                    TextStudio2DBoundStyle resolvedBinding = binding ?? (candidate.FallbackBinding = new TextStudio2DBoundStyle());
                    resolvedBinding.Bind(style);
                    if (needsFields) candidate.Fields = entry.Fields ?? new TextStudioPreparedFieldBinding(_pages);
                    else candidate.Demand = null;
                    candidate.Fields?.BeginUpdate();
                    try
                    {
                        BuildEntry(candidate, in frame, style, resolvedBinding);
                        if (frame.AppearanceBinding != null && frame.AppearanceProgramIndex >= 0)
                            ExpandAppearanceMappings(candidate, in frame, style, resolvedBinding);
                    }
                    finally { candidate.Fields?.EndUpdate(); }
                    candidate.Memory.Commit();
                    bool wasActive = entry.Active;
                    if (ReferenceEquals(candidate.Fields, entry.Fields)) entry.Fields = null;
                    _slots[slot] = candidate;
                    candidate = null;
                    entry.Clear();
                    if (!wasActive) _activeCount++;
                    _dirty = true;
                    _published = true;
                    return true;
                }
                finally
                {
                    if (candidate != null && ReferenceEquals(candidate.Fields, entry.Fields)) candidate.Fields = null;
                    candidate?.Clear();
                    payload?.Dispose();
                    workspace?.Dispose();
                }
            }

            bool CanRefreshAppearanceSelection(SlotEntry entry, in TextStudioFrame frame, CompiledTextStudio2DStyle style)
            {
                TextStudio2DAppearanceBinding binding = frame.AppearanceBinding;
                if (binding == null || frame.AppearanceProgramIndex < 0 || entry.SourceGlyphIndices == null ||
                    !ReferenceEquals(entry.AppearancePlanIdentity, binding.Plan) || entry.AppearanceProgramIndex != frame.AppearanceProgramIndex ||
                    entry.RenderGlyphCount != frame.VisualSnapshot.RenderGlyphCount || !entry.SourceBounds.Equals(frame.VisualSnapshot.SourceBounds)) return false;
                IReadOnlyList<TextStudio2DAppearanceBinding.GlyphBinding> rows = binding.GetProgramGlyphs(frame.AppearanceProgramIndex);
                int matches = CountMatchFrames(style, rows.Count, out bool adjustments);
                if (entry.Glyphs.Length != rows.Count || entry.MatchFrames.Length != matches ||
                    entry.SelectionMasks.Length != (adjustments ? rows.Count * SelectionWords(style) : 0)) return false;
                for (int index = 0; index < rows.Count; index++)
                {
                    TextStudio2DAppearanceBinding.GlyphBinding row = rows[index];
                    TextStudio2DGpuGlyphMapping glyph = entry.Glyphs[index];
                    if (glyph.AppearanceMetadata.x != row.RenderGlyphIndex + 1 || glyph.AppearanceMetadata.y != row.OccurrenceIndex ||
                        !glyph.AppearanceBounds.Equals(binding.GetDomainBounds(row.OccurrenceIndex))) return false;
                }
                return true;
            }

            bool TryEnsurePackedCapacity(int replacedSlot, int glyphCount, int extraRangeCount)
            {
                for (int slot = 1; slot < _slots.Length; slot++)
                {
                    SlotEntry entry = _slots[slot];
                    if (slot == replacedSlot || entry?.Active != true) continue;
                    glyphCount = checked(glyphCount + entry.Glyphs.Length);
                    extraRangeCount = checked(extraRangeCount + entry.RangeBounds.Length + entry.MatchFrames.Length + entry.SelectionMasks.Length + 1);
                }
                return _headerStorage.TryEnsureCapacity(_maximumStyles) &&
                    _glyphStorage.TryEnsureCapacity(TextStudioBufferCapacity.ForCount(Mathf.Max(1, glyphCount))) &&
                    _rangeStorage.TryEnsureCapacity(TextStudioBufferCapacity.ForCount(Mathf.Max(1, extraRangeCount)));
            }

            internal string GetMemoryWarning(int slot) => slot > 0 && slot < _slots.Length ? _slots[slot]?.MemoryWarning ?? string.Empty : string.Empty;

            internal bool CanRetryMemory(int slot)
            {
                SlotEntry entry = slot > 0 && slot < _slots.Length ? _slots[slot] : null;
                return !string.IsNullOrEmpty(entry?.MemoryWarning) && entry.FailedAvailability != TextStudioAppearanceMemory.AvailabilityRevision;
            }

            internal Vector4 GetPreparedDemand(int slot)
            {
                SlotEntry entry = slot > 0 && slot < _slots.Length ? _slots[slot] : null;
                if (entry?.Active != true || entry.Demand == null) return default;
                return new Vector4(entry.Demand.MaximumDensity, entry.Demand.MaximumDistance,
                    entry.Fields?.HasPendingUpgrade == true ? 1 : 0, entry.Fields?.HasMissingCoverage == true ? 1 : 0);
            }

            internal bool CanRetryPreparedUpgrade(int slot)
            {
                SlotEntry entry = slot > 0 && slot < _slots.Length ? _slots[slot] : null;
                return entry?.Active == true && entry.Fields?.HasPendingUpgrade == true && entry.PageAvailabilityRevision != _pages.AvailabilityRevision;
            }

            static ulong MappingFingerprint(CompiledTextStudio2DStyle style)
            {
                TextStudioDeterministicHash64 hash = default;
                hash.Add((byte)style.SourceFieldRequest.Quality);
                hash.Add(style.SourceFieldRequest.MinimumPixelsPerEm);
                hash.Add(style.SourceFieldRequest.MinimumSafeDistanceEm);
                for (int index = 0; index < style.OperationCount; index++)
                {
                    ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                    hash.Add((byte)ResolveMapping(style, in operation));
                    hash.Add(UsesPaintMatchMapping(style, in operation));
                    hash.Add(UsesPatternMatchMapping(in operation));
                    hash.Add(operation.Kind == TextStudio2DLayerKind.ColorAdjustment);
                    hash.Add(style.GetSelector(operation.SelectorIndex).Fingerprint);
                }
                return hash.Value;
            }

            static ulong MembershipFingerprint(CompiledTextStudio2DStyle style, in TextStudioFrame frame, TextStudio2DSelectorPass pass)
            {
                if (frame.AppearanceBinding != null && frame.AppearanceProgramIndex >= 0)
                    return frame.AppearanceBinding.GetProgramRevision(frame.AppearanceProgramIndex);
                TextStudioDeterministicHash64 hash = default;
                pass.Begin();
                for (int index = 0; index < style.SelectorCount; index++)
                {
                    TextStudio2DCompiledSelector selector = style.GetSelector(index);
                    if (!selector.HasDynamicMembership) continue;
                    for (int glyph = 0; glyph < frame.VisualSnapshot.GlyphCount; glyph++)
                        hash.Add(TextStudio2DSelectorEvaluator.Evaluate(in selector, in frame, glyph, pass));
                }
                return hash.Value;
            }

            internal void Release(int slot, uint generation)
            {
                if (_disposed || slot <= 0 || slot >= _maximumStyles)
                    return;
                SlotEntry entry = _slots[slot];
                if (entry == null || entry.Generation != generation) return;
                if (entry.Active) _activeCount--;
                entry.Clear();
                SetIdle(_activeCount == 0);
                _dirty = true;
            }

            internal void FlushAndBind()
            {
                if (_disposed)
                    return;
                if (_dirty && !RebuildBuffers())
                {
                    Shader.SetGlobalFloat(_enabledProperty, 0f);
                    return;
                }
                if (!_headerStorage.HasGpuStorage || !_glyphStorage.HasGpuStorage || !_rangeStorage.HasGpuStorage)
                    return;
                _headerStorage.Bind(_headersProperty, _headersData);
                _glyphStorage.Bind(_glyphsProperty, _glyphsData);
                _rangeStorage.Bind(_rangesProperty, _rangesData);
                Shader.SetGlobalFloat(_enabledProperty, HasActiveSlots() ? 1f : 0f);
            }

            bool RebuildBuffers()
            {
                if (_activeCount == 0)
                {
                    ReleasePayloads();
                    Shader.SetGlobalFloat(_enabledProperty, 0f);
                    _dirty = false;
                    return true;
                }
                int glyphCount = 0;
                for (int slot = 1; slot < _maximumStyles; slot++)
                    if (_slots[slot]?.Active == true) glyphCount += _slots[slot].Glyphs.Length;
                Array.Clear(_headers, 0, _headers.Length);
                Array.Clear(_ranges, 0, _ranges.Length);
                Array.Clear(_packedGlyphs, 0, _packedGlyphs.Length);
                int rangeOffset = 0;
                int glyphOffset = 0;
                for (int slot = 1; slot < _maximumStyles; slot++)
                {
                    SlotEntry entry = _slots[slot];
                    if (entry == null || !entry.Active)
                        continue;
                    // Each text owns one contiguous block: operation ranges, selection rows, source bounds, match frames.
                    int operationRanges = rangeOffset;
                    Array.Copy(entry.RangeBounds, 0, _ranges, rangeOffset, entry.RangeBounds.Length);
                    rangeOffset += entry.RangeBounds.Length;
                    int selectionStart = rangeOffset;
                    Array.Copy(entry.SelectionMasks, 0, _ranges, rangeOffset, entry.SelectionMasks.Length);
                    rangeOffset += entry.SelectionMasks.Length;
                    _headers[slot] = new TextStudio2DGpuMappingHeader
                    {
                        Metadata = new Vector4(
                            glyphOffset,
                            entry.Glyphs.Length,
                            entry.SelectionMasks.Length > 0 ? selectionStart + 1 : 0,
                            rangeOffset + 1),
                        ObjectBounds = entry.ObjectBounds,
                        Program = new Vector4(operationRanges, entry.SelectionWords, 0f, 0f)
                    };
                    Array.Copy(
                        entry.Glyphs,
                        0,
                        _packedGlyphs,
                        glyphOffset,
                        entry.Glyphs.Length);
                    glyphOffset += entry.Glyphs.Length;
                    _ranges[rangeOffset++] = entry.SourceBounds;
                    Array.Copy(entry.MatchFrames, 0, _ranges, rangeOffset, entry.MatchFrames.Length);
                    rangeOffset += entry.MatchFrames.Length;
                }

                try
                {
                    if (!Publish(_headerStorage, _headers.Length) ||
                        !Publish(_glyphStorage, Mathf.Max(1, glyphCount)) || !Publish(_rangeStorage, _ranges.Length)) return false;
                    _dirty = false;
                    return true;
                }
                catch (Exception exception)
                {
                    DisposeBuffers();
                    Debug.LogWarning(
                        "[TextStudio] Exact 2D paint mapping buffers could not be published. " +
                        "Extended mappings were disabled. " + exception.Message);
                    return false;
                }
            }

            static bool Publish<T>(TextStudioAppearanceBuffer<T> storage, int count) where T : struct
            {
                if (!storage.TryEnsureGpuBuffer(out bool uploaded)) return false;
                if (!uploaded) storage.Upload(0, count);
                return true;
            }

            bool HasActiveSlots() => _activeCount > 0;

            void SetIdle(bool idle)
            {
                _headerStorage.SetIdle(idle);
                _glyphStorage.SetIdle(idle);
                _rangeStorage.SetIdle(idle);
            }

            public void ReclaimUnused(long gpuBytes, long cpuBytes)
            {
                if (_disposed || _preparing || _activeCount != 0) return;
                bool reclaimCpu = cpuBytes > 0 && (_headerStorage.CpuBytes + _glyphStorage.CpuBytes + _rangeStorage.CpuBytes) > 0;
                bool reclaimGpu = gpuBytes > 0 && (_headerStorage.GpuBytes + _glyphStorage.GpuBytes + _rangeStorage.GpuBytes) > 0;
                if (!reclaimCpu && !reclaimGpu) return;
                if (reclaimCpu) ReleasePayloads();
                else DisposeBuffers();
                Shader.SetGlobalFloat(_enabledProperty, 0f);
                TextStudioAppearanceMemory.RecordEviction(TextStudioAppearanceResourceKind.Tables);
            }

            void ReleasePayloads()
            {
                _headerStorage.Dispose();
                _glyphStorage.Dispose();
                _rangeStorage.Dispose();
                TextStudio2DGpuFallbackResources.RestoreMappingBindings(_headersProperty, _glyphsProperty, _rangesProperty);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                for (int index = 1; index < _slots.Length; index++) _slots[index]?.Clear();
                TextStudioAppearanceMemory.UnregisterIdleOwner(this);
                _activeCount = 0;
                ReleasePayloads();
                Shader.SetGlobalFloat(_enabledProperty, 0f);
            }

            void DisposeBuffers()
            {
                _headerStorage.ReleaseGpu();
                _glyphStorage.ReleaseGpu();
                _rangeStorage.ReleaseGpu();
                TextStudio2DGpuFallbackResources.RestoreMappingBindings(_headersProperty, _glyphsProperty, _rangesProperty);
            }

            static void BuildEntry(
                SlotEntry entry,
                in TextStudioFrame frame,
                CompiledTextStudio2DStyle style,
                TextStudio2DBoundStyle binding)
            {
                TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
                int glyphCount = snapshot.RenderGlyphCount;
                BoundsAccumulator[] wordBounds = new BoundsAccumulator[
                    ResolveSemanticCount(in snapshot, true)];
                BoundsAccumulator[] lineBounds = new BoundsAccumulator[
                    ResolveSemanticCount(in snapshot, false)];
                BoundsAccumulator[] glyphBounds = new BoundsAccumulator[glyphCount];

                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                    BoundsAccumulator bounds = GlyphBounds(in glyph, frame.GetEffectUnitScale(glyph.TextUnitIndex));
                    glyphBounds[glyphIndex] = bounds;
                    if (glyph.WordIndex >= 0 && glyph.WordIndex < wordBounds.Length)
                        wordBounds[glyph.WordIndex].Include(in bounds);
                    if (glyph.LineIndex >= 0 && glyph.LineIndex < lineBounds.Length)
                        lineBounds[glyph.LineIndex].Include(in bounds);
                }

                entry.Glyphs = new TextStudio2DGpuGlyphMapping[glyphCount];
                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                    BoundsAccumulator fallback = glyphBounds[glyphIndex];
                    BoundsAccumulator word = glyph.WordIndex >= 0 &&
                                             glyph.WordIndex < wordBounds.Length &&
                                             wordBounds[glyph.WordIndex].IsValid
                        ? wordBounds[glyph.WordIndex]
                        : fallback;
                    BoundsAccumulator line = glyph.LineIndex >= 0 &&
                                             glyph.LineIndex < lineBounds.Length &&
                                             lineBounds[glyph.LineIndex].IsValid
                        ? lineBounds[glyph.LineIndex]
                        : fallback;
                    entry.Glyphs[glyphIndex] = new TextStudio2DGpuGlyphMapping
                    {
                        WordBounds = word.Vector,
                        LineBounds = line.Vector,
                        SourceMetadata = new Vector4(glyph.TextUnitIndex, glyph.FieldPixelsPerEm, (float)glyph.SourceKind, 0)
                    };
                    if (entry.External != null)
                        entry.External.TryMap(glyphIndex,
                            out entry.Glyphs[glyphIndex].PreparedTransform,
                            out entry.Glyphs[glyphIndex].PreparedBounds,
                            out entry.Glyphs[glyphIndex].PreparedDecode);
                    else if (glyph.SourceKind == TextStudioVisualSourceKind.FontGlyph && entry.Fields != null && entry.Demand.TryGetFont(glyph.FontIdentity, out TextStudioPreparedFieldDemand.Font font))
                        entry.Fields.TryMap(in glyph, font.Data, font.Selection.Tier,
                            out entry.Glyphs[glyphIndex].PreparedTransform,
                            out entry.Glyphs[glyphIndex].PreparedBounds,
                            out entry.Glyphs[glyphIndex].PreparedDecode);
                }

                entry.RangeBounds = new Vector4[style.OperationCount];
                int matchFrameCount = CountMatchFrames(style, glyphCount, out bool usesAdjustments);
                entry.MatchFrames = new Vector4[matchFrameCount];
                entry.SelectionWords = SelectionWords(style);
                entry.SelectionMasks = usesAdjustments ? new Vector4[glyphCount * entry.SelectionWords] : Array.Empty<Vector4>();
                BuildSelectionMappings(entry, in frame, style, binding, false, glyphBounds);
            }

            static void BuildSelectionMappings(SlotEntry entry, in TextStudioFrame frame,
                CompiledTextStudio2DStyle style, TextStudio2DBoundStyle binding,
                bool appearance, BoundsAccumulator[] glyphBounds = null)
            {
                TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
                int glyphCount = snapshot.RenderGlyphCount;
                int operationCount = style.OperationCount;
                CountMatchFrames(style, glyphCount, out bool usesAdjustments);
                int matchOffset = 0;
                for (int operationIndex = 0;
                     operationIndex < operationCount;
                     operationIndex++)
                {
                    ref readonly TextStudio2DCompiledOperation operation =
                        ref style.OperationAt(operationIndex);
                    bool paintMatch = UsesPaintMatchMapping(style, in operation);
                    bool patternMatch = UsesPatternMatchMapping(in operation);
                    if (!usesAdjustments && !paintMatch && !patternMatch && ResolveMapping(style, in operation) !=
                        TextStudio2DMappingSpace.Range)
                    {
                        continue;
                    }

                    BoundsAccumulator selectedBounds = default;
                    TextStudio2DResolvedSelection selection = binding.GetSelection(in frame, operation.SelectorIndex);
                    int paintStart = paintMatch ? matchOffset : -1;
                    if (paintMatch) matchOffset += glyphCount;
                    int patternStart = patternMatch ? matchOffset : -1;
                    if (patternMatch) matchOffset += glyphCount;
                    for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                    {
                        ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                        int owner = selection.GetOwner(glyph.TextUnitIndex);
                        bool selected = owner >= 0 && TextStudio2DSourceApplicabilityUtility.Includes(in operation, glyph.SourceKind);
                        if (!appearance && usesAdjustments && selected)
                        {
                            int row = glyphIndex * entry.SelectionWords + (operationIndex >> 6);
                            Vector4 mask = entry.SelectionMasks[row];
                            int chunk = (operationIndex >> 4) & 3;
                            mask[chunk] = (uint)mask[chunk] | 1u << (operationIndex & 15);
                            entry.SelectionMasks[row] = mask;
                        }
                        if (selected)
                        {
                            BoundsAccumulator bounds = glyphBounds != null ? glyphBounds[glyphIndex]
                                : GlyphBounds(in glyph, frame.GetEffectUnitScale(glyph.TextUnitIndex));
                            selectedBounds.Include(in bounds);
                        }
                        if (!appearance && paintMatch)
                            entry.MatchFrames[paintStart + glyphIndex] = selected
                                ? selection.GetBounds(owner) : snapshot.SourceBounds;
                    }
                    if (!appearance && patternMatch)
                        BuildPatternCoordinates(entry.MatchFrames, patternStart, selection, in snapshot);
                    entry.RangeBounds[operationIndex] = paintMatch || patternMatch
                        ? new Vector4(paintStart, glyphCount, patternStart, 0f)
                        : selectedBounds.IsValid
                        ? selectedBounds.Vector
                        : snapshot.SourceBounds;
                }
            }

            /// <summary>Selection rows hold 64 operations per word; programs beyond 64 operations add words instead of a cap.</summary>
            static int SelectionWords(CompiledTextStudio2DStyle style) => Mathf.Max(1, (style.OperationCount + 63) >> 6);

            static int CountMatchFrames(CompiledTextStudio2DStyle style, int glyphCount, out bool usesAdjustments)
            {
                int count = 0;
                usesAdjustments = false;
                for (int index = 0; index < style.OperationCount; index++)
                {
                    ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                    if (UsesPaintMatchMapping(style, in operation)) count = checked(count + glyphCount);
                    if (UsesPatternMatchMapping(in operation)) count = checked(count + glyphCount);
                    usesAdjustments |= operation.Kind == TextStudio2DLayerKind.ColorAdjustment;
                }
                return count;
            }

            internal Vector4 GetPreparedQuality(int slot, int glyphIndex)
            {
                if (_disposed || slot <= 0 || slot >= _slots.Length) return Vector4.zero;
                SlotEntry entry = _slots[slot];
                if (entry?.SourceGlyphIndices != null && !entry.SourceGlyphIndices.TryGetValue(glyphIndex, out glyphIndex)) return default;
                TextStudio2DGpuGlyphMapping[] glyphs = entry?.Glyphs;
                return entry != null && entry.Active && glyphIndex >= 0 && glyphs != null && glyphIndex < glyphs.Length
                    ? glyphs[glyphIndex].PreparedDecode : Vector4.zero;
            }

            static bool UsesPaintMatchMapping(CompiledTextStudio2DStyle style, in TextStudio2DCompiledOperation operation)
            {
                return ResolveMapping(style, in operation) == TextStudio2DMappingSpace.Range &&
                       operation.PaintIndex >= 0 && operation.PaintIndex < style.PaintCount &&
                       style.GetPaint(operation.PaintIndex).RangeMapping == TextStudio2DRangeMapping.EachMatch;
            }

            static bool UsesPatternMatchMapping(in TextStudio2DCompiledOperation operation)
            {
                return operation.Kind == TextStudio2DLayerKind.Decoration &&
                    operation.Decoration.Kind <= TextStudio2DDecorationKind.Overline &&
                    operation.Decoration.Line.Pattern >= TextStudio2DLinePattern.Dotted &&
                    operation.Decoration.Line.PhaseRestart == TextStudio2DLinePhaseRestart.Match;
            }

            static void BuildPatternCoordinates(Vector4[] destination, int start, TextStudio2DResolvedSelection selection, in TextStudioVisualSnapshot snapshot)
            {
                for (int fragmentIndex = 0; fragmentIndex < selection.FragmentCount; fragmentIndex++)
                {
                    TextStudio2DVisualFragment fragment = selection.GetFragment(fragmentIndex);
                    float direction = fragment.Direction.x < 0 ? -1f : 1f;
                    float offset = selection.GetFragmentDistance(fragmentIndex) +
                        (direction < 0 ? fragment.Bounds.z : -fragment.Bounds.x);
                    for (int member = fragment.FirstMember; member < fragment.FirstMember + fragment.MemberCount; member++)
                    {
                        int glyphIndex = snapshot.GetRenderGlyphIndexForUnit(selection.GetMember(member));
                        if (glyphIndex < 0) continue;
                        destination[start + glyphIndex] = new Vector4(offset, direction, 0f, 0f);
                    }
                }
                for (int index = 0; index < snapshot.RenderGlyphCount; index++)
                {
                    int first = snapshot.GetRenderGlyphIndexForUnit(snapshot.RenderGlyphAt(index).TextUnitIndex);
                    if (first >= 0) destination[start + index] = destination[start + first];
                }
            }

            static void ExpandAppearanceMappings(SlotEntry entry, in TextStudioFrame frame, CompiledTextStudio2DStyle style,
                TextStudio2DBoundStyle selections)
            {
                TextStudio2DAppearanceBinding binding = frame.AppearanceBinding;
                IReadOnlyList<TextStudio2DAppearanceBinding.GlyphBinding> rows = binding.GetProgramGlyphs(frame.AppearanceProgramIndex);
                TextStudio2DGpuGlyphMapping[] source = entry.Glyphs;
                entry.SourceGlyphIndices = new Dictionary<int, int>(rows.Count);
                entry.Glyphs = new TextStudio2DGpuGlyphMapping[rows.Count];
                int matchCount = CountMatchFrames(style, rows.Count, out bool usesAdjustments);
                entry.MatchFrames = new Vector4[matchCount];
                entry.SelectionMasks = usesAdjustments ? new Vector4[rows.Count * entry.SelectionWords] : Array.Empty<Vector4>();
                for (int index = 0; index < rows.Count; index++)
                {
                    TextStudio2DAppearanceBinding.GlyphBinding row = rows[index];
                    if (!entry.SourceGlyphIndices.ContainsKey(row.RenderGlyphIndex)) entry.SourceGlyphIndices.Add(row.RenderGlyphIndex, index);
                    TextStudioAppearanceOccurrence occurrence = binding.Plan.Occurrences[row.OccurrenceIndex];
                    TextStudio2DGpuGlyphMapping glyph = source[row.RenderGlyphIndex];
                    glyph.AppearanceBounds = binding.GetDomainBounds(row.OccurrenceIndex);
                    glyph.AppearanceMetadata = new Vector4(row.RenderGlyphIndex + 1, row.OccurrenceIndex, occurrence.Domain.Start, occurrence.Domain.Length);
                    entry.Glyphs[index] = glyph;
                }
                ExpandAppearanceSelectionMappings(entry, in frame, style, selections);
            }

            static void ExpandAppearanceSelectionMappings(SlotEntry entry, in TextStudioFrame frame,
                CompiledTextStudio2DStyle style, TextStudio2DBoundStyle selections)
            {
                TextStudio2DAppearanceBinding binding = frame.AppearanceBinding;
                IReadOnlyList<TextStudio2DAppearanceBinding.GlyphBinding> rows = binding.GetProgramGlyphs(frame.AppearanceProgramIndex);
                if (entry.SelectionMasks.Length > 0)
                    for (int index = 0; index < rows.Count; index++)
                    {
                        TextStudio2DAppearanceBinding.GlyphBinding row = rows[index];
                        for (int word = 0; word < entry.SelectionWords; word++)
                        {
                            ulong mask = binding.GetOperationMask(row.OccurrenceIndex, (int)entry.Glyphs[index].SourceMetadata.x, word);
                            entry.SelectionMasks[index * entry.SelectionWords + word] = new Vector4(mask & 65535UL, (mask >> 16) & 65535UL,
                                (mask >> 32) & 65535UL, (mask >> 48) & 65535UL);
                        }
                    }
                int offset = 0;
                for (int operationIndex = 0; operationIndex < style.OperationCount; operationIndex++)
                {
                    ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
                    bool paintMatch = UsesPaintMatchMapping(style, in operation), patternMatch = UsesPatternMatchMapping(in operation);
                    int paintStart = paintMatch ? offset : -1;
                    if (paintMatch) offset += rows.Count;
                    int patternStart = patternMatch ? offset : -1;
                    if (patternMatch) offset += rows.Count;
                    if (!paintMatch && !patternMatch) continue;
                    entry.RangeBounds[operationIndex] = new Vector4(paintStart, rows.Count, patternStart, 0);
                    for (int occurrenceIndex = 0; occurrenceIndex < binding.Plan.Occurrences.Count; occurrenceIndex++)
                    {
                        TextStudioAppearanceOccurrence occurrence = binding.Plan.Occurrences[occurrenceIndex];
                        if (!occurrence.IsActive || occurrence.ProgramIndex != frame.AppearanceProgramIndex) continue;
                        TextStudioFrame scoped = new TextStudioFrame(in frame, style, occurrenceIndex, binding);
                        TextStudio2DResolvedSelection selection = selections.GetSelection(in scoped, operation.SelectorIndex);
                        IReadOnlyList<int> occurrenceGlyphs = binding.GetOccurrenceRenderGlyphs(occurrenceIndex);
                        if (paintMatch)
                            for (int glyphIndex = 0; glyphIndex < occurrenceGlyphs.Count; glyphIndex++)
                            {
                                int glyph = occurrenceGlyphs[glyphIndex];
                                int unit = frame.VisualSnapshot.RenderGlyphAt(glyph).TextUnitIndex;
                                int owner = selection.GetOwner(unit);
                                int mapping = binding.GetMappingGlyphIndex(occurrenceIndex, glyph);
                                entry.MatchFrames[paintStart + mapping] = owner >= 0 ? selection.GetBounds(owner) : binding.GetDomainBounds(occurrenceIndex);
                            }
                        if (patternMatch)
                        {
                            for (int fragmentIndex = 0; fragmentIndex < selection.FragmentCount; fragmentIndex++)
                            {
                                TextStudio2DVisualFragment fragment = selection.GetFragment(fragmentIndex);
                                float direction = fragment.Direction.x < 0 ? -1 : 1;
                                float distance = selection.GetFragmentDistance(fragmentIndex) + (direction < 0 ? fragment.Bounds.z : -fragment.Bounds.x);
                                for (int member = fragment.FirstMember; member < fragment.FirstMember + fragment.MemberCount; member++)
                                {
                                    int glyph = frame.VisualSnapshot.GetRenderGlyphIndexForUnit(selection.GetMember(member));
                                    int mapping = binding.GetMappingGlyphIndex(occurrenceIndex, glyph);
                                    if (mapping >= 0) entry.MatchFrames[patternStart + mapping] = new Vector4(distance, direction, 0, 0);
                                }
                            }
                            for (int glyphIndex = 0; glyphIndex < occurrenceGlyphs.Count; glyphIndex++)
                            {
                                int glyph = occurrenceGlyphs[glyphIndex];
                                int first = frame.VisualSnapshot.GetRenderGlyphIndexForUnit(frame.VisualSnapshot.RenderGlyphAt(glyph).TextUnitIndex);
                                int mappedFirst = binding.GetMappingGlyphIndex(occurrenceIndex, first);
                                int mapping = binding.GetMappingGlyphIndex(occurrenceIndex, glyph);
                                if (mappedFirst >= 0) entry.MatchFrames[patternStart + mapping] = entry.MatchFrames[patternStart + mappedFirst];
                            }
                        }
                    }
                }
            }

            static int ResolveSemanticCount(
                in TextStudioVisualSnapshot snapshot,
                bool word)
            {
                int maximum = -1;
                for (int glyphIndex = 0; glyphIndex < snapshot.RenderGlyphCount; glyphIndex++)
                {
                    ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                    maximum = Mathf.Max(maximum, word ? glyph.WordIndex : glyph.LineIndex);
                }
                return maximum + 1;
            }

            static BoundsAccumulator GlyphBounds(
                in TextStudioVisualGlyph glyph,
                float emScale)
            {
                float left = Mathf.Min(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
                float right = Mathf.Max(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
                float bottom = glyph.Baseline - Mathf.Max(.2f, emScale * .2f);
                float top = glyph.Baseline + Mathf.Max(.8f, emScale * .8f);
                if (glyph.IsVisible)
                {
                    left = Mathf.Min(left, glyph.BoundsMin.x);
                    right = Mathf.Max(right, glyph.BoundsMax.x);
                    bottom = glyph.BoundsMin.y;
                    top = glyph.BoundsMax.y;
                }
                if (right - left < .0001f)
                    right = left + .0001f;
                if (top - bottom < .0001f)
                    top = bottom + .0001f;
                return new BoundsAccumulator(left, bottom, right, top);
            }

            static TextStudio2DMappingSpace ResolveMapping(
                CompiledTextStudio2DStyle style,
                in TextStudio2DCompiledOperation operation)
            {
                if (TextStudio2DGpuStylePacking.OperationUsesPaint(operation.Kind) &&
                    operation.PaintIndex >= 0 &&
                    operation.PaintIndex < style.PaintCount)
                {
                    return style.GetPaint(operation.PaintIndex).Mapping;
                }
                if (operation.Kind == TextStudio2DLayerKind.GraphicEffect)
                    return operation.GraphicEffect.Mapping;
                if (operation.Kind == TextStudio2DLayerKind.AnimatedEffect)
                    return operation.AnimatedEffect.Mapping;
                return TextStudio2DMappingSpace.Glyph;
            }

            static bool UsesExtendedMapping(CompiledTextStudio2DStyle style)
            {
                for (int index = 0; index < style.OperationCount; index++)
                {
                    ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                    TextStudio2DMappingSpace mapping = ResolveMapping(style, in operation);
                    if (operation.Kind == TextStudio2DLayerKind.ColorAdjustment) return true;
                    if (operation.Kind == TextStudio2DLayerKind.Decoration && operation.Decoration.Line.Pattern >= TextStudio2DLinePattern.Dotted)
                        return true;
                    if (mapping != TextStudio2DMappingSpace.Glyph &&
                        mapping != TextStudio2DMappingSpace.Text)
                    {
                        return true;
                    }
                }
                return false;
            }

            sealed class SlotEntry
            {
                internal Dictionary<int, int> SourceGlyphIndices;
                internal bool Active;
                internal TextStudioAppearanceMemoryLease Memory;
                internal string MemoryWarning = string.Empty;
                internal ulong FailedFingerprint;
                internal uint FailedAvailability;
                internal uint Generation;
                internal uint LayoutVersion;
                internal uint GeometryVersion;
                internal uint AtlasVersion;
                internal uint MaterialsVersion;
                internal ulong FontFingerprint;
                internal TextStudioPreparedFieldBinding Fields;
                internal TextStudioPreparedFieldDemand Demand;
                /// <summary>Engine-owned glyph fields the rows name instead of prepared fields.</summary>
                internal TextStudio2DExternalGlyphSource External;
                internal ulong QualityFingerprint;
                internal uint PageAvailabilityRevision;
                internal uint PlanVersion;
                internal CompiledTextStudioAppearancePlan AppearancePlanIdentity;
                internal TextStudio2DAppearanceBinding AppearanceBindingIdentity;
                internal int AppearanceProgramIndex;
                internal int RenderGlyphCount;
                internal TextStudioStackFingerprint StyleFingerprint;
                internal ulong MappingFingerprint;
                internal ulong MembershipFingerprint;
                internal TextStudio2DBoundStyle FallbackBinding;
                internal Vector4 ObjectBounds;
                internal Vector4 SourceBounds;
                internal TextStudio2DGpuGlyphMapping[] Glyphs =
                    Array.Empty<TextStudio2DGpuGlyphMapping>();
                internal Vector4[] RangeBounds = Array.Empty<Vector4>();
                internal Vector4[] MatchFrames = Array.Empty<Vector4>();
                internal Vector4[] SelectionMasks = Array.Empty<Vector4>();
                internal int SelectionWords = 1;

                internal void Clear()
                {
                    Fields?.Release();
                    Fields = null;
                    Demand = null;
                    External = null;
                    Active = false;
                    Memory?.Dispose();
                    Memory = null;
                    MemoryWarning = string.Empty;
                    FailedFingerprint = 0;
                    FailedAvailability = 0;
                    Glyphs = Array.Empty<TextStudio2DGpuGlyphMapping>();
                    SourceGlyphIndices = null;
                    AppearancePlanIdentity = null;
                    AppearanceBindingIdentity = null;
                    AppearanceProgramIndex = -1;
                    RenderGlyphCount = 0;
                    RangeBounds = Array.Empty<Vector4>();
                    MatchFrames = Array.Empty<Vector4>();
                    SelectionMasks = Array.Empty<Vector4>();
                    FallbackBinding?.Bind(null);
                    FallbackBinding = null;
                }
            }

            struct BoundsAccumulator
            {
                float _left;
                float _bottom;
                float _right;
                float _top;
                bool _initialized;

                internal bool IsValid => _initialized;
                internal Vector4 Vector => new Vector4(
                    _left,
                    _bottom,
                    _right,
                    _top);

                internal BoundsAccumulator(
                    float left,
                    float bottom,
                    float right,
                    float top)
                {
                    _left = left;
                    _bottom = bottom;
                    _right = right;
                    _top = top;
                    _initialized = true;
                }

                internal void Include(in BoundsAccumulator other)
                {
                    if (!other._initialized)
                        return;
                    if (!_initialized)
                    {
                        this = other;
                        return;
                    }
                    _left = Mathf.Min(_left, other._left);
                    _bottom = Mathf.Min(_bottom, other._bottom);
                    _right = Mathf.Max(_right, other._right);
                    _top = Mathf.Max(_top, other._top);
                }
            }
        }
    }
}
