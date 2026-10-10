using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Clock-driven title treatments routed to fused field or Hero rendering by capability.</summary>
    public enum TextStudio2DAnimatedEffectKind : byte
    {
        LightSweep = 0,
        Shimmer = 1,
        LiquidFill = 2,
        BurningEdge = 3,
        ElectricOutline = 4,
        GlitchDisplacement = 5,
        HeatHaze = 6,
        LiquidWarp = 7,
        PixelDissolve = 8,
        EchoTrails = 9,
        MotionSmear = 10,
        ProgressiveBlurFocus = 11
    }

    /// <summary>
    /// Compact deterministic parameters shared by the animated Appearance family. Playback uses the
    /// owning Text Studio presentation clock, so scrubbing, pause, reverse, and reduced motion remain coherent.
    /// </summary>
    [Serializable]
    public struct TextStudio2DAnimatedEffectSpec
    {
        [SerializeField] TextStudio2DAnimatedEffectKind _kind;
        [SerializeField] TextStudio2DMappingSpace _mapping;
        [SerializeField, Range(0f, 4f), Tooltip("Effect strength from 0 to 4. Values above 1 preserve intensified lighting and treatment responses.")] float _amount;
        [SerializeField, Range(-32f, 32f), Tooltip("Clock multiplier from -32 to 32. Zero holds the phase; negative values reverse playback.")] float _speed;
        [SerializeField, Tooltip("Offset the effect's cycle phase. Values may extend beyond one cycle.")] float _phase;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.PatternScale), Tooltip("Pattern frequency in mapping-space units, from 0.01 to 4096. The slider is logarithmic; the numeric input uses the exact frequency.")] float _scale;
        [SerializeField, Range(0f, 2f), Tooltip("Effect width in its normalized domain, from 0 to 2.")] float _width;
        [SerializeField, Range(0f, 2f), Tooltip("Effect edge softness in its normalized domain, from 0 to 2.")] float _softness;
        [SerializeField, Range(0f, 2f), Tooltip("Movement or distortion amplitude in the effect's normalized domain, from 0 to 2.")] float _amplitude;
        [SerializeField, Range(0.01f, 128f), Tooltip("Wave or modulation frequency from 0.01 to 128.")] float _frequency;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.AnimationDirection)] Vector2 _direction;
        [SerializeField] Color _primaryColor;
        [SerializeField] Color _secondaryColor;
        [SerializeField] int _seed;
        [SerializeField, Range(1, TextStudioVisualPrimitiveMath.MaximumHistoryFrames), Tooltip("Retained history frames from 1 to 4. Used by effects that sample earlier rendered frames.")] int _historyFrames;
        [SerializeField] TextStudioReducedMotionBehavior _reducedMotionBehavior;

        public TextStudio2DAnimatedEffectKind Kind => _kind;
        public TextStudio2DMappingSpace Mapping => _mapping;
        public float Amount => _amount;
        public float Speed => _speed;
        public float Phase => _phase;
        public float Scale => _scale;
        public float Width => _width;
        public float Softness => _softness;
        public float Amplitude => _amplitude;
        public float Frequency => _frequency;
        public Vector2 Direction => _direction;
        public Color PrimaryColor => _primaryColor;
        public Color SecondaryColor => _secondaryColor;
        public int Seed => _seed;
        public int HistoryFrames => _historyFrames;
        public TextStudioReducedMotionBehavior ReducedMotionBehavior =>
            _reducedMotionBehavior;

        /// <summary>Creates an animated-effect specification with the supplied mapping, timing, shape, colors, seed, history, and reduced-motion behavior.</summary>
        public TextStudio2DAnimatedEffectSpec(
            TextStudio2DAnimatedEffectKind kind,
            TextStudio2DMappingSpace mapping,
            float amount,
            float speed,
            float phase,
            float scale,
            float width,
            float softness,
            float amplitude,
            float frequency,
            Vector2 direction,
            Color primaryColor,
            Color secondaryColor,
            int seed,
            int historyFrames,
            TextStudioReducedMotionBehavior reducedMotionBehavior)
        {
            _kind = kind;
            _mapping = mapping;
            _amount = amount;
            _speed = speed;
            _phase = phase;
            _scale = scale;
            _width = width;
            _softness = softness;
            _amplitude = amplitude;
            _frequency = frequency;
            _direction = direction;
            _primaryColor = primaryColor;
            _secondaryColor = secondaryColor;
            _seed = seed;
            _historyFrames = historyFrames;
            _reducedMotionBehavior = reducedMotionBehavior;
        }

        /// <summary>Returns a polished deterministic starting point for an animated family.</summary>
        public static TextStudio2DAnimatedEffectSpec Preset(
            TextStudio2DAnimatedEffectKind kind)
        {
            switch (kind)
            {
                case TextStudio2DAnimatedEffectKind.LightSweep:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1.12f, 0.28f, 0.08f, 1f, 0.14f, 0.016f, 0.12f, 1f, new Vector2(1f, 0.06f),
                        Color.white, new Color(1f, 0.55f, 0.08f, 0.5f), 401, 1);
                case TextStudio2DAnimatedEffectKind.Shimmer:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1.08f, 0.42f, 0.17f, 38f, 0.075f, 0.022f, 0.76f, 2.8f, new Vector2(1f, 0.16f),
                        Color.white, new Color(1f, 0.58f, 0.1f, 0.52f), 419, 1);
                case TextStudio2DAnimatedEffectKind.LiquidFill:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1.08f, 0.16f, 0.32f, 4.2f, 0.065f, 0.018f, 0.052f, 2.1f, Vector2.up,
                        new Color(0.72f, 1f, 1f, 1f), new Color(0.015f, 0.3f, 0.86f, 0.98f), 431, 1);
                case TextStudio2DAnimatedEffectKind.BurningEdge:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 1.24f, 0.32f, 0.08f, 11f, 0.16f, 0.025f, 0.22f, 2.35f, Vector2.up,
                        new Color(1f, 0.95f, 0.42f, 1f), new Color(1f, 0.07f, 0.005f, 0.9f), 443, 1);
                case TextStudio2DAnimatedEffectKind.ElectricOutline:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 1.28f, 1.12f, 0.23f, 15f, 0.105f, 0.018f, 0.14f, 3.8f, new Vector2(0.86f, 0.5f),
                        Color.white, new Color(0.18f, 0.28f, 1f, 0.96f), 457, 1);
                case TextStudio2DAnimatedEffectKind.GlitchDisplacement:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.96f, 0.78f, 0.11f, 16f, 0.13f, 0.016f, 0.078f, 7f, Vector2.right,
                        new Color(1f, 0.03f, 0.28f, 0.96f), new Color(0.02f, 0.94f, 1f, 0.96f), 467, 1);
                case TextStudio2DAnimatedEffectKind.HeatHaze:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.86f, 0.36f, 0.27f, 2.8f, 0.09f, 0.03f, 0.04f, 2.4f, Vector2.up,
                        new Color(1f, 0.72f, 0.22f, 0.5f), new Color(0.42f, 0.05f, 0.01f, 0.28f), 479, 1);
                case TextStudio2DAnimatedEffectKind.LiquidWarp:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.88f, 0.25f, 0.41f, 1.9f, 0.1f, 0.026f, 0.052f, 1.7f, new Vector2(0.74f, 0.67f),
                        new Color(0.62f, 0.96f, 1f, 0.54f), new Color(0.18f, 0.08f, 0.68f, 0.34f), 487, 1);
                case TextStudio2DAnimatedEffectKind.PixelDissolve:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1.08f, 0.18f, 0f, 30f, 0.09f, 0.014f, 0.3f, 1f, new Vector2(1f, 0.28f),
                        new Color(0.88f, 0.98f, 1f, 1f), new Color(0.48f, 0.18f, 1f, 1f), 499, 1);
                case TextStudio2DAnimatedEffectKind.EchoTrails:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1.04f, 0.68f, 0.12f, 1f, 0.09f, 0.04f, 0.075f, 1f, new Vector2(-0.94f, 0.34f),
                        new Color(0.2f, 0.86f, 1f, 0.92f), new Color(0.78f, 0.12f, 1f, 0.72f), 509, 4);
                case TextStudio2DAnimatedEffectKind.MotionSmear:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1f, 0.52f, 0.35f, 1f, 0.075f, 0.032f, 0.18f, 1f, new Vector2(-1f, 0.04f),
                        Color.white, new Color(0.08f, 0.48f, 1f, 0.62f), 521, 1);
                case TextStudio2DAnimatedEffectKind.ProgressiveBlurFocus:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1f, 0.2f, 0.42f, 1f, 0.11f, 0.04f, 0.3f, 1f, Vector2.right,
                        Color.white, new Color(0.25f, 0.55f, 1f, 0.36f), 541, 1);
                default:
                    return Create(kind, TextStudio2DMappingSpace.Text, 1.08f, 0.34f, 0.09f, 1f, 0.13f, 0.035f, 0.16f, 1f, new Vector2(0.98f, 0.2f),
                        new Color(1f, 1f, 1f, 0.98f), new Color(1f, 0.68f, 0.22f, 0.42f), 401, 1);
            }
        }

        /// <summary>Returns an animated-effect copy with the supplied amount, speed, and phase.</summary>
        public TextStudio2DAnimatedEffectSpec WithTiming(
            float amount,
            float speed,
            float phase)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._amount = amount;
            value._speed = speed;
            value._phase = phase;
            return value;
        }

        /// <summary>Returns an animated-effect copy with the supplied scale, width, softness, amplitude, frequency, and direction.</summary>
        public TextStudio2DAnimatedEffectSpec WithShape(
            float scale,
            float width,
            float softness,
            float amplitude,
            float frequency,
            Vector2 direction)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._scale = scale;
            value._width = width;
            value._softness = softness;
            value._amplitude = amplitude;
            value._frequency = frequency;
            value._direction = direction;
            return value;
        }

        /// <summary>Returns an animated-effect copy with the supplied primary and secondary colors.</summary>
        public TextStudio2DAnimatedEffectSpec WithColors(Color primary, Color secondary)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._primaryColor = primary;
            value._secondaryColor = secondary;
            return value;
        }

        /// <summary>Returns an animated-effect copy using the supplied mapping space.</summary>
        public TextStudio2DAnimatedEffectSpec WithMapping(TextStudio2DMappingSpace mapping)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._mapping = mapping;
            return value;
        }

        /// <summary>Returns an animated-effect copy using the supplied deterministic seed.</summary>
        public TextStudio2DAnimatedEffectSpec WithSeed(int seed)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._seed = seed;
            return value;
        }

        /// <summary>Returns an animated-effect copy with the supplied temporal-history frame count.</summary>
        public TextStudio2DAnimatedEffectSpec WithHistoryFrames(int historyFrames)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._historyFrames = historyFrames;
            return value;
        }

        /// <summary>Returns an animated-effect copy with the supplied reduced-motion behavior.</summary>
        public TextStudio2DAnimatedEffectSpec WithReducedMotion(
            TextStudioReducedMotionBehavior behavior)
        {
            TextStudio2DAnimatedEffectSpec value = this;
            value._reducedMotionBehavior = behavior;
            return value;
        }

        static TextStudio2DAnimatedEffectSpec Create(
            TextStudio2DAnimatedEffectKind kind,
            TextStudio2DMappingSpace mapping,
            float amount,
            float speed,
            float phase,
            float scale,
            float width,
            float softness,
            float amplitude,
            float frequency,
            Vector2 direction,
            Color primary,
            Color secondary,
            int seed,
            int historyFrames)
        {
            TextStudioReducedMotionBehavior reducedMotion =
                TextStudioVisualPrimitiveCatalog.TryGet(
                    kind,
                    out TextStudioVisualPrimitiveContract contract)
                    ? contract.ReducedMotionBehavior
                    : TextStudioReducedMotionBehavior.Reduce;
            return new TextStudio2DAnimatedEffectSpec(
                kind,
                mapping,
                amount,
                speed,
                phase,
                scale,
                width,
                softness,
                amplitude,
                frequency,
                direction,
                primary,
                secondary,
                seed,
                historyFrames,
                reducedMotion);
        }
    }

    internal static class TextStudio2DAnimatedEffectUtility
    {
        internal const int MaximumHistoryFrames =
            TextStudioVisualPrimitiveMath.MaximumHistoryFrames;

        internal static TextStudioVisualPrimitiveContract GetContract(
            TextStudio2DAnimatedEffectKind kind)
        {
            return TextStudioVisualPrimitiveCatalog.TryGet(
                kind,
                out TextStudioVisualPrimitiveContract contract)
                ? contract
                : default;
        }

        internal static bool UsesNoise(TextStudio2DAnimatedEffectKind kind)
        {
            return GetContract(kind).UsesProceduralNoise;
        }

        internal static bool RequiresPostProcess(TextStudio2DAnimatedEffectKind kind)
        {
            return GetContract(kind).RequiresPostProcess;
        }

        internal static bool RequiresHistory(TextStudio2DAnimatedEffectKind kind)
        {
            return GetContract(kind).RequiresHistory;
        }

        internal static bool RequiresSpatialScratch(
            TextStudio2DAnimatedEffectKind kind)
        {
            return GetContract(kind).RequiresSpatialScratch;
        }

        internal static bool RequiresDownsampledBlur(
            TextStudio2DAnimatedEffectKind kind)
        {
            return GetContract(kind).RequiresDownsampledBlur;
        }

        internal static int ExtraPassCount(TextStudio2DAnimatedEffectKind kind)
        {
            return GetContract(kind).AdditionalPassCount;
        }
    }
}
