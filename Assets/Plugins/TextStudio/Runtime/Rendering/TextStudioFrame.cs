using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Read-only frame view passed to every <see cref="ITextStudioRenderer"/> each pipeline tick.
    /// The view owns no buffers and does not allocate. Its data is engine-owned and may be
    /// overwritten or replaced by later ticks, so consumers that need stable historical data
    /// must copy only the values they retain.
    /// </summary>
    public readonly struct TextStudioFrame
    {
        /// <summary>The compiled plan (tokens, resolved text, segmentation, effect bindings).</summary>
        public TextStudioPlanView Plan { get; }

        /// <summary>Per-glyph placement results AFTER the placement strategy has run (Linear, Arc, Wave, ...).</summary>
        public TextStudioReadOnlyBuffer<GlyphPlacement> Placements { get; }

        /// <summary>Number of valid entries in <see cref="Placements"/>.</summary>
        public int PlacementCount { get; }

        /// <summary>Per-glyph linear base positions produced by the active text backend.</summary>
        public TextStudioReadOnlyBuffer<Vector3> BasePositions { get; }

        /// <summary>Optional per-unit measured layout coverage. Empty means complete coverage.</summary>
        public TextStudioReadOnlyBuffer<bool> SourceLayoutValidity { get; }
        /// <summary>Returns whether the supplied unit has measured source layout, treating an empty validity buffer as complete coverage.</summary>
        public bool HasSourceLayout(int unit) => (uint)unit < (uint)BasePositions.Count &&
            (SourceLayoutValidity.Count == 0 || SourceLayoutValidity[unit]);

        /// <summary>Per-glyph source vertex colors supplied by the active text backend.</summary>
        public TextStudioReadOnlyBuffer<TextStudioGlyphColorQuad> SourceColors { get; }

        /// <summary>Per-glyph local bounds used to sample source corner colors.</summary>
        public TextStudioReadOnlyBuffer<TextStudioGlyphColorBounds> SourceColorBounds { get; }

        /// <summary>
        /// Per-glyph conversion from normalized Text Studio effect units to backend-local units.
        /// </summary>
        public TextStudioReadOnlyBuffer<float> EffectUnitScales { get; }

        /// <summary>Authored placement mode. Valid Placements also include orientation and text-change interpolation in Linear mode.</summary>
        public PlacementMode PlacementMode { get; }

        /// <summary>Per-glyph animated state (position offset, rotation, scale, color override, alpha, phase).</summary>
        public TextStudioReadOnlyBuffer<EvaluatedGlyphState> Animated { get; }

        /// <summary>Number of valid entries in <see cref="Animated"/>.</summary>
        public int AnimatedCount { get; }

        /// <summary>Whether all valid animated entries carry the same rendered state.</summary>
        public bool AnimatedStateIsUniform { get; }

        /// <summary>Typed renderer properties stored independently from core per-glyph state.</summary>
        public TextStudioRenderPropertyView RenderProperties { get; }

        /// <summary>
        /// Final per-binding renderer ownership. Renderers must only execute prepared effects whose
        /// binding is selected here, even when they previously reported exact support.
        /// </summary>
        public TextStudioRendererEffectExecutionView RendererEffectExecution { get; }

        /// <summary>Persistent compiled 2D appearance, independent from animated effect properties.</summary>
        public CompiledTextStudio2DStyle AppearanceStyle { get; }
        /// <summary>Source-scoped composition of the default appearance and explicit invocations.</summary>
        public CompiledTextStudioAppearancePlan AppearancePlan { get; }
        internal TextStudio2DAppearanceBinding AppearanceBinding { get; }
        internal int AppearanceOccurrenceIndex { get; }
        internal int AppearanceProgramIndex { get; }

        /// <summary>Persistent compiled writing treatment and prepared path sources.</summary>
        public CompiledTextStudioWritingStyle WritingStyle { get; }

        /// <summary>Canonical per-unit Typewriter lifecycle for this frame.</summary>
        public TextStudioGlyphTransitionView Transitions { get; }
        public bool EraseWritingOnHide { get; }

        /// <summary>Renderer-neutral glyph, atlas, field, bounds, and source-version metadata.</summary>
        public TextStudioVisualSnapshot VisualSnapshot { get; }

        /// <summary>Per-glyph dirty bits indicating which glyphs changed since the last frame (for incremental writes).</summary>
        public TextStudioDirtyGlyphView Dirty { get; }

        /// <summary>Per-glyph composed layout, placement, and animation state.</summary>
        public TextStudioReadOnlyBuffer<TextStudioGlyphInstanceState> GlyphStates { get; }

        /// <summary>Number of valid entries in <see cref="GlyphStates"/>.</summary>
        public int GlyphStateCount { get; }

        /// <summary>Per-glyph presentation state after generic and add-on glyph substitution.</summary>
        public TextStudioReadOnlyBuffer<PresentationGlyph> PresentationGlyphs { get; }

        /// <summary>Number of valid entries in <see cref="PresentationGlyphs"/>.</summary>
        public int PresentationGlyphCount { get; }

        /// <summary>Normalized reveal progress (0..1).</summary>
        public float RevealProgress { get; }

        /// <summary>Dirty flags that changed between the last frame and this frame at the global (plan/layout/topology) level.</summary>
        public DirtyFlags GlobalDirty { get; }

        /// <summary>Seconds since the previous hub tick.</summary>
        public float DeltaTime { get; }

        /// <summary>Hub-local monotonically increasing time in seconds.</summary>
        public float Time { get; }

        /// <summary>
        /// Presentation time in seconds for persistent visual treatments and glyph presentation.
        /// This clock restarts with presentation content and follows edit-mode preview seek and pause.
        /// </summary>
        public float PresentationTime { get; }

        /// <summary>
        /// Effect playback time in seconds. Unlike <see cref="Time"/>, this clock may restart,
        /// seek, pause, or reverse when preview and playback controls request it.
        /// </summary>
        public float PlaybackTime { get; }

        /// <summary>Resolved accessibility policy for this frame, including the host's project preference.</summary>
        public TextStudioMotionPolicy MotionPolicy { get; }

        internal TextStudioFrame(
            TextStudioCompiledPlan plan,
            GlyphPlacement[] placements,
            int placementCount,
            Vector3[] basePositions,
            PlacementMode placementMode,
            EvaluatedGlyphState[] animated,
            int animatedCount,
            GlyphDirtyBits dirty,
            float revealProgress,
            DirtyFlags globalDirty,
            TextStudioGlyphInstanceState[] glyphStates = null,
            int glyphStateCount = 0,
            PresentationGlyph[] presentationGlyphs = null,
            int presentationGlyphCount = 0,
            float deltaTime = 0f,
            float time = 0f,
            TextStudioGlyphColorQuad[] sourceColors = null,
            TextStudioGlyphColorBounds[] sourceColorBounds = null,
            float[] effectUnitScales = null,
            bool animatedStateIsUniform = false,
            TextStudioRenderPropertyView renderProperties = default,
            CompiledTextStudio2DStyle appearanceStyle = null,
            CompiledTextStudioWritingStyle writingStyle = null,
            TextStudioGlyphTransitionView transitions = default,
            TextStudioVisualGlyph[] visualGlyphs = null,
            TextStudioVisualCapabilities visualCapabilities = TextStudioVisualCapabilities.None,
            TextStudioVisualVersion visualVersion = default,
            Vector4 visualSourceBounds = default,
            float playbackTime = float.NaN,
            float presentationTime = float.NaN,
            bool[] rendererOwnedEffectBindings = null,
            int rendererOwnedEffectBindingCount = 0,
            TextStudioVisualFontSource[] fontSources = null,
            int fontSourceCount = 0,
            TextStudioVisualGlyph[] renderGlyphs = null, int renderGlyphCount = 0, int[] unitToRenderGlyph = null,
            TextStudioColorSource[] colorSources = null, int colorSourceCount = 0,
            TextStudioMotionPolicy motionPolicy = TextStudioMotionPolicy.Inherit,
            bool eraseWritingOnHide = false, bool[] sourceLayoutValidity = null,
            CompiledTextStudioAppearancePlan appearancePlan = null)
        {
            int glyphCount = plan != null ? plan.VisibleGlyphCount : animatedCount;
            Plan = new TextStudioPlanView(plan);
            SourceLayoutValidity = new TextStudioReadOnlyBuffer<bool>(sourceLayoutValidity, sourceLayoutValidity == null ? 0 : glyphCount);
            Placements = new TextStudioReadOnlyBuffer<GlyphPlacement>(placements, placementCount);
            PlacementCount = placementCount;
            BasePositions = new TextStudioReadOnlyBuffer<Vector3>(
                basePositions,
                glyphCount);
            SourceColors = new TextStudioReadOnlyBuffer<TextStudioGlyphColorQuad>(
                sourceColors,
                glyphCount);
            SourceColorBounds = new TextStudioReadOnlyBuffer<TextStudioGlyphColorBounds>(
                sourceColorBounds,
                glyphCount);
            EffectUnitScales = new TextStudioReadOnlyBuffer<float>(
                effectUnitScales,
                glyphCount);
            PlacementMode = placementMode;
            Animated = new TextStudioReadOnlyBuffer<EvaluatedGlyphState>(animated, animatedCount);
            AnimatedCount = animatedCount;
            AnimatedStateIsUniform = animatedStateIsUniform;
            RenderProperties = renderProperties;
            RendererEffectExecution =
                new TextStudioRendererEffectExecutionView(
                    rendererOwnedEffectBindings,
                    rendererOwnedEffectBindingCount);
            AppearanceStyle = appearanceStyle;
            AppearancePlan = appearancePlan;
            AppearanceBinding = null;
            AppearanceOccurrenceIndex = -1;
            AppearanceProgramIndex = -1;
            WritingStyle = writingStyle;
            Transitions = transitions;
            EraseWritingOnHide = eraseWritingOnHide;
            VisualSnapshot = new TextStudioVisualSnapshot(
                visualGlyphs,
                glyphCount,
                visualCapabilities,
                visualVersion,
                visualSourceBounds, fontSources, fontSourceCount, renderGlyphs, renderGlyphCount, unitToRenderGlyph, colorSources, colorSourceCount);
            Dirty = new TextStudioDirtyGlyphView(dirty);
            GlyphStates = new TextStudioReadOnlyBuffer<TextStudioGlyphInstanceState>(
                glyphStates,
                glyphStateCount);
            GlyphStateCount = glyphStateCount;
            PresentationGlyphs = new TextStudioReadOnlyBuffer<PresentationGlyph>(
                presentationGlyphs,
                presentationGlyphCount);
            PresentationGlyphCount = presentationGlyphCount;
            RevealProgress = revealProgress;
            GlobalDirty = globalDirty;
            DeltaTime = deltaTime;
            Time = time;
            MotionPolicy = TextStudioMotionSettings.Resolve(motionPolicy);
            PresentationTime = float.IsNaN(presentationTime) ||
                               float.IsInfinity(presentationTime)
                ? time
                : presentationTime;
            PlaybackTime = float.IsNaN(playbackTime) || float.IsInfinity(playbackTime)
                ? time
                : playbackTime;
        }

        internal TextStudioFrame(
            in TextStudioFrame source,
            TextStudioGlyphInstanceState[] glyphStates,
            int glyphStateCount,
            GlyphDirtyBits dirty,
            DirtyFlags globalDirty,
            TextStudioRenderPropertyView renderProperties)
        {
            Plan = source.Plan;
            Placements = source.Placements;
            PlacementCount = source.PlacementCount;
            BasePositions = source.BasePositions;
            SourceLayoutValidity = source.SourceLayoutValidity;
            SourceColors = source.SourceColors;
            SourceColorBounds = source.SourceColorBounds;
            EffectUnitScales = source.EffectUnitScales;
            PlacementMode = source.PlacementMode;
            Animated = source.Animated;
            AnimatedCount = source.AnimatedCount;
            AnimatedStateIsUniform = source.AnimatedStateIsUniform;
            RenderProperties = renderProperties;
            RendererEffectExecution = source.RendererEffectExecution;
            AppearanceStyle = source.AppearanceStyle;
            AppearancePlan = source.AppearancePlan;
            AppearanceBinding = source.AppearanceBinding;
            AppearanceOccurrenceIndex = source.AppearanceOccurrenceIndex;
            AppearanceProgramIndex = source.AppearanceProgramIndex;
            WritingStyle = source.WritingStyle;
            Transitions = source.Transitions;
            EraseWritingOnHide = source.EraseWritingOnHide;
            VisualSnapshot = source.VisualSnapshot;
            Dirty = new TextStudioDirtyGlyphView(dirty);
            GlyphStates = new TextStudioReadOnlyBuffer<TextStudioGlyphInstanceState>(
                glyphStates,
                glyphStateCount);
            GlyphStateCount = glyphStateCount;
            PresentationGlyphs = source.PresentationGlyphs;
            PresentationGlyphCount = source.PresentationGlyphCount;
            RevealProgress = source.RevealProgress;
            GlobalDirty = globalDirty;
            DeltaTime = source.DeltaTime;
            Time = source.Time;
            MotionPolicy = source.MotionPolicy;
            PresentationTime = source.PresentationTime;
            PlaybackTime = source.PlaybackTime;
        }

        /// <summary>Gets backend source corner colors for a glyph, falling back to opaque white.</summary>
        public TextStudioGlyphColorQuad GetSourceColors(int glyphIndex)
        {
            return SourceColors.IsCreated &&
                   glyphIndex >= 0 &&
                   glyphIndex < SourceColors.Length
                ? SourceColors[glyphIndex]
                : TextStudioGlyphColorQuad.White;
        }

        /// <summary>Gets the positive backend conversion scale for normalized effect units.</summary>
        public float GetEffectUnitScale(int glyphIndex, float fallback = 1f)
        {
            float scale = EffectUnitScales.IsCreated &&
                          glyphIndex >= 0 &&
                          glyphIndex < EffectUnitScales.Length
                ? EffectUnitScales[glyphIndex]
                : fallback;
            return Mathf.Max(0.0001f, scale);
        }

        /// <summary>Gets source-color sampling bounds, or an invalid default when unavailable.</summary>
        public TextStudioGlyphColorBounds GetSourceColorBounds(int glyphIndex)
        {
            return SourceColorBounds.IsCreated &&
                   glyphIndex >= 0 &&
                   glyphIndex < SourceColorBounds.Length
                ? SourceColorBounds[glyphIndex]
                : default;
        }

        internal TextStudioFrame(in TextStudioFrame source, CompiledTextStudio2DStyle style, int occurrenceIndex,
            TextStudio2DAppearanceBinding binding, int programIndex = -1)
        {
            this = source;
            AppearanceStyle = style;
            AppearanceBinding = binding;
            AppearanceOccurrenceIndex = occurrenceIndex;
            AppearanceProgramIndex = programIndex >= 0 ? programIndex : occurrenceIndex >= 0
                ? binding.Plan.Occurrences[occurrenceIndex].ProgramIndex : -1;
        }

        /// <summary>Gets the typed renderer-property values resolved for one glyph.</summary>
        public TextStudioRenderPropertyGlyphView GetRenderProperties(int glyphIndex)
        {
            return RenderProperties.GetGlyph(glyphIndex);
        }
    }
}
