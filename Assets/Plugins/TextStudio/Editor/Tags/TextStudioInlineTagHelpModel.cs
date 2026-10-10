using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioInlineTagFragment
    {
        public TextStudioInlineTagFragment(
            string sourceText,
            string prefix,
            bool hasParameter,
            bool usesDirectAssignment,
            bool isClosingTag,
            bool exactOnly,
            int tagStart,
            int tagNameStart,
            int tagNameEnd,
            int tagEnd,
            bool hasTerminatingBracket,
            bool isAppearance = false,
            bool isAppearanceReference = false,
            char referenceQuote = '\0')
        {
            IsValid = true;
            SourceText = sourceText ?? string.Empty;
            Prefix = prefix ?? string.Empty;
            HasParameter = hasParameter;
            UsesDirectAssignment = usesDirectAssignment;
            IsClosingTag = isClosingTag;
            ExactOnly = exactOnly;
            TagStart = tagStart;
            TagNameStart = tagNameStart;
            TagNameEnd = tagNameEnd;
            TagEnd = tagEnd;
            HasTerminatingBracket = hasTerminatingBracket;
            IsAppearance = isAppearance;
            IsAppearanceReference = isAppearanceReference;
            ReferenceQuote = referenceQuote;
        }

        public bool IsValid { get; }
        public string SourceText { get; }
        public string Prefix { get; }
        public bool HasParameter { get; }
        public bool UsesDirectAssignment { get; }
        public bool IsClosingTag { get; }
        public bool ExactOnly { get; }
        public int TagStart { get; }
        public int TagNameStart { get; }
        public int TagNameEnd { get; }
        public int TagEnd { get; }
        public bool HasTerminatingBracket { get; }
        public bool IsAppearance { get; }
        public bool IsAppearanceReference { get; }
        public char ReferenceQuote { get; }
    }

    internal readonly struct TextStudioInlineTagHelpItem
    {
        readonly TextStudioTagBrowserItem _browserItem;

        public TextStudioInlineTagHelpItem(TextStudioTagBrowserItem browserItem, bool isExactMatch, string matchedName)
        {
            _browserItem = browserItem;
            IsExactMatch = isExactMatch;
            MatchedName = matchedName ?? string.Empty;
        }

        public TextStudioTagBrowserItem BrowserItem => _browserItem;
        public TextStudioTagDefinition Definition => _browserItem.Definition;
        public string GroupDisplayName => _browserItem.GroupDisplayName;
        public string Example => _browserItem.Example;
        public string ParameterSummary => _browserItem.ParameterSummary;
        public TextStudioTagCategory Category => _browserItem.Category;
        public bool IsExactMatch { get; }
        public string MatchedName { get; }
    }

#if UNITY_6000_7_OR_NEWER
    // Empty is an immutable value sentinel and does not carry play-session state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal readonly partial struct TextStudioInlineTagHelpState
    {
        public static readonly TextStudioInlineTagHelpState Empty =
            new TextStudioInlineTagHelpState(
                default,
                0,
                Array.Empty<TextStudioInlineTagHelpItem>());

        readonly TextStudioInlineTagHelpItem[] _items;

        public TextStudioInlineTagHelpState(
            TextStudioInlineTagFragment fragment,
            int cursorIndex,
            TextStudioInlineTagHelpItem[] items)
        {
            IsActiveTagFragment = fragment.IsValid;
            SourceText = fragment.SourceText ?? string.Empty;
            Prefix = fragment.Prefix ?? string.Empty;
            HasParameter = fragment.HasParameter;
            IsClosingTag = fragment.IsClosingTag;
            TagStart = fragment.TagStart;
            TagNameStart = fragment.TagNameStart;
            TagNameEnd = fragment.TagNameEnd;
            TagEnd = fragment.TagEnd;
            HasTerminatingBracket = fragment.HasTerminatingBracket;
            IsAppearance = fragment.IsAppearance;
            IsAppearanceReference = fragment.IsAppearanceReference;
            ReferenceQuote = fragment.ReferenceQuote;
            CursorIndex = cursorIndex;
            _items = items ?? Array.Empty<TextStudioInlineTagHelpItem>();
        }

        public bool IsActiveTagFragment { get; }
        public bool Visible => _items != null && _items.Length > 0;
        public string SourceText { get; }
        public string Prefix { get; }
        public bool HasParameter { get; }
        public bool IsClosingTag { get; }
        public int TagStart { get; }
        public int TagNameStart { get; }
        public int TagNameEnd { get; }
        public int TagEnd { get; }
        public bool HasTerminatingBracket { get; }
        public bool IsAppearance { get; }
        public bool IsAppearanceReference { get; }
        public char ReferenceQuote { get; }
        public int CursorIndex { get; }
        public IReadOnlyList<TextStudioInlineTagHelpItem> Items => _items ?? Array.Empty<TextStudioInlineTagHelpItem>();
        public TextStudioInlineTagHelpItem PrimaryItem => Visible ? _items[0] : default;
    }

#if UNITY_6000_7_OR_NEWER
    // Native syntax documentation is immutable and does not carry play-session state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioInlineTagHelpModel
    {
        static readonly TextStudioTagDefinition NativeColorHelp = new TextStudioTagDefinition("#RRGGBB", TextStudioTagKind.NativeRichText, TextStudioTagCategories.Styling, "Native color", "Use 3, 4, 6 or 8 hexadecimal digits. Four and eight digits include alpha. Close with </color>. Native colors do not look up an appearance style.", examples: new[] { "<#FFAA00>text</color>" });


        public static TextStudioInlineTagHelpState Build(
            string source,
            int cursorIndex,
            TextStudioTagAnalysisContext context)
        {
            source = source ?? string.Empty;
            context ??= TextStudioTagInspectorContext.Build(null, source);
            int cursor = Math.Max(0, Math.Min(cursorIndex, source.Length));
            if (TextStudioAppearanceAuthoringMetadata.IsLiteral(context, source, cursor)) return TextStudioInlineTagHelpState.Empty;
            if (!TryReadTagFragment(source, cursor, out TextStudioInlineTagFragment fragment))
                return TextStudioInlineTagHelpState.Empty;

            if (fragment.Prefix.StartsWith("#", StringComparison.Ordinal))
            {
                TextStudioTagBrowserItem color = new TextStudioTagBrowserItem(NativeColorHelp, TextStudioTagCategories.Styling, "Native color", "Color", "Native color", "<#FFAA00>text</color>", string.Empty, TextStudioTagCategory.Formatting);
                return new TextStudioInlineTagHelpState(fragment, cursor, new[] { new TextStudioInlineTagHelpItem(color, true, fragment.Prefix) });
            }
            if (fragment.IsClosingTag)
            {
                TextStudioTagAnalysisContext closingContext = context ?? new TextStudioTagAnalysisContext();
                IReadOnlyList<TextStudioTagBrowserItem> browser = TextStudioTagBrowserModel.Build(closingContext).Items;
                IReadOnlyList<TextStudioTagBrowserItem> appearances = TextStudioAppearanceAuthoringProviders.AtCaret(closingContext, cursor);
                List<TextStudioInlineTagHelpItem> closers = new List<TextStudioInlineTagHelpItem>();
                HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                IReadOnlyList<TextStudioTagDiagnostic> openTags = TextStudioAppearanceAuthoringMetadata.OpenTags(closingContext, source, fragment.TagStart);
                foreach (TextStudioTagDiagnostic open in openTags)
                {
                    if (!open.Name.StartsWith(fragment.Prefix, StringComparison.OrdinalIgnoreCase) || !added.Add(open.Name)) continue;
                    IReadOnlyList<TextStudioTagBrowserItem> choices = open.Name.StartsWith("$", StringComparison.Ordinal) ? appearances : browser;
                    foreach (TextStudioTagBrowserItem candidate in choices)
                    {
                        if (candidate.Definition.Kind != open.Kind || !string.Equals(candidate.Definition.Id, open.Name, StringComparison.OrdinalIgnoreCase)) continue;
                        closers.Add(new TextStudioInlineTagHelpItem(candidate, string.Equals(open.Name, fragment.Prefix, StringComparison.OrdinalIgnoreCase), open.Name));
                        break;
                    }
                }
                if (openTags.Count > 0) return new TextStudioInlineTagHelpState(fragment, cursor, closers.ToArray());
            }
            if (fragment.IsAppearance)
            {
                List<TextStudioInlineTagHelpItem> appearances = new List<TextStudioInlineTagHelpItem>();
                if (!fragment.IsAppearanceReference && TextStudioAppearanceTagSyntax.HasLegacyNameCollision(fragment.Prefix))
                {
                    foreach (TextStudioTagBrowserItem legacy in TextStudioTagBrowserModel.Build(context).Items)
                    {
                        if (legacy.Definition.Kind != TextStudioTagKind.Appearance && string.Equals(legacy.Definition.Id, fragment.Prefix, StringComparison.OrdinalIgnoreCase))
                        { appearances.Add(new TextStudioInlineTagHelpItem(legacy, true, legacy.Definition.Id)); break; }
                    }
                }
                IReadOnlyList<TextStudioTagBrowserItem> available = TextStudioAppearanceAuthoringProviders.AtCaret(context, cursor);
                for (int start = 0; start < available.Count;)
                {
                    int end = start + 1;
                    while (end < available.Count && available[end].ScopeOrder == available[start].ScopeOrder) end++;
                    for (int i = start; i < end; i++)
                    {
                        TextStudioTagBrowserItem item = available[i];
                        if (string.Equals(item.Definition.Id, fragment.Prefix, StringComparison.OrdinalIgnoreCase)) appearances.Add(new TextStudioInlineTagHelpItem(item, true, item.Definition.Id));
                    }
                    for (int i = start; i < end; i++)
                    {
                        TextStudioTagBrowserItem item = available[i];
                        if (string.Equals(item.Definition.Id, fragment.Prefix, StringComparison.OrdinalIgnoreCase) || !item.Definition.Id.StartsWith(fragment.Prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        appearances.Add(new TextStudioInlineTagHelpItem(item, false, item.Definition.Id));
                    }
                    start = end;
                }
                return new TextStudioInlineTagHelpState(fragment, cursor, appearances.ToArray());
            }
            string prefix = fragment.Prefix;
            bool hasParameter = fragment.HasParameter;
            bool usesDirectAssignment = fragment.UsesDirectAssignment;
            bool isClosingTag = fragment.IsClosingTag;
            bool exactOnly = fragment.ExactOnly;
            if (prefix.Length == 0)
                return new TextStudioInlineTagHelpState(
                    fragment,
                    cursor,
                    Array.Empty<TextStudioInlineTagHelpItem>());

            TextStudioTagAnalysisContext activeContext = context;
            TextStudioTagCatalog catalog = activeContext != null && activeContext.Catalog != null
                ? activeContext.Catalog
                : TextStudioTagCatalog.BuildDefault();
            TextStudioTagBrowserModel browserModel = TextStudioTagBrowserModel.Build(activeContext);
            IReadOnlyList<TextStudioTagBrowserItem> browserItems = browserModel.Items;
            List<TextStudioInlineTagHelpItem> matches = new List<TextStudioInlineTagHelpItem>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (isClosingTag)
            {
                TryAddExactClosingMatch(browserItems, prefix, matches, seenIds);
                if (exactOnly && matches.Count > 0)
                {
                    return new TextStudioInlineTagHelpState(
                        fragment,
                        cursor,
                        matches.ToArray());
                }
            }
            else if (TryResolveSyntaxDefinition(
                         catalog,
                         prefix,
                         hasParameter,
                         usesDirectAssignment,
                         out TextStudioTagDefinition syntaxDefinition) &&
                     TryFindBrowserItem(
                         browserItems,
                         syntaxDefinition,
                         out TextStudioTagBrowserItem syntaxItem))
            {
                matches.Add(new TextStudioInlineTagHelpItem(syntaxItem, true, syntaxDefinition.Id));
                seenIds.Add(syntaxDefinition.Id);
                if (exactOnly)
                {
                    return new TextStudioInlineTagHelpState(
                        fragment,
                        cursor,
                        matches.ToArray());
                }
            }
            else if (TryFindCanonicalExactMatch(
                         browserItems,
                         prefix,
                         hasParameter,
                         out TextStudioTagBrowserItem exactItem))
            {
                matches.Add(new TextStudioInlineTagHelpItem(exactItem, true, exactItem.Definition.Id));
                seenIds.Add(exactItem.Definition.Id);
                if (exactOnly)
                {
                    return new TextStudioInlineTagHelpState(
                        fragment,
                        cursor,
                        matches.ToArray());
                }
            }
            else if (catalog.TryGetDefinition(prefix, hasParameter, out TextStudioTagDefinition exactDefinition))
            {
                if (TryFindBrowserItem(browserModel.Items, exactDefinition, out exactItem))
                {
                    matches.Add(new TextStudioInlineTagHelpItem(exactItem, true, exactDefinition.Id));
                    seenIds.Add(exactDefinition.Id);
                    if (exactOnly)
                    {
                        return new TextStudioInlineTagHelpState(
                            fragment,
                            cursor,
                            matches.ToArray());
                    }
                }
            }

            for (int i = 0; i < browserItems.Count; i++)
            {
                TextStudioTagBrowserItem item = browserItems[i];
                TextStudioTagDefinition definition = item.Definition;
                if (definition == null || string.IsNullOrEmpty(definition.Id) || seenIds.Contains(definition.Id))
                    continue;
                if (isClosingTag && definition.Shape != TextStudioTagShape.Range)
                    continue;

                if (!TryMatchPrefix(
                        definition,
                        prefix,
                        out bool isExactMatch,
                        out string matchedName,
                        out bool matchedCanonicalId))
                    continue;
                if (!matchedCanonicalId &&
                    HasCanonicalDefinition(browserItems, matchedName, hasParameter, isClosingTag))
                    continue;

                matches.Add(new TextStudioInlineTagHelpItem(item, isExactMatch, matchedName));
                seenIds.Add(definition.Id);
            }

            return new TextStudioInlineTagHelpState(
                fragment,
                cursor,
                matches.ToArray());
        }

        static bool TryReadTagFragment(
            string source,
            int cursor,
            out TextStudioInlineTagFragment fragment)
        {
            fragment = default;
            int tagStart = -1;

            for (int i = cursor - 1; i >= 0; i--)
            {
                char c = source[i];
                if (c == '>' || c == '\r' || c == '\n')
                    return false;
                if (c == '<')
                {
                    tagStart = i;
                    break;
                }
            }

            if (tagStart < 0 || TextStudioTagExpansionUtility.IsEscaped(source, tagStart))
                return false;
            if (TryReadAppearanceReference(source, cursor, tagStart, out fragment)) return true;

            int fragmentEnd = source.Length;
            bool hasTerminatingBracket = false;
            for (int i = tagStart + 1; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '>')
                {
                    fragmentEnd = i;
                    hasTerminatingBracket = true;
                    break;
                }

                if (c == '<' || c == '\r' || c == '\n')
                {
                    fragmentEnd = cursor;
                    break;
                }
            }
            if (cursor > fragmentEnd)
                return false;

            int nameStart = tagStart + 1;
            while (nameStart < fragmentEnd && char.IsWhiteSpace(source[nameStart]))
                nameStart++;

            if (nameStart >= fragmentEnd || cursor < nameStart)
                return false;

            char first = source[nameStart];
            bool isClosingTag = false;
            if (first == '/')
            {
                isClosingTag = true;
                nameStart++;
                while (nameStart < fragmentEnd && char.IsWhiteSpace(source[nameStart]))
                    nameStart++;
                if (cursor < nameStart) return false;
                if (nameStart >= fragmentEnd)
                {
                    fragment = new TextStudioInlineTagFragment(source, string.Empty, false, false, true, false, tagStart, nameStart, nameStart, fragmentEnd, hasTerminatingBracket);
                    return true;
                }
                first = source[nameStart];
            }

            if (first == '!' || first == '?')
                return false;

            bool appearance = first == '$';
            int nameEnd = nameStart + (appearance || first == '#' ? 1 : 0);
            while (nameEnd < fragmentEnd && IsTagNameCharacter(source[nameEnd]))
                nameEnd++;

            if (nameEnd == nameStart)
                return false;

            int parameterStart = nameEnd;
            while (parameterStart < fragmentEnd && char.IsWhiteSpace(source[parameterStart]))
                parameterStart++;
            bool usesDirectAssignment = !isClosingTag &&
                                        parameterStart < fragmentEnd &&
                                        source[parameterStart] == '=';

            bool hasParameter = false;
            for (int i = nameEnd; !isClosingTag && i < fragmentEnd; i++)
            {
                char c = source[i];
                if (c == '=' || (!char.IsWhiteSpace(c) && c != '/'))
                {
                    hasParameter = true;
                    break;
                }
            }

            string prefix = source.Substring(nameStart, nameEnd - nameStart);
            bool exactOnly = cursor < nameEnd || hasTerminatingBracket;
            fragment = new TextStudioInlineTagFragment(
                source,
                prefix,
                hasParameter,
                usesDirectAssignment,
                isClosingTag,
                exactOnly,
                tagStart,
                nameStart,
                nameEnd,
                fragmentEnd,
                hasTerminatingBracket,
                appearance);
            return true;
        }

        internal static bool TryReadAppearanceReference(string source, int cursor, int tagStart, out TextStudioInlineTagFragment fragment)
        {
            fragment = default;
            if (TextStudioAppearanceTagSyntax.HasLegacyNameCollision("style")) return false;
            int name = tagStart + 1;
            if (name + 5 > source.Length || !string.Equals(source.Substring(name, 5), "style", StringComparison.OrdinalIgnoreCase)) return false;
            int position = name + 5;
            if (position >= source.Length || !char.IsWhiteSpace(source[position])) return false;
            while (position < source.Length && position <= cursor)
            {
                while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
                int keyStart = position;
                while (position < source.Length && IsTagNameCharacter(source[position])) position++;
                if (position == keyStart) return false;
                string key = source.Substring(keyStart, position - keyStart);
                while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
                if (position >= source.Length || source[position++] != '=') return false;
                while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
                char quote = position < source.Length && (source[position] == '\"' || source[position] == '\'') ? source[position++] : '\0';
                int start = position;
                while (position < source.Length && source[position] != '>' && source[position] != '<' && (quote != '\0' ? source[position] != quote : !char.IsWhiteSpace(source[position]))) position++;
                int end = position;
                if (string.Equals(key, "ref", StringComparison.OrdinalIgnoreCase) && cursor >= start && cursor <= end)
                {
                    int tagEnd = end;
                    char remainingQuote = quote;
                    while (tagEnd < source.Length)
                    {
                        char next = source[tagEnd];
                        if (next == '<' || next == '\r' || next == '\n' || next == '>' && remainingQuote == '\0') break;
                        if (next == remainingQuote) remainingQuote = '\0';
                        else if (remainingQuote == '\0' && (next == '"' || next == '\'')) remainingQuote = next;
                        // A missing ref quote must not consume the bracket or a following tag.
                        if (tagEnd == end && next == '>') break;
                        tagEnd++;
                    }
                    bool bracket = tagEnd < source.Length && source[tagEnd] == '>';
                    fragment = new TextStudioInlineTagFragment(source, "$" + source.Substring(start, Math.Min(cursor, end) - start), true, false, false, false, tagStart, start, end, tagEnd, bracket, true, true, quote);
                    return true;
                }
                if (quote != '\0' && position < source.Length && source[position] == quote) position++;
            }
            return false;
        }

        static bool TryResolveSyntaxDefinition(
            TextStudioTagCatalog catalog,
            string prefix,
            bool hasParameter,
            bool usesDirectAssignment,
            out TextStudioTagDefinition definition)
        {
            TextStudioTagKind kind;
            if (BuiltInStyles.IsNativeRichTextFormattingInvocation(
                    prefix,
                    hasParameter,
                    usesDirectAssignment))
            {
                kind = TextStudioTagKind.NativeRichText;
            }
            else if (TextStudioPhaseTagUtility.TryResolveEffectId(prefix, out _))
            {
                kind = TextStudioTagKind.TextStudioEffect;
            }
            else
            {
                definition = null;
                return false;
            }

            return catalog.TryGetDefinition(prefix, hasParameter, kind, out definition);
        }

        static void TryAddExactClosingMatch(
            IReadOnlyList<TextStudioTagBrowserItem> items,
            string prefix,
            List<TextStudioInlineTagHelpItem> matches,
            HashSet<string> seenIds)
        {
            for (int i = 0; i < items.Count; i++)
            {
                TextStudioTagBrowserItem item = items[i];
                TextStudioTagDefinition definition = item.Definition;
                if (definition == null ||
                    definition.Shape != TextStudioTagShape.Range ||
                    !string.Equals(definition.Id, prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                matches.Add(new TextStudioInlineTagHelpItem(item, true, definition.Id));
                seenIds.Add(definition.Id);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                TextStudioTagBrowserItem item = items[i];
                TextStudioTagDefinition definition = item.Definition;
                if (definition == null || definition.Shape != TextStudioTagShape.Range)
                    continue;
                if (!TryMatchPrefix(
                        definition,
                        prefix,
                        out bool exact,
                        out string matchedName,
                        out bool matchedCanonicalId) ||
                    !exact ||
                    matchedCanonicalId)
                    continue;

                matches.Add(new TextStudioInlineTagHelpItem(item, true, matchedName));
                seenIds.Add(definition.Id);
                return;
            }
        }

        static bool TryFindCanonicalExactMatch(
            IReadOnlyList<TextStudioTagBrowserItem> items,
            string prefix,
            bool hasParameter,
            out TextStudioTagBrowserItem item)
        {
            if (TryFindCanonicalExactMatch(items, prefix, hasParameter, true, out item))
                return true;

            return TryFindCanonicalExactMatch(items, prefix, hasParameter, false, out item);
        }

        static bool TryFindCanonicalExactMatch(
            IReadOnlyList<TextStudioTagBrowserItem> items,
            string prefix,
            bool hasParameter,
            bool requireSpecificParameterState,
            out TextStudioTagBrowserItem item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                TextStudioTagDefinition definition = items[i].Definition;
                if (definition == null ||
                    !string.Equals(definition.Id, prefix, StringComparison.OrdinalIgnoreCase) ||
                    !definition.MatchesParameterState(hasParameter))
                    continue;

                bool hasSpecificParameterState =
                    definition.ParameterRequirement != TextStudioTagParameterRequirement.Any;
                if (hasSpecificParameterState != requireSpecificParameterState)
                    continue;

                item = items[i];
                return true;
            }

            item = default;
            return false;
        }

        static bool HasCanonicalDefinition(
            IReadOnlyList<TextStudioTagBrowserItem> items,
            string tagName,
            bool hasParameter,
            bool isClosingTag)
        {
            for (int i = 0; i < items.Count; i++)
            {
                TextStudioTagDefinition definition = items[i].Definition;
                if (definition == null ||
                    !string.Equals(definition.Id, tagName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (isClosingTag)
                {
                    if (definition.Shape == TextStudioTagShape.Range)
                        return true;
                }
                else if (definition.MatchesParameterState(hasParameter))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsTagNameCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == ':';
        }

        static bool TryFindBrowserItem(
            IReadOnlyList<TextStudioTagBrowserItem> items,
            TextStudioTagDefinition definition,
            out TextStudioTagBrowserItem item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i].Definition, definition))
                {
                    item = items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        static bool TryMatchPrefix(
            TextStudioTagDefinition definition,
            string prefix,
            out bool isExactMatch,
            out string matchedName,
            out bool matchedCanonicalId)
        {
            isExactMatch = false;
            matchedName = string.Empty;
            matchedCanonicalId = false;
            if (MatchesName(definition.Id, prefix, out isExactMatch))
            {
                matchedName = definition.Id;
                matchedCanonicalId = true;
                return true;
            }

            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = 0; i < aliases.Count; i++)
            {
                if (MatchesName(aliases[i], prefix, out isExactMatch))
                {
                    matchedName = aliases[i];
                    return true;
                }
            }

            return false;
        }

        static bool MatchesName(string candidate, string prefix, out bool isExactMatch)
        {
            isExactMatch = false;
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(prefix))
                return false;

            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            isExactMatch = string.Equals(candidate, prefix, StringComparison.OrdinalIgnoreCase);
            return true;
        }
    }

    internal static class TextStudioInlineTagEditModel
    {
        readonly struct SourceSpan
        {
            public SourceSpan(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start { get; }
            public int End { get; }
        }

        public static bool CanCompleteTag(
            string source,
            TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition)
        {
            if (!TryGetCompletionReplacement(source, state, definition, out string replacement, out bool appendQuote)) return false;
            return !state.HasTerminatingBracket || appendQuote ||
                state.TagNameEnd - state.TagNameStart != replacement.Length ||
                string.Compare(source, state.TagNameStart, replacement, 0, replacement.Length, StringComparison.Ordinal) != 0;
        }

        static bool TryGetCompletionReplacement(string source, TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition, out string replacement, out bool appendQuote)
        {
            replacement = null;
            appendQuote = false;
            if (!IsCurrentState(source ?? string.Empty, state) || definition == null ||
                string.IsNullOrEmpty(definition.Id) || state.Prefix.StartsWith("#", StringComparison.Ordinal)) return false;
            replacement = definition.Id;
            if (!state.IsAppearance || definition.Kind != TextStudioTagKind.Appearance) return true;
            string id = definition.Id.TrimStart('$');
            string opening = TextStudioAppearanceTagSyntax.GetPreferredOpeningTag(id);
            if (opening == null) return false;
            if (state.IsAppearanceReference) replacement = id;
            else if (!state.IsClosingTag && !opening.StartsWith("<$", StringComparison.Ordinal)) replacement = opening.Substring(1, opening.Length - 2);
            appendQuote = state.IsAppearanceReference && state.ReferenceQuote != '\0' &&
                (state.TagNameEnd >= source.Length || source[state.TagNameEnd] != state.ReferenceQuote);
            return true;
        }

        public static bool TryCompleteTag(
            string source,
            TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition,
            out TextStudioTagSourceEditor.EditResult edit)
        {
            edit = default;
            source = source ?? string.Empty;
            if (!TryGetCompletionReplacement(source, state, definition, out string replacement, out bool appendQuote)) return false;

            if (state.IsAppearance && definition.Kind == TextStudioTagKind.Appearance)
                return TryCompleteAppearance(source, state, definition, replacement, appendQuote, out edit);

            string next = TextStudioTagSourceEditor.ReplaceRange(
                source,
                state.TagNameStart,
                state.TagNameEnd,
                definition.Id);
            int lengthDelta = definition.Id.Length - (state.TagNameEnd - state.TagNameStart);
            int tagEnd = state.TagEnd + lengthDelta;
            if (!state.HasTerminatingBracket)
                next = TextStudioTagSourceEditor.ReplaceRange(next, tagEnd, tagEnd, ">");

            if (string.Equals(next, source, StringComparison.Ordinal))
                return false;

            int caret = tagEnd + 1;
            edit = new TextStudioTagSourceEditor.EditResult(next, caret, caret);
            return true;
        }

        static bool TryCompleteAppearance(string source, TextStudioInlineTagHelpState state, TextStudioTagDefinition definition,
            string replacement, bool appendQuote, out TextStudioTagSourceEditor.EditResult edit)
        {
            edit = default;
            string id = definition.Id.TrimStart('$');
            string next = TextStudioTagSourceEditor.ReplaceRange(source, state.TagNameStart, state.TagNameEnd, replacement);
            int delta = replacement.Length - (state.TagNameEnd - state.TagNameStart);
            if (appendQuote)
            {
                int quotePosition = state.TagNameEnd + delta;
                next = TextStudioTagSourceEditor.ReplaceRange(next, quotePosition, quotePosition, state.ReferenceQuote.ToString());
                delta++;
            }
            int end = state.TagEnd + delta;
            if (!state.HasTerminatingBracket)
            {
                string suffix = ">";
                int caret = end + suffix.Length;
                if (!state.IsClosingTag) suffix += state.IsAppearanceReference || !replacement.StartsWith("$", StringComparison.Ordinal) ? "</style>" : "</$" + id + ">";
                next = TextStudioTagSourceEditor.ReplaceRange(next, end, end, suffix);
                edit = new TextStudioTagSourceEditor.EditResult(next, caret, caret);
            }
            else
            {
                if (next == source) return false;
                int caret = state.IsAppearanceReference ? state.TagNameStart + replacement.Length : end + 1;
                edit = new TextStudioTagSourceEditor.EditResult(next, caret, caret);
            }
            return true;
        }

        public static bool TryInsertParameter(
            string source,
            TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter,
            out TextStudioTagSourceEditor.EditResult edit)
        {
            edit = default;
            source = source ?? string.Empty;
            if (!IsCurrentState(source, state) ||
                state.IsClosingTag ||
                definition == null ||
                parameter == null ||
                !ContainsParameter(definition, parameter))
            {
                return false;
            }

            int parameterEnd = FindParameterEnd(source, state);
            if (TryFindExistingParameter(
                    source,
                    state.TagNameEnd,
                    parameterEnd,
                    parameter,
                    out SourceSpan existing))
            {
                edit = new TextStudioTagSourceEditor.EditResult(
                    source,
                    existing.Start,
                    existing.End);
                return true;
            }

            string suggestedValue = ResolveSuggestedValue(definition, parameter);
            if (!TryFormatValue(
                    suggestedValue,
                    out string formattedValue,
                    out int formattedSelectionStart,
                    out int formattedSelectionEnd))
            {
                return false;
            }

            bool directAssignment =
                definition.Kind == TextStudioTagKind.NativeRichText &&
                parameter.PositionalIndex == 0;
            int insertionIndex;
            string insertion;
            int valueOffset;
            if (directAssignment)
            {
                insertionIndex = state.TagNameEnd;
                insertion = "=" + formattedValue;
                valueOffset = 1;
            }
            else
            {
                insertionIndex = FindNamedParameterInsertionIndex(source, state);
                string separator = insertionIndex > state.TagNameEnd &&
                                   char.IsWhiteSpace(source[insertionIndex - 1])
                    ? string.Empty
                    : " ";
                insertion = separator + parameter.Name + "=" + formattedValue;
                valueOffset = separator.Length + parameter.Name.Length + 1;
            }

            string next = TextStudioTagSourceEditor.ReplaceRange(
                source,
                insertionIndex,
                insertionIndex,
                insertion);
            if (!state.HasTerminatingBracket)
            {
                int tagEnd = state.TagEnd + insertion.Length;
                next = TextStudioTagSourceEditor.ReplaceRange(next, tagEnd, tagEnd, ">");
            }

            int selectionStart = insertionIndex + valueOffset + formattedSelectionStart;
            int selectionEnd = insertionIndex + valueOffset + formattedSelectionEnd;
            edit = new TextStudioTagSourceEditor.EditResult(next, selectionStart, selectionEnd);
            return true;
        }

        public static string ResolveSuggestedValue(
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter)
        {
            if (parameter == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(parameter.DefaultValue))
                return parameter.DefaultValue;
            if (!string.IsNullOrEmpty(parameter.ExampleValue))
                return parameter.ExampleValue;

            string example = ResolveDefinitionExampleValue(definition, parameter);
            if (!string.IsNullOrEmpty(example))
                return example;
            if (parameter.AllowedValues.Count > 0)
                return parameter.AllowedValues[0];

            switch (parameter.Kind)
            {
                case TextStudioTagParameterKind.Float:
                case TextStudioTagParameterKind.Integer:
                    return "1";
                case TextStudioTagParameterKind.Boolean:
                    return "true";
                case TextStudioTagParameterKind.Color:
                    return "#FFFFFF";
                case TextStudioTagParameterKind.Vector2:
                    return "0,0";
                case TextStudioTagParameterKind.Vector3:
                    return "0,0,0";
                case TextStudioTagParameterKind.Pivot:
                    return "center";
                case TextStudioTagParameterKind.RouteId:
                    return "eventRoute";
                case TextStudioTagParameterKind.AssetReference:
                    return "assetName";
                case TextStudioTagParameterKind.Enum:
                case TextStudioTagParameterKind.String:
                default:
                    return "value";
            }
        }

        static bool IsCurrentState(string source, TextStudioInlineTagHelpState state)
        {
            if (!state.IsActiveTagFragment ||
                !string.Equals(source, state.SourceText, StringComparison.Ordinal) ||
                state.TagStart < 0 ||
                state.TagStart >= source.Length ||
                source[state.TagStart] != '<' ||
                state.TagNameStart <= state.TagStart ||
                state.TagNameEnd < state.TagNameStart ||
                state.TagNameEnd == state.TagNameStart && !state.IsAppearanceReference ||
                state.TagNameEnd > source.Length ||
                state.TagEnd < state.TagNameEnd ||
                state.TagEnd > source.Length)
            {
                return false;
            }

            if (state.HasTerminatingBracket)
            {
                if (state.TagEnd >= source.Length || source[state.TagEnd] != '>')
                    return false;
            }
            else if (state.TagEnd != source.Length && state.TagEnd != state.CursorIndex &&
                     !(state.IsAppearanceReference &&
                       (source[state.TagEnd] == '<' || source[state.TagEnd] == '\r' || source[state.TagEnd] == '\n')))
            {
                return false;
            }

            // Reference help adds '$' for matching and only includes the value before the caret.
            // The authored range still covers the complete value so accepting replaces it once.
            int prefixStart = state.IsAppearanceReference ? 1 : 0;
            int nameLength = (state.IsAppearanceReference ? Math.Min(state.CursorIndex, state.TagNameEnd) : state.TagNameEnd) - state.TagNameStart;
            if (nameLength < 0 || state.Prefix.Length != nameLength + prefixStart ||
                prefixStart != 0 && state.Prefix[0] != '$') return false;
            return string.Compare(source, state.TagNameStart, state.Prefix, prefixStart, nameLength, StringComparison.Ordinal) == 0;
        }

        static bool ContainsParameter(
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter)
        {
            IReadOnlyList<TextStudioTagParameterDefinition> parameters = definition.Parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (ReferenceEquals(parameters[i], parameter))
                    return true;
            }

            return false;
        }

        static int FindParameterEnd(string source, TextStudioInlineTagHelpState state)
        {
            int end = state.TagEnd;
            while (end > state.TagNameEnd && char.IsWhiteSpace(source[end - 1]))
                end--;
            if (end > state.TagNameEnd && source[end - 1] == '/')
            {
                end--;
                while (end > state.TagNameEnd && char.IsWhiteSpace(source[end - 1]))
                    end--;
            }

            return end;
        }

        static int FindNamedParameterInsertionIndex(
            string source,
            TextStudioInlineTagHelpState state)
        {
            int trimmedEnd = state.TagEnd;
            while (trimmedEnd > state.TagNameEnd && char.IsWhiteSpace(source[trimmedEnd - 1]))
                trimmedEnd--;
            return trimmedEnd > state.TagNameEnd && source[trimmedEnd - 1] == '/'
                ? trimmedEnd - 1
                : state.TagEnd;
        }

        static bool TryFindExistingParameter(
            string source,
            int start,
            int end,
            TextStudioTagParameterDefinition parameter,
            out SourceSpan span)
        {
            if (TryFindNamedParameter(source, start, end, parameter, out span))
                return true;
            return TryFindPositionalParameter(source, start, end, parameter, out span);
        }

        static bool TryFindNamedParameter(
            string source,
            int start,
            int end,
            TextStudioTagParameterDefinition parameter,
            out SourceSpan span)
        {
            span = default;
            bool found = false;
            int index = start;
            while (index < end)
            {
                while (index < end && char.IsWhiteSpace(source[index]))
                    index++;
                if (index >= end)
                    break;

                int tokenStart = index;
                int tokenEnd = FindTokenEnd(source, tokenStart, end);
                index = tokenEnd;
                if (source[tokenStart] == '=')
                    continue;

                int operation = FindOperation(source, tokenStart, tokenEnd);
                if (operation > tokenStart)
                {
                    string name = source.Substring(tokenStart, operation - tokenStart).Trim();
                    if (!ParameterNameMatches(parameter, name))
                        continue;

                    span = NormalizeValueSpan(source, operation + 1, tokenEnd);
                    found = true;
                }
                else
                {
                    string name = source.Substring(tokenStart, tokenEnd - tokenStart).Trim();
                    if (!ParameterNameMatches(parameter, name))
                        continue;

                    span = new SourceSpan(tokenStart, tokenEnd);
                    found = true;
                }
            }

            return found;
        }

        static bool TryFindPositionalParameter(
            string source,
            int start,
            int end,
            TextStudioTagParameterDefinition parameter,
            out SourceSpan span)
        {
            span = default;
            if (parameter.PositionalIndex < 0)
                return false;

            int index = start;
            while (index < end && char.IsWhiteSpace(source[index]))
                index++;
            if (index < end && source[index] == '=')
            {
                index++;
                while (index < end && char.IsWhiteSpace(source[index]))
                    index++;
                if (parameter.PositionalIndex != 0)
                    return false;

                int valueEnd = FindTokenEnd(source, index, end);
                span = NormalizeValueSpan(source, index, valueEnd);
                return true;
            }

            int positionalIndex = 0;
            while (index < end)
            {
                while (index < end && char.IsWhiteSpace(source[index]))
                    index++;
                if (index >= end)
                    break;

                int tokenStart = index;
                int tokenEnd = FindTokenEnd(source, tokenStart, end);
                index = tokenEnd;
                if (FindOperation(source, tokenStart, tokenEnd) >= 0)
                    continue;

                int partStart = tokenStart;
                bool quoted = false;
                char quote = '\0';
                for (int i = tokenStart; i <= tokenEnd; i++)
                {
                    if (i < tokenEnd)
                    {
                        char c = source[i];
                        if (quoted)
                        {
                            if (c == quote)
                                quoted = false;
                            continue;
                        }

                        if (c == '"' || c == '\'')
                        {
                            quoted = true;
                            quote = c;
                            continue;
                        }

                        if (c != ',')
                            continue;
                    }

                    SourceSpan candidate = NormalizeValueSpan(source, partStart, i);
                    if (candidate.End > candidate.Start)
                    {
                        if (positionalIndex == parameter.PositionalIndex)
                        {
                            span = candidate;
                            return true;
                        }
                        positionalIndex++;
                    }
                    partStart = i + 1;
                }
            }

            return false;
        }

        static int FindTokenEnd(string source, int start, int end)
        {
            bool quoted = false;
            char quote = '\0';
            int index = start;
            while (index < end)
            {
                char c = source[index];
                if (quoted)
                {
                    if (c == quote)
                        quoted = false;
                }
                else if (c == '"' || c == '\'')
                {
                    quoted = true;
                    quote = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    break;
                }

                index++;
            }

            return index;
        }

        static int FindOperation(string source, int start, int end)
        {
            bool quoted = false;
            char quote = '\0';
            for (int i = start; i < end; i++)
            {
                char c = source[i];
                if (quoted)
                {
                    if (c == quote)
                        quoted = false;
                }
                else if (c == '"' || c == '\'')
                {
                    quoted = true;
                    quote = c;
                }
                else if (c == '=' || c == '*')
                {
                    return i;
                }
            }

            return -1;
        }

        static bool ParameterNameMatches(
            TextStudioTagParameterDefinition parameter,
            string name)
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;

            IReadOnlyList<string> aliases = parameter.Aliases;
            for (int i = 0; i < aliases.Count; i++)
            {
                if (string.Equals(aliases[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static SourceSpan NormalizeValueSpan(string source, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            while (end > start && char.IsWhiteSpace(source[end - 1]))
                end--;
            if (end - start >= 2)
            {
                char first = source[start];
                char last = source[end - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    start++;
                    end--;
                }
            }

            return new SourceSpan(start, end);
        }

        static string ResolveDefinitionExampleValue(
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter)
        {
            if (definition == null)
                return string.Empty;

            for (int i = 0; i < definition.Examples.Count; i++)
            {
                string example = definition.Examples[i] ?? string.Empty;
                int tagStart = example.IndexOf('<');
                int tagEnd = example.IndexOf('>', tagStart + 1);
                if (tagStart < 0 || tagEnd <= tagStart + 1 || example[tagStart + 1] == '/')
                    continue;

                int nameEnd = tagStart + 1;
                while (nameEnd < tagEnd &&
                       !char.IsWhiteSpace(example[nameEnd]) &&
                       example[nameEnd] != '=')
                {
                    nameEnd++;
                }

                string raw = example.Substring(nameEnd, tagEnd - nameEnd).Trim();
                if (raw.StartsWith("=", StringComparison.Ordinal))
                    raw = raw.Substring(1);
                if (string.IsNullOrEmpty(raw))
                    continue;

                TextStudioTagParameters parsed = TextStudioTagParameterParser.Parse(raw, true);
                if (parsed.TryGetString(parameter.Name, out string value) &&
                    !string.IsNullOrEmpty(value))
                {
                    return value;
                }

                for (int aliasIndex = 0; aliasIndex < parameter.Aliases.Count; aliasIndex++)
                {
                    if (parsed.TryGetString(parameter.Aliases[aliasIndex], out value) &&
                        !string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                if (parameter.PositionalIndex >= 0)
                {
                    value = parsed.GetString(parameter.PositionalIndex);
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            return string.Empty;
        }

        static bool TryFormatValue(
            string value,
            out string formatted,
            out int selectionStart,
            out int selectionEnd)
        {
            value = value ?? string.Empty;
            formatted = value;
            selectionStart = 0;
            selectionEnd = value.Length;
            if (value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0 ||
                value.IndexOf('>') >= 0 ||
                (value.IndexOf('"') >= 0 && value.IndexOf('\'') >= 0))
            {
                return false;
            }

            bool requiresQuotes = value.IndexOfAny(new[] { ' ', '\t', ',', '=' }) >= 0 ||
                                  value.IndexOf('"') >= 0 ||
                                  value.IndexOf('\'') >= 0;
            if (!requiresQuotes)
                return true;

            char quote = value.IndexOf('"') >= 0 ? '\'' : '"';
            formatted = quote + value + quote;
            selectionStart = 1;
            selectionEnd = formatted.Length - 1;
            return true;
        }
    }
}
