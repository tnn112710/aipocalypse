using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Punctuation-aware character timings for natural typewriter pacing.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterTimings",
        menuName = "Text Studio/Animation/Character Timings",
        order = 106)]
    public class CharacterTimings : TypingTimingsBase
    {
        [SerializeField, Tooltip("Delay (seconds) between every regular character.")]
        float _baseDelay = 0.03f;

        [SerializeField, Tooltip("Delay applied to terminator punctuation. Raise it above the regular character delay to add a sentence pause.")]
        float _longPunctDelay = 0.03f;

        [SerializeField, Tooltip("Delay applied to separator punctuation. Raise it above the regular character delay to add a shorter reading pause.")]
        float _mediumPunctDelay = 0.03f;

        [SerializeField, Tooltip("Characters that use the long reading pause.")]
        string _longPunctChars = ".!?";
        [SerializeField, Tooltip("Characters that use the medium reading pause.")]
        string _mediumPunctChars = ",;:-";
        [SerializeField, Tooltip("When enabled, only the first punctuation mark in a run adds a long or medium pause.")]
        bool _collapseMultiplePunctuation = true;
        [SerializeField, Tooltip("When disabled, newline characters reveal without adding an extra wait.")]
        bool _waitForNewLines = true;
        [SerializeField, Tooltip("Delay applied to newline characters when Wait For New Lines is enabled.")]
        float _newLineDelay = 0.03f;
        [SerializeField, Tooltip("When enabled, the reveal completes only after the final unit's trailing delay.")]
        bool _waitAfterFinalReveal = true;
        [SerializeField, Tooltip("When enabled, a final punctuation mark uses the regular character delay instead of a reading pause.")]
        bool _skipLastPunctuationDelay;
        [SerializeField, Tooltip("Comma-separated abbreviations whose trailing period should not create a sentence pause.")]
        string _abbreviationWhitelist = "Dr,Mr,Ms,Mrs,Prof,Sr,Jr,St,etc,e.g,i.e";
        public float BaseDelay { get => _baseDelay; set => _baseDelay = Mathf.Max(0f, value); }
        public float LongPunctuationDelay { get => _longPunctDelay; set => _longPunctDelay = Mathf.Max(0f, value); }
        public float MediumPunctuationDelay { get => _mediumPunctDelay; set => _mediumPunctDelay = Mathf.Max(0f, value); }
        public string LongPunctuationChars { get => _longPunctChars ?? string.Empty; set => _longPunctChars = value ?? string.Empty; }
        public string MediumPunctuationChars { get => _mediumPunctChars ?? string.Empty; set => _mediumPunctChars = value ?? string.Empty; }
        public bool CollapseMultiplePunctuation { get => _collapseMultiplePunctuation; set => _collapseMultiplePunctuation = value; }
        public bool WaitForNewLines { get => _waitForNewLines; set => _waitForNewLines = value; }
        public float NewLineDelay { get => _newLineDelay; set => _newLineDelay = Mathf.Max(0f, value); }
        public bool WaitAfterFinalReveal { get => _waitAfterFinalReveal; set => _waitAfterFinalReveal = value; }
        public bool SkipLastPunctuationDelay { get => _skipLastPunctuationDelay; set => _skipLastPunctuationDelay = value; }
        public string AbbreviationWhitelist { get => _abbreviationWhitelist ?? string.Empty; set => _abbreviationWhitelist = value ?? string.Empty; }

        public override bool IncludesFinalRevealDelay => _waitAfterFinalReveal;

        /// <inheritdoc/>
        public override float GetRevealDelayAfter(in TextStudioTimingContext context)
        {
            char character = context.PrimaryCharacter;
            if (context.IsLineBreak)
                return _waitForNewLines ? _newLineDelay : 0f;

            bool punctuation = IsLongPunctuation(character) || IsMediumPunctuation(character);
            if (punctuation)
            {
                if (_collapseMultiplePunctuation && HasPreviousPunctuation(in context))
                    return _baseDelay;
                if (_skipLastPunctuationDelay && context.IsLastVisibleUnit)
                    return _baseDelay;
                if (character == '.' && IsWhitelistedAbbreviation(in context))
                    return _baseDelay;
            }

            if (IsLongPunctuation(character)) return _longPunctDelay;
            if (IsMediumPunctuation(character)) return _mediumPunctDelay;
            return _baseDelay;
        }

        bool IsLongPunctuation(char character)
        {
            return !string.IsNullOrEmpty(_longPunctChars) && _longPunctChars.IndexOf(character) >= 0;
        }

        bool IsMediumPunctuation(char character)
        {
            return !string.IsNullOrEmpty(_mediumPunctChars) && _mediumPunctChars.IndexOf(character) >= 0;
        }

        bool HasPreviousPunctuation(in TextStudioTimingContext context)
        {
            if (context.PreviousUnitIndex < 0)
                return false;

            char previous = context.Plan.GetPrimaryChar(context.PreviousUnitIndex);
            return IsLongPunctuation(previous) || IsMediumPunctuation(previous);
        }

        bool IsWhitelistedAbbreviation(in TextStudioTimingContext context)
        {
            string text = context.ResolvedText;
            int periodIndex = context.ResolvedStart;
            if (string.IsNullOrEmpty(_abbreviationWhitelist) || periodIndex <= 0)
                return false;

            int start = periodIndex - 1;
            while (start >= 0 && (char.IsLetter(text[start]) || text[start] == '.'))
                start--;
            start++;

            int length = periodIndex - start;
            if (length <= 0)
                return false;

            int tokenStart = 0;
            for (int i = 0; i <= _abbreviationWhitelist.Length; i++)
            {
                bool atEnd = i == _abbreviationWhitelist.Length;
                char character = atEnd ? ',' : _abbreviationWhitelist[i];
                if (!atEnd && character != ',' && character != ';' && character != '|')
                    continue;

                int tokenEnd = i;
                while (tokenStart < tokenEnd && char.IsWhiteSpace(_abbreviationWhitelist[tokenStart]))
                    tokenStart++;
                while (tokenEnd > tokenStart && char.IsWhiteSpace(_abbreviationWhitelist[tokenEnd - 1]))
                    tokenEnd--;

                int tokenLength = tokenEnd - tokenStart;
                if (tokenLength == length &&
                    string.Compare(_abbreviationWhitelist, tokenStart, text, start, length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                tokenStart = i + 1;
            }

            return false;
        }
    }
}
