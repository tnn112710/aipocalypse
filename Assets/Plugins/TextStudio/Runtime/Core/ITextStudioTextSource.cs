using System;

namespace TextStudio
{
    /// <summary>
    /// Text source interface for canonical text input.
    /// Sources provide text with version tracking for dirty detection.
    /// </summary>
    public interface ITextStudioTextSource
    {
        /// <summary>Gets the complete canonical markup represented by the source.</summary>
        /// <returns>Current markup. Implementations should return an empty string instead of <see langword="null"/>.</returns>
        string GetCanonicalText();

        /// <summary>Gets a monotonically changing value used to detect source mutations without comparing text.</summary>
        uint GetVersion();

        /// <summary>Occurs after canonical text or its effective interpretation changes.</summary>
        event Action Changed;
    }

    /// <summary>
    /// Mutable in-memory text source that tracks replacements and append operations for incremental compilation.
    /// </summary>
    public class StringTextSource : ITextStudioTextSource
    {
        string _text;
        uint _version;
        uint _appendBaseVersion;
        uint _appendVersion;
        int _appendStart = -1;

        public event Action Changed;

        /// <summary>Creates a source with optional initial markup.</summary>
        /// <param name="text">Initial markup. A null value is treated as empty when first processed.</param>
        public StringTextSource(string text = "")
        {
            _text = text;
        }

        /// <inheritdoc/>
        public string GetCanonicalText() => _text;

        /// <inheritdoc/>
        public uint GetVersion() => _version;

        /// <summary>Replaces canonical markup and notifies listeners when its value changes.</summary>
        /// <param name="text">Replacement markup.</param>
        public void SetText(string text)
        {
            if (_text == text) return;
            _text = text;
            _version++;
            ClearAppendMetadata();
            Changed?.Invoke();
        }

        /// <summary>
        /// Append markup to the current text without treating it as a fresh replacement.
        /// Bumps the version so downstream stages rebuild, but callers that want to preserve
        /// reveal/animation state for the already-shown prefix can key off text length diffs.
        /// </summary>
        public void AppendText(string markup)
        {
            if (string.IsNullOrEmpty(markup)) return;
            string existingText = _text ?? string.Empty;
            SetAppendedText(existingText + markup, existingText.Length);
        }

        internal void SetAppendedText(string text, int appendStart)
        {
            text ??= string.Empty;
            if (_text == text)
                return;

            int previousLength = _text != null ? _text.Length : 0;
            if (appendStart != previousLength ||
                appendStart < 0 ||
                appendStart > text.Length ||
                (appendStart > 0 &&
                 string.CompareOrdinal(_text, 0, text, 0, appendStart) != 0))
            {
                SetText(text);
                return;
            }

            if (_appendStart < 0 || _appendVersion != _version)
            {
                _appendBaseVersion = _version;
                _appendStart = appendStart;
            }

            _text = text;
            _version++;
            _appendVersion = _version;
            Changed?.Invoke();
        }

        internal bool TryConsumeAppendRange(
            uint processedVersion,
            uint currentVersion,
            out int appendStart)
        {
            bool valid = HasAppendRangeSince(processedVersion, currentVersion);
            appendStart = valid ? _appendStart : -1;
            ClearAppendMetadata();
            return valid;
        }

        /// <summary>Whether every change since the processed version only appended text, without consuming the range.</summary>
        internal bool HasAppendRangeSince(uint processedVersion, uint currentVersion)
        {
            return _appendStart >= 0 &&
                   _appendBaseVersion == processedVersion &&
                   _appendVersion == currentVersion;
        }

        void ClearAppendMetadata()
        {
            _appendBaseVersion = 0;
            _appendVersion = 0;
            _appendStart = -1;
        }
    }
}
