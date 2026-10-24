using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Chooses whether typing follows its cadence or waits for a unit's reveal animation to finish before applying the reading pause.</summary>
    public enum TextStudioTypewriterTimingMode
    {
        Cadence,
        FinishCurrentUnit
    }

    /// <summary>Chooses manual Typewriter start or automatic start when the host is enabled, its text changes, or either event occurs.</summary>
    public enum TextStudioTypewriterStartMode
    {
        Manual = 0,
        OnEnable = 1,
        OnTextChanged = 2,
        OnEnableOrTextChanged = 3
    }

    /// <summary>Controls how currently displayed content leaves when typewriter-driven text is replaced.</summary>
    public enum TextStudioTextChangeMode
    {
        /// <summary>Replace the compiled text immediately, then use the normal reveal policy.</summary>
        Immediate,
        /// <summary>Finish the current hide sequence before compiling and revealing the replacement.</summary>
        HideThenReveal,
        /// <summary>Let capable renderers retain outgoing visuals while the replacement reveals immediately.</summary>
        OverlapHandoff
    }

    /// <summary>Controls which semantic glyphs participate in an animated text replacement.</summary>
    public enum TextStudioTextChangeScope
    {
        /// <summary>Keep matching glyphs continuous and animate only inserted, removed, or replaced glyphs.</summary>
        ChangedGlyphs,
        /// <summary>Treat every glyph in both strings as replaced.</summary>
        WholeText,
        /// <summary>Matches one formatted numeric value by decimal place, preserving equal digits in the same position.</summary>
        NumericValue
    }

    /// <summary>Chooses whether skipping reveal settles reveal effects immediately or lets those effects finish after all units become revealed.</summary>
    public enum TextStudioTypewriterSkipRevealMode
    {
        SettleRevealEffectsImmediately,
        AllowRevealEffectsToFinish
    }

    /// <summary>Chooses whether skipping hide removes glyphs immediately or lets the current hide effect finish.</summary>
    public enum TextStudioTypewriterSkipHideMode
    {
        HideImmediately,
        FinishHideEffect
    }

    /// <summary>Identifies a Typewriter wait that ends after a duration, on input, or through a custom wait handler.</summary>
    public enum TextStudioTypewriterWaitKind
    {
        Timed,
        Input,
        Custom
    }

    /// <summary>Immutable notification describing a timed, input, or custom wait in reveal or hide playback.</summary>
    public readonly struct TextStudioTypewriterWaitInfo
    {
        public readonly TextStudioTypewriterWaitKind Kind;
        public readonly int GlyphIndex;
        public readonly char Character;
        public readonly bool IsHide;
        public readonly string ActionId;
        public readonly float Duration;

        /// <summary>Creates a wait notification with glyph, action, phase, and duration metadata.</summary>
        public TextStudioTypewriterWaitInfo(
            TextStudioTypewriterWaitKind kind,
            int glyphIndex,
            char character,
            bool isHide,
            string actionId,
            float duration)
        {
            Kind = kind;
            GlyphIndex = glyphIndex;
            Character = character;
            IsHide = isHide;
            ActionId = actionId ?? string.Empty;
            Duration = duration;
        }
    }

    /// <summary>Complete serializable reveal, hide, timing, transition-effect, skip, and looping configuration for a typewriter host.</summary>
    [Serializable]
    public struct TextStudioTypewriterSettings :
        IEquatable<TextStudioTypewriterSettings>
    {
        public RevealMode RevealMode;
        [Tooltip("Cadence starts units at their typing intervals; Finish Current Unit waits for the current reveal animation before its reading pause.")]
        public TextStudioTypewriterTimingMode TimingMode;
        [Tooltip("Draw letters using a Writing Style during their Typewriter reveal.")]
        public bool WritingEnabled;
        [Tooltip("Reusable drawing technique, brush, and optional authored paths.")]
        public TextStudioWritingStyleAsset WritingStyle;
        [Tooltip("Erase drawn strokes during Hide, starting from their current coverage.")]
        public bool EraseOnHide;
        public TypingTimingsBase Timings;
        public RevealTimingsPreset BuiltInTimingsPreset;
        [Tooltip("When no custom timing asset is assigned, include the built-in cadence's final trailing delay before reveal completion.")]
        public bool WaitAfterFinalReveal;
        [Min(0.01f)] public float RevealSpeedMultiplier;
        [Min(0.01f)] public float RevealAppearanceDuration;
        [Min(0.01f)] public float RevealDisappearanceDuration;
        public TextStudioSequenceSettings RevealSequence;
        public TextStudioSequenceSettings HideSequence;
        public TextStudioTypewriterStartMode StartMode;
        public TextStudioTextChangeMode TextChangeMode;
        public TextStudioTextChangeScope TextChangeScope;
        public bool ResetSpeedOnNewText;
        public TextStudioTypewriterSkipRevealMode SkipRevealMode;
        public bool TriggerRemainingMarkersOnSkip;
        public bool SkipCurrentWaitOnSkip;
        public TextStudioTypewriterSkipHideMode SkipHideMode;
        public bool LoopReveal;
        [Min(0f)] public float LoopRevealDelay;
        [Tooltip("Optional shared effect definition used only while each semantic unit appears.")]
        public TextStudioEffectDefinitionAsset RevealEffect;
        [Tooltip("Optional shared effect definition used only while each semantic unit disappears.")]
        public TextStudioEffectDefinitionAsset HideEffect;
        [TextArea(1, 3), HideInInspector] public string RevealEffectExpression;
        [TextArea(1, 3), HideInInspector] public string HideEffectExpression;

        public bool Equals(TextStudioTypewriterSettings other)
        {
            return TimingMode == other.TimingMode && WritingEnabled == other.WritingEnabled && WritingStyle == other.WritingStyle && EraseOnHide == other.EraseOnHide && RevealMode == other.RevealMode &&
                   Timings == other.Timings &&
                   BuiltInTimingsPreset == other.BuiltInTimingsPreset &&
                   WaitAfterFinalReveal == other.WaitAfterFinalReveal &&
                   RevealSpeedMultiplier.Equals(other.RevealSpeedMultiplier) &&
                   RevealAppearanceDuration.Equals(other.RevealAppearanceDuration) &&
                   RevealDisappearanceDuration.Equals(other.RevealDisappearanceDuration) &&
                   RevealSequence.Equals(other.RevealSequence) &&
                   HideSequence.Equals(other.HideSequence) &&
                   StartMode == other.StartMode &&
                   TextChangeMode == other.TextChangeMode &&
                   TextChangeScope == other.TextChangeScope &&
                   ResetSpeedOnNewText == other.ResetSpeedOnNewText &&
                   SkipRevealMode == other.SkipRevealMode &&
                   TriggerRemainingMarkersOnSkip == other.TriggerRemainingMarkersOnSkip &&
                   SkipCurrentWaitOnSkip == other.SkipCurrentWaitOnSkip &&
                   SkipHideMode == other.SkipHideMode &&
                   LoopReveal == other.LoopReveal &&
                   LoopRevealDelay.Equals(other.LoopRevealDelay) &&
                   RevealEffect == other.RevealEffect &&
                   HideEffect == other.HideEffect &&
                   string.Equals(RevealEffectExpression, other.RevealEffectExpression, StringComparison.Ordinal) &&
                   string.Equals(HideEffectExpression, other.HideEffectExpression, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioTypewriterSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)RevealMode;
                hash = (hash * 397) ^ (int)TimingMode;
                hash = (hash * 397) ^ WritingEnabled.GetHashCode();
                hash = (hash * 397) ^ (WritingStyle != null ? WritingStyle.GetHashCode() : 0);
                hash = (hash * 397) ^ EraseOnHide.GetHashCode();
                hash = (hash * 397) ^ (Timings != null ? Timings.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)BuiltInTimingsPreset;
                hash = (hash * 397) ^ WaitAfterFinalReveal.GetHashCode();
                hash = (hash * 397) ^ RevealSpeedMultiplier.GetHashCode();
                hash = (hash * 397) ^ RevealAppearanceDuration.GetHashCode();
                hash = (hash * 397) ^ RevealDisappearanceDuration.GetHashCode();
                hash = (hash * 397) ^ RevealSequence.GetHashCode();
                hash = (hash * 397) ^ HideSequence.GetHashCode();
                hash = (hash * 397) ^ (int)StartMode;
                hash = (hash * 397) ^ (int)TextChangeMode;
                hash = (hash * 397) ^ (int)TextChangeScope;
                hash = (hash * 397) ^ ResetSpeedOnNewText.GetHashCode();
                hash = (hash * 397) ^ (int)SkipRevealMode;
                hash = (hash * 397) ^ TriggerRemainingMarkersOnSkip.GetHashCode();
                hash = (hash * 397) ^ SkipCurrentWaitOnSkip.GetHashCode();
                hash = (hash * 397) ^ (int)SkipHideMode;
                hash = (hash * 397) ^ LoopReveal.GetHashCode();
                hash = (hash * 397) ^ LoopRevealDelay.GetHashCode();
                hash = (hash * 397) ^ (RevealEffect != null ? RevealEffect.GetHashCode() : 0);
                hash = (hash * 397) ^ (HideEffect != null ? HideEffect.GetHashCode() : 0);
                hash = (hash * 397) ^ (RevealEffectExpression ?? string.Empty).GetHashCode();
                hash = (hash * 397) ^ (HideEffectExpression ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        public static TextStudioTypewriterSettings Default
        {
            get
            {
                return new TextStudioTypewriterSettings
                {
                    RevealMode = RevealMode.ByGlyph,
                    Timings = null,
                    BuiltInTimingsPreset = RevealTimingsPreset.Expressive,
                    WaitAfterFinalReveal = true,
                    RevealSpeedMultiplier = 1f,
                    RevealAppearanceDuration = 0.3f,
                    RevealDisappearanceDuration = 0.3f,
                    RevealSequence = TextStudioSequenceSettings.Default,
                    HideSequence = TextStudioSequenceSettings.Default,
                    StartMode = TextStudioTypewriterStartMode.Manual,
                    TextChangeMode = TextStudioTextChangeMode.Immediate,
                    TextChangeScope = TextStudioTextChangeScope.WholeText,
                    ResetSpeedOnNewText = true,
                    SkipRevealMode = TextStudioTypewriterSkipRevealMode.SettleRevealEffectsImmediately,
                    TriggerRemainingMarkersOnSkip = false,
                    SkipCurrentWaitOnSkip = true,
                    SkipHideMode = TextStudioTypewriterSkipHideMode.HideImmediately,
                    LoopReveal = false,
                    LoopRevealDelay = 0.75f,
                    RevealEffect = null,
                    HideEffect = null,
                    RevealEffectExpression = string.Empty,
                    HideEffectExpression = string.Empty
                };
            }
        }

        /// <summary>Clamps serialized ranges and restores safe defaults for transition effects.</summary>
        public static TextStudioTypewriterSettings NormalizeSerializedSettings(TextStudioTypewriterSettings settings)
        {
            settings.RevealSpeedMultiplier = Mathf.Max(0.01f, settings.RevealSpeedMultiplier);
            settings.RevealAppearanceDuration = Mathf.Max(0.01f, settings.RevealAppearanceDuration);
            settings.RevealDisappearanceDuration = Mathf.Max(0.01f, settings.RevealDisappearanceDuration);
            if (settings.RevealSequence.Equals(default))
                settings.RevealSequence = TextStudioSequenceSettings.Default;
            if (settings.HideSequence.Equals(default))
                settings.HideSequence = TextStudioSequenceSettings.Default;
            settings.RevealSequence.Spread = Mathf.Max(0f, settings.RevealSequence.Spread);
            settings.HideSequence.Spread = Mathf.Max(0f, settings.HideSequence.Spread);
            settings.LoopRevealDelay = Mathf.Max(0f, settings.LoopRevealDelay);
            settings.RevealEffectExpression ??= string.Empty;
            settings.HideEffectExpression ??= string.Empty;
            return settings;
        }

        /// <summary>Normalizes serialized Typewriter timing and Reveal/Hide effect settings.</summary>
        public static TextStudioTypewriterSettings NormalizeAuthoringSettings(TextStudioTypewriterSettings settings)
        {
            return NormalizeSerializedSettings(settings);
        }

        /// <summary>Resolves runtime-ready Reveal/Hide effect expressions and shared effect references.</summary>
        public static TextStudioTypewriterSettings ResolveRuntimeSettings(TextStudioTypewriterSettings settings)
        {
            settings = NormalizeSerializedSettings(settings);
            if (settings.RevealEffect != null)
                settings.RevealEffectExpression = string.Empty;
            if (settings.HideEffect != null)
                settings.HideEffectExpression = string.Empty;
            return settings;
        }
    }

    /// <summary>Reusable authoring asset containing a complete typewriter settings recipe and gallery metadata.</summary>
    [CreateAssetMenu(
        fileName = "TextStudioTypewriterPreset",
        menuName = "Text Studio/Animation/Typewriter Preset",
        order = 104)]
    public sealed class TextStudioTypewriterPreset : ScriptableObject
    {
        [SerializeField] string _displayName = "Typewriter Preset";
        [SerializeField, TextArea(2, 4)] string _description;
        [SerializeField, TextArea(2, 4)] string _sampleText = "This line reveals like a typewriter.";
        [SerializeField] TextStudioTypewriterSettings _settings = TextStudioTypewriterSettings.Default;

        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? name : _displayName;
            set => _displayName = value ?? string.Empty;
        }

        public string Description
        {
            get => _description ?? string.Empty;
            set => _description = value ?? string.Empty;
        }

        public string SampleText
        {
            get => _sampleText ?? string.Empty;
            set => _sampleText = value ?? string.Empty;
        }

        public TextStudioTypewriterSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        void Reset()
        {
            _settings = TextStudioTypewriterSettings.Default;
        }

        void OnValidate()
        {
            _settings = TextStudioTypewriterSettings.NormalizeAuthoringSettings(_settings);
        }
    }
}
