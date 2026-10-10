using System;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        bool[] _rendererOwnedEffectBindings = Array.Empty<bool>();
        int _rendererOwnedEffectBindingCount;
        uint _rendererEffectExecutionNegotiationVersion;
        // Detached evaluators for bindings without a prebound effect, kept until the animator rebinds effects.
        ITextStudioEffect[] _rendererNegotiationEffects = Array.Empty<ITextStudioEffect>();
        uint _rendererNegotiationEffectGeneration;

        // ─────────────────────────────────────────────────────────────────────
        //  TMP regeneration callback
        // ─────────────────────────────────────────────────────────────────────

        void HandleTMPRegenerated(bool layoutChanged)
        {
            bool pulledText = ProcessTMPTextPull();
            if (layoutChanged || pulledText)
            {
                _runtime.SetDirty(
                    DirtyFlags.Topology |
                    DirtyFlags.Layout |
                    DirtyFlags.Placement |
                    DirtyFlags.Animation |
                    DirtyFlags.Visual);
            }
            else if (IsDrivingTextStudioOutput && !IsRenderingRawExternalOutput)
            {
                _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            }
            else
            {
                return;
            }

            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Frame dispatch
        // ─────────────────────────────────────────────────────────────────────

        bool DispatchFrame(float dt)
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (plan == null) return false;

            bool hasStale = false;
            bool hasActiveExternalRenderer = false;
            bool hasActiveFrameComposers =
                _extensions != null && _extensions.HasActiveFrameComposers;
            bool needsComposedGlyphState =
                TextBackendNeedsComposedGlyphState || hasActiveFrameComposers;
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (!IsUsableRenderer(renderer, ref hasStale)) continue;
                hasActiveExternalRenderer = true;
                if ((renderer.Capabilities & RendererCapabilities.ConsumesRawFrameState) == 0)
                    needsComposedGlyphState = true;
            }

            TextStudioGlyphInstanceBuffer glyphInstances = null;
            if (needsComposedGlyphState)
            {
                glyphInstances = TextStudioGlyphInstanceBufferPool.Acquire();
                glyphInstances.Build(
                    plan,
                    _layout.BasePositions,
                    _placementEngine.Placements,
                    _placementEngine.Count,
                    _placementMode,
                    _runtime.Animator.States,
                    _runtime.Animator.StateCount,
                    _layout.SourceColors,
                    _layout.EffectUnitScales,
                    _runtime.MeshDirty);
            }

            try
            {
                TextStudioFrame frame = new TextStudioFrame(
                    plan: plan,
                    placements: _placementEngine.Placements,
                    placementCount: _placementEngine.Count,
                    basePositions: _layout.BasePositions,
                    placementMode: _placementMode,
                    animated: _runtime.Animator.States,
                    animatedCount: _runtime.Animator.StateCount,
                    dirty: _runtime.MeshDirty,
                    revealProgress: _runtime.Reveal.RevealProgress,
                    globalDirty: _globalDirtyThisTick,
                    glyphStates: glyphInstances?.States,
                    glyphStateCount: glyphInstances?.Count ?? 0,
                    presentationGlyphs: _presentationGlyphs,
                    presentationGlyphCount: _presentationGlyphs != null
                        ? plan.VisibleGlyphCount
                        : 0,
                    deltaTime: dt,
                    time: _time,
                    sourceColors: _layout.SourceColors,
                    sourceColorBounds: _layout.SourceColorBounds,
                    effectUnitScales: _layout.EffectUnitScales,
                    animatedStateIsUniform: _runtime.Animator.EvaluatedStatesAreUniform,
                    renderProperties: _runtime.Animator.RenderProperties,
                    appearanceStyle: ActiveCompiledAppearanceStyle,
                    appearancePlan: ActiveCompiledAppearancePlan,
                    writingStyle: _compiledWritingStyle,
                    transitions: new TextStudioGlyphTransitionView(_runtime.Reveal),
                    // SyncRuntimeConfiguration resolved these settings earlier this tick, and placement-only ticks require them current.
                    eraseWritingOnHide: _hasAppliedRuntimeSettings
                        ? _appliedRuntimeSettings.EraseOnHide
                        : ResolveTypewriterSettings().EraseOnHide,
                    visualGlyphs: _layout.VisualGlyphs,
                    visualCapabilities: _layout.VisualCapabilities,
                    visualVersion: _layout.VisualVersion,
                    visualSourceBounds: _layout.SourceTextBounds,
                    playbackTime: _runtime.Animator.PlaybackTimeForDebug,
                    presentationTime: GetPresentationEvaluationTime(),
                    rendererOwnedEffectBindings: _rendererOwnedEffectBindings,
                    rendererOwnedEffectBindingCount:
                        _rendererOwnedEffectBindingCount,
                    fontSources: _layout.FontSources,
                    fontSourceCount: _layout.FontSourceCount,
                    renderGlyphs: _layout.RenderGlyphs, renderGlyphCount: _layout.RenderGlyphCount, unitToRenderGlyph: _layout.UnitToRenderGlyph,
                    colorSources: _layout.ColorSources, colorSourceCount: _layout.ColorSourceCount,
                    motionPolicy: _motionPolicy, sourceLayoutValidity: _layout.SourceLayoutValidity);

                bool suppressTextBackendOutput =
                    ShouldSuppressTextBackendOutput(hasActiveExternalRenderer);
                UpdateTextBackendVisibility(suppressTextBackendOutput);
                TextStudioFrame textBackendFrame = ComposeFrameForTarget(
                    in frame,
                    TextBackendFrameTarget);
                RenderTextBackend(in textBackendFrame, suppressTextBackendOutput);

                // External renderer components.
                for (int i = 0; i < _renderers.Count; i++)
                {
                    ITextStudioRenderer r = _renderers[i];
                    if (!IsUsableRenderer(r, ref hasStale)) continue;
                    TextStudioFrame rendererFrame = ComposeFrameForTarget(
                        in frame,
                        GetRendererFrameTarget(r));
                    try { r.Render(in rendererFrame); }
                    catch (Exception ex) { Debug.LogException(ex, this); }
                }
                if (hasStale) _renderers.RemoveAll(r => r == null || (r is UnityEngine.Object uo2 && uo2 == null));

                _runtime.MeshDirty.ClearAll();
                return true;
            }
            finally
            {
                if (glyphInstances != null)
                    TextStudioGlyphInstanceBufferPool.Release(glyphInstances);
            }
        }

        TextStudioFrame ComposeFrameForTarget(
            in TextStudioFrame canonicalFrame,
            TextStudioFrameTarget target)
        {
            if (_extensions == null)
                return canonicalFrame;

            try
            {
                return _extensions.TryComposeFrame(
                    in canonicalFrame,
                    in target,
                    in _latestCommittedGlyphChanges,
                    out TextStudioFrame composedFrame)
                    ? composedFrame
                    : canonicalFrame;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                return canonicalFrame;
            }
        }

        bool ShouldSuppressTextBackendOutput(bool hasActiveExternalRenderer)
        {
            if (!hasActiveExternalRenderer)
                return false;
            if (Application.isPlaying || _authoringMode != TextStudioAuthoringMode.Preview)
                return true;
            return _previewStageMode != TextStudioPreviewStageMode.Flat2D &&
                   _previewStageMode != TextStudioPreviewStageMode.Compare;
        }

        void UpdateRendererEffectExecution(
            TextStudioCompiledPlan plan,
            bool renderRawWithoutEffects)
        {
            int bindingCount = plan != null
                ? Mathf.Max(0, plan.EffectBindingCount)
                : 0;
            int clearCount = Mathf.Max(
                _rendererOwnedEffectBindingCount,
                bindingCount);
            if (_rendererOwnedEffectBindings.Length < bindingCount)
            {
                _rendererOwnedEffectBindings = new bool[
                    TextStudioBufferCapacity.ForCount(bindingCount)];
            }
            else if (clearCount > 0)
            {
                Array.Clear(
                    _rendererOwnedEffectBindings,
                    0,
                    Mathf.Min(clearCount, _rendererOwnedEffectBindings.Length));
            }

            _rendererOwnedEffectBindingCount = bindingCount;
            unchecked
            {
                _rendererEffectExecutionNegotiationVersion++;
            }
            if (_rendererEffectExecutionNegotiationVersion == 0u)
                _rendererEffectExecutionNegotiationVersion = 1u;
            // Only an external renderer, a mesh backend or a lens following an influence target can own a binding exactly,
            // so a plain TMP text skips the per-binding negotiation every frame.
            bool canNegotiate = !renderRawWithoutEffects &&
                bindingCount > 0 &&
                plan.EffectBindings != null &&
                (_extensions == null || !_extensions.HasActiveFrameComposers) &&
                (_renderers.Count > 0 || _activeMeshBackend != null || _influenceTarget != null);
            if (canNegotiate)
            {
                TextStudioMotionPolicy motionPolicy =
                    TextStudioMotionSettings.Resolve(_runtime.MotionPolicy);
                for (int bindingIndex = 0;
                     bindingIndex < bindingCount;
                     bindingIndex++)
                {
                    ref EffectBinding binding =
                        ref plan.EffectBindings[bindingIndex];
                    ITextStudioEffect effect = binding.effect ??
                        ResolveRendererNegotiationEffect(bindingIndex, in binding);
                    if (effect == null)
                        continue;

                    TextStudioEffectCapabilities capabilities =
                        AnimationEvaluator.ResolveCapabilities(effect);
                    TextStudioRendererEffectExecutionRequest request =
                        new TextStudioRendererEffectExecutionRequest(
                            plan,
                            bindingIndex,
                            in binding,
                            effect,
                            in capabilities,
                            motionPolicy,
                            _runtime.Reveal.AreAllGlyphsPersistent,
                            _runtime.Animator.PersistentEffectsEnabled,
                            _rendererEffectExecutionNegotiationVersion);
                    _rendererOwnedEffectBindings[bindingIndex] =
                        CanUseRendererExactEffectExecution(in request);
                }
            }

            if (_runtime.Animator.SetRendererDeferredEffects(
                    _rendererOwnedEffectBindings,
                    bindingCount))
            {
                const DirtyFlags executionDirty =
                    DirtyFlags.Animation | DirtyFlags.Visual;
                _runtime.SetDirty(executionDirty);
                _globalDirtyThisTick |= executionDirty;
            }
        }

        ITextStudioEffect ResolveRendererNegotiationEffect(
            int bindingIndex,
            in EffectBinding binding)
        {
            uint generation = _runtime.Animator.EffectBindingGeneration;
            if (_rendererNegotiationEffectGeneration != generation)
            {
                Array.Clear(
                    _rendererNegotiationEffects,
                    0,
                    _rendererNegotiationEffects.Length);
                _rendererNegotiationEffectGeneration = generation;
            }

            if (_rendererNegotiationEffects.Length <= bindingIndex)
            {
                Array.Resize(
                    ref _rendererNegotiationEffects,
                    TextStudioBufferCapacity.ForCount(bindingIndex + 1));
            }

            ITextStudioEffect effect = _rendererNegotiationEffects[bindingIndex];
            if (effect == null &&
                TextStudioEffects.TryCreate(
                    binding.effectId,
                    in binding.parameters,
                    out effect))
            {
                _rendererNegotiationEffects[bindingIndex] = effect;
            }

            return effect;
        }

        internal bool CanUseRendererExactEffectExecution(
            in TextStudioRendererEffectExecutionRequest request)
        {
            TextStudioRenderDomain effectDomains =
                request.Capabilities.SupportedRenderDomains;
            if (effectDomains == TextStudioRenderDomain.None ||
                request.Effect == null ||
                _extensions != null && _extensions.HasActiveFrameComposers)
            {
                return false;
            }

            bool hasStale = false;
            bool hasActiveExternalRenderer = false;
            bool hasRelevantRenderer = false;
            bool allRelevantRenderersAccepted = true;
            for (int index = 0; index < _renderers.Count; index++)
            {
                ITextStudioRenderer renderer = _renderers[index];
                if (!IsUsableRenderer(renderer, ref hasStale))
                    continue;

                hasActiveExternalRenderer = true;
                TextStudioRenderDomain rendererDomain =
                    TextStudioRenderDomain.All;
                try
                {
                    if (renderer is ITextStudioRenderDomainProvider provider)
                    {
                        rendererDomain = provider.RenderDomain;
                        if (rendererDomain == TextStudioRenderDomain.None)
                            rendererDomain = TextStudioRenderDomain.All;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                    allRelevantRenderersAccepted = false;
                    continue;
                }

                if ((rendererDomain & effectDomains) ==
                    TextStudioRenderDomain.None)
                {
                    continue;
                }

                hasRelevantRenderer = true;
                if (renderer is not ITextStudioRendererEffectExecution execution)
                {
                    allRelevantRenderersAccepted = false;
                    continue;
                }

                try
                {
                    if (execution.GetEffectExecution(in request) !=
                        TextStudioRendererEffectExecution.RendererExact)
                    {
                        allRelevantRenderersAccepted = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                    allRelevantRenderersAccepted = false;
                }
            }

            if (hasStale)
            {
                _renderers.RemoveAll(
                    renderer => renderer == null ||
                    renderer is UnityEngine.Object unityObject &&
                    unityObject == null);
            }

            bool textBackendVisible =
                !ShouldSuppressTextBackendOutput(hasActiveExternalRenderer);
            if (textBackendVisible &&
                (effectDomains & TextStudioRenderDomain.FlatText) != 0)
            {
                hasRelevantRenderer = true;
                try
                {
                    TextStudioRendererEffectExecution backendExecution;
                    if (_activeMeshBackend != null)
                    {
                        backendExecution = _activeMeshBackend is
                            ITextStudioRendererEffectExecution exactBackend
                                ? exactBackend.GetEffectExecution(in request)
                                : TextStudioRendererEffectExecution.Cpu;
                    }
                    else
                    {
                        backendExecution = TmpBridge.GetEffectExecution(in request);
                    }

                    if (backendExecution !=
                        TextStudioRendererEffectExecution.RendererExact)
                    {
                        allRelevantRenderersAccepted = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                    allRelevantRenderersAccepted = false;
                }
            }

            return hasRelevantRenderer && allRelevantRenderersAccepted;
        }

        void ApplyPreviewStageSettings()
        {
            bool useAuthoredStage = !Application.isPlaying &&
                _authoringMode == TextStudioAuthoringMode.Preview;
            TextStudioPreviewStageSettings settings = PreviewStageSettings;
            for (int i = _previewStageProviders.Count - 1; i >= 0; i--)
            {
                ITextStudioPreviewStageProvider provider = _previewStageProviders[i];
                if (provider == null ||
                    (provider is UnityEngine.Object unityObject && unityObject == null))
                {
                    _previewStageProviders.RemoveAt(i);
                    continue;
                }

                try
                {
                    if (useAuthoredStage)
                        provider.ConfigurePreviewStage(in settings);
                    else
                        provider.ResetPreviewStage();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

            UpdateTextBackendVisibility(
                ShouldSuppressTextBackendOutput(HasAvailableExternalRenderer()));
        }

        bool HasPrimaryOutputOwnerReusingBackendComponents()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] is ITextStudioPrimaryOutputOwner owner &&
                    owner.PrimaryOutputOwnership ==
                    TextStudioPrimaryOutputOwnership.ReuseBackendComponents)
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsUsableRenderer(ITextStudioRenderer renderer, ref bool hasStale)
        {
            if (renderer == null)
            {
                hasStale = true;
                return false;
            }

            if (renderer is UnityEngine.Object unityObject && unityObject == null)
            {
                hasStale = true;
                return false;
            }

            if (!renderer.IsAvailable) return false;
            if (renderer is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
            return true;
        }

        bool HasAvailableExternalRenderer()
        {
            bool hasStale = false;
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (IsUsableRenderer(_renderers[i], ref hasStale))
                    return true;
            }

            if (hasStale)
                _renderers.RemoveAll(r => r == null || (r is UnityEngine.Object uo && uo == null));
            return false;
        }

        // Discovery pre-hides the backend for enabled renderers that have not run OnEnable yet, but a disabled renderer component no longer owns the output.
        bool HasEnabledExternalRenderer()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (renderer == null ||
                    (renderer is UnityEngine.Object unityObject && unityObject == null))
                {
                    continue;
                }

                if (renderer is Behaviour behaviour && !behaviour.enabled)
                    continue;
                return true;
            }
            return false;
        }

        bool PlanRequiresDrivenText(TextStudioCompiledPlan plan)
        {
            if (_placementMode != PlacementMode.Linear || _placementRotation.HasEffect || _textChangeLayoutDuration > 0f)
                return true;

            TMP_Text tmpText = UsesCustomTextBackend ? null : TmpBridge.TMPText;
            if (tmpText != null &&
                (tmpText.fontStyle & (FontStyles.Underline | FontStyles.Strikethrough)) != 0)
                return true;

            if (plan == null)
                return false;

            if (plan.CharDecorations != null)
                return true;

            string displayText = plan.DisplayText ?? plan.ResolvedText ?? string.Empty;
            string markup = _markup ?? string.Empty;
            return !string.Equals(displayText, markup, StringComparison.Ordinal);
        }

        void AddExternalTextSource(ITextStudioExternalTextSource source)
        {
            if (!IsUsableExternalTextSource(source)) return;
            if (_externalSources.Contains(source)) return;
            _externalSources.Add(source);
        }

        static bool IsUsableExternalTextSource(ITextStudioExternalTextSource source)
        {
            if (source == null) return false;
            if (source is UnityEngine.Object unityObject && unityObject == null) return false;
            if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
            return true;
        }

        void AddExternalEffectSource(ITextStudioExternalEffectSource source)
        {
            if (!IsUsableExternalEffectSource(source) || _externalEffectSources.Contains(source))
                return;
            _externalEffectSources.Add(source);
        }

        static bool IsUsableExternalEffectSource(ITextStudioExternalEffectSource source)
        {
            if (source == null)
                return false;
            return source is not UnityEngine.Object unityObject || unityObject != null;
        }

        bool IsExternalEffectSourceActive(ITextStudioExternalEffectSource source)
        {
            if (!IsUsableExternalEffectSource(source))
                return false;
            if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;
            try
            {
                return source.IsActive;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        bool HasActiveExternalEffectSources()
        {
            for (int i = _externalEffectSources.Count - 1; i >= 0; i--)
            {
                ITextStudioExternalEffectSource source = _externalEffectSources[i];
                if (!IsUsableExternalEffectSource(source))
                {
                    _externalEffectSources.RemoveAt(i);
                    continue;
                }

                if (IsExternalEffectSourceActive(source))
                    return true;
            }

            return false;
        }

        void RebuildExtensionTagProcessors(MonoBehaviour[] components)
        {
            for (int i = 0; i < _extensionProcessors.Count; i++)
                _runtime.UnregisterExtensionTagProcessor(_extensionProcessors[i]);
            _extensionProcessors.Clear();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ITextStudioExtensionTagProcessor processor)
                    AddExtensionTagProcessor(processor);
            }

            for (int i = 0; i < _registeredExtensionProcessors.Count; i++)
                AddExtensionTagProcessor(_registeredExtensionProcessors[i]);
        }

        void AddExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            if (!IsUsableExtensionTagProcessor(processor)) return;
            if (_extensionProcessors.Contains(processor)) return;
            _extensionProcessors.Add(processor);
            _runtime.RegisterExtensionTagProcessor(processor);
        }

        static bool IsUsableExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            if (processor == null) return false;
            if (processor is UnityEngine.Object unityObject && unityObject == null) return false;
            if (processor is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
            return true;
        }
    }
}
