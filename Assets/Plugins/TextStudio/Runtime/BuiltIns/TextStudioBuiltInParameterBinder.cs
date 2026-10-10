using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    internal static class TextStudioBuiltInParameterBinder
    {
        internal static bool Apply(CodeEffectBase effect, in TextStudioTagParameters parameters)
        {
            TextStudioTagParameters normalized = NormalizeParameters(effect, in parameters);
            return ApplyNormalized(effect, in normalized);
        }

        static bool ApplyNormalized(CodeEffectBase effect, in TextStudioTagParameters parameters)
        {
            switch (effect)
            {
                case AccordionEffect value: return Apply(value, in parameters);
                case BlinkEffect value: return Apply(value, in parameters);
                case BounceEffect value: return Apply(value, in parameters);
                case BurnEffect value: return Apply(value, in parameters);
                case CardFlipYEffect value: return Apply(value, in parameters);
                case ChromaticGlitchEffect value: return Apply(value, in parameters);
                case ColorPulseEffect value: return Apply(value, in parameters);
                case ColorSweepEffect value: return Apply(value, in parameters);
                case CornerWarpEffect value: return Apply(value, in parameters);
                case LensMagnifierEffect value: return Apply(value, in parameters);
                case DangleEffect value: return Apply(value, in parameters);
                case DecryptEffect value: return Apply(value, in parameters);
                case DissolveEffect value: return Apply(value, in parameters);
                case DominoLineEffect value: return Apply(value, in parameters);
                case ElasticEffect value: return Apply(value, in parameters);
                case EmphasisEffect value: return Apply(value, in parameters);
                case ExpandEffect value: return Apply(value, in parameters);
                case FadeEffect value: return Apply(value, in parameters);
                case FloatEffect value: return Apply(value, in parameters);
                case FocusEffect value: return Apply(value, in parameters);
                case FoldAwayEffect value: return Apply(value, in parameters);
                case GlitchEffect value: return Apply(value, in parameters);
                case GradientFlowEffect value: return Apply(value, in parameters);
                case HeroDepthRiseEffect value: return Apply(value, in parameters);
                case OrbitEffect value: return Apply(value, in parameters);
                case OutlineGlowEffect value: return Apply(value, in parameters);
                case PendulumEffect value: return Apply(value, in parameters);
                case PixelGridEffect value: return Apply(value, in parameters);
                case SquiggleEffect value: return Apply(value, in parameters);
                case PopEffect value: return Apply(value, in parameters);
                case PulseEffect value: return Apply(value, in parameters);
                case RainbowEffect value: return Apply(value, in parameters);
                case RollingRibbonEffect value: return Apply(value, in parameters);
                case RotateEffect value: return Apply(value, in parameters);
                case Scatter2DEffect value: return Apply(value, in parameters);
                case ShakeEffect value: return Apply(value, in parameters);
                case ShearEffect value: return Apply(value, in parameters);
                case ShimmerEffect value: return Apply(value, in parameters);
                case SizeEffect value: return Apply(value, in parameters);
                case SlideHorizontalEffect value: return Apply(value, in parameters);
                case SlideVerticalEffect value: return Apply(value, in parameters);
                case SoftSpatialFloatEffect value: return Apply(value, in parameters);
                case SpatialScatterEffect value: return Apply(value, in parameters);
                case SpiralAssembleEffect value: return Apply(value, in parameters);
                case SurfaceSweepEffect value: return Apply(value, in parameters);
                case SwingEffect value: return Apply(value, in parameters);
                case TopHingeEffect value: return Apply(value, in parameters);
                case TypewriterEffect value: return Apply(value, in parameters);
                case VisualTrackingEffect value: return Apply(value, in parameters);
                case WaveEffect value: return Apply(value, in parameters);
                case WiggleEffect value: return Apply(value, in parameters);
                default: return false;
            }
        }

        internal static bool HasParameter(CodeEffectBase effect, string parameterName)
        {
            if (effect == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            IReadOnlyList<TextStudioTagParameterDefinition> definitions = effect.Metadata.Parameters;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].MatchesName(parameterName))
                    return true;
            }

            return false;
        }

        static TextStudioTagParameters NormalizeParameters(
            CodeEffectBase effect,
            in TextStudioTagParameters parameters)
        {
            if (parameters.Named.Count == 0)
                return parameters;

            IReadOnlyList<TextStudioTagParameterDefinition> definitions = effect.Metadata.Parameters;
            IReadOnlyList<TextStudioNamedTagParameter> named = parameters.Named;
            List<TextStudioNamedTagParameter> normalized = null;
            for (int i = 0; i < named.Count; i++)
            {
                TextStudioNamedTagParameter parameter = named[i];
                string canonicalName = null;
                for (int definitionIndex = 0;
                     definitionIndex < definitions.Count;
                     definitionIndex++)
                {
                    TextStudioTagParameterDefinition definition = definitions[definitionIndex];
                    if (!definition.MatchesName(parameter.Name))
                        continue;

                    canonicalName = definition.Name;
                    break;
                }

                if (normalized == null)
                {
                    if (string.Equals(canonicalName, parameter.Name, StringComparison.Ordinal))
                        continue;

                    normalized = new List<TextStudioNamedTagParameter>(named.Count);
                    for (int previous = 0; previous < i; previous++)
                        normalized.Add(named[previous]);
                }
                if (canonicalName != null)
                    normalized.Add(new TextStudioNamedTagParameter(canonicalName, parameter.Value, parameter.IsFlag, parameter.Operation));
            }

            if (normalized == null)
                return parameters;

            string[] positional = new string[parameters.PositionalCount];
            for (int i = 0; i < positional.Length; i++)
                positional[i] = parameters.Positional[i];

            return new TextStudioTagParameters(
                parameters.Raw,
                positional,
                normalized.ToArray());
        }



        static bool Apply(AccordionEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.compression, in parameters, "compression");
            applied |= TryApply(ref value.depth, in parameters, "depth");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(BlinkEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.frequency, in parameters, "frequency", "f");
            applied |= TryApply(ref value.dutyCycle, in parameters, "dutyCycle", "duty");
            applied |= TryApply(ref value.minAlpha, in parameters, "minAlpha", "min");
            applied |= TryApply(ref value.duration, in parameters, "duration");
            applied |= TryApply(ref value.playback, in parameters, "playback");
            return applied;
        }

        static bool Apply(BounceEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.height, in parameters, "height", "a", "amplitude");
            applied |= TryApply(ref value.direction, in parameters, "direction");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.stagger, in parameters, "stagger");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(CardFlipYEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.angle, in parameters, "angle");
            applied |= TryApply(ref value.depth, in parameters, "depth");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(ChromaticGlitchEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.frequency, in parameters, "frequency");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            applied |= TryApply(ref value.activity, in parameters, "activity");
            return applied;
        }

        static bool Apply(ColorPulseEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.colorA, in parameters, "from", "colorA", "c1");
            applied |= TryApply(ref value.colorB, in parameters, "to", "colorB", "c2");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            return applied;
        }

        static bool Apply(ColorSweepEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.color, in parameters, "color", "c", "to");
            applied |= TryApply(ref value.softness, in parameters, "softness", "w");
            applied |= TryApply(ref value.reverse, in parameters, "reverse", "r");
            return applied;
        }

        static bool Apply(CornerWarpEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.mode, in parameters, "mode");
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread");
            return applied;
        }

        static bool Apply(LensMagnifierEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.radius, in parameters, "radius");
            applied |= TryApply(
                ref value.centerMagnification,
                in parameters,
                "centerMagnification");
            applied |= TryApply(
                ref value.edgeMagnification,
                in parameters,
                "edgeMagnification");
            applied |= TryApply(ref value.bulge, in parameters, "bulge");
            applied |= TryApply(ref value.feather, in parameters, "feather");
            return applied;
        }

        static bool Apply(DangleEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.maxDegrees, in parameters, "maxDegrees", "a", "amplitude");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApplyPivot(ref value.pivot, in parameters, "pivot");
            applied |= TryApply(ref value.horizontal, in parameters, "horizontal");
            applied |= TryApply(ref value.vertical, in parameters, "vertical");
            return applied;
        }

        static bool Apply(DecryptEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.shakeStrength, in parameters, "shakeStrength", "strength", "a");
            applied |= TryApply(ref value.shakeSpeed, in parameters, "shakeSpeed", "speed", "s");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            return applied;
        }

        static bool Apply(DissolveEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.softness, in parameters, "softness");
            applied |= TryApply(ref value.space, in parameters, "space");
            return applied;
        }

        static bool Apply(BurnEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.softness, in parameters, "softness");
            applied |= TryApply(ref value.space, in parameters, "space");
            return applied;
        }

        static bool Apply(DominoLineEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.angle, in parameters, "angle");
            applied |= TryApply(ref value.depth, in parameters, "depth");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(ElasticEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.oscillations, in parameters, "oscillations", "cycles");
            applied |= TryApply(ref value.damping, in parameters, "damping");
            applied |= TryApply(ref value.anticipation, in parameters, "anticipation");
            applied |= TryApply(ref value.preserveArea, in parameters, "preserveArea", "preserve");
            return applied;
        }

        static bool Apply(EmphasisEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.amplitude, in parameters, "amplitude", "a");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(ExpandEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.amplitude, in parameters, "amplitude", "a");
            applied |= TryApply(ref value.mode, in parameters, "mode");
            applied |= TryApply(ref value.origin, in parameters, "origin");
            applied |= TryApply(ref value.pulseSpeed, in parameters, "pulseSpeed", "rate");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(FadeEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.minAlpha, in parameters, "minAlpha", "min");
            applied |= TryApply(ref value.maxAlpha, in parameters, "maxAlpha", "max");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApply(ref value.color, in parameters, "color");
            applied |= TryApply(ref value.mode, in parameters, "mode");
            return applied;
        }

        static bool Apply(FloatEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.verticalAmplitude, in parameters, "verticalAmplitude", "vertical", "a");
            applied |= TryApply(ref value.horizontalAmplitude, in parameters, "horizontalAmplitude", "horizontal");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread", "f");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(FocusEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.amount, in parameters, "amount", "a");
            return applied;
        }

        static bool Apply(FoldAwayEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.angle, in parameters, "angle");
            applied |= TryApply(ref value.depth, in parameters, "depth");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(GlitchEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength", "a");
            applied |= TryApply(ref value.frequency, in parameters, "frequency", "f");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            applied |= TryApply(ref value.activity, in parameters, "activity");
            return applied;
        }

        static bool Apply(GradientFlowEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.colorA, in parameters, "colorA", "from");
            applied |= TryApply(ref value.colorB, in parameters, "colorB", "to");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread", "f");
            return applied;
        }

        static bool Apply(HeroDepthRiseEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.lift, in parameters, "lift");
            applied |= TryApply(ref value.tilt, in parameters, "tilt");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(OrbitEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.radius, in parameters, "radius");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread");
            applied |= TryApply(ref value.faceMotion, in parameters, "faceMotion", "face");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(OutlineGlowEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.outline, in parameters, "outline");
            applied |= TryApply(ref value.glow, in parameters, "glow");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.floor, in parameters, "floor");
            return applied;
        }

        static bool Apply(PendulumEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.maxDegrees, in parameters, "maxDegrees", "a", "amplitude");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApplyPivot(ref value.pivot, in parameters, "pivot");
            return applied;
        }

        static bool Apply(PixelGridEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.cellSize, in parameters, "cellSize", "size");
            applied |= TryApply(ref value.activeAmount, in parameters, "activeAmount", "active");
            applied |= TryApply(ref value.transitionAmount, in parameters, "transitionAmount", "transition");
            applied |= TryApply(ref value.space, in parameters, "space");
            applied |= TryApply(ref value.quality, in parameters, "quality");
            applied |= TryApply(ref value.pulseSpeed, in parameters, "pulseSpeed", "pulse", "speed");
            applied |= TryApply(ref value.minCellSize, in parameters, "minCellSize", "min");
            applied |= TryApply(ref value.maxCellSize, in parameters, "maxCellSize", "max");
            return applied;
        }

        static bool Apply(SquiggleEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength", "a");
            applied |= TryApply(ref value.detail, in parameters, "detail");
            applied |= TryApply(ref value.rate, in parameters, "rate", "speed");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            return applied;
        }

        static bool Apply(PopEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.startScale, in parameters, "startScale", "start");
            applied |= TryApply(ref value.overshoot, in parameters, "overshoot", "a");
            return applied;
        }

        static bool Apply(PulseEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.scaleAmplitude, in parameters, "scaleAmplitude", "scale", "a");
            applied |= TryApply(ref value.alphaAmplitude, in parameters, "alphaAmplitude", "alpha");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.glyphPhaseOffset, in parameters, "glyphPhaseOffset", "stagger");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(RainbowEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.saturation, in parameters, "saturation");
            applied |= TryApply(ref value.value, in parameters, "value");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread", "f");
            return applied;
        }

        static bool Apply(RollingRibbonEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.angle, in parameters, "angle");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(RotateEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.startDegrees, in parameters, "startDegrees", "angle", "a", "amplitude");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.loopDegrees, in parameters, "loopDegrees", "loop");
            applied |= TryApply(ref value.axis, in parameters, "axis");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApplyPivot(ref value.pivot, in parameters, "pivot");
            return applied;
        }

        static bool Apply(Scatter2DEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.distance, in parameters, "distance");
            applied |= TryApply(ref value.direction, in parameters, "direction");
            applied |= TryApply(ref value.coneAngle, in parameters, "coneAngle", "cone");
            applied |= TryApply(ref value.radial, in parameters, "radial");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            applied |= TryApply(ref value.overshoot, in parameters, "overshoot");
            return applied;
        }

        static bool Apply(ShakeEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength", "a", "amplitude");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.progressIndexWithTime, in parameters, "progressIndexWithTime", "dynamic");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(ShearEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.amplitude, in parameters, "amplitude", "a");
            applied |= TryApply(ref value.horizontal, in parameters, "horizontal");
            applied |= TryApply(ref value.vertical, in parameters, "vertical");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(ShimmerEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.highlightColor, in parameters, "highlightColor", "color");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.width, in parameters, "width", "w");
            applied |= TryApply(ref value.scaleBoost, in parameters, "scaleBoost", "scale");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApply(ref value.curve, in parameters, "curve");
            return applied;
        }

        static bool Apply(SizeEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.startScale, in parameters, "startScale", "start", "a", "amplitude");
            applied |= TryApply(ref value.endScale, in parameters, "endScale", "end");
            applied |= TryApply(ref value.pulseAmplitude, in parameters, "pulseAmplitude", "pulse");
            applied |= TryApply(ref value.pulseSpeed, in parameters, "pulseSpeed", "rate");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApply(ref value.scale, in parameters, "scale");
            return applied;
        }

        static bool Apply(SlideHorizontalEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.distance, in parameters, "distance", "a", "amplitude");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.stagger, in parameters, "stagger");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApply(ref value.horizontal, in parameters, "horizontal");
            applied |= TryApply(ref value.vertical, in parameters, "vertical");
            return applied;
        }

        static bool Apply(SlideVerticalEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.distance, in parameters, "distance", "a", "amplitude");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.stagger, in parameters, "stagger");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApply(ref value.horizontal, in parameters, "horizontal");
            applied |= TryApply(ref value.vertical, in parameters, "vertical");
            return applied;
        }

        static bool Apply(SoftSpatialFloatEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.spread, in parameters, "spread");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(SpatialScatterEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.strength, in parameters, "strength");
            applied |= TryApply(ref value.seed, in parameters, "seed");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(SpiralAssembleEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.radius, in parameters, "radius");
            applied |= TryApply(ref value.depth, in parameters, "depth");
            applied |= TryApply(ref value.turns, in parameters, "turns");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(SurfaceSweepEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.width, in parameters, "width");
            applied |= TryApply(ref value.angle, in parameters, "angle");
            applied |= TryApply(ref value.brightness, in parameters, "brightness");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApply(ref value.curve, in parameters, "curve");
            return applied;
        }

        static bool Apply(SwingEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.maxDegrees, in parameters, "maxDegrees", "a", "amplitude");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            applied |= TryApplyPivot(ref value.pivot, in parameters, "pivot");
            return applied;
        }

        static bool Apply(TopHingeEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.angle, in parameters, "angle");
            applied |= TryApply(ref value.reducedMotion, in parameters, "reducedMotion", "reduced");
            return applied;
        }

        static bool Apply(TypewriterEffect value, in TextStudioTagParameters parameters)
        {
            return false;
        }

        static bool Apply(VisualTrackingEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.amount, in parameters, "amount");
            applied |= TryApply(ref value.anchor, in parameters, "anchor");
            return applied;
        }

        static bool Apply(WaveEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.amplitude, in parameters, "amplitude", "a");
            applied |= TryApply(ref value.direction, in parameters, "direction");
            applied |= TryApply(ref value.frequency, in parameters, "frequency", "f");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool Apply(WiggleEffect value, in TextStudioTagParameters parameters)
        {
            bool applied = false;
            applied |= TryApply(ref value.positionStrength, in parameters, "positionStrength", "a", "amplitude");
            applied |= TryApply(ref value.rotationDegrees, in parameters, "rotationDegrees", "rotation", "r");
            applied |= TryApply(ref value.speed, in parameters, "speed", "s");
            applied |= TryApply(ref value.progressIndexWithTime, in parameters, "progressIndexWithTime", "dynamic");
            applied |= TryApply(ref value.duration, in parameters, "duration", "d");
            applied |= TryApply(ref value.playback, in parameters, "playback", "p");
            return applied;
        }

        static bool TryApply(ref float target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            if (!TryGetParameter(in parameters, name, alias1, alias2, alias3, out TextStudioNamedTagParameter parameter) ||
                parameter.IsFlag ||
                !float.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float operand) ||
                float.IsNaN(operand) || float.IsInfinity(operand))
                return false;
            float resolved = parameter.Operation == TextStudioTagParameterOperation.Multiply ? target * operand : operand;
            if (float.IsNaN(resolved) || float.IsInfinity(resolved)) return false;
            target = resolved;
            return true;
        }

        static bool TryApply(ref int target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            if (!TryGetParameter(in parameters, name, alias1, alias2, alias3, out TextStudioNamedTagParameter parameter) || parameter.IsFlag) return false;
            if (parameter.Operation == TextStudioTagParameterOperation.Set && int.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int setValue))
            { target = setValue; return true; }
            if (parameter.Operation == TextStudioTagParameterOperation.Multiply && float.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float factor) && !float.IsNaN(factor) && !float.IsInfinity(factor))
            { target = Mathf.RoundToInt(target * factor); return true; }
            return false;
        }

        static bool TryApply(ref bool target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            if (!TryGetParameter(in parameters, name, alias1, alias2, alias3, out TextStudioNamedTagParameter parameter)) return false;
            if (parameter.IsFlag) { target = true; return true; }
            if (parameter.Operation != TextStudioTagParameterOperation.Set) return false;
            if (bool.TryParse(parameter.Value, out bool parsed)) { target = parsed; return true; }
            if (string.Equals(parameter.Value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(parameter.Value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(parameter.Value, "on", StringComparison.OrdinalIgnoreCase))
            { target = true; return true; }
            if (string.Equals(parameter.Value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(parameter.Value, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(parameter.Value, "off", StringComparison.OrdinalIgnoreCase))
            { target = false; return true; }
            return false;
        }

        static bool TryApply(ref string target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            if (!TryGetParameter(in parameters, name, alias1, alias2, alias3, out TextStudioNamedTagParameter parameter) || parameter.IsFlag || parameter.Operation != TextStudioTagParameterOperation.Set) return false;
            target = parameter.Value;
            return true;
        }

        static bool TryApply(ref Vector2 target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            if (!TryGetParameter(in parameters, name, alias1, alias2, alias3, out TextStudioNamedTagParameter parameter) || parameter.IsFlag || parameter.Operation != TextStudioTagParameterOperation.Set) return false;
            if (!TextStudioPivotParser.TryParseCoordinates(parameter.Value, out Vector2 parsed)) return false;
            target = parsed;
            return true;
        }

        static bool TryApplyPivot(
            ref Vector2 target,
            in TextStudioTagParameters parameters,
            string name)
        {
            if (!TryGetParameter(in parameters, name, null, null, null, out TextStudioNamedTagParameter parameter) ||
                parameter.IsFlag ||
                parameter.Operation != TextStudioTagParameterOperation.Set ||
                !TextStudioPivotParser.TryParse(parameter.Value, out Vector2 parsed))
            {
                return false;
            }

            target = parsed;
            return true;
        }

        static bool TryApply(ref Color32 target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            if (!TryGetParameter(in parameters, name, alias1, alias2, alias3, out TextStudioNamedTagParameter parameter) || parameter.IsFlag || parameter.Operation != TextStudioTagParameterOperation.Set || !TextStudioColorUtility.TryParseColor(parameter.Value, out Color32 parsed)) return false;
            target = parsed;
            return true;
        }

        static bool TryApply(ref Color target, in TextStudioTagParameters parameters, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            Color32 parsed = target;
            if (!TryApply(ref parsed, in parameters, name, alias1, alias2, alias3)) return false;
            target = parsed;
            return true;
        }

        static bool TryGetParameter(in TextStudioTagParameters parameters, string name, string alias1, string alias2, string alias3, out TextStudioNamedTagParameter parameter)
        {
            IReadOnlyList<TextStudioNamedTagParameter> named = parameters.Named;
            for (int i = named.Count - 1; i >= 0; i--)
            {
                TextStudioNamedTagParameter candidate = named[i];
                if (!NameMatches(candidate.Name, name, alias1, alias2, alias3)) continue;
                parameter = candidate;
                return true;
            }
            parameter = default;
            return false;
        }

        static bool NameMatches(string value, string name, string alias1 = null, string alias2 = null, string alias3 = null)
        {
            return string.Equals(value, name, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(alias1) && string.Equals(value, alias1, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(alias2) && string.Equals(value, alias2, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(alias3) && string.Equals(value, alias3, StringComparison.OrdinalIgnoreCase));
        }

    }

    internal static class TextStudioPivotParser
    {
        static readonly string[] s_namedValues =
        {
            "topLeft",
            "topCenter",
            "topRight",
            "middleLeft",
            "center",
            "middleRight",
            "bottomLeft",
            "bottomCenter",
            "bottomRight"
        };

        internal static IReadOnlyList<string> NamedValues => s_namedValues;

        internal static bool TryParse(string value, out Vector2 pivot)
        {
            if (string.Equals(value, "topLeft", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(0f, 1f);
            else if (string.Equals(value, "topCenter", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(0.5f, 1f);
            else if (string.Equals(value, "topRight", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(1f, 1f);
            else if (string.Equals(value, "middleLeft", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(0f, 0.5f);
            else if (string.Equals(value, "center", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(0.5f, 0.5f);
            else if (string.Equals(value, "middleRight", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(1f, 0.5f);
            else if (string.Equals(value, "bottomLeft", StringComparison.OrdinalIgnoreCase))
                pivot = Vector2.zero;
            else if (string.Equals(value, "bottomCenter", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(0.5f, 0f);
            else if (string.Equals(value, "bottomRight", StringComparison.OrdinalIgnoreCase))
                pivot = new Vector2(1f, 0f);
            else
                return TryParseCoordinates(value, out pivot);

            return true;
        }

        internal static bool TryParseCoordinates(string value, out Vector2 vector)
        {
            vector = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                float.IsNaN(x) ||
                float.IsInfinity(x) ||
                float.IsNaN(y) ||
                float.IsInfinity(y))
            {
                return false;
            }

            vector = new Vector2(x, y);
            return true;
        }

        internal static string Format(Vector2 pivot)
        {
            for (int i = 0; i < s_namedValues.Length; i++)
            {
                if (TryParse(s_namedValues[i], out Vector2 namedPivot) &&
                    namedPivot == pivot)
                {
                    return s_namedValues[i];
                }
            }

            return FormatCoordinates(pivot);
        }

        internal static string FormatCoordinates(Vector2 value)
        {
            return value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                   value.y.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
