using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Curve evaluation interface for effects and placement.
    /// </summary>
    public interface ITextStudioCurve
    {
        /// <summary>Evaluate the curve at normalized input t in [0, 1].</summary>
        float Evaluate(float t);
    }

    /// <summary>Reusable shape sampled by effect playback and text placement.</summary>
    [CreateAssetMenu(
        fileName = "New Curve",
        menuName = "Text Studio/Text Authoring/Curve Profile",
        order = 105)]
    public class TextStudioCurveProfile : ScriptableObject, ITextStudioCurve
    {
        [SerializeField] CurveType _curveType = CurveType.Sin;
        [SerializeField] AnimationCurve _customCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [NonSerialized] uint _revision;

        /// <summary>Raised after the profile's curve definition changes and its revision advances.</summary>
        public event Action<TextStudioCurveProfile> Changed;

        public CurveType CurveType => _curveType;
        public uint Revision => _revision;
        public TextStudioCurveDefinition Definition => new TextStudioCurveDefinition(_curveType, _customCurve);

        internal void Initialize(CurveType curveType)
        {
            Initialize(curveType, null);
        }

        internal void Initialize(CurveType curveType, AnimationCurve customCurve)
        {
            _curveType = curveType;
            _customCurve = TextStudioCurveDefinition.CopyCurve(customCurve);
            NotifyChanged();
        }

        /// <summary>Replaces the profile's curve definition, advances its revision, and notifies subscribers.</summary>
        public void SetDefinition(in TextStudioCurveDefinition definition)
        {
            _curveType = definition.CurveType;
            _customCurve = definition.CustomCurve;
            NotifyChanged();
        }

        public float Evaluate(float t) => TextStudioCurveDefinition.Evaluate(_curveType, _customCurve, t);

        void OnValidate() => NotifyChanged();

        void NotifyChanged()
        {
            unchecked { _revision++; }
            Changed?.Invoke(this);
        }
    }
}
