namespace TextStudio
{
    /// <summary>
    /// Base interface for all text effects.
    /// Effects modify glyph states through a channel-based evaluation model
    /// with explicit blend modes (not accumulation-only like Text Animator).
    /// </summary>
    public interface ITextStudioEffectDefinition
    {
        string Id { get; }

        TextStudioEffectAreaSettings DefaultArea { get; }
    }

    /// <summary>
    /// Runtime leaf evaluator. Expansion-only definitions such as Composed Effects deliberately
    /// do not implement this interface and are flattened into leaf evaluators before binding.
    /// </summary>
    public interface ITextStudioEffect : ITextStudioEffectDefinition
    {

        /// <summary>
        /// Evaluate the effect for a single glyph.
        /// </summary>
        /// <param name="state">Current glyph state (read/write).</param>
        /// <param name="context">Evaluation context with timing, index, and effect area data.</param>
        void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context);

        /// <summary>
        /// Reset any internal state. Called when text changes or effect is recycled.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Creates an isolated leaf evaluator for one effect invocation. Factories are definitions,
    /// never invocation state, and are called only while a plan or external-effect cache is bound.
    /// </summary>
    public interface ITextStudioEffectFactory : ITextStudioEffectDefinition
    {
        /// <summary>Creates an invocation-specific evaluator from parsed tag parameters.</summary>
        /// <param name="parameters">Read-only parsed parameters for one effect occurrence.</param>
        /// <returns>A detached evaluator, or <see langword="null"/> when binding fails.</returns>
        ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters);
    }

    /// <summary>
    /// Context passed to effect evaluation. Contains all timing and effect area information
    /// needed for effect computation without accessing global state.
    /// </summary>
    public readonly struct EffectEvaluationContext
    {
        /// <summary>Index of the glyph within visible glyph array.</summary>
        public readonly int glyphIndex;

        /// <summary>Total visible glyph count.</summary>
        public readonly int totalGlyphs;

        /// <summary>Word index the glyph belongs to.</summary>
        public readonly int wordIndex;

        /// <summary>Line index the glyph belongs to.</summary>
        public readonly int lineIndex;

        /// <summary>Current animation phase for this glyph.</summary>
        public readonly AnimationPhase phase;

        /// <summary>Progress within the current phase (0–1).</summary>
        public readonly float phaseProgress;

        /// <summary>Time since text controller started (seconds).</summary>
        public readonly float time;

        /// <summary>
        /// Time since the current effect invocation started (seconds). This normally matches
        /// <see cref="time"/>, but previews may restart finite playback without resetting the
        /// continuous clock used by oscillators and procedural motion.
        /// </summary>
        public readonly float playbackTime;

        /// <summary>
        /// Whether the effect clock continues from the preceding adjacent enabled phase.
        /// </summary>
        internal readonly bool usesContinuousPhaseClock;

        /// <summary>Delta time since last evaluation (seconds).</summary>
        public readonly float deltaTime;

        /// <summary>Normalized position of glyph within the current effect binding (0–1).</summary>
        public readonly float normalizedIndex;

        public readonly TextStudioEffectAreaMode area;
        public readonly int groupSize;
        public readonly int unitIndex;
        public readonly int unitCount;
        public readonly int unitStartGlyph;
        public readonly int unitGlyphCount;
        public readonly int indexInUnit;
        public readonly float normalizedIndexInUnit;
        public readonly float normalizedUnitIndex;
        public readonly float normalizedIndexInBinding;
        public readonly int randomSeedIndex;
        public readonly float playbackOffset;

        /// <summary>
        /// Local text-space size of one effect unit for this glyph. Effects that derive local
        /// geometry offsets from spatial coordinates divide by this value before writing them.
        /// </summary>
        public readonly float effectUnitScale;

        /// <summary>Glyph center from the source TMP layout, before placement.</summary>
        public readonly UnityEngine.Vector3 sourcePosition;

        /// <summary>
        /// Glyph center normalized inside the complete source-text visual bounds.
        /// This remains stable when placement, animation, or object transforms move the glyph.
        /// </summary>
        public readonly UnityEngine.Vector3 sourceTextNormalizedPosition;

        /// <summary>Immutable center sample of the glyph's original four-corner source color.</summary>
        public readonly UnityEngine.Color sourceColor;

        /// <summary>Whether the active renderer supplied an original source-color sample.</summary>
        public readonly bool hasSourceColor;

        /// <summary>Glyph center after the active Text Studio placement.</summary>
        public readonly UnityEngine.Vector3 spatialPosition;

        /// <summary>Placed glyph center transformed into world space by the owning hub.</summary>
        public readonly UnityEngine.Vector3 worldPosition;

        /// <summary>Whether the owning hub currently supplies a shared spatial effect target.</summary>
        public readonly bool hasInfluenceTarget;

        /// <summary>Shared effect target transformed into the owning hub's local text space.</summary>
        public readonly UnityEngine.Vector3 influenceTargetLocalPosition;

        /// <summary>Shared effect target normalized inside the complete source-text bounds.</summary>
        internal readonly UnityEngine.Vector3 influenceTargetSourceTextNormalizedPosition;

        /// <summary>Shared effect target in world space.</summary>
        public readonly UnityEngine.Vector3 influenceTargetWorldPosition;

        /// <summary>Normalized playback value supplied by an authored progress-driving layer.</summary>
        public readonly float effectProgress;

        /// <summary>Whether <see cref="effectProgress"/> is authoritative for this invocation.</summary>
        public readonly bool hasEffectProgress;

        /// <summary>Aggregate source-layout visual bounds for the resolved effect area unit.</summary>
        public readonly TextStudioGlyphVisualBounds sourceAreaBounds;

        /// <summary>Whether aggregate source-layout visual bounds are available.</summary>
        public readonly bool hasSourceAreaBounds;

        readonly TextStudioRenderPropertyWriter _renderProperties;

        /// <summary>Scoped typed renderer-property writer for this glyph evaluation.</summary>
        public TextStudioRenderPropertyWriter RenderProperties => _renderProperties;

        /// <summary>Creates evaluation context without extended source-unit metadata.</summary>
        public EffectEvaluationContext(
            int glyphIndex, int totalGlyphs,
            int wordIndex, int lineIndex,
            AnimationPhase phase, float phaseProgress,
            float time, float deltaTime,
            float normalizedIndex)
            : this(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                time,
                deltaTime,
                normalizedIndex,
                TextStudioEffectUnitContext.SingleGlyph(glyphIndex, totalGlyphs))
        {
        }

        /// <summary>Creates evaluation context with renderer-neutral source-unit metadata.</summary>
        public EffectEvaluationContext(
            int glyphIndex, int totalGlyphs,
            int wordIndex, int lineIndex,
            AnimationPhase phase, float phaseProgress,
            float time, float deltaTime,
            float normalizedIndex,
            in TextStudioEffectUnitContext unit)
            : this(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                time,
                deltaTime,
                normalizedIndex,
                in unit)
        {
        }

        /// <summary>Creates complete evaluation context including source-text normalized position.</summary>
        public EffectEvaluationContext(
            int glyphIndex, int totalGlyphs,
            int wordIndex, int lineIndex,
            AnimationPhase phase, float phaseProgress,
            float time, float playbackTime, float deltaTime,
            float normalizedIndex,
            in TextStudioEffectUnitContext unit)
            : this(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                in unit,
                phase == AnimationPhase.Persistent,
                new UnityEngine.Vector3(normalizedIndex, 0f, 0f),
                new UnityEngine.Vector3(normalizedIndex, 0f, 0f),
                UnityEngine.Color.white,
                false,
                new UnityEngine.Vector3(normalizedIndex, 0f, 0f),
                new UnityEngine.Vector3(normalizedIndex, 0f, 0f))
        {
        }

        EffectEvaluationContext(
            int glyphIndex, int totalGlyphs,
            int wordIndex, int lineIndex,
            AnimationPhase phase, float phaseProgress,
            float time, float playbackTime, float deltaTime,
            float normalizedIndex,
            in TextStudioEffectUnitContext unit,
            bool usesContinuousPhaseClock,
            UnityEngine.Vector3 sourcePosition,
            UnityEngine.Vector3 sourceTextNormalizedPosition,
            UnityEngine.Color sourceColor,
            bool hasSourceColor,
            UnityEngine.Vector3 spatialPosition,
            UnityEngine.Vector3 worldPosition,
            bool hasInfluenceTarget = false,
            UnityEngine.Vector3 influenceTargetLocalPosition = default,
            UnityEngine.Vector3 influenceTargetWorldPosition = default,
            UnityEngine.Vector3 influenceTargetSourceTextNormalizedPosition = default,
            float effectProgress = 0f,
            bool hasEffectProgress = false,
            TextStudioGlyphVisualBounds sourceAreaBounds = default,
            bool hasSourceAreaBounds = false,
            TextStudioRenderPropertyWriter renderProperties = default,
            float effectUnitScale = 1f)
        {
            this.glyphIndex = glyphIndex;
            this.totalGlyphs = totalGlyphs;
            this.wordIndex = wordIndex;
            this.lineIndex = lineIndex;
            this.phase = phase;
            this.phaseProgress = phaseProgress;
            this.time = time;
            this.playbackTime = playbackTime;
            this.usesContinuousPhaseClock = usesContinuousPhaseClock;
            this.deltaTime = deltaTime;
            this.normalizedIndex = normalizedIndex;
            area = unit.area;
            groupSize = unit.groupSize;
            unitIndex = unit.unitIndex;
            unitCount = unit.unitCount;
            unitStartGlyph = unit.unitStartGlyph;
            unitGlyphCount = unit.unitGlyphCount;
            indexInUnit = unit.indexInUnit;
            normalizedIndexInUnit = unit.normalizedIndexInUnit;
            normalizedUnitIndex = unit.normalizedUnitIndex;
            normalizedIndexInBinding = unit.normalizedIndexInBinding;
            randomSeedIndex = unit.randomSeedIndex;
            playbackOffset = unit.playbackOffset;
            this.effectUnitScale = UnityEngine.Mathf.Max(0.0001f, effectUnitScale);
            this.sourcePosition = sourcePosition;
            this.sourceTextNormalizedPosition = sourceTextNormalizedPosition;
            this.sourceColor = sourceColor;
            this.hasSourceColor = hasSourceColor;
            this.spatialPosition = spatialPosition;
            this.worldPosition = worldPosition;
            this.hasInfluenceTarget = hasInfluenceTarget;
            this.influenceTargetLocalPosition = influenceTargetLocalPosition;
            this.influenceTargetWorldPosition = influenceTargetWorldPosition;
            this.influenceTargetSourceTextNormalizedPosition =
                influenceTargetSourceTextNormalizedPosition;
            this.effectProgress = effectProgress;
            this.hasEffectProgress = hasEffectProgress;
            this.sourceAreaBounds = sourceAreaBounds;
            this.hasSourceAreaBounds = hasSourceAreaBounds;
            _renderProperties = renderProperties;
        }

        internal EffectEvaluationContext WithSpatialPositions(
            UnityEngine.Vector3 source,
            UnityEngine.Vector3 spatial,
            UnityEngine.Vector3 world)
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                source,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatial,
                world,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithInfluenceTarget(
            UnityEngine.Vector3 localPosition,
            UnityEngine.Vector3 worldPosition,
            UnityEngine.Vector3 targetSourceTextNormalizedPosition = default)
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                sourcePosition,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatialPosition,
                this.worldPosition,
                true,
                localPosition,
                worldPosition,
                targetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithSourceContext(
            UnityEngine.Vector3 normalizedPosition,
            UnityEngine.Color color,
            bool hasColor)
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                sourcePosition,
                normalizedPosition,
                color,
                hasColor,
                spatialPosition,
                worldPosition,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithContinuousPhaseClock()
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                true,
                sourcePosition,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatialPosition,
                worldPosition,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithEffectProgress(float progress)
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                sourcePosition,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatialPosition,
                worldPosition,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                UnityEngine.Mathf.Clamp01(progress),
                true,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithSourceAreaBounds(
            in TextStudioGlyphVisualBounds bounds,
            bool hasBounds)
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                sourcePosition,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatialPosition,
                worldPosition,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                bounds,
                hasBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithTimeScale(float scale)
        {
            float resolvedScale = UnityEngine.Mathf.Max(0f, scale);
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time * resolvedScale,
                playbackTime * resolvedScale,
                deltaTime * resolvedScale,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                sourcePosition,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatialPosition,
                worldPosition,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                effectUnitScale);
        }

        internal EffectEvaluationContext WithRenderProperties(
            in TextStudioRenderPropertyWriter renderProperties)
        {
            return new EffectEvaluationContext(in this, in renderProperties);
        }

        /// <summary>Returns this context with a glyph's positions, effect unit scale, source samples and area bounds.</summary>
        internal EffectEvaluationContext WithSpatialSource(
            UnityEngine.Vector3 source,
            UnityEngine.Vector3 spatial,
            UnityEngine.Vector3 world,
            float effectUnitScale,
            UnityEngine.Vector3 sourceTextNormalizedPosition,
            UnityEngine.Color sourceColor,
            bool hasSourceColor,
            in TextStudioGlyphVisualBounds sourceAreaBounds,
            bool hasSourceAreaBounds)
        {
            return new EffectEvaluationContext(
                in this,
                source,
                spatial,
                world,
                effectUnitScale,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                in sourceAreaBounds,
                hasSourceAreaBounds);
        }

        // Per-glyph evaluation derives contexts by copying the source and replacing only the changed fields, so each
        // derivation is one struct copy instead of a rebuild of every field and the unit context.
        EffectEvaluationContext(
            in EffectEvaluationContext source,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            this = source;
            _renderProperties = renderProperties;
        }

        EffectEvaluationContext(
            in EffectEvaluationContext source,
            UnityEngine.Vector3 sourcePosition,
            UnityEngine.Vector3 spatialPosition,
            UnityEngine.Vector3 worldPosition,
            float effectUnitScale,
            UnityEngine.Vector3 sourceTextNormalizedPosition,
            UnityEngine.Color sourceColor,
            bool hasSourceColor,
            in TextStudioGlyphVisualBounds sourceAreaBounds,
            bool hasSourceAreaBounds)
        {
            this = source;
            this.sourcePosition = sourcePosition;
            this.spatialPosition = spatialPosition;
            this.worldPosition = worldPosition;
            this.effectUnitScale = UnityEngine.Mathf.Max(0.0001f, effectUnitScale);
            this.sourceTextNormalizedPosition = sourceTextNormalizedPosition;
            this.sourceColor = sourceColor;
            this.hasSourceColor = hasSourceColor;
            this.sourceAreaBounds = sourceAreaBounds;
            this.hasSourceAreaBounds = hasSourceAreaBounds;
        }

        internal EffectEvaluationContext WithEffectUnitScale(float scale)
        {
            return new EffectEvaluationContext(
                glyphIndex,
                totalGlyphs,
                wordIndex,
                lineIndex,
                phase,
                phaseProgress,
                time,
                playbackTime,
                deltaTime,
                normalizedIndex,
                new TextStudioEffectUnitContext(
                    area,
                    groupSize,
                    unitIndex,
                    unitCount,
                    unitStartGlyph,
                    unitGlyphCount,
                    indexInUnit,
                    normalizedIndexInBinding,
                    playbackOffset),
                usesContinuousPhaseClock,
                sourcePosition,
                sourceTextNormalizedPosition,
                sourceColor,
                hasSourceColor,
                spatialPosition,
                worldPosition,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                effectProgress,
                hasEffectProgress,
                sourceAreaBounds,
                hasSourceAreaBounds,
                _renderProperties,
                scale);
        }
    }
}
