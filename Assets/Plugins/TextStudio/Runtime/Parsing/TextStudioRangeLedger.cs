using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;

namespace TextStudio
{
    internal enum TextStudioRangeOwner : byte { None, Native, Appearance, Other, Extension }

    internal struct TextStudioRangeEntry
    {
        internal TextStudioRangeOwner Owner;
        internal string Name;
        internal int OpeningTokenIndex;
        internal int AppearanceIndex;
        internal int ParentAppearanceIndex;
        internal string AppearanceId;
        internal bool IsAdHoc;
        internal bool IsRange;
        internal TextStudioTagParameters Parameters;
    }

    /// <summary>One opening ledger owns closing interpretation for both display emission and semantic resolution.</summary>
    internal sealed class TextStudioRangeLedger
    {
        readonly List<int> _open = new List<int>();
        TextStudioRangeEntry[] _entries = Array.Empty<TextStudioRangeEntry>();
        int _appearanceCount;
        // Tokens the last build interpreted; an append resumes after them (BuildAppended).
        int _builtCount = -1;
        internal TextStudioRangeEntry this[int index] => _entries[index];

        /// <param name="names">The tokenizer's tag name per token, or null to read each name from the source.</param>
        internal void Build(string source, TextToken[] tokens, int count, string[] names = null)
        {
            if (_entries.Length < count) Array.Resize(ref _entries, TextStudioBufferCapacity.ForCount(count));
            _open.Clear();
            _appearanceCount = 0;
            Interpret(source, tokens, 0, count, names);
        }

        /// <summary>
        /// Interprets the tokens from <paramref name="start"/> on after a build of the same tokens before it, when every token
        /// the last build interpreted from there on is text, so its open ranges and appearance count are those at
        /// <paramref name="start"/>. Returns false, changing nothing, otherwise.
        /// </summary>
        internal bool BuildAppended(string source, TextToken[] tokens, int start, int count, string[] names)
        {
            if (_builtCount < start || start > count)
                return false;
            for (int i = start; i < _builtCount; i++)
            {
                if (_entries[i].OpeningTokenIndex >= 0)
                    return false;
            }

            if (_entries.Length < count) Array.Resize(ref _entries, TextStudioBufferCapacity.ForCount(count));
            Interpret(source, tokens, start, count, names);
            return true;
        }

        void Interpret(string source, TextToken[] tokens, int start, int count, string[] names)
        {
            int appearanceCount = _appearanceCount;
            for (int i = start; i < count; i++)
            {
                TextToken token = tokens[i];
                TextStudioRangeEntry entry = new TextStudioRangeEntry { OpeningTokenIndex = -1, AppearanceIndex = -1, ParentAppearanceIndex = -1 };
                bool isClose = token.kind == TokenKind.StyleClose || token.kind == TokenKind.ExtensionClose;
                bool isOpen = token.kind == TokenKind.StyleOpen || token.kind == TokenKind.ExtensionTag;
                if (!isOpen && !isClose) { _entries[i] = entry; continue; }
                string name = names != null ? names[i] : token.GetNameSpan(source).ToString().Trim();
                if (isClose)
                {
                    string closeKey = CanonicalCloseName(name);
                    for (int j = _open.Count - 1; j >= 0; j--)
                    {
                        int opening = _open[j];
                        if (name.Length > 0 && !string.Equals(_entries[opening].Name, closeKey, StringComparison.OrdinalIgnoreCase)) continue;
                        entry = _entries[opening];
                        entry.OpeningTokenIndex = opening;
                        _open.RemoveAt(j);
                        break;
                    }
                    _entries[i] = entry;
                    continue;
                }
                entry.Name = CanonicalCloseName(name);
                entry.OpeningTokenIndex = i;
                entry.IsRange = !token.selfClosing;
                if (token.kind == TokenKind.ExtensionTag)
                {
                    entry.Owner = TextStudioRangeOwner.Extension;
                    entry.IsRange &= TextStudioTagUtility.TryGetExtensionTag(name, token.parameterStart > 0, out TextStudioExtensionTagDefinition extension) && extension.Shape == TextStudioExtensionTagShape.Range;
                }
                else
                {
                    bool candidate = name.StartsWith("$", StringComparison.Ordinal) || string.Equals(name, "style", StringComparison.OrdinalIgnoreCase);
                    if (candidate) entry.Parameters = TextStudioAppearanceTagSyntax.GetParameters(source, in token);
                    if (candidate && TextStudioAppearanceTagSyntax.TryClassify(name,
                            TextStudioAppearanceTagSyntax.UsesDirectAssignment(source, in token), in entry.Parameters,
                            out entry.AppearanceId, out entry.IsAdHoc))
                    {
                        entry.Owner = TextStudioRangeOwner.Appearance;
                        entry.AppearanceIndex = appearanceCount++;
                        for (int j = _open.Count - 1; j >= 0; j--)
                        {
                            TextStudioRangeEntry parent = _entries[_open[j]];
                            if (parent.Owner != TextStudioRangeOwner.Appearance) continue;
                            entry.ParentAppearanceIndex = parent.AppearanceIndex;
                            break;
                        }
                    }
                    else if (TextStudioPipeline.IsNativeRichTextInvocation(source, in token, name))
                    {
                        entry.Owner = TextStudioRangeOwner.Native;
                        entry.IsRange &= !BuiltInStyles.IsSelfContainedNativeRichTextFormatting(name, token.parameterStart > 0);
                    }
                    else entry.Owner = TextStudioRangeOwner.Other;
                }
                _entries[i] = entry;
                if (entry.IsRange) _open.Add(i);
            }

            _appearanceCount = appearanceCount;
            _builtCount = count;
        }

        internal static string CanonicalCloseName(string name)
        {
            if (BuiltInStyles.IsNativeColorShorthand(name)) return "color";
            if (TextStudioTagUtility.TryGetExtensionTag(name, false, out TextStudioExtensionTagDefinition extension)) return extension.Id;
            if (TextStudioPhaseTagUtility.TryGetPhase(name, out AnimationPhase phase)) return TextStudioPhaseTagUtility.GetTagName(phase);
            if (BuiltInCharacterSubstitutions.TryGetCanonicalTagId(name, out string substitution)) return substitution;
            return name;
        }
    }
}
