using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioTagExpansionClosingMode
    {
        Auto,
        Custom
    }

    /// <summary>One stable shorthand tag expansion with opening and derived or custom closing markup.</summary>
    [Serializable]
    public sealed class TextStudioTagExpansionEntry
    {
        [SerializeField] string _tagId = "style";
        [SerializeField, TextArea(1, 4)] string _openingMarkup = string.Empty;
        [SerializeField] TextStudioTagExpansionClosingMode _closingMode = TextStudioTagExpansionClosingMode.Auto;
        [SerializeField, TextArea(1, 4)] string _customClosingMarkup = string.Empty;

        public string TagId => _tagId ?? string.Empty;
        public string OpeningMarkup => _openingMarkup ?? string.Empty;
        public TextStudioTagExpansionClosingMode ClosingMode => _closingMode;
        public string CustomClosingMarkup => _customClosingMarkup ?? string.Empty;
        public string ClosingMarkup => _closingMode == TextStudioTagExpansionClosingMode.Custom
            ? CustomClosingMarkup
            : TextStudioTagExpansionUtility.DeriveClosingMarkup(OpeningMarkup);

        public TextStudioTagExpansionEntry()
        {
        }

        /// <summary>Creates an expansion whose closing markup is derived from its opening markup.</summary>
        public TextStudioTagExpansionEntry(string tagId, string openingMarkup)
            : this(tagId, openingMarkup, TextStudioTagExpansionClosingMode.Auto, string.Empty)
        {
        }

        /// <summary>Creates an expansion with explicit custom closing markup.</summary>
        public TextStudioTagExpansionEntry(string tagId, string openingMarkup, string customClosingMarkup)
            : this(tagId, openingMarkup, TextStudioTagExpansionClosingMode.Custom, customClosingMarkup)
        {
        }

        /// <summary>Creates an expansion with explicit closing policy and optional custom closing markup.</summary>
        public TextStudioTagExpansionEntry(
            string tagId,
            string openingMarkup,
            TextStudioTagExpansionClosingMode closingMode,
            string customClosingMarkup)
        {
            _tagId = tagId ?? string.Empty;
            _openingMarkup = openingMarkup ?? string.Empty;
            _closingMode = closingMode;
            _customClosingMarkup = customClosingMarkup ?? string.Empty;
        }
    }

    /// <summary>Reusable shorthand-tag sheet resolved before canonical markup parsing.</summary>
    [CreateAssetMenu(
        fileName = "TextStudioTagExpansionSheet",
        menuName = "Text Studio/Text Authoring/Tag Expansion Sheet",
        order = 104)]
    public sealed class TextStudioTagExpansionSheet : ScriptableObject
    {
        [SerializeField] List<TextStudioTagExpansionEntry> _entries = new List<TextStudioTagExpansionEntry>();

        public IReadOnlyList<TextStudioTagExpansionEntry> Entries => _entries;

        /// <summary>Tries to resolve an enabled expansion by case-insensitive stable tag ID.</summary>
        public bool TryGet(string tagId, out TextStudioTagExpansionEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(tagId) || _entries == null)
                return false;

            string normalized = tagId.Trim();
            for (int i = 0; i < _entries.Count; i++)
            {
                TextStudioTagExpansionEntry candidate = _entries[i];
                if (candidate == null ||
                    !string.Equals(candidate.TagId, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry = candidate;
                return true;
            }

            return false;
        }

        internal void InitializeForTests(IEnumerable<TextStudioTagExpansionEntry> entries)
        {
            SetEntries(entries);
        }

        internal void SetEntries(IEnumerable<TextStudioTagExpansionEntry> entries)
        {
            _entries.Clear();
            if (entries == null)
                return;

            foreach (TextStudioTagExpansionEntry entry in entries)
            {
                if (entry != null)
                    _entries.Add(entry);
            }
        }
    }

    public static class TextStudioTagExpansionUtility
    {
        public static string DeriveClosingMarkup(string openingMarkup)
        {
            if (string.IsNullOrEmpty(openingMarkup) || openingMarkup.IndexOf('<') < 0)
                return string.Empty;

            List<string> openTags = new List<string>(4);
            int cursor = 0;
            while (cursor < openingMarkup.Length)
            {
                int tagStart = openingMarkup.IndexOf('<', cursor);
                if (tagStart < 0)
                    break;

                if (IsEscaped(openingMarkup, tagStart))
                {
                    cursor = tagStart + 1;
                    continue;
                }

                int tagEnd = openingMarkup.IndexOf('>', tagStart + 1);
                if (tagEnd < 0)
                    break;

                if (TryParseTag(openingMarkup, tagStart, tagEnd, out string name, out bool isClosing, out bool selfClosing, out bool hasParameter))
                {
                    if (isClosing)
                    {
                        RemoveLast(openTags, name);
                    }
                    else if (!selfClosing && !IsSelfContained(name, hasParameter))
                    {
                        openTags.Add(name);
                    }
                }

                cursor = tagEnd + 1;
            }

            if (openTags.Count == 0)
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(openTags.Count * 8);
            for (int i = openTags.Count - 1; i >= 0; i--)
                builder.Append("</").Append(openTags[i]).Append('>');
            return builder.ToString();
        }

        internal static bool TryParseTag(
            string source,
            int tagStart,
            int tagEnd,
            out string name,
            out bool isClosing,
            out bool selfClosing,
            out bool hasParameter)
        {
            name = string.Empty;
            isClosing = false;
            selfClosing = false;
            hasParameter = false;

            int index = tagStart + 1;
            while (index < tagEnd && char.IsWhiteSpace(source[index]))
                index++;

            if (index >= tagEnd)
                return false;

            isClosing = source[index] == '/';
            if (isClosing)
            {
                index++;
                while (index < tagEnd && char.IsWhiteSpace(source[index]))
                    index++;
            }

            int trimmedEnd = tagEnd;
            while (trimmedEnd > index && char.IsWhiteSpace(source[trimmedEnd - 1]))
                trimmedEnd--;

            selfClosing = !isClosing && trimmedEnd > index && source[trimmedEnd - 1] == '/';
            if (selfClosing)
            {
                trimmedEnd--;
                while (trimmedEnd > index && char.IsWhiteSpace(source[trimmedEnd - 1]))
                    trimmedEnd--;
            }

            int nameStart = index;
            while (index < trimmedEnd &&
                   !char.IsWhiteSpace(source[index]) &&
                   source[index] != '=' &&
                   source[index] != '/')
            {
                index++;
            }

            if (index <= nameStart)
                return false;

            name = source.Substring(nameStart, index - nameStart);
            hasParameter = index < trimmedEnd;
            return true;
        }

        internal static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;

            return slashCount % 2 == 1;
        }

        // Compared in place: searching for "<!--" from each '<' scanned the rest of the text when it held no comment.
        internal static bool IsCommentStart(string source, int index)
        {
            return index + 3 < source.Length &&
                   source[index] == '<' &&
                   source[index + 1] == '!' &&
                   source[index + 2] == '-' &&
                   source[index + 3] == '-';
        }

        static bool IsSelfContained(string tagName, bool hasParameter)
        {
            return BuiltInActions.TryGetCanonicalActionId(tagName, out _) ||
                   BuiltInActions.IsMarkerTag(tagName) ||
                   BuiltInStyles.IsSelfContainedNativeRichTextFormatting(tagName, hasParameter);
        }

        static void RemoveLast(List<string> openTags, string tagName)
        {
            for (int i = openTags.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(openTags[i], tagName, StringComparison.OrdinalIgnoreCase))
                    continue;

                openTags.RemoveAt(i);
                return;
            }
        }
    }
}
