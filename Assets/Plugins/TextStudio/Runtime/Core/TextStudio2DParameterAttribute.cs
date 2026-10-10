using System;
using UnityEngine;

namespace TextStudio
{
    internal enum TextStudio2DParameterKind
    {
        PaintScale,
        DistanceRange,
        AnimationDirection,
        CornerRadii,
        HalftoneMinimumRadius,
        HalftoneMaximumRadius,
        PatternScale,
        Softness,
        Minimum
    }

    /// <summary>Appearance constraints that must be applied before serialized binding publishes an edit.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class TextStudio2DParameterAttribute : PropertyAttribute
    {
        internal const float MinimumPatternScale = 0.01f;
        internal const float MaximumPatternScale = 4096f;
        internal const float SuggestedEmSoftness = 0.25f;
        internal const float SuggestedPixelSoftness = 16f;
        internal TextStudio2DParameterKind Kind { get; }
        internal float Minimum { get; }

        internal TextStudio2DParameterAttribute(TextStudio2DParameterKind kind)
        {
            Kind = kind;
        }

        internal TextStudio2DParameterAttribute(float minimum)
        {
            Kind = TextStudio2DParameterKind.Minimum;
            Minimum = minimum;
        }
    }
}
