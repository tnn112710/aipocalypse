using UnityEngine;

namespace TextStudio
{
    /// <summary>A source-layout curve frame shared by rendered decorations and Scene authoring.</summary>
    internal readonly struct TextStudioCurvePlacementGuide
    {
        readonly ITextStudioCurve _curve;
        readonly float _height;
        readonly float _domainMin;
        readonly float _domainMax;
        readonly float _baseline;
        readonly float _depth;

        internal float DomainMin => _domainMin;
        internal float DomainMax => _domainMax;
        internal float Baseline => _baseline;
        internal float Height => _height;
        internal float Depth => _depth;
        internal float SegmentMin { get; }
        internal float SegmentMax { get; }

        internal TextStudioCurvePlacementGuide(ITextStudioCurve curve, float height,
            float domainMin, float domainMax, float segmentMin, float segmentMax,
            float baseline, float depth)
        {
            _curve = curve;
            _height = height;
            _domainMin = domainMin;
            _domainMax = domainMax;
            SegmentMin = segmentMin;
            SegmentMax = segmentMax;
            _baseline = baseline;
            _depth = depth;
        }

        internal Vector3 Evaluate(float x, out Vector3 up)
        {
            float width = _domainMax - _domainMin;
            float t = width > 0.00001f ? Mathf.Clamp01((x - _domainMin) / width) : 0.5f;
            float height = _height * (_curve?.Evaluate(t) ?? 0f);
            if (!float.IsFinite(height)) height = 0f;
            float slope = 0f;
            if (width > 0.00001f)
            {
                float left = Mathf.Max(0f, t - .001f);
                float right = Mathf.Min(1f, t + .001f);
                if (right > left)
                    slope = _height * (_curve.Evaluate(right) - _curve.Evaluate(left)) / ((right - left) * width);
            }
            if (!float.IsFinite(slope)) slope = 0f;
            slope = Mathf.Clamp(slope, -1000f, 1000f);
            Vector3 tangent = new Vector3(1f, slope, 0f).normalized;
            up = new Vector3(-tangent.y, tangent.x, 0f);
            return new Vector3(x, _baseline + height, _depth);
        }
    }
}
