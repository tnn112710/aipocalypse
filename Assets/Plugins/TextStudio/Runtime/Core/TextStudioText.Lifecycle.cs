using System;
using System.Collections.Generic;
using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Reset()
        {
            _appearanceSource = TextStudioAppearanceSource.Inline;
            TextStudioDefaultSettings defaultSettings = TextStudioDefaultSettings.Instance;
            if (defaultSettings != null)
            {
                _revealMode = defaultSettings.defaultRevealMode;
                _placementMode = defaultSettings.defaultPlacementMode;
            }

            bool autoAdd = defaultSettings == null || defaultSettings.autoAddTMPOnHubReset;
            if (autoAdd && !HasOptionalTextBackendComponent())
                TmpBridge.EnsurePresent();
        }

        void OnEnable()
        {
            _hasEnabledLifecycle = true;
            _isShuttingDown = false;
            _isIdleSleeping = false;
            TextStudioHubEvents.Register(this);
            TextStudioRegistry.EnsureInitialized();
            HostTime.Reset(ref _lastHostTime, _timing);
            _actionRunner ??= new TextStudioMonoBehaviourActionRunner(this, GetActionDeltaTime);
            SyncActionRunner();

            MonoBehaviour[] localComponents = GetComponents<MonoBehaviour>();
            InitializeTextBackendIntegration(localComponents);

            string backendText = GetPrimaryTextBackendText();
            if (string.IsNullOrEmpty(_markup) && !string.IsNullOrEmpty(backendText))
                SetCanonicalMarkup(backendText);
            else
                SetCanonicalMarkup(_markup);

            SyncRuntimeConfiguration();
            ApplyDefaultEffects();
            EnsureAppearanceStyleCurrent(true);
            EnsureWritingStyleCurrent(true);
            if (ShouldMirrorRuntimeTypewriterToInlineSettings())
            {
                _revealMode = _runtime.RevealMode;
                _revealSpeedMultiplier = _runtime.RevealSpeedMultiplier;
                _revealAppearanceDuration = _runtime.RevealAppearanceDuration;
                _revealDisappearanceDuration = _runtime.RevealDisappearanceDuration;
            }

            // Enabling commits the latest requested markup, including requests made while disabled.
            ClearPendingTextChangeState();
            _runtime.SetText(_markup, false);
            SubscribeRuntimeEvents();
            SyncReactions();

            ApplyPlacementStrategy();
            SyncCurveProfileSubscription();
            DiscoverRenderersAndProviders(localComponents);

            _runtime.SetDirty(DirtyFlags.All);

            TextStudioEditorHooks.NotifyHubEnabled(this);
            NotifyWorldBatchGroups();
            if (Application.isPlaying)
            {
                Preview.PrepareForPlayMode();
                TextStudioHubScheduler.Register(this);
            }
            bool? initialReveal = _revealOnFirstEnable;
            _revealOnFirstEnable = null;
            if (initialReveal ?? ShouldStartTypewriterOnEnable())
                StartRevealInternal(resetProgress: true);
            else if (initialReveal == false)
                SkipReveal();
        }

        internal void PreparePreviewForPlayMode()
        {
            // Fast Play Mode entry can retain a hub without calling OnEnable.
            TextStudioHubScheduler.Register(this);
            if (_preview == null || !_preview.PrepareForPlayMode())
                return;

            SyncActionRunner();
            if (ShouldStartTypewriterOnEnable())
                StartRevealInternal(resetProgress: true);
        }

        void OnDisable()
        {
            TextStudioHubScheduler.Unregister(this);
            _isShuttingDown = true;
            _isIdleSleeping = false;
            _runtime.ReleaseReactions(TextStudioReactionCancelReason.HostDisabled);
            _reactionCanvasParticles?.Dispose();
            _reactionCanvasParticles = null;
            _reactionCompositeCapture?.ReleaseAll();
            TextStudioHubEvents.Unregister(this);
            if (_subscribedCurveProfile != null)
            {
                _subscribedCurveProfile.Changed -= HandleCurveProfileChanged;
                _subscribedCurveProfile = null;
            }
            ClearPendingTextChangeState();
            _layoutInterpolation = null;
            TextStudioEditorHooks.NotifyHubDisabled(this);
            ReleaseWorldBatchState();

            _actionRunner?.Clear();
            UnsubscribeRuntimeEvents();
            _runtime.SetActionRunner(null);
            _editModeActionRunner.Clear();

            for (int i = 0; i < _extensionProcessors.Count; i++)
                _runtime.UnregisterExtensionTagProcessor(_extensionProcessors[i]);

            for (int i = 0; i < _renderers.Count; i++)
            {
                try { _renderers[i]?.Detach(); } catch (Exception ex) { Debug.LogException(ex, this); }
            }
            for (int i = 0; i < _previewStageProviders.Count; i++)
            {
                try { _previewStageProviders[i]?.ResetPreviewStage(); }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
            _renderers.Clear();
            _previewStageProviders.Clear();
            _extensionProcessors.Clear();
            _localActions.Clear();
            _runtime.SetLocalActions(null);
            ClearDiscoveredExternalSources();
            _externalEffectSources.Clear();

            DisposeTextBackendIntegration();
        }

        void SubscribeRuntimeEvents()
        {
            UnsubscribeRuntimeEvents();
            _runtime.TextSet += HandleRuntimeTextSet;
            _runtime.PlanChanged += HandleRuntimePlanChanged;
            _runtime.GlyphRevealed += HandleRuntimeGlyphRevealed;
            _runtime.GlyphDisappeared += HandleRuntimeGlyphDisappeared;
            _runtime.TextUnitRevealed += HandleRuntimeTextUnitRevealed;
            _runtime.MarkerReached += HandleRuntimeMarkerReached;
            _runtime.ExtensionTagsChanged += HandleRuntimeExtensionTagsChanged;
            _runtime.ExtensionTagReached += HandleRuntimeExtensionTagReached;
            _runtime.WaitStarted += HandleRuntimeWaitStarted;
            _runtime.WaitFinished += HandleRuntimeWaitFinished;
            _runtime.TypewriterStarted += HandleRuntimeTypewriterStarted;
            _runtime.ShowComplete += HandleRuntimeShowComplete;
            _runtime.DisappearComplete += HandleRuntimeDisappearComplete;
            _runtime.RevealReset += HandleRuntimeRevealReset;
        }

        void UnsubscribeRuntimeEvents()
        {
            _runtime.TextSet -= HandleRuntimeTextSet;
            _runtime.PlanChanged -= HandleRuntimePlanChanged;
            _runtime.GlyphRevealed -= HandleRuntimeGlyphRevealed;
            _runtime.GlyphDisappeared -= HandleRuntimeGlyphDisappeared;
            _runtime.TextUnitRevealed -= HandleRuntimeTextUnitRevealed;
            _runtime.MarkerReached -= HandleRuntimeMarkerReached;
            _runtime.ExtensionTagsChanged -= HandleRuntimeExtensionTagsChanged;
            _runtime.ExtensionTagReached -= HandleRuntimeExtensionTagReached;
            _runtime.WaitStarted -= HandleRuntimeWaitStarted;
            _runtime.WaitFinished -= HandleRuntimeWaitFinished;
            _runtime.TypewriterStarted -= HandleRuntimeTypewriterStarted;
            _runtime.ShowComplete -= HandleRuntimeShowComplete;
            _runtime.DisappearComplete -= HandleRuntimeDisappearComplete;
            _runtime.RevealReset -= HandleRuntimeRevealReset;
        }

        void ClearDiscoveredExternalSources()
        {
            _externalSources.Clear();
            _hasExternalTextSource = false;
        }

        bool _isValidating;
        bool _hasEnabledLifecycle;
        bool? _revealOnFirstEnable;

        void RecordInitialRevealRequest(bool reveal)
        {
            // Other components can issue commands from Awake before this hub enables.
            // Initial preview preparation must not discard their explicit playback intent.
            if (Application.isPlaying && !_hasEnabledLifecycle)
                _revealOnFirstEnable = reveal;
        }

        void OnValidate()
        {
            if (_isValidating)
                return;

            unchecked { _reactionSerializationRevision++; }
            if (_reactionSerializationRevision == 0)
                _reactionSerializationRevision = 1;
            _isValidating = true;
            try
            {
                ValidateConfiguration();
            }
            finally
            {
                _isValidating = false;
            }
        }

        void ValidateConfiguration()
        {
            _inlineAppearanceSettings.Validate();
            TmpBridge.FindTMP();
            _revealSpeedMultiplier = Mathf.Max(0.01f, _revealSpeedMultiplier);
            _revealAppearanceDuration = Mathf.Max(0.01f, _revealAppearanceDuration);
            _revealDisappearanceDuration = Mathf.Max(0.01f, _revealDisappearanceDuration);
            _loopRevealDelay = Mathf.Max(0f, _loopRevealDelay);
            _previewOrbitAngles = new Vector2(
                Mathf.Repeat(_previewOrbitAngles.x + 180f, 360f) - 180f,
                Mathf.Clamp(_previewOrbitAngles.y, -85f, 85f));
            _previewCompareSpacing = Mathf.Max(0f, _previewCompareSpacing);
            _inlineDefaultEffects ??= string.Empty;
            _inlineDefaultRevealEffects ??= string.Empty;
            _inlineDefaultActiveEffects ??= string.Empty;
            _inlineDefaultHideEffects ??= string.Empty;
            SyncRuntimeConfiguration();
            ApplyDefaultEffects();
            EnsureAppearanceStyleCurrent();
            EnsureWritingStyleCurrent();

            if (_runtime.Markup != _markup)
            {
                SetCanonicalMarkup(_markup);
                _runtime.SetText(_markup, false);
            }
            ApplyPlacementStrategy();
            _curvePlacement?.InvalidateSelection();
            SyncCurveProfileSubscription();
            ApplyPreviewStageSettings();
            IPresentationGlyphProvider validatedGlyphProvider =
                _presentationProvider as IPresentationGlyphProvider;
            if (!ReferenceEquals(_glyphProvider, validatedGlyphProvider))
            {
                _glyphProvider = validatedGlyphProvider;
                HandleRuntimePlanChanged(_runtime.Plan);
            }
            // OnValidate can run before OnEnable discovery when a scene loads.
            // Keep same-GameObject action tags known before edit-time compilation.
            RebuildLocalActions(true);
            _runtime.SetDirty(DirtyFlags.Style | DirtyFlags.Topology | DirtyFlags.Placement | DirtyFlags.Layout | DirtyFlags.Visual);

            // Deserialization validates before scene-owned effect registrars can enable.
            // Keep live Inspector edits immediate, but let initial output use the normal enable/preview lifecycle.
            if (_hasEnabledLifecycle && !_isShuttingDown)
                SyncValidatedTextImmediate();
        }

        void SyncCurveProfileSubscription()
        {
            TextStudioCurveProfile next = isActiveAndEnabled ? _curvePlacement?.Curve.Profile : null;
            if (_subscribedCurveProfile == next) return;
            if (_subscribedCurveProfile != null)
                _subscribedCurveProfile.Changed -= HandleCurveProfileChanged;
            _subscribedCurveProfile = next;
            if (next != null)
                next.Changed += HandleCurveProfileChanged;
        }

        void HandleCurveProfileChanged(TextStudioCurveProfile profile)
        {
            if (profile != _subscribedCurveProfile) return;
            SchedulePlacementRefresh();
            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        internal void HandleAppearanceStyleAssetChanged(TextStudio2DStyleAsset changedStyle)
        {
            if (!IsAppearanceDependency(changedStyle))
                return;

            EnsureAppearanceStyleCurrent(true);
            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        internal void HandleWritingStyleAssetChanged(TextStudioWritingStyleAsset changedStyle)
        {
            if (!ReferenceEquals(WritingStyle, changedStyle))
                return;
            EnsureWritingStyleCurrent(true);
            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        internal void HandleWritingFontAssetChanged(WritingFontAsset changedFont)
        {
            if (WritingStyle == null ||
                !ReferenceEquals(WritingStyle.FontAsset, changedFont))
            {
                return;
            }
            EnsureWritingStyleCurrent(true);
            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        internal void HandleWritingPhraseAssetChanged(WritingPhraseAsset changedPhrase)
        {
            if (WritingStyle == null ||
                !ReferenceEquals(WritingStyle.PhraseAsset, changedPhrase))
            {
                return;
            }
            EnsureWritingStyleCurrent(true);
            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        bool IsAppearanceDependency(TextStudio2DStyleAsset candidate)
        {
            if (candidate == null)
                return false;
            if (IsAppearanceInvocationDependency(candidate)) return true;

            TextStudio2DStyleAsset current = AppearanceSource == TextStudioAppearanceSource.Inline
                ? (_inlineAppearanceSettings.InheritanceMode == TextStudio2DStyleInheritanceMode.Replace
                    ? null
                    : _inlineAppearanceSettings.BaseStyle)
                : AppearanceStyle;
            int depth = 0;
            while (current != null && depth < 64)
            {
                if (ReferenceEquals(current, candidate))
                    return true;
                if (current.InheritanceMode == TextStudio2DStyleInheritanceMode.Replace)
                    return false;
                current = current.BaseStyle;
                depth++;
            }
            return false;
        }

        int _rendererOutputParentingDepth;

        internal void ParentRendererOutput(Transform output, Transform parent)
        {
            _rendererOutputParentingDepth++;
            try
            {
                output.SetParent(parent, false);
            }
            finally
            {
                _rendererOutputParentingDepth--;
            }
        }

        void OnTransformChildrenChanged()
        {
            // Attaching an owned output does not change the component's source or providers.
            if (_isShuttingDown || !isActiveAndEnabled || _rendererOutputParentingDepth > 0)
                return;

            DiscoverTextBackends();
            DiscoverRenderersAndProviders();
        }

        bool HasOptionalTextBackendComponent()
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ITextStudioTextBackend backend &&
                    IsCompleteTextBackend(backend))
                {
                    return true;
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Renderer / provider discovery
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Rediscovers attached renderers, placement providers, backends, and registered extensions.</summary>
        public void DiscoverRenderersAndProviders()
        {
            DiscoverRenderersAndProviders(GetComponents<MonoBehaviour>());
        }

        void DiscoverRenderersAndProviders(MonoBehaviour[] components)
        {
            if (_isShuttingDown || !isActiveAndEnabled)
                return;

            for (int i = _renderers.Count - 1; i >= 0; i--)
            {
                ITextStudioRenderer existing = _renderers[i];
                bool stillPresent = false;
                for (int j = 0; j < components.Length; j++)
                {
                    if (ReferenceEquals(components[j], existing))
                    {
                        stillPresent = true;
                        break;
                    }
                }
                if (!stillPresent)
                {
                    try { existing?.Detach(); } catch (Exception ex) { Debug.LogException(ex, this); }
                    _renderers.RemoveAt(i);
                }
            }
            for (int i = 0; i < components.Length; i++)
            {
                ITextStudioRenderer r = components[i] as ITextStudioRenderer;
                if (r == null) continue;
                if (_renderers.Contains(r)) continue;
                _renderers.Add(r);
                try { r.Attach(this); } catch (Exception ex) { Debug.LogException(ex, this); }
            }

            _previewStageProviders.Clear();
            for (int i = 0; i < components.Length; i++)
            {
                ITextStudioPreviewStageProvider provider =
                    components[i] as ITextStudioPreviewStageProvider;
                if (provider != null)
                    _previewStageProviders.Add(provider);
            }
            ApplyPreviewStageSettings();

            _placementStrategies.Clear();
            for (int i = 0; i < components.Length; i++)
            {
                IPlacementStrategy placementStrategy =
                    components[i] as IPlacementStrategy;
                if (IsPlacementStrategyAvailable(placementStrategy))
                    _placementStrategies.Add(placementStrategy);
            }
            ApplyPlacementStrategy();

            RebuildLocalActions(components, false);

            _externalSources.Clear();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ITextStudioExternalTextSource source)
                    AddExternalTextSource(source);
            }
            for (int i = 0; i < _registeredExternalSources.Count; i++)
                AddExternalTextSource(_registeredExternalSources[i]);
            _hasExternalTextSource = _externalSources.Count > 0;

            _externalEffectSources.Clear();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ITextStudioExternalEffectSource source)
                    AddExternalEffectSource(source);
            }
            for (int i = 0; i < _registeredExternalEffectSources.Count; i++)
                AddExternalEffectSource(_registeredExternalEffectSources[i]);

            RebuildExtensionTagProcessors(components);

            IPresentationGlyphProvider discoveredGlyphProvider =
                _presentationProvider as IPresentationGlyphProvider;
            if (!ReferenceEquals(_glyphProvider, discoveredGlyphProvider))
            {
                _glyphProvider = discoveredGlyphProvider;
                HandleRuntimePlanChanged(_runtime.Plan);
            }

            UpdateTextBackendVisibility(
                ShouldSuppressTextBackendOutput(HasEnabledExternalRenderer()));

            _runtime.SetDirty(DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void RefreshLocalActionAvailability()
        {
            if (_runtime != null) RebuildLocalActions();
        }

        void RebuildLocalActions(bool includePendingEnable = false)
        {
            RebuildLocalActions(
                GetComponents<MonoBehaviour>(),
                includePendingEnable);
        }

        void RebuildLocalActions(
            MonoBehaviour[] components,
            bool includePendingEnable)
        {
            _localActions.Clear();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ITextStudioAction action)
                    AddLocalAction(action, includePendingEnable);
            }

            _runtime.SetLocalActions(_localActions);
        }

        void AddLocalAction(ITextStudioAction action, bool includePendingEnable = false)
        {
            if (!IsUsableLocalAction(action, includePendingEnable)) return;
            if (_localActions.Contains(action)) return;
            _localActions.Add(action);
        }

        static bool IsUsableLocalAction(ITextStudioAction action, bool includePendingEnable)
        {
            if (action == null) return false;
            if (action is UnityEngine.Object unityObject && unityObject == null) return false;
            if (action is Behaviour behaviour)
            {
                if (includePendingEnable)
                    return behaviour.enabled && behaviour.gameObject != null && behaviour.gameObject.activeSelf;

                return behaviour.isActiveAndEnabled;
            }

            return true;
        }

    }
}
