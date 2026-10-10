using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Pipeline internals
        // ─────────────────────────────────────────────────────────────────────

        bool ShouldMirrorRuntimeTypewriterToInlineSettings()
        {
            return !UsesBehaviorProfileTypewriter &&
                   _typewriterSource == TextStudioTypewriterSource.Inline;
        }

        bool ShouldStartTypewriterOnEnable()
        {
            TextStudioTypewriterStartMode startMode = ResolveTypewriterSettings().StartMode;
            return startMode == TextStudioTypewriterStartMode.OnEnable ||
                   startMode == TextStudioTypewriterStartMode.OnEnableOrTextChanged;
        }

        bool ShouldStartTypewriterOnTextChanged()
        {
            return ShouldStartTypewriterOnTextChanged(ResolveTypewriterSettings().StartMode);
        }

        static bool ShouldStartTypewriterOnTextChanged(TextStudioTypewriterStartMode startMode)
        {
            return startMode == TextStudioTypewriterStartMode.OnTextChanged ||
                   startMode == TextStudioTypewriterStartMode.OnEnableOrTextChanged;
        }

        void ApplyBehaviorProfile()
        {
            ApplyDefaultEffects();
            SyncRuntimeConfiguration();
        }

        // Every dirty tick resolves the behavior settings once for both the runtime configuration and the default effects.
        void SyncBehaviorConfiguration()
        {
            TextStudioResolvedBehaviorSettings behavior = ResolveBehaviorSettings();
            SyncRuntimeConfiguration(behavior.Typewriter.Resolve());
            ApplyDefaultEffects(behavior.DefaultEffects);
            RecordBehaviorProfileSync();
        }

        void ApplyDefaultEffects()
        {
            ApplyDefaultEffects(ResolveBehaviorSettings().DefaultEffects);
        }

        void ApplyDefaultEffects(TextStudioDefaultEffectSettings settings)
        {
            if (_hasAppliedDefaultEffects && _appliedDefaultEffects.Equals(settings))
                return;

            _runtime.ApplyDefaultEffects(settings);
            _appliedDefaultEffects = settings;
            _hasAppliedDefaultEffects = true;
        }

        void SyncRuntimeConfiguration()
        {
            SyncRuntimeConfiguration(ResolveTypewriterSettings());
        }

        void SyncRuntimeConfiguration(in TextStudioTypewriterSettings settings)
        {
            if (!_hasAppliedRuntimeSettings || !RuntimeSettingsEqual(in _appliedRuntimeSettings, in settings))
            {
                _runtime.Configure(
                    settings.RevealMode,
                    settings.Timings,
                    settings.BuiltInTimingsPreset,
                    settings.WaitAfterFinalReveal,
                    settings.RevealSpeedMultiplier,
                    settings.RevealAppearanceDuration,
                    settings.RevealDisappearanceDuration,
                    settings.LoopReveal,
                    settings.LoopRevealDelay,
                    settings.RevealEffectExpression,
                    settings.HideEffectExpression,
                    string.IsNullOrEmpty(settings.RevealEffectExpression) &&
                    string.IsNullOrEmpty(settings.HideEffectExpression)
                        ? TextStudioDefaultEffectMode.None
                        : TextStudioDefaultEffectMode.Constant,
                    settings.SkipRevealMode,
                    settings.TriggerRemainingMarkersOnSkip,
                    settings.SkipCurrentWaitOnSkip,
                    settings.SkipHideMode,
                    settings.RevealSequence,
                    settings.HideSequence,
                    settings.RevealEffect,
                    settings.HideEffect,
                    settings.TimingMode);
                _appliedRuntimeSettings = settings;
                _hasAppliedRuntimeSettings = true;
            }

            if (_runtime.RunActionsInEditModePreview != _runActionsInPreview)
                _runtime.RunActionsInEditModePreview = _runActionsInPreview;
            _runtime.MotionPolicy = _motionPolicy;
        }

        static bool RuntimeSettingsEqual(
            in TextStudioTypewriterSettings left,
            in TextStudioTypewriterSettings right)
        {
            return left.Equals(right);
        }

        TextStudioTypewriterSettings ResolveTypewriterSettings()
        {
            return ResolveTypewriterRecipe().Resolve();
        }

        // Every tick checks the writing style, so read only the recipe fields Resolve() would copy into WritingEnabled and WritingStyle.
        TextStudioWritingStyleAsset ResolveWritingStyle()
        {
            if (_behaviorProfile != null && _behaviorProfile.Provides(TextStudioBehaviorDomain.Typewriter))
                return WritingStyleOf(_behaviorProfile.Typewriter);
            switch (_typewriterSource)
            {
                case TextStudioTypewriterSource.None:
                    return null;
                case TextStudioTypewriterSource.Custom:
                    return _typewriterPreset != null ? WritingStyleOf(_typewriterPreset.Settings) : null;
                case TextStudioTypewriterSource.Inline:
                    return _writingEnabled ? _writingStyle : null;
                default:
                    return BuiltInWritingStyle(_typewriterPresetId);
            }
        }

        static TextStudioWritingStyleAsset WritingStyleOf(in TextStudioTypewriterRecipe recipe)
        {
            switch (recipe.Source)
            {
                case TextStudioTypewriterSource.None:
                    return null;
                case TextStudioTypewriterSource.Custom:
                    return recipe.CustomPreset != null ? WritingStyleOf(recipe.CustomPreset.Settings) : null;
                case TextStudioTypewriterSource.Inline:
                    return WritingStyleOf(recipe.InlineSettings);
                default:
                    return BuiltInWritingStyle(recipe.BuiltInPresetId);
            }
        }

        static TextStudioWritingStyleAsset BuiltInWritingStyle(string presetId)
        {
            TextStudio.BuiltIns.TextStudioBuiltInTypewriterPreset? preset = BuiltIns.BuiltInTypewriterPresets.ForId(
                string.IsNullOrEmpty(presetId) ? BuiltIns.BuiltInTypewriterPresets.Default.Id : presetId);
            return WritingStyleOf((preset ?? BuiltIns.BuiltInTypewriterPresets.Default).Settings);
        }

        static TextStudioWritingStyleAsset WritingStyleOf(in TextStudioTypewriterSettings settings) =>
            settings.WritingEnabled ? settings.WritingStyle : null;

        TextStudioTypewriterRecipe ResolveTypewriterRecipe()
        {
            return ResolveBehaviorSettings().Typewriter;
        }

        TextStudioTypewriterRecipe GetLocalTypewriterRecipe()
        {
            switch (_typewriterSource)
            {
                case TextStudioTypewriterSource.None:
                    return TextStudioTypewriterRecipe.None;
                case TextStudioTypewriterSource.Custom:
                    return TextStudioTypewriterRecipe.Custom(_typewriterPreset);
                case TextStudioTypewriterSource.Inline:
                    return TextStudioTypewriterRecipe.Inline(GetInlineTypewriterSettings());
                default:
                    return TextStudioTypewriterRecipe.BuiltIn(_typewriterPresetId);
            }
        }

        TextStudioTypewriterSettings GetInlineTypewriterSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealMode = _revealMode;
            settings.TimingMode = _typewriterTimingMode;
            settings.WritingEnabled = _writingEnabled;
            settings.WritingStyle = _writingStyle;
            settings.EraseOnHide = _eraseOnHide;
            settings.Timings = _timings;
            settings.BuiltInTimingsPreset = _builtInTimingsPreset;
            settings.WaitAfterFinalReveal = _waitAfterFinalReveal;
            settings.RevealSpeedMultiplier = Mathf.Max(0.01f, _revealSpeedMultiplier);
            settings.RevealAppearanceDuration = Mathf.Max(0.01f, _revealAppearanceDuration);
            settings.RevealDisappearanceDuration = Mathf.Max(0.01f, _revealDisappearanceDuration);
            settings.RevealSequence = !_revealSequence.Equals(default)
                ? _revealSequence
                : TextStudioSequenceSettings.Default;
            settings.HideSequence = !_hideSequence.Equals(default)
                ? _hideSequence
                : TextStudioSequenceSettings.Default;
            settings.StartMode = _typewriterStartMode;
            settings.TextChangeMode = _textChangeMode;
            settings.TextChangeScope = _textChangeScope;
            settings.ResetSpeedOnNewText = _resetSpeedOnNewText;
            settings.SkipRevealMode = _skipRevealMode;
            settings.TriggerRemainingMarkersOnSkip = _triggerRemainingMarkersOnSkip;
            settings.SkipCurrentWaitOnSkip = _skipCurrentWaitOnSkip;
            settings.SkipHideMode = _skipHideMode;
            settings.LoopReveal = _loopReveal;
            settings.LoopRevealDelay = Mathf.Max(0f, _loopRevealDelay);
            settings.RevealEffect = _typewriterRevealEffect;
            settings.HideEffect = _typewriterHideEffect;
            settings.RevealEffectExpression = _typewriterRevealEffectExpression ?? string.Empty;
            settings.HideEffectExpression = _typewriterHideEffectExpression ?? string.Empty;
            return TextStudioTypewriterSettings.NormalizeAuthoringSettings(settings);
        }

        static TextStudioTypewriterSettings CreateNoTypewriterSettings()
        {
            return TextStudioTypewriterRecipe.CreateNoTypewriterSettings();
        }

        TextStudioDefaultEffectSettings GetInlineDefaultEffectSettings()
        {
            return new TextStudioDefaultEffectSettings
            {
                Mode = _inlineDefaultEffectMode,
                AllPhaseEffects = _inlineDefaultEffects ?? string.Empty,
                RevealEffects = _inlineDefaultRevealEffects ?? string.Empty,
                ActiveEffects = _inlineDefaultActiveEffects ?? string.Empty,
                HideEffects = _inlineDefaultHideEffects ?? string.Empty
            };
        }

        /// <summary>
        /// Captures complete component-local behavior even while an assigned profile temporarily
        /// replaces one or both domains.
        /// </summary>
        public TextStudioBehaviorSettings GetLocalBehaviorSettings()
        {
            return new TextStudioBehaviorSettings
            {
                Domains = TextStudioBehaviorDomain.DefaultEffects |
                          TextStudioBehaviorDomain.Typewriter |
                          TextStudioBehaviorDomain.Reactions,
                DefaultEffects = GetInlineDefaultEffectSettings(),
                Typewriter = GetLocalTypewriterRecipe(),
                Reactions = _reactions
            };
        }

        TextStudioResolvedBehaviorSettings ResolveBehaviorSettings()
        {
            return TextStudioBehaviorResolver.Resolve(
                _behaviorProfile,
                GetInlineDefaultEffectSettings(),
                GetLocalTypewriterRecipe());
        }

        bool EnsureInlineTypewriterSource()
        {
            if (_typewriterSource == TextStudioTypewriterSource.Inline)
                return true;

            TextStudioTypewriterSettings settings =
                GetLocalTypewriterRecipe().Resolve();
            _typewriterSource = TextStudioTypewriterSource.Inline;
            StoreInlineTypewriterSettings(settings);
            return true;
        }

        void SyncActionRunner()
        {
            _runtime.ActionOwner = this;
            if (Application.isPlaying)
                _editModeActionRunner.Clear();
            _runtime.SetActionRunner(Application.isPlaying ? _actionRunner : _editModeActionRunner);
        }

        void ApplyPlacementStrategy()
        {
            switch (_placementMode)
            {
                case PlacementMode.Arc: _placementEngine.SetStrategy(_arcPlacement); break;
                case PlacementMode.Circle: _placementEngine.SetStrategy(_circlePlacement); break;
                case PlacementMode.Wave: _placementEngine.SetStrategy(_wavePlacement); break;
                case PlacementMode.Grid: _placementEngine.SetStrategy(_gridPlacement); break;
                case PlacementMode.Curve: _placementEngine.SetStrategy(_curvePlacement ??= new CurvePlacement()); break;
                case PlacementMode.Spline: _placementEngine.SetStrategy(FindPlacementStrategy(PlacementMode.Spline)); break;
                case PlacementMode.Extension: _placementEngine.SetStrategy(FindPlacementExtension(_extensionPlacementId)); break;
                case PlacementMode.Linear:
                default:
                    _placementEngine.SetStrategy(null);
                    break;
            }
        }

        IPlacementStrategy FindPlacementStrategy(PlacementMode mode)
        {
            for (int i = 0; i < _placementStrategies.Count; i++)
            {
                IPlacementStrategy strategy = _placementStrategies[i];
                if (strategy != null &&
                    IsPlacementStrategyAvailable(strategy) &&
                    strategy.Mode == mode)
                    return strategy;
            }

            return null;
        }

        ITextStudioPlacementExtension FindPlacementExtension(string placementId)
        {
            ITextStudioPlacementExtension fallback = null;
            for (int i = 0; i < _placementStrategies.Count; i++)
            {
                if (_placementStrategies[i] is not ITextStudioPlacementExtension extension)
                    continue;
                if (!IsPlacementStrategyAvailable(extension))
                    continue;
                fallback ??= extension;
                if (!string.IsNullOrEmpty(placementId) &&
                    string.Equals(extension.PlacementId, placementId, StringComparison.Ordinal))
                {
                    return extension;
                }
            }

            return string.IsNullOrEmpty(placementId) ? fallback : null;
        }

        static bool IsPlacementStrategyAvailable(IPlacementStrategy strategy)
        {
            if (strategy == null)
                return false;
            if (strategy is UnityEngine.Object unityObject && unityObject == null)
                return false;

            return strategy is not Behaviour behaviour || behaviour.isActiveAndEnabled;
        }

        bool ProcessPipeline()
        {
            bool changed = _runtime.ProcessPipeline(
                !Application.isPlaying && !Preview.IsPlaying && !Preview.IsScrubbing,
                IsRenderingRawExternalOutput,
                _markupSyntaxOverride);
            _preview?.ConfigureRevealActiveBlendForCurrentLifecycleInternal();
            EnsureAppearancePlanCurrent();
            return changed;
        }

        void SyncValidatedTextImmediate()
        {
            if (_runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
            {
                bool planChanged;
                using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Pipeline))
                    planChanged = ProcessPipeline();
                _runtime.ClearProcessedTextAndStyleDirty();
                if (planChanged)
                {
                    DirtyFlags pipelineDirty = DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual;
                    _runtime.SetDirty(pipelineDirty);

                    if (!Application.isPlaying && Preview.IsScrubbing)
                        Preview.ApplyScrubProgress();
                }
            }

            using (TextStudioPerformanceDiagnostics.Measure(
                       TextStudioPerformanceStage.BackendPublication))
            {
                PushTextBackend(IsDrivingTextBackend);
            }
            FinishImmediatePlainTextPublication();
        }

        // Plain text published immediately settles here, so the following tick stays idle instead of repeating the full
        // path to reach the same pass-through. Renderer objects can only be disposed outside OnValidate and Tick.
        void FinishImmediatePlainTextPublication()
        {
            if (!Application.isPlaying || _isTicking || _isValidating || _startRevealAfterDeferredPipeline ||
                !isActiveAndEnabled)
            {
                return;
            }

            SyncBehaviorConfiguration();
            if (_runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
                return;

            bool isDrivingTextBackend = IsDrivingTextBackend;
            if (isDrivingTextBackend)
                TryUseUnanimatedTMPPassThrough(isDrivingTextBackend, true);
        }

        void SyncTextImmediateForCurrentMode()
        {
            if (IsDrivingTextBackend)
                SyncValidatedTextImmediate();
            else if (IsRenderingRawExternalOutput)
            {
                SyncValidatedTextImmediate();
                FlushVisualStateImmediate();
            }
            else
            {
                using (TextStudioPerformanceDiagnostics.Measure(
                           TextStudioPerformanceStage.BackendPublication))
                {
                    PushRawAuthoringTextBackend();
                }
            }
        }

        bool ProcessPendingTextForReveal()
        {
            bool changed = _runtime.ProcessPendingTextForReveal();
            if (changed) EnsureAppearancePlanCurrent();
            return changed;
        }

        bool ProcessTMPTextPull()
        {
            return ProcessTextBackendPull();
        }

        bool ProcessTMPTextPull(bool isDrivingTMPText)
        {
            return ProcessTextBackendPull(isDrivingTMPText);
        }

        void PollExternalEffectSources()
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (plan == null || _externalEffectSources.Count == 0)
                return;

            TextStudioExternalEffectSubmissionContext context =
                new TextStudioExternalEffectSubmissionContext(_runtime, Effects, plan);
            for (int i = 0; i < _externalEffectSources.Count; i++)
            {
                ITextStudioExternalEffectSource source = _externalEffectSources[i];
                if (!IsExternalEffectSourceActive(source))
                    continue;

                try
                {
                    source.Submit(in context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        void UpdatePresentation(float dt)
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (plan == null) return;

            int glyphCount = plan.VisibleGlyphCount;
            bool providerOwnsGlyphIdentity =
                _glyphProvider != null && _glyphProvider.OwnsGlyphIdentity;
            bool changed = false;
            if (!providerOwnsGlyphIdentity)
            {
                changed = _characterSubstitutionPresenter.Update(
                    plan,
                    _presentationGlyphs,
                    glyphCount,
                    dt,
                    GetPresentationEvaluationTime(),
                    _presentationTimeDiscontinuous,
                    _runtime.Reveal,
                    _motionPolicy);
            }

            _presentationTimeDiscontinuous = false;
            if (_glyphProvider != null && (_glyphProvider.IsDirty || changed))
            {
                _glyphProvider.UpdatePresentation(_presentationGlyphs, glyphCount, dt);
                changed = true;
            }

            if (!changed)
                return;

            _runtime.MeshDirty.SetAllDirty(glyphCount);
            _runtime.SetDirty(DirtyFlags.Visual);
        }

    }
}
