using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Placement-agnostic orientation override. Placement Default preserves the
    /// strategy-authored rotation while the other policies reinterpret its frame.
    /// </summary>
    [System.Serializable]
    public class PlacementOrientation
    {
        [Tooltip("How glyphs orient after placement. Placement Default preserves the strategy's natural facing.")]
        public TextStudioPlacementOrientation orientation = TextStudioPlacementOrientation.PlacementDefault;

        [Tooltip("Additional local Euler rotation in degrees, applied after the selected orientation.")]
        public Vector3 fineRotation;

        public bool HasEffect =>
            orientation != TextStudioPlacementOrientation.PlacementDefault ||
            fineRotation.sqrMagnitude > 0.000001f;

        /// <summary>
        /// Resolves the glyph rotation for the selected orientation policy, then
        /// applies <see cref="fineRotation"/> as a local Euler adjustment in degrees.
        /// </summary>
        public Quaternion Apply(in GlyphPlacement placement)
        {
            Quaternion baseRotation;
            switch (orientation)
            {
                case TextStudioPlacementOrientation.KeepOriginal:
                    baseRotation = Quaternion.identity;
                    break;
                case TextStudioPlacementOrientation.FollowPath:
                    baseRotation = AlignLocalRight(placement.tangent, placement.up);
                    break;
                case TextStudioPlacementOrientation.FaceOutward:
                    baseRotation = IsPlanarFrame(in placement)
                        ? AlignPlanarUp(placement.outward)
                        : LookAlongSurfaceNormal(-placement.outward);
                    break;
                case TextStudioPlacementOrientation.FaceInward:
                    baseRotation = IsPlanarFrame(in placement)
                        ? AlignPlanarUp(-placement.outward)
                        : LookAlongSurfaceNormal(placement.outward);
                    break;
                case TextStudioPlacementOrientation.PlacementDefault:
                default:
                    baseRotation = placement.rotation;
                    break;
            }

            return fineRotation.sqrMagnitude > 0.000001f
                ? baseRotation * Quaternion.Euler(fineRotation)
                : baseRotation;
        }

        internal static Quaternion AlignLocalRight(Vector3 tangent, Vector3 preferredUp)
        {
            Vector3 right = SafeDirection(tangent, Vector3.right);
            Vector3 up = Vector3.ProjectOnPlane(preferredUp, right);
            if (up.sqrMagnitude <= 0.000001f)
                up = Vector3.ProjectOnPlane(Vector3.forward, right);
            up = SafeDirection(up, Vector3.up);
            Vector3 forward = SafeDirection(Vector3.Cross(right, up), Vector3.forward);
            return Quaternion.LookRotation(forward, up);
        }

        internal static Quaternion LookAlongSurfaceNormal(Vector3 forward)
        {
            Vector3 safeForward = SafeDirection(forward, Vector3.forward);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, safeForward);
            if (up.sqrMagnitude <= 0.000001f)
                up = Vector3.ProjectOnPlane(Vector3.right, safeForward);
            return Quaternion.LookRotation(safeForward, SafeDirection(up, Vector3.up));
        }

        static bool IsPlanarFrame(in GlyphPlacement placement)
        {
            return Mathf.Abs(placement.tangent.z) <= 0.0001f &&
                   Mathf.Abs(placement.up.z) <= 0.0001f &&
                   Mathf.Abs(placement.outward.z) <= 0.0001f;
        }

        static Quaternion AlignPlanarUp(Vector3 up)
        {
            Vector3 safeUp = SafeDirection(up, Vector3.up);
            return Quaternion.LookRotation(Vector3.forward, safeUp);
        }

        static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : fallback;
        }
    }

    /// <summary>
    /// Linear placement: glyphs stay at their layout positions (identity transform).
    /// </summary>
    public class LinearPlacement : IPlacementContextStrategy
    {
        public PlacementMode Mode => PlacementMode.Linear;

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        /// <summary>Writes placements for every visible glyph using its current layout and line metadata.</summary>
        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            for (int i = 0; i < context.glyphCount; i++)
            {
                placements[i] = GlyphPlacement.Default(context.layoutPositions[i]);
                if (i > 0)
                {
                    placements[i].pathProgress = (float)i / (context.glyphCount - 1);
                }
            }
        }
    }

    /// <summary>
    /// Arc placement: distributes glyphs along a circular arc.
    /// Only controls position + path frame. Glyph rotation is handled globally by the
    /// hub's <see cref="PlacementOrientation"/> module.
    /// </summary>
    [System.Serializable]
    public class ArcPlacement : IPlacementContextStrategy
    {
        [Tooltip("Radius of the arc.")]
        public float radius = 5f;

        [Tooltip("Automatically increases the effective radius when the current text layout would overlap on the authored radius.")]
        public bool autoFitRadius = true;

        [Tooltip("Extra spacing applied by Auto Fit Radius. 1 = exact fit, higher values add breathing room.")]
        [Range(1f, 2f)]
        public float autoFitPadding = 1.08f;

        [Tooltip("Total arc angle in degrees. 360° = full circle.")]
        [Range(1f, 360f)]
        public float arcAngle = 160f;

        [Tooltip("Start angle offset in degrees (0 = top, then clockwise). " +
                 "The completed arc remains centered on the authored text layout.")]
        [Range(-360f, 360f)]
        public float startAngle = -80f;

        [Tooltip("Controls whether glyphs sit above the arc on their source baseline, remain centered on it, or sit below it by their visible top edge.")]
        public PathPosition pathPosition = PathPosition.Above;

        [Tooltip("How glyphs are spaced along the arc.\n" +
                 "• Even: equal arc-length per glyph - the designer-friendly default, " +
                 "stable regardless of text or arc size.\n" +
                 "• Proportional: follows the source TMP layout spacing (keeps wide " +
                 "spaces wide). Useful when exact typographic spacing matters, but can " +
                 "make letters clump and spaces balloon on tight arcs.")]
        public PathSpacing spacing = PathSpacing.Even;

        [Tooltip("When Spacing = Proportional, how much of the arc to actually fill. " +
                 "1 = fill the entire arc (edges touch), 0.5 = cluster around the center, " +
                 "etc. Ignored when Spacing = Even.")]
        [Range(0.1f, 1f)]
        public float fill = 1f;

        [Tooltip("Reverses progression around the arc.")]
        public bool reverse;

        [Tooltip("Signed radius change from the first glyph to the last. Positive values spiral outward.")]
        public float radiusChange;

        public PlacementMode Mode => PlacementMode.Arc;

        // Pooled advances array to avoid per-call allocation
        float[] _advances;
        [System.NonSerialized] bool _guideValid;
        [System.NonSerialized] float _guideEffectiveBaseRadius;
        [System.NonSerialized] Vector3 _guideAlignmentOffset;
        [System.NonSerialized] float[] _guideLineRadii;
        [System.NonSerialized] int _guideLineCount;

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        /// <summary>Writes placements for every visible glyph using its current layout and line metadata.</summary>
        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            int glyphCount = context.glyphCount;
            _guideValid = false;
            if (glyphCount == 0) return;

            float totalRad = arcAngle * Mathf.Deg2Rad * (reverse ? -1f : 1f);
            float startRad = startAngle * Mathf.Deg2Rad;
            Vector3 sourceCenter = PlacementLineUtility.ComputeSourceCenter(in context);
            Vector3 sourceAnchor = PlacementLineUtility.ComputeSourceAnchor(in context);
            float sourceCenterY = sourceCenter.y;
            float effectiveBaseRadius = ResolveEffectiveBaseRadius(in context, totalRad, sourceCenterY);
            CacheGuideLineRadii(in context, sourceCenterY, effectiveBaseRadius);

            for (int i = 0; i < glyphCount; i++)
                placements[i] = GlyphPlacement.Default(context.layoutPositions[i]);

            if (_advances == null || _advances.Length < glyphCount)
                _advances = new float[TextStudioBufferCapacity.ForCount(glyphCount)];

            if (context.lineRanges != null && context.lineCount > 0)
            {
                float sourceLeft = float.PositiveInfinity, sourceRight = float.NegativeInfinity;
                for (int index = 0; index < context.lineCount; index++)
                {
                    LineRange sourceLine = context.lineRanges[index];
                    int first = Mathf.Clamp(sourceLine.glyphStart, 0, glyphCount);
                    int count = Mathf.Clamp(sourceLine.glyphCount, 0, glyphCount - first);
                    for (int glyph = first; glyph < first + count; glyph++)
                    {
                        sourceLeft = Mathf.Min(sourceLeft, context.layoutPositions[glyph].x);
                        sourceRight = Mathf.Max(sourceRight, context.layoutPositions[glyph].x);
                    }
                }
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, glyphCount);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, glyphCount - lineStart);
                    if (lineGlyphCount <= 0)
                        continue;

                    float lineRadius = _guideLineRadii != null &&
                                       lineIndex < _guideLineRadii.Length
                        ? _guideLineRadii[lineIndex]
                        : Mathf.Max(
                            0.0001f,
                            effectiveBaseRadius + PlacementLineUtility.ComputeLineRadialOffset(
                                in context,
                                lineStart,
                                lineGlyphCount,
                                sourceCenterY));
                    ComputeArcRun(
                        context.layoutPositions,
                        placements,
                        lineStart,
                        lineGlyphCount,
                        startRad,
                        totalRad,
                        lineRadius,
                        sourceLeft,
                        sourceRight);
                }
            }
            else
            {
                ComputeArcRun(
                    context.layoutPositions,
                    placements,
                    0,
                    glyphCount,
                    startRad,
                    totalRad,
                    Mathf.Max(0.0001f, effectiveBaseRadius));
            }

            _guideAlignmentOffset =
                PlacementLineUtility.AlignPlacements(in context, placements, sourceAnchor);
            PathPlacementUtility.Apply(in context, placements, pathPosition);
            _guideEffectiveBaseRadius = effectiveBaseRadius;
            _guideLineCount = context.lineRanges != null ? context.lineCount : 0;
            _guideValid = true;
        }

        void CacheGuideLineRadii(
            in PlacementContext context,
            float sourceCenterY,
            float effectiveBaseRadius)
        {
            if (context.lineRanges == null || context.lineCount <= 0)
            {
                _guideLineRadii = null;
                return;
            }

            if (_guideLineRadii == null || _guideLineRadii.Length != context.lineCount)
                _guideLineRadii = new float[context.lineCount];

            for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
            {
                LineRange line = context.lineRanges[lineIndex];
                int lineStart = Mathf.Clamp(line.glyphStart, 0, context.glyphCount);
                int lineGlyphCount = Mathf.Clamp(
                    line.glyphCount,
                    0,
                    context.glyphCount - lineStart);
                _guideLineRadii[lineIndex] = lineGlyphCount > 0
                    ? Mathf.Max(
                        0.0001f,
                        effectiveBaseRadius + PlacementLineUtility.ComputeLineRadialOffset(
                            in context,
                            lineStart,
                            lineGlyphCount,
                            sourceCenterY))
                    : Mathf.Max(0.0001f, effectiveBaseRadius);
            }
        }

        internal int GuideRunCount =>
            _guideValid ? Mathf.Max(1, _guideLineCount) : 0;

        internal bool TryGetGuide(int runIndex, out CircularPlacementGuide guide)
        {
            guide = default;
            if (runIndex < 0 || runIndex >= GuideRunCount) return false;
            float resolvedRadius = _guideLineCount > 0 && _guideLineRadii != null && runIndex < _guideLineRadii.Length
                ? _guideLineRadii[runIndex] : _guideEffectiveBaseRadius;
            guide = new CircularPlacementGuide(Mathf.Max(.0001f, resolvedRadius), startAngle,
                (reverse ? -1f : 1f) * Mathf.Clamp(arcAngle, 1f, 360f), radiusChange, _guideAlignmentOffset);
            return true;
        }

        internal bool TryBuildGuidePoints(int runIndex, Vector3[] points, out float resolvedRadius)
        {
            resolvedRadius = 0;
            if (points == null || points.Length < 2 || !TryGetGuide(runIndex, out CircularPlacementGuide guide)) return false;
            resolvedRadius = guide.Radius;
            for (int i = 0; i < points.Length; i++) points[i] = guide.Evaluate((float)i / (points.Length - 1), out _);
            return true;
        }
        float ResolveEffectiveBaseRadius(in PlacementContext context, float totalRad, float sourceCenterY)
        {
            if (!autoFitRadius)
                return radius;

            float padding = Mathf.Max(1f, autoFitPadding);
            float requiredBaseRadius = radius;
            if (context.lineRanges != null && context.lineCount > 0)
            {
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, context.glyphCount);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, context.glyphCount - lineStart);
                    if (lineGlyphCount <= 0)
                        continue;

                    float lineRadius = PlacementAutoFit.ComputeArcRadius(
                        context.layoutPositions,
                        lineStart,
                        lineGlyphCount,
                        totalRad,
                        context.autoFitGlyphMargin) * padding;
                    float radialOffset = PlacementLineUtility.ComputeLineRadialOffset(in context, lineStart, lineGlyphCount, sourceCenterY);
                    float lineInwardPathOffset = PathPlacementUtility.ComputeMaximumInwardOffset(
                        in context,
                        lineStart,
                        lineGlyphCount,
                        pathPosition);
                    requiredBaseRadius = Mathf.Max(
                        requiredBaseRadius,
                        lineRadius + lineInwardPathOffset - radialOffset - Mathf.Min(0f, radiusChange));
                }

                return requiredBaseRadius;
            }

            float computedAutoFitRadius = PlacementAutoFit.ComputeArcRadius(
                context.layoutPositions,
                0,
                context.glyphCount,
                totalRad,
                context.autoFitGlyphMargin);
            float inwardPathOffset = PathPlacementUtility.ComputeMaximumInwardOffset(
                in context,
                0,
                context.glyphCount,
                pathPosition);
            return Mathf.Max(
                radius,
                computedAutoFitRadius * padding + inwardPathOffset - Mathf.Min(0f, radiusChange));
        }

        void ComputeArcRun(
            Vector3[] layoutPositions,
            GlyphPlacement[] placements,
            int start,
            int count,
            float startRad,
            float totalRad,
            float effectiveRadius,
            float sourceLeft = float.NaN,
            float sourceRight = float.NaN)
        {
            // Only compute advances when we actually need them (proportional mode).
            float totalAdvance = 0f;
            if (spacing == PathSpacing.Proportional)
            {
                for (int i = 0; i < count; i++)
                {
                    _advances[i] = totalAdvance;
                    if (i + 1 < count)
                        totalAdvance += Vector3.Distance(layoutPositions[start + i], layoutPositions[start + i + 1]);
                }
            }

            for (int i = 0; i < count; i++)
            {
                float t;
                if (spacing == PathSpacing.Even || totalAdvance <= 0f)
                {
                    // Even arc-length per glyph. Centers glyphs across the full arc so
                    // edges are symmetrical when the arc isn't a full 360°.
                    t = count > 1 ? (float)i / (count - 1) : 0.5f;
                }
                else
                {
                    // Proportional spacing with a `fill` fraction so authors can pull
                    // the text away from the arc edges when the text is short.
                    float proportional = _advances[i] / totalAdvance;
                    t = (1f - fill) * 0.5f + proportional * fill;
                }
                // Preserve the source line's alignment and share the angular span
                // across the block instead of justifying every short line.
                if (float.IsFinite(sourceLeft) && sourceRight - sourceLeft > .00001f)
                {
                    float linePosition = Mathf.Lerp(layoutPositions[start].x, layoutPositions[start + count - 1].x, t);
                    t = Mathf.InverseLerp(sourceLeft, sourceRight, linePosition);
                }
                float angle = startRad + t * totalRad;
                float glyphRadius = Mathf.Max(0.0001f, effectiveRadius + radiusChange * t);

                float x = Mathf.Sin(angle) * glyphRadius;
                float y = Mathf.Cos(angle) * glyphRadius;

                float direction = totalRad < 0f ? -1f : 1f;
                float tx = Mathf.Cos(angle) * direction;
                float ty = -Mathf.Sin(angle) * direction;
                // Up points radially outward - the correct orientation for "standing on
                // the arc". The rotation module uses this when rotate=true.
                Vector3 upVec = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);

                placements[start + i] = new GlyphPlacement
                {
                    position = new Vector3(x, y, 0f),
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                    tangent = new Vector3(tx, ty, 0f),
                    up = upVec,
                    outward = upVec,
                    pathProgress = t
                };
            }
        }
    }

    /// <summary>
    /// Circle placement: distributes glyphs evenly around a full circle.
    /// Rotation is handled by the hub-level <see cref="PlacementOrientation"/> module.
    /// </summary>
    [System.Serializable]
    public class CirclePlacement : IPlacementContextStrategy
    {
        public float radius = 3f;

        [Tooltip("Automatically increases the effective radius when the current text layout would overlap on the authored radius.")]
        public bool autoFitRadius = true;

        [Tooltip("Extra spacing applied by Auto Fit Radius. 1 = exact fit, higher values add breathing room.")]
        [Range(1f, 2f)]
        public float autoFitPadding = 1.08f;

        [Tooltip("Start angle offset in degrees (0 = top).")]
        public float startAngle;

        [Tooltip("Controls whether glyphs sit above the circle on their source baseline, remain centered on it, or sit below it by their visible top edge.")]
        public PathPosition pathPosition = PathPosition.Above;

        [Tooltip("How glyphs are distributed around the circle.")]
        public PathSpacing spacing = PathSpacing.Even;

        [Tooltip("Reverses progression around the circle.")]
        public bool reverse;

        [Tooltip("Signed radius change from the first glyph to the last. Positive values spiral outward.")]
        public float radiusChange;

        [System.NonSerialized] bool _layoutMetricsCached;
        [System.NonSerialized] Vector3[] _cachedLayoutPositions;
        [System.NonSerialized] float[] _cachedSourceCenterBaselineOffsets;
        [System.NonSerialized] TextStudioGlyphVisualBounds[] _cachedSourceVisualBounds;
        [System.NonSerialized] LineRange[] _cachedLineRanges;
        [System.NonSerialized] int _cachedGlyphCount;
        [System.NonSerialized] int _cachedLineCount;
        [System.NonSerialized] float _cachedAutoFitGlyphMargin;
        [System.NonSerialized] float _cachedAuthoredRadius;
        [System.NonSerialized] float _cachedAutoFitPadding;
        [System.NonSerialized] bool _cachedAutoFitRadius;
        [System.NonSerialized] float _cachedRadiusChange;
        [System.NonSerialized] PathPosition _cachedPathPosition;
        [System.NonSerialized] Vector2 _cachedSourceAlignment;
        [System.NonSerialized] float _cachedSourceCenterY;
        [System.NonSerialized] Vector3 _cachedSourceAnchor;
        [System.NonSerialized] float _cachedEffectiveBaseRadius;
        [System.NonSerialized] Vector3 _guideAlignmentOffset;
        [System.NonSerialized] bool _guideValid;
        [System.NonSerialized] float[] _cachedLineRadii;
        [System.NonSerialized] float[] _advances;

        public PlacementMode Mode => PlacementMode.Circle;

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        /// <summary>Writes placements for every visible glyph using its current layout and line metadata.</summary>
        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            ComputePlacements(in context, placements, false);
        }

        internal void ComputePlacements(
            in PlacementContext context,
            GlyphPlacement[] placements,
            bool allowCachedLayoutMetrics)
        {
            int glyphCount = context.glyphCount;
            _guideValid = false;
            if (glyphCount == 0) return;

            float startRad = startAngle * Mathf.Deg2Rad;
            bool reuseLayoutMetrics = allowCachedLayoutMetrics &&
                                      _layoutMetricsCached &&
                                      ReferenceEquals(
                                          _cachedLayoutPositions,
                                          context.layoutPositions) &&
                                      ReferenceEquals(
                                          _cachedSourceCenterBaselineOffsets,
                                          context.sourceCenterBaselineOffsets) &&
                                      ReferenceEquals(
                                          _cachedSourceVisualBounds,
                                          context.sourceVisualBounds) &&
                                      ReferenceEquals(_cachedLineRanges, context.lineRanges) &&
                                      _cachedGlyphCount == glyphCount &&
                                      _cachedLineCount == context.lineCount &&
                                      _cachedAutoFitGlyphMargin.Equals(context.autoFitGlyphMargin) &&
                                      _cachedAuthoredRadius.Equals(radius) &&
                                       _cachedAutoFitPadding.Equals(autoFitPadding) &&
                                       _cachedAutoFitRadius == autoFitRadius &&
                                       _cachedRadiusChange.Equals(radiusChange) &&
                                       _cachedPathPosition == pathPosition &&
                                       _cachedSourceAlignment.Equals(context.sourceAlignment);
            float sourceCenterY;
            Vector3 sourceAnchor;
            float effectiveBaseRadius;
            if (reuseLayoutMetrics)
            {
                sourceCenterY = _cachedSourceCenterY;
                sourceAnchor = _cachedSourceAnchor;
                effectiveBaseRadius = _cachedEffectiveBaseRadius;
            }
            else
            {
                sourceCenterY = PlacementLineUtility.ComputeSourceCenterY(in context);
                sourceAnchor = PlacementLineUtility.ComputeSourceAnchor(in context);
                effectiveBaseRadius = ResolveEffectiveBaseRadius(in context, sourceCenterY);
                _layoutMetricsCached = true;
                _cachedLayoutPositions = context.layoutPositions;
                _cachedSourceCenterBaselineOffsets = context.sourceCenterBaselineOffsets;
                _cachedSourceVisualBounds = context.sourceVisualBounds;
                _cachedLineRanges = context.lineRanges;
                _cachedGlyphCount = glyphCount;
                _cachedLineCount = context.lineCount;
                _cachedAutoFitGlyphMargin = context.autoFitGlyphMargin;
                _cachedAuthoredRadius = radius;
                _cachedAutoFitPadding = autoFitPadding;
                _cachedAutoFitRadius = autoFitRadius;
                _cachedRadiusChange = radiusChange;
                _cachedPathPosition = pathPosition;
                _cachedSourceAlignment = context.sourceAlignment;
                _cachedSourceCenterY = sourceCenterY;
                _cachedSourceAnchor = sourceAnchor;
                _cachedEffectiveBaseRadius = effectiveBaseRadius;
                CacheLineRadii(in context, sourceCenterY, effectiveBaseRadius);
            }

            if (context.lineRanges != null && context.lineCount > 0)
            {
                int initializedUntil = 0;
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, glyphCount);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, glyphCount - lineStart);
                    if (lineGlyphCount <= 0)
                        continue;

                    for (int gapIndex = initializedUntil; gapIndex < lineStart; gapIndex++)
                        placements[gapIndex] = GlyphPlacement.Default(context.layoutPositions[gapIndex]);

                    float lineRadius = _cachedLineRadii != null &&
                                       lineIndex < _cachedLineRadii.Length
                        ? _cachedLineRadii[lineIndex]
                        : Mathf.Max(
                            0.0001f,
                            effectiveBaseRadius + PlacementLineUtility.ComputeLineRadialOffset(
                                in context,
                                lineStart,
                                lineGlyphCount,
                                sourceCenterY));
                    ComputeCircleRun(
                        context.layoutPositions,
                        placements,
                        lineStart,
                        lineGlyphCount,
                        startRad,
                        lineRadius);
                    initializedUntil = Mathf.Max(initializedUntil, lineStart + lineGlyphCount);
                }

                for (int gapIndex = initializedUntil; gapIndex < glyphCount; gapIndex++)
                    placements[gapIndex] = GlyphPlacement.Default(context.layoutPositions[gapIndex]);
            }
            else
            {
                ComputeCircleRun(
                    context.layoutPositions,
                    placements,
                    0,
                    glyphCount,
                    startRad,
                    Mathf.Max(0.0001f, effectiveBaseRadius));
            }

            _guideAlignmentOffset =
                PlacementLineUtility.AlignPlacements(in context, placements, sourceAnchor);
            PathPlacementUtility.Apply(in context, placements, pathPosition);
            _guideValid = true;
        }

        void CacheLineRadii(
            in PlacementContext context,
            float sourceCenterY,
            float effectiveBaseRadius)
        {
            if (context.lineRanges == null || context.lineCount <= 0)
            {
                _cachedLineRadii = null;
                return;
            }
            if (_cachedLineRadii == null || _cachedLineRadii.Length != context.lineCount)
                _cachedLineRadii = new float[context.lineCount];

            for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
            {
                LineRange line = context.lineRanges[lineIndex];
                int lineStart = Mathf.Clamp(line.glyphStart, 0, context.glyphCount);
                int lineGlyphCount = Mathf.Clamp(
                    line.glyphCount,
                    0,
                    context.glyphCount - lineStart);
                _cachedLineRadii[lineIndex] = lineGlyphCount > 0
                    ? Mathf.Max(
                        0.0001f,
                        effectiveBaseRadius + PlacementLineUtility.ComputeLineRadialOffset(
                            in context,
                            lineStart,
                            lineGlyphCount,
                            sourceCenterY))
                    : Mathf.Max(0.0001f, effectiveBaseRadius);
            }
        }

        internal int GuideRunCount =>
            _guideValid ? Mathf.Max(1, _cachedLineCount) : 0;

        internal bool TryGetGuide(int runIndex, out CircularPlacementGuide guide)
        {
            guide = default;
            if (runIndex < 0 || runIndex >= GuideRunCount) return false;
            float resolvedRadius = _cachedLineCount > 0 && _cachedLineRadii != null && runIndex < _cachedLineRadii.Length
                ? _cachedLineRadii[runIndex] : _cachedEffectiveBaseRadius;
            guide = new CircularPlacementGuide(Mathf.Max(.0001f, resolvedRadius), startAngle,
                (reverse ? -1f : 1f) * 360f, radiusChange, _guideAlignmentOffset);
            return true;
        }

        internal bool TryBuildGuidePoints(int runIndex, Vector3[] points, out float resolvedRadius)
        {
            resolvedRadius = 0;
            if (points == null || points.Length < 2 || !TryGetGuide(runIndex, out CircularPlacementGuide guide)) return false;
            resolvedRadius = guide.Radius;
            for (int i = 0; i < points.Length; i++) points[i] = guide.Evaluate((float)i / (points.Length - 1), out _);
            return true;
        }
        float ResolveEffectiveBaseRadius(in PlacementContext context, float sourceCenterY)
        {
            if (!autoFitRadius)
                return radius;

            float padding = Mathf.Max(1f, autoFitPadding);
            float requiredBaseRadius = radius;
            if (context.lineRanges != null && context.lineCount > 0)
            {
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, context.glyphCount);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, context.glyphCount - lineStart);
                    if (lineGlyphCount <= 0)
                        continue;

                    float lineRadius = PlacementAutoFit.ComputeCircleRadius(
                        context.layoutPositions,
                        lineStart,
                        lineGlyphCount,
                        context.autoFitGlyphMargin) * padding;
                    float radialOffset = PlacementLineUtility.ComputeLineRadialOffset(in context, lineStart, lineGlyphCount, sourceCenterY);
                    float lineInwardPathOffset = PathPlacementUtility.ComputeMaximumInwardOffset(
                        in context,
                        lineStart,
                        lineGlyphCount,
                        pathPosition);
                    requiredBaseRadius = Mathf.Max(
                        requiredBaseRadius,
                        lineRadius + lineInwardPathOffset - radialOffset - Mathf.Min(0f, radiusChange));
                }

                return requiredBaseRadius;
            }

            float computedAutoFitRadius = PlacementAutoFit.ComputeCircleRadius(
                context.layoutPositions,
                0,
                context.glyphCount,
                context.autoFitGlyphMargin);
            float inwardPathOffset = PathPlacementUtility.ComputeMaximumInwardOffset(
                in context,
                0,
                context.glyphCount,
                pathPosition);
            return Mathf.Max(
                radius,
                computedAutoFitRadius * padding + inwardPathOffset - Mathf.Min(0f, radiusChange));
        }

        void ComputeCircleRun(
            Vector3[] layoutPositions,
            GlyphPlacement[] placements,
            int start,
            int count,
            float startRad,
            float effectiveRadius)
        {
            if (_advances == null || _advances.Length < count)
                _advances = new float[TextStudioBufferCapacity.ForCount(count)];

            float totalAdvance = 0f;
            if (spacing == PathSpacing.Proportional && count > 1)
            {
                for (int i = 0; i < count; i++)
                {
                    _advances[i] = totalAdvance;
                    if (i + 1 < count)
                        totalAdvance += Vector3.Distance(
                            layoutPositions[start + i],
                            layoutPositions[start + i + 1]);
                }

                float averageAdvance = totalAdvance / (count - 1);
                totalAdvance += averageAdvance;
            }

            float direction = reverse ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                float angularProgress = spacing == PathSpacing.Proportional && totalAdvance > 0.000001f
                    ? _advances[i] / totalAdvance
                    : (float)i / count;
                float radiusProgress = count > 1 ? (float)i / (count - 1) : 0f;
                float angle = startRad + direction * angularProgress * Mathf.PI * 2f;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                float glyphRadius = Mathf.Max(
                    0.0001f,
                    effectiveRadius + radiusChange * radiusProgress);
                Vector3 outward = new Vector3(sin, cos, 0f);

                placements[start + i] = new GlyphPlacement
                {
                    position = outward * glyphRadius,
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                    tangent = new Vector3(cos * direction, -sin * direction, 0f),
                    up = outward,
                    outward = outward,
                    pathProgress = angularProgress
                };
            }
        }
    }

    internal static class PathPlacementUtility
    {
        public static void Apply(
            in PlacementContext context,
            GlyphPlacement[] placements,
            PathPosition pathPosition)
        {
            if (pathPosition == PathPosition.Centered ||
                placements == null ||
                context.layoutPositions == null)
            {
                return;
            }

            int count = Mathf.Min(
                context.glyphCount,
                Mathf.Min(placements.Length, context.layoutPositions.Length));
            for (int i = 0; i < count; i++)
            {
                float offset = ResolveOffset(in context, i, pathPosition);
                if (Mathf.Abs(offset) <= 0.000001f)
                    continue;

                placements[i].position += placements[i].up * offset;
            }
        }

        public static float ComputeMaximumInwardOffset(
            in PlacementContext context,
            int start,
            int count,
            PathPosition pathPosition)
        {
            if (pathPosition != PathPosition.Below ||
                context.layoutPositions == null ||
                count <= 0)
            {
                return 0f;
            }

            int safeStart = Mathf.Clamp(start, 0, context.layoutPositions.Length);
            int safeCount = Mathf.Min(count, context.layoutPositions.Length - safeStart);
            float maximum = 0f;
            for (int i = 0; i < safeCount; i++)
            {
                float offset = ResolveOffset(in context, safeStart + i, pathPosition);
                maximum = Mathf.Max(maximum, -offset);
            }

            return maximum;
        }

        static float ResolveOffset(
            in PlacementContext context,
            int glyphIndex,
            PathPosition pathPosition)
        {
            if (pathPosition == PathPosition.Above &&
                context.sourceCenterBaselineOffsets != null &&
                glyphIndex < context.sourceCenterBaselineOffsets.Length)
            {
                float baselineOffset = context.sourceCenterBaselineOffsets[glyphIndex];
                if (!float.IsNaN(baselineOffset) && !float.IsInfinity(baselineOffset))
                    return baselineOffset;
            }

            if (context.sourceVisualBounds == null ||
                glyphIndex >= context.sourceVisualBounds.Length)
            {
                return 0f;
            }

            TextStudioGlyphVisualBounds bounds = context.sourceVisualBounds[glyphIndex];
            if (!bounds.IsValid)
                return 0f;

            float centerY = context.layoutPositions[glyphIndex].y;
            return pathPosition switch
            {
                PathPosition.Above => Mathf.Max(0f, centerY - bounds.Minimum.y),
                PathPosition.Below => -Mathf.Max(0f, bounds.Maximum.y - centerY),
                _ => 0f
            };
        }
    }

    static class PlacementAutoFit
    {
        public static float ComputeArcRadius(Vector3[] layoutPositions, int glyphCount, float arcRadians, float glyphMargin = 0f)
        {
            return ComputeArcRadius(layoutPositions, 0, glyphCount, arcRadians, glyphMargin);
        }

        public static float ComputeArcRadius(Vector3[] layoutPositions, int start, int glyphCount, float arcRadians, float glyphMargin = 0f)
        {
            if (layoutPositions == null || glyphCount < 2 || arcRadians <= 1e-4f)
                return 0f;

            float totalAdvance = ComputeOpenAdvance(layoutPositions, start, glyphCount);
            if (totalAdvance <= 1e-4f)
                return 0f;

            float arcRadius = totalAdvance / arcRadians;
            int gaps = Mathf.Max(1, glyphCount - 1);
            float averageAdvance = totalAdvance / gaps;
            float stepRadians = arcRadians / gaps;
            float adjacentChordRadius = 0f;
            float sinStepHalf = Mathf.Sin(stepRadians * 0.5f);
            if (sinStepHalf > 1e-4f)
                adjacentChordRadius = averageAdvance / (2f * sinStepHalf);

            return Mathf.Max(arcRadius, adjacentChordRadius) + Mathf.Max(0f, glyphMargin);
        }

        public static float ComputeCircleRadius(Vector3[] layoutPositions, int glyphCount, float glyphMargin = 0f)
        {
            return ComputeCircleRadius(layoutPositions, 0, glyphCount, glyphMargin);
        }

        public static float ComputeCircleRadius(Vector3[] layoutPositions, int start, int glyphCount, float glyphMargin = 0f)
        {
            if (layoutPositions == null || glyphCount < 2)
                return 0f;

            float totalAdvance = ComputeOpenAdvance(layoutPositions, start, glyphCount);
            if (totalAdvance <= 1e-4f)
                return 0f;

            float averageAdvance = totalAdvance / Mathf.Max(1, glyphCount - 1);
            float sinStep = Mathf.Sin(Mathf.PI / glyphCount);
            float chordRadius = sinStep > 1e-4f ? averageAdvance / (2f * sinStep) : 0f;
            float circumferenceRadius = totalAdvance / (Mathf.PI * 2f);
            return Mathf.Max(chordRadius, circumferenceRadius) + Mathf.Max(0f, glyphMargin);
        }

        static float ComputeOpenAdvance(Vector3[] layoutPositions, int glyphCount)
        {
            return ComputeOpenAdvance(layoutPositions, 0, glyphCount);
        }

        static float ComputeOpenAdvance(Vector3[] layoutPositions, int start, int glyphCount)
        {
            int safeStart = Mathf.Clamp(start, 0, layoutPositions.Length);
            int count = Mathf.Min(glyphCount, layoutPositions.Length - safeStart);
            float total = 0f;
            for (int i = 0; i + 1 < count; i++)
                total += Vector3.Distance(layoutPositions[safeStart + i], layoutPositions[safeStart + i + 1]);
            return total;
        }
    }

    static class PlacementLineUtility
    {
        public static Vector3 ComputeSourceCenter(in PlacementContext context)
        {
            Vector3[] positions = context.layoutPositions;
            if (positions == null || context.glyphCount <= 0)
                return Vector3.zero;

            bool hasValue = false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;

            if (context.lineRanges != null && context.lineCount > 0)
            {
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, positions.Length);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, positions.Length - lineStart);
                    for (int i = 0; i < lineGlyphCount; i++)
                        if (context.HasSourceLayout(lineStart + i)) IncludePosition(positions[lineStart + i], ref hasValue, ref minimum, ref maximum);
                }
            }

            if (!hasValue)
            {
                int count = Mathf.Min(context.glyphCount, positions.Length);
                for (int i = 0; i < count; i++)
                    if (context.HasSourceLayout(i)) IncludePosition(positions[i], ref hasValue, ref minimum, ref maximum);
            }

            return hasValue ? (minimum + maximum) * 0.5f : Vector3.zero;
        }

        public static float ComputeSourceCenterY(in PlacementContext context)
        {
            return ComputeSourceCenter(in context).y;
        }

        public static Vector3 ComputeSourceAnchor(in PlacementContext context)
        {
            if (!TryComputeSourceBounds(in context, out Vector3 minimum, out Vector3 maximum))
                return Vector3.zero;

            return InterpolateBounds(minimum, maximum, context.sourceAlignment);
        }

        public static Vector3 AlignPlacements(
            in PlacementContext context,
            GlyphPlacement[] placements,
            Vector3 sourceAnchor)
        {
            if (placements == null || context.glyphCount <= 0)
                return Vector3.zero;

            bool hasValue = false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;

            if (context.lineRanges != null && context.lineCount > 0)
            {
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, placements.Length);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, placements.Length - lineStart);
                    for (int i = 0; i < lineGlyphCount; i++)
                        IncludePosition(placements[lineStart + i].position, ref hasValue, ref minimum, ref maximum);
                }
            }

            if (!hasValue)
            {
                int count = Mathf.Min(context.glyphCount, placements.Length);
                for (int i = 0; i < count; i++)
                    IncludePosition(placements[i].position, ref hasValue, ref minimum, ref maximum);
            }

            if (!hasValue)
                return Vector3.zero;

            Vector3 placementAnchor = InterpolateBounds(
                minimum,
                maximum,
                context.sourceAlignment);
            Vector3 offset = sourceAnchor - placementAnchor;
            if (offset.sqrMagnitude <= 0.000001f)
                return Vector3.zero;

            if (context.lineRanges != null && context.lineCount > 0)
            {
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, placements.Length);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, placements.Length - lineStart);
                    for (int i = 0; i < lineGlyphCount; i++)
                        placements[lineStart + i].position += offset;
                }
                return offset;
            }

            int placementCount = Mathf.Min(context.glyphCount, placements.Length);
            for (int i = 0; i < placementCount; i++)
                placements[i].position += offset;
            return offset;
        }

        static bool TryComputeSourceBounds(
            in PlacementContext context,
            out Vector3 minimum,
            out Vector3 maximum)
        {
            Vector3[] positions = context.layoutPositions;
            bool hasValue = false;
            minimum = Vector3.zero;
            maximum = Vector3.zero;
            if (positions == null || context.glyphCount <= 0)
                return false;

            if (context.lineRanges != null && context.lineCount > 0)
            {
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, positions.Length);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, positions.Length - lineStart);
                    for (int i = 0; i < lineGlyphCount; i++)
                        if (context.HasSourceLayout(lineStart + i)) IncludePosition(positions[lineStart + i], ref hasValue, ref minimum, ref maximum);
                }
            }

            if (!hasValue)
            {
                int count = Mathf.Min(context.glyphCount, positions.Length);
                for (int i = 0; i < count; i++)
                    if (context.HasSourceLayout(i)) IncludePosition(positions[i], ref hasValue, ref minimum, ref maximum);
            }

            return hasValue;
        }

        static Vector3 InterpolateBounds(Vector3 minimum, Vector3 maximum, Vector2 alignment)
        {
            return new Vector3(
                Mathf.LerpUnclamped(minimum.x, maximum.x, alignment.x),
                Mathf.LerpUnclamped(minimum.y, maximum.y, alignment.y),
                (minimum.z + maximum.z) * 0.5f);
        }

        public static float ComputeLineRadialOffset(in PlacementContext context, int start, int count, float sourceCenterY)
        {
            Vector3[] positions = context.layoutPositions;
            if (positions == null || count <= 0)
                return 0f;

            int safeStart = Mathf.Clamp(start, 0, positions.Length);
            int safeCount = Mathf.Min(count, positions.Length - safeStart);
            if (safeCount <= 0)
                return 0f;

            float minY = positions[safeStart].y;
            float maxY = minY;
            for (int i = 1; i < safeCount; i++)
            {
                float y = positions[safeStart + i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            return (minY + maxY) * 0.5f - sourceCenterY;
        }

        static void IncludePosition(
            Vector3 position,
            ref bool hasValue,
            ref Vector3 minimum,
            ref Vector3 maximum)
        {
            if (!hasValue)
            {
                minimum = position;
                maximum = position;
                hasValue = true;
                return;
            }

            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }
    }

    /// <summary>
    /// Wave placement: applies a sine wave offset to layout Y positions. The local
    /// tangent of the sine curve is stored in <c>tangent</c> so the hub's rotation
    /// module can align glyphs to the slope when <c>rotate</c> is on.
    ///
    /// <para>The Text Studio hub supplies its preview-controlled evaluation time so
    /// playback speed, pause, restart, scrubbing, and Raw authoring all produce
    /// deterministic placement. Direct strategy callers retain the real-time clock
    /// used by the original public placement contract.</para>
    /// </summary>
    [System.Serializable]
    public class WavePlacement : IPlacementContextStrategy
    {
        public float amplitude = 0.5f;
        public float frequency = 1f;
        public float phase;
        public float speed = 1f;
        [Tooltip("Depth amplitude in local units, a quarter cycle ahead of the vertical wave.")]
        public float depthAmplitude;
        [Tooltip("Continuous glyph roll in revolutions per second.")]
        public float spinSpeed;
        [Tooltip("Roll offset in revolutions from the first glyph to the last.")]
        public float spinStagger;

        internal bool IsAnimated =>
            ((Mathf.Abs(amplitude) > 0.0001f || Mathf.Abs(depthAmplitude) > 0.0001f) &&
             Mathf.Abs(speed) > 0.0001f) || Mathf.Abs(spinSpeed) > 0.0001f;

        public PlacementMode Mode => PlacementMode.Wave;

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        /// <summary>Writes placements for every visible glyph using its current layout and line metadata.</summary>
        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            ComputePlacements(in context, placements, Time.realtimeSinceStartup);
        }

        internal void ComputePlacements(
            in PlacementContext context,
            GlyphPlacement[] placements,
            float evaluationTime)
        {
            Vector3[] layoutPositions = context.layoutPositions;
            int glyphCount = context.glyphCount;
            float clock = Mathf.Max(0f, evaluationTime) * speed;
            clock = Mathf.Repeat(clock, 1000f); // Prevent float precision loss over long sessions
            float spin = Mathf.Repeat(Mathf.Max(0f, evaluationTime) * spinSpeed, 1f) * 360f;

            // Compute the world-space x-extent the text occupies, so we can convert
            // the parametric slope dy/dt into a real dy/dx slope. Without this
            // normalisation the slope is N-1 times too large and the glyph's "up"
            // vector ends up almost horizontal at wave peaks, which is why the
            // rotation looked wrong on tmp text.
            float totalX;
            if (glyphCount > 1)
            {
                float minX = layoutPositions[0].x;
                float maxX = minX;
                for (int i = 1; i < glyphCount; i++)
                {
                    float xi = layoutPositions[i].x;
                    if (xi < minX) minX = xi;
                    if (xi > maxX) maxX = xi;
                }
                totalX = Mathf.Max(1e-4f, maxX - minX);
            }
            else
            {
                totalX = 1f;
            }

            float dThetaDx = (frequency * Mathf.PI * 2f) / totalX;

            for (int i = 0; i < glyphCount; i++)
            {
                float t = (float)i / Mathf.Max(1, glyphCount - 1);
                float theta = (t * frequency + phase + clock) * Mathf.PI * 2f;
                float waveY = Mathf.Sin(theta) * amplitude;

                Vector3 pos = layoutPositions[i];
                pos.y += waveY;
                pos.z += Mathf.Cos(theta) * depthAmplitude;

                // True world-space slope dy/dx = d(A·sin(ω·x+φ))/dx = A·cos(θ)·ω
                // where ω = 2π · frequency / totalX. Include dz/dx in the tangent;
                // the xy normal remains perpendicular to this spatial tangent.
                float slope = amplitude * Mathf.Cos(theta) * dThetaDx;
                float depthSlope = -depthAmplitude * Mathf.Sin(theta) * dThetaDx;
                Vector3 tangent = new Vector3(1f, slope, depthSlope).normalized;
                Vector3 up = new Vector3(-slope, 1f, 0f).normalized;

                placements[i] = new GlyphPlacement
                {
                    position = pos,
                    rotation = Quaternion.Euler(0f, 0f, spin + t * spinStagger * 360f),
                    scale = Vector3.one,
                    tangent = tangent,
                    up = up,
                    outward = Vector3.Cross(up, tangent).normalized,
                    pathProgress = t
                };
            }
        }
    }

    /// <summary>
    /// Grid placement: arranges glyphs in a grid pattern.
    /// </summary>
    [System.Serializable]
    public class GridPlacement : IPlacementContextStrategy
    {
        [Min(1)]
        public int columns = 10;

        [Tooltip("Auto derives cell dimensions from rendered glyph bounds. Fixed uses Cell Size.")]
        public GridCellSizing cellSizing = GridCellSizing.Auto;

        [Tooltip("Authored cell dimensions used when Cell Sizing is Fixed.")]
        public Vector2 cellSize = new Vector2(0.6f, 1f);

        [Tooltip("Additional horizontal and vertical space between cells.")]
        public Vector2 gap;

        [Tooltip("Whether rows or columns fill first.")]
        public GridFlow flow = GridFlow.RowsFirst;

        [Tooltip("Reverses the fill direction.")]
        public bool reverse;

        [Tooltip("Starts each rendered source line on a new grid row. Applies to Rows First flow.")]
        public bool respectSourceLines;

        int[] _rows;
        int[] _columns;

        public PlacementMode Mode => PlacementMode.Grid;

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        /// <summary>Writes placements for every visible glyph using its current layout and line metadata.</summary>
        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            int cols = Mathf.Max(1, columns);
            int glyphCount = context.glyphCount;
            if (glyphCount <= 0)
                return;

            EnsureBuffers(glyphCount);
            BuildCellMap(in context, glyphCount, cols);
            Vector2 step = ResolveCellStep(in context);

            for (int i = 0; i < glyphCount; i++)
            {
                int col = _columns[i];
                int row = _rows[i];
                float t = (float)i / Mathf.Max(1, glyphCount - 1);
                float x = col * step.x;
                float y = -row * step.y;
                float baselineOffset =
                    context.sourceCenterBaselineOffsets != null &&
                    i < context.sourceCenterBaselineOffsets.Length
                        ? context.sourceCenterBaselineOffsets[i]
                        : 0f;

                placements[i] = new GlyphPlacement
                {
                    position = new Vector3(x, y + baselineOffset, 0f),
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                    tangent = Vector3.right,
                    up = Vector3.up,
                    outward = Vector3.back,
                    pathProgress = t
                };
            }

            Vector3 sourceAnchor = PlacementLineUtility.ComputeSourceAnchor(in context);
            PlacementLineUtility.AlignPlacements(in context, placements, sourceAnchor);
        }

        void EnsureBuffers(int glyphCount)
        {
            if (_rows == null || _rows.Length < glyphCount)
            {
                int capacity = TextStudioBufferCapacity.ForCount(glyphCount);
                _rows = new int[capacity];
                _columns = new int[capacity];
            }
        }

        int BuildCellMap(in PlacementContext context, int glyphCount, int columnLimit)
        {
            if (respectSourceLines &&
                flow == GridFlow.RowsFirst &&
                context.lineRanges != null &&
                context.lineCount > 0)
            {
                int nextRow = 0;
                int initializedUntil = 0;
                for (int lineIndex = 0; lineIndex < context.lineCount; lineIndex++)
                {
                    LineRange line = context.lineRanges[lineIndex];
                    int lineStart = Mathf.Clamp(line.glyphStart, 0, glyphCount);
                    int lineGlyphCount = Mathf.Clamp(line.glyphCount, 0, glyphCount - lineStart);

                    for (int gapIndex = initializedUntil; gapIndex < lineStart; gapIndex++)
                    {
                        _rows[gapIndex] = nextRow;
                        _columns[gapIndex] = 0;
                        nextRow++;
                    }

                    for (int lineOffset = 0; lineOffset < lineGlyphCount; lineOffset++)
                    {
                        int slot = reverse ? lineGlyphCount - 1 - lineOffset : lineOffset;
                        int glyphIndex = lineStart + lineOffset;
                        _rows[glyphIndex] = nextRow + slot / columnLimit;
                        _columns[glyphIndex] = slot % columnLimit;
                    }

                    nextRow += Mathf.Max(1, Mathf.CeilToInt(lineGlyphCount / (float)columnLimit));
                    initializedUntil = Mathf.Max(initializedUntil, lineStart + lineGlyphCount);
                }

                for (int glyphIndex = initializedUntil; glyphIndex < glyphCount; glyphIndex++)
                {
                    _rows[glyphIndex] = nextRow;
                    _columns[glyphIndex] = 0;
                    nextRow++;
                }

                return Mathf.Max(1, nextRow);
            }

            int rows = Mathf.Max(1, Mathf.CeilToInt(glyphCount / (float)columnLimit));
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                int slot = reverse ? glyphCount - 1 - glyphIndex : glyphIndex;
                if (flow == GridFlow.ColumnsFirst)
                {
                    _rows[glyphIndex] = slot % rows;
                    _columns[glyphIndex] = slot / rows;
                }
                else
                {
                    _rows[glyphIndex] = slot / columnLimit;
                    _columns[glyphIndex] = slot % columnLimit;
                }
            }

            return rows;
        }

        Vector2 ResolveCellStep(in PlacementContext context)
        {
            Vector2 resolvedSize = new Vector2(
                Mathf.Max(0.0001f, Mathf.Abs(cellSize.x)),
                Mathf.Max(0.0001f, Mathf.Abs(cellSize.y)));
            if (cellSizing == GridCellSizing.Auto &&
                context.sourceVisualBounds != null)
            {
                float maxWidth = 0f;
                float maxHeight = 0f;
                int count = Mathf.Min(context.glyphCount, context.sourceVisualBounds.Length);
                for (int i = 0; i < count; i++)
                {
                    if (!context.HasSourceLayout(i)) continue;
                    TextStudioGlyphVisualBounds bounds = context.sourceVisualBounds[i];
                    if (!bounds.IsValid || !bounds.IsVisible)
                        continue;

                    Vector2 size = bounds.Maximum - bounds.Minimum;
                    maxWidth = Mathf.Max(maxWidth, size.x);
                    maxHeight = Mathf.Max(maxHeight, size.y);
                }

                if (maxWidth > 0.0001f)
                    resolvedSize.x = maxWidth;
                if (maxHeight > 0.0001f)
                    resolvedSize.y = maxHeight;
            }

            if (cellSizing == GridCellSizing.Auto &&
                context.layoutPositions != null &&
                context.glyphCount > 1)
            {
                float derivedWidth = 0f;
                float derivedHeight = 0f;
                int count = Mathf.Min(
                    context.glyphCount,
                    context.layoutPositions.Length);
                for (int i = 1; i < count; i++)
                {
                    if (!context.HasSourceLayout(i) || !context.HasSourceLayout(i - 1)) continue;
                    Vector3 delta =
                        context.layoutPositions[i] -
                        context.layoutPositions[i - 1];
                    if (delta.x > 0.0001f)
                        derivedWidth = Mathf.Max(derivedWidth, delta.x);
                    if (Mathf.Abs(delta.y) > 0.0001f)
                        derivedHeight = Mathf.Max(derivedHeight, Mathf.Abs(delta.y));
                }

                if (derivedWidth > resolvedSize.x)
                    resolvedSize.x = derivedWidth;
                if (derivedHeight > resolvedSize.y)
                    resolvedSize.y = derivedHeight;
                else if (resolvedSize.y <= 1.0001f && derivedWidth > 0.0001f)
                    resolvedSize.y = derivedWidth * 1.5f;
            }

            return new Vector2(
                Mathf.Max(0.0001f, resolvedSize.x + gap.x),
                Mathf.Max(0.0001f, resolvedSize.y + gap.y));
        }
    }

    /// <summary>
    /// Manages placement strategy selection and computation.
    /// </summary>
    internal sealed class PlacementEngine
    {
        GlyphPlacement[] _placements;
        int _count;
        IPlacementStrategy _strategy;

        public GlyphPlacement[] Placements => _placements;
        public int Count => _count;

        readonly LinearPlacement _defaultLinear = new LinearPlacement();

        public void Clear()
        {
            _count = 0;
        }

        public void SetStrategy(IPlacementStrategy strategy)
        {
            _strategy = strategy;
        }

        public void ComputePlacements(
            Vector3[] layoutPositions,
            int glyphCount,
            float autoFitGlyphMargin = 0f,
            float[] sourceCenterBaselineOffsets = null,
            LineRange[] lineRanges = null,
            int lineCount = 0,
            bool layoutChanged = true,
            Vector2? sourceAlignment = null,
            float evaluationTime = float.NaN,
            TextStudioGlyphVisualBounds[] sourceVisualBounds = null, bool[] sourceLayoutValidity = null)
        {
            if (_placements == null || _placements.Length < glyphCount)
                _placements = new GlyphPlacement[TextStudioBufferCapacity.ForCount(glyphCount)];

            _count = glyphCount;
            IPlacementStrategy strategy = _strategy ?? _defaultLinear;
            PlacementContext context = new PlacementContext(
                layoutPositions,
                glyphCount,
                lineRanges,
                lineCount,
                autoFitGlyphMargin,
                sourceCenterBaselineOffsets,
                sourceAlignment,
                sourceVisualBounds, sourceLayoutValidity);
            if (strategy is CirclePlacement circlePlacement)
                circlePlacement.ComputePlacements(
                    in context,
                    _placements,
                    !layoutChanged);
            else if (strategy is WavePlacement wavePlacement &&
                     !float.IsNaN(evaluationTime))
                wavePlacement.ComputePlacements(in context, _placements, evaluationTime);
            else if (strategy is IPlacementContextStrategy contextStrategy)
                contextStrategy.ComputePlacements(in context, _placements);
            else
                strategy.ComputePlacements(layoutPositions, _placements, glyphCount);
        }

        /// <summary>
        /// Apply the hub-level orientation module to every computed placement. Called by
        /// <see cref="TextStudioText"/> right after <see cref="ComputePlacements"/> so
        /// glyph rotation is uniform across every placement strategy.
        /// </summary>
        public void ApplyOrientation(PlacementOrientation module)
        {
            if (module == null || !module.HasEffect)
                return;

            for (int i = 0; i < _count; i++)
            {
                if (_strategy is CurvePlacement curve && !curve.IsSelected(i))
                    continue;
                _placements[i].rotation = module.Apply(in _placements[i]);
            }
        }
    }
}
