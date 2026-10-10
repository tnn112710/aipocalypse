using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Renders a Text Studio 2D Appearance for a text engine that keeps shaping, layout and interaction but hands its
    /// glyph output to Text Studio. Programs bind in the engine's frame; drawing waits for the engine's next glyph
    /// publication. Direct programs draw as ordered primitives and composited programs through the Hero compositor; rows
    /// this path cannot draw keep the engine's native output and report why.
    /// </summary>
    internal sealed class TextStudio2DExternalAppearanceRenderer : IDisposable
    {
        readonly TextStudioText _owner;
        readonly TextStudioTMPAppearanceAdapter _program = new TextStudioTMPAppearanceAdapter();
        readonly TextStudioTMPAppearancePlanAdapter _plan = new TextStudioTMPAppearancePlanAdapter();
        TextStudio2DOrderedRenderer _renderer;
        TextStudio2DHeroCompositor _hero;
        TextStudioFrame _frame;
        bool _usesPlan;
        bool _usesHero;
        bool _prepared;
        bool _forceRefresh;
        bool _disposed;

        internal TextStudio2DExternalAppearanceRenderer(TextStudioText owner)
        {
            _owner = owner;
            owner.ExternalAppearance = this;
        }

        /// <summary>The prepared frame asks Text Studio to own the engine's output.</summary>
        internal bool WantsOwnership { get; private set; }
        /// <summary>Why the engine keeps its native output for an effective appearance, or empty.</summary>
        internal string CompatibilityWarning { get; private set; } = string.Empty;
        /// <summary>Largest outward field reach of the prepared programs in em, for the engine's field rim.</summary>
        internal float RequiredReachEm { get; private set; }
        internal bool HasOutput => _usesHero ? _hero != null && _hero.HasOutput : _renderer != null && _renderer.DrawCount > 0;
        internal TextStudio2DOrderedRenderer OrderedRenderer => _renderer;
        internal TextStudio2DHeroCompositor HeroCompositor => _hero;

        /// <summary>
        /// Binds the frame's appearance to shared program slots and publishes mapping rows naming the source's fields.
        /// Returns whether Text Studio should own the output; otherwise <see cref="CompatibilityWarning"/> explains a
        /// decline for an effective appearance.
        /// </summary>
        /// <param name="sourceLimitation">Why the source cannot represent this text for owned output, or null.</param>
        internal bool Prepare(in TextStudioFrame frame, TextStudio2DExternalGlyphSource source, string sourceLimitation = null)
        {
            ThrowIfDisposed();
            _frame = frame;
            _prepared = false;
            CompatibilityWarning = string.Empty;
            RequiredReachEm = 0;
            CompiledTextStudioAppearancePlan plan = frame.AppearancePlan;
            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            bool effective = plan != null && plan.HasInvocations || style != null && style.IsValid && !style.IsPassThrough;
            if (effective && !string.IsNullOrEmpty(sourceLimitation))
            {
                Release();
                CompatibilityWarning = sourceLimitation;
                return false;
            }
            bool changed;
            if (plan != null && plan.HasInvocations)
            {
                changed = !_usesPlan;
                _usesPlan = true;
                _program.Release();
                changed |= _plan.Prepare(plan, null);
            }
            else
            {
                changed = _usesPlan;
                if (_usesPlan) _plan.Release();
                _usesPlan = false;
                changed |= _program.Update(frame.AppearanceStyle, false, null);
            }
            _forceRefresh |= changed;
            WantsOwnership = ResolveOwnership(in frame, out string warning);
            CompatibilityWarning = warning;
            if (!WantsOwnership || source == null || !frame.VisualSnapshot.IsCreated) return WantsOwnership;
            if (_usesPlan) _plan.UpdateMappings(in frame, null, null, source);
            else
            {
                _program.UpdateMappings(in frame, null, null, source);
                _program.UpdateVisualModifiers(in frame, frame.MotionPolicy);
            }
            if (_usesPlan) _plan.FlushAndBind();
            else _program.FlushAndBind();
            string mappingWarning = _usesPlan ? _plan.CompatibilityWarning : _program.CompatibilityWarning;
            if (!string.IsNullOrEmpty(mappingWarning)) CompatibilityWarning = mappingWarning;
            _prepared = true;
            return true;
        }

        /// <summary>Draws the prepared frame from the source's latest publication.</summary>
        internal bool Render(TextStudio2DExternalGlyphSource source)
        {
            ThrowIfDisposed();
            if (!_prepared || !WantsOwnership || source == null || source.Count == 0 || !source.HasAtlas) { Hide(); return false; }
            bool force = _forceRefresh;
            _forceRefresh = false;
            if (_usesHero)
            {
                _renderer?.HideOutput();
                _hero ??= new TextStudio2DHeroCompositor(_owner);
                bool composed = _usesPlan
                    ? _hero.RenderAppearanceExternal(in _frame, source, _plan.Programs, _plan.Binding, force)
                    : _program.HeroProgram != null &&
                      _hero.RenderExternal(in _frame, source, _program.HeroProgram, _program.HeroStyleSlot, force, _program.SourceDemand);
                if (!composed)
                {
                    _hero.HideOutput();
                    if (!string.IsNullOrEmpty(_hero.CompatibilityWarning)) CompatibilityWarning = _hero.CompatibilityWarning;
                }
                return composed;
            }
            _hero?.HideOutput();
            _renderer ??= new TextStudio2DOrderedRenderer(_owner);
            return _usesPlan
                ? _renderer.RenderAppearanceExternal(in _frame, source, _plan, force)
                : _program.FieldStyleSlot > 0 && _renderer.RenderExternal(in _frame, source, _program.FieldStyleSlot, force);
        }

        /// <summary>The executed route and scheduled draws of the latest publication.</summary>
        internal TextStudio2DRenderStatistics CaptureStatistics()
        {
            if (!WantsOwnership)
                return new TextStudio2DRenderStatistics(TextStudio2DRoute.PassThrough,
                    string.IsNullOrEmpty(CompatibilityWarning) ? TextStudio2DRouteReason.None : TextStudio2DRouteReason.EngineKeepsNativeOutput,
                    0, false, 0, 0, 0, 0, 0, 0, 0);
            if (_usesHero)
            {
                TextStudio2DHeroCompositorStatistics hero = _hero != null ? _hero.Statistics : default;
                bool composed = _hero != null && _hero.HasOutput;
                return new TextStudio2DRenderStatistics(TextStudio2DRoute.Composited, _usesPlan ? _plan.CompositionReason : _program.Route.Reason,
                    composed ? 1 : 0, false,
                    composed ? hero.LayerDrawCount : 0,
                    composed ? Math.Max(0, hero.CompositePassCount - hero.EffectPassCount) : 0,
                    composed ? hero.EffectPassCount : 0,
                    composed ? hero.PublishPassCount : 0,
                    0,
                    composed ? hero.TargetWidth : 0,
                    composed ? hero.TargetHeight : 0);
            }
            TextStudio2DRouteReason reason = _usesPlan ? TextStudio2DRouteReason.ScopedAppearance
                : _program.Route.Reason == TextStudio2DRouteReason.CompactField ? TextStudio2DRouteReason.EngineOwnedOutput : _program.Route.Reason;
            return new TextStudio2DRenderStatistics(TextStudio2DRoute.OrderedPrimitives, reason, _renderer?.DrawCount ?? 0, false,
                0, 0, 0, 0, 0, 0, 0);
        }

        internal void CrossFade(Color color, float duration, bool ignoreTimeScale, bool useAlpha, bool useRgb) =>
            _renderer?.CrossFade(color, duration, ignoreTimeScale, useAlpha, useRgb);

        /// <summary>Hides owned output while the engine draws natively; the next publication rebuilds it.</summary>
        internal void Hide()
        {
            _renderer?.HideOutput();
            _hero?.HideOutput();
        }

        /// <summary>Releases shared program slots and owned output; the engine restores its native output.</summary>
        internal void Release()
        {
            _renderer?.Dispose();
            _renderer = null;
            _hero?.Dispose();
            _hero = null;
            _usesHero = false;
            _program.Release();
            _plan.Release();
            _usesPlan = false;
            _prepared = false;
            WantsOwnership = false;
            CompatibilityWarning = string.Empty;
            RequiredReachEm = 0;
            _frame = default;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Release();
            if (ReferenceEquals(_owner != null ? _owner.ExternalAppearance : null, this)) _owner.ExternalAppearance = null;
            _disposed = true;
        }

        bool ResolveOwnership(in TextStudioFrame frame, out string warning)
        {
            warning = string.Empty;
            _usesHero = false;
            if (_usesPlan)
            {
                _usesHero = _plan.UsesHero;
                bool any = false;
                CompiledTextStudioAppearancePlan plan = frame.AppearancePlan;
                for (int index = 0; index < _plan.Programs.Count && index < plan.Programs.Count; index++)
                {
                    TextStudioTMPAppearanceAdapter program = _plan.Programs[index];
                    if (_usesHero ? !program.HasHeroProgram : !program.HasActiveProgram) continue;
                    if (HasRetainedOperations(plan.Programs[index]))
                    {
                        warning = DeclineRetained();
                        return false;
                    }
                    any = true;
                    IncludeReach(plan.Programs[index]);
                }
                if (!any && !string.IsNullOrEmpty(_plan.CompatibilityWarning)) warning = _plan.CompatibilityWarning;
                return any;
            }
            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            if (style == null || !style.IsValid || style.IsPassThrough) return false;
            // Declined programs stay bound, so an unchanged style does not rebuild them every frame.
            if (HasRetainedOperations(style))
            {
                warning = DeclineRetained();
                return false;
            }
            _usesHero = _program.Route.IsComposited;
            if (_usesHero ? !_program.HasHeroProgram : !_program.HasActiveProgram)
            {
                warning = _program.CompatibilityWarning;
                return false;
            }
            IncludeReach(style);
            return true;
        }

        void IncludeReach(CompiledTextStudio2DStyle style)
        {
            if (style == null) return;
            TextStudio2DPrimitiveSupport.Support support = default;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                support.Include(in operation);
            }
            TextStudio2DBoundsExpansion reach = support.ReachEm;
            RequiredReachEm = Mathf.Max(RequiredReachEm, Mathf.Max(Mathf.Max(reach.Left, reach.Right), Mathf.Max(reach.Bottom, reach.Top)));
        }

        /// <summary>Retained decoration, contour-stroke and surface rows need TMP source geometry this path does not build.</summary>
        static bool HasRetainedOperations(CompiledTextStudio2DStyle style)
        {
            if (style == null) return false;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry || operation.Kind == TextStudio2DLayerKind.Decoration ||
                    TextStudio2DStyleDecorations.UsesContourStroke(in operation)) return true;
            }
            return false;
        }

        static string DeclineRetained() =>
            "This appearance draws retained decoration, stroke or depth geometry, which engine-owned output does not build yet; the text engine keeps its native output.";

        void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextStudio2DExternalAppearanceRenderer));
        }
    }
}
