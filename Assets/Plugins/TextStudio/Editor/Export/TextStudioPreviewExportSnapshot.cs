using System;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio.Editor
{
    internal sealed class TextStudioPreviewExportSnapshot : IDisposable
    {
        readonly AnimationEvaluator _evaluator = new AnimationEvaluator();
        readonly GlyphDirtyBits _dirty = new GlyphDirtyBits();
        readonly TextStudioRenderPropertyBuffer _renderProperties =
            new TextStudioRenderPropertyBuffer();
        readonly UnityEngine.Object _ownedDefinition;
        readonly ITextStudioPreviewExportStateModifier _stateModifier;
        EvaluatedGlyphState[] _states;

        TextStudioPreviewExportSnapshot(
            string displayName,
            string sampleText,
            Color32 baseColor,
            TextStudioPreviewSegment segments,
            float speed,
            int activeDurationMultiplier,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            string rendererId,
            TextStudioCompiledPlan plan,
            UnityEngine.Object ownedDefinition,
            ITextStudioPreviewExportStateModifier stateModifier = null)
        {
            DisplayName = displayName;
            SampleText = sampleText;
            BaseColor = baseColor;
            Segments = segments;
            Speed = Mathf.Max(0.01f, speed);
            ActiveDurationMultiplier = Mathf.Max(1, activeDurationMultiplier);
            RevealDuration = Mathf.Max(0.0001f, revealDuration);
            ActiveDuration = Mathf.Max(0.0001f, activeDuration);
            HideDuration = Mathf.Max(0.0001f, hideDuration);
            RendererId = rendererId ?? string.Empty;
            Plan = plan;
            _ownedDefinition = ownedDefinition;
            _stateModifier = stateModifier;
            SourceDuration = TextStudioEffectPreviewUtility.GetCycleDuration(
                Segments,
                RevealDuration,
                ActiveDuration,
                HideDuration,
                ActiveDurationMultiplier);
            DisplayDuration = SourceDuration / Speed;
            _states = new EvaluatedGlyphState[Mathf.Max(Plan.VisibleGlyphCount, 1)];
            _dirty.EnsureCapacity(Plan.VisibleGlyphCount);
            _dirty.SetAllDirty(Plan.VisibleGlyphCount);
        }

        internal string DisplayName { get; }

        internal string SampleText { get; }

        internal Color32 BaseColor { get; }

        internal Color BackgroundColor { get; set; } = new Color32(34, 37, 45, 255);

        internal TextStudioPreviewSegment Segments { get; }

        internal float Speed { get; }

        internal int ActiveDurationMultiplier { get; }

        internal float RevealDuration { get; }

        internal float ActiveDuration { get; }

        internal float HideDuration { get; }

        internal float SourceDuration { get; }

        internal float DisplayDuration { get; }

        internal string RendererId { get; }

        internal TextStudioCompiledPlan Plan { get; }

        internal EvaluatedGlyphState[] States => _states;

        internal GlyphDirtyBits Dirty => _dirty;

        internal TextStudioRenderPropertyView RenderProperties =>
            _renderProperties.View;

        internal static bool TryCreate(
            TextStudioText source,
            string displayName,
            float duration,
            string rendererId,
            ITextStudioPreviewExportStateModifier stateModifier,
            out TextStudioPreviewExportSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (source == null)
            {
                stateModifier?.Dispose();
                error = "The Text Studio source is no longer available.";
                return false;
            }
            if (stateModifier == null)
            {
                error = "The stateful export source is no longer available.";
                return false;
            }

            try
            {
                source.ForceMeshRefresh(0f);
                string markup = source.Markup;
                if (string.IsNullOrEmpty(markup) && source.CompiledPlan != null)
                    markup = source.CompiledPlan.ResolvedText;
                TextStudioRegistry.EnsureInitialized();
                TextStudioPipeline pipeline = new TextStudioPipeline();
                pipeline.Process(new StringTextSource(markup ?? string.Empty));
                TextStudioCompiledPlan plan = pipeline.Plan;
                if (plan == null || plan.VisibleGlyphCount == 0)
                {
                    stateModifier.Dispose();
                    error = "The Text Studio source did not produce any renderable glyphs.";
                    return false;
                }

                Color32 baseColor = TextStudioEffectPreviewUtility.BaseColor;
                TMPro.TMP_Text text = source.GetComponent<TMPro.TMP_Text>();
                if (text != null)
                    baseColor = text.color;
                snapshot = new TextStudioPreviewExportSnapshot(
                    string.IsNullOrWhiteSpace(displayName) ? source.name : displayName,
                    !string.IsNullOrEmpty(plan.DisplayText) ? plan.DisplayText : plan.ResolvedText,
                    baseColor,
                    TextStudioPreviewSegment.Active,
                    1f,
                    1,
                    1f,
                    Mathf.Max(0.0001f, duration),
                    1f,
                    string.IsNullOrWhiteSpace(rendererId)
                        ? TextStudioPreviewExportRendererRegistry.FlatRendererId
                        : rendererId,
                    plan,
                    null,
                    stateModifier);
                snapshot.Evaluate(0f);
                snapshot.ResetEvaluation();
                return true;
            }
            catch (Exception exception)
            {
                if (snapshot != null)
                {
                    snapshot.Dispose();
                    snapshot = null;
                }
                else
                {
                    stateModifier.Dispose();
                }
                error = "Text Studio could not prepare the stateful export: " + exception.Message;
                return false;
            }
        }

        internal static bool TryCreate(
            TextStudioCodeEffectAsset source,
            TextStudioPreviewSegment segments,
            float speed,
            out TextStudioPreviewExportSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (source == null)
            {
                error = "The Code Effect is no longer available.";
                return false;
            }

            TextStudioCodeEffectAsset effect = UnityEngine.Object.Instantiate(source);
            effect.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                TextStudioRegistry.EnsureInitialized();
                string previewId = "__textstudio_code_export_" + effect.GetStableId().ToString("x");
                string markup = "<" + previewId + ">" +
                    TextStudioEffectPreviewUtility.SampleText +
                    "</" + previewId + ">";
                TextStudioRegistry.Effects.Register(previewId, effect);
                TextStudioPipeline pipeline = new TextStudioPipeline();
                try
                {
                    pipeline.Process(new StringTextSource(markup));
                }
                finally
                {
                    TextStudioRegistry.Effects.Unregister(previewId);
                }
                TextStudioCompiledPlan plan = pipeline.Plan;
                if (plan == null || plan.VisibleGlyphCount == 0 || plan.EffectBindingCount == 0)
                {
                    error = "The Code Effect preview did not produce a bound evaluator.";
                    UnityEngine.Object.DestroyImmediate(effect);
                    return false;
                }

                snapshot = new TextStudioPreviewExportSnapshot(
                    source.name,
                    TextStudioEffectPreviewUtility.SampleText,
                    TextStudioEffectPreviewUtility.BaseColor,
                    segments,
                    speed,
                    1,
                    1f,
                    1f,
                    1f,
                    TextStudioPreviewExportRendererRegistry.FlatRendererId,
                    plan,
                    effect);
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Object.DestroyImmediate(effect);
                error = "Text Studio could not prepare this Code Effect for export: " + exception.Message;
                return false;
            }
        }

        internal static bool TryCreate(
            string markup,
            TextStudioPreviewSegment segments,
            float speed,
            out TextStudioPreviewExportSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(markup))
            {
                error = "The preview markup is empty.";
                return false;
            }

            try
            {
                TextStudioRegistry.EnsureInitialized();
                TextStudioPipeline pipeline = new TextStudioPipeline();
                pipeline.Process(new StringTextSource(markup));
                TextStudioCompiledPlan plan = pipeline.Plan;
                if (plan == null || plan.VisibleGlyphCount == 0)
                {
                    error = "The preview markup did not produce any renderable glyphs.";
                    return false;
                }

                if (plan.EffectBindings == null || plan.EffectBindingCount == 0)
                {
                    error = "The preview markup did not resolve a Text Studio effect.";
                    return false;
                }

                snapshot = new TextStudioPreviewExportSnapshot(
                    "Text Gallery Preview",
                    !string.IsNullOrEmpty(plan.DisplayText) ? plan.DisplayText : plan.ResolvedText,
                    TextStudioEffectPreviewUtility.BaseColor,
                    segments,
                    speed,
                    1,
                    TextStudioComposedEffectAssetEditor.GetPreviewCycleDuration(
                        plan,
                        AnimationPhase.Appearance),
                    TextStudioComposedEffectAssetEditor.GetPreviewCycleDuration(
                        plan,
                        AnimationPhase.Persistent),
                    TextStudioComposedEffectAssetEditor.GetPreviewCycleDuration(
                        plan,
                        AnimationPhase.Disappearance),
                    TextStudioPreviewExportRendererRegistry.FlatRendererId,
                    plan,
                    null);
                return true;
            }
            catch (Exception exception)
            {
                snapshot?.Dispose();
                snapshot = null;
                error = "Text Studio could not prepare the gallery preview: " + exception.Message;
                return false;
            }
        }

        internal static bool TryCreate(
            TextStudioComposedEffectAsset source,
            TextStudioPreviewSegment segments,
            float speed,
            int activeDurationMultiplier,
            out TextStudioPreviewExportSnapshot snapshot,
            out string error,
            string previewSoloLayerId = null)
        {
            snapshot = null;
            error = string.Empty;
            if (source == null)
            {
                error = "The Composed Effect is no longer available.";
                return false;
            }

            if (!source.TryCompileForValidation(out string validationError))
            {
                error = string.IsNullOrWhiteSpace(validationError)
                    ? "Resolve the Composed Effect validation errors before exporting."
                    : validationError;
                return false;
            }

            string rendererId = source.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD
                ? TextStudioPreviewExportRendererRegistry.GetFirstExtensionRendererId()
                : TextStudioPreviewExportRendererRegistry.FlatRendererId;
            if (string.IsNullOrEmpty(rendererId))
            {
                error = "Text Studio 3D is required to export a 3D Composed Effect.";
                return false;
            }

            TextStudioComposedEffectAsset composed = UnityEngine.Object.Instantiate(source);
            composed.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                TextStudioRegistry.EnsureInitialized();
                string previewId = "__textstudio_composed_export_" + composed.GetStableId().ToString("x");
                string markup = "<" + previewId + ">" +
                    TextStudioComposedEffectAssetEditor.PreviewSampleText +
                    "</" + previewId + ">";
                TextStudioRegistry.Effects.Register(previewId, composed);
                TextStudioPipeline pipeline = new TextStudioPipeline();
                try
                {
                    pipeline.Process(new StringTextSource(markup), composed, previewSoloLayerId);
                }
                finally
                {
                    TextStudioRegistry.Effects.Unregister(previewId);
                }

                TextStudioCompiledPlan plan = pipeline.Plan;
                if (plan == null || plan.VisibleGlyphCount == 0)
                {
                    error = "The composed preview did not produce any renderable glyphs.";
                    UnityEngine.Object.DestroyImmediate(composed);
                    return false;
                }

                float revealDuration = TextStudioComposedEffectAssetEditor.GetPreviewCycleDuration(
                    plan,
                    AnimationPhase.Appearance);
                float activeDuration = TextStudioComposedEffectAssetEditor.GetPreviewCycleDuration(
                    plan,
                    AnimationPhase.Persistent);
                float hideDuration = TextStudioComposedEffectAssetEditor.GetPreviewCycleDuration(
                    plan,
                    AnimationPhase.Disappearance);
                snapshot = new TextStudioPreviewExportSnapshot(
                    source.name,
                    TextStudioComposedEffectAssetEditor.PreviewSampleText,
                    TextStudioEffectPreviewUtility.BaseColor,
                    segments,
                    speed,
                    activeDurationMultiplier,
                    revealDuration,
                    activeDuration,
                    hideDuration,
                    rendererId,
                    plan,
                    composed);
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Object.DestroyImmediate(composed);
                error = "Text Studio could not prepare this Composed Effect for export: " + exception.Message;
                return false;
            }
        }

        internal EvaluatedGlyphState[] Evaluate(float lifecycleTime)
        {
            return Evaluate(lifecycleTime, lifecycleTime);
        }

        internal EvaluatedGlyphState[] Evaluate(float continuousTime, float lifecycleTime)
        {
            TextStudioPreviewSpatialEvaluationContext spatialContext = default;
            return Evaluate(continuousTime, lifecycleTime, in spatialContext);
        }

        internal EvaluatedGlyphState[] Evaluate(
            float continuousTime,
            float lifecycleTime,
            in TextStudioPreviewSpatialEvaluationContext spatialContext)
        {
            float clampedLifecycleTime = Mathf.Clamp(lifecycleTime, 0f, SourceDuration);
            float safeContinuousTime = Mathf.Max(0f, continuousTime);
            TextStudioEffectPreviewUtility.ResolveFrame(
                clampedLifecycleTime,
                Segments,
                RevealDuration,
                ActiveDuration,
                HideDuration,
                ActiveDurationMultiplier,
                out AnimationPhase phase,
                out float progress,
                out float phaseTime,
                out _);

            EvaluateBoundPlan(
                phase,
                progress,
                phaseTime,
                safeContinuousTime,
                clampedLifecycleTime,
                in spatialContext);

            _renderProperties.CopyFrom(
                _evaluator.RenderProperties,
                Plan.VisibleGlyphCount);
            TextStudioRenderPropertyWriter renderProperties =
                _renderProperties.CreateWriter(0, Plan.VisibleGlyphCount);

            _stateModifier?.Apply(
                safeContinuousTime,
                clampedLifecycleTime,
                Plan,
                _states,
                Plan.VisibleGlyphCount,
                in renderProperties);

            _dirty.SetAllDirty(Plan.VisibleGlyphCount);
            return _states;
        }

        internal void ResetEvaluation()
        {
            _evaluator.Reset();
            _renderProperties.Clear(Plan.VisibleGlyphCount);
            _stateModifier?.ResetEvaluation();
        }

        void EvaluateBoundPlan(
            AnimationPhase phase,
            float progress,
            float phaseTime,
            float continuousTime,
            float lifecycleTime,
            in TextStudioPreviewSpatialEvaluationContext spatialContext)
        {
            RevealController reveal = new RevealController
            {
                Mode = RevealMode.ByGlyph,
                AppearanceDuration = RevealDuration,
                DisappearanceDuration = HideDuration
            };
            reveal.Initialize(Plan);
            reveal.SetRevealActiveBlendEnabledForPreview(
                (Segments & TextStudioPreviewSegment.Reveal) != 0);
            if (phase == AnimationPhase.Appearance)
            {
                reveal.StartReveal();
                float revealScrubDuration = reveal.GetApproximateVisualRevealDuration();
                reveal.SetScrubElapsedTime(progress * revealScrubDuration);
            }
            else if (phase == AnimationPhase.Disappearance)
            {
                float activeElapsedBeforeHide =
                    (Segments & TextStudioPreviewSegment.Active) != 0
                        ? ActiveDuration * Mathf.Max(1, ActiveDurationMultiplier)
                        : 0f;
                reveal.SetActiveElapsedTimeForPreview(
                    activeElapsedBeforeHide + Mathf.Max(0f, phaseTime));
                reveal.StartDisappear();
                reveal.SetDisappearanceProgressForPreview(progress);
            }
            else
            {
                reveal.SetActiveElapsedTimeForPreview(Mathf.Max(0f, phaseTime));
            }

            _evaluator.Initialize(Plan.VisibleGlyphCount);
            _evaluator.BindEffects(Plan);
            TextStudioPreviewSpatialTarget target = spatialContext.InfluenceTarget;
            _evaluator.SetSpatialContext(
                spatialContext.SourcePositions,
                spatialContext.SourceTextNormalizedPositions,
                spatialContext.SourceColors,
                spatialContext.SourceColorValidity,
                spatialContext.IsCreated
                    ? Mathf.Min(Plan.VisibleGlyphCount, spatialContext.GlyphCount)
                    : 0,
                null,
                0,
                PlacementMode.Linear,
                spatialContext.IsCreated
                    ? spatialContext.LocalToWorld
                    : Matrix4x4.identity,
                spatialContext.IsCreated && spatialContext.HasInfluenceTarget,
                target.LocalPosition,
                target.WorldPosition,
                target.SourceTextNormalizedPosition,
                spatialContext.SourceVisualBounds,
                unchecked((int)Plan.SourceVersion));
            if (phase == AnimationPhase.Persistent)
            {
                _evaluator.EvaluateWithPersistentPhaseProgress(
                    continuousTime,
                    lifecycleTime,
                    reveal,
                    Plan,
                    progress);
            }
            else
            {
                _evaluator.EvaluateWithPlaybackTime(
                    continuousTime,
                    lifecycleTime,
                    reveal,
                    Plan);
            }

            EvaluatedGlyphState[] evaluated = _evaluator.States;
            if (_states == null || _states.Length < Plan.VisibleGlyphCount)
                _states = new EvaluatedGlyphState[Mathf.Max(Plan.VisibleGlyphCount, 1)];
            Array.Copy(evaluated, _states, Plan.VisibleGlyphCount);
        }

        public void Dispose()
        {
            _stateModifier?.Dispose();
            if (_ownedDefinition != null)
                UnityEngine.Object.DestroyImmediate(_ownedDefinition);
        }
    }
}
