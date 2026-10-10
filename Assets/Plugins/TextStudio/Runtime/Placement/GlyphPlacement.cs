using UnityEngine;

namespace TextStudio
{
    internal readonly struct CircularPlacementGuide
    {
        internal readonly float Radius;
        internal readonly float Sweep;
        readonly float _start;
        readonly float _radiusChange;
        readonly Vector3 _origin;

        internal CircularPlacementGuide(float radius, float start, float sweep, float radiusChange, Vector3 origin)
        {
            Radius = radius;
            Sweep = sweep;
            _start = start;
            _radiusChange = radiusChange;
            _origin = origin;
        }

        internal Vector3 Evaluate(float progress, out Vector3 up)
        {
            float angle = (_start + Sweep * progress) * Mathf.Deg2Rad;
            up = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0);
            return _origin + up * Mathf.Max(.0001f, Radius + _radiusChange * progress);
        }
    }

    /// <summary>
    /// Per-glyph placement result. 3D-capable with tangent and up vectors
    /// for path-following placement modes.
    /// </summary>
    public struct GlyphPlacement
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector3 tangent;
        public Vector3 up;
        public Vector3 outward;
        public float pathProgress;

        /// <summary>Creates an identity placement at a layout position with canonical tangent, up, and outward axes.</summary>
        public static GlyphPlacement Default(Vector3 position)
        {
            return new GlyphPlacement
            {
                position = position,
                rotation = Quaternion.identity,
                scale = Vector3.one,
                tangent = Vector3.right,
                up = Vector3.up,
                outward = Vector3.back,
                pathProgress = 0f
            };
        }
    }

    /// <summary>
    /// Shared input for placement strategies. Keeps per-glyph positions contiguous
    /// while allowing path strategies to preserve rendered line boundaries.
    /// </summary>
    public readonly struct PlacementContext
    {
        public readonly Vector3[] layoutPositions;
        public readonly int glyphCount;
        public readonly LineRange[] lineRanges;
        public readonly int lineCount;
        public readonly float autoFitGlyphMargin;
        public readonly float[] sourceCenterBaselineOffsets;
        public readonly TextStudioGlyphVisualBounds[] sourceVisualBounds;
        public readonly bool[] sourceLayoutValidity;
        /// <summary>Reports whether the unit index is in range and has valid native source-layout data.</summary>
        public bool HasSourceLayout(int unit) => (uint)unit < (uint)glyphCount &&
            (sourceLayoutValidity == null || ((uint)unit < (uint)sourceLayoutValidity.Length && sourceLayoutValidity[unit]));
        /// <summary>
        /// Normalized source-layout anchor, where (0, 0) is bottom-left and
        /// (1, 1) is top-right.
        /// </summary>
        public readonly Vector2 sourceAlignment;

        /// <summary>Creates transient placement input, clamping counts to supplied arrays and using centered alignment by default.</summary>
        public PlacementContext(
            Vector3[] layoutPositions,
            int glyphCount,
            LineRange[] lineRanges = null,
            int lineCount = 0,
            float autoFitGlyphMargin = 0f,
            float[] sourceCenterBaselineOffsets = null,
            Vector2? sourceAlignment = null,
            TextStudioGlyphVisualBounds[] sourceVisualBounds = null)
            : this(layoutPositions, glyphCount, lineRanges, lineCount, autoFitGlyphMargin,
                sourceCenterBaselineOffsets, sourceAlignment, sourceVisualBounds, null)
        {
        }

        /// <summary>Creates placement input with explicit measured-layout coverage for each semantic unit.</summary>
        public PlacementContext(
            Vector3[] layoutPositions,
            int glyphCount,
            LineRange[] lineRanges,
            int lineCount,
            float autoFitGlyphMargin,
            float[] sourceCenterBaselineOffsets,
            Vector2? sourceAlignment,
            TextStudioGlyphVisualBounds[] sourceVisualBounds,
            bool[] sourceLayoutValidity)
        {
            this.layoutPositions = layoutPositions;
            this.glyphCount = Mathf.Max(0, glyphCount);
            this.lineRanges = lineRanges;
            this.lineCount = lineRanges != null
                ? Mathf.Min(Mathf.Max(0, lineCount), lineRanges.Length)
                : 0;
            this.autoFitGlyphMargin = Mathf.Max(0f, autoFitGlyphMargin);
            this.sourceCenterBaselineOffsets = sourceCenterBaselineOffsets;
            this.sourceAlignment = sourceAlignment ?? new Vector2(0.5f, 0.5f);
            this.sourceVisualBounds = sourceVisualBounds;
            this.sourceLayoutValidity = sourceLayoutValidity;
        }
    }

    /// <summary>
    /// Interface for placement strategies.
    /// Placement modes transform linear layout positions into final 3D positions.
    /// </summary>
    public interface IPlacementStrategy
    {
        PlacementMode Mode { get; }

        /// <summary>
        /// Compute placements for all visible glyphs.
        /// </summary>
        /// <param name="layoutPositions">Input: baseline positions from the layout engine.</param>
        /// <param name="placements">Output: final 3D placements.</param>
        /// <param name="glyphCount">Number of glyphs.</param>
        void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount);
    }

    /// <summary>
    /// Extended placement contract for strategies that need rendered line ranges
    /// and sizing metadata in addition to the flat glyph positions.
    /// </summary>
    public interface IPlacementContextStrategy : IPlacementStrategy
    {
        /// <summary>
        /// Compute placements for all visible glyphs.
        /// </summary>
        /// <param name="context">Input layout, rendered line ranges, and sizing metadata.</param>
        /// <param name="placements">Output: final 3D placements.</param>
        void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements);
    }

    /// <summary>
    /// Optional metadata contract for component-based placement extensions.
    /// The Text Studio hub owns selection and composition while add-ons provide
    /// spatial algorithms and their specialized authoring controls.
    /// </summary>
    public interface ITextStudioPlacementExtension : IPlacementContextStrategy
    {
        /// <summary>Stable serialized identifier, namespaced by the owning package.</summary>
        string PlacementId { get; }

        /// <summary>Customer-facing name shown in Text Studio's placement chooser.</summary>
        string DisplayName { get; }

        /// <summary>Short explanation of the placement's intended use.</summary>
        string Description { get; }

        /// <summary>Whether the placement uses depth in addition to the text plane.</summary>
        TextStudioPlacementDimension Dimension { get; }
    }
}
