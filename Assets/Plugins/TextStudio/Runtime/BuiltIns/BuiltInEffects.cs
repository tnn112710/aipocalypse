using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Internal prototypes for the code-defined effect library. Parameterized effects are cloned
    /// from these definitions so authored configuration never mutates shared runtime state.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Built-in effect definitions are immutable and cloned before authored configuration is applied.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class BuiltInEffects
    {
        public static readonly WaveEffect Wave = new WaveEffect();
        public static readonly ShakeEffect Shake = new ShakeEffect();
        public static readonly WiggleEffect Wiggle = new WiggleEffect();
        public static readonly BounceEffect Bounce = new BounceEffect();
        public static readonly SwingEffect Swing = new SwingEffect();
        public static readonly PendulumEffect Pendulum = new PendulumEffect();
        public static readonly DangleEffect Dangle = new DangleEffect();
        public static readonly PulseEffect Pulse = new PulseEffect();
        public static readonly BlinkEffect Blink = new BlinkEffect();
        public static readonly ShimmerEffect Shimmer = new ShimmerEffect();
        public static readonly GradientFlowEffect GradientFlow = new GradientFlowEffect();
        public static readonly ColorSweepEffect ColorSweep = new ColorSweepEffect();
        public static readonly ColorPulseEffect ColorPulse = new ColorPulseEffect();
        public static readonly PopEffect Pop = new PopEffect();
        public static readonly FloatEffect Float = new FloatEffect();
        public static readonly RainbowEffect Rainbow = new RainbowEffect();
        public static readonly FadeEffect Fade = new FadeEffect();
        public static readonly SizeEffect Size = new SizeEffect();
        public static readonly RotateEffect Rotate = new RotateEffect();
        public static readonly ShearEffect Shear = new ShearEffect();
        public static readonly SlideHorizontalEffect SlideHorizontal = new SlideHorizontalEffect();
        public static readonly SlideVerticalEffect SlideVertical = new SlideVerticalEffect();
        public static readonly ExpandEffect Expand = new ExpandEffect();
        public static readonly TypewriterEffect Typewriter = new TypewriterEffect();
        public static readonly DecryptEffect Decrypt = new DecryptEffect();
        public static readonly GlitchEffect Glitch = new GlitchEffect();
        public static readonly EmphasisEffect Emphasis = new EmphasisEffect();
        public static readonly HeroDepthRiseEffect HeroDepthRise = new HeroDepthRiseEffect();
        public static readonly CardFlipYEffect CardFlipY = new CardFlipYEffect();
        public static readonly TopHingeEffect TopHinge = new TopHingeEffect();
        public static readonly DominoLineEffect DominoLine = new DominoLineEffect();
        public static readonly SpiralAssembleEffect SpiralAssemble = new SpiralAssembleEffect();
        public static readonly SpatialScatterEffect SpatialScatter = new SpatialScatterEffect();
        public static readonly FoldAwayEffect FoldAway = new FoldAwayEffect();
        public static readonly RollingRibbonEffect RollingRibbon = new RollingRibbonEffect();
        public static readonly SoftSpatialFloatEffect SoftSpatialFloat = new SoftSpatialFloatEffect();
        public static readonly AccordionEffect Accordion = new AccordionEffect();
        public static readonly OrbitEffect Orbit = new OrbitEffect();
        public static readonly ElasticEffect Elastic = new ElasticEffect();
        public static readonly Scatter2DEffect Scatter2D = new Scatter2DEffect();
        public static readonly VisualTrackingEffect Tracking = new VisualTrackingEffect();
        public static readonly CornerWarpEffect CornerWarp = new CornerWarpEffect();
        public static readonly FocusEffect Focus = new FocusEffect();
        public static readonly DissolveEffect Dissolve = new DissolveEffect();
        public static readonly BurnEffect Burn = new BurnEffect();
        public static readonly OutlineGlowEffect OutlineGlow = new OutlineGlowEffect();
        public static readonly SurfaceSweepEffect SurfaceSweep = new SurfaceSweepEffect();
        public static readonly ChromaticGlitchEffect ChromaticGlitch = new ChromaticGlitchEffect();
        public static readonly PixelGridEffect PixelGrid = new PixelGridEffect();
        public static readonly SquiggleEffect Squiggle = new SquiggleEffect();
        public static readonly LensMagnifierEffect LensMagnifier = new LensMagnifierEffect();

        public static System.Collections.Generic.IEnumerable<CodeEffectBase> All()
        {
            yield return Wave;
            yield return Shake;
            yield return Wiggle;
            yield return Bounce;
            yield return Swing;
            yield return Pendulum;
            yield return Dangle;
            yield return Pulse;
            yield return Blink;
            yield return Shimmer;
            yield return GradientFlow;
            yield return ColorSweep;
            yield return ColorPulse;
            yield return Pop;
            yield return Float;
            yield return Rainbow;
            yield return Fade;
            yield return Size;
            yield return Rotate;
            yield return Shear;
            yield return SlideHorizontal;
            yield return SlideVertical;
            yield return Expand;
            yield return Typewriter;
            yield return Decrypt;
            yield return Glitch;
            yield return Emphasis;
            yield return HeroDepthRise;
            yield return CardFlipY;
            yield return TopHinge;
            yield return DominoLine;
            yield return SpiralAssemble;
            yield return SpatialScatter;
            yield return FoldAway;
            yield return RollingRibbon;
            yield return SoftSpatialFloat;
            yield return Accordion;
            yield return Orbit;
            yield return Elastic;
            yield return Scatter2D;
            yield return Tracking;
            yield return CornerWarp;
            yield return Focus;
            yield return Dissolve;
            yield return Burn;
            yield return OutlineGlow;
            yield return SurfaceSweep;
            yield return ChromaticGlitch;
            yield return PixelGrid;
            yield return Squiggle;
            yield return LensMagnifier;
        }
    }

    static class BuiltInEffectTiming
    {
        public static float EvaluateActivePlayback(string playback, float time, float phaseProgress, float duration)
        {
            return EvaluateActivePlayback(playback, time, time, phaseProgress, duration);
        }

        // Every value accepted here makes EvaluateActivePlayback return exactly 1, so a persistent shortcut can drop the envelope without changing a result.
        public static bool IsInfinitePlayback(string playback)
        {
            return ReferenceEquals(playback, "infinite") ||
                   string.IsNullOrWhiteSpace(playback) ||
                   string.Equals(playback, "infinite", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(playback, "forever", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(playback, "persistent", StringComparison.OrdinalIgnoreCase);
        }

        public static float EvaluateActivePlayback(
            string playback,
            float time,
            float playbackTime,
            float phaseProgress,
            float duration)
        {
            float d = Mathf.Max(0.0001f, duration);
            if (ReferenceEquals(playback, "infinite") ||
                ReferenceEquals(playback, "forever") ||
                ReferenceEquals(playback, "persistent"))
            {
                return 1f;
            }
            if (ReferenceEquals(playback, "loop") ||
                ReferenceEquals(playback, "cycle") ||
                ReferenceEquals(playback, "pingpong"))
            {
                return Mathf.PingPong(time / d, 1f);
            }
            if (ReferenceEquals(playback, "once") || ReferenceEquals(playback, "short"))
                return playbackTime >= d * 2f ? 0f : Mathf.PingPong(playbackTime / d, 1f);
            if (ReferenceEquals(playback, "external") || ReferenceEquals(playback, "weighted"))
                return Mathf.Clamp01(phaseProgress);

            if (string.IsNullOrWhiteSpace(playback) ||
                string.Equals(playback, "infinite", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "forever", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "persistent", System.StringComparison.OrdinalIgnoreCase))
            {
                return 1f;
            }

            if (string.Equals(playback, "loop", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "cycle", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "pingpong", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.PingPong(time / d, 1f);
            }

            if (string.Equals(playback, "once", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "short", System.StringComparison.OrdinalIgnoreCase))
            {
                return playbackTime >= d * 2f ? 0f : Mathf.PingPong(playbackTime / d, 1f);
            }

            if (string.Equals(playback, "external", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "weighted", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01(phaseProgress);
            }

            string normalized = NormalizePlayback(playback);
            if (string.Equals(normalized, "infinite", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "forever", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "persistent", System.StringComparison.OrdinalIgnoreCase))
            {
                return 1f;
            }

            if (string.Equals(normalized, "loop", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "cycle", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.PingPong(time / d, 1f);
            }

            if (string.Equals(normalized, "pingpong", System.StringComparison.OrdinalIgnoreCase))
                return Mathf.PingPong(time / d, 1f);

            if (string.Equals(normalized, "once", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "short", System.StringComparison.OrdinalIgnoreCase))
            {
                return playbackTime >= d * 2f ? 0f : Mathf.PingPong(playbackTime / d, 1f);
            }

            if (string.Equals(normalized, "external", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "weighted", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01(phaseProgress);
            }

            string registeredId = string.IsNullOrWhiteSpace(playback) ? string.Empty : playback.Trim();
            if (!string.IsNullOrEmpty(registeredId) &&
                TextStudioRegistry.Playbacks.TryGetQuiet(registeredId, out IEffectPlayback registeredPlayback) &&
                registeredPlayback != null)
            {
                float source = registeredPlayback.Mode == PlaybackMode.External
                    ? phaseProgress
                    : UsesInvocationTime(registeredPlayback)
                        ? playbackTime
                        : time;
                return registeredPlayback.Evaluate(source, 0f);
            }

            return Mathf.Clamp01(phaseProgress);
        }

        public static float EvaluateActivePlayback(
            string playback,
            in EffectEvaluationContext context,
            float duration)
        {
            return EvaluateActivePlayback(
                playback,
                context.time,
                context.playbackTime,
                context.phaseProgress,
                duration);
        }

        public static bool TryGetFinitePlaybackDuration(
            string playback,
            float duration,
            out float totalDuration)
        {
            totalDuration = 0f;
            float d = Mathf.Max(0.0001f, duration);
            string normalized = NormalizePlayback(playback);
            if (string.Equals(normalized, "once", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "short", System.StringComparison.OrdinalIgnoreCase))
            {
                totalDuration = d * 2f;
                return true;
            }

            string registeredId = string.IsNullOrWhiteSpace(playback) ? string.Empty : playback.Trim();
            if (string.IsNullOrEmpty(registeredId) ||
                !TextStudioRegistry.Playbacks.TryGetQuiet(registeredId, out IEffectPlayback registeredPlayback) ||
                registeredPlayback is not EffectPlaybackAsset asset ||
                asset.IsInfinite)
            {
                return false;
            }

            totalDuration = asset.GetTotalDuration();
            return !float.IsNaN(totalDuration) && !float.IsInfinity(totalDuration);
        }

        public static float EvaluatePersistentEnvelope(string playback, in EffectEvaluationContext ctx, float duration)
        {
            return ctx.phase == AnimationPhase.Persistent
                ? EvaluateActivePlayback(playback, in ctx, duration)
                : 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolvePersistentMotionTime(
            string playback,
            float duration,
            float time,
            float playbackTime)
        {
            float d = Mathf.Max(0.0001f, duration);
            if (ReferenceEquals(playback, "loop") ||
                ReferenceEquals(playback, "cycle") ||
                ReferenceEquals(playback, "pingpong"))
            {
                return Mathf.Repeat(time, d * 2f);
            }

            if (ReferenceEquals(playback, "once") || ReferenceEquals(playback, "short"))
                return playbackTime;

            if (!string.IsNullOrWhiteSpace(playback) &&
                (string.Equals(playback, "loop", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(playback, "cycle", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(playback, "pingpong", System.StringComparison.OrdinalIgnoreCase)))
            {
                return Mathf.Repeat(time, d * 2f);
            }

            if (time == playbackTime)
                return time;

            if (string.IsNullOrWhiteSpace(playback) ||
                ReferenceEquals(playback, "infinite") ||
                ReferenceEquals(playback, "forever") ||
                ReferenceEquals(playback, "persistent") ||
                ReferenceEquals(playback, "loop") ||
                ReferenceEquals(playback, "cycle") ||
                ReferenceEquals(playback, "pingpong") ||
                ReferenceEquals(playback, "external") ||
                ReferenceEquals(playback, "weighted"))
            {
                return time;
            }

            if (string.Equals(playback, "once", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(playback, "short", System.StringComparison.OrdinalIgnoreCase))
            {
                return playbackTime;
            }

            return TryGetFinitePlaybackDuration(playback, duration, out _)
                ? playbackTime
                : time;
        }

        static bool UsesInvocationTime(IEffectPlayback playback)
        {
            if (playback is EffectPlaybackAsset asset)
                return !asset.IsInfinite;

            return playback.Mode == PlaybackMode.Once || playback.Mode == PlaybackMode.Weighted;
        }

        static string NormalizePlayback(string playback)
        {
            if (string.IsNullOrWhiteSpace(playback))
                return "infinite";

            return playback.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
        }
    }

    static class BuiltInProgressTiming
    {
        internal const float DefaultDelayBetweenCycles = 0.75f;

        internal static float Evaluate(
            in EffectEvaluationContext context,
            float speed,
            string playback,
            string curve)
        {
            if (context.hasEffectProgress)
                return Mathf.Clamp01(context.effectProgress);

            float duration = 1f / Mathf.Max(0.01f, speed);
            string normalizedPlayback = Normalize(playback);
            float progress;
            if (normalizedPlayback == "external" ||
                normalizedPlayback == "playback.external" ||
                normalizedPlayback == "weighted" ||
                normalizedPlayback == "playback.weighted")
            {
                progress = context.phaseProgress;
            }
            else if (normalizedPlayback == "once" ||
                     normalizedPlayback == "playback.once" ||
                     normalizedPlayback == "appearance" ||
                     normalizedPlayback == "playback.appearance" ||
                     normalizedPlayback == "short")
            {
                progress = Mathf.Clamp01(context.playbackTime / duration);
            }
            else if (normalizedPlayback == "pingpong" || normalizedPlayback == "playback.pingpong")
            {
                progress = EvaluatePingPong(
                    context.time,
                    duration,
                    DefaultDelayBetweenCycles);
            }
            else if (normalizedPlayback == "loop" ||
                     normalizedPlayback == "playback.loop" ||
                     normalizedPlayback == "cycle" ||
                     normalizedPlayback == "playback.cycle" ||
                     normalizedPlayback == "infinite" ||
                     normalizedPlayback == "forever" ||
                     normalizedPlayback == "persistent" ||
                     string.IsNullOrEmpty(normalizedPlayback))
            {
                progress = EvaluateLoop(
                    context.time,
                    duration,
                    DefaultDelayBetweenCycles);
            }
            else if (TextStudioEffects.TryGetPlayback(
                         playback,
                         out IEffectPlayback registeredPlayback) &&
                     registeredPlayback != null)
            {
                float source = registeredPlayback.Mode switch
                {
                    PlaybackMode.External => context.phaseProgress,
                    PlaybackMode.Once or PlaybackMode.Weighted => context.playbackTime,
                    _ => context.time
                };
                progress = registeredPlayback.Evaluate(source, duration);
            }
            else
            {
                progress = EvaluateLoop(
                    context.time,
                    duration,
                    DefaultDelayBetweenCycles);
            }

            return Mathf.Clamp01(EvaluateCurve(curve, Mathf.Clamp01(progress)));
        }

        static float EvaluateLoop(float time, float duration, float delay)
        {
            float cycleTime = Mathf.Repeat(
                Mathf.Max(0f, time),
                duration + Mathf.Max(0f, delay));
            return cycleTime >= duration ? 1f : cycleTime / duration;
        }

        static float EvaluatePingPong(float time, float duration, float delay)
        {
            float motionDuration = duration * 2f;
            float cycleTime = Mathf.Repeat(
                Mathf.Max(0f, time),
                motionDuration + Mathf.Max(0f, delay));
            if (cycleTime >= motionDuration)
                return 0f;
            return cycleTime <= duration
                ? cycleTime / duration
                : 1f - (cycleTime - duration) / duration;
        }

        static float EvaluateCurve(string curve, float progress)
        {
            string normalized = Normalize(curve);
            switch (normalized)
            {
                case "reverse":
                case "reverselinear":
                case "curve.reverselinear":
                    return 1f - progress;
                case "sine":
                case "curve.sine":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.Sine,
                        progress);
                case "halfsine":
                case "curve.halfsine":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.HalfSine,
                        progress);
                case "square":
                case "curve.square":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.Square,
                        progress);
                case "step":
                case "curve.step":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.Step,
                        progress);
                case "hold":
                case "curve.hold":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.Hold,
                        progress);
                case "bounce":
                case "curve.bounce":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.Bounce,
                        progress);
                case "easein":
                case "curve.easein":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.EaseIn,
                        progress);
                case "easeout":
                case "curve.easeout":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.EaseOut,
                        progress);
                case "easeinout":
                case "curve.easeinout":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.EaseInOut,
                        progress);
                case "backout":
                case "curve.backout":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.BackOut,
                        progress);
                case "elasticout":
                case "curve.elasticout":
                    return TextStudioEffectCurveUtility.Evaluate(
                        TextStudioEffectCurvePreset.ElasticOut,
                        progress);
                case "linear":
                case "curve.linear":
                case "":
                    return progress;
            }

            return TextStudioEffects.TryGetCurve(curve, out ITextStudioCurve registeredCurve) &&
                   registeredCurve != null
                ? registeredCurve.Evaluate(progress)
                : progress;
        }

        static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }
    }

    static class BuiltInEffectMath
    {
        const int TrigTableSize = 4096;
        const int TrigTableMask = TrigTableSize - 1;
        const int TrigQuarterTurn = TrigTableSize / 4;
        const float TrigTableScale = TrigTableSize / (Mathf.PI * 2f);
        static readonly float[] SineTable = CreateSineTable();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion ZRotation(float degrees)
        {
            float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
            FastRotationSinCos(halfRadians, out float sine, out float cosine);
            return new Quaternion(0f, 0f, sine, cosine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyZRotation(ref Quaternion rotation, float degrees)
        {
            float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
            FastRotationSinCos(halfRadians, out float sine, out float cosine);
            if (rotation.x == 0f && rotation.y == 0f)
            {
                float z = rotation.z;
                float w = rotation.w;
                rotation.z = cosine * z + sine * w;
                rotation.w = cosine * w - sine * z;
                return;
            }

            rotation = new Quaternion(0f, 0f, sine, cosine) * rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyAxisRotation(ref Quaternion rotation, float degrees, string axis)
        {
            // The default axis literal skips two case-insensitive compares per glyph.
            if (ReferenceEquals(axis, "z"))
            {
                ApplyZRotation(ref rotation, degrees);
                return;
            }

            if (string.Equals(axis, "x", StringComparison.OrdinalIgnoreCase))
            {
                float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
                FastRotationSinCos(halfRadians, out float sine, out float cosine);
                rotation = new Quaternion(sine, 0f, 0f, cosine) * rotation;
                return;
            }

            if (string.Equals(axis, "y", StringComparison.OrdinalIgnoreCase))
            {
                float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
                FastRotationSinCos(halfRadians, out float sine, out float cosine);
                rotation = new Quaternion(0f, sine, 0f, cosine) * rotation;
                return;
            }

            ApplyZRotation(ref rotation, degrees);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplySmallZRotation(ref Quaternion rotation, float degrees)
        {
            float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
            float square = halfRadians * halfRadians;
            float sine = halfRadians * (1f - square * (1f / 6f));
            float cosine = 1f - square * 0.5f;
            if (rotation.x == 0f && rotation.y == 0f)
            {
                float z = rotation.z;
                float w = rotation.w;
                rotation.z = cosine * z + sine * w;
                rotation.w = cosine * w - sine * z;
                return;
            }

            rotation = new Quaternion(0f, 0f, sine, cosine) * rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetZRotation(ref Quaternion rotation, float degrees)
        {
            float halfRadians = degrees * Mathf.Deg2Rad * 0.5f;
            FastRotationSinCos(halfRadians, out rotation.z, out rotation.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSin(float radians)
        {
            float scaled = radians * TrigTableScale;
            int lower = (int)scaled;
            if (scaled < lower)
                lower--;
            float fraction = scaled - lower;
            int index = lower & TrigTableMask;
            float from = SineTable[index];
            return from + (SineTable[(index + 1) & TrigTableMask] - from) * fraction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastCos(float radians)
        {
            float scaled = radians * TrigTableScale;
            int lower = (int)scaled;
            if (scaled < lower)
                lower--;
            float fraction = scaled - lower;
            int index = (lower + TrigQuarterTurn) & TrigTableMask;
            float from = SineTable[index];
            return from + (SineTable[(index + 1) & TrigTableMask] - from) * fraction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothRandomSample(float samplePosition, int unitIndex, int salt)
        {
            int sampleIndex = Mathf.FloorToInt(samplePosition);
            float blend = samplePosition - sampleIndex;
            blend = blend * blend * (3f - 2f * blend);
            int fromKey = unchecked(unitIndex + sampleIndex * 4099);
            int toKey = unchecked(unitIndex + (sampleIndex + 1) * 4099);
            float from = SpatialTransformEffectMath.Hash01(fromKey, salt);
            float to = SpatialTransformEffectMath.Hash01(toKey, salt);
            return (from + (to - from) * blend) * 2f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSinSample(float radians)
        {
            return SineTable[(int)(radians * TrigTableScale) & TrigTableMask];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FastSinCosSample(float radians, out float sine, out float cosine)
        {
            int index = (int)(radians * TrigTableScale) & TrigTableMask;
            sine = SineTable[index];
            cosine = SineTable[(index + TrigQuarterTurn) & TrigTableMask];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FastSinCos(float radians, out float sine, out float cosine)
        {
            float scaled = radians * TrigTableScale;
            int lower = (int)scaled;
            if (scaled < lower)
                lower--;
            float fraction = scaled - lower;
            int sineIndex = lower & TrigTableMask;
            int cosineIndex = (lower + TrigQuarterTurn) & TrigTableMask;
            float sineFrom = SineTable[sineIndex];
            float cosineFrom = SineTable[cosineIndex];
            sine = sineFrom + (SineTable[(sineIndex + 1) & TrigTableMask] - sineFrom) * fraction;
            cosine = cosineFrom + (SineTable[(cosineIndex + 1) & TrigTableMask] - cosineFrom) * fraction;
            float magnitudeCorrection = 1.5f - 0.5f * (sine * sine + cosine * cosine);
            sine *= magnitudeCorrection;
            cosine *= magnitudeCorrection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FastRotationSinCos(float radians, out float sine, out float cosine)
        {
            if (Mathf.Abs(radians) <= 0.25f)
            {
                float squared = radians * radians;
                float fourth = squared * squared;
                sine = radians * (1f - squared * (1f / 6f) + fourth * (1f / 120f));
                cosine = 1f - squared * 0.5f + fourth * (1f / 24f);
                return;
            }

            FastSinCos(radians, out sine, out cosine);
        }

        static float[] CreateSineTable()
        {
            float[] table = new float[TrigTableSize];
            float step = Mathf.PI * 2f / TrigTableSize;
            for (int i = 0; i < table.Length; i++)
                table[i] = Mathf.Sin(i * step);
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 NormalizeDirection(Vector2 direction)
        {
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
                return Vector2.up;
            if (Mathf.Abs(lengthSquared - 1f) <= 0.000001f)
                return direction;
            return direction / Mathf.Sqrt(lengthSquared);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 HsvToRgb32(float hue, float saturation, float value)
        {
            float scaledHue = hue * 6f;
            int sector = (int)scaledHue;
            float fraction = scaledHue - sector;
            float minimum = value * (1f - saturation);
            float descending = value * (1f - saturation * fraction);
            float ascending = value * (1f - saturation * (1f - fraction));
            float red;
            float green;
            float blue;
            switch (sector)
            {
                case 0:
                    red = value;
                    green = ascending;
                    blue = minimum;
                    break;
                case 1:
                    red = descending;
                    green = value;
                    blue = minimum;
                    break;
                case 2:
                    red = minimum;
                    green = value;
                    blue = ascending;
                    break;
                case 3:
                    red = minimum;
                    green = descending;
                    blue = value;
                    break;
                case 4:
                    red = ascending;
                    green = minimum;
                    blue = value;
                    break;
                default:
                    red = value;
                    green = minimum;
                    blue = descending;
                    break;
            }

            return new Color32(
                (byte)(red * 255f),
                (byte)(green * 255f),
                (byte)(blue * 255f),
                255);
        }
    }

    static class TextAnimatorStateCompatibility
    {
        const float ShearAmountScale = 0.15f;

        public static bool HasShearAxisParameter(string raw)
        {
            return !string.IsNullOrWhiteSpace(raw);
        }

        public static void ApplyShear(
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext ctx,
            float amplitude,
            string horizontal,
            string vertical,
            float speed,
            string playback,
            float duration)
        {
            float horizontalSign = ResolveHorizontalShear(horizontal);
            float verticalSign = ResolveVerticalShear(vertical);
            if (Mathf.Abs(horizontalSign) <= 0.0001f && Mathf.Abs(verticalSign) <= 0.0001f)
                return;

            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float phase = (ctx.time * speed + ctx.normalizedIndex * 0.25f) * Mathf.PI * 2f;
            float amount = BuiltInEffectMath.FastSin(phase) * amplitude * ShearAmountScale * envelope;
            state.skew.x += amount * horizontalSign;
            state.skew.y += amount * verticalSign;
        }

        public static TextStudioColorBlendMode ParseColorMode(string raw)
        {
            string normalized = NormalizeMode(raw);
            if (string.IsNullOrEmpty(normalized) ||
                string.Equals(normalized, "1", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "full", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "set", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "setcolor", System.StringComparison.OrdinalIgnoreCase))
            {
                return TextStudioColorBlendMode.Set;
            }

            if (string.Equals(normalized, "0", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "alpha", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "alphaonly", System.StringComparison.OrdinalIgnoreCase))
            {
                return TextStudioColorBlendMode.AlphaOnly;
            }

            if (string.Equals(normalized, "multiply", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "mul", System.StringComparison.OrdinalIgnoreCase))
            {
                return TextStudioColorBlendMode.Multiply;
            }

            return TextStudioColorBlendMode.Set;
        }

        static float ResolveHorizontalShear(string raw)
        {
            string normalized = NormalizeMode(raw);
            if (string.IsNullOrEmpty(normalized) ||
                string.Equals(normalized, "0", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "none", System.StringComparison.OrdinalIgnoreCase))
            {
                return 0f;
            }

            if (string.Equals(normalized, "2", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "left", System.StringComparison.OrdinalIgnoreCase))
            {
                return -1f;
            }

            return 1f;
        }

        static float ResolveVerticalShear(string raw)
        {
            string normalized = NormalizeMode(raw);
            if (string.IsNullOrEmpty(normalized) ||
                string.Equals(normalized, "0", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "none", System.StringComparison.OrdinalIgnoreCase))
            {
                return 0f;
            }

            if (string.Equals(normalized, "2", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "bottom", System.StringComparison.OrdinalIgnoreCase))
            {
                return -1f;
            }

            return 1f;
        }

        static string NormalizeMode(string raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : raw.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        }
    }

    // ────────────────────────── Persistent motion ──────────────────────────

    internal sealed class WaveEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Wave;
        public override string DisplayName => "Wave";
        public override string Description => "Vertical sine wave traveling through the text.";

        public float amplitude = 0.12f;
        public Vector2 direction = Vector2.up;
        public float frequency = 0.65f;
        public float speed = 1.05f;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;
        internal bool SupportsInfinitePersistentFastPath => BuiltInEffectTiming.IsInfinitePlayback(playback);
        internal Vector2 NormalizedDirection => BuiltInEffectMath.NormalizeDirection(direction);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float phase = ctx.normalizedIndex * frequency + ctx.time * speed;
            float envelope = ctx.phase == AnimationPhase.Persistent
                ? BuiltInEffectTiming.EvaluateActivePlayback(playback, in ctx, duration)
                : 1f;
            Vector2 axis = BuiltInEffectMath.NormalizeDirection(direction);
            float offset = BuiltInEffectMath.FastSin(phase * Mathf.PI * 2f) * amplitude * envelope;
            state.positionOffset += new Vector3(axis.x * offset, axis.y * offset, 0f);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float phase = normalizedIndex * frequency + time * speed;
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            Vector2 axis = BuiltInEffectMath.NormalizeDirection(direction);
            float offset = BuiltInEffectMath.FastSin(phase * Mathf.PI * 2f) * amplitude * envelope;
            state.positionOffset += new Vector3(axis.x * offset, axis.y * offset, 0f);
        }

        // Matches EvaluatePersistentFast for infinite playback, whose envelope is exactly 1, with the direction normalized once by the caller.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float normalizedIndex,
            in Vector2 normalizedDirection)
        {
            float phase = normalizedIndex * frequency + time * speed;
            float offset = BuiltInEffectMath.FastSin(phase * Mathf.PI * 2f) * amplitude;
            state.positionOffset += new Vector3(normalizedDirection.x * offset, normalizedDirection.y * offset, 0f);
        }
    }

    [TextStudioEffectAlias("jitter", "Search alias only; existing jitter markup resolves to shake.")]
    [TextStudioEffectAlias("tremble")]
    [TextStudioEffectAlias("rumble")]
    internal sealed class ShakeEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Shake;
        public override string DisplayName => "Shake";
        public override string Description => "Rapid deterministic pseudo-random jitter per glyph.";

        public float strength = 0.08f;
        public float speed = 20f;
        public int seed = 1931;
        public bool progressIndexWithTime = true;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = ctx.phase != AnimationPhase.Persistent || ReferenceEquals(playback, "infinite")
                ? 1f
                : BuiltInEffectTiming.EvaluateActivePlayback(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            ApplyShake(ref state, ctx.time, ctx.unitIndex, envelope);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = ReferenceEquals(playback, "infinite")
                ? 1f
                : BuiltInEffectTiming.EvaluateActivePlayback(
                    playback,
                    time,
                    playbackTime,
                    phaseProgress,
                    duration);
            if (envelope <= 0f)
                return;

            ApplyShake(ref state, time, unitIndex, envelope);
        }

        void ApplyShake(ref EvaluatedGlyphState state, float time, int unitIndex, float envelope)
        {
            float samplePosition = progressIndexWithTime ? time * speed : 0f;
            float x = BuiltInEffectMath.SmoothRandomSample(samplePosition, unitIndex, seed);
            float y = BuiltInEffectMath.SmoothRandomSample(
                samplePosition,
                unitIndex,
                unchecked(seed + 7346));
            state.positionOffset.x += x * strength * envelope;
            state.positionOffset.y += y * strength * envelope;
        }
    }

    [TextStudioEffectAlias("wobble")]
    [TextStudioEffectAlias("wiggly")]
    internal sealed class WiggleEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Wiggle;
        public override string DisplayName => "Wiggle";
        public override string Description => "Small organic position + rotation wobble.";

        public float positionStrength = 0.04f;
        public float rotationDegrees = 3f;
        public float speed = 1.2f;
        public bool progressIndexWithTime = true;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;
        internal bool SupportsInfinitePersistentFastPath => ReferenceEquals(playback, "infinite");

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = ctx.phase != AnimationPhase.Persistent || ReferenceEquals(playback, "infinite")
                ? 1f
                : BuiltInEffectTiming.EvaluateActivePlayback(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            ApplyWiggle(ref state, ctx.time, ctx.unitIndex, envelope);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = ReferenceEquals(playback, "infinite")
                ? 1f
                : BuiltInEffectTiming.EvaluateActivePlayback(
                    playback,
                    time,
                    playbackTime,
                    phaseProgress,
                    duration);
            if (envelope <= 0f)
                return;

            ApplyWiggle(ref state, time, unitIndex, envelope);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex)
        {
            ApplyWiggle(ref state, time, unitIndex, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ApplyWiggle(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex,
            float envelope)
        {
            float phase = (progressIndexWithTime ? time * speed : 0f) + unitIndex * 0.7f;
            BuiltInEffectMath.FastSinCosSample(phase, out float sine, out float cosine);
            float harmonic = 2f * sine * cosine;
            state.positionOffset.x += sine * positionStrength * envelope;
            state.positionOffset.y += (cosine + harmonic * 0.18f) * positionStrength * envelope;
            float degrees = (sine - harmonic * 0.12f) * rotationDegrees * envelope;
            BuiltInEffectMath.ApplySmallZRotation(ref state.rotation, degrees);
        }
    }

    [TextStudioEffectAlias("hop")]
    [TextStudioEffectAlias("jump")]
    internal sealed class BounceEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Bounce;
        public override string DisplayName => "Bounce";
        public override string Description => "Bounces the glyph upward with a stagger across indices.";

        public float height = 0.14f;
        public Vector2 direction = Vector2.up;
        public float speed = 1.05f;
        public float stagger = 0.045f;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;
        internal bool SupportsInfinitePersistentFastPath =>
            ReferenceEquals(playback, "infinite") ||
            string.IsNullOrWhiteSpace(playback) ||
            string.Equals(playback, "infinite", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(playback, "forever", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(playback, "persistent", StringComparison.OrdinalIgnoreCase);
        internal Vector2 NormalizedDirection => BuiltInEffectMath.NormalizeDirection(direction);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float phase = ctx.time * speed - ctx.unitIndex * stagger;
            float u = Mathf.Repeat(phase, 1f);
            Vector2 axis = BuiltInEffectMath.NormalizeDirection(direction);
            float offset = Mathf.Abs(BuiltInEffectMath.FastSin(u * Mathf.PI)) * height * envelope;
            state.positionOffset += new Vector3(axis.x * offset, axis.y * offset, 0f);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            if (envelope <= 0f)
                return;

            float phase = time * speed - unitIndex * stagger;
            float progress = Mathf.Repeat(phase, 1f);
            Vector2 axis = BuiltInEffectMath.NormalizeDirection(direction);
            float offset = Mathf.Abs(BuiltInEffectMath.FastSin(progress * Mathf.PI)) * height * envelope;
            state.positionOffset += new Vector3(axis.x * offset, axis.y * offset, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex,
            in Vector2 normalizedDirection)
        {
            float phase = time * speed - unitIndex * stagger;
            float progress = Mathf.Repeat(phase, 1f);
            float offset = Mathf.Abs(BuiltInEffectMath.FastSin(progress * Mathf.PI)) * height;
            state.positionOffset.x += normalizedDirection.x * offset;
            state.positionOffset.y += normalizedDirection.y * offset;
        }
    }

    [TextStudioEffectAlias("sway")]
    internal sealed class SwingEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Swing;
        public override string DisplayName => "Swing";
        public override string Description => "Rotational swing around the glyph origin.";

        public float maxDegrees = 12f;
        public float speed = 1f;
        public float duration = 1f;
        public string playback = "infinite";
        public Vector2 pivot = new Vector2(0.5f, 1f);

        internal override bool SupportsPersistentFastPath => true;
        internal bool SupportsInfinitePersistentFastPath => BuiltInEffectTiming.IsInfinitePlayback(playback);
        internal Vector2 PivotOffset => pivot - new Vector2(0.5f, 0.5f);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float deg = BuiltInEffectMath.FastSin(
                ctx.time * speed * Mathf.PI * 2f + ctx.unitIndex * 0.4f) * maxDegrees * envelope;
            state.Pivot = pivot;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, deg);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            if (envelope <= 0f)
                return;

            float degrees = BuiltInEffectMath.FastSin(
                time * speed * Mathf.PI * 2f + unitIndex * 0.4f) * maxDegrees * envelope;
            state.Pivot = pivot;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, degrees);
        }

        // Matches EvaluatePersistentFast for infinite playback, whose envelope is exactly 1, with the pivot offset resolved once by the caller.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex,
            in Vector2 pivotOffset)
        {
            float degrees = BuiltInEffectMath.FastSin(
                time * speed * Mathf.PI * 2f + unitIndex * 0.4f) * maxDegrees;
            state.pivotOffset = pivotOffset;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, degrees);
        }
    }

    internal sealed class PendulumEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Pendulum;
        public override string DisplayName => "Pendulum";
        public override string Description => "Slow uniform swing with damped amplitude.";

        public float maxDegrees = 16f;
        public float speed = 0.34f;
        public float duration = 1f;
        public string playback = "infinite";
        public Vector2 pivot = new Vector2(0.5f, 1f);

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;
        internal bool SupportsInfinitePersistentFastPath => BuiltInEffectTiming.IsInfinitePlayback(playback);
        internal Vector2 PivotOffset => pivot - new Vector2(0.5f, 0.5f);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float deg = BuiltInEffectMath.FastSin(ctx.time * speed * Mathf.PI * 2f) * maxDegrees * envelope;
            state.Pivot = pivot;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, deg);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            if (envelope <= 0f)
                return;

            float degrees = BuiltInEffectMath.FastSin(time * speed * Mathf.PI * 2f) * maxDegrees * envelope;
            state.Pivot = pivot;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, degrees);
        }

        // Matches EvaluatePersistentFast for infinite playback, whose envelope is exactly 1, with the pivot offset resolved once by the caller.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            in Vector2 pivotOffset)
        {
            float degrees = BuiltInEffectMath.FastSin(time * speed * Mathf.PI * 2f) * maxDegrees;
            state.pivotOffset = pivotOffset;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, degrees);
        }
    }

    [TextStudioEffectAlias("hanging")]
    internal sealed class DangleEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Dangle;
        public override string DisplayName => "Dangle";
        public override string Description => "Each glyph dangles with position-dependent phase.";

        public float maxDegrees = 8f;
        public float speed = 1f;
        public float duration = 1f;
        public string playback = "infinite";
        public Vector2 pivot = new Vector2(0.5f, 1f);
        public string horizontal = string.Empty;
        public string vertical = string.Empty;

        internal override bool SupportsPersistentFastPath =>
            !TextAnimatorStateCompatibility.HasShearAxisParameter(horizontal) &&
            !TextAnimatorStateCompatibility.HasShearAxisParameter(vertical);
        internal bool SupportsInfinitePersistentFastPath =>
            SupportsPersistentFastPath &&
            (ReferenceEquals(playback, "infinite") ||
             string.IsNullOrWhiteSpace(playback) ||
             string.Equals(playback, "infinite", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(playback, "forever", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(playback, "persistent", StringComparison.OrdinalIgnoreCase));
        internal Vector2 PivotOffset => pivot - new Vector2(0.5f, 0.5f);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = ctx.phase != AnimationPhase.Persistent || ReferenceEquals(playback, "infinite")
                ? 1f
                : BuiltInEffectTiming.EvaluateActivePlayback(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float phase = ctx.time * speed * Mathf.PI * 2f + ctx.normalizedIndex * 4f;
            float sine = BuiltInEffectMath.FastSinSample(phase);
            float deg = sine * maxDegrees * envelope;
            state.pivotOffset.x = pivot.x - 0.5f;
            state.pivotOffset.y = pivot.y - 0.5f;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, deg);

            if (TextAnimatorStateCompatibility.HasShearAxisParameter(horizontal) ||
                TextAnimatorStateCompatibility.HasShearAxisParameter(vertical))
            {
                TextAnimatorStateCompatibility.ApplyShear(
                    ref state,
                    in ctx,
                    maxDegrees,
                    horizontal,
                    vertical,
                    speed,
                    playback,
                    duration);
            }
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = ReferenceEquals(playback, "infinite")
                ? 1f
                : BuiltInEffectTiming.EvaluateActivePlayback(
                    playback,
                    time,
                    playbackTime,
                    phaseProgress,
                    duration);
            if (envelope <= 0f)
                return;

            float phase = time * speed * Mathf.PI * 2f + normalizedIndex * 4f;
            float sine = BuiltInEffectMath.FastSinSample(phase);
            float degrees = sine * maxDegrees * envelope;
            state.pivotOffset.x = pivot.x - 0.5f;
            state.pivotOffset.y = pivot.y - 0.5f;
            BuiltInEffectMath.ApplyZRotation(ref state.rotation, degrees);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float normalizedIndex,
            in Vector2 pivotOffset)
        {
            float phase = time * speed * Mathf.PI * 2f + normalizedIndex * 4f;
            float degrees = BuiltInEffectMath.FastSinSample(phase) * maxDegrees;
            state.pivotOffset = pivotOffset;
            BuiltInEffectMath.SetZRotation(ref state.rotation, degrees);
        }
    }

    [TextStudioEffectAlias("breathe", "Search alias only; existing breathe markup resolves to pulse.")]
    [TextStudioEffectAlias("breathing")]
    [TextStudioEffectAlias("beat")]
    internal sealed class PulseEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Pulse;
        public override string DisplayName => "Pulse";
        public override string Description => "Gentle scale and alpha pulse for prompts and interactive text.";

        public float scaleAmplitude = 0.08f;
        public float alphaAmplitude = 0.08f;
        public float speed = 1.35f;
        public float glyphPhaseOffset = 0.18f;
        public float duration = 1f;
        public string playback = "infinite";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float phase = (ctx.time * speed + ctx.unitIndex * glyphPhaseOffset) * Mathf.PI * 2f;
            float pulse = (BuiltInEffectMath.FastSin(phase) + 1f) * 0.5f;
            state.scale *= 1f + pulse * scaleAmplitude * envelope;

            if (alphaAmplitude > 0f)
                state.alpha *= Mathf.Clamp01(1f - alphaAmplitude * envelope + pulse * alphaAmplitude * envelope);
        }
    }

    [TextStudioEffectAlias("flash", "Search alias only; existing flash markup resolves to blink.")]
    [TextStudioEffectAlias("flicker")]
    internal sealed class BlinkEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Blink;
        public override string DisplayName => "Blink";
        public override string Description => "On/off alpha blink for warnings, cursors, and alert text.";

        public float frequency = 1.75f;
        public float dutyCycle = 0.55f;
        public float minAlpha;
        public float duration = 1f;
        public string playback = "infinite";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (frequency <= 0f) return;

            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            float phase = Mathf.Repeat(ctx.time * frequency / Mathf.Max(0.0001f, duration), 1f);
            float visibleFraction = Mathf.Clamp01(dutyCycle);
            float blinkAlpha = phase <= visibleFraction ? 1f : Mathf.Clamp01(minAlpha);
            state.alpha *= Mathf.Lerp(1f, blinkAlpha, envelope);
        }
    }

    [TextStudioEffectAlias("shine", "Search alias only; existing shine markup resolves to shimmer.")]
    [TextStudioEffectAlias("sparkle", "Search alias only; sparkle markup is intentionally not parsed as a built-in effect.")]
    [TextStudioEffectAlias("highlight")]
    internal sealed class ShimmerEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Shimmer;
        public override string DisplayName => "Vertex Shimmer";
        public override string Description => "A lightweight vertex-color highlight band; use Surface Sweep for material shine.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Active;
        public override TextStudioEffectAreaSettings DefaultArea => TextStudioEffectAreaSettings.Range;
        public override TextStudioEffectPlaybackRole PlaybackRole => TextStudioEffectPlaybackRole.Progress;
        public override TextStudioEffectPlaybackPreset DefaultPlayback => TextStudioEffectPlaybackPreset.Loop;
        public override TextStudioEffectCurvePreset DefaultCurve => TextStudioEffectCurvePreset.Linear;
        public override float DefaultPlaybackDuration => 1f / Mathf.Max(0.01f, speed);
        public override float DefaultDelayBetweenCycles => BuiltInProgressTiming.DefaultDelayBetweenCycles;

        public Color32 highlightColor = new Color32(255, 246, 190, 255);
        public float speed = 0.65f;
        public float width = 0.16f;
        public float scaleBoost = 0.035f;
        public string playback = "loop";
        public string curve = "linear";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (width <= 0f) return;

            float progress = BuiltInProgressTiming.Evaluate(
                in ctx,
                speed,
                playback,
                curve);
            float center = Mathf.LerpUnclamped(-width, 1f + width, progress);
            float distance = Mathf.Abs(ctx.normalizedIndexInUnit - center);
            if (distance > width) return;

            float strength = 1f - distance / Mathf.Max(width, 0.0001f);
            state.BlendColor(highlightColor, TextStudioColorBlendMode.Set, strength);
            state.scale *= 1f + scaleBoost * strength;
        }
    }

    [TextStudioEffectAlias("gradient flow")]
    [TextStudioEffectAlias("gradient sweep")]
    internal sealed class GradientFlowEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.GradientFlow;
        public override string DisplayName => "Gradient Flow";
        public override string Description =>
            "Animates two colors across the glyph range. Use gradient=Preset for a static TMP vertex gradient.";

        public Color32 colorA = new Color32(70, 214, 255, 255);
        public Color32 colorB = new Color32(255, 92, 184, 255);
        public float speed = 0.35f;
        public float spread = 1f;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float phase = (ctx.normalizedIndex * spread + ctx.time * speed) * Mathf.PI * 2f;
            float t = (BuiltInEffectMath.FastSin(phase) + 1f) * 0.5f;
            state.BlendColor(Color32.Lerp(colorA, colorB, t), TextStudioColorBlendMode.Set, 1f);
        }
    }

    [TextStudioEffectAlias("progressive highlight")]
    [TextStudioEffectAlias("paint on")]
    [TextStudioEffectAlias("karaoke sweep")]
    internal sealed class ColorSweepEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.ColorSweep;
        public override string DisplayName => "Color Sweep";
        public override string Description => "Progressively blends a target color across the current effect range.";

        public Color32 color = new Color32(88, 255, 218, 255);
        public float softness = 0.14f;
        public bool reverse;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float progress = Mathf.Clamp01(ctx.phaseProgress);
            if (progress <= 0f)
                return;

            if (progress >= 1f)
            {
                state.BlendColor(color, TextStudioColorBlendMode.Set, 1f);
                return;
            }

            int unitCount = Mathf.Max(1, ctx.unitCount);
            float position = (Mathf.Clamp(ctx.unitIndex, 0, unitCount - 1) + 0.5f) / unitCount;
            if (reverse)
                position = 1f - position;

            float width = Mathf.Max(0f, softness);
            float weight = width <= 0.0001f
                ? (position <= progress ? 1f : 0f)
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - position + width * 0.5f) / width));
            if (weight <= 0f)
                return;

            state.BlendColor(color, TextStudioColorBlendMode.Set, weight);
        }
    }

    [TextStudioEffectAlias("color cycle")]
    [TextStudioEffectAlias("palette pulse")]
    [TextStudioEffectAlias("tint pulse")]
    internal sealed class ColorPulseEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.ColorPulse;
        public override string DisplayName => "Color Pulse";
        public override string Description => "Pulses between two colors for active, rare, critical, or highlighted words.";

        public Color32 colorA = new Color32(255, 255, 255, 255);
        public Color32 colorB = new Color32(255, 211, 72, 255);
        public float speed = 1.2f;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            EvaluateColorPulse(ref state, in ctx, colorA, colorB, speed);
        }

        public override ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            ColorPulseEffect effect = (ColorPulseEffect)base.CreateEffect(in parameters);
            Color32 resolvedColorA = effect.colorA;
            Color32 resolvedColorB = effect.colorB;
            float resolvedSpeed = effect.speed;

            if (TextStudioEffectParameterUtility.TryGetColor(in parameters, 0, "from", out Color32 from))
            {
                resolvedColorA = from;
            }

            if (TextStudioEffectParameterUtility.TryGetColor(in parameters, 1, "to", out Color32 to))
            {
                resolvedColorB = to;
            }

            if (TextStudioEffectParameterUtility.TryGetFloat(in parameters, 2, "speed", out float parsedSpeed))
            {
                resolvedSpeed = parsedSpeed;
            }

            effect.colorA = resolvedColorA;
            effect.colorB = resolvedColorB;
            effect.speed = resolvedSpeed;
            return effect;
        }

        static void EvaluateColorPulse(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx, Color32 from, Color32 to, float pulseSpeed)
        {
            float t = (BuiltInEffectMath.FastSin(ctx.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            state.BlendColor(Color32.Lerp(from, to, t), TextStudioColorBlendMode.Set, 1f);
        }
    }

    [TextStudioEffectAlias("popIn", "Search alias only; existing popIn markup resolves to pop.")]
    [TextStudioEffectAlias("punch")]
    [TextStudioEffectAlias("overshoot")]
    internal sealed class PopEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Pop;
        public override string DisplayName => "Pop";
        public override string Description => "Punchy overshoot scale-in for rewards, pickups, and dialogue beats.";

        public float startScale = 0.15f;
        public float overshoot = 0.24f;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (ctx.phase == AnimationPhase.Appearance)
            {
                float t = Mathf.Clamp01(ctx.phaseProgress);
                float scale = Mathf.Lerp(startScale, 1f, t) +
                              BuiltInEffectMath.FastSin(t * Mathf.PI) * overshoot;
                state.scale *= Mathf.Max(0f, scale);
                state.alpha *= t;
            }
            else if (ctx.phase == AnimationPhase.Disappearance)
            {
                float t = 1f - Mathf.Clamp01(ctx.phaseProgress);
                state.scale *= Mathf.Lerp(startScale, 1f, t);
                state.alpha *= t;
            }
        }
    }

    [TextStudioEffectAlias("drift", "Search alias only; existing drift markup resolves to float.")]
    [TextStudioEffectAlias("hover")]
    [TextStudioEffectAlias("levitate")]
    internal sealed class FloatEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Float;
        public override string DisplayName => "Float";
        public override string Description => "Slow buoyant drift for magical, ambient, or holographic text.";

        public float verticalAmplitude = 0.16f;
        public float horizontalAmplitude = 0.04f;
        public float speed = 0.75f;
        public float spread = 1.25f;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            if (envelope <= 0f)
                return;

            ApplyFloat(ref state, ctx.time, ctx.normalizedIndex, envelope);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            if (envelope <= 0f)
                return;

            ApplyFloat(ref state, time, normalizedIndex, envelope);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ApplyFloat(
            ref EvaluatedGlyphState state,
            float time,
            float normalizedIndex,
            float envelope)
        {
            float phase = (time * speed + normalizedIndex * spread) * Mathf.PI * 2f;
            state.positionOffset.y += BuiltInEffectMath.FastSin(phase) * verticalAmplitude * envelope;
            state.positionOffset.x += BuiltInEffectMath.FastCos(phase * 0.73f) * horizontalAmplitude * envelope;
        }
    }

    // ────────────────────────── Color ──────────────────────────

    internal sealed class RainbowEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Rainbow;
        public override string DisplayName => "Rainbow";
        public override string Description => "Animated rainbow hue cycling across glyphs.";

        public float saturation = 0.85f;
        public float value = 1f;
        public float speed = 0.25f;
        public float spread = 0.8f;

        internal override bool SupportsPersistentFastPath => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            ApplyColor(ref state, ctx.time, ctx.normalizedIndex);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            ApplyColor(ref state, time, normalizedIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluatePersistentColorFast(
            ref EvaluatedGlyphState state,
            float time,
            float normalizedIndex)
        {
            ApplyColor(ref state, time, normalizedIndex);
        }

        void ApplyColor(ref EvaluatedGlyphState state, float time, float normalizedIndex)
        {
            float hue = Mathf.Repeat(normalizedIndex * spread + time * speed, 1f);
            Color32 color;
            if (saturation >= 0f && saturation <= 1f && value >= 0f && value <= 1f)
            {
                color = BuiltInEffectMath.HsvToRgb32(hue, saturation, value);
            }
            else
            {
                Color converted = Color.HSVToRGB(hue, saturation, value);
                color = new Color32(
                    (byte)(converted.r * 255f),
                    (byte)(converted.g * 255f),
                    (byte)(converted.b * 255f),
                    255);
            }
            state.SetColor(color);
        }
    }

    // ────────────────────────── Appearance ──────────────────────────

    [TextStudioEffectAlias("alpha")]
    [TextStudioEffectAlias("fade in")]
    [TextStudioEffectAlias("fade out")]
    internal sealed class FadeEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Fade;
        public override string DisplayName => "Fade";
        public override string Description => "Fades glyphs in on reveal, pulses while active, and fades out on disappearance.";

        public float minAlpha;
        public float maxAlpha = 1f;
        public float duration = 1f;
        public string playback = "pingpong";
        public Color32 color = new Color32(255, 255, 255, 255);
        public string mode = "full";

        bool _hasColorOverride;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;
        internal bool SupportsPersistentAlphaFastPath => !_hasColorOverride;

        public override ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            FadeEffect effect = (FadeEffect)base.CreateEffect(in parameters);
            if ((parameters.TryGetColor("color", out Color32 parsedColor) ||
                 parameters.TryGetColor("c", out parsedColor)))
            {
                effect.color = parsedColor;
                effect._hasColorOverride = true;
            }

            if (parameters.TryGetString("mode", out string parsedMode))
            {
                effect.mode = parsedMode;
            }

            return effect;
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (_hasColorOverride)
            {
                float weight;
                switch (ctx.phase)
                {
                    case AnimationPhase.Appearance:
                        weight = 1f - Mathf.Clamp01(ctx.phaseProgress);
                        break;
                    case AnimationPhase.Disappearance:
                        weight = Mathf.Clamp01(ctx.phaseProgress);
                        break;
                    case AnimationPhase.Persistent:
                        weight = BuiltInEffectTiming.EvaluateActivePlayback(
                            playback,
                            in ctx,
                            duration);
                        break;
                    default:
                        weight = 0f;
                        break;
                }

                state.BlendColor(
                    color,
                    TextAnimatorStateCompatibility.ParseColorMode(mode),
                    Mathf.Clamp01(weight));
                return;
            }

            switch (ctx.phase)
            {
                case AnimationPhase.Appearance:
                    state.alpha *= Mathf.Lerp(minAlpha, maxAlpha, Mathf.Clamp01(ctx.phaseProgress));
                    break;
                case AnimationPhase.Disappearance:
                    state.alpha *= Mathf.Lerp(maxAlpha, minAlpha, Mathf.Clamp01(ctx.phaseProgress));
                    break;
                case AnimationPhase.Persistent:
                    float playbackProgress = BuiltInEffectTiming.EvaluateActivePlayback(
                        playback,
                        in ctx,
                        duration);
                    state.alpha *= Mathf.Lerp(minAlpha, maxAlpha, playbackProgress);
                    break;
            }
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float playbackProgress = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            if (_hasColorOverride)
            {
                state.BlendColor(
                    color,
                    TextAnimatorStateCompatibility.ParseColorMode(mode),
                    Mathf.Clamp01(playbackProgress));
                return;
            }

            state.alpha *= minAlpha + (maxAlpha - minAlpha) * playbackProgress;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float EvaluatePersistentAlphaFast(
            float time,
            float playbackTime,
            float phaseProgress)
        {
            float playbackProgress = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            return minAlpha + (maxAlpha - minAlpha) * playbackProgress;
        }
    }

    [TextStudioEffectAlias("big", "Search alias only; existing big markup resolves to size.")]
    [TextStudioEffectAlias("small", "Search alias only; existing small markup resolves to size.")]
    [TextStudioEffectAlias("grow")]
    [TextStudioEffectAlias("shrink")]
    internal sealed class SizeEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Size;
        public override string DisplayName => "Size";
        public override string Description => "Subtly scales glyphs during reveal and gently pulses while active.";

        public float startScale = 0.85f;
        public float endScale = 1f;
        public float pulseAmplitude = 0.08f;
        public float pulseSpeed = 1f;
        public float duration = 1f;
        public string playback = "infinite";
        public string scale = string.Empty;

        bool _hasScaleOverride;
        Vector3 _scaleOverride = Vector3.one;

        internal override bool SupportsPersistentFastPath => true;
        internal bool SupportsInfinitePersistentFastPath =>
            !_hasScaleOverride && BuiltInEffectTiming.IsInfinitePlayback(playback);

        public override ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            SizeEffect effect = (SizeEffect)base.CreateEffect(in parameters);
            if (parameters.TryGetString("scale", out string rawScale) &&
                TryParseScale(rawScale, out Vector3 parsedScale))
            {
                effect._hasScaleOverride = true;
                effect._scaleOverride = parsedScale;
            }

            return effect;
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (_hasScaleOverride)
            {
                ApplyScaleOverride(ref state, in ctx);
                return;
            }

            float s;
            if (ctx.phase == AnimationPhase.Appearance)
            {
                s = Mathf.Lerp(startScale, endScale, ctx.phaseProgress);
            }
            else if (ctx.phase == AnimationPhase.Disappearance)
            {
                s = Mathf.Lerp(endScale, startScale, ctx.phaseProgress);
            }
            else
            {
                float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
                float phase = (ctx.time * pulseSpeed + ctx.unitIndex * 0.039f) * Mathf.PI * 2f;
                s = endScale * Mathf.Max(
                    0f,
                    1f + BuiltInEffectMath.FastSin(phase) * pulseAmplitude * envelope);
            }

            state.scale *= s;
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            if (_hasScaleOverride)
            {
                state.scale = Vector3.Scale(
                    state.scale,
                    Vector3.Lerp(Vector3.one, _scaleOverride, envelope));
                return;
            }

            float phase = (time * pulseSpeed + unitIndex * 0.039f) * Mathf.PI * 2f;
            float scaleValue = endScale * Mathf.Max(
                0f,
                1f + BuiltInEffectMath.FastSin(phase) * pulseAmplitude * envelope);
            state.scale *= scaleValue;
        }

        // Matches EvaluatePersistentFast for infinite playback without a scale override, whose envelope is exactly 1.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateInfinitePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex)
        {
            float phase = (time * pulseSpeed + unitIndex * 0.039f) * Mathf.PI * 2f;
            float scaleValue = endScale * Mathf.Max(
                0f,
                1f + BuiltInEffectMath.FastSin(phase) * pulseAmplitude);
            state.scale *= scaleValue;
        }

        void ApplyScaleOverride(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            Vector3 scaleValue;
            if (ctx.phase == AnimationPhase.Appearance)
            {
                scaleValue = Vector3.Lerp(_scaleOverride, Vector3.one, ctx.phaseProgress);
            }
            else if (ctx.phase == AnimationPhase.Disappearance)
            {
                scaleValue = Vector3.Lerp(Vector3.one, _scaleOverride, ctx.phaseProgress);
            }
            else if (ctx.phase == AnimationPhase.Persistent)
            {
                float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
                scaleValue = Vector3.Lerp(Vector3.one, _scaleOverride, envelope);
            }
            else
            {
                return;
            }

            state.scale = Vector3.Scale(state.scale, scaleValue);
        }

        static bool TryParseScale(string raw, out Vector3 value)
        {
            value = Vector3.one;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string[] parts = raw.Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scalar))
            {
                value = new Vector3(scalar, scalar, 1f);
                return true;
            }

            if (parts.Length < 2 || parts.Length > 3)
                return false;

            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                return false;
            }

            float z = 1f;
            if (parts.Length == 3 &&
                !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }
    }

    [TextStudioEffectAlias("spin")]
    [TextStudioEffectAlias("rotate in")]
    internal sealed class RotateEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Rotate;
        public override string DisplayName => "Rotate In";
        public override string Description =>
            "Rotates glyphs around the selected x, y, or z axis on reveal and while active.";

        public float startDegrees = 45f;
        public float speed = 0.34f;
        public float loopDegrees = 360f;
        public string axis = "z";
        public float duration = 1f;
        public string playback = "infinite";
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float deg;
            if (ctx.phase == AnimationPhase.Appearance)
                deg = Mathf.Lerp(startDegrees, EvaluatePersistentDegrees(in ctx), ctx.phaseProgress);
            else if (ctx.phase == AnimationPhase.Disappearance)
                deg = Mathf.Lerp(0f, startDegrees, ctx.phaseProgress);
            else if (ctx.phase == AnimationPhase.Persistent)
                deg = EvaluatePersistentDegrees(in ctx);
            else
                return;

            state.Pivot = pivot;
            BuiltInEffectMath.ApplyAxisRotation(ref state.rotation, deg, axis);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float motionTime = BuiltInEffectTiming.ResolvePersistentMotionTime(
                playback,
                duration,
                time,
                playbackTime);
            float degrees = motionTime * speed * loopDegrees *
                            BuiltInEffectTiming.EvaluateActivePlayback(
                                playback,
                                time,
                                playbackTime,
                                phaseProgress,
                                duration);
            state.Pivot = pivot;
            BuiltInEffectMath.ApplyAxisRotation(ref state.rotation, degrees, axis);
        }

        float EvaluatePersistentDegrees(in EffectEvaluationContext context)
        {
            float motionTime = BuiltInEffectTiming.ResolvePersistentMotionTime(
                playback,
                duration,
                context.time,
                context.playbackTime);
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                context.time,
                context.playbackTime,
                context.phaseProgress,
                duration);
            return motionTime * speed * loopDegrees * envelope;
        }
    }

    [TextStudioEffectAlias("skew", "Search alias only; skew markup resolves to shear.")]
    internal sealed class ShearEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Shear;
        public override string DisplayName => "Shear";
        public override string Description => "Applies animated glyph-space shear on horizontal and vertical axes.";

        public float amplitude = 1f;
        public string horizontal = "0";
        public string vertical = "allSides";
        public float speed = 1f;
        public float duration = 1f;
        public string playback = "infinite";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            TextAnimatorStateCompatibility.ApplyShear(
                ref state,
                in ctx,
                amplitude,
                horizontal,
                vertical,
                speed,
                playback,
                duration);
        }
    }

    [TextStudioEffectAlias("slide", "Search alias only; existing slide markup resolves to slideHorizontal.")]
    [TextStudioEffectAlias("slideH", "Search alias only; existing slideH markup resolves to slideHorizontal.")]
    [TextStudioEffectAlias("slideX", "Search alias only; existing slideX markup resolves to slideHorizontal.")]
    internal sealed class SlideHorizontalEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.SlideHorizontal;
        public override string DisplayName => "Slide Horizontal";
        public override string Description => "Glyphs slide in from the left on appearance and drift gently while active.";

        public float distance = 0.22f;
        public float speed = 1f;
        public float stagger = 0.03f;
        public float duration = 1f;
        public string playback = "infinite";
        public string horizontal = string.Empty;
        public string vertical = string.Empty;

        internal override bool SupportsPersistentFastPath =>
            !TextAnimatorStateCompatibility.HasShearAxisParameter(horizontal) &&
            !TextAnimatorStateCompatibility.HasShearAxisParameter(vertical);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (ctx.phase == AnimationPhase.Appearance)
                state.positionOffset.x -= distance * (1f - ctx.phaseProgress);
            else if (ctx.phase == AnimationPhase.Disappearance)
                state.positionOffset.x += distance * ctx.phaseProgress;
            else if (ctx.phase == AnimationPhase.Persistent)
                state.positionOffset.x += BuiltInEffectMath.FastSin(
                                              (ctx.time * speed + ctx.unitIndex * stagger) * Mathf.PI * 2f) *
                                          distance *
                                          BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);

            if (TextAnimatorStateCompatibility.HasShearAxisParameter(horizontal) ||
                TextAnimatorStateCompatibility.HasShearAxisParameter(vertical))
            {
                TextAnimatorStateCompatibility.ApplyShear(
                    ref state,
                    in ctx,
                    distance,
                    horizontal,
                    vertical,
                    speed,
                    playback,
                    duration);
            }
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            state.positionOffset.x += BuiltInEffectMath.FastSin(
                                          (time * speed + unitIndex * stagger) * Mathf.PI * 2f) *
                                      distance *
                                      envelope;
        }
    }

    [TextStudioEffectAlias("slideV", "Search alias only; existing slideV markup resolves to slideVertical.")]
    [TextStudioEffectAlias("slideY", "Search alias only; existing slideY markup resolves to slideVertical.")]
    [TextStudioEffectAlias("rise")]
    [TextStudioEffectAlias("drop")]
    internal sealed class SlideVerticalEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.SlideVertical;
        public override string DisplayName => "Slide Vertical";
        public override string Description => "Glyphs slide up into place on appearance and drift gently while active.";

        public float distance = 0.16f;
        public float speed = 1f;
        public float stagger = 0.03f;
        public float duration = 1f;
        public string playback = "infinite";
        public string horizontal = string.Empty;
        public string vertical = string.Empty;

        internal override bool SupportsPersistentFastPath =>
            !TextAnimatorStateCompatibility.HasShearAxisParameter(horizontal) &&
            !TextAnimatorStateCompatibility.HasShearAxisParameter(vertical);

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (ctx.phase == AnimationPhase.Appearance)
                state.positionOffset.y -= distance * (1f - ctx.phaseProgress);
            else if (ctx.phase == AnimationPhase.Disappearance)
                state.positionOffset.y -= distance * ctx.phaseProgress;
            else if (ctx.phase == AnimationPhase.Persistent)
                state.positionOffset.y += BuiltInEffectMath.FastSin(
                                              (ctx.time * speed + ctx.unitIndex * stagger) * Mathf.PI * 2f) *
                                          distance *
                                          BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);

            if (TextAnimatorStateCompatibility.HasShearAxisParameter(horizontal) ||
                TextAnimatorStateCompatibility.HasShearAxisParameter(vertical))
            {
                TextAnimatorStateCompatibility.ApplyShear(
                    ref state,
                    in ctx,
                    distance,
                    horizontal,
                    vertical,
                    speed,
                    playback,
                    duration);
            }
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            state.positionOffset.y += BuiltInEffectMath.FastSin(
                                          (time * speed + unitIndex * stagger) * Mathf.PI * 2f) *
                                      distance *
                                      envelope;
        }
    }

    [TextStudioEffectAlias("scale fade")]
    [TextStudioEffectAlias("soft pop")]
    internal sealed class ExpandEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Expand;
        public override string DisplayName => "Expand";
        public override string Description => "Combines fade and size for a soft pop-in.";

        public float amplitude = 1f;
        public string mode = "both";
        public string origin = "center";
        public float pulseSpeed = 1f;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            BuiltInEffects.Fade.Evaluate(ref state, in ctx);
            ApplyOrigin(ref state, origin);

            float strength = Mathf.Max(0f, amplitude);
            float scale = 1f;
            if (ctx.phase == AnimationPhase.Appearance)
            {
                scale = Mathf.Lerp(GetStartScale(strength), 1f, ctx.phaseProgress);
            }
            else if (ctx.phase == AnimationPhase.Disappearance)
            {
                scale = Mathf.Lerp(1f, GetStartScale(strength), ctx.phaseProgress);
            }
            else if (ctx.phase == AnimationPhase.Persistent)
            {
                float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
                float phase = (ctx.time * pulseSpeed + ctx.unitIndex * 0.039f) * Mathf.PI * 2f;
                scale = Mathf.Max(
                    0f,
                    1f + BuiltInEffectMath.FastSin(phase) * 0.08f * strength * envelope);
            }

            ApplyModeScale(ref state, scale, mode);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            BuiltInEffects.Fade.EvaluatePersistentFast(
                ref state,
                time,
                playbackTime,
                phaseProgress,
                unitIndex,
                normalizedIndex);

            float strength = Mathf.Max(0f, amplitude);
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            float phase = (time * pulseSpeed + unitIndex * 0.039f) * Mathf.PI * 2f;
            float scale = Mathf.Max(
                0f,
                1f + BuiltInEffectMath.FastSin(phase) * 0.08f * strength * envelope);
            ApplyModeScale(ref state, scale, mode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PrepareSharedPersistentFast(
            float time,
            float playbackTime,
            float phaseProgress,
            out float fadeAlpha,
            out float strength,
            out float envelope)
        {
            fadeAlpha = BuiltInEffects.Fade.EvaluatePersistentAlphaFast(
                time,
                playbackTime,
                phaseProgress);
            strength = Mathf.Max(0f, amplitude);
            envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EvaluateSharedPersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex,
            float fadeAlpha,
            float strength,
            float envelope)
        {
            state.alpha *= fadeAlpha;
            float phase = (time * pulseSpeed + unitIndex * 0.039f) * Mathf.PI * 2f;
            float scale = Mathf.Max(
                0f,
                1f + BuiltInEffectMath.FastSin(phase) * 0.08f * strength * envelope);
            ApplyModeScale(ref state, scale, mode);
        }

        static float GetStartScale(float strength)
        {
            return Mathf.Max(0f, 1f - 0.15f * strength);
        }

        static void ApplyModeScale(ref EvaluatedGlyphState state, float scale, string rawMode)
        {
            if (ReferenceEquals(rawMode, "both") || string.IsNullOrEmpty(rawMode))
            {
                state.scale *= scale;
                return;
            }

            string normalized = rawMode ?? string.Empty;
            if (normalized.IndexOf("horizontal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(normalized, "x", System.StringComparison.OrdinalIgnoreCase))
            {
                state.scale = Vector3.Scale(state.scale, new Vector3(scale, 1f, 1f));
                return;
            }

            if (normalized.IndexOf("vertical", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(normalized, "y", System.StringComparison.OrdinalIgnoreCase))
            {
                state.scale = Vector3.Scale(state.scale, new Vector3(1f, scale, 1f));
                return;
            }

            state.scale *= scale;
        }

        static void ApplyOrigin(ref EvaluatedGlyphState state, string rawOrigin)
        {
            if (string.IsNullOrEmpty(rawOrigin) ||
                string.Equals(rawOrigin, "center", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(rawOrigin, "left", System.StringComparison.OrdinalIgnoreCase))
                state.Pivot = new Vector2(0f, 0.5f);
            else if (string.Equals(rawOrigin, "right", System.StringComparison.OrdinalIgnoreCase))
                state.Pivot = new Vector2(1f, 0.5f);
            else if (string.Equals(rawOrigin, "top", System.StringComparison.OrdinalIgnoreCase))
                state.Pivot = new Vector2(0.5f, 1f);
            else if (string.Equals(rawOrigin, "bottom", System.StringComparison.OrdinalIgnoreCase))
                state.Pivot = new Vector2(0.5f, 0f);
        }
    }

    // ────────────────────────── Reveal-time styling ──────────────────────────

    [TextStudioEffectAlias("typewriter reveal")]
    [TextStudioEffectAlias("appear")]
    internal sealed class TypewriterEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Typewriter;
        public override string DisplayName => "Typewriter";
        public override string Description => "Hides glyphs until revealed (reveal controller already handles this).";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (ctx.phase == AnimationPhase.None)
                state.alpha = 0f;
        }
    }

    [TextStudioEffectAlias("scramble")]
    [TextStudioEffectAlias("decode")]
    internal sealed class DecryptEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Decrypt;
        public override string DisplayName => "Decrypt";
        public override string Description => "Glyphs shake until they 'lock in' at the end of appearance.";

        public float shakeStrength = 0.12f;
        public float shakeSpeed = 32f;
        public int seed = 1931;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            if (ctx.phase != AnimationPhase.Appearance) return;
            float remaining = 1f - ctx.phaseProgress;
            float samplePosition = ctx.time * shakeSpeed;
            float x = BuiltInEffectMath.SmoothRandomSample(samplePosition, ctx.unitIndex, seed);
            float y = BuiltInEffectMath.SmoothRandomSample(
                samplePosition,
                ctx.unitIndex,
                unchecked(seed + 7346));
            state.positionOffset.x += x * shakeStrength * remaining;
            state.positionOffset.y += y * shakeStrength * remaining;
        }
    }

    [TextStudioEffectAlias("corrupt")]
    [TextStudioEffectAlias("digital noise")]
    internal sealed class GlitchEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Glitch;
        public override string DisplayName => "Glitch";
        public override string Description => "Deterministic block-based horizontal slicing jitter with a restrained color shift.";

        public float strength = 0.1f;
        public float frequency = 8f;
        public int seed = 2719;
        [Range(0f, 1f)] public float activity = 0.18f;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            int timeBlock = Mathf.FloorToInt(ctx.time * Mathf.Max(0.01f, frequency));
            float noise = SpatialTransformEffectMath.Hash01(
                ctx.randomSeedIndex + timeBlock * 4099,
                seed);
            if (noise > 1f - Mathf.Clamp01(activity))
            {
                float signed = SpatialTransformEffectMath.Hash01(ctx.unitIndex + timeBlock, seed + 37) * 2f - 1f;
                state.positionOffset.x += signed * strength;
                state.BlendColor(new Color32(255, 96, 96, 255), TextStudioColorBlendMode.Set, 0.7f);
            }
        }
    }

    [TextStudioEffectAlias("em", "Search alias only; existing em markup resolves to emphasis.")]
    [TextStudioEffectAlias("important")]
    [TextStudioEffectAlias("bold")]
    internal sealed class EmphasisEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Emphasis;
        public override string DisplayName => "Emphasis";
        public override string Description => "Gentle size pulse used for <em>/bold-like emphasis.";

        public float amplitude = 0.08f;
        public float speed = 4f;
        public float duration = 1f;
        public string playback = "infinite";

        internal override bool SupportsPersistentFastPath => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext ctx)
        {
            float envelope = BuiltInEffectTiming.EvaluatePersistentEnvelope(playback, in ctx, duration);
            ApplyScale(ref state, ctx.time, ctx.unitIndex, envelope);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            float envelope = BuiltInEffectTiming.EvaluateActivePlayback(
                playback,
                time,
                playbackTime,
                phaseProgress,
                duration);
            ApplyScale(ref state, time, unitIndex, envelope);
        }

        void ApplyScale(
            ref EvaluatedGlyphState state,
            float time,
            int unitIndex,
            float envelope)
        {
            float pulse = 1f + BuiltInEffectMath.FastSin(time * speed + unitIndex * 0.3f) *
                          amplitude * envelope;
            state.scale *= pulse;
        }
    }
}
