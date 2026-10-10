using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Typewriter settings whose applicability depends on reveal mode or related authoring state.</summary>
    internal enum TextStudioTypewriterModeField
    {
        TimingMode,
        CadencePreset,
        TimingAsset,
        FinalRevealWait,
        RevealDuration,
        RevealSequence,
        RevealEffect,
        RevealEffectTags,
        HideEffectTags,
        SkipReveal,
        RepeatDelay,
        WritingOptions
    }

    /// <summary>Authoring values that decide which Typewriter settings apply.</summary>
    internal readonly struct TextStudioTypewriterModeState
    {
        public TextStudioTypewriterModeState(
            RevealMode mode,
            bool customTimingSelected,
            bool hasTimingAsset,
            bool repeat,
            bool writingEnabled,
            bool hasRevealEffectAsset,
            bool hasHideEffectAsset)
        {
            Mode = mode;
            CustomTimingSelected = customTimingSelected || hasTimingAsset;
            HasTimingAsset = hasTimingAsset;
            Repeat = repeat;
            WritingEnabled = writingEnabled;
            HasRevealEffectAsset = hasRevealEffectAsset;
            HasHideEffectAsset = hasHideEffectAsset;
        }

        public RevealMode Mode { get; }
        public bool CustomTimingSelected { get; }
        public bool HasTimingAsset { get; }
        public bool Repeat { get; }
        public bool WritingEnabled { get; }
        public bool HasRevealEffectAsset { get; }
        public bool HasHideEffectAsset { get; }

        /// <summary>Reads state from an owner's serialized properties. Missing properties read as unset.</summary>
        public static TextStudioTypewriterModeState Read(
            SerializedProperty revealMode,
            bool customTimingSelected,
            SerializedProperty timings,
            SerializedProperty repeat,
            SerializedProperty writingEnabled,
            SerializedProperty revealEffect,
            SerializedProperty hideEffect)
        {
            return new TextStudioTypewriterModeState(
                revealMode != null ? (RevealMode)revealMode.enumValueIndex : RevealMode.Instant,
                customTimingSelected,
                timings != null && timings.objectReferenceValue != null,
                repeat != null && repeat.boolValue,
                writingEnabled != null && writingEnabled.boolValue,
                revealEffect != null && revealEffect.objectReferenceValue != null,
                hideEffect != null && hideEffect.objectReferenceValue != null);
        }
    }

    /// <summary>Mode guidance and setting applicability shared by every Typewriter authoring surface.</summary>
    internal static class TextStudioTypewriterModePresentation
    {
        public const string ModeLabel = "Mode";
        public const string ModeTooltip =
            "Choose how text is grouped for reveal: all at once, by glyph, word, line, tagged effect region, shaped letter cluster, or groups defined by a scripted timing asset.";
        public const string PlaybackSpeedLabel = "Playback Speed";
        public const string PlaybackSpeedTooltip =
            "Scales authored reveal and hide playback at runtime; 2 plays twice as fast. The Live Preview speed control is separate and does not change this value.";
        public const string RevealDurationSectionTitle = "Reveal / Hide Duration";
        public const string HideDurationSectionTitle = "Hide & Repeat";
        public const string RevealEffectsSectionTitle = "Reveal & Hide Effects";
        public const string HideEffectsSectionTitle = "Hide Effects";
        public const string InstantWritingGuidance =
            "With Instant, letters appear fully drawn. Erase on Hide still animates when the text hides.";

        const string SectionTitleClass = "ts-section-title";

        public static string GetGuidance(RevealMode mode)
        {
            switch (mode)
            {
                case RevealMode.Instant:
                    return "Instant shows the whole text at once, so reveal timing and reveal effects do not apply. Hide playback, Playback Speed, Repeat, cues and skip waits still work.";
                case RevealMode.ByGlyph:
                    return "By Glyph reveals each character in text order at the selected cadence.";
                case RevealMode.ByWord:
                    return "By Word reveals each word as one group; its letters share one transition.";
                case RevealMode.ByLine:
                    return "By Line reveals each rendered line as one group.";
                case RevealMode.ByRegion:
                    return "By Region reveals each tagged effect range as one group. For example, <wave>AB</wave><shake>CD</shake> reveals AB, then CD. Untagged text reveals one unit at a time.";
                case RevealMode.ByLetter:
                    return "By Letter reveals each shaped letter cluster as one group, so combining marks, ligatures and emoji sequences appear together. Without renderer cluster data, units reveal individually.";
                case RevealMode.Custom:
                    return "Custom groups adjacent units whose timing asset returns the same ID from GetRevealGroup. The same asset supplies the delay after each group and whether to wait after the final one. Character Timings and Word Timings do not define groups.";
                default:
                    return string.Empty;
            }
        }

        public static string GetMissingTimingAssetMessage(RevealMode mode)
        {
            return mode == RevealMode.Custom
                ? "Assign a scripted timing asset that derives from TypingTimingsBase and overrides GetRevealGroup. Until then, each unit reveals individually using the stored built-in cadence."
                : "Assign a timing asset, or use + to create one.";
        }

        public static bool IsApplicable(TextStudioTypewriterModeField field, in TextStudioTypewriterModeState state)
        {
            bool reveals = state.Mode != RevealMode.Instant;
            bool custom = state.Mode == RevealMode.Custom;
            switch (field)
            {
                case TextStudioTypewriterModeField.TimingMode:
                case TextStudioTypewriterModeField.RevealDuration:
                case TextStudioTypewriterModeField.RevealSequence:
                case TextStudioTypewriterModeField.RevealEffect:
                case TextStudioTypewriterModeField.SkipReveal:
                    return reveals;
                case TextStudioTypewriterModeField.CadencePreset:
                    return reveals && !custom;
                case TextStudioTypewriterModeField.TimingAsset:
                    return reveals && (custom || state.CustomTimingSelected);
                case TextStudioTypewriterModeField.FinalRevealWait:
                    return reveals && (custom ? !state.HasTimingAsset : !state.CustomTimingSelected);
                case TextStudioTypewriterModeField.RevealEffectTags:
                    return reveals && !state.HasRevealEffectAsset;
                case TextStudioTypewriterModeField.HideEffectTags:
                    return !state.HasHideEffectAsset;
                case TextStudioTypewriterModeField.RepeatDelay:
                    return state.Repeat;
                case TextStudioTypewriterModeField.WritingOptions:
                    return state.WritingEnabled;
                default:
                    return true;
            }
        }

        internal static void SetDisplayed(VisualElement element, bool displayed)
        {
            if (element != null)
                element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal static Label FindSectionTitle(VisualElement section)
        {
            return section?.Q<Label>(className: SectionTitleClass);
        }
    }

    /// <summary>
    /// Retained Typewriter elements of one authoring surface. Owners build and bind fields, register the ones whose
    /// applicability depends on mode, and route every dependent property change through <see cref="Refresh"/>.
    /// </summary>
    internal sealed class TextStudioTypewriterModeView
    {
        const int FieldCount = (int)TextStudioTypewriterModeField.WritingOptions + 1;

        readonly Func<TextStudioTypewriterModeState> _readState;
        readonly List<VisualElement>[] _fields = new List<VisualElement>[FieldCount];
        Label _guidance;
        VisualElement _writingGuidance;
        HelpBox _missingTimingAsset;
        Button _createTimingAsset;
        bool _createTimingAssetEditable;
        Label _durationTitle;
        Label _effectsTitle;

        public TextStudioTypewriterModeView(Func<TextStudioTypewriterModeState> readState)
        {
            _readState = readState ?? throw new ArgumentNullException(nameof(readState));
        }

        public void Add(TextStudioTypewriterModeField field, VisualElement element)
        {
            if (element == null)
                return;

            List<VisualElement> elements = _fields[(int)field];
            if (elements == null)
            {
                elements = new List<VisualElement>(2);
                _fields[(int)field] = elements;
            }
            elements.Add(element);
        }

        public void SetGuidance(Label guidance)
        {
            _guidance = guidance;
            if (_guidance != null)
                _guidance.enableRichText = false;
        }

        /// <summary>Registers the Instant Writing explanation; <paramref name="container"/> is the element to show or hide when the label is wrapped.</summary>
        public void SetWritingGuidance(Label guidance, VisualElement container = null)
        {
            if (guidance == null)
                return;

            guidance.text = TextStudioTypewriterModePresentation.InstantWritingGuidance;
            _writingGuidance = container ?? guidance;
        }

        public void SetMissingTimingAsset(HelpBox missing) => _missingTimingAsset = missing;

        /// <summary>Registers the ordinary timing-asset creation shortcut; read-only owners pass <paramref name="editable"/> false.</summary>
        public void SetCreateTimingAsset(Button create, bool editable = true)
        {
            _createTimingAsset = create;
            _createTimingAssetEditable = editable;
        }

        public void SetDurationSection(VisualElement section) =>
            _durationTitle = TextStudioTypewriterModePresentation.FindSectionTitle(section);

        public void SetEffectsSection(VisualElement section) =>
            _effectsTitle = TextStudioTypewriterModePresentation.FindSectionTitle(section);

        /// <summary>Refreshes the view whenever any listed property changes, including Undo and external edits.</summary>
        public void Track(VisualElement host, params SerializedProperty[] properties)
        {
            if (host == null || properties == null)
                return;

            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i] != null)
                    host.TrackPropertyValue(properties[i], _ => Refresh());
            }
        }

        public void Refresh() => Apply(_readState());

        void Apply(in TextStudioTypewriterModeState state)
        {
            for (int i = 0; i < _fields.Length; i++)
            {
                List<VisualElement> elements = _fields[i];
                if (elements == null)
                    continue;

                bool applicable = TextStudioTypewriterModePresentation.IsApplicable((TextStudioTypewriterModeField)i, state);
                for (int j = 0; j < elements.Count; j++)
                    TextStudioTypewriterModePresentation.SetDisplayed(elements[j], applicable);
            }

            bool reveals = state.Mode != RevealMode.Instant;
            if (_guidance != null)
                _guidance.text = TextStudioTypewriterModePresentation.GetGuidance(state.Mode);
            if (_writingGuidance != null)
                TextStudioTypewriterModePresentation.SetDisplayed(_writingGuidance, !reveals && state.WritingEnabled);
            if (_durationTitle != null)
            {
                _durationTitle.text = reveals
                    ? TextStudioTypewriterModePresentation.RevealDurationSectionTitle
                    : TextStudioTypewriterModePresentation.HideDurationSectionTitle;
            }
            if (_effectsTitle != null)
            {
                _effectsTitle.text = reveals
                    ? TextStudioTypewriterModePresentation.RevealEffectsSectionTitle
                    : TextStudioTypewriterModePresentation.HideEffectsSectionTitle;
            }

            bool timingAssetShown = TextStudioTypewriterModePresentation.IsApplicable(
                TextStudioTypewriterModeField.TimingAsset,
                state);
            if (_missingTimingAsset != null)
            {
                _missingTimingAsset.text = TextStudioTypewriterModePresentation.GetMissingTimingAssetMessage(state.Mode);
                TextStudioTypewriterModePresentation.SetDisplayed(
                    _missingTimingAsset,
                    timingAssetShown && !state.HasTimingAsset);
            }
            if (_createTimingAsset != null)
            {
                if (state.Mode == RevealMode.Custom)
                    TextStudioTypewriterModePresentation.SetDisplayed(_createTimingAsset, false);
                else
                {
                    TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                        _createTimingAsset,
                        _createTimingAssetEditable && !state.HasTimingAsset);
                }
            }
        }
    }
}
