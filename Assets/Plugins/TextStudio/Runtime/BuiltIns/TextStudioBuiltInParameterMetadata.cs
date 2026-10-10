using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Reflection-free authoring metadata for every parameter accepted by the built-in effects.
    /// Definitions are authored in display order: effect controls first, then shared timing controls.
    /// </summary>
    internal static class TextStudioBuiltInParameterMetadata
    {
        internal static TextStudioTagParameterDefinition[] Create(
            CodeEffectBase effect,
            string[] playbackValues)
        {
            return AddAutomaticOneLetterAliases(
                CreateExplicit(effect, playbackValues));
        }

        static TextStudioTagParameterDefinition[] CreateExplicit(
            CodeEffectBase effect,
            string[] playbackValues)
        {
            switch (effect)
            {
                case AccordionEffect value:
                    return new[]
                    {
                        FloatParameter("compression", "Compression", "Horizontal compression applied before the glyph opens into place.", value.compression),
                        FloatParameter("depth", "Depth", "Depth displacement added to the alternating folds.", value.depth),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case BlinkEffect value:
                    return new[]
                    {
                        FloatParameter("frequency", "Frequency", "Blink cycles per second.", value.frequency, "f"),
                        FloatParameter("duty", "Visible Duty Cycle", "Fraction of each blink cycle that remains fully visible.", value.dutyCycle),
                        FloatParameter("min", "Minimum Alpha", "Lowest alpha reached while the blink is off.", value.minAlpha),
                        DurationParameter(value.duration, false),
                        PlaybackParameter(value.playback, playbackValues, false)
                    };
                case BounceEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum bounce distance in local text units. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.height, "a"),
                        DirectionParameter("direction", "Direction", "Two-dimensional bounce direction written as x,y.", value.direction),
                        FloatParameter("stagger", "Stagger", "Phase offset added between consecutive effect units.", value.stagger),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case BurnEffect value:
                    return new[]
                    {
                        FloatParameter("softness", "Edge Softness", "Width of the hot transition edge, clamped above zero.", value.softness),
                        EnumParameter("space", "Mapping", "Glyph restarts the procedural pattern on every character. Text maps one continuous pattern across the complete source-text bounds.", value.space, new[] { "glyph", "text" })
                    };
                case CardFlipYEffect value:
                    return new[]
                    {
                        FloatParameter("angle", "Angle", "Starting Y-axis flip angle in degrees.", value.angle),
                        FloatParameter("depth", "Depth", "Forward depth added while the glyph flips.", value.depth),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case ChromaticGlitchEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Magnitude of RGB separation and horizontal slicing.", value.strength),
                        FloatParameter("frequency", "Frequency", "Rate at which seeded glitch samples change.", value.frequency),
                        IntegerParameter("seed", "Seed", "Seed used for deterministic chromatic glitch sampling.", value.seed),
                        RangedFloatParameter("activity", "Slice Activity", "Fraction of samples that add horizontal slicing.", value.activity, 0f, 1f)
                    };
                case ColorPulseEffect value:
                    return new[]
                    {
                        ColorParameter("from", "From Color", "Starting pulse color.", value.colorA, 0),
                        ColorParameter("to", "To Color", "Ending pulse color.", value.colorB, 1),
                        SpeedParameter(value.speed, 2)
                    };
                case ColorSweepEffect value:
                    return new[]
                    {
                        ColorParameter("color", "Sweep Color", "Color applied by the travelling sweep.", value.color, -1, "c", "to"),
                        FloatParameter("softness", "Softness", "Width of the sweep's feathered edge.", value.softness, "w"),
                        BooleanParameter("reverse", "Reverse", "Moves the sweep in the opposite direction.", value.reverse, "r")
                    };
                case CornerWarpEffect value:
                    return new[]
                    {
                        EnumParameter("mode", "Mode", "Corner deformation profile used by the effect.", value.mode, new[] { "ripple", "flag", "jelly", "squeeze", "bulge" }),
                        FloatParameter("strength", "Strength", "Maximum corner displacement in local text units.", value.strength),
                        FloatParameter("spread", "Spread", "Phase separation between consecutive glyphs.", value.spread),
                        SpeedParameter(value.speed)
                    };
                case LensMagnifierEffect value:
                    return new[]
                    {
                        RangedFloatParameter("radius", "Radius", "Clear lens radius in local effect units.", value.radius, 0.001f, 4096f),
                        RangedFloatParameter("centerMagnification", "Center Magnification", "Optical magnification at the center of the lens.", value.centerMagnification, 1f, 8f),
                        RangedFloatParameter("edgeMagnification", "Edge Magnification", "Optical magnification already present at the visible lens rim.", value.edgeMagnification, 1f, 8f),
                        RangedFloatParameter("bulge", "Bulge", "Curves the radial magnification profile between the rim and center.", value.bulge, 0f, 1f),
                        RangedFloatParameter("feather", "Aperture Feather", "Narrow blend width at the physical aperture edge.", value.feather, 0f, 0.25f)
                    };
                case DangleEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum dangling rotation in degrees. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.maxDegrees, "a"),
                        PivotParameter("pivot", "Pivot", "Pivot relative to each glyph's bounds.", value.pivot),
                        HorizontalAxisParameter(value.horizontal),
                        VerticalAxisParameter(value.vertical),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case DecryptEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Shake Strength", "Maximum position jitter while a glyph is decrypting.", value.shakeStrength, "a"),
                        FloatParameter("speed", "Shake Speed", "Rate at which the decrypt jitter changes.", value.shakeSpeed, "s"),
                        IntegerParameter("seed", "Seed", "Seed used for deterministic decrypt jitter.", value.seed)
                    };
                case DissolveEffect value:
                    return new[]
                    {
                        FloatParameter("softness", "Edge Softness", "Width of the dissolve transition edge, clamped above zero.", value.softness),
                        EnumParameter("space", "Mapping", "Glyph restarts the procedural pattern on every character. Text maps one continuous pattern across the complete source-text bounds.", value.space, new[] { "glyph", "text" })
                    };
                case DominoLineEffect value:
                    return new[]
                    {
                        FloatParameter("angle", "Angle", "Sideways fall angle in degrees.", value.angle),
                        FloatParameter("depth", "Depth", "Depth displacement used during the fall.", value.depth),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case ElasticEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Maximum elastic scale displacement.", value.strength),
                        FloatParameter("cycles", "Oscillations", "Number of elastic oscillations during the transition.", value.oscillations),
                        FloatParameter("damping", "Damping", "Rate at which the elastic oscillation settles.", value.damping),
                        FloatParameter("anticipation", "Anticipation", "Initial motion in the opposite direction before the elastic release.", value.anticipation),
                        BooleanParameter("preserve", "Preserve Area", "Balances X and Y scale so the glyph area remains more stable.", value.preserveArea)
                    };
                case EmphasisEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum emphasis scale and lift strength.", value.amplitude, "a"),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case ExpandEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Strength of reveal scaling and active pulsing.", value.amplitude, "a"),
                        EnumParameter("mode", "Mode", "Axis or axes affected by expansion.", value.mode, new[] { "both", "horizontal", "vertical", "x", "y", "HorizontallyFromCenter", "VerticallyFromCenter" }),
                        EnumParameter("origin", "Origin", "Glyph edge or center used as the expansion origin.", value.origin, new[] { "center", "left", "right", "top", "bottom" }),
                        FloatParameter("rate", "Pulse Speed", "Active expansion pulse cycles per second.", value.pulseSpeed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case FadeEffect value:
                    return new[]
                    {
                        FloatParameter("min", "Minimum Alpha", "Alpha at the transparent end of the fade.", value.minAlpha),
                        FloatParameter("max", "Maximum Alpha", "Alpha at the opaque end of the fade.", value.maxAlpha),
                        ColorParameter("color", "Color", "Optional fade target color, including transparent colors such as #ffffff00.", value.color),
                        EnumParameter("mode", "Color Mode", "Color blend mode used with the fade target color.", value.mode, new[] { "0", "1", "alpha", "alphaOnly", "full", "set", "setColor", "multiply", "mul" }),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case FloatEffect value:
                    return new[]
                    {
                        FloatParameter("vertical", "Vertical Amplitude", "Maximum vertical drift in local text units. Use vertical=value to replace it or vertical*factor to scale the default.", value.verticalAmplitude, "a"),
                        FloatParameter("horizontal", "Horizontal Amplitude", "Maximum horizontal drift in local text units.", value.horizontalAmplitude),
                        FloatParameter("spread", "Spread", "Phase separation between consecutive glyphs.", value.spread, "f"),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case FocusEffect value:
                    return new[]
                    {
                        FloatParameter("amount", "Amount", "Maximum defocus-to-sharp transition amount.", value.amount, "a")
                    };
                case FoldAwayEffect value:
                    return new[]
                    {
                        FloatParameter("angle", "Angle", "Fold-away rotation angle in degrees.", value.angle),
                        FloatParameter("depth", "Depth", "Depth displacement used while folding away.", value.depth),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case GlitchEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Maximum seeded position displacement.", value.strength, "a"),
                        FloatParameter("frequency", "Frequency", "Rate at which glitch samples change.", value.frequency, "f"),
                        IntegerParameter("seed", "Seed", "Seed used for deterministic glitch sampling.", value.seed),
                        FloatParameter("activity", "Activity", "Fraction of time that the glitch is visibly active.", value.activity)
                    };
                case GradientFlowEffect value:
                    return new[]
                    {
                        ColorParameter("from", "From Color", "First color in the flowing gradient.", value.colorA),
                        ColorParameter("to", "To Color", "Second color in the flowing gradient.", value.colorB),
                        FloatParameter("spread", "Spread", "Gradient phase separation between consecutive glyphs.", value.spread, "f"),
                        SpeedParameter(value.speed)
                    };
                case HeroDepthRiseEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Overall depth-rise transition strength.", value.strength),
                        FloatParameter("lift", "Lift", "Vertical lift in local text units.", value.lift),
                        FloatParameter("tilt", "Tilt", "Starting three-dimensional tilt in degrees.", value.tilt),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case OrbitEffect value:
                    return new[]
                    {
                        FloatParameter("radius", "Radius", "Radius of each glyph's orbit in local text units.", value.radius),
                        FloatParameter("spread", "Spread", "Orbit phase separation between consecutive glyphs.", value.spread),
                        BooleanParameter("face", "Face Motion", "Rotates glyphs to follow the orbit direction.", value.faceMotion),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion),
                        SpeedParameter(value.speed)
                    };
                case OutlineGlowEffect value:
                    return new[]
                    {
                        FloatParameter("outline", "Outline", "Maximum SDF outline amount.", value.outline),
                        FloatParameter("glow", "Glow", "Maximum SDF glow amount.", value.glow),
                        SpeedParameter(value.speed),
                        RangedFloatParameter("floor", "Minimum Strength", "Lowest retained outline and glow strength during the pulse.", value.floor, 0f, 1f)
                    };
                case PendulumEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum pendulum rotation in degrees. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.maxDegrees, "a"),
                        PivotParameter("pivot", "Pivot", "Pivot relative to each glyph's bounds.", value.pivot),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case PixelGridEffect value:
                    return new[]
                    {
                        FloatParameter("size", "Cell Size", "Resting grid-cell size in current render-target pixels. Values are clamped from 1 to 64.", value.cellSize),
                        FloatParameter("active", "Active Amount", "Pixelation retained while the effect is active, clamped from 0 to 1.", value.activeAmount),
                        FloatParameter("transition", "Transition Amount", "Pixelation at appearance start and disappearance end, clamped from 0 to 1.", value.transitionAmount),
                        EnumParameter("space", "Grid Space", "Screen keeps cells aligned to render pixels. Glyph anchors cells to each moving and rotating glyph.", value.space, new[] { "screen", "glyph" }),
                        EnumParameter("quality", "Sampling Quality", "Preserve uses four coverage samples for thin strokes. Crisp uses one faster cell-center sample.", value.quality, new[] { "preserve", "crisp" }),
                        FloatParameter("pulse", "Pulse Speed", "Active cell-size cycles per second. Zero disables pulsing; runtime values are clamped from 0 to 20.", value.pulseSpeed),
                        FloatParameter("min", "Minimum Cell Size", "Smallest pulsing cell size in render-target pixels, clamped from 1 to 64.", value.minCellSize),
                        FloatParameter("max", "Maximum Cell Size", "Largest transition and pulsing cell size in render-target pixels, clamped from 1 to 64.", value.maxCellSize)
                    };
                case SquiggleEffect value:
                    return new[]
                    {
                        RangedFloatParameter("strength", "Strength", "Local contour displacement as a fraction of the glyph quad. Zero preserves the original glyph.", value.strength, 0f, .08f, "a"),
                        RangedFloatParameter("detail", "Detail", "Noise cells across each glyph, rounded to a whole number.", value.detail, 1f, 12f),
                        RangedFloatParameter("rate", "Redraw Rate", "Distinct hand-drawn frames per second. Zero freezes the seeded shape.", value.rate, 0f, 12f, "speed"),
                        FloatParameter("seed", "Seed", "Deterministic noise variation. Equal seeds and times reproduce the same contour.", value.seed)
                    };
                case PopEffect value:
                    return new[]
                    {
                        FloatParameter("start", "Start Scale", "Scale used at the beginning of the pop transition.", value.startScale),
                        FloatParameter("overshoot", "Overshoot", "Scale overshoot above the authored size.", value.overshoot, "a")
                    };
                case PulseEffect value:
                    return new[]
                    {
                        FloatParameter("scale", "Scale Amplitude", "Scale added at the peak of the pulse.", value.scaleAmplitude, "a"),
                        FloatParameter("alpha", "Alpha Amplitude", "Alpha reduction applied at the dimmest point of the pulse.", value.alphaAmplitude),
                        FloatParameter("stagger", "Glyph Stagger", "Pulse phase offset added between consecutive glyphs.", value.glyphPhaseOffset),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case RainbowEffect value:
                    return new[]
                    {
                        FloatParameter("saturation", "Saturation", "HSV saturation of generated rainbow colors.", value.saturation),
                        FloatParameter("value", "Value", "HSV brightness of generated rainbow colors.", value.value),
                        FloatParameter("spread", "Spread", "Hue separation between consecutive glyphs.", value.spread, "f"),
                        SpeedParameter(value.speed)
                    };
                case RollingRibbonEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Maximum vertical displacement in local text units.", value.strength),
                        FloatParameter("angle", "Angle", "Maximum Y-axis rolling angle in degrees.", value.angle),
                        FloatParameter("spread", "Spread", "Wave phase separation between consecutive glyphs.", value.spread),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion),
                        SpeedParameter(value.speed)
                    };
                case RotateEffect value:
                    return new[]
                    {
                        FloatParameter("angle", "Angle", "Reveal and hide rotation angle. Use angle=value to replace it or angle*factor to scale the default.", value.startDegrees, "a"),
                        FloatParameter("loop", "Loop Degrees", "Degrees travelled per active rotation cycle.", value.loopDegrees),
                        EnumParameter("axis", "Axis", "Axis used by the rotate effect. Z preserves flat-text rotation.", value.axis, new[] { "x", "y", "z" }),
                        PivotParameter("pivot", "Pivot", "Pivot relative to each glyph's bounds.", value.pivot),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case Scatter2DEffect value:
                    return new[]
                    {
                        FloatParameter("distance", "Distance", "Maximum scatter distance in local text units.", value.distance),
                        DirectionParameter("direction", "Direction", "Central scatter direction written as x,y.", value.direction),
                        FloatParameter("cone", "Cone Angle", "Angular width of directional scatter in degrees.", value.coneAngle),
                        BooleanParameter("radial", "Radial", "Scatters around the full circle instead of the configured direction cone.", value.radial),
                        IntegerParameter("seed", "Seed", "Seed used for deterministic scatter positions.", value.seed),
                        FloatParameter("overshoot", "Overshoot", "Distance overshoot applied while glyphs settle.", value.overshoot)
                    };
                case ShakeEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum shake displacement in local text units. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.strength, "a"),
                        IntegerParameter("seed", "Seed", "Seed used for deterministic two-axis shake sampling.", value.seed),
                        BooleanParameter("dynamic", "Dynamic Noise", "Changes random-position sampling over time. False keeps the per-glyph offset stable.", value.progressIndexWithTime),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case ShearEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum glyph-space shear strength.", value.amplitude, "a"),
                        HorizontalAxisParameter(value.horizontal),
                        VerticalAxisParameter(value.vertical),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case ShimmerEffect value:
                    return new[]
                    {
                        ColorParameter("color", "Highlight Color", "Color of the travelling shimmer highlight.", value.highlightColor),
                        FloatParameter("width", "Width", "Width of the shimmer highlight band.", value.width, "w"),
                        FloatParameter("scale", "Scale Boost", "Temporary glyph scale added at the highlight peak.", value.scaleBoost),
                        SpeedParameter(value.speed),
                        ProgressPlaybackParameter(value.playback),
                        ProgressCurveParameter(value.curve)
                    };
                case SizeEffect value:
                    return new[]
                    {
                        FloatParameter("start", "Start Scale", "Scale at the beginning of reveal and end of hide.", value.startScale),
                        FloatParameter("end", "End Scale", "Scale reached at the end of reveal and start of hide.", value.endScale),
                        FloatParameter("pulse", "Pulse Amplitude", "Active scale variation around the end scale.", value.pulseAmplitude),
                        FloatParameter("rate", "Pulse Speed", "Active size pulse cycles per second.", value.pulseSpeed),
                        StringParameter("scale", "Scale", "Scale override. Accepts scalar, x,y, or x,y,z values.", value.scale, "1.5"),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case SlideHorizontalEffect value:
                    return CreateSlideParameters(value.distance, value.stagger, value.horizontal, value.vertical, value.speed, value.duration, value.playback, playbackValues, "horizontal");
                case SlideVerticalEffect value:
                    return CreateSlideParameters(value.distance, value.stagger, value.horizontal, value.vertical, value.speed, value.duration, value.playback, playbackValues, "vertical");
                case SoftSpatialFloatEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Maximum three-axis drift in local text units.", value.strength),
                        FloatParameter("spread", "Spread", "Motion phase separation between consecutive glyphs.", value.spread),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion),
                        SpeedParameter(value.speed)
                    };
                case SpatialScatterEffect value:
                    return new[]
                    {
                        FloatParameter("strength", "Strength", "Maximum three-dimensional scatter distance.", value.strength),
                        IntegerParameter("seed", "Seed", "Seed used for deterministic three-dimensional scatter positions.", value.seed),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case SpiralAssembleEffect value:
                    return new[]
                    {
                        FloatParameter("radius", "Radius", "Starting radius of the assembly spiral.", value.radius),
                        FloatParameter("depth", "Depth", "Starting depth of the assembly spiral.", value.depth),
                        FloatParameter("turns", "Turns", "Number of turns travelled before glyphs settle.", value.turns),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case SurfaceSweepEffect value:
                    return new[]
                    {
                        RangedFloatParameter("angle", "Angle", "Direction of the highlight band in source-layout degrees.", value.angle, -180f, 180f),
                        RangedFloatParameter("width", "Band Width", "Half-width of the material highlight band as a fraction of the selected area's projected span.", value.width, 0.001f, 5f),
                        RangedFloatParameter("brightness", "Brightness", "Per-effect highlight brightness multiplier.", value.brightness, 0f, 3f),
                        SpeedParameter(value.speed),
                        ProgressPlaybackParameter(value.playback),
                        ProgressCurveParameter(value.curve)
                    };
                case SwingEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum swing rotation in degrees. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.maxDegrees, "a"),
                        PivotParameter("pivot", "Pivot", "Pivot relative to each glyph's bounds.", value.pivot),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case TopHingeEffect value:
                    return new[]
                    {
                        FloatParameter("angle", "Angle", "Starting X-axis hinge angle in degrees.", value.angle),
                        BooleanParameter("reduced", "Reduced Motion", "Uses the effect's reduced-motion-safe presentation.", value.reducedMotion)
                    };
                case TypewriterEffect _:
                    return Array.Empty<TextStudioTagParameterDefinition>();
                case VisualTrackingEffect value:
                    return new[]
                    {
                        FloatParameter("amount", "Amount", "Additional visual spacing between neighbouring glyphs.", value.amount),
                        EnumParameter("anchor", "Anchor", "Side of each glyph that remains fixed while tracking changes.", value.anchor, new[] { "left", "center", "right" })
                    };
                case WaveEffect value:
                    return new[]
                    {
                        RangedFloatParameter("amplitude", "Amplitude", "Maximum wave displacement in local text units. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.amplitude, 0f, 5f, "a"),
                        DirectionParameter("direction", "Direction", "Two-dimensional wave direction written as x,y.", value.direction),
                        RangedFloatParameter("frequency", "Frequency", "Wave phase separation between consecutive glyphs.", value.frequency, 0f, 10f, "f"),
                        RangedFloatParameter("speed", "Speed", "Animation cycles or samples per second.", value.speed, 0f, 20f, "s"),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                case WiggleEffect value:
                    return new[]
                    {
                        FloatParameter("amplitude", "Amplitude", "Maximum position displacement in local text units. Use amplitude=value to replace it or amplitude*factor to scale the default.", value.positionStrength, "a"),
                        FloatParameter("rotation", "Rotation Degrees", "Maximum random glyph rotation in degrees.", value.rotationDegrees, "r"),
                        BooleanParameter("dynamic", "Dynamic Noise", "Changes random-position sampling over time. False keeps the per-glyph offset stable.", value.progressIndexWithTime),
                        SpeedParameter(value.speed),
                        DurationParameter(value.duration),
                        PlaybackParameter(value.playback, playbackValues)
                    };
                default:
                    return Array.Empty<TextStudioTagParameterDefinition>();
            }
        }

        static TextStudioTagParameterDefinition[] CreateSlideParameters(
            float distance,
            float stagger,
            string horizontal,
            string vertical,
            float speed,
            float duration,
            string playback,
            string[] playbackValues,
            string direction)
        {
            return new[]
            {
                FloatParameter("distance", "Distance", "Maximum " + direction + " slide distance in local text units. Use distance=value to replace it or distance*factor to scale the default.", distance, "a"),
                FloatParameter("stagger", "Stagger", "Phase offset added between consecutive effect units.", stagger),
                HorizontalAxisParameter(horizontal),
                VerticalAxisParameter(vertical),
                SpeedParameter(speed),
                DurationParameter(duration),
                PlaybackParameter(playback, playbackValues)
            };
        }

        static TextStudioTagParameterDefinition SpeedParameter(float value, int positionalIndex = -1)
        {
            return new TextStudioTagParameterDefinition(
                "speed",
                TextStudioTagParameterKind.Float,
                positionalIndex: positionalIndex,
                displayName: "Speed",
                description: "Animation cycles or samples per second.",
                aliases: new[] { "s" },
                defaultValue: Format(value));
        }

        static TextStudioTagParameterDefinition DurationParameter(float value, bool shortAlias = true)
        {
            return new TextStudioTagParameterDefinition(
                "duration",
                TextStudioTagParameterKind.Float,
                displayName: "Duration",
                description: "Seconds used by finite and looping playback. Infinite playback ignores this value.",
                aliases: shortAlias ? new[] { "d" } : null,
                defaultValue: Format(value));
        }

        static TextStudioTagParameterDefinition PlaybackParameter(
            string value,
            string[] allowedValues,
            bool shortAlias = true)
        {
            string[] resolvedAllowedValues = IncludeDefaultValue(allowedValues, value);
            return new TextStudioTagParameterDefinition(
                "playback",
                TextStudioTagParameterKind.Enum,
                displayName: "Playback",
                description: "Effect playback mode. Built-in keywords and Playback assets registered in Text Studio Project Settings are accepted directly.",
                allowedValues: resolvedAllowedValues,
                aliases: shortAlias ? new[] { "p" } : null,
                defaultValue: value);
        }

        static TextStudioTagParameterDefinition ProgressCurveParameter(string value)
        {
            return new TextStudioTagParameterDefinition(
                "curve",
                TextStudioTagParameterKind.Enum,
                displayName: "Progress Curve",
                description: "Shapes sweep position. Built-in keywords and Curve assets registered in Text Studio Project Settings are accepted directly.",
                allowedValues: IncludeDefaultValue(new[]
                {
                    "linear",
                    "reverseLinear",
                    "sine",
                    "halfSine",
                    "square",
                    "step",
                    "hold",
                    "bounce",
                    "easeIn",
                    "easeOut",
                    "easeInOut",
                    "backOut",
                    "elasticOut"
                }, value),
                defaultValue: value);
        }

        static TextStudioTagParameterDefinition ProgressPlaybackParameter(string value)
        {
            return PlaybackParameter(value, new[]
            {
                "once",
                "loop",
                "pingpong",
                "cycle",
                "weighted",
                "appearance",
                "infinite",
                "external",
                "short"
            });
        }

        static string[] IncludeDefaultValue(string[] allowedValues, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(defaultValue))
                return allowedValues;
            if (allowedValues != null)
            {
                for (int i = 0; i < allowedValues.Length; i++)
                {
                    if (string.Equals(allowedValues[i], defaultValue, StringComparison.OrdinalIgnoreCase))
                        return allowedValues;
                }
            }

            int existingCount = allowedValues != null ? allowedValues.Length : 0;
            string[] result = new string[existingCount + 1];
            if (existingCount > 0)
                Array.Copy(allowedValues, result, existingCount);
            result[existingCount] = defaultValue;
            return result;
        }

        static TextStudioTagParameterDefinition HorizontalAxisParameter(string value)
        {
            return EnumParameter(
                "horizontal",
                "Horizontal Shear",
                "Horizontal shear axis. Numeric Text Animator values 0, 1, 2, and 3 are accepted.",
                string.IsNullOrEmpty(value) ? "none" : value,
                new[] { "0", "1", "2", "3", "none", "left", "right", "all", "allSides" });
        }

        static TextStudioTagParameterDefinition VerticalAxisParameter(string value)
        {
            return EnumParameter(
                "vertical",
                "Vertical Shear",
                "Vertical shear axis. Numeric Text Animator values 0, 1, 2, and 3 are accepted.",
                string.IsNullOrEmpty(value) ? "none" : value,
                new[] { "0", "1", "2", "3", "none", "top", "bottom", "all", "allSides" });
        }

        static TextStudioTagParameterDefinition FloatParameter(
            string name,
            string displayName,
            string description,
            float value,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Float,
                displayName: displayName,
                description: description,
                aliases: Normalize(aliases),
                defaultValue: Format(value));
        }

        static TextStudioTagParameterDefinition IntegerParameter(
            string name,
            string displayName,
            string description,
            int value,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Integer,
                displayName: displayName,
                description: description,
                aliases: Normalize(aliases),
                defaultValue: value.ToString(CultureInfo.InvariantCulture));
        }

        static TextStudioTagParameterDefinition RangedFloatParameter(
            string name,
            string displayName,
            string description,
            float value,
            float minimum,
            float maximum,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Float,
                displayName: displayName,
                description: description,
                aliases: Normalize(aliases),
                defaultValue: Format(value),
                minimum: minimum,
                maximum: maximum);
        }

        static TextStudioTagParameterDefinition BooleanParameter(
            string name,
            string displayName,
            string description,
            bool value,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Boolean,
                displayName: displayName,
                description: description,
                aliases: Normalize(aliases),
                defaultValue: value ? "true" : "false");
        }

        static TextStudioTagParameterDefinition ColorParameter(
            string name,
            string displayName,
            string description,
            Color32 value,
            int positionalIndex = -1,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Color,
                positionalIndex: positionalIndex,
                displayName: displayName,
                description: description,
                aliases: Normalize(aliases),
                defaultValue: "#" +
                              value.r.ToString("X2", CultureInfo.InvariantCulture) +
                              value.g.ToString("X2", CultureInfo.InvariantCulture) +
                              value.b.ToString("X2", CultureInfo.InvariantCulture) +
                              value.a.ToString("X2", CultureInfo.InvariantCulture));
        }

        static TextStudioTagParameterDefinition EnumParameter(
            string name,
            string displayName,
            string description,
            string value,
            string[] allowedValues,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Enum,
                displayName: displayName,
                description: description,
                allowedValues: allowedValues,
                aliases: Normalize(aliases),
                defaultValue: value);
        }

        static TextStudioTagParameterDefinition StringParameter(
            string name,
            string displayName,
            string description,
            string value,
            string exampleValue,
            params string[] aliases)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.String,
                displayName: displayName,
                description: description,
                aliases: Normalize(aliases),
                defaultValue: value,
                exampleValue: exampleValue);
        }

        static TextStudioTagParameterDefinition DirectionParameter(
            string name,
            string displayName,
            string description,
            Vector2 value)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Vector2,
                displayName: displayName,
                description: description,
                defaultValue: TextStudioPivotParser.FormatCoordinates(value),
                exampleValue: "0,1");
        }

        static TextStudioTagParameterDefinition PivotParameter(
            string name,
            string displayName,
            string description,
            Vector2 value)
        {
            return new TextStudioTagParameterDefinition(
                name,
                TextStudioTagParameterKind.Pivot,
                displayName: displayName,
                description: description + " Coordinates use x,y with 0 at left/bottom, 0.5 at center, and 1 at right/top.",
                defaultValue: TextStudioPivotParser.Format(value),
                exampleValue: "0.25,0.75");
        }

        static string Format(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        static string[] Normalize(string[] aliases)
        {
            return aliases != null && aliases.Length > 0 ? aliases : null;
        }

        static TextStudioTagParameterDefinition[] AddAutomaticOneLetterAliases(
            IReadOnlyList<TextStudioTagParameterDefinition> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return Array.Empty<TextStudioTagParameterDefinition>();

            Dictionary<char, int> initialCounts = new Dictionary<char, int>();
            HashSet<string> claimedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = parameters[i];
                claimedNames.Add(parameter.Name);
                if (parameter.Name.Length > 1)
                {
                    char initial = char.ToLowerInvariant(parameter.Name[0]);
                    initialCounts.TryGetValue(initial, out int count);
                    initialCounts[initial] = count + 1;
                }

                for (int aliasIndex = 0; aliasIndex < parameter.Aliases.Count; aliasIndex++)
                    claimedNames.Add(parameter.Aliases[aliasIndex]);
            }

            TextStudioTagParameterDefinition[] result =
                new TextStudioTagParameterDefinition[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = parameters[i];
                List<string> aliases = new List<string>(parameter.Aliases.Count + 1);
                for (int aliasIndex = 0; aliasIndex < parameter.Aliases.Count; aliasIndex++)
                    aliases.Add(parameter.Aliases[aliasIndex]);

                if (parameter.Name.Length > 1)
                {
                    char initial = char.ToLowerInvariant(parameter.Name[0]);
                    string alias = initial.ToString();
                    if (initialCounts[initial] == 1 && !claimedNames.Contains(alias))
                    {
                        aliases.Add(alias);
                        claimedNames.Add(alias);
                    }
                }

                result[i] = new TextStudioTagParameterDefinition(
                    parameter.Name,
                    parameter.Kind,
                    parameter.Required,
                    parameter.PositionalIndex,
                    parameter.DisplayName,
                    parameter.Description,
                    parameter.SuggestionKey,
                    ToArray(parameter.AllowedValues),
                    aliases.Count > 0 ? aliases.ToArray() : null,
                    parameter.DefaultValue,
                    parameter.ExampleValue,
                    parameter.Minimum,
                    parameter.Maximum,
                    parameter.Snap,
                    parameter.Suffix,
                    parameter.AllowOverflow);
            }

            return result;
        }

        static string[] ToArray(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] result = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                result[i] = values[i];
            return result;
        }
    }
}
