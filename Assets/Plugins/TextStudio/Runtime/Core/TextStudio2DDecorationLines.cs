using System;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudio2DLinePattern : byte { Solid, Double, Dotted, Dashed, Wavy }
    public enum TextStudio2DMetricMode : byte { Custom, Auto, FromFont }
    public enum TextStudio2DLinePhaseRestart : byte { Match, Line }
    public enum TextStudio2DLineCap : byte { Butt, Round, Square }
    public enum TextStudio2DSkipInk : byte { Off, Auto, Precise }

    /// <summary>Settings for decoration-line patterns, font metrics, phase, caps, and clearance around glyph ink.</summary>
    [Serializable]
    public struct TextStudio2DDecorationLineSpec
    {
        [SerializeField] TextStudio2DLinePattern _pattern;
        [SerializeField] TextStudio2DMetricMode _thicknessMode;
        [SerializeField] TextStudio2DMetricMode _offsetMode;
        [SerializeField] TextStudio2DUnit _units;
        [SerializeField, Tooltip("Use each resolved font run's metrics instead of the first selected font on the line.")] bool _followFontRuns;
        [SerializeField] TextStudio2DLinePhaseRestart _phaseRestart;
        [SerializeField, TextStudio2DParameter(0f)] float _dashLength;
        [SerializeField, TextStudio2DParameter(0f)] float _gap;
        [SerializeField, TextStudio2DParameter(0f)] float _dotSpacing;
        [SerializeField, TextStudio2DParameter(0f)] float _waveAmplitude;
        [SerializeField, TextStudio2DParameter(0f)] float _wavelength;
        [SerializeField, TextStudio2DParameter(0f)] float _doubleSeparation;
        [SerializeField] float _phase;
        [SerializeField] TextStudio2DLineCap _cap;
        [SerializeField, Tooltip("Auto uses prepared font outlines when available and font metric bounds otherwise. Precise requires prepared font appearance data.")] TextStudio2DSkipInk _skipInk;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Clearance around the original glyph face, measured in em units.")] float _inkClearance;

        public TextStudio2DLinePattern Pattern => _pattern;
        public TextStudio2DMetricMode ThicknessMode => _thicknessMode;
        public TextStudio2DMetricMode OffsetMode => _offsetMode;
        public TextStudio2DUnit Units => _units;
        public bool FollowFontRuns => _followFontRuns;
        public TextStudio2DLinePhaseRestart PhaseRestart => _phaseRestart;
        public float DashLength => _dashLength;
        public float Gap => _gap;
        public float DotSpacing => _dotSpacing;
        public float WaveAmplitude => _waveAmplitude;
        public float Wavelength => _wavelength;
        public float DoubleSeparation => _doubleSeparation;
        public float Phase => _phase;
        public TextStudio2DLineCap Cap => _cap;
        public TextStudio2DSkipInk SkipInk => _skipInk;
        public float InkClearance => _inkClearance;

        public static TextStudio2DDecorationLineSpec Default => new TextStudio2DDecorationLineSpec {
            _thicknessMode = TextStudio2DMetricMode.Custom, _offsetMode = TextStudio2DMetricMode.FromFont,
            _dashLength = .2f, _gap = .12f, _dotSpacing = .15f, _waveAmplitude = .035f,
            _wavelength = .25f, _doubleSeparation = .06f
        };

        /// <summary>Returns a line specification copy with the supplied pattern and dash, dot, wave, and double-line dimensions.</summary>
        public TextStudio2DDecorationLineSpec WithPattern(TextStudio2DLinePattern pattern,
            float dashLength = .2f, float gap = .12f, float dotSpacing = .15f,
            float waveAmplitude = .035f, float wavelength = .25f, float doubleSeparation = .06f)
        {
            TextStudio2DDecorationLineSpec value = this;
            value._pattern = pattern; value._dashLength = dashLength; value._gap = gap;
            value._dotSpacing = dotSpacing; value._waveAmplitude = waveAmplitude;
            value._wavelength = wavelength; value._doubleSeparation = doubleSeparation;
            return value;
        }

        /// <summary>Returns a line specification copy with the supplied thickness and offset metric modes, units, and font-run behavior.</summary>
        public TextStudio2DDecorationLineSpec WithMetrics(TextStudio2DMetricMode thickness, TextStudio2DMetricMode offset,
            TextStudio2DUnit units = TextStudio2DUnit.Em, bool followFontRuns = false)
        {
            TextStudio2DDecorationLineSpec value = this;
            value._thicknessMode = thickness; value._offsetMode = offset;
            value._units = units; value._followFontRuns = followFontRuns;
            return value;
        }

        /// <summary>Returns a line specification copy with the supplied pattern phase, restart scope, and cap style.</summary>
        public TextStudio2DDecorationLineSpec WithPhase(float phase, TextStudio2DLinePhaseRestart restart = TextStudio2DLinePhaseRestart.Match,
            TextStudio2DLineCap cap = TextStudio2DLineCap.Butt)
        {
            TextStudio2DDecorationLineSpec value = this;
            value._phase = phase; value._phaseRestart = restart; value._cap = cap;
            return value;
        }

        /// <summary>Returns a line specification copy with the supplied ink-avoidance mode and clearance in em units.</summary>
        public TextStudio2DDecorationLineSpec WithSkipInk(TextStudio2DSkipInk mode, float clearance = .025f)
        {
            TextStudio2DDecorationLineSpec value = this;
            value._skipInk = mode; value._inkClearance = clearance;
            return value;
        }

        internal bool IsValid => (uint)_pattern <= (uint)TextStudio2DLinePattern.Wavy &&
            (uint)_thicknessMode <= (uint)TextStudio2DMetricMode.FromFont && (uint)_offsetMode <= (uint)TextStudio2DMetricMode.FromFont &&
            (uint)_units <= (uint)TextStudio2DUnit.Pixels && (uint)_phaseRestart <= (uint)TextStudio2DLinePhaseRestart.Line &&
            (uint)_cap <= (uint)TextStudio2DLineCap.Square && float.IsFinite(_phase) &&
            (uint)_skipInk <= (uint)TextStudio2DSkipInk.Precise && Nonnegative(_inkClearance) &&
            (_pattern != TextStudio2DLinePattern.Dashed || Positive(_dashLength) && Nonnegative(_gap)) &&
            (_pattern != TextStudio2DLinePattern.Dotted || Positive(_dotSpacing)) &&
            (_pattern != TextStudio2DLinePattern.Wavy || Positive(_wavelength) && Nonnegative(_waveAmplitude)) &&
            (_pattern != TextStudio2DLinePattern.Double || Nonnegative(_doubleSeparation));

        internal void Hash(ref TextStudioDeterministicHash64 hash)
        {
            hash.Add((byte)_pattern); hash.Add((byte)_thicknessMode); hash.Add((byte)_offsetMode); hash.Add((byte)_units);
            hash.Add(_followFontRuns); hash.Add((byte)_phaseRestart); hash.Add(_dashLength); hash.Add(_gap); hash.Add(_dotSpacing);
            hash.Add(_waveAmplitude); hash.Add(_wavelength); hash.Add(_doubleSeparation); hash.Add(_phase); hash.Add((byte)_cap);
            hash.Add((byte)_skipInk); hash.Add(_inkClearance);
        }

        static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0;
        static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }
}
