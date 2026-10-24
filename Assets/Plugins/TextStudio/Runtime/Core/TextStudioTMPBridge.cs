using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TextStudio
{
    /// <summary>
    /// Encapsulates all TMP-specific integration: text synchronization (mirror/drive),
    /// layout extraction from TMP's textInfo, vertex/color manipulation for animation
    /// and placement, TMP renderer visibility management, and decoration mesh handling.
    ///
    /// Owned by <see cref="TextStudioText"/>. Receives data via method parameters and
    /// returns results via return values - no back-references to hub internals.
    /// </summary>
    internal sealed partial class TextStudioTMPBridge
    {
        static readonly ProfilerMarker TextWriteProfilerMarker = new ProfilerMarker("Text Studio TMP Text Write");
        static readonly ProfilerMarker RegenerationProfilerMarker = new ProfilerMarker("Text Studio TMP Regeneration");
        static readonly ProfilerMarker LayoutPrepareProfilerMarker = new ProfilerMarker("Text Studio Layout Prepare");
        static readonly ProfilerMarker LayoutSourceDataProfilerMarker = new ProfilerMarker("Text Studio Layout Source Data");

        readonly TextStudioText _hub;
        readonly TextStudioTMPFrameApplier _frameApplier = new TextStudioTMPFrameApplier();
        readonly TextStudioTMPMaterialEffectAdapter _materialEffectAdapter = new TextStudioTMPMaterialEffectAdapter();
        readonly TextStudioTMPAppearanceAdapter _appearanceAdapter = new TextStudioTMPAppearanceAdapter();
        readonly TextStudioTMPSourceVisibility _replacementSourceVisibility = new TextStudioTMPSourceVisibility();
        readonly TextStudioTMPSourceVisibility _passThroughSourceVisibility = new TextStudioTMPSourceVisibility();
        bool _isPassThroughHidden;
        TextStudioVisualFontSource[] _fontSources = Array.Empty<TextStudioVisualFontSource>();
        int _fontSourceCount;
        readonly List<MeshRenderer> _worldShadowRenderers = new List<MeshRenderer>(4);
        readonly List<ShadowCastingMode> _worldShadowSourceModes =
            new List<ShadowCastingMode>(4);
        readonly List<TMP_SubMesh> _worldShadowSubMeshes = new List<TMP_SubMesh>(4);
        TextStudio2DHeroCompositor _heroCompositor;
        readonly TextStudioTMPAppearancePlanAdapter _appearancePlanAdapter = new TextStudioTMPAppearancePlanAdapter();
        TextStudio2DOrderedRenderer _orderedRenderer;
        TextStudioWritingRenderer _writingRenderer;

        TMP_Text _tmpText;
        bool _layoutValid;
        bool _hasFreshMeshForLayout;
        bool _isUsingUnanimatedPassThrough;
        bool _requiresExternalMaterialIntent;

        bool AppearanceRequiresFieldMaterial => _appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.RequiresFieldMaterial : _appearanceAdapter.RequiresFieldMaterial;
        TextStudio2DFieldPaddingTier AppearancePaddingTier => _appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.PaddingTier : _appearanceAdapter.PaddingTier;
        int AppearanceFieldStyleSlot => _appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.PrimaryFieldAdapter?.FieldStyleSlot ?? 0 : _appearanceAdapter.FieldStyleSlot;
        bool AppearanceRequiresFullShaderTier => _appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.UsesHero : _appearanceAdapter.HasHeroProgram;

        // The rows the material adapter draws through the text's own TMP materials, or default on every other route.
        TextStudio2DTextMeshProStyle AppearanceTextMeshProStyle => _appearancePlanAdapter.Plan != null
            ? default : _appearanceAdapter.TextMeshProStyle;

        /// <summary>Whether the text draws through its own TMP materials on the TextMeshPro route.</summary>
        internal bool UsesTextMeshProRoute => _materialEffectAdapter.UsesTextMeshProRoute;

        bool PrepareAppearance(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan, bool forceHero = false,
            in TextStudio2DTextMeshProStyle textMeshProStyle = default)
        {
            if (plan != null && plan.HasInvocations)
            {
                bool changed = _appearancePlanAdapter.Plan == null;
                _appearanceAdapter.Release();
                return _appearancePlanAdapter.Prepare(plan, _tmpText, forceHero) || changed;
            }
            bool hadPlan = _appearancePlanAdapter.Plan != null;
            if (hadPlan) _appearancePlanAdapter.Release();
            return _appearanceAdapter.Update(style, forceHero, _tmpText, TextStudio2DRouteReason.LensMagnifier, in textMeshProStyle) || hadPlan;
        }

        // A TextMeshPro-expressible style draws through the text's own TMP materials when the context allows it and every source
        // material can draw it (TextStudio2DTextMeshProRoute); default keeps the exact path. The style check runs first, since
        // most styles fail it.
        TextStudio2DTextMeshProStyle ResolveTextMeshProStyle(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan,
            TextStudioCompiledPlan effects, bool writing) =>
            TextStudio2DTextMeshProRoute.TrySelect(style, plan, out TextStudio2DTextMeshProStyle selected) &&
            AllowsTextMeshProRoute(effects, writing) && _materialEffectAdapter.SupportsTextMeshProRoute(_tmpText, in selected)
                ? selected
                : default;

        // The TextMeshPro route replaces Text Studio's materials and drops their payload streams, so a requirement for those
        // materials, Writing, a World Batch Group that merges Text Studio primitives, or a lens magnifier, whose exact lens
        // composites through Hero output, keeps the exact path.
        bool AllowsTextMeshProRoute(TextStudioCompiledPlan plan, bool writing) =>
            !_requiresExternalMaterialIntent && !writing && !_hub.IsWorldBatched &&
            !TextStudioTMPMaterialEffectAdapter.RequiresProductionMaterial(plan) && !UsesLensMagnifier(plan);

        static bool UsesLensMagnifier(TextStudioCompiledPlan plan)
        {
            if (plan?.EffectBindings == null)
                return false;
            int count = System.Math.Min(plan.EffectBindingCount, plan.EffectBindings.Length);
            for (int i = 0; i < count; i++)
            {
                if (plan.EffectBindings[i].effect is BuiltIns.LensMagnifierEffect ||
                    string.Equals(plan.EffectBindings[i].effectId, TextStudioBuiltInEffectIds.LensMagnifier,
                        System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Whether a new appearance style moves this text off the TextMeshPro route: its rows no longer qualify or a source
        /// material cannot draw them. Layout must then prepare the payload streams of Text Studio's materials before the next frame.
        /// </summary>
        internal bool LeavesTextMeshProRoute(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan) =>
            _materialEffectAdapter.UsesTextMeshProRoute &&
            (!TextStudio2DTextMeshProRoute.TrySelect(style, plan, out TextStudio2DTextMeshProStyle selected) ||
             !_materialEffectAdapter.SupportsTextMeshProRoute(_tmpText, in selected));

        internal string MaterialCompatibilityWarning
        {
            get
            {
                string materialWarning = _materialEffectAdapter.CompatibilityWarning;
                string appearanceWarning = _appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.CompatibilityWarning : _appearanceAdapter.CompatibilityWarning;
                string compositorWarning = _heroCompositor?.CompatibilityWarning;
                string writingWarning = _writingRenderer?.CompatibilityWarning;
                string geometryWarning = _styleDecorations?.CompatibilityWarning ?? _heroCompositor?.GeometryCompatibilityWarning;
                string warning = materialWarning;
                if (!string.IsNullOrEmpty(appearanceWarning))
                    warning = string.IsNullOrEmpty(warning)
                        ? appearanceWarning
                        : warning + " " + appearanceWarning;
                if (!string.IsNullOrEmpty(compositorWarning))
                    warning = string.IsNullOrEmpty(warning)
                        ? compositorWarning
                        : warning + " " + compositorWarning;
                if (!string.IsNullOrEmpty(writingWarning))
                    warning = string.IsNullOrEmpty(warning)
                        ? writingWarning
                        : warning + " " + writingWarning;
                if (!string.IsNullOrEmpty(geometryWarning))
                    warning = string.IsNullOrEmpty(warning) ? geometryWarning : warning + " " + geometryWarning;
                string colorWarning = _heroCompositor?.ColorSourceCompatibilityWarning;
                if (!string.IsNullOrEmpty(colorWarning)) warning = string.IsNullOrEmpty(warning) ? colorWarning : warning + " " + colorWarning;
                return warning;
            }
        }

        string _lastFontQualityHint, _lastColorQualityHint, _combinedQualityHint;
        internal string AppearanceQualityHint
        {
            get
            {
                string font = _appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.SourceQualityHint : _appearanceAdapter.SourceQualityHint;
                string color = _heroCompositor?.ColorSourceQualityHint ?? string.Empty;
                if (_lastFontQualityHint != font || _lastColorQualityHint != color)
                {
                    _lastFontQualityHint = font;
                    _lastColorQualityHint = color;
                    _combinedQualityHint = font.Length == 0 ? color : color.Length == 0 ? font : font + " " + color;
                }
                return _combinedQualityHint ?? string.Empty;
            }
        }
        internal bool NeedsCameraQualityRefresh() => (_appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.NeedsCameraQualityRefresh(_tmpText) : _appearanceAdapter.NeedsCameraQualityRefresh(_tmpText)) || _heroCompositor?.NeedsColorSourceRefresh == true;

        // NeedsCameraQualityRefresh cannot turn true without a tick: no program has prepared-field demand or a memory retry,
        // and no Hero compositor polls color sources.
        internal bool HasStaticCameraQuality => _heroCompositor == null && (_appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.HasStaticCameraQuality : _appearanceAdapter.HasStaticCameraQuality);

        // Adds the Canvas payload inputs a parked hub compares in place of NeedsCanvasPayloadRefresh. False while that check
        // must keep running in the hub: a moving Canvas text, or one without a canvas.
        internal bool TryCaptureParkedCanvasPayload(ref TextStudioHubScheduler.ParkedAppearance appearance)
        {
            if (!(_tmpText is TextMeshProUGUI ui) || !_frameApplier.HasMaterialPayloadStorage) return true;
            Canvas canvas = ui.canvas;
            if (canvas == null || !_frameApplier.TryGetConfirmedCanvasPayloadInputs(out appearance.PoseMatrix, out appearance.PoseRootMatrix))
                return false;
            appearance.PoseTransform = ui.rectTransform;
            appearance.PoseCanvas = canvas;
            return true;
        }

        internal TextStudio2DHeroCompositorStatistics HeroCompositorStatistics =>
            _heroCompositor != null ? _heroCompositor.Statistics : default;
        internal bool NeedsCanvasPayloadRefresh() => _frameApplier.NeedsCanvasPayloadRefresh(_tmpText);
        internal long AppearanceSelectionPayloadBytes => _appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.SelectionPayloadBytes : _appearanceAdapter.BoundStyle.ManagedPayloadBytes;
        internal long AppearanceMeshPayloadBytes => _frameApplier.ManagedMeshPayloadBytes + (_orderedRenderer?.ManagedPayloadBytes ?? 0) +
            (_styleDecorations?.ManagedPayloadBytes ?? 0) + (_heroCompositor?.ManagedPayloadBytes ?? 0);

        internal int OrderedGeometryUploadCount => _orderedRenderer?.GeometryUploadCount ?? 0;
        internal int OrderedDrawCount => _orderedRenderer?.DrawCount ?? 0;
        internal TextStudio2DOrderedRenderer OrderedRenderer => _orderedRenderer;

        internal TextStudio2DRenderStatistics CaptureRenderStatistics()
        {
            TextStudio2DRoute route;
            TextStudio2DRouteReason reason;
            if (_appearancePlanAdapter.Plan != null)
            {
                route = _appearancePlanAdapter.UsesHero ? TextStudio2DRoute.Composited : TextStudio2DRoute.OrderedPrimitives;
                reason = _appearancePlanAdapter.UsesHero ? _appearancePlanAdapter.CompositionReason : TextStudio2DRouteReason.ScopedAppearance;
            }
            else
            {
                route = _appearanceAdapter.Route.Route;
                reason = _appearanceAdapter.Route.Reason;
            }
            bool batched = _hub.IsWorldBatched;
            if (batched && route == TextStudio2DRoute.NativeField)
            {
                route = TextStudio2DRoute.OrderedPrimitives;
                reason = TextStudio2DRouteReason.WorldBatchGroup;
            }
            TextStudio2DHeroCompositorStatistics hero = _heroCompositor != null ? _heroCompositor.Statistics : default;
            bool composed = _heroCompositor != null && _heroCompositor.HasOutput;
            int direct = batched ? 0
                : composed ? 1
                : _orderedRenderer != null && _orderedRenderer.DrawCount > 0 ? _orderedRenderer.DrawCount
                : _tmpText != null && _tmpText.textInfo != null && _tmpText.textInfo.characterCount > 0 ? System.Math.Max(1, _tmpText.textInfo.materialCount)
                : 0;
            // Alpha-clipped world shadows draw every presented run once more into the shadow map.
            bool shadows = !(_tmpText is TMPro.TextMeshProUGUI) &&
                           _hub.CompiledAppearanceStyle?.WorldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped;
            return new TextStudio2DRenderStatistics(route, reason, direct, batched,
                composed ? hero.LayerDrawCount : 0,
                composed ? System.Math.Max(0, hero.CompositePassCount - hero.EffectPassCount) : 0,
                composed ? hero.EffectPassCount : 0,
                composed ? hero.PublishPassCount : 0,
                shadows ? direct : 0,
                composed ? hero.TargetWidth : 0,
                composed ? hero.TargetHeight : 0);
        }
        internal TextStudio2DHeroCompositor HeroCompositor => _heroCompositor;

        internal void SetWorldOutputSuppressed(bool suppressed)
        {
            _orderedRenderer?.SetWorldOutputSuppressed(suppressed);
            _heroCompositor?.SetWorldOutputSuppressed(suppressed);
        }
        internal int AppearanceResolvedMatchCount => _appearancePlanAdapter.Plan != null
            ? _appearancePlanAdapter.ResolvedMatchCount : _appearanceAdapter.BoundStyle.ResolvedMatchCount;
        internal TextStudioAppearanceSourceStatistics AppearanceSourceStatistics =>
            _appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.SourceStatistics :
                _appearanceAdapter.SourceDemand != null ? _appearanceAdapter.SourceDemand.Statistics : default;

        internal Vector4 GetAppearancePreparedQuality(int renderGlyphIndex) =>
            _appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.GetPreparedQuality(renderGlyphIndex) : _appearanceAdapter.GetPreparedQuality(renderGlyphIndex);

        internal TextStudioRendererEffectExecution GetEffectExecution(
            in TextStudioRendererEffectExecutionRequest request)
        {
            if (_tmpText == null || !_tmpText.enabled || _hub.WritingStyle != null)
                return TextStudioRendererEffectExecution.Cpu;
            return TextStudioLensMagnifierDescriptor.CanExecuteExactly(
                    in request,
                    _hub)
                ? TextStudioRendererEffectExecution.RendererExact
                : TextStudioRendererEffectExecution.Cpu;
        }

        // TMP-specific line segmentation that can differ from the compiled plan after wrapping.
        LineRange[] _cachedPlacementLines;
        int _cachedPlacementLineCount;
        int[] _unitToCharacterInfo;
        int[] _previousVisibleColorUnits;
        int[] _nextVisibleColorUnits;

        // Auto-hide tracking: records when we suppressed TMP's own draw pass
        // because an external ITextStudioRenderer is handling visuals.
        bool _meshRendererAutoHidden;
        bool _canvasRendererAutoCulled;
        bool _visibilityStateInitialized;
        bool _lastHasExternalRenderers;
        bool _lastPreserveWorldMeshRenderer;
        bool _hasInternalReplacement;

        // Text-source tracking for mirror/drive mode.
        string _lastSeenText = string.Empty;
        TMP_FontAsset _lastSeenFontAsset;
        string _pendingGeneratedTextWrite = string.Empty;
        bool _hasPendingGeneratedTextWrite;
        // Set while SetTMPTextFromTextStudio writes TMP's text property (HandleTMPLayoutCallback).
        bool _isWritingGeneratedText;

        // TMP event subscription state.
        bool _subscribedToChanged;
        bool _suppressRegen;
        bool _hasLayoutFingerprint;
        int _layoutFingerprint;
        readonly LiteralMarkupPreprocessor _literalMarkupPreprocessor = new LiteralMarkupPreprocessor();
        readonly RawMarkupSafetyPreprocessor _rawMarkupSafetyPreprocessor = new RawMarkupSafetyPreprocessor();
        ITextPreprocessor _previousTextPreprocessor;
        bool _literalMarkupRendering;

        // Curved decorations (underline/strikethrough tiles that follow placement).
        TextStudioDecorations _decorations;
        TextStudio2DStyleDecorations _styleDecorations;
        bool _skippedVisualFrameWhileSuppressed;
        // The input objects of the last frame RenderFrame found plain, which RenderPlainFrame applies for the steady lane.
        bool _hasPlainFrame;
        int _plainFrameCountForDebug;
        internal int PlainFrameCountForDebug => _plainFrameCountForDebug;
        CompiledTextStudio2DStyle _plainAppearanceStyle;
        CompiledTextStudioAppearancePlan _plainAppearancePlan;
        CompiledTextStudioWritingStyle _plainWritingStyle;
        TextStudioCompiledPlan _plainCompiledPlan;

        /// <summary>Reports native mesh or material changes. The first argument indicates that the consumed layout or material state needs refresh; the second is true only when TMP has generated a fresh native mesh.</summary>
        public event Action<bool, bool> OnRegenerated;

        public TMP_Text TMPText => _tmpText;
        public string CurrentSourceText => GetCurrentSourceText();
        public bool IsLayoutValid => _layoutValid;
        internal bool IsUsingUnanimatedPassThrough => _isUsingUnanimatedPassThrough;
        public bool HasUnobservedTextChange =>
            _tmpText != null &&
            !string.Equals(_tmpText.text ?? string.Empty, _lastSeenText, StringComparison.Ordinal);

        // A sleeping hub wakes when TMP's text or font no longer matches what the bridge last observed.
        internal bool AreObservedTextAndFontCurrent()
        {
            TMP_Text tmpText = _tmpText;
            return tmpText != null &&
                   ReferenceEquals(tmpText.font, _lastSeenFontAsset) &&
                   string.Equals(tmpText.text ?? string.Empty, _lastSeenText, StringComparison.Ordinal);
        }

        // The hub's steady lane skips TryConsumeFontAssetChange and the text pull while this holds: TMP keeps the observed text
        // and font, and pass-through is off, so both find no change. A generated write still waiting for confirmation is the
        // observed text itself, so the pull would only confirm it, which ConfirmSteadyText replays (an import from an input
        // field is excluded by the hub).
        internal bool IsSteadyTextCurrent()
        {
            TMP_Text tmpText = _tmpText;
            return tmpText != null &&
                   (!_hasPendingGeneratedTextWrite ||
                    string.Equals(_pendingGeneratedTextWrite, _lastSeenText, StringComparison.Ordinal)) &&
                   !_isUsingUnanimatedPassThrough &&
                   ReferenceEquals(tmpText.font, _lastSeenFontAsset) &&
                   string.Equals(tmpText.text, _lastSeenText, StringComparison.Ordinal);
        }

        // The state change of the text pull and push that a steady tick skips while IsSteadyTextCurrent holds: TMP still holds
        // the observed text, which confirms a pending generated write of it.
        internal void ConfirmSteadyText()
        {
            ClearPendingGeneratedTextWriteIfMatched(_lastSeenText);
        }

        public bool TryConsumeFontAssetChange()
        {
            TMP_FontAsset fontAsset = _tmpText != null ? _tmpText.font : null;
            if (ReferenceEquals(fontAsset, _lastSeenFontAsset))
                return false;

            _lastSeenFontAsset = fontAsset;
            return true;
        }

        public bool IsVisualOutputEnabled
        {
            get
            {
                if (_tmpText == null || !_tmpText.enabled || !_tmpText.gameObject.activeInHierarchy)
                    return false;

                if (_tmpText is TextMeshProUGUI uiText)
                {
                    Canvas canvas = uiText.canvas;
                    return canvas != null &&
                           canvas.enabled &&
                           (!uiText.canvasRenderer.cull || _hasInternalReplacement);
                }

                return true;
            }
        }

        public TextStudioTMPBridge(TextStudioText hub)
        {
            _hub = hub;
        }

        sealed class LiteralMarkupPreprocessor : ITextPreprocessor
        {
            string _lastSource;
            string _lastResult;

            public string PreprocessText(string text)
            {
                text ??= string.Empty;
                if (string.Equals(text, _lastSource, StringComparison.Ordinal))
                    return _lastResult;

                _lastSource = text;
                _lastResult = TextStudioPipeline.WrapLiteralMarkupForTMP(text);
                return _lastResult;
            }
        }

        sealed class RawMarkupSafetyPreprocessor : ITextPreprocessor
        {
            internal ITextPreprocessor Previous;
            string _lastSource;
            string _lastResult;

            public string PreprocessText(string text)
            {
                text = Previous != null ? Previous.PreprocessText(text) : text;
                text ??= string.Empty;
                if (string.Equals(text, _lastSource, StringComparison.Ordinal))
                    return _lastResult;

                System.Text.StringBuilder safe = null;
                int copied = 0;
                for (int start = 0; start < text.Length; start++)
                {
                    if (text[start] != '<') continue;
                    int end = start + 1;
                    int separators = 0;
                    bool quoted = false;
                    for (; end < text.Length && text[end] != '<'; end++)
                    {
                        char c = text[end];
                        if (c == '"') quoted = !quoted;
                        if (!quoted && c == '>') { end++; break; }
                        if (!quoted && c == ' ') separators++;
                    }
                    // TMP has eight attribute slots and advances to the next slot
                    // after a value. Preserve normal rich text; literalize oversized tags.
                    if (separators >= 7)
                    {
                        safe ??= new System.Text.StringBuilder(text.Length + 32);
                        safe.Append(text, copied, start - copied);
                        safe.Append(TextStudioPipeline.WrapLiteralMarkupForTMP(text.Substring(start, end - start)));
                        copied = end;
                    }
                    start = end - 1;
                }
                if (safe != null) safe.Append(text, copied, text.Length - copied);
                _lastSource = text;
                _lastResult = safe != null ? safe.ToString() : text;
                return _lastResult;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TMP component management
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Guarantee a <see cref="TMP_Text"/> on the host GameObject. Idempotent.
        /// Picks UGUI or world-space flavour based on Canvas ancestry.
        /// </summary>
        public TMP_Text EnsurePresent()
        {
            if (_tmpText != null) return _tmpText;
            _tmpText = ResolveOrAddTMP();
            SyncTextChangedRegistration();
            return _tmpText;
        }

        TMP_Text ResolveOrAddTMP()
        {
            TMP_Text text = _hub.GetComponent<TMP_Text>();
            if (text != null) return text;

            // Unity permits only one Graphic on a GameObject. Optional text
            // backends such as UniText already occupy that slot, including
            // during brief disable and re-enable lifecycle transitions.
            if (_hub.GetComponent<Graphic>() != null)
                return null;

            if (_hub.GetComponentInParent<Canvas>(true) != null)
                return _hub.gameObject.AddComponent<TextMeshProUGUI>();
            return _hub.gameObject.AddComponent<TextMeshPro>();
        }

        /// <summary>Resolve TMP without adding one. Used by OnValidate before OnEnable.</summary>
        public void FindTMP()
        {
            if (_tmpText == null)
                _tmpText = _hub.GetComponent<TMP_Text>();
            SyncTextChangedRegistration();
            RestoreOrphanedProductionMaterialsIfUnowned();
        }

        /// <summary>Full initialization: ensure TMP exists and prime text tracking.</summary>
        public void Initialize()
        {
            _tmpText = EnsurePresent();
            RestoreOrphanedProductionMaterialsIfUnowned();
            _lastSeenText = _tmpText != null ? _tmpText.text : string.Empty;
            _lastSeenFontAsset = _tmpText != null ? _tmpText.font : null;
            _isUsingUnanimatedPassThrough = false;
            ReleasePassThroughVisibility();
        }

        bool RestoreOrphanedProductionMaterialsIfUnowned()
        {
            // A prepared adapter owns its transient production materials. OnValidate
            // runs for every inspector edit, so restoring those live materials here
            // would swap TMP shaders and rebuild the material pipeline per keystroke.
            // A newly constructed bridge has no prepared adapter state and can still
            // recover genuinely orphaned scene references after reload or import.
            return !_materialEffectAdapter.HasPreparedState &&
                   TextStudioTMPMaterialEffectAdapter.RestoreOrphanedProductionMaterials(
                       _tmpText);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Text synchronization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mirror-mode pull: check if TMP's text was edited externally.
        /// Returns true if text changed, with the new text in <paramref name="newMarkup"/>.
        /// </summary>
        public bool TryPullText(
            bool isDriving,
            TextStudioBackendWriteHandling handling,
            string currentMarkup,
            out string newMarkup,
            out bool ignoredExternalWrite)
        {
            newMarkup = currentMarkup ?? string.Empty;
            ignoredExternalWrite = false;
            if (_tmpText == null) return false;

            string tmpText = _tmpText.text ?? string.Empty;
            if (handling == TextStudioBackendWriteHandling.ImportAsSource &&
                TryGetInputSource(out string inputText))
            {
                // Retained transitions may still display the previous glyph stream. The field owns the editable source.
                _lastSeenText = tmpText;
                ClearPendingGeneratedTextWrite();
                newMarkup = inputText;
                return !string.Equals(inputText, currentMarkup ?? string.Empty, StringComparison.Ordinal);
            }
            if (tmpText == _lastSeenText)
            {
                ClearPendingGeneratedTextWriteIfMatched(tmpText);
                return false;
            }

            if (_hasPendingGeneratedTextWrite &&
                string.Equals(tmpText, _pendingGeneratedTextWrite, StringComparison.Ordinal))
            {
                _lastSeenText = tmpText;
                ClearPendingGeneratedTextWrite();
                return false;
            }

            ClearPendingGeneratedTextWrite();

            if (handling == TextStudioBackendWriteHandling.Ignore ||
                handling == TextStudioBackendWriteHandling.Warn)
            {
                ignoredExternalWrite = true;
                _lastSeenText = tmpText;
                return false;
            }

            newMarkup = GetCurrentSourceText();
            _lastSeenText = tmpText;
            return true;
        }

        bool TryGetInputSource(out string inputText)
        {
            TMP_InputField input = _tmpText != null ? _tmpText.GetComponentInParent<TMP_InputField>() : null;
            if (input != null && input.inputType != TMP_InputField.InputType.Password &&
                ReferenceEquals(input.textComponent, _tmpText))
            {
                inputText = input.text ?? string.Empty;
                return true;
            }
            inputText = string.Empty;
            return false;
        }

        string GetCurrentSourceText()
        {
            return TryGetInputSource(out string inputText)
                ? inputText
                : _tmpText != null ? _tmpText.text ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Drive-mode push: sync the authored text into TMP's text field.
        /// </summary>
        public void PushText(bool isDriving, TextStudioCompiledPlan plan, string markup)
        {
            if (_tmpText == null) return;

            string target;
            if (isDriving && plan != null)
                target = plan.DisplayText ?? plan.ResolvedText ?? markup ?? string.Empty;
            else
                target = markup ?? string.Empty;

            bool changed;
            using (TextWriteProfilerMarker.Auto())
                changed = SetTMPTextFromTextStudio(target);
            if (changed && _tmpText.font != null)
            {
                using (RegenerationProfilerMarker.Auto())
                    _hasFreshMeshForLayout = SuppressedForceMeshUpdate();
            }
        }

        /// <summary>
        /// Immediate text sync for SetText / OnValidate. Pushes markup to TMP right now
        /// so inspector edits are visible without waiting for the next tick.
        /// </summary>
        public void SyncTextImmediate(string markup)
        {
            if (_tmpText == null) return;
            string target = markup ?? string.Empty;
            if (SetTMPTextFromTextStudio(target) && _tmpText.font != null)
                _hasFreshMeshForLayout = SuppressedForceMeshUpdate();
        }

        /// <summary>
        /// Raw authoring mode without an attached renderer hands rendering back to TMP.
        /// Force a TMP mesh rebuild even when the text string is unchanged so preview
        /// alpha and vertex edits do not leak into normal text display.
        /// </summary>
        public void SyncRawTextImmediate(
            string markup,
            bool preserveWorldMeshRenderer = false)
        {
            if (_tmpText == null) return;
            SetLiteralMarkupRendering(false);
            if (!ReferenceEquals(_tmpText.textPreprocessor, _rawMarkupSafetyPreprocessor))
            {
                _rawMarkupSafetyPreprocessor.Previous = _tmpText.textPreprocessor;
                _tmpText.textPreprocessor = _rawMarkupSafetyPreprocessor;
            }
            string target = markup ?? string.Empty;
            SetTMPTextFromTextStudio(target);
            if (_tmpText.font != null && !preserveWorldMeshRenderer)
                SuppressedForceMeshUpdate();
            _layoutValid = false;
            _hasFreshMeshForLayout = false;
            _isUsingUnanimatedPassThrough = false;
            ReleasePassThroughVisibility();
            _hasLayoutFingerprint = false;
            _skippedVisualFrameWhileSuppressed = false;
            DisposeDecorations();
            DisposeStyleDecorations();
            RestoreVisibility();
        }

        public void SetLiteralMarkupRendering(bool enabled)
        {
            if (_tmpText != null && ReferenceEquals(_tmpText.textPreprocessor, _rawMarkupSafetyPreprocessor))
            {
                _tmpText.textPreprocessor = _rawMarkupSafetyPreprocessor.Previous;
                _rawMarkupSafetyPreprocessor.Previous = null;
            }
            if (_tmpText == null || _literalMarkupRendering == enabled)
                return;

            if (enabled)
            {
                _previousTextPreprocessor = _tmpText.textPreprocessor;
                _tmpText.textPreprocessor = _literalMarkupPreprocessor;
                _literalMarkupRendering = true;
            }
            else
            {
                if (ReferenceEquals(_tmpText.textPreprocessor, _literalMarkupPreprocessor))
                    _tmpText.textPreprocessor = _previousTextPreprocessor;
                _previousTextPreprocessor = null;
                _literalMarkupRendering = false;
            }

            _tmpText.SetVerticesDirty();
            _tmpText.SetLayoutDirty();
            _layoutValid = false;
            _hasFreshMeshForLayout = false;
            _hasLayoutFingerprint = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Layout extraction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extract glyph layout from TMP's textInfo and populate the layout engine.
        /// Returns true on success, false if TMP can't produce a layout (no font, etc.).
        /// </summary>
        public bool RunLayout(
            TextStudioCompiledPlan plan,
            TextStudioLayout layout,
            bool cacheVisualSourceData = true,
            bool requiresExternalMaterial = false,
            TextStudioSourceContextRequirements externalSourceRequirements =
                TextStudioSourceContextRequirements.None,
            CompiledTextStudio2DStyle appearanceStyle = null,
            CompiledTextStudioWritingStyle writingStyle = null,
            CompiledTextStudioAppearancePlan appearancePlan = null,
            TextStudioKeptGlyphPrefix keptPrefix = default)
        {
            _hasPlainFrame = false;
            _isUsingUnanimatedPassThrough = false;
            ReleasePassThroughVisibility();
            _layoutValid = false;
            if (_tmpText == null || plan == null || _tmpText.font == null)
            {
                InvalidateAppendLayout();
                return false;
            }
            bool requiresWritingMaterial;
            bool meshOk;
            using (LayoutPrepareProfilerMarker.Auto())
            {
                _requiresExternalMaterialIntent = requiresExternalMaterial;
                requiresWritingMaterial =
                    TextStudioWritingRenderer.RequiresWritingMaterial(
                        plan,
                        writingStyle);
                // Layout reads the current values of the source materials, which runtime edits change without an event.
                _materialEffectAdapter.InvalidateTextMeshProSupport();
                PrepareAppearance(appearanceStyle, appearancePlan, false,
                    ResolveTextMeshProStyle(appearanceStyle, appearancePlan, plan, requiresWritingMaterial));
                UpdateWorldShadowState(_appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.WorldShadowStyle : appearanceStyle);

                SetLiteralMarkupRendering(plan.IsLiteralMarkup);

                string displayText = plan.IsLiteralMarkup
                    ? plan.CanonicalText ?? string.Empty
                    : plan.DisplayText ?? plan.ResolvedText ?? string.Empty;
                if (_tmpText.text != displayText)
                {
                    SetTMPTextFromTextStudio(displayText);
                }

                _suppressRegen = true;
                try
                {
                    bool hadPreparedMaterialState = _materialEffectAdapter.HasPreparedState;
                    bool restoredOrphanedMaterial =
                        RestoreOrphanedProductionMaterialsIfUnowned();
                    _materialEffectAdapter.Prepare(
                        _tmpText,
                        plan,
                        requiresExternalMaterial,
                        requiresWritingMaterial,
                        AppearanceRequiresFieldMaterial,
                        AppearancePaddingTier,
                        AppearanceRequiresFullShaderTier ? null : appearanceStyle,
                        AppearanceRequiresFullShaderTier ? null : _appearancePlanAdapter.Plan,
                        AppearanceTextMeshProStyle);
                    // A source the route check did not see, such as a fallback font this text now uses, keeps the exact path until
                    // the next layout.
                    if (_materialEffectAdapter.ConsumeTextMeshProRouteFailure())
                    {
                        PrepareAppearance(appearanceStyle, appearancePlan);
                        _materialEffectAdapter.Prepare(
                            _tmpText,
                            plan,
                            requiresExternalMaterial,
                            requiresWritingMaterial,
                            AppearanceRequiresFieldMaterial,
                            AppearancePaddingTier,
                            AppearanceRequiresFullShaderTier ? null : appearanceStyle,
                            AppearanceRequiresFullShaderTier ? null : _appearancePlanAdapter.Plan);
                    }
                    // TMP's own material reads TMP's normals and no payload; restore them while the cached sources still match.
                    if (_materialEffectAdapter.UsesTextMeshProRoute)
                        _frameApplier.ReleaseMaterialStreams(_tmpText);
                    // PushText already regenerated an unmodified TMP mesh after changing the
                    // string. It remains a valid visual-source snapshot unless material
                    // preparation was active before or after this call.
                    meshOk = _hasFreshMeshForLayout &&
                             !restoredOrphanedMaterial &&
                             !hadPreparedMaterialState &&
                             !_materialEffectAdapter.HasPreparedState;
                    if (!meshOk)
                        meshOk = TryForceMeshUpdate(_tmpText);
                }
                finally { _suppressRegen = false; }
                _hasFreshMeshForLayout = false;
            }
            if (!meshOk)
            {
                InvalidateAppendLayout();
                return false;
            }

            TMP_TextInfo textInfo = _tmpText.textInfo;
            int count = plan.VisibleGlyphCount;
            uint materialsVersion = ComputeMaterialsVersion(_tmpText);
            // Appended text whose settings keep earlier TMP lines in place records the state the next append resumes from, so text
            // that is replaced rather than appended pays nothing for it; the first of consecutive appends lays out in full.
            bool appendEligible = keptPrefix.IsDefined && AllowsAppendLayout(_tmpText);
            int appendSettings = appendEligible
                ? ComputeAppendLayoutSettings(plan, cacheVisualSourceData, requiresExternalMaterial, externalSourceRequirements,
                    appearanceStyle, writingStyle, appearancePlan)
                : 0;
            // An append that kept the TMP lines before resumeLine resumes every pass below at that line's first unit and
            // character, with the state the last pass reached there; a full layout starts at zero.
            int resumeLine = -1;
            bool resume = appendEligible &&
                          TryResumeAppendLayout(plan, layout, keptPrefix, textInfo, count, appendSettings, materialsVersion,
                              out resumeLine);
            if (!appendEligible)
                AppendLayoutRejectionForDebug = keptPrefix.IsDefined ? "text settings" : "no kept prefix";
            LineResumePoint resumePoint = resume ? _lineResumePoints[resumeLine] : default;
            int startUnit = resume ? resumePoint.Unit : 0;
            int startCharacter = resume ? resumePoint.Character : 0;
            BuildUnitToCharacterInfoMap(plan, textInfo, count, startUnit, startCharacter);

            // Populate the layout-owned arrays directly. Keeping a second retained copy in the
            // bridge doubled this storage for every TMP-backed Text Studio instance.
            layout.EnsureCapacity(count);
            Vector3[] positions = layout.BasePositions;
            float[] sourceCenterBaselineOffsets = layout.SourceCenterBaselineOffsets;
            TextStudioGlyphColorQuad[] sourceColors = layout.SourceColors;
            bool[] sourceColorValidity = layout.SourceColorValidity;
            TextStudioGlyphColorBounds[] sourceColorBounds = layout.SourceColorBounds;
            TextStudioGlyphVisualBounds[] sourceVisualBounds = layout.SourceVisualBounds;
            float[] effectUnitScales = layout.EffectUnitScales;
            TextStudioVisualGlyph[] visualGlyphs = layout.VisualGlyphs;
            if (!resume)
            {
                // A resumed pass keeps the sources of the kept lines; it only resumes after a pass that registered none.
                if (_fontSourceCount > 0) Array.Clear(_fontSources, 0, _fontSourceCount);
                _fontSourceCount = 0;
                ResetColorSources();
                _appendLayoutFonts.Clear();
            }
            _layoutSources.Reset();
            _versionSources.Reset();

            float autoFitGlyphMargin = resume ? resumePoint.AutoFitGlyphMargin : 0f;
            FontStyles globalStyle = _tmpText.fontStyle;
            TMP_FontAsset fontAsset = _tmpText.font;
            NoteAppendLayoutFont(fontAsset);
            float defaultEffectUnitScale = ResolveEffectUnitScale(_tmpText, default);
            EnsureLineResumePointCapacity(textInfo.lineCount);
            // Line numbers grow with units and characters in TMP's order; a pass that meets them out of order records no lines.
            bool linesInOrder = true;
            int unitLine = resume ? resumeLine - 1 : -1;
            for (int i = startUnit; i < count; i++)
            {
                int characterInfoIndex = GetCharacterInfoIndex(i);
                if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                {
                    // A mapped unit overwrites every record below, so only an unmapped unit writes defaults.
                    sourceColors[i] = TextStudioGlyphColorQuad.White;
                    sourceColorValidity[i] = false;
                    sourceColorBounds[i] = default;
                    sourceVisualBounds[i] = default;
                    visualGlyphs[i] = default;
                    effectUnitScales[i] = defaultEffectUnitScale;
                    positions[i] = i > 0 ? positions[i - 1] : Vector3.zero;
                    sourceCenterBaselineOffsets[i] = 0f;
                    continue;
                }

                // TMP_CharacterInfo spans several hundred bytes, so per-glyph loops read it in place instead of copying it.
                ref readonly TMP_CharacterInfo charInfo = ref textInfo.characterInfo[characterInfoIndex];
                int line = charInfo.lineNumber;
                if (line != unitLine)
                {
                    if (line < unitLine || line >= _lineResumePoints.Length)
                    {
                        linesInOrder = false;
                    }
                    else
                    {
                        // Lines without a unit of their own cannot be resumed at.
                        for (int skipped = unitLine + 1; skipped < line; skipped++)
                            _lineResumePoints[skipped].Unit = -1;
                        _lineResumePoints[line].Unit = i;
                        _lineResumePoints[line].AutoFitGlyphMargin = autoFitGlyphMargin;
                        unitLine = line;
                    }
                }
                // Selecting the font first lets the unit scale read its cached point size; BuildVisualGlyph then finds it selected and registered.
                if (_layoutSources.SelectFont(charInfo.fontAsset))
                {
                    RegisterFontSource(charInfo.fontAsset, _layoutSources.FontIdentity);
                    NoteAppendLayoutFont(charInfo.fontAsset);
                }
                sourceColorValidity[i] = true;
                effectUnitScales[i] = ResolveLayoutEffectUnitScale(in charInfo);
                Color32 fallbackColor = (charInfo.style & FontStyles.Underline) != 0
                    ? charInfo.underlineColor
                    : (charInfo.style & FontStyles.Strikethrough) != 0
                        ? charInfo.strikethroughColor
                        : charInfo.color;
                sourceColors[i] = TextStudioGlyphColorQuad.Uniform(fallbackColor);
                sourceColorBounds[i] = ResolveFallbackColorBounds(in charInfo);
                if (charInfo.isVisible)
                {
                    if (IsCharacterVertexRangeValid(textInfo, in charInfo))
                    {
                        int vertIdx = charInfo.vertexIndex;
                        int matIdx = charInfo.materialReferenceIndex;
                        Vector3[] verts = textInfo.meshInfo[matIdx].vertices;
                        positions[i] = (verts[vertIdx] + verts[vertIdx + 2]) * 0.5f;
                        Color32[] colors = textInfo.meshInfo[matIdx].colors32;
                        if (colors != null && colors.Length >= vertIdx + 4)
                        {
                            sourceColors[i] = new TextStudioGlyphColorQuad(
                                colors[vertIdx],
                                colors[vertIdx + 1],
                                colors[vertIdx + 2],
                                colors[vertIdx + 3]);
                        }
                        sourceColorBounds[i] = ResolveSourceColorBounds(
                            verts,
                            vertIdx,
                            in charInfo);
                    }
                    else
                    {
                        float cx = (charInfo.origin + charInfo.xAdvance) * 0.5f;
                        float cy = charInfo.baseLine;
                        positions[i] = new Vector3(cx, cy, 0f);
                    }
                }
                else
                {
                    float cx = (charInfo.origin + charInfo.xAdvance) * 0.5f;
                    float cy = charInfo.baseLine;
                    positions[i] = new Vector3(cx, cy, 0f);
                }
                sourceCenterBaselineOffsets[i] = positions[i].y - charInfo.baseLine;
                TextStudioGlyphColorBounds visualBounds = sourceColorBounds[i].IsValid
                    ? sourceColorBounds[i]
                    : ResolveFallbackColorBounds(in charInfo);
                sourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                    new Vector2(
                        charInfo.origin + visualBounds.Minimum.x,
                        charInfo.baseLine + visualBounds.Minimum.y),
                    new Vector2(
                        charInfo.origin + visualBounds.Maximum.x,
                        charInfo.baseLine + visualBounds.Maximum.y),
                    charInfo.isVisible);
                visualGlyphs[i] = BuildVisualGlyph(
                    plan,
                    textInfo,
                    in charInfo,
                    characterInfoIndex,
                    i,
                    in sourceVisualBounds[i],
                    effectUnitScales[i]);

                autoFitGlyphMargin = Mathf.Max(
                    autoFitGlyphMargin,
                    ComputeAutoFitGlyphMargin(textInfo, plan, fontAsset, globalStyle, in charInfo, i, positions[i]));
            }

            for (int line = unitLine + 1; line < textInfo.lineCount && line < _lineResumePoints.Length; line++)
                _lineResumePoints[line].Unit = -1;

            // Whitespace has no mesh, but its decorations need the same visual pivot and
            // vertical span as the surrounding run. Font ascent/descent is not ink bounds.
            int[] widenedUnits = EnsureWidenedLayoutUnitCapacity(count + textInfo.characterCount);
            int widenedUnitCount = 0;
            // A resumed pass starts at a line's first unit, which no unit of an earlier line neighbours.
            int previousVisible = -1, nextVisible = startUnit;
            for (int i = startUnit; i < count; i++)
            {
                // A visual glyph spans 160 bytes, so only the whitespace glyphs this pass rewrites are copied.
                ref readonly TextStudioVisualGlyph current = ref visualGlyphs[i];
                if (current.IsVisible) { previousVisible = i; continue; }
                if (Mathf.Abs(current.Advance) <= 0.0001f) continue;
                TextStudioVisualGlyph glyph = current;
                if (nextVisible <= i)
                {
                    nextVisible = i + 1;
                    while (nextVisible < count && !visualGlyphs[nextVisible].IsVisible) nextVisible++;
                }
                int previous = previousVisible, next = nextVisible;
                bool hasPrevious = previous >= 0 && visualGlyphs[previous].LineIndex == glyph.LineIndex && visualGlyphs[previous].IsVisible;
                bool hasNext = next < count && visualGlyphs[next].LineIndex == glyph.LineIndex && visualGlyphs[next].IsVisible;
                Vector2 minimum = glyph.BoundsMin, maximum = glyph.BoundsMax;
                if (hasPrevious || hasNext)
                {
                    ref readonly TextStudioVisualGlyph left = ref visualGlyphs[hasPrevious ? previous : next];
                    ref readonly TextStudioVisualGlyph right = ref visualGlyphs[hasNext ? next : previous];
                    float weight = hasPrevious && hasNext ? Mathf.InverseLerp(positions[previous].x, positions[next].x, positions[i].x) : 0f;
                    minimum.y = glyph.Baseline + Mathf.Lerp(left.BoundsMin.y - left.Baseline, right.BoundsMin.y - right.Baseline, weight);
                    maximum.y = glyph.Baseline + Mathf.Lerp(left.BoundsMax.y - left.Baseline, right.BoundsMax.y - right.Baseline, weight);
                }
                positions[i].y = (minimum.y + maximum.y) * 0.5f;
                sourceCenterBaselineOffsets[i] = positions[i].y - glyph.Baseline;
                TextStudioGlyphVisualBounds whitespaceBounds = new TextStudioGlyphVisualBounds(minimum, maximum, false);
                sourceVisualBounds[i] = whitespaceBounds;
                visualGlyphs[i] = glyph.WithBounds(minimum, maximum, false);
                // Source bounds store a normalized copy, which differs from the raw bounds only when they are degenerate.
                if (!HasSameBits(whitespaceBounds.Minimum, minimum) || !HasSameBits(whitespaceBounds.Maximum, maximum))
                    widenedUnits[widenedUnitCount++] = i;
            }

            // This pass reads every TMP record once, so it also accumulates the layout fingerprint, which HandleTMPTextChanged recomputes with the same steps, and the geometry and atlas parts of the visual version.
            int characterCount = textInfo.characterCount;
            TextStudioVisualGlyph[] renderedGlyphs = layout.EnsureRenderGlyphCapacity(characterCount);
            int renderedCount = resume ? resumePoint.RenderedGlyphCount : 0;
            int layoutFingerprintChain = resume ? resumePoint.LayoutFingerprint : LayoutFingerprintSeed;
            VisualVersionHash geometryVersion = resume ? resumePoint.Geometry : VisualVersionHash.Create();
            VisualVersionHash atlasVersion;
            if (resume)
            {
                atlasVersion = resumePoint.Atlas;
            }
            else
            {
                atlasVersion = VisualVersionHash.Create();
                AddFontAtlasVersion(ref atlasVersion, fontAsset, -1);
            }
            int characterLine = resume ? resumeLine - 1 : -1;
            for (int index = startCharacter; index < characterCount; index++)
            {
                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[index];
                int line = character.lineNumber;
                if (line != characterLine)
                {
                    if (line < characterLine || line >= _lineResumePoints.Length)
                    {
                        linesInOrder = false;
                    }
                    else
                    {
                        for (int skipped = characterLine + 1; skipped < line; skipped++)
                            _lineResumePoints[skipped].Character = -1;
                        ref LineResumePoint point = ref _lineResumePoints[line];
                        point.Character = index;
                        point.LayoutFingerprint = layoutFingerprintChain;
                        point.Geometry = geometryVersion;
                        point.Atlas = atlasVersion;
                        point.RenderedGlyphCount = renderedCount;
                        point.Origin = character.origin;
                        point.Baseline = character.baseLine;
                        characterLine = line;
                    }
                }
                layoutFingerprintChain = AddLayoutFingerprintCharacter(layoutFingerprintChain, textInfo, in character);
                AddGeometryVersion(ref geometryVersion, textInfo, in character);
                int unit = plan.GetUnitIndexForDisplayChar(character.index);
                if (unit < 0 || unit >= count)
                {
                    AddCharacterAtlasVersion(ref atlasVersion, in character, ResolveAtlasUvRect(textInfo, in character));
                    continue;
                }

                if (GetCharacterInfoIndex(unit) == index)
                {
                    // The primary record was already built above, including its atlas rectangle.
                    renderedGlyphs[renderedCount++] = visualGlyphs[unit];
                    AddCharacterAtlasVersion(ref atlasVersion, in character, visualGlyphs[unit].AtlasUvRect);
                    // Its bounds equal the unit's own unless its source bounds were degenerate and the unit fell back to font metrics.
                    if (character.isVisible && !sourceColorBounds[unit].IsValid)
                    {
                        TextStudioGlyphVisualBounds primaryBounds = ResolveGlyphVisualBounds(in character, in sourceColorBounds[unit]);
                        sourceVisualBounds[unit] = UniteGlyphBounds(in sourceVisualBounds[unit], in primaryBounds);
                        widenedUnits[widenedUnitCount++] = unit;
                    }
                    continue;
                }

                // Additional renderer glyphs still need their own metrics and atlas data, even when they share a text unit.
                TextStudioGlyphColorBounds colorBounds = character.isVisible && IsCharacterVertexRangeValid(textInfo, in character)
                    ? ResolveSourceColorBounds(textInfo.meshInfo[character.materialReferenceIndex].vertices, character.vertexIndex, in character)
                    : ResolveFallbackColorBounds(in character);
                TextStudioGlyphVisualBounds bounds = ResolveGlyphVisualBounds(in character, in colorBounds);
                renderedGlyphs[renderedCount] = BuildVisualGlyph(plan, textInfo, in character, index, unit, in bounds, ResolveEffectUnitScale(_tmpText, in character));
                AddCharacterAtlasVersion(ref atlasVersion, in character, renderedGlyphs[renderedCount].AtlasUvRect);
                renderedCount++;
                if (character.isVisible)
                {
                    sourceVisualBounds[unit] = UniteGlyphBounds(in sourceVisualBounds[unit], in bounds);
                    widenedUnits[widenedUnitCount++] = unit;
                }
            }
            // Only units widened above, or whitespace with degenerate bounds, differ from their source bounds; rewriting a unit twice is harmless.
            for (int i = 0; i < widenedUnitCount; i++)
            {
                int unit = widenedUnits[i];
                if (sourceVisualBounds[unit].IsValid)
                    visualGlyphs[unit] = visualGlyphs[unit].WithBounds(sourceVisualBounds[unit].Minimum, sourceVisualBounds[unit].Maximum, sourceVisualBounds[unit].IsVisible);
            }

            InheritInvisibleSourceColors(
                plan,
                textInfo,
                sourceColors,
                sourceColorBounds,
                count,
                startUnit);

            BuildPlacementLines(plan, textInfo, positions, count, startUnit);
            int currentLayoutFingerprint = FinishLayoutFingerprint(_tmpText, characterCount, layoutFingerprintChain);
            FinishGeometryVersion(ref geometryVersion, textInfo, characterCount);
            TextStudioVisualVersion visualVersion = new TextStudioVisualVersion(
                unchecked((uint)currentLayoutFingerprint),
                geometryVersion.Value,
                atlasVersion.Value,
                materialsVersion);

            layout.SetFromPositions(
                positions, count,
                plan.LineRanges, plan.LineCount,
                plan.WordRanges, plan.WordCount,
                autoFitGlyphMargin,
                sourceCenterBaselineOffsets,
                _cachedPlacementLines,
                _cachedPlacementLineCount,
                sourceColors,
                sourceColorValidity,
                sourceColorBounds,
                sourceVisualBounds,
                effectUnitScales,
                plan.GlyphToLineIndex,
                plan.GlyphToWordIndex,
                plan.SourceContextRequirements | externalSourceRequirements,
                ResolveSourceAlignment(_tmpText),
                visualGlyphs,
                ResolveVisualCapabilities(_tmpText, textInfo),
                visualVersion,
                _fontSources, _fontSourceCount, renderedGlyphs, renderedCount, _colorSources, _colorSourceCount,
                unchangedGlyphCount: startUnit);
            if (resume)
                AppendLayoutCountForDebug++;
            _lineResumePointCount = linesInOrder ? characterLine + 1 : 0;
            if (appendEligible)
                RecordAppendLayoutState(plan, layout, count, appendSettings, materialsVersion);
            else
                InvalidateAppendLayout();

            TextStudioSourceContextRequirements sourceContextRequirements =
                plan.SourceContextRequirements | externalSourceRequirements;
            _frameApplier.SetSourceTextBounds(
                layout.SourceTextBounds,
                _materialEffectAdapter.HasPreparedState &&
                !_materialEffectAdapter.UsesTextMeshProRoute &&
                (AppearanceRequiresFieldMaterial ||
                 requiresWritingMaterial ||
                 (sourceContextRequirements &
                  TextStudioSourceContextRequirements.SourceTextNormalizedPosition) != 0));
            _frameApplier.SetAppearanceProgram(
                AppearanceFieldStyleSlot,
                AppearanceRequiresFieldMaterial ? (_appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.PrimaryFieldStyle : appearanceStyle) : null);

            bool materialPayloadReady = false;
            using (LayoutSourceDataProfilerMarker.Auto())
            {
                if (cacheVisualSourceData)
                {
                    _frameApplier.CacheSourceData(textInfo, plan);
                    if (_materialEffectAdapter.HasPreparedState && !_materialEffectAdapter.UsesTextMeshProRoute)
                    {
                        materialPayloadReady =
                            _frameApplier.InitializeMaterialPayloadStreams(_tmpText);
                    }
                }
                else
                {
                    _frameApplier.Clear();
                }
            }
            _materialEffectAdapter.SetPayloadEnabled(materialPayloadReady);
            if (materialPayloadReady)
                _appearanceAdapter.FlushAndBind();
            _layoutFingerprint = currentLayoutFingerprint;
            _hasLayoutFingerprint = true;
            _layoutValid = true;
            return true;
        }

        TextStudioVisualGlyph BuildVisualGlyph(
            TextStudioCompiledPlan plan,
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character,
            int characterInfoIndex,
            int textUnitIndex,
            in TextStudioGlyphVisualBounds visualBounds,
            float effectUnitScale)
        {
            Vector4 atlasUvRect = ResolveAtlasUvRect(textInfo, in character);
            uint glyphIndex = character.textElement != null
                ? character.textElement.glyphIndex
                : character.character;
            int atlasIndex = character.textElement != null && character.textElement.glyph != null
                ? character.textElement.glyph.atlasIndex
                : 0;
            TextStudioTextUnit unit = plan != null &&
                                      plan.TextUnits != null &&
                                      textUnitIndex >= 0 &&
                                      textUnitIndex < plan.TextUnitCount
                ? plan.TextUnits[textUnitIndex]
                : new TextStudioTextUnit(textUnitIndex, textUnitIndex, 1, textUnitIndex, 1);
            Vector2 minimum = visualBounds.IsValid
                ? visualBounds.Minimum
                : new Vector2(character.origin, character.descender);
            Vector2 maximum = visualBounds.IsValid
                ? visualBounds.Maximum
                : new Vector2(character.xAdvance, character.ascender);
            _layoutSources.SelectMaterial(character.material);
            float fieldScale = _layoutSources.GradientScale;
            if (_layoutSources.SelectFont(character.fontAsset))
                RegisterFontSource(character.fontAsset, _layoutSources.FontIdentity);
            float fallbackPixelsPerEm = _layoutSources.FallbackPixelsPerEm;
            TextStudioGlyphFieldFormat fieldFormat = _layoutSources.FieldFormat;
            float fieldPixelsPerEm = _layoutSources.ResolvePixelsPerEm(fallbackPixelsPerEm);
            float fallbackSafeDistance = fieldPixelsPerEm > 0.0001f
                ? Mathf.Max(0f, fieldScale - 1f) / fieldPixelsPerEm
                : 0f;
            float fieldSafeDistance = _layoutSources.ResolveSafeDistanceEm(fallbackSafeDistance);
            ulong fontIdentity = _layoutSources.FontIdentity;
            return new TextStudioVisualGlyph(
                fontIdentity,
                glyphIndex,
                textUnitIndex,
                textUnitIndex,
                characterInfoIndex,
                unit.resolvedStart,
                unit.resolvedLength,
                character.lineNumber,
                plan != null && plan.GlyphToWordIndex != null &&
                textUnitIndex < plan.GlyphToWordIndex.Length
                    ? plan.GlyphToWordIndex[textUnitIndex]
                    : -1,
                new Vector2(character.origin, character.baseLine),
                character.xAdvance - character.origin,
                character.baseLine,
                minimum,
                maximum,
                Vector2.right,
                atlasUvRect,
                unchecked((ushort)Mathf.Clamp(atlasIndex, 0, ushort.MaxValue)),
                fieldScale,
                0f,
                character.isVisible,
                fieldFormat,
                fieldPixelsPerEm,
                fieldSafeDistance,
                character.elementType == TMP_TextElementType.Sprite ? TextStudioVisualSourceKind.InlineSprite : TextStudioVisualSourceKind.FontGlyph,
                character.elementType == TMP_TextElementType.Sprite ? default : _layoutSources.AtlasToEm(character.textElement?.glyph),
                effectUnitScale,
                character.elementType == TMP_TextElementType.Sprite ? 0 : _layoutSources.ShapingIdentity,
                RegisterColorSource(textInfo, in character));
        }

        void RegisterFontSource(TMP_FontAsset font, ulong identity)
        {
            TextStudioFontAppearanceData data = _hub.FindAppearanceFont(font);
            if (data == null) return;
            for (int index = 0; index < _fontSourceCount; index++) if (_fontSources[index].FontIdentity == identity) return;
            if (_fontSourceCount == _fontSources.Length) Array.Resize(ref _fontSources, Math.Max(4, _fontSourceCount * 2));
            _fontSources[_fontSourceCount++] = new TextStudioVisualFontSource(identity, data);
        }

        static Vector4 ResolveAtlasUvRect(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character)
        {
            if (!IsCharacterVertexRangeValid(textInfo, in character))
                return Vector4.zero;

            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            Vector4[] uv = textInfo.meshInfo[materialIndex].uvs0;
            if (uv == null || vertexIndex < 0 || vertexIndex + 3 >= uv.Length)
                return Vector4.zero;

            Vector2 minimum = new Vector2(uv[vertexIndex].x, uv[vertexIndex].y);
            Vector2 maximum = minimum;
            for (int corner = 1; corner < 4; corner++)
            {
                Vector4 coordinate = uv[vertexIndex + corner];
                Vector2 point = new Vector2(coordinate.x, coordinate.y);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            return new Vector4(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        static ulong ResolveFontIdentity(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return 0UL;
            TextStudioDeterministicHash64 hash = default;
            hash.Add(fontAsset.GetStableId());
            hash.Add(fontAsset.name);
            hash.Add(fontAsset.faceInfo.familyName);
            hash.Add(fontAsset.faceInfo.styleName);
            hash.Add(fontAsset.faceInfo.pointSize);
            hash.Add(fontAsset.atlasWidth);
            hash.Add(fontAsset.atlasHeight);
            return hash.Value;
        }

        static TextStudioVisualCapabilities ResolveVisualCapabilities(
            TMP_Text text,
            TMP_TextInfo textInfo)
        {
            TextStudioVisualCapabilities capabilities =
                TextStudioVisualCapabilities.LogicalVisualMapping |
                TextStudioVisualCapabilities.ClusterMapping |
                TextStudioVisualCapabilities.LineAndWordMapping |
                TextStudioVisualCapabilities.GlyphMetrics |
                TextStudioVisualCapabilities.VisualBounds |
                TextStudioVisualCapabilities.Orientation |
                TextStudioVisualCapabilities.AtlasCoordinates |
                TextStudioVisualCapabilities.AtlasPages |
                TextStudioVisualCapabilities.SourceVertexColors |
                TextStudioVisualCapabilities.SourceColorBounds;
            Material material = text != null ? text.fontSharedMaterial : null;
            if (material != null && material.HasProperty("_GradientScale"))
                capabilities |= TextStudioVisualCapabilities.SignedDistanceField;
            if (TextStudioGlyphFieldContract.ResolveFormat(material) ==
                TextStudioGlyphFieldFormat.MultiDistance)
            {
                capabilities |= TextStudioVisualCapabilities.MultiChannelSignedDistanceField;
            }
            if (textInfo != null && textInfo.meshInfo != null)
            {
                for (int index = 0; index < textInfo.meshInfo.Length; index++)
                {
                    Material candidate = textInfo.meshInfo[index].material;
                    if (TextStudioGlyphFieldContract.ResolveFormat(candidate) !=
                        TextStudioGlyphFieldFormat.MultiDistance)
                    {
                        continue;
                    }
                    capabilities |=
                        TextStudioVisualCapabilities.MultiChannelSignedDistanceField;
                    break;
                }
            }
            return capabilities;
        }

        // RunLayout accumulates the layout, geometry and atlas parts while it reads each TMP record; materials are read once per
        // shared material here.
        static uint ComputeMaterialsVersion(TMP_Text text)
        {
            VisualVersionHash materials = VisualVersionHash.Create();
            Material[] sharedMaterials = text != null ? text.fontSharedMaterials : null;
            materials.Add(sharedMaterials?.Length ?? 0);
            if (sharedMaterials != null)
            {
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material material = sharedMaterials[i];
                    materials.Add(material != null ? material.GetStableId() : 0);
                    // This version only invalidates live renderer state; shader identity needs no allocated name string.
                    Shader shader = material != null ? material.shader : null;
                    materials.Add(shader != null ? shader.GetStableId() : 0);
                    materials.Add(material != null ? material.ComputeCRC() : 0);
                    AddTextureVersion(
                        ref materials,
                        material != null ? material.mainTexture : null);
                }
            }
            return materials.Value;
        }

        static void AddFontAtlasVersion(
            ref VisualVersionHash hash,
            TMP_FontAsset font,
            int selectedAtlasIndex)
        {
            hash.Add(ResolveFontIdentity(font));
            Texture2D[] atlasTextures = font != null ? font.atlasTextures : null;
            if (atlasTextures == null)
            {
                hash.Add(0);
                return;
            }

            hash.Add(atlasTextures.Length);
            if (selectedAtlasIndex >= 0)
            {
                hash.Add(selectedAtlasIndex);
                Texture2D selected = selectedAtlasIndex < atlasTextures.Length
                    ? atlasTextures[selectedAtlasIndex]
                    : null;
                AddTextureVersion(ref hash, selected);
                return;
            }

            for (int i = 0; i < atlasTextures.Length; i++)
                AddTextureVersion(ref hash, atlasTextures[i]);
        }

        static void AddTextureVersion(
            ref VisualVersionHash hash,
            Texture texture)
        {
            hash.Add(texture != null ? texture.GetStableId() : 0);
            hash.Add(texture != null ? texture.updateCount : 0u);
        }

        static Vector2 ResolveSourceAlignment(TMP_Text text)
        {
            if (text == null)
                return new Vector2(0.5f, 0.5f);

            float horizontal = text.horizontalAlignment switch
            {
                HorizontalAlignmentOptions.Center => 0.5f,
                HorizontalAlignmentOptions.Geometry => 0.5f,
                HorizontalAlignmentOptions.Right => 1f,
                _ => 0f
            };
            float vertical = text.verticalAlignment switch
            {
                VerticalAlignmentOptions.Top => 1f,
                VerticalAlignmentOptions.Capline => 1f,
                VerticalAlignmentOptions.Middle => 0.5f,
                VerticalAlignmentOptions.Geometry => 0.5f,
                _ => 0f
            };
            return new Vector2(horizontal, vertical);
        }

        public void UseUnanimatedPassThrough(TextStudioCompiledPlan plan)
        {
            _hasPlainFrame = false;
            if (_tmpText == null)
                return;

            if (_isUsingUnanimatedPassThrough)
            {
                // Pass-through creates none of the outputs its first entry released, so re-entering only resets layout state.
                _layoutValid = false;
                _hasFreshMeshForLayout = false;
                _hasLayoutFingerprint = false;
                _skippedVisualFrameWhileSuppressed = false;
                // Renderer discovery between pass-through ticks may have auto-hidden TMP again; pass-through always draws it.
                UpdateVisibility(false);
                return;
            }

            if (!_hasFreshMeshForLayout && _tmpText.font != null)
                SuppressedForceMeshUpdate();

            _frameApplier.SetWritingSourceSampleOffset(_tmpText, -1);
            _materialEffectAdapter.Prepare(_tmpText, plan);
            _appearanceAdapter.Release();
            _appearancePlanAdapter.Release();
            RestoreWorldShadowState();
            DisposeOrderedRenderer();
            DisposeHeroCompositor();
            DisposeWritingRenderer();
            SetInternalReplacementVisibility(false);
            _frameApplier.Clear();
            _layoutValid = false;
            _hasFreshMeshForLayout = false;
            _hasLayoutFingerprint = false;
            _skippedVisualFrameWhileSuppressed = false;
            _isUsingUnanimatedPassThrough = true;
            DisposeDecorations();
            DisposeStyleDecorations();
            UpdateVisibility(false);
        }

        // Pass-through text whose every glyph is held hidden suppresses TMP's draw whole instead of leaving pass-through.
        internal void SetPassThroughHidden(bool hidden)
        {
            if (!hidden)
            {
                ReleasePassThroughVisibility();
                return;
            }

            if (_isPassThroughHidden)
                return;
            _isPassThroughHidden = true;
            _passThroughSourceVisibility.Synchronize(_tmpText, true);
        }

        void ReleasePassThroughVisibility()
        {
            if (!_isPassThroughHidden)
                return;
            _isPassThroughHidden = false;
            _passThroughSourceVisibility.Restore();
        }

        internal void ReleaseMaterialEffectsIfUnused(
            TextStudioCompiledPlan plan,
            bool requiresExternalMaterial)
        {
            if (_materialEffectAdapter.ReleaseIfUnused(plan, requiresExternalMaterial))
                _frameApplier.RestoreSourceNormals(_tmpText);
        }

        // False while ReleaseMaterialEffectsIfUnused would change nothing, whatever the plan requires: the material adapter
        // holds nothing to restore and no mesh waits for its source normals.
        internal bool HasReleasableMaterialState =>
            _materialEffectAdapter.HasRestorableState || _frameApplier.NeedsSourceNormalRestore;

        internal static float ResolveEffectUnitScale(
            TMP_Text tmpText,
            in TMP_CharacterInfo character)
        {
            TMP_FontAsset fontAsset = character.fontAsset;
            float facePointSize = character.elementType == TMP_TextElementType.Sprite &&
                character.textElement is TMP_SpriteCharacter sprite && sprite.textAsset is TMP_SpriteAsset spriteAsset
                ? spriteAsset.faceInfo.pointSize
                : fontAsset != null ? fontAsset.faceInfo.pointSize : 0f;
            if (character.scale > 0f && facePointSize > 0f)
                return Mathf.Max(0.0001f, character.scale * facePointSize);

            float cameraMultiplier = tmpText != null && !tmpText.isOrthographic ? 0.1f : 1f;
            float pointSize = character.pointSize > 0f
                ? character.pointSize
                : tmpText != null
                    ? tmpText.fontSize
                    : 1f;
            float faceScale = fontAsset != null && fontAsset.faceInfo.scale > 0f
                ? fontAsset.faceInfo.scale
                : 1f;
            return Mathf.Max(0.0001f, pointSize * faceScale * cameraMultiplier);
        }

        internal static float ResolveEffectUnitScale(TMP_Text tmpText)
        {
            return ResolveEffectUnitScale(tmpText, default);
        }

        void InheritInvisibleSourceColors(
            TextStudioCompiledPlan plan,
            TMP_TextInfo textInfo,
            TextStudioGlyphColorQuad[] sourceColors,
            TextStudioGlyphColorBounds[] sourceColorBounds,
            int count,
            int startUnit = 0)
        {
            // Neighbours never cross a TMP line, so a pass from a line's first unit leaves the units before it unchanged.
            if (_previousVisibleColorUnits == null || _previousVisibleColorUnits.Length < count)
                _previousVisibleColorUnits = new int[TextStudioBufferCapacity.ForCount(count)];
            if (_nextVisibleColorUnits == null || _nextVisibleColorUnits.Length < count)
                _nextVisibleColorUnits = new int[TextStudioBufferCapacity.ForCount(count)];

            int previousVisibleUnit = -1;
            int previousLine = -1;
            for (int i = startUnit; i < count; i++)
            {
                int characterInfoIndex = GetCharacterInfoIndex(i);
                if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                {
                    _previousVisibleColorUnits[i] = -1;
                    previousVisibleUnit = -1;
                    previousLine = -1;
                    continue;
                }

                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
                if (character.lineNumber != previousLine)
                {
                    previousVisibleUnit = -1;
                    previousLine = character.lineNumber;
                }
                _previousVisibleColorUnits[i] = previousVisibleUnit;
                if (character.isVisible && sourceColors[i].IsInitialized)
                    previousVisibleUnit = i;
            }

            int nextVisibleUnit = -1;
            int nextLine = -1;
            for (int i = count - 1; i >= startUnit; i--)
            {
                int characterInfoIndex = GetCharacterInfoIndex(i);
                if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                {
                    _nextVisibleColorUnits[i] = -1;
                    nextVisibleUnit = -1;
                    nextLine = -1;
                    continue;
                }

                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
                if (character.lineNumber != nextLine)
                {
                    nextVisibleUnit = -1;
                    nextLine = character.lineNumber;
                }
                _nextVisibleColorUnits[i] = nextVisibleUnit;
                if (character.isVisible && sourceColors[i].IsInitialized)
                    nextVisibleUnit = i;
            }

            for (int i = startUnit; i < count; i++)
            {
                int characterInfoIndex = GetCharacterInfoIndex(i);
                if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                    continue;

                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
                if (character.isVisible)
                    continue;

                Color32 semanticColor = ResolveSemanticColor(in character);
                int previousUnit = _previousVisibleColorUnits[i];
                int nextUnit = _nextVisibleColorUnits[i];
                bool usePrevious = IsCompatibleColorNeighbor(
                    textInfo,
                    previousUnit,
                    character.lineNumber,
                    semanticColor);
                bool useNext = IsCompatibleColorNeighbor(
                    textInfo,
                    nextUnit,
                    character.lineNumber,
                    semanticColor);

                if (usePrevious && useNext)
                {
                    ref readonly TMP_CharacterInfo previousCharacter = ref textInfo.characterInfo[
                        GetCharacterInfoIndex(previousUnit)];
                    ref readonly TMP_CharacterInfo nextCharacter = ref textInfo.characterInfo[
                        GetCharacterInfoIndex(nextUnit)];
                    float currentCenter = GetCharacterHorizontalCenter(in character);
                    float previousCenter = GetCharacterHorizontalCenter(in previousCharacter);
                    float nextCenter = GetCharacterHorizontalCenter(in nextCharacter);
                    float amount = Mathf.InverseLerp(previousCenter, nextCenter, currentCenter);
                    sourceColors[i] = TextStudioGlyphColorQuad.Lerp(
                        in sourceColors[previousUnit],
                        in sourceColors[nextUnit],
                        amount);
                    sourceColorBounds[i] = ResolveInheritedColorBounds(
                        in character,
                        in sourceColorBounds[previousUnit],
                        in sourceColorBounds[nextUnit],
                        amount);
                }
                else if (usePrevious)
                {
                    sourceColors[i] = sourceColors[previousUnit];
                    sourceColorBounds[i] = ResolveInheritedColorBounds(
                        in character,
                        in sourceColorBounds[previousUnit]);
                }
                else if (useNext)
                {
                    sourceColors[i] = sourceColors[nextUnit];
                    sourceColorBounds[i] = ResolveInheritedColorBounds(
                        in character,
                        in sourceColorBounds[nextUnit]);
                }
            }
        }

        bool IsCompatibleColorNeighbor(
            TMP_TextInfo textInfo,
            int unitIndex,
            int lineNumber,
            Color32 semanticColor)
        {
            if (unitIndex < 0)
                return false;
            int characterInfoIndex = GetCharacterInfoIndex(unitIndex);
            if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                return false;
            ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
            return character.isVisible &&
                   character.lineNumber == lineNumber &&
                   ResolveSemanticColor(in character).Equals(semanticColor);
        }

        static float GetCharacterHorizontalCenter(in TMP_CharacterInfo character)
        {
            return (character.origin + character.xAdvance) * 0.5f;
        }

        static TextStudioGlyphColorBounds ResolveSourceColorBounds(
            Vector3[] vertices,
            int vertexIndex,
            in TMP_CharacterInfo character)
        {
            if (vertices == null || vertexIndex < 0 || vertexIndex + 3 >= vertices.Length)
                return ResolveFallbackColorBounds(in character);

            Vector2 minimum = new Vector2(vertices[vertexIndex].x, vertices[vertexIndex].y);
            Vector2 maximum = minimum;
            for (int corner = 1; corner < 4; corner++)
            {
                Vector3 vertex = vertices[vertexIndex + corner];
                minimum = Vector2.Min(minimum, new Vector2(vertex.x, vertex.y));
                maximum = Vector2.Max(maximum, new Vector2(vertex.x, vertex.y));
            }

            Vector2 origin = new Vector2(character.origin, character.baseLine);
            return new TextStudioGlyphColorBounds(minimum - origin, maximum - origin);
        }

        static TextStudioGlyphColorBounds ResolveFallbackColorBounds(
            in TMP_CharacterInfo character)
        {
            float advance = character.xAdvance - character.origin;
            float minimumX = Mathf.Min(0f, advance);
            float maximumX = Mathf.Max(0f, advance);
            if (maximumX - minimumX <= 0.0001f)
                maximumX = minimumX + 0.0001f;

            float minimumY = character.descender - character.baseLine;
            float maximumY = character.ascender - character.baseLine;
            if (maximumY - minimumY <= 0.0001f)
                maximumY = minimumY + Mathf.Max(0.0001f, character.pointSize);
            return new TextStudioGlyphColorBounds(
                new Vector2(minimumX, minimumY),
                new Vector2(maximumX, maximumY));
        }

        static TextStudioGlyphColorBounds ResolveInheritedColorBounds(
            in TMP_CharacterInfo character,
            in TextStudioGlyphColorBounds inherited)
        {
            TextStudioGlyphColorBounds fallback = ResolveFallbackColorBounds(in character);
            if (!inherited.IsValid)
                return fallback;
            return inherited.WithHorizontalRange(
                fallback.Minimum.x,
                fallback.Maximum.x);
        }

        static TextStudioGlyphColorBounds ResolveInheritedColorBounds(
            in TMP_CharacterInfo character,
            in TextStudioGlyphColorBounds previous,
            in TextStudioGlyphColorBounds next,
            float amount)
        {
            if (!previous.IsValid)
                return ResolveInheritedColorBounds(in character, in next);
            if (!next.IsValid)
                return ResolveInheritedColorBounds(in character, in previous);

            float t = Mathf.Clamp01(amount);
            TextStudioGlyphColorBounds fallback = ResolveFallbackColorBounds(in character);
            return new TextStudioGlyphColorBounds(
                new Vector2(
                    fallback.Minimum.x,
                    Mathf.Lerp(previous.Minimum.y, next.Minimum.y, t)),
                new Vector2(
                    fallback.Maximum.x,
                    Mathf.Lerp(previous.Maximum.y, next.Maximum.y, t)));
        }

        static Color32 ResolveSemanticColor(in TMP_CharacterInfo character)
        {
            if ((character.style & FontStyles.Underline) != 0)
                return character.underlineColor;
            if ((character.style & FontStyles.Strikethrough) != 0)
                return character.strikethroughColor;
            return character.color;
        }

        // A pass from startUnit, the first unit of a TMP line, keeps the lines that end before it.
        void BuildPlacementLines(
            TextStudioCompiledPlan plan,
            TMP_TextInfo textInfo,
            Vector3[] positions,
            int glyphCount,
            int startUnit = 0)
        {
            if (startUnit <= 0)
            {
                _cachedPlacementLineCount = 0;
            }
            else
            {
                while (_cachedPlacementLineCount > 0)
                {
                    LineRange last = _cachedPlacementLines[_cachedPlacementLineCount - 1];
                    if (last.glyphStart + last.glyphCount <= startUnit)
                        break;
                    _cachedPlacementLineCount--;
                }
            }
            if (plan == null || textInfo == null || glyphCount <= 0)
                return;

            int activeStart = -1;
            int activeLineNumber = 0;
            for (int i = startUnit; i < glyphCount; i++)
            {
                if (plan.IsLineBreakUnit(i))
                {
                    AddCachedPlacementLine(activeStart, i, positions);
                    activeStart = -1;
                    continue;
                }

                int lineNumber = ResolveTMPLineNumber(plan, textInfo, i, activeLineNumber);
                if (activeStart < 0)
                {
                    activeStart = i;
                    activeLineNumber = lineNumber;
                }
                else if (lineNumber != activeLineNumber)
                {
                    AddCachedPlacementLine(activeStart, i, positions);
                    activeStart = i;
                    activeLineNumber = lineNumber;
                }
            }

            AddCachedPlacementLine(activeStart, glyphCount, positions);
        }

        int ResolveTMPLineNumber(TextStudioCompiledPlan plan, TMP_TextInfo textInfo, int glyphIndex, int fallbackLineNumber)
        {
            int characterInfoIndex = GetCharacterInfoIndex(glyphIndex);
            if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                return fallbackLineNumber;

            return textInfo.characterInfo[characterInfoIndex].lineNumber;
        }

        // Maps units from startUnit on to their first TMP character from startCharacter on; an append layout keeps the entries
        // of the units before them, whose characters precede startCharacter.
        void BuildUnitToCharacterInfoMap(
            TextStudioCompiledPlan plan,
            TMP_TextInfo textInfo,
            int unitCount,
            int startUnit = 0,
            int startCharacter = 0)
        {
            if (_unitToCharacterInfo == null || _unitToCharacterInfo.Length < unitCount)
            {
                int[] map = new int[TextStudioBufferCapacity.ForCount(unitCount)];
                if (startUnit > 0)
                    Array.Copy(_unitToCharacterInfo, map, startUnit);
                _unitToCharacterInfo = map;
            }

            for (int i = startUnit; i < unitCount; i++)
                _unitToCharacterInfo[i] = -1;

            if (plan == null || textInfo == null || textInfo.characterInfo == null)
                return;

            int characterCount = Mathf.Min(textInfo.characterCount, textInfo.characterInfo.Length);
            for (int characterInfoIndex = startCharacter; characterInfoIndex < characterCount; characterInfoIndex++)
            {
                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
                if (character.elementType != TMP_TextElementType.Character)
                    continue;

                int unitIndex = plan.GetUnitIndexForDisplayChar(character.index);
                if (unitIndex < 0 || unitIndex >= unitCount || _unitToCharacterInfo[unitIndex] >= 0)
                    continue;

                _unitToCharacterInfo[unitIndex] = characterInfoIndex;
            }
        }

        int GetCharacterInfoIndex(int unitIndex)
        {
            return _unitToCharacterInfo != null &&
                   unitIndex >= 0 &&
                   unitIndex < _unitToCharacterInfo.Length
                ? _unitToCharacterInfo[unitIndex]
                : -1;
        }

        void AddCachedPlacementLine(int start, int end, Vector3[] positions)
        {
            if (start < 0 || end <= start)
                return;

            if (_cachedPlacementLines == null || _cachedPlacementLines.Length <= _cachedPlacementLineCount)
            {
                int newSize = _cachedPlacementLines == null
                    ? 8
                    : Mathf.Max(_cachedPlacementLines.Length * 2, _cachedPlacementLineCount + 1);
                Array.Resize(ref _cachedPlacementLines, newSize);
            }

            _cachedPlacementLines[_cachedPlacementLineCount] = new LineRange
            {
                glyphStart = start,
                glyphCount = end - start,
                lineIndex = _cachedPlacementLineCount,
                width = 0f,
                baseline = positions != null && start < positions.Length ? positions[start].y : 0f
            };
            _cachedPlacementLineCount++;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Frame rendering (TMP vertex/color manipulation)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Restores current colors for a fresh layout or prepared TMP streams for an unchanged layout without rebuilding replacement graphics.</summary>
        internal void RepublishCurrentFrame(
            in TextStudioFrame frame,
            PresentationGlyph[] presentationGlyphs,
            IPresentationGlyphProvider glyphProvider,
            bool applyPlacement,
            bool freshLayoutOnly)
        {
            if (!_layoutValid || _tmpText == null || !_tmpText.isActiveAndEnabled ||
                _suppressRegen || _skippedVisualFrameWhileSuppressed)
                return;

            if (freshLayoutOnly)
            {
                if (_frameApplier.RepublishFreshLayoutColors(_tmpText, in frame, presentationGlyphs, glyphProvider))
                    _replacementSourceVisibility.Synchronize(_tmpText, _hasInternalReplacement);
                return;
            }
            if (_materialEffectAdapter.RequiresRefresh(_tmpText))
                return;

            // Native regeneration can run inside a Canvas graphic rebuild. Reuse the already prepared
            // material bindings and streams; replacement graphics and resource preparation belong to the scheduler.
            _frameApplier.RefreshCanvasPayloadTransform(_tmpText);
            _frameApplier.RetainHiddenGlyphGeometry = _hub.RetainsHiddenGlyphGeometry;
            _frameApplier.ApplyFrame(
                _tmpText, in frame, presentationGlyphs, glyphProvider,
                forceAll: true, applyPlacement: applyPlacement, notifyMeshUpdated: false);
            _replacementSourceVisibility.Synchronize(_tmpText, _hasInternalReplacement);
        }

        /// <summary>
        /// Apply an animated frame to TMP's mesh vertices and colors, then rebuild
        /// curved decoration tiles. No-op when TMP layout is invalid, TMP is disabled,
        /// or TMP visual output is suppressed by an external renderer.
        /// </summary>
        public void RenderFrame(
            in TextStudioFrame frame,
            PresentationGlyph[] presentationGlyphs,
            IPresentationGlyphProvider glyphProvider,
            bool suppressVisualMeshUpdate,
            bool applyPlacement = true,
            bool steadyFrame = false)
        {
            if (!_layoutValid || _tmpText == null || !_tmpText.enabled) return;

            if (suppressVisualMeshUpdate)
            {
                _hasPlainFrame = false;
                _skippedVisualFrameWhileSuppressed = true;
                DisposeOrderedRenderer();
                DisposeHeroCompositor();
                SetInternalReplacementVisibility(false);
                DisposeDecorations();
                DisposeStyleDecorations();
                _frameApplier.SetWritingSourceSampleOffset(_tmpText, -1);
                DisposeWritingRenderer();
                return;
            }

            if (steadyFrame && IsPlainFrameCurrent(in frame))
            {
                RenderPlainFrame(in frame, presentationGlyphs, glyphProvider, applyPlacement);
                return;
            }

            _hasPlainFrame = false;
            bool hasExactLens = TextStudioLensMagnifierDescriptor.TryPrepare(
                in frame,
                _hub,
                out TextStudioLensMagnifierDescriptor lensMagnifier);
            bool potentiallyHasWriting =
                TextStudioWritingRenderer.RequiresWritingMaterial(
                    frame.Plan.CompiledPlan,
                    frame.WritingStyle);
            TextStudio2DTextMeshProStyle textMeshProStyle = ResolveTextMeshProStyle(frame.AppearanceStyle, frame.AppearancePlan,
                frame.Plan.CompiledPlan, potentiallyHasWriting);
            // Leaving the TextMeshPro route needs the payload streams only layout prepares. Style, Writing and World Batch Group
            // changes lay out first; any other change that ends the route keeps this route frame and lays out again.
            if (!textMeshProStyle.IsValid && _materialEffectAdapter.UsesTextMeshProRoute)
            {
                _hub.ScheduleLayoutRebuild();
                return;
            }
            TextStudioWritingPrepareResult writingResult = default;
            if (potentiallyHasWriting)
            {
                _writingRenderer ??= new TextStudioWritingRenderer(_hub);
                writingResult = _writingRenderer.Prepare(
                    in frame,
                    _tmpText,
                    _hub.MotionPolicy);
            }
            else
            {
                DisposeWritingRenderer();
            }

            bool appearanceMaterialChanged =
                PrepareAppearance(frame.AppearanceStyle, frame.AppearancePlan, hasExactLens, in textMeshProStyle);
            bool hasAppearancePlan = _appearancePlanAdapter.Plan != null;
            UpdateWorldShadowState(hasAppearancePlan ? _appearancePlanAdapter.WorldShadowStyle : frame.AppearanceStyle);
            TextStudioVisualSnapshot visualSnapshot = frame.VisualSnapshot;
            // Plain text skips per-frame mapping and global table binding; the first plain frame still releases prior state.
            bool appearanceAdapterUnused =
                !hasAppearancePlan && _appearanceAdapter.IsUnusedFor(frame.AppearanceStyle);
            if (hasAppearancePlan)
                appearanceMaterialChanged |= _appearancePlanAdapter.UpdateMappings(in frame, _tmpText, _materialEffectAdapter);
            else if (!appearanceAdapterUnused)
            {
                _appearanceAdapter.UpdateMappings(in frame, _tmpText, _materialEffectAdapter);
            }
            bool prepareMaterial =
                appearanceMaterialChanged ||
                writingResult.SourceStateChanged ||
                (frame.GlobalDirty &
                 (DirtyFlags.AppearanceRenderState |
                  DirtyFlags.WritingRenderState)) != 0;
            if (prepareMaterial)
            {
                _materialEffectAdapter.Prepare(
                    _tmpText,
                    frame.Plan.CompiledPlan,
                    _requiresExternalMaterialIntent,
                    writingResult.RequiresSourceMaterial,
                    AppearanceRequiresFieldMaterial,
                    AppearancePaddingTier,
                    AppearanceRequiresFullShaderTier ? null : frame.AppearanceStyle,
                    AppearanceRequiresFullShaderTier ? null : _appearancePlanAdapter.Plan,
                    AppearanceTextMeshProStyle);
                // A source that cannot take or keep its route material, such as one edited at runtime, is decided again at layout.
                if (_materialEffectAdapter.ConsumeTextMeshProRouteFailure())
                    _hub.ScheduleLayoutRebuild();
                if (_materialEffectAdapter.UsesTextMeshProRoute)
                    _frameApplier.ReleaseMaterialStreams(_tmpText);
            }
            bool writingSourceBindingChanged =
                _frameApplier.SetWritingSourceSampleOffset(
                    _tmpText,
                    writingResult.HasWriting
                        ? writingResult.SourceSampleOffset
                        : -1);
            _frameApplier.SetAppearanceProgram(
                AppearanceFieldStyleSlot,
                AppearanceRequiresFieldMaterial
                    ? (hasAppearancePlan ? _appearancePlanAdapter.PrimaryFieldStyle : frame.AppearanceStyle)
                    : null);
            if (hasAppearancePlan) _appearancePlanAdapter.FlushAndBind();
            else if (!appearanceAdapterUnused)
            {
                _appearanceAdapter.UpdateVisualModifiers(in frame, _hub.MotionPolicy);
                _appearanceAdapter.FlushAndBind();
            }

            DirtyFlags structuralDirty = StructuralRefreshDirty;
            bool forceVisualRefresh =
                _skippedVisualFrameWhileSuppressed ||
                writingSourceBindingChanged ||
                (frame.GlobalDirty & structuralDirty) != 0;
            bool forceGeometryRefresh = _skippedVisualFrameWhileSuppressed || writingSourceBindingChanged ||
                (frame.GlobalDirty & (structuralDirty & ~(DirtyFlags.AppearanceTopology | DirtyFlags.AppearanceRenderState))) != 0;
            if (_skippedVisualFrameWhileSuppressed)
                _frameApplier.CacheSourceData(_tmpText.textInfo, frame.Plan.CompiledPlan);
            _skippedVisualFrameWhileSuppressed = false;

            _frameApplier.RefreshCanvasPayloadTransform(_tmpText);
            _frameApplier.RetainHiddenGlyphGeometry = _hub.RetainsHiddenGlyphGeometry;
            _frameApplier.ApplyFrame(
                _tmpText,
                in frame,
                presentationGlyphs,
                glyphProvider,
                forceVisualRefresh,
                applyPlacement: applyPlacement);

            if (hasAppearancePlan)
            {
                // Program preparation also reports paint-only changes. Geometry reuse is
                // guarded by each renderer's slots, source versions and scoped binding revisions.
                DisposeStyleDecorations();
                bool rendered;
                if (_appearancePlanAdapter.UsesHero)
                {
                    DisposeOrderedRenderer();
                    _heroCompositor ??= new TextStudio2DHeroCompositor(_hub);
                    rendered = _appearancePlanAdapter.IsReady && _heroCompositor.RenderAppearance(in frame, _tmpText,
                        _appearancePlanAdapter.Programs, _appearancePlanAdapter.Binding, in lensMagnifier,
                        _materialEffectAdapter, _frameApplier, forceGeometryRefresh,
                        _appearancePlanAdapter.OutputPixelsPerUnit);
                    if (!rendered) _heroCompositor.HideOutput();
                }
                else
                {
                    DisposeHeroCompositor();
                    _orderedRenderer ??= new TextStudio2DOrderedRenderer(_hub);
                    rendered = _orderedRenderer.RenderAppearance(in frame, _tmpText, _appearancePlanAdapter,
                        _materialEffectAdapter, _frameApplier, forceVisualRefresh);
                }
                SetInternalReplacementVisibility(rendered);
            }
            else
            {
            bool heroActive = _appearanceAdapter.HasHeroProgram;
            bool orderedSupported = !heroActive && _appearanceAdapter.HasActiveProgram && TextStudio2DOrderedRenderer.Supports(frame.AppearanceStyle);
            if (!heroActive && _appearanceAdapter.HasRetainedProgram)
            {
                bool created = _styleDecorations == null;
                _styleDecorations ??= new TextStudio2DStyleDecorations();
                if (created || forceGeometryRefresh || ShouldRebuildStyleDecorations(in frame))
                    _styleDecorations.Rebuild(in frame, _tmpText, _appearanceAdapter.StyleSlot,
                        _appearanceAdapter.BoundStyle, _appearanceAdapter.SourceDemand, _frameApplier);
            }
            else DisposeStyleDecorations();
            if (heroActive)
            {
                DisposeOrderedRenderer();
                DisposeStyleDecorations();
                _heroCompositor ??= new TextStudio2DHeroCompositor(_hub);
                bool rendered = _heroCompositor.Render(
                    in frame,
                    _tmpText,
                    _appearanceAdapter.HeroProgram,
                    _appearanceAdapter.HeroStyleSlot,
                    in lensMagnifier,
                    _materialEffectAdapter,
                    _frameApplier,
                    forceGeometryRefresh,
                    _appearanceAdapter.OutputPixelsPerUnit,
                    _appearanceAdapter.SourceDemand);
                SetInternalReplacementVisibility(rendered);
                if (!rendered)
                    _heroCompositor.HideOutput();
            }
            else
            {
                DisposeHeroCompositor();
                bool orderedActive = false;
                if (orderedSupported)
                {
                    _orderedRenderer ??= new TextStudio2DOrderedRenderer(_hub);
                    orderedActive = _orderedRenderer.Render(in frame, _tmpText, _appearanceAdapter.StyleSlot,
                        _materialEffectAdapter, _frameApplier, forceVisualRefresh || appearanceMaterialChanged, _styleDecorations,
                        _appearanceAdapter.RequiresOrderedPrimitives || _hub.IsWorldBatched);
                }
                else DisposeOrderedRenderer();
                SetInternalReplacementVisibility(orderedActive);
            }
            }

            RebuildDecorationsIfNeeded(in frame, forceVisualRefresh);

            // A frame without a lens, writing, an appearance plan or program, material preparation, a writing binding change,
            // a world shadow or an internal replacement left all of that state released, and the next frame with the same
            // inputs would release nothing again; RenderPlainFrame applies such frames of the hub's steady lane.
            _hasPlainFrame =
                !hasExactLens &&
                !potentiallyHasWriting &&
                !hasAppearancePlan &&
                appearanceAdapterUnused &&
                !prepareMaterial &&
                !writingSourceBindingChanged &&
                !_hasInternalReplacement &&
                _worldShadowRenderers.Count == 0 &&
                !HasCompositionLayerBinding(frame.Plan.CompiledPlan);
            _plainAppearanceStyle = _hasPlainFrame ? frame.AppearanceStyle : null;
            _plainAppearancePlan = _hasPlainFrame ? frame.AppearancePlan : null;
            _plainWritingStyle = _hasPlainFrame ? frame.WritingStyle : null;
            _plainCompiledPlan = _hasPlainFrame ? frame.Plan.CompiledPlan : null;
        }

        // The steady lane hands RenderFrame only frames that follow the hub's previous frame without a change notification,
        // so no other bridge call has touched this state since; same input objects then mean the same plain outcome.
        bool IsPlainFrameCurrent(in TextStudioFrame frame)
        {
            return _hasPlainFrame &&
                   !_skippedVisualFrameWhileSuppressed &&
                   (frame.GlobalDirty & (DirtyFlags.AppearanceRenderState | DirtyFlags.WritingRenderState)) == 0 &&
                   ReferenceEquals(frame.AppearanceStyle, _plainAppearanceStyle) &&
                   ReferenceEquals(frame.AppearancePlan, _plainAppearancePlan) &&
                   ReferenceEquals(frame.WritingStyle, _plainWritingStyle) &&
                   ReferenceEquals(frame.Plan.CompiledPlan, _plainCompiledPlan) &&
                   !_hub.HasInfluenceTarget;
        }

        // RenderFrame for a plain frame (IsPlainFrameCurrent): the lens, writing, appearance, material, world shadow, renderer
        // disposal and replacement steps would change nothing, so the glyphs and decorations are all that remain.
        void RenderPlainFrame(
            in TextStudioFrame frame,
            PresentationGlyph[] presentationGlyphs,
            IPresentationGlyphProvider glyphProvider,
            bool applyPlacement)
        {
            unchecked { _plainFrameCountForDebug++; }
            // RenderFrame's refresh rule without a suppressed frame or a writing binding change.
            bool forceVisualRefresh = (frame.GlobalDirty & StructuralRefreshDirty) != 0;
            _frameApplier.RefreshCanvasPayloadTransform(_tmpText);
            _frameApplier.RetainHiddenGlyphGeometry = _hub.RetainsHiddenGlyphGeometry;
            _frameApplier.ApplyFrame(
                _tmpText,
                in frame,
                presentationGlyphs,
                glyphProvider,
                forceVisualRefresh,
                applyPlacement: applyPlacement);
            RebuildDecorationsIfNeeded(in frame, forceVisualRefresh);
        }

        // Frame changes that make RenderFrame refresh every glyph and rebuild decorations.
        const DirtyFlags StructuralRefreshDirty =
            DirtyFlags.Text |
            DirtyFlags.Style |
            DirtyFlags.Topology |
            DirtyFlags.Layout |
            DirtyFlags.Placement |
            DirtyFlags.AppearanceTopology |
            DirtyFlags.AppearanceRenderState |
            DirtyFlags.WritingTopology |
            DirtyFlags.WritingBounds |
            DirtyFlags.WritingRenderState;

        void RebuildDecorationsIfNeeded(in TextStudioFrame frame, bool forceVisualRefresh)
        {
            if (forceVisualRefresh || ShouldRebuildDecorations(in frame))
            {
                if (_decorations == null)
                    _decorations = new TextStudioDecorations(_hub);
                Material decorationMaterial =
                    _materialEffectAdapter.SourcePrimaryMaterial ??
                    _tmpText.fontSharedMaterial;
                _decorations.Rebuild(in frame, _tmpText, decorationMaterial);
            }
        }

        // Composition layers can enable writing without a frame change, so plans with them never replay a plain frame.
        static bool HasCompositionLayerBinding(TextStudioCompiledPlan plan)
        {
            if (plan?.EffectBindings == null)
                return false;
            for (int index = 0; index < plan.EffectBindingCount; index++)
            {
                if (plan.EffectBindings[index].effect is TextStudioCompositionLayer)
                    return true;
            }
            return false;
        }

        internal void RestoreTransientMaterialState()
        {
            _hasPlainFrame = false;
            DisposeOrderedRenderer();
            DisposeHeroCompositor();
            SetInternalReplacementVisibility(false);
            _frameApplier.SetWritingSourceSampleOffset(_tmpText, -1);
            _frameApplier.RestoreSourceNormals(_tmpText);
            _materialEffectAdapter.Restore();
            _appearanceAdapter.Release();
            _appearancePlanAdapter.Release();
            DisposeWritingRenderer();
        }

        bool ShouldRebuildDecorations(in TextStudioFrame frame)
        {
            DirtyFlags structuralDirty = DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Layout | DirtyFlags.Placement;
            if ((frame.GlobalDirty & structuralDirty) != 0) return true;
            // Decorations come from text, style, layout and page state only, so animation cannot add them to text that had none.
            if (_decorations == null || _decorations.IsEmpty) return false;
            if (!frame.Dirty.IsCreated) return true;
            return frame.Dirty.AnyDirtyInRange(0, frame.AnimatedCount);
        }

        bool ShouldRebuildStyleDecorations(in TextStudioFrame frame)
        {
            if (_styleDecorations == null)
                return false;
            if (_styleDecorations.StyleSlot != _appearanceAdapter.StyleSlot ||
                _styleDecorations.GeometryFingerprint != (frame.AppearanceStyle?.GeometryFingerprint ?? 0) ||
                frame.AppearanceStyle?.RequiresContourGeometry == true &&
                _styleDecorations.ContourPrecisionFingerprint != (_appearanceAdapter.SourceDemand?.GeometryFingerprint ?? 0))
                return true;
            if (!frame.Dirty.IsCreated)
                return true;
            return frame.Dirty.AnyDirtyInRange(0, frame.AnimatedCount);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Visibility management
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Show or hide TMP's visual output based on whether external renderers are present.
        /// TMP continues to run its layout engine; only the draw pass is suppressed.
        /// </summary>
        public void UpdateVisibility(
            bool hasExternalRenderers,
            bool preserveWorldMeshRenderer = false)
        {
            if (_tmpText == null) return;
            if (_visibilityStateInitialized &&
                _lastHasExternalRenderers == hasExternalRenderers &&
                _lastPreserveWorldMeshRenderer == preserveWorldMeshRenderer)
                return;

            _visibilityStateInitialized = true;
            _lastHasExternalRenderers = hasExternalRenderers;
            _lastPreserveWorldMeshRenderer = preserveWorldMeshRenderer;

            // Complete internal replacements use TextStudioTMPSourceVisibility so suppression remains transient across scene saves. This path owns only external renderer substitution.
            bool suppressSource = hasExternalRenderers;
            bool preserveSourceWorldRenderer =
                hasExternalRenderers &&
                preserveWorldMeshRenderer;
            MeshRenderer mr = _hub.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (preserveSourceWorldRenderer)
                {
                    if (_meshRendererAutoHidden)
                        mr.enabled = true;
                    _meshRendererAutoHidden = false;
                }
                else if (suppressSource && mr.enabled)
                {
                    mr.enabled = false;
                    _meshRendererAutoHidden = true;
                }
                else if (!suppressSource && _meshRendererAutoHidden)
                {
                    mr.enabled = true;
                    _meshRendererAutoHidden = false;
                }
            }

            CanvasRenderer cr = _hub.GetComponent<CanvasRenderer>();
            if (cr != null)
            {
                if (suppressSource && !cr.cull)
                {
                    cr.cull = true;
                    _canvasRendererAutoCulled = true;
                }
                else if (!suppressSource && _canvasRendererAutoCulled)
                {
                    cr.cull = false;
                    _canvasRendererAutoCulled = false;
                }
            }
        }

        void SetInternalReplacementVisibility(bool active)
        {
            _replacementSourceVisibility.Synchronize(_tmpText, active);
            if (_hasInternalReplacement == active)
                return;
            _hasInternalReplacement = active;
            _visibilityStateInitialized = false;
            UpdateVisibility(
                _lastHasExternalRenderers,
                _lastPreserveWorldMeshRenderer);
        }

        /// <summary>
        /// Restore any TMP renderers we auto-hid. Called on hub disable so disabling
        /// Text Studio doesn't leave TMP silently invisible.
        /// </summary>
        public void RestoreVisibility()
        {
            _hasPlainFrame = false;
            _replacementSourceVisibility.Restore();
            ReleasePassThroughVisibility();
            if (_tmpText == null) return;
            _visibilityStateInitialized = false;
            if (_meshRendererAutoHidden)
            {
                MeshRenderer mr = _hub.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = true;
                _meshRendererAutoHidden = false;
            }
            if (_canvasRendererAutoCulled)
            {
                CanvasRenderer cr = _hub.GetComponent<CanvasRenderer>();
                if (cr != null) cr.cull = false;
                _canvasRendererAutoCulled = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TMP event subscription
        // ─────────────────────────────────────────────────────────────────────

        public void Subscribe()
        {
            if (_subscribedToChanged) return;
            _subscribedToChanged = true;
            SyncTextChangedRegistration();
            // Material events match by referenced material, so every subscribed bridge receives them.
            TMPro_EventManager.MATERIAL_PROPERTY_EVENT.Add(HandleTMPMaterialPropertyChanged);
        }

        public void Unsubscribe()
        {
            if (!_subscribedToChanged) return;
            _subscribedToChanged = false;
            SyncTextChangedRegistration();
            TMPro_EventManager.MATERIAL_PROPERTY_EVENT.Remove(HandleTMPMaterialPropertyChanged);
        }

        void HandleTMPTextChanged(UnityEngine.Object obj)
        {
            if (_tmpText == null || !ReferenceEquals(obj, _tmpText)) return;
            _lastSeenFontAsset = _tmpText.font;
            _hasFreshMeshForLayout = false;
            if (_suppressRegen) return;
            _frameApplier.InvalidateSubmittedMesh();
            bool layoutChanged =
                _materialEffectAdapter.RequiresRefresh(_tmpText) ||
                !_hasLayoutFingerprint ||
                _layoutFingerprint != ComputeLayoutFingerprint(_tmpText);
            OnRegenerated?.Invoke(layoutChanged, true);
        }

        void HandleTMPMaterialPropertyChanged(bool changed, Material material)
        {
            if (changed)
            {
                _hasFreshMeshForLayout = false;
                // An edited source material may stop or start drawing the TextMeshPro route.
                _materialEffectAdapter.InvalidateTextMeshProSupport();
            }
            if (!changed || _suppressRegen ||
                !_materialEffectAdapter.ReferencesSourceMaterial(material))
            {
                return;
            }

            OnRegenerated?.Invoke(true, false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cleanup
        // ─────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            DisposeRetainedReactionOutputs();
            ResetColorSources();
            DisposeHeroCompositor();
            SetInternalReplacementVisibility(false);
            ReleasePassThroughVisibility();
            SetLiteralMarkupRendering(false);
            RestoreTransientMaterialState();
            RestoreWorldShadowState();
            DisposeDecorations();
            DisposeStyleDecorations();
            DisposeWritingRenderer();
            RestoreTMPRendering();
            _frameApplier.Clear();
            _layoutValid = false;
            _hasFreshMeshForLayout = false;
            _hasLayoutFingerprint = false;
            _skippedVisualFrameWhileSuppressed = false;
            _lastSeenFontAsset = null;
        }

        void DisposeHeroCompositor()
        {
            _heroCompositor?.Dispose();
            _heroCompositor = null;
        }

        void DisposeOrderedRenderer()
        {
            _orderedRenderer?.Dispose();
            _orderedRenderer = null;
        }

        void UpdateWorldShadowState(CompiledTextStudio2DStyle style)
        {
            bool enable = style != null &&
                          style.WorldShadowMode ==
                          TextStudio2DWorldShadowMode.AlphaClipped &&
                          !(_appearancePlanAdapter.Plan != null ? _appearancePlanAdapter.UsesHero : _appearanceAdapter.HasHeroProgram) &&
                          _tmpText is TextMeshPro;
            if (!enable)
            {
                RestoreWorldShadowState();
                return;
            }

            MeshRenderer primary = _hub.GetComponent<MeshRenderer>();
            _tmpText.GetComponentsInChildren(true, _worldShadowSubMeshes);
            // TMP can destroy a sub-mesh component but keep its object and renderer, and nested texts own their own
            // sub-meshes. Only the primary renderer and this text's current sub-mesh renderers stay forced on.
            for (int index = _worldShadowRenderers.Count - 1; index >= 0; index--)
            {
                MeshRenderer renderer = _worldShadowRenderers[index];
                if (renderer != null)
                {
                    if (renderer == primary || IsOwnedWorldShadowSubMeshRenderer(renderer))
                        continue;
                    renderer.shadowCastingMode = _worldShadowSourceModes[index];
                }
                _worldShadowRenderers.RemoveAt(index);
                _worldShadowSourceModes.RemoveAt(index);
            }

            TrackWorldShadowRenderer(primary);
            for (int index = 0; index < _worldShadowSubMeshes.Count; index++)
            {
                TMP_SubMesh subMesh = _worldShadowSubMeshes[index];
                if (subMesh != null && subMesh.textComponent == _tmpText)
                    TrackWorldShadowRenderer(subMesh.GetComponent<MeshRenderer>());
            }
            _worldShadowSubMeshes.Clear();
        }

        bool IsOwnedWorldShadowSubMeshRenderer(MeshRenderer renderer)
        {
            for (int index = 0; index < _worldShadowSubMeshes.Count; index++)
            {
                TMP_SubMesh subMesh = _worldShadowSubMeshes[index];
                if (subMesh != null && subMesh.textComponent == _tmpText &&
                    subMesh.GetComponent<MeshRenderer>() == renderer)
                    return true;
            }
            return false;
        }

        void TrackWorldShadowRenderer(MeshRenderer renderer)
        {
            if (renderer == null)
                return;
            int trackedIndex = _worldShadowRenderers.IndexOf(renderer);
            if (trackedIndex < 0)
            {
                _worldShadowRenderers.Add(renderer);
                _worldShadowSourceModes.Add(renderer.shadowCastingMode);
            }
            if (renderer.shadowCastingMode != ShadowCastingMode.On)
                renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        internal void RestoreWorldShadowState()
        {
            // Renderers and their source modes are tracked in pairs, so text that forces no shadows restores nothing every frame.
            if (_worldShadowRenderers.Count == 0)
                return;
            for (int index = 0; index < _worldShadowRenderers.Count; index++)
            {
                MeshRenderer renderer = _worldShadowRenderers[index];
                if (renderer != null)
                    renderer.shadowCastingMode = _worldShadowSourceModes[index];
            }
            _worldShadowRenderers.Clear();
            _worldShadowSourceModes.Clear();
        }

        void DisposeWritingRenderer()
        {
            _writingRenderer?.Dispose();
            _writingRenderer = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Safe ForceMeshUpdate that handles destroyed internal mesh during domain
        /// reload or teardown. Checks mesh availability before calling to avoid
        /// exception-based flow control.
        /// </summary>
        static bool TryForceMeshUpdate(
            TMP_Text tmp,
            bool ignoreActiveState = false,
            bool forceTextReparsing = false)
        {
            if (tmp == null) return false;
            // Forcing an inactive hierarchy can make TMP create submesh children while Unity is
            // deactivating or unloading their parent, which Unity rejects. Component-only disable
            // keeps the hierarchy active and can still restore the source mesh immediately.
            if (ignoreActiveState && !tmp.gameObject.activeInHierarchy) return false;
            // TMP's mesh can be destroyed while the component is still alive
            // (domain reload, scene teardown). Check before calling to avoid
            // MissingReferenceException in the common path.
            if (tmp.mesh == null) return false;
            try
            {
                tmp.ForceMeshUpdate(ignoreActiveState, forceTextReparsing);
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        void RestoreTMPRendering()
        {
            if (_tmpText == null || _tmpText.font == null)
                return;
            // A disabled TMP component, such as one torn down earlier in its destroyed GameObject, rebuilds its source
            // mesh when it is enabled again, so regenerating it here was discarded work for every destroyed label.
            if (!_tmpText.isActiveAndEnabled)
                return;

            _suppressRegen = true;
            try
            {
                TryForceMeshUpdate(
                    _tmpText,
                    ignoreActiveState: true,
                    forceTextReparsing: true);
            }
            finally
            {
                _suppressRegen = false;
            }
        }

        bool SuppressedForceMeshUpdate()
        {
            _suppressRegen = true;
            try { return TryForceMeshUpdate(_tmpText); }
            finally { _suppressRegen = false; }
        }

        internal static int ComputeLayoutFingerprint(TMP_Text tmpText)
        {
            if (tmpText == null)
                return 0;

            // RunLayout accumulates the same steps while it reads each record; both must combine them in this order.
            TMP_TextInfo textInfo = tmpText.textInfo;
            int characterCount = textInfo != null ? textInfo.characterCount : 0;
            int chain = LayoutFingerprintSeed;
            for (int i = 0; i < characterCount; i++)
                chain = AddLayoutFingerprintCharacter(chain, textInfo, in textInfo.characterInfo[i]);
            return FinishLayoutFingerprint(tmpText, characterCount, chain);
        }

        static int CombineHash(int current, int value)
        {
            return unchecked(current * 31 + value);
        }

        bool SetTMPTextFromTextStudio(string target)
        {
            if (_tmpText == null) return false;
            target ??= string.Empty;
            if (_tmpText.text == target)
            {
                _lastSeenText = target;
                ClearPendingGeneratedTextWriteIfMatched(target);
                return false;
            }

            _hasPendingGeneratedTextWrite = true;
            _pendingGeneratedTextWrite = target;
            _hasFreshMeshForLayout = false;
            if (target.Length != 0)
                SizeEmptyMeshToText(_tmpText, target);
            // The property defers parsing to the mesh update. SetText(string) also filled TMP's character arrays at once,
            // which the following regeneration filled again.
            _isWritingGeneratedText = true;
            try
            {
                _tmpText.text = target;
            }
            finally
            {
                _isWritingGeneratedText = false;
            }
            _lastSeenText = target;
            return true;
        }

        // TMP sizes a mesh that never held text to its first text but grows a used mesh to the next power of two, so a
        // label first generated empty kept up to twice the vertices of its text and uploaded them every frame. Resizing
        // the one-quad empty allocation in place gives it the capacity of its first text. Releasing the allocation instead
        // made TMP clear and rebuild the mesh, which grew the player working set by megabytes per label.
        static void SizeEmptyMeshToText(TMP_Text tmpText, string text)
        {
            TMP_TextInfo textInfo = tmpText.textInfo;
            if (textInfo == null || textInfo.characterCount != 0 || textInfo.meshInfo == null ||
                textInfo.meshInfo.Length == 0)
            {
                return;
            }

            ref TMP_MeshInfo meshInfo = ref textInfo.meshInfo[0];
            if (meshInfo.vertices == null || meshInfo.vertices.Length > 4 || meshInfo.mesh == null)
                return;

            // Count visible characters, as TMP does: whitespace, zero-width spaces and the second half of a surrogate
            // pair do not need quads. An underestimate still leaves TMP's normal mesh growth in place.
            int quadCount = 0;
            bool richText = tmpText.richText;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (richText && character == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i)
                    {
                        i = close;
                        continue;
                    }
                }
                if (character == ' ' || character == '\t' || character == '\n' || character == '\v' ||
                    character == '\r' || character == '\u200B' || character == '\u2028' || character == '\u2029' ||
                    char.IsLowSurrogate(character) && i > 0 && char.IsHighSurrogate(text[i - 1]))
                {
                    continue;
                }
                quadCount++;
            }

            // Like TMP, grow only when the allocation cannot hold the text, to TMP's first-allocation size.
            if (quadCount > meshInfo.vertices.Length / 4)
                meshInfo.ResizeMeshInfo(quadCount + 1);
        }

        void ClearPendingGeneratedTextWriteIfMatched(string text)
        {
            if (!_hasPendingGeneratedTextWrite)
                return;

            if (string.Equals(text, _pendingGeneratedTextWrite, StringComparison.Ordinal))
                ClearPendingGeneratedTextWrite();
        }

        void ClearPendingGeneratedTextWrite()
        {
            _hasPendingGeneratedTextWrite = false;
            _pendingGeneratedTextWrite = string.Empty;
        }

        static float ComputeAutoFitGlyphMargin(
            TMP_TextInfo textInfo,
            TextStudioCompiledPlan plan,
            TMP_FontAsset fontAsset,
            FontStyles globalStyle,
            in TMP_CharacterInfo character,
            int characterIndex,
            Vector3 sourceCenter)
        {
            float margin = 0f;
            if (character.isVisible && IsCharacterVertexRangeValid(textInfo, in character))
            {
                Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                int vertexIndex = character.vertexIndex;
                for (int v = 0; v < 4; v++)
                    margin = Mathf.Max(margin, Mathf.Abs(vertices[vertexIndex + v].y - sourceCenter.y));
            }

            if (fontAsset == null)
                return margin;

            DecorationBits bits = GetDecorationBits(plan, globalStyle, in character, characterIndex);
            if (bits == DecorationBits.None)
                return margin;

            float charScale = Mathf.Max(0.0001f, character.scale);
            float underlineCenter = character.baseLine + fontAsset.faceInfo.underlineOffset * charScale;
            float underlineThickness = Mathf.Max(0.01f, fontAsset.faceInfo.underlineThickness * charScale);

            if ((bits & DecorationBits.Underline) != 0)
                margin = IncludeDecorationBandMargin(margin, sourceCenter.y, underlineCenter, underlineThickness);

            if ((bits & DecorationBits.Strikethrough) != 0)
            {
                float strikeCenter = character.baseLine + fontAsset.faceInfo.strikethroughOffset * charScale;
                float strikeThickness = Mathf.Max(0.01f, fontAsset.faceInfo.strikethroughThickness * charScale);
                margin = IncludeDecorationBandMargin(margin, sourceCenter.y, strikeCenter, strikeThickness);
            }

            if ((bits & DecorationBits.DoubleUnderline) != 0)
            {
                float doubleUnderlineGap = underlineThickness * 1.25f;
                float secondCenter = underlineCenter - (underlineThickness + doubleUnderlineGap);
                margin = IncludeDecorationBandMargin(margin, sourceCenter.y, underlineCenter, underlineThickness);
                margin = IncludeDecorationBandMargin(margin, sourceCenter.y, secondCenter, underlineThickness);
            }

            if ((bits & DecorationBits.Overline) != 0)
            {
                float overlineCenter = character.baseLine + fontAsset.faceInfo.ascentLine * charScale + underlineThickness * 0.5f;
                margin = IncludeDecorationBandMargin(margin, sourceCenter.y, overlineCenter, underlineThickness);
            }

            return margin;
        }

        static DecorationBits GetDecorationBits(
            TextStudioCompiledPlan plan,
            FontStyles globalStyle,
            in TMP_CharacterInfo character,
            int characterIndex)
        {
            DecorationBits bits = DecorationBits.None;
            FontStyles style = character.style | globalStyle;
            if ((style & FontStyles.Underline) != 0) bits |= DecorationBits.Underline;
            if ((style & FontStyles.Strikethrough) != 0) bits |= DecorationBits.Strikethrough;

            DecorationBits[] planDecorations = plan != null ? plan.CharDecorations : null;
            if (planDecorations != null && characterIndex < planDecorations.Length)
                bits |= planDecorations[characterIndex];

            return bits;
        }

        static float IncludeDecorationBandMargin(float current, float sourceCenterY, float bandCenterY, float bandThickness)
        {
            float halfThickness = Mathf.Max(0.01f, bandThickness) * 0.5f;
            return Mathf.Max(current, Mathf.Abs(bandCenterY - sourceCenterY) + halfThickness);
        }

        static bool IsCharacterVertexRangeValid(TMP_TextInfo textInfo, in TMP_CharacterInfo character)
        {
            if (textInfo == null || textInfo.meshInfo == null)
                return false;
            if (character.materialReferenceIndex < 0 || character.materialReferenceIndex >= textInfo.meshInfo.Length)
                return false;

            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            return HasQuad(vertices, character.vertexIndex);
        }

        static bool HasQuad(Vector3[] values, int vertexIndex)
        {
            return values != null && vertexIndex >= 0 && vertexIndex + 3 < values.Length;
        }

        void DisposeDecorations()
        {
            if (_decorations == null) return;
            _decorations.Dispose();
            _decorations = null;
        }

        void DisposeStyleDecorations()
        {
            if (_styleDecorations == null)
                return;
            _styleDecorations.Dispose();
            _styleDecorations = null;
        }
    }
}
