using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Word-paced reveal timing settings.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WordTimings",
        menuName = "Text Studio/Animation/Word Timings",
        order = 107)]
    public class WordTimings : TypingTimingsBase
    {
        [SerializeField, Tooltip("Delay inserted at whitespace between words.")]
        float _normalWordDelay = 0.3f;
        [SerializeField, Tooltip("Delay inserted after punctuation at the end of a word.")]
        float _punctuationWordDelay = 0.5f;
        [SerializeField, Tooltip("Characters that use the punctuation boundary delay.")]
        string _punctuationChars = ".!?,;:";
        [SerializeField, Tooltip("When enabled, the reveal completes only after the final word unit's trailing delay.")]
        bool _waitAfterFinalReveal = true;

        public float NormalWordDelay { get => _normalWordDelay; set => _normalWordDelay = Mathf.Max(0f, value); }
        public float PunctuationWordDelay { get => _punctuationWordDelay; set => _punctuationWordDelay = Mathf.Max(0f, value); }
        public string PunctuationChars { get => _punctuationChars ?? string.Empty; set => _punctuationChars = value ?? string.Empty; }
        public bool WaitAfterFinalReveal { get => _waitAfterFinalReveal; set => _waitAfterFinalReveal = value; }

        public override bool IncludesFinalRevealDelay => _waitAfterFinalReveal;

        /// <inheritdoc/>
        public override float GetRevealDelayAfter(in TextStudioTimingContext context)
        {
            char character = context.PrimaryCharacter;
            if (char.IsWhiteSpace(character))
            {
                int previousUnitIndex = context.PreviousUnitIndex;
                while (previousUnitIndex >= 0 &&
                       context.Plan.IsWhiteSpaceUnit(previousUnitIndex))
                {
                    previousUnitIndex--;
                }

                if (previousUnitIndex >= 0 &&
                    IsPunctuation(context.Plan.GetPrimaryChar(previousUnitIndex)))
                {
                    return 0f;
                }

                return _normalWordDelay;
            }
            if (IsPunctuation(character))
                return _punctuationWordDelay;
            return 0f;
        }

        bool IsPunctuation(char character)
        {
            return !string.IsNullOrEmpty(_punctuationChars) && _punctuationChars.IndexOf(character) >= 0;
        }
    }
}
