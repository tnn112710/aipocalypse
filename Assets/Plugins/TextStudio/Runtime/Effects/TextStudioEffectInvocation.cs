using System;

namespace TextStudio
{
    [Flags]
    public enum TextStudioPhaseMask
    {
        None = 0,
        Reveal = 1,
        Active = 2,
        Hide = 4,
        All = Reveal | Active | Hide
    }

    /// <summary>One externally submitted renderer-neutral effect layer and its normalized progress, intensity, phase, and glyph range.</summary>
    public readonly struct TextStudioEffectInvocation
    {
        public readonly string EffectId;
        public readonly TextStudioTagParameters Parameters;
        public readonly TextStudioPhaseMask PhaseMask;
        public readonly float Intensity;
        public readonly float TimeOffset;
        public readonly bool HasRange;
        public readonly TextStudioTextRange Range;

        /// <summary>Creates an external effect submission, clamping normalized progress and intensity.</summary>
        public TextStudioEffectInvocation(
            string effectId,
            TextStudioTagParameters parameters,
            TextStudioPhaseMask phaseMask,
            float intensity,
            float timeOffset,
            bool hasRange,
            TextStudioTextRange range)
        {
            EffectId = effectId ?? string.Empty;
            Parameters = parameters;
            PhaseMask = phaseMask;
            Intensity = intensity;
            TimeOffset = timeOffset;
            HasRange = hasRange;
            Range = range;
        }
    }
}
