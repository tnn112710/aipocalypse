using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Serializable curve shape shared by inline settings and reusable profiles.</summary>
    [Serializable]
    public struct TextStudioCurveDefinition : ITextStudioCurve
    {
        [SerializeField] CurveType _curveType;
        [SerializeField] AnimationCurve _customCurve;

        public CurveType CurveType => _curveType;
        public AnimationCurve CustomCurve => CopyCurve(_customCurve);

        /// <summary>Creates a curve definition and copies any supplied custom animation curve.</summary>
        public TextStudioCurveDefinition(CurveType curveType, AnimationCurve customCurve = null)
        {
            _curveType = curveType;
            _customCurve = CopyCurve(customCurve);
        }

        public float Evaluate(float t) => Evaluate(_curveType, _customCurve, t);

        /// <summary>Returns an independent curve definition with a copied custom animation curve.</summary>
        public TextStudioCurveDefinition Copy() => new TextStudioCurveDefinition(_curveType, _customCurve);

        internal static float Evaluate(CurveType type, AnimationCurve customCurve, float t)
        {
            if (!float.IsFinite(t)) return 0f;
            t = Mathf.Clamp01(t);
            float value = type switch
            {
                CurveType.Linear => t,
                CurveType.Sin => Mathf.Sin(t * Mathf.PI * 2f),
                CurveType.Bounce => Mathf.Abs(Mathf.Sin(t * Mathf.PI)),
                CurveType.Step => t < 0.5f ? 0f : 1f,
                CurveType.Square => Mathf.Sin(t * Mathf.PI * 2f) >= 0f ? 1f : -1f,
                CurveType.Custom => customCurve != null ? customCurve.Evaluate(t) : t,
                CurveType.Hold => 1f,
                CurveType.Flat => 0f,
                CurveType.Arch => Mathf.Sin(t * Mathf.PI),
                CurveType.Valley => -Mathf.Sin(t * Mathf.PI),
                CurveType.SCurve => Mathf.Sin(t * Mathf.PI * 2f),
                _ => t
            };
            return float.IsFinite(value) ? value : 0f;
        }

        internal static AnimationCurve CopyCurve(AnimationCurve source)
        {
            if (source == null) return AnimationCurve.Linear(0f, 0f, 1f, 1f);
            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }
    }
}
