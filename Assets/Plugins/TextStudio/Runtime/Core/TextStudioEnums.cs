using System;

namespace TextStudio
{
    [Flags]
    public enum DirtyFlags : uint
    {
        None       = 0,
        Text       = 1 << 0,
        Style      = 1 << 1,
        Layout     = 1 << 2,
        Placement  = 1 << 3,
        Animation  = 1 << 4,
        Visual     = 1 << 5,
        Topology   = 1 << 6,
        Simulation = 1 << 7,
        AppearanceTopology = 1 << 8,
        AppearanceParameters = 1 << 9,
        AppearanceRenderState = 1 << 10,
        AppearanceBounds = 1 << 11,
        WritingTopology = 1 << 12,
        WritingParameters = 1 << 13,
        WritingRenderState = 1 << 14,
        WritingBounds = 1 << 15,

        Appearance = AppearanceTopology | AppearanceParameters |
                     AppearanceRenderState | AppearanceBounds,
        Writing = WritingTopology | WritingParameters |
                  WritingRenderState | WritingBounds,

        // Addon extension bits (reserved range 16–31)
        Addon0     = 1u << 16,
        Addon1     = 1u << 17,
        Addon2     = 1u << 18,
        Addon3     = 1u << 19,

        // Core-owned flags only. Addons set and clear their extension bits independently.
        All        = Text | Style | Layout | Placement | Animation | Visual | Topology |
                     Simulation | Appearance | Writing
    }

    public enum PlacementMode
    {
        Linear,
        Arc,
        Circle,
        Spline,
        Wave,
        Grid,
        Extension,
        Curve
    }

    public enum TextStudioCurveMapping
    {
        EachMatch,
        EntireSelection
    }

    /// <summary>Describes whether a placement stays in the text plane or uses depth.</summary>
    public enum TextStudioPlacementDimension
    {
        Planar,
        Spatial
    }

    /// <summary>Shared orientation policies used by path and spatial placement extensions.</summary>
    public enum TextStudioPlacementOrientation
    {
        KeepOriginal,
        FollowPath,
        FaceOutward,
        FaceInward,
        PlacementDefault
    }

    /// <summary>Controls whether grid cells follow rendered glyph bounds or authored dimensions.</summary>
    public enum GridCellSizing
    {
        Auto,
        Fixed
    }

    /// <summary>Controls which grid axis fills first.</summary>
    public enum GridFlow
    {
        RowsFirst,
        ColumnsFirst
    }

    /// <summary>
    /// Controls how glyphs are distributed along path-following placements (Arc, Spline).
    /// </summary>
    public enum PathSpacing
    {
        /// <summary>Equal arc-length per glyph. Stable regardless of text width.</summary>
        Even,
        /// <summary>Proportional to the source layout (preserves TMP tracking / space widths).</summary>
        Proportional
    }

    /// <summary>Controls which vertical part of a glyph is positioned on a curve path.</summary>
    public enum PathPosition
    {
        /// <summary>Places the source baseline on the path so ordinary letters sit above it.</summary>
        Above,
        /// <summary>Places the source glyph center on the path.</summary>
        Centered,
        /// <summary>Places the source glyph top edge on the path so the glyph sits below it.</summary>
        Below
    }

    /// <summary>
    /// Built-in reveal timing presets, used when the hub has no custom
    /// <c>TypingTimingsBase</c> asset assigned. See <c>BuiltInTimings</c> for the
    /// actual delay curves.
    /// </summary>
    public enum RevealTimingsPreset
    {
        /// <summary>Constant delay per character. Smooth, predictable, no pauses.</summary>
        Uniform,
        /// <summary>Half the base delay - rapid typewriter.</summary>
        Fast,
        /// <summary>Double the base delay - deliberate narration.</summary>
        Slow,
        /// <summary>Reading pacing: pauses on sentence terminators (.!?) and mid-sentence punctuation (,;:-).</summary>
        Expressive,
        /// <summary>Dramatic pacing: long pauses on terminators, generous on commas, slight pause after each word.</summary>
        Cinematic,
        /// <summary>Very short, uniform delay - rapid-fire machine-gun text.</summary>
        Staccato,
        /// <summary>Pause between words only; characters within a word flash in together.</summary>
        ByWord,
        /// <summary>Subtle jitter per character so the typewriter feels human, not mechanical.</summary>
        Typist,
        /// <summary>Longer pause at sentence ends, gentle overall; good for dialogue boxes.</summary>
        Narrative
    }

    public enum RevealMode
    {
        Instant,
        ByGlyph,
        ByWord,
        ByLine,
        ByRegion,
        ByLetter,
        Custom
    }

    public enum TextStudioTypewriterSource
    {
        None,
        BuiltIn,
        Custom,
        Inline
    }

    [Flags]
    public enum TextStudioBehaviorDomain
    {
        None = 0,
        DefaultEffects = 1 << 0,
        Typewriter = 1 << 1,
        Reactions = 1 << 2
    }

    public enum TextStudioDefaultEffectsSource
    {
        None,
        BehaviorProfile,
        Inline
    }

    public enum TextStudioAuthoringMode
    {
        Raw,
        Preview
    }

    public enum TextStudioDefaultEffectMode
    {
        None = 0,
        Fallback = 1,
        Constant = 2
    }

    public enum AnimationPhase
    {
        None,
        Appearance,
        Persistent,
        Disappearance
    }

    public enum EffectBlendMode
    {
        Additive,
        Multiply,
        Override,
        Lerp
    }

    public enum TextStudioColorBlendMode
    {
        Set,
        AlphaOnly,
        Multiply
    }

    public enum TextStudioEffectAreaMode
    {
        Glyph,
        Word,
        Line,
        Paragraph,
        Range
    }

    public enum PlaybackMode
    {
        Once,
        Loop,
        PingPong,
        External,
        Weighted
    }

    public enum CurveType
    {
        Linear = 0,
        Sin = 1,
        Bounce = 2,
        Step = 3,
        Square = 4,
        Hold = 6,
        Flat = 7,
        Arch = 8,
        Valley = 9,
        SCurve = 10,
        Custom = 5
    }

    public enum TextStudioEffectPlaybackPreset
    {
        Once,
        Loop,
        PingPong,
        Cycle,
        Weighted,
        Appearance,
        External,
        Custom
    }

    public enum TextStudioEffectCurvePreset
    {
        Linear,
        Sine,
        HalfSine,
        Square,
        Step,
        Hold,
        Bounce,
        EaseIn,
        EaseOut,
        EaseInOut,
        BackOut,
        ElasticOut,
        Custom,
        ReverseLinear
    }

}
