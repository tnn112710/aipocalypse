using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Owns one stable shared-table slot for a TMP-backed Text Studio object. The slot references one interned program
    /// for every route; the route planner decides whether it draws directly or through isolated composition.
    /// </summary>
    internal sealed class TextStudioTMPAppearanceAdapter
    {
        internal TextStudio2DBoundStyle BoundStyle { get; } = new TextStudio2DBoundStyle();
        TextStudio2DStyleTableHandle _handle;
        bool _hasRetainedOperations;
        TextStudio2DStyleTableHandle _mappedHandle;
        TextStudio2DHeroProgram _heroProgram;
        TextStudio2DStyleTableHandle _visualUniformHandle;
        CompiledTextStudio2DStyle _compiledStyle;
        string _programCompatibilityWarning = string.Empty;
        string _retainedCompatibilityWarning = string.Empty;
        string _sourceCompatibilityWarning = string.Empty;
        string _mappingCompatibilityWarning = string.Empty;
        bool _mappingReady = true;
        SourceReadiness _sourceReadiness;
        bool _hasSourceReadiness;
        float _lastVisualUniformTime;
        TextStudioMotionPolicy _lastVisualMotionPolicy;
        uint _visualDiscontinuityGeneration;
        bool _hasVisualUniformState;
        uint _cameraUniformLease;
        TextStudioPreparedFieldDemand _sourceDemand;
        TextStudioFrame _sourceFrame;
        List<NativeTextureDependency> _nativeTextures;
        ulong _mappedQualityFingerprint, _readinessFingerprint;
        float _mappedOutputPixelsPerUnit;
        ulong _mappedGeometryFingerprint;
        internal TextStudioPreparedFieldDemand SourceDemand => _sourceDemand;
        internal float OutputPixelsPerUnit => _sourceDemand?.OutputPixelsPerUnit ?? 0;

        bool HasStoredProgram => TextStudio2DStyleTable.Shared.IsCurrent(_handle);
        /// <summary>A direct field program is bound. Composited programs report <see cref="HasHeroProgram"/> instead.</summary>
        internal bool HasActiveProgram => _heroProgram == null && HasStoredProgram;
        internal int StyleSlot => HasActiveProgram ? _handle.Slot : 0;
        internal bool RequiresFieldMaterial { get; private set; }
        internal int FieldStyleSlot => _mappingReady && RequiresFieldMaterial ? StyleSlot : 0;
        internal bool HasFusedModifiers =>
            TextStudio2DStyleTable.Shared.HasVisualModifiers(_handle);
        /// <summary>The direct program draws retained decoration, stroke or depth geometry in painter order.</summary>
        internal bool HasRetainedProgram => _mappingReady && _hasRetainedOperations && HasActiveProgram;
        bool HasAllocatedHeroProgram => _heroProgram != null && HasStoredProgram;
        internal bool HasHeroProgram => _mappingReady && HasAllocatedHeroProgram;
        internal int HeroStyleSlot => HasHeroProgram ? _handle.Slot : 0;
        internal TextStudio2DHeroProgram HeroProgram =>
            HasHeroProgram ? _heroProgram : null;
        internal TextStudio2DFieldPaddingTier PaddingTier { get; private set; }
        /// <summary>
        /// The direct field route must draw its own primitives: they extend beyond the padded source quad, or an
        /// isolated operation needs a single-operation primitive the native TMP quad cannot provide.
        /// </summary>
        internal bool RequiresOrderedPrimitives { get; private set; }
        internal TextStudio2DFieldProgramStatus Status { get; private set; }
        internal TextStudio2DHeroProgramStatus HeroProgramStatus { get; private set; }
        /// <summary>The executed route and the condition that selected it.</summary>
        internal TextStudio2DRouteDecision Route { get; private set; }
        /// <summary>
        /// The rows the text's TextMeshPro material draws on the TextMeshPro route (<see cref="TextStudio2DTextMeshProRoute"/>);
        /// invalid on every other route. The route holds no style-table program, payload or ordered primitives.
        /// </summary>
        internal TextStudio2DTextMeshProStyle TextMeshProStyle { get; private set; }
        internal bool UsesTextMeshProRoute => TextMeshProStyle.IsValid;
        internal string SourceQualityHint { get; private set; } = string.Empty;
        internal string CompatibilityWarning => CombineWarnings(
            _programCompatibilityWarning,
            _retainedCompatibilityWarning,
            _sourceCompatibilityWarning,
            _mappingCompatibilityWarning);

        internal Vector4 GetPreparedQuality(int renderGlyphIndex)
        {
            if (!_mappingReady || !_mappedHandle.IsValid) return Vector4.zero;
            return TextStudio2DMappingTable.Shared.GetPreparedQuality(_mappedHandle.Slot, renderGlyphIndex);
        }

        // Set by an update whose style cannot render (none, invalid or pass-through) without a lens; repeating it with the
        // same compiled style would only release the same state again.
        bool _releasedWithoutStyle;
        CompiledTextStudio2DStyle _releasedStyle;

        /// <param name="textMeshProStyle">
        /// The style's TextMeshPro rows when the caller found the text, its context and every source material able to draw them
        /// (<see cref="TextStudio2DTextMeshProRoute"/>); the route then needs no route planning and yields only to forced Hero output.
        /// </param>
        internal bool Update(
            CompiledTextStudio2DStyle style,
            bool forceHero = false,
            TMP_Text sourceText = null,
            TextStudio2DRouteReason compositionReason = TextStudio2DRouteReason.LensMagnifier,
            in TextStudio2DTextMeshProStyle textMeshProStyle = default)
        {
            if (!forceHero && _releasedWithoutStyle && ReferenceEquals(style, _releasedStyle))
                return false;
            _releasedWithoutStyle = false;
            _releasedStyle = null;
            bool wasActive = HasActiveProgram;
            bool wasHero = HasHeroProgram;
            bool previouslyRequiredFieldMaterial = RequiresFieldMaterial;
            TextStudio2DFieldPaddingTier previousPaddingTier = PaddingTier;
            bool previouslyOrdered = RequiresOrderedPrimitives;
            TextStudio2DTextMeshProStyle previousTextMeshProStyle = TextMeshProStyle;
            TextMeshProStyle = default;
            CompiledTextStudio2DStyle heroStyle = forceHero
                ? TextStudioLensMagnifierHeroStyle.Resolve(style)
                : style;
            _compiledStyle = heroStyle;
            _retainedCompatibilityWarning = string.Empty;
            if (heroStyle == null || !heroStyle.IsValid || heroStyle.IsPassThrough)
            {
                ClearSourceReadiness();
                ReleaseNativeTextures();
            }
            if (!forceHero && textMeshProStyle.IsValid)
                return SelectTextMeshProRoute(in textMeshProStyle, in previousTextMeshProStyle, wasActive, wasHero,
                    previouslyRequiredFieldMaterial, previousPaddingTier, previouslyOrdered);
            Route = TextStudio2DRoutePlanner.Resolve(heroStyle, sourceText, forceHero ? compositionReason : TextStudio2DRouteReason.None);
            bool leftTextMeshProRoute = previousTextMeshProStyle.IsValid;
            bool requiresHero = Route.IsComposited;
            RequiresOrderedPrimitives = Route.OwnsPrimitives;
            TextStudio2DCameraUniforms.Synchronize(ref _cameraUniformLease, requiresHero || style?.RequiresCameraPixels == true || style?.RequiresContourGeometry == true ||
                style != null && !style.IsPassThrough && style.SourceFieldRequest.Quality != TextStudio2DSourceFieldQuality.Native);
            _hasRetainedOperations = false;
            if (requiresHero)
            {
                RequiresFieldMaterial = false;
                PaddingTier = TextStudio2DFieldPaddingTier.None;
                Status = TextStudio2DFieldProgramStatus.PassThrough;

                TextStudio2DFieldProgramStatus storage = TextStudio2DFieldProgramStatus.GpuUnsupported;
                bool stored = TextStudio2DStyleTable.IsGpuSupported &&
                    TextStudio2DStyleTable.Shared.TryUpdate(ref _handle, heroStyle, out storage);
                if (!TextStudio2DStyleTable.IsGpuSupported)
                    TextStudio2DStyleTable.Shared.Release(ref _handle);
                if (stored && HasStoredProgram &&
                    (_heroProgram == null ||
                     !_heroProgram.Fingerprint.Equals(heroStyle.Fingerprint)))
                {
                    if (!TextStudio2DHeroProgram.TryCreate(
                            heroStyle,
                            out _heroProgram,
                            out TextStudio2DHeroProgramStatus programStatus))
                    {
                        TextStudio2DStyleTable.Shared.Release(ref _handle);
                    }
                    HeroProgramStatus = programStatus;
                }
                else if (!stored || !HasStoredProgram)
                {
                    _heroProgram = null;
                    HeroProgramStatus = TextStudio2DHeroProgramStatus.InvalidStyle;
                }
                else
                {
                    HeroProgramStatus = TextStudio2DHeroProgramStatus.Ready;
                }

                _programCompatibilityWarning = HeroProgramStatus != TextStudio2DHeroProgramStatus.Ready
                    ? BuildHeroWarning(HeroProgramStatus)
                    : BuildWarning(storage);
                return wasActive ||
                       wasHero != HasHeroProgram ||
                       previouslyRequiredFieldMaterial ||
                       previousPaddingTier != PaddingTier ||
                       previouslyOrdered ||
                       leftTextMeshProRoute;
            }

            _heroProgram = null;
            HeroProgramStatus = TextStudio2DHeroProgramStatus.Empty;
            TextStudio2DFieldProgramStatus validation = TextStudio2DStyleTable.Validate(style);
            if (validation == TextStudio2DFieldProgramStatus.Ready &&
                !TextStudio2DStyleTable.IsGpuSupported)
            {
                TextStudio2DStyleTable.Shared.Release(ref _handle);
                PaddingTier = TextStudio2DFieldPaddingTier.None;
                RequiresFieldMaterial = false;
                Status = TextStudio2DFieldProgramStatus.GpuUnsupported;
                _programCompatibilityWarning = BuildWarning(Status);
                return wasHero || wasActive || previousPaddingTier != PaddingTier || leftTextMeshProRoute;
            }

            bool updated = validation == TextStudio2DFieldProgramStatus.Ready
                ? TextStudio2DStyleTable.Shared.TryUpdate(ref _handle, style, out TextStudio2DFieldProgramStatus status)
                : ReleaseInvalid(validation, out status);
            Status = status;
            _programCompatibilityWarning = BuildWarning(status);
            _hasRetainedOperations = HasRetainedOperations(style);
            bool isActive = HasActiveProgram;
            RequiresFieldMaterial = isActive &&
                (HasFusedOperations(style) ||
                 style.WorldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped);
            PaddingTier = RequiresFieldMaterial
                ? Route.PaddingTier
                : TextStudio2DFieldPaddingTier.None;
            _releasedWithoutStyle = !forceHero && !isActive && (style == null || !style.IsValid || style.IsPassThrough);
            _releasedStyle = _releasedWithoutStyle ? style : null;
            return wasActive != isActive ||
                   wasHero ||
                   previouslyRequiredFieldMaterial != RequiresFieldMaterial ||
                   previousPaddingTier != PaddingTier ||
                   previouslyOrdered != RequiresOrderedPrimitives ||
                   !updated && wasActive ||
                   leftTextMeshProRoute;
        }

        // The TextMeshPro route draws with the text's own TMP material, so it holds no style-table program, mapping, padding tier
        // or ordered primitives. Entering or leaving it, or changing its rows, changes the material.
        bool SelectTextMeshProRoute(in TextStudio2DTextMeshProStyle textMeshProStyle, in TextStudio2DTextMeshProStyle previous, bool wasActive,
            bool wasHero, bool previouslyRequiredFieldMaterial, TextStudio2DFieldPaddingTier previousPaddingTier, bool previouslyOrdered)
        {
            Route = new TextStudio2DRouteDecision(TextStudio2DRoute.NativeField, TextStudio2DRouteReason.TextMeshProMaterial,
                TextStudio2DFieldPaddingTier.None);
            TextMeshProStyle = textMeshProStyle;
            RequiresOrderedPrimitives = false;
            TextStudio2DCameraUniforms.Synchronize(ref _cameraUniformLease, false);
            _hasRetainedOperations = false;
            _heroProgram = null;
            HeroProgramStatus = TextStudio2DHeroProgramStatus.Empty;
            TextStudio2DStyleTable.Shared.Release(ref _handle);
            RequiresFieldMaterial = false;
            PaddingTier = TextStudio2DFieldPaddingTier.None;
            Status = TextStudio2DFieldProgramStatus.Ready;
            _programCompatibilityWarning = string.Empty;
            return wasActive || wasHero || previouslyRequiredFieldMaterial || previousPaddingTier != PaddingTier || previouslyOrdered ||
                   !previous.Equals(textMeshProStyle);
        }

        bool ReleaseInvalid(TextStudio2DFieldProgramStatus validation, out TextStudio2DFieldProgramStatus status)
        {
            TextStudio2DStyleTable.Shared.Release(ref _handle);
            status = validation;
            return validation == TextStudio2DFieldProgramStatus.PassThrough;
        }

        // Without an active or hero program, mapping, prepared demand or native textures, and with the frame style already
        // bound, mapping is a no-op and the shared tables are published by the texts that use them.
        internal bool IsUnusedFor(CompiledTextStudio2DStyle frameStyle)
        {
            return (frameStyle == null || !frameStyle.IsValid || frameStyle.IsPassThrough) &&
                   ReferenceEquals(BoundStyle.Style, frameStyle) &&
                   (_compiledStyle == null || !_compiledStyle.IsValid || _compiledStyle.IsPassThrough) &&
                   _heroProgram == null &&
                   !_mappedHandle.IsValid &&
                   _sourceDemand == null &&
                   (_nativeTextures == null || _nativeTextures.Count == 0) &&
                   _mappingReady &&
                   !HasStoredProgram;
        }

        internal void FlushAndBind()
        {
            // Shader preparation can create fallbacks after a session reset. Initialize them
            // before publishing live tables so it cannot clear this frame's bindings.
            if (HasStoredProgram)
                TextStudio2DGpuFallbackResources.EnsureInitialized();
            if (HasStoredProgram)
                TextStudio2DStyleTable.Shared.FlushAndBind();
            TextStudio2DMappingTable.Shared.FlushAndBind();
        }

        internal void UpdateMappings(in TextStudioFrame frame, TMP_Text text, TextStudioTMPMaterialEffectAdapter nativeMaterials = null,
            TextStudio2DExternalGlyphSource external = null)
        {
            _mappingReady = true;
            _mappingCompatibilityWarning = string.Empty;
            UpdateNativeTextures(text, nativeMaterials);
            BoundStyle.Bind(HasHeroProgram ? _heroProgram.Style : frame.AppearanceStyle);
            CompiledTextStudio2DStyle sourceStyle = HasHeroProgram ? _heroProgram.Style : frame.AppearanceStyle;
            if (sourceStyle != null && sourceStyle.IsValid && !sourceStyle.IsPassThrough &&
                (HasHeroProgram || sourceStyle.RequiresContourGeometry || sourceStyle.SourceFieldRequest.Quality != TextStudio2DSourceFieldQuality.Native))
            {
                _sourceDemand ??= new TextStudioPreparedFieldDemand();
                _sourceDemand.Update(in frame, text, sourceStyle, nativeMaterials: nativeMaterials);
            }
            else _sourceDemand = null;
            if (!_mappedHandle.Equals(_handle))
            {
                TextStudio2DMappingTable.Shared.Release(_mappedHandle);
                _mappedHandle = default;
            }

            if (HasStoredProgram)
            {
                _mappingReady &= TextStudio2DMappingTable.Shared.Update(_handle, in frame, text, sourceStyle, BoundStyle, _sourceDemand, external);
                _mappedHandle = _handle;
            }
            if (!_mappingReady)
                _mappingCompatibilityWarning = TextStudio2DMappingTable.Shared.GetMemoryWarning(_handle.Slot);
            RefreshSourceCompatibility(in frame);
            _sourceFrame = _sourceDemand != null ? frame : default;
            _mappedQualityFingerprint = _sourceDemand?.Fingerprint ?? 0;
            _readinessFingerprint = _sourceDemand?.ReadinessFingerprint ?? 0;
            _mappedOutputPixelsPerUnit = OutputPixelsPerUnit;
            _mappedGeometryFingerprint = _sourceDemand?.GeometryFingerprint ?? 0;
        }

        // NeedsCameraQualityRefresh stays false until a tick maps this adapter again: there is no prepared-field demand to
        // revalidate and no rejected mapping that a later memory availability would retry. Only mapping writes the warning.
        internal bool HasStaticCameraQuality =>
            _sourceDemand == null && TextStudio2DMappingTable.Shared.GetMemoryWarning(_handle.Slot).Length == 0;

        internal bool NeedsCameraQualityRefresh(TMP_Text text)
        {
            if (TextStudio2DMappingTable.Shared.CanRetryMemory(_handle.Slot)) return true;
            if (_sourceDemand == null || _compiledStyle == null || !_sourceFrame.VisualSnapshot.IsCreated) return false;
            // Borrowed frame data stays valid between publications. Camera demand and native face-weight parameters can change while the hub sleeps.
            _sourceDemand.Update(in _sourceFrame, text, _compiledStyle, true);
            if (_readinessFingerprint != _sourceDemand.ReadinessFingerprint)
            {
                RefreshSourceCompatibility(in _sourceFrame);
                _readinessFingerprint = _sourceDemand.ReadinessFingerprint;
            }
            return _sourceDemand.Fingerprint != _mappedQualityFingerprint ||
                _compiledStyle.RequiresContourGeometry && _mappedGeometryFingerprint != _sourceDemand.GeometryFingerprint ||
                HasHeroProgram && !Mathf.Approximately(_mappedOutputPixelsPerUnit, OutputPixelsPerUnit) ||
                TextStudio2DMappingTable.Shared.CanRetryPreparedUpgrade(_mappedHandle.Slot);
        }

        internal bool UpdateVisualModifiers(
            in TextStudioFrame frame,
            TextStudioMotionPolicy motionPolicy)
        {
            if (!HasFusedModifiers)
            {
                ResetVisualUniformTracking();
                return false;
            }

            TextStudioMotionPolicy resolvedPolicy =
                TextStudioMotionSettings.Resolve(motionPolicy);
            DirtyFlags discontinuityFlags =
                DirtyFlags.Text |
                DirtyFlags.Topology |
                DirtyFlags.Layout |
                DirtyFlags.AppearanceTopology |
                DirtyFlags.AppearanceRenderState;
            float elapsed = frame.PresentationTime - _lastVisualUniformTime;
            float continuityTolerance = Mathf.Max(
                0.001f,
                frame.DeltaTime * 0.05f);
            bool discontinuous = !_hasVisualUniformState ||
                                 !_visualUniformHandle.Equals(_handle) ||
                                 resolvedPolicy != _lastVisualMotionPolicy ||
                                 elapsed < -continuityTolerance ||
                                 Mathf.Abs(elapsed - frame.DeltaTime) >
                                 continuityTolerance ||
                                 (frame.GlobalDirty & discontinuityFlags) != 0;
            if (discontinuous)
            {
                _visualDiscontinuityGeneration++;
                if (_visualDiscontinuityGeneration == 0u)
                    _visualDiscontinuityGeneration = 1u;
            }

            TextStudio2DVisualModifierUniformState state =
                new TextStudio2DVisualModifierUniformState(
                    frame.PresentationTime,
                    frame.DeltaTime,
                    frame.RevealProgress,
                    resolvedPolicy,
                    _visualDiscontinuityGeneration);
            bool accepted = TextStudio2DStyleTable.Shared.TryUpdateVisualUniform(
                _handle,
                in state);
            if (accepted)
            {
                _visualUniformHandle = _handle;
                _lastVisualUniformTime = frame.PresentationTime;
                _lastVisualMotionPolicy = resolvedPolicy;
                _hasVisualUniformState = true;
            }
            return accepted;
        }

        void UpdateNativeTextures(TMP_Text text, TextStudioTMPMaterialEffectAdapter nativeMaterials)
        {
            if (_compiledStyle == null || !_compiledStyle.IsValid || _compiledStyle.IsPassThrough || text == null)
            {
                ReleaseNativeTextures();
                return;
            }
            TMP_TextInfo info = text.textInfo;
            int count = info?.meshInfo == null ? 0 : Mathf.Min(info.materialCount, info.meshInfo.Length);
            if (count == 0) { ReleaseNativeTextures(); return; }
            _nativeTextures ??= new List<NativeTextureDependency>(count);
            while (_nativeTextures.Count > count)
            {
                int last = _nativeTextures.Count - 1;
                _nativeTextures[last].Lease?.Dispose();
                _nativeTextures.RemoveAt(last);
            }
            for (int index = 0; index < count; index++)
            {
                Material material = nativeMaterials != null
                    ? nativeMaterials.ResolveSourceMaterial(text, index) : info.meshInfo[index].material;
                Texture texture = info.meshInfo[index].vertexCount > 0 && material != null ? material.mainTexture : null;
                if (_nativeTextures.Count <= index) _nativeTextures.Add(default);
                NativeTextureDependency prior = _nativeTextures[index];
                if (ReferenceEquals(prior.Texture, texture) &&
                    (texture == null || prior.Revision == texture.updateCount)) continue;
                long bytes = TextStudioAppearanceMemory.TextureBytes(texture);
                if (ReferenceEquals(prior.Texture, texture) && prior.Bytes == bytes)
                {
                    _nativeTextures[index] = new NativeTextureDependency(texture, bytes, prior.Lease);
                    continue;
                }
                prior.Lease?.Dispose();
                _nativeTextures[index] = new NativeTextureDependency(texture, bytes, TextStudioAppearanceMemory.Borrow(texture));
            }
        }

        void ReleaseNativeTextures()
        {
            if (_nativeTextures == null) return;
            foreach (NativeTextureDependency dependency in _nativeTextures) dependency.Lease?.Dispose();
            _nativeTextures = null;
        }

        readonly struct NativeTextureDependency
        {
            internal readonly Texture Texture;
            internal readonly uint Revision;
            internal readonly long Bytes;
            internal readonly TextStudioBorrowedTextureLease Lease;
            internal NativeTextureDependency(Texture texture, long bytes, TextStudioBorrowedTextureLease lease)
            { Texture = texture; Revision = texture != null ? texture.updateCount : 0; Bytes = bytes; Lease = lease; }
        }

        internal void Release()
        {
            _releasedWithoutStyle = false;
            _releasedStyle = null;
            ReleaseNativeTextures();
            TextStudio2DCameraUniforms.Synchronize(ref _cameraUniformLease, false);
            BoundStyle.Bind(null);
            _sourceDemand = null;
            _sourceFrame = default;
            _mappedQualityFingerprint = _readinessFingerprint = 0;
            _mappedOutputPixelsPerUnit = 0;
            _mappedGeometryFingerprint = 0;
            TextStudio2DMappingTable.Shared.Release(_mappedHandle);
            _mappedHandle = default;
            TextStudio2DStyleTable.Shared.Release(ref _handle);
            _hasRetainedOperations = false;
            _heroProgram = null;
            PaddingTier = TextStudio2DFieldPaddingTier.None;
            RequiresFieldMaterial = false;
            RequiresOrderedPrimitives = false;
            Status = TextStudio2DFieldProgramStatus.PassThrough;
            HeroProgramStatus = TextStudio2DHeroProgramStatus.Empty;
            Route = default;
            TextMeshProStyle = default;
            _compiledStyle = null;
            _programCompatibilityWarning = string.Empty;
            _retainedCompatibilityWarning = string.Empty;
            _mappingCompatibilityWarning = string.Empty;
            _mappingReady = true;
            ClearSourceReadiness();
            _visualDiscontinuityGeneration = 0u;
            ResetVisualUniformTracking();
        }

        void ResetVisualUniformTracking()
        {
            _visualUniformHandle = default;
            _lastVisualUniformTime = 0f;
            _lastVisualMotionPolicy = TextStudioMotionPolicy.Inherit;
            _hasVisualUniformState = false;
        }

        static bool HasRetainedOperations(CompiledTextStudio2DStyle style)
        {
            if (style == null) return false;
            for (int index = 0; index < style.OperationCount; index++)
                if (style.OperationAt(index).ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry) return true;
            return false;
        }

        static bool HasFusedOperations(CompiledTextStudio2DStyle style)
        {
            if (style == null)
                return false;
            for (int index = 0; index < style.OperationCount; index++)
            {
                if (style.OperationAt(index).ExecutionPath ==
                    TextStudio2DExecutionPath.FusedField)
                {
                    return true;
                }
            }
            return false;
        }

        void RefreshSourceCompatibility(in TextStudioFrame frame)
        {
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            if (_compiledStyle == null || !_compiledStyle.IsValid || _compiledStyle.IsPassThrough || !snapshot.IsCreated)
            {
                ClearSourceReadiness();
                return;
            }

            TextStudio2DSourceFieldRequest request = _compiledStyle.SourceFieldRequest;
            Vector4 demand = TextStudio2DMappingTable.Shared.GetPreparedDemand(_mappedHandle.Slot);
            bool requiresContours = (_compiledStyle.Requirements &
                (TextStudio2DStyleRequirements.ContourDash | TextStudio2DStyleRequirements.ExactStrokeJoin)) != 0;
            bool allContours = true;
            float densityShortfall = 0;
            float availableDensity = 0;
            float requiredDensity = 0;
            bool distanceShortfall = false;
            bool foundVisibleField = false;
            IReadOnlyList<TextStudio2DAppearanceBinding.GlyphBinding> appearanceGlyphs = frame.AppearanceBinding != null && frame.AppearanceProgramIndex >= 0
                ? frame.AppearanceBinding.GetProgramGlyphs(frame.AppearanceProgramIndex) : null;
            int glyphCount = appearanceGlyphs?.Count ?? snapshot.RenderGlyphCount;
            for (int row = 0; row < glyphCount; row++)
            {
                int index = appearanceGlyphs != null ? appearanceGlyphs[row].RenderGlyphIndex : row;
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                if (appearanceGlyphs != null && !frame.AppearanceBinding.Contains(appearanceGlyphs[row].OccurrenceIndex, glyph.TextUnitIndex)) continue;
                if (!glyph.IsVisible)
                    continue;
                if (glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph) continue;
                foundVisibleField = true;
                Vector4 prepared = TextStudio2DMappingTable.Shared.GetPreparedQuality(_mappedHandle.Slot, index);
                float density = prepared.x > 0 ? prepared.z : glyph.FieldPixelsPerEm;
                float safeDistance = prepared.x > 0 ? prepared.w : glyph.FieldSafeDistanceEm;
                float requestedDensity = request.MinimumPixelsPerEm;
                float requestedDistance = request.MinimumSafeDistanceEm;
                if (request.Quality != TextStudio2DSourceFieldQuality.Native && _sourceDemand != null &&
                    _sourceDemand.TryGetFont(glyph.FontIdentity, out TextStudioPreparedFieldDemand.Font font))
                {
                    requestedDensity = font.Selection.RequiredPixelsPerEm;
                    requestedDistance = font.Selection.RequiredSafeDistanceEm;
                }
                float shortfall = requestedDensity - Mathf.Max(0, density);
                if (shortfall > densityShortfall)
                { densityShortfall = shortfall; availableDensity = density; requiredDensity = requestedDensity; }
                distanceShortfall |= requestedDistance > .0001f && safeDistance + .0001f < requestedDistance;
                allContours &= glyph.SourceKind == TextStudioVisualSourceKind.FontGlyph &&
                    snapshot.TryGetFontSource(glyph.FontIdentity, out _);
            }

            SourceReadiness readiness = new SourceReadiness(requiresContours && foundVisibleField && !allContours,
                densityShortfall > .01f, distanceShortfall, demand.z > 0, demand.w > 0,
                float.IsFinite(availableDensity) && availableDensity > 0 ? Mathf.RoundToInt(availableDensity) : -1,
                Mathf.RoundToInt(requiredDensity));
            if (_hasSourceReadiness && _sourceReadiness.Matches(in readiness)) return;
            _sourceReadiness = readiness;
            _hasSourceReadiness = true;
            _sourceCompatibilityWarning = string.Empty;
            SourceQualityHint = string.Empty;

            if (readiness.MissingContours)
            {
                AppendSourceWarning("Exact joins and contour dashes need prepared font outlines. Use Prepare Appearance Data for each active font. Glyphs without a contour source keep their native face while this outline layer stays inactive.");
            }
            if (readiness.LowDensity)
            {
                string measured = readiness.AvailableDensity >= 0
                    ? readiness.AvailableDensity.ToString()
                    : "unknown";
                AppendSourceWarning(
                    "An active font field provides " + measured +
                    " pixels per em, below the " +
                    readiness.RequiredDensity +
                    " pixels per em needed at this output scale. Prepare Appearance Data for this font and the current text, or select Native quality to accept the source resolution. The best available field remains in use.");
            }
            if (readiness.InsufficientDistance)
            {
                SourceQualityHint = "The available font field does not cover the full effect distance at this output scale. Prepare Appearance Data for each active font, or reduce the effect size. The best available field remains in use.";
            }
            if (readiness.CapacityExceeded) AppendSourceWarning("A sharper prepared field could not obtain a page within the shared memory budget. The previous usable field remains active; it will retry when page capacity becomes available.");
            if (readiness.MissingCoverage) AppendSourceWarning("Some glyphs are missing from the requested prepared tier. Prepare Appearance Data for the current text to extend its coverage.");
        }

        void ClearSourceReadiness()
        {
            _hasSourceReadiness = false;
            _sourceReadiness = default;
            _sourceCompatibilityWarning = string.Empty;
            SourceQualityHint = string.Empty;
        }

        readonly struct SourceReadiness
        {
            internal readonly bool MissingContours, LowDensity, InsufficientDistance, CapacityExceeded, MissingCoverage;
            internal readonly int AvailableDensity, RequiredDensity;

            internal SourceReadiness(bool missingContours, bool lowDensity, bool insufficientDistance,
                bool capacityExceeded, bool missingCoverage, int availableDensity, int requiredDensity)
            {
                MissingContours = missingContours; LowDensity = lowDensity; InsufficientDistance = insufficientDistance;
                CapacityExceeded = capacityExceeded; MissingCoverage = missingCoverage;
                AvailableDensity = lowDensity ? availableDensity : 0;
                RequiredDensity = lowDensity ? requiredDensity : 0;
            }

            internal bool Matches(in SourceReadiness other) => MissingContours == other.MissingContours && LowDensity == other.LowDensity &&
                InsufficientDistance == other.InsufficientDistance && CapacityExceeded == other.CapacityExceeded && MissingCoverage == other.MissingCoverage &&
                AvailableDensity == other.AvailableDensity && RequiredDensity == other.RequiredDensity;
        }

        void AppendSourceWarning(string warning)
        {
            _sourceCompatibilityWarning = string.IsNullOrEmpty(_sourceCompatibilityWarning)
                ? warning
                : _sourceCompatibilityWarning + " " + warning;
        }

        static string CombineWarnings(string first, string second, string third, string fourth)
        {
            string result = first ?? string.Empty;
            if (!string.IsNullOrEmpty(second))
                result = string.IsNullOrEmpty(result) ? second : result + " " + second;
            if (!string.IsNullOrEmpty(third))
                result = string.IsNullOrEmpty(result) ? third : result + " " + third;
            if (!string.IsNullOrEmpty(fourth))
                result = string.IsNullOrEmpty(result) ? fourth : result + " " + fourth;
            return result;
        }

        static string BuildWarning(TextStudio2DFieldProgramStatus status)
        {
            return status switch
            {
                TextStudio2DFieldProgramStatus.MemoryBudgetExceeded =>
                    TextStudioAppearanceMemory.LastRejection.ToString(),
                TextStudio2DFieldProgramStatus.InvalidStyle =>
                    "The assigned 2D appearance contains compiler errors or exceeds the 512-operation program limit.",
                TextStudio2DFieldProgramStatus.UnsupportedExecutionPath =>
                    "The assigned 2D appearance needs retained geometry or Hero compositing, which is not active in the current renderer checkpoint.",
                TextStudio2DFieldProgramStatus.UnsupportedPaint =>
                    "The assigned 2D appearance uses a paint family that is not active in the current field renderer checkpoint.",
                TextStudio2DFieldProgramStatus.UnsupportedBlend =>
                    "The assigned 2D appearance needs a non-normal blend path that is not active in the current field renderer checkpoint.",
                TextStudio2DFieldProgramStatus.UnsupportedMapping =>
                    "The assigned 2D appearance uses a paint mapping space that is not active in the current field renderer checkpoint.",
                TextStudio2DFieldProgramStatus.UnsupportedTexturePlatform =>
                    "This graphics device cannot populate the shared 2D texture array. Text Studio preserved the source TMP rendering.",
                TextStudio2DFieldProgramStatus.TextureCapacityExhausted =>
                    "The shared 2D paint texture array reached its bounded capacity of 64 active texture resources.",
                TextStudio2DFieldProgramStatus.TextureResourceConflict =>
                    "Two active 2D paint textures use the same stable resource ID with different content.",
                TextStudio2DFieldProgramStatus.TextureUploadFailed =>
                    "A 2D paint texture could not be prepared in the shared texture array. Text Studio preserved the source TMP rendering.",
                TextStudio2DFieldProgramStatus.UnsupportedBounds =>
                    "The assigned 2D appearance exceeds the prepared signed-distance range and needs retained geometry or a prepared wide-range font.",
                TextStudio2DFieldProgramStatus.CapacityExhausted =>
                    "The shared 2D appearance table reached its capacity of 1,023 active text objects.",
                TextStudio2DFieldProgramStatus.GpuUnsupported =>
                    "This graphics device supports neither structured buffers nor full-float data textures for the field renderer. Text Studio preserved the source TMP rendering.",
                _ => string.Empty
            };
        }

        static string BuildHeroWarning(TextStudio2DHeroProgramStatus programStatus)
        {
            return programStatus switch
            {
                TextStudio2DHeroProgramStatus.OperationCapacityExceeded =>
                    "The Hero compositor supports at most 64 compiled appearance operations per text object. Styles without groups, masks or blend modes render directly at any operation count.",
                TextStudio2DHeroProgramStatus.RelationshipDepthExceeded =>
                    "The Hero compositor supports at most eight nested group or mask relationships.",
                TextStudio2DHeroProgramStatus.InvalidStyle =>
                    "The assigned Hero appearance contains invalid group or mask relationships.",
                _ => string.Empty
            };
        }
    }
}
