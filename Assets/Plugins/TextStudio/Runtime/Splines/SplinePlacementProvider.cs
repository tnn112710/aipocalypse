using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace TextStudio.Splines
{
    /// <summary>
    /// Optional Unity Splines placement provider for <see cref="TextStudioText"/>.
    /// Add this component beside the hub and set Placement Mode to Spline.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextStudioText))]
    [AddComponentMenu("Text Studio/Placement/Spline Placement Provider")]
    public sealed class SplinePlacementProvider : MonoBehaviour, IPlacementContextStrategy
    {
        [SerializeField, Tooltip("Spline container that provides the curve.")]
        SplineContainer _splineContainer;

        [SerializeField, Tooltip("Index of the spline within the container.")]
        int _splineIndex;

        [SerializeField, Tooltip("How glyphs are spaced along the spline.")]
        PathSpacing _spacing = PathSpacing.Even;

        [SerializeField, Range(0.1f, 1f), Tooltip("Fraction of the spline used by the text.")]
        float _fill = 1f;

        [SerializeField, Tooltip("Reverses progression along the spline.")]
        bool _reverse;

        [SerializeField, Range(0f, 1f), Tooltip("Normalized starting point on a closed spline.")]
        float _startOffset;

        [SerializeField, Tooltip("Controls whether glyphs sit above the spline on their source baseline, remain centered on it, or sit below it by their visible top edge.")]
        PathPosition _pathPosition = PathPosition.Above;

        float[] _advances;
        TextStudioText _hub;

        public PlacementMode Mode => PlacementMode.Spline;

        public SplineContainer SplineContainer
        {
            get => _splineContainer;
            set
            {
                if (_splineContainer == value) return;
                _splineContainer = value;
                RequestRefresh();
            }
        }

        public int SplineIndex
        {
            get => _splineIndex;
            set
            {
                int index = Mathf.Max(0, value);
                if (_splineIndex == index) return;
                _splineIndex = index;
                RequestRefresh();
            }
        }

        public PathSpacing Spacing
        {
            get => _spacing;
            set
            {
                if (_spacing == value) return;
                _spacing = value;
                RequestRefresh();
            }
        }

        public float Fill
        {
            get => _fill;
            set
            {
                float fill = Mathf.Clamp(value, 0.1f, 1f);
                if (Mathf.Approximately(_fill, fill)) return;
                _fill = fill;
                RequestRefresh();
            }
        }

        public bool Reverse
        {
            get => _reverse;
            set
            {
                if (_reverse == value) return;
                _reverse = value;
                RequestRefresh();
            }
        }

        public float StartOffset
        {
            get => _startOffset;
            set
            {
                float offset = Mathf.Repeat(value, 1f);
                if (Mathf.Approximately(_startOffset, offset)) return;
                _startOffset = offset;
                RequestRefresh();
            }
        }

        public PathPosition PathPosition
        {
            get => _pathPosition;
            set
            {
                if (_pathPosition == value) return;
                _pathPosition = value;
                RequestRefresh();
            }
        }

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            Vector3[] layoutPositions = context.layoutPositions;
            int glyphCount = context.glyphCount;
            if (!TryGetSpline(out Spline spline) || glyphCount == 0)
            {
                FillLinearFallback(layoutPositions, placements, glyphCount);
                return;
            }

            float totalLength = spline.GetLength();
            Matrix4x4 splineToPlacement =
                transform.worldToLocalMatrix *
                _splineContainer.transform.localToWorldMatrix;
            EnsureAdvanceCapacity(glyphCount);

            float totalAdvance = 0f;
            if (_spacing == PathSpacing.Proportional)
            {
                for (int i = 0; i < glyphCount; i++)
                {
                    _advances[i] = totalAdvance;
                    if (i + 1 < glyphCount)
                        totalAdvance += Vector3.Distance(layoutPositions[i], layoutPositions[i + 1]);
                }

                if (spline.Closed && glyphCount > 1)
                    totalAdvance += totalAdvance / (glyphCount - 1);
            }

            bool closed = spline.Closed;
            float direction = _reverse ? -1f : 1f;
            for (int i = 0; i < glyphCount; i++)
            {
                float progress = ResolveSplineProgress(
                    i,
                    glyphCount,
                    totalLength,
                    totalAdvance,
                    closed);
                float t = closed
                    ? Mathf.Repeat(_startOffset + progress * direction, 1f)
                    : _reverse ? 1f - progress : progress;
                SplineUtility.Evaluate(spline, t, out float3 position, out float3 tangent, out float3 up);
                if (math.lengthsq(tangent) <= 0.000001f)
                {
                    float tangentT = closed
                        ? Mathf.Repeat(t + direction * 0.0001f, 1f)
                        : Mathf.Clamp(t, 0.0001f, 0.9999f);
                    tangent = SplineUtility.EvaluateTangent(spline, tangentT);
                }

                Vector3 forward = splineToPlacement.MultiplyVector((Vector3)tangent).normalized * direction;
                if (forward.sqrMagnitude <= 0.000001f)
                    forward = splineToPlacement.MultiplyVector(Vector3.right).normalized * direction;

                Vector3 upVector = splineToPlacement.MultiplyVector((Vector3)up);
                upVector = Vector3.ProjectOnPlane(upVector, forward).normalized;
                if (upVector.sqrMagnitude <= 0.000001f)
                {
                    upVector = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
                    if (upVector.sqrMagnitude <= 0.000001f)
                        upVector = Vector3.forward;
                }

                placements[i] = new GlyphPlacement
                {
                    position = splineToPlacement.MultiplyPoint3x4((Vector3)position),
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                    tangent = forward,
                    up = upVector,
                    outward = upVector,
                    pathProgress = progress
                };
            }

            PathPlacementUtility.Apply(in context, placements, _pathPosition);
        }

        bool TryGetSpline(out Spline spline)
        {
            spline = null;
            if (_splineContainer == null || _splineContainer.Splines == null)
                return false;

            int count = _splineContainer.Splines.Count;
            if (_splineIndex < 0 || _splineIndex >= count)
                return false;

            spline = _splineContainer.Splines[_splineIndex];
            return spline != null && spline.Count >= 2;
        }

        void OnEnable()
        {
            _hub = GetComponent<TextStudioText>();
            if (_hub != null)
            {
                _hub.DiscoverRenderersAndProviders();
                _hub.SchedulePlacementRefresh();
            }
        }

        void OnDisable()
        {
            if (_hub != null)
                _hub.DiscoverRenderersAndProviders();
        }

        void OnValidate()
        {
            _splineIndex = Mathf.Max(0, _splineIndex);
            _fill = Mathf.Clamp(_fill, 0.1f, 1f);
            _startOffset = Mathf.Repeat(_startOffset, 1f);
            RequestRefresh();
        }

        void RequestRefresh()
        {
            if (_hub == null)
                _hub = GetComponent<TextStudioText>();
            if (_hub != null)
                _hub.SchedulePlacementRefresh();
        }

        void EnsureAdvanceCapacity(int glyphCount)
        {
            if (_advances == null || _advances.Length < glyphCount)
                _advances = new float[
                    glyphCount > 0 ? Mathf.NextPowerOfTwo(Mathf.Max(8, glyphCount)) : 0];
        }

        float ResolveSplineProgress(
            int glyphIndex,
            int glyphCount,
            float totalLength,
            float totalAdvance,
            bool closed)
        {
            float fill = Mathf.Clamp(_fill, 0.1f, 1f);
            float normalized;
            if (_spacing == PathSpacing.Proportional &&
                totalAdvance > 0f &&
                totalLength > 0f)
            {
                normalized = _advances[glyphIndex] / totalAdvance;
            }
            else if (closed && Mathf.Approximately(fill, 1f))
            {
                normalized = glyphCount > 0 ? (float)glyphIndex / glyphCount : 0f;
            }
            else
            {
                normalized = glyphCount > 1
                    ? (float)glyphIndex / (glyphCount - 1)
                    : 0.5f;
            }

            if (closed)
                return Mathf.Repeat(normalized * fill, 1f);

            return Mathf.Clamp01((1f - fill) * 0.5f + normalized * fill);
        }

        static void FillLinearFallback(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            for (int i = 0; i < glyphCount; i++)
            {
                Vector3 position = layoutPositions != null && i < layoutPositions.Length
                    ? layoutPositions[i]
                    : Vector3.zero;
                placements[i] = GlyphPlacement.Default(position);
            }
        }
    }
}
