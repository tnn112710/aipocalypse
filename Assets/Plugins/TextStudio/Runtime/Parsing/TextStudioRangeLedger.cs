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
        internal TextStudioRangeEntry this[int index] => _entries[index];

        internal void Build(string source, TextToken[] tokens, int count)
        {
            if (_entries.Length < count) Array.Resize(ref _entries, TextStudioBufferCapacity.ForCount(count));
            _open.Clear();
            int appearanceCount = 0;
            for (int i = 0; i < count; i++)
            {
                TextToken token = tokens[i];
                TextStudioRangeEntry entry = new TextStudioRangeEntry { OpeningTokenIndex = -1, AppearanceIndex = -1, ParentAppearanceIndex = -1 };
                bool isClose = token.kind == TokenKind.StyleClose || token.kind == TokenKind.ExtensionClose;
                bool isOpen = token.kind == TokenKind.StyleOpen || token.kind == TokenKind.ExtensionTag;
                if (!isOpen && !isClose) { _entries[i] = entry; continue; }
                string name = token.GetNameSpan(source).ToString().Trim();
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
