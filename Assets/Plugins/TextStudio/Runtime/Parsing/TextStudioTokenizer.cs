using System;
using TextStudio.BuiltIns;

namespace TextStudio
{
    /// <summary>
    /// Token types produced by the single-pass tokenizer.
    /// </summary>
    public enum TokenKind : byte
    {
        TextSpan,
        StyleOpen,
        StyleClose,
        EffectTag,
        Marker,
        Wait,
        Action,
        ExtensionTag,
        ExtensionClose,
        EscapedLiteral,
        Whitespace
    }

    /// <summary>
    /// Flat token struct output by the tokenizer.
    /// References ranges into the source text via indices.
    /// </summary>
    public struct TextToken
    {
        public TokenKind kind;
        public int sourceStart;
        public int sourceEnd;
        public int nameHash;
        public int parameterStart;
        public int parameterEnd;
        public int nestingDepth;
        public int outerStart;
        public int outerEnd;
        public int nameEnd;
        public bool selfClosing;

        public int Length => sourceEnd - sourceStart;

        /// <summary>Gets the tag-name slice from the original source without allocating.</summary>
        public ReadOnlySpan<char> GetNameSpan(string source)
        {
            if (nameEnd > sourceStart)
                return source.AsSpan(sourceStart, nameEnd - sourceStart);
            if (parameterStart > sourceStart)
                return source.AsSpan(sourceStart, parameterStart - sourceStart);
            return source.AsSpan(sourceStart, sourceEnd - sourceStart);
        }

        /// <summary>Gets the raw parameter slice from the original source without allocating.</summary>
        public ReadOnlySpan<char> GetParameterSpan(string source)
        {
            if (parameterStart <= 0 || parameterEnd <= parameterStart)
                return ReadOnlySpan<char>.Empty;
            return source.AsSpan(parameterStart, parameterEnd - parameterStart);
        }
    }

    /// <summary>
    /// Hand-written single-pass tokenizer. No regex, no allocations during parsing.
    /// Produces a flat array of TextToken structs.
    /// </summary>
    internal sealed class TextStudioTokenizer
    {
        TextToken[] _tokens;
        int _tokenCount;

        public ReadOnlySpan<TextToken> Tokens => new ReadOnlySpan<TextToken>(_tokens, 0, _tokenCount);
        public int TokenCount => _tokenCount;
        public Func<string, bool> ActionTagResolver { get; set; }

        public TextStudioTokenizer(int initialCapacity = 128)
        {
            _tokens = new TextToken[initialCapacity];
        }

        public void Tokenize(string source)
        {
            _tokenCount = 0;
            if (string.IsNullOrEmpty(source)) return;

            int i = 0;
            int depth = 0;
            int len = source.Length;

            while (i < len)
            {
                char c = source[i];

                // Escaped character: \< \> \\
                if (c == '\\' && i + 1 < len)
                {
                    char next = source[i + 1];
                    if (next == '<' || next == '>' || next == '\\')
                    {
                        AddToken(TokenKind.EscapedLiteral, i + 1, i + 2, 0, depth);
                        i += 2;
                        continue;
                    }
                }
                if (c == '\\')
                {
                    AddToken(TokenKind.TextSpan, i, i + 1, 0, depth);
                    i++;
                    continue;
                }

                // Tag: <...>
                if (c == '<')
                {
                    // Compared in place: searching from each '<' scanned the rest of the text when it held no comment.
                    if (i + 3 < len && source[i + 1] == '!' && source[i + 2] == '-' && source[i + 3] == '-')
                    {
                        int commentEnd = source.IndexOf("-->", i + 4, StringComparison.Ordinal);
                        i = commentEnd >= 0 ? commentEnd + 3 : source.Length;
                        continue;
                    }
                    int tagEnd = FindTagEnd(source, i + 1, len);
                    if (tagEnd > 0)
                    {
                        ParseTag(source, i, i + 1, tagEnd, ref depth);
                        i = tagEnd + 1;
                        if (_tokenCount > 0)
                        {
                            TextToken opening = _tokens[_tokenCount - 1];
                            if (opening.kind == TokenKind.StyleOpen &&
                                opening.GetNameSpan(source).Equals("noparse".AsSpan(), StringComparison.OrdinalIgnoreCase))
                            {
                                int closeStart = source.IndexOf("</noparse>", i, StringComparison.OrdinalIgnoreCase);
                                int literalEnd = closeStart >= 0 ? closeStart : len;
                                if (literalEnd > i) AddToken(TokenKind.TextSpan, i, literalEnd, 0, depth);
                                if (closeStart >= 0)
                                {
                                    ParseTag(source, closeStart, closeStart + 1, closeStart + 9, ref depth);
                                    i = closeStart + 10;
                                }
                                else i = len;
                            }
                        }
                        continue;
                    }

                    AddToken(TokenKind.TextSpan, i, i + 1, 0, depth);
                    i++;
                    continue;
                }

                // Whitespace run
                if (char.IsWhiteSpace(c))
                {
                    int wsStart = i;
                    while (i < len && char.IsWhiteSpace(source[i])) i++;
                    AddToken(TokenKind.Whitespace, wsStart, i, 0, depth);
                    continue;
                }

                // Plain text
                int textStart = i;
                while (i < len && source[i] != '<' && source[i] != '\\' && !char.IsWhiteSpace(source[i]))
                    i++;
                if (i > textStart)
                    AddToken(TokenKind.TextSpan, textStart, i, 0, depth);
            }
        }

        static int FindTagEnd(string source, int start, int len)
        {
            for (int i = start; i < len; i++)
            {
                if (source[i] == '>') return i;
                if (source[i] == '<') return -1; // Nested < means broken tag
            }
            return -1;
        }

        void ParseTag(string source, int tagStart, int innerStart, int innerEnd, ref int depth)
        {
            if (innerStart >= innerEnd) return;

            int trimmedStart = innerStart;
            int trimmedEnd = innerEnd;
            while (trimmedStart < trimmedEnd && char.IsWhiteSpace(source[trimmedStart])) trimmedStart++;
            while (trimmedEnd > trimmedStart && char.IsWhiteSpace(source[trimmedEnd - 1])) trimmedEnd--;
            if (trimmedStart >= trimmedEnd) return;

            // Closing tag: </name>. Emit at the outer depth (same as the matching open
            // tag) so a well-formed <a>…</a> pair brackets its children at depth+1.
            if (source[trimmedStart] == '/')
            {
                int nameStart = trimmedStart + 1;
                while (nameStart < trimmedEnd && char.IsWhiteSpace(source[nameStart])) nameStart++;
                int nameEnd = ScanNameEnd(source, nameStart, trimmedEnd);
                if (nameEnd <= nameStart)
                {
                    if (nameStart >= trimmedEnd)
                    {
                        if (depth > 0)
                            depth--;
                        AddToken(TokenKind.StyleClose, nameStart, nameStart, 0, depth, tagStart, innerEnd + 1, nameStart);
                    }
                    return;
                }
                string closeName = source.Substring(nameStart, nameEnd - nameStart);
                int nameHash = ComputeHash(source, nameStart, nameEnd);
                TokenKind closeKind = TextStudioTagUtility.TryGetExtensionTag(closeName, false, out _)
                    ? TokenKind.ExtensionClose
                    : TokenKind.StyleClose;
                if (closeKind != TokenKind.ExtensionClose && depth > 0)
                    depth--;
                AddToken(closeKind, nameStart, nameEnd, nameHash, depth, tagStart, innerEnd + 1, nameEnd);
                return;
            }

            bool selfClosing = false;
            if (trimmedEnd > trimmedStart && source[trimmedEnd - 1] == '/')
            {
                selfClosing = true;
                trimmedEnd--;
                while (trimmedEnd > trimmedStart && char.IsWhiteSpace(source[trimmedEnd - 1])) trimmedEnd--;
            }

            int nameEndOpen = ScanNameEnd(source, trimmedStart, trimmedEnd);
            if (nameEndOpen <= trimmedStart) return;

            string tagName = source.Substring(trimmedStart, nameEndOpen - trimmedStart);
            int paramStart = 0;
            int paramEnd = 0;
            int cursor = nameEndOpen;
            while (cursor < trimmedEnd && char.IsWhiteSpace(source[cursor])) cursor++;
            if (cursor < trimmedEnd)
            {
                if (source[cursor] == '=')
                {
                    cursor++;
                    while (cursor < trimmedEnd && char.IsWhiteSpace(source[cursor])) cursor++;
                }
                paramStart = cursor;
                paramEnd = trimmedEnd;
            }

            int hash = ComputeHash(source, trimmedStart, nameEndOpen);
            TokenKind kind = ClassifyTag(tagName, paramStart > 0);

            var token = new TextToken
            {
                kind = kind,
                sourceStart = trimmedStart,
                sourceEnd = trimmedEnd,
                nameHash = hash,
                parameterStart = paramStart,
                parameterEnd = paramEnd,
                nestingDepth = depth,
                outerStart = tagStart,
                outerEnd = innerEnd + 1,
                nameEnd = nameEndOpen,
                selfClosing = selfClosing
            };

            EnsureCapacity();
            _tokens[_tokenCount++] = token;

            // Only style/effect opens contribute to nesting; actions/waits/markers are
            // self-contained and don't wrap children.
            if (kind == TokenKind.StyleOpen || kind == TokenKind.EffectTag)
                depth++;
        }

        static int ScanNameEnd(string source, int start, int end)
        {
            int i = start;
            while (i < end)
            {
                char c = source[i];
                if (char.IsWhiteSpace(c) || c == '=' || c == '/')
                    break;
                i++;
            }

            return i;
        }

        TokenKind ClassifyTag(string tagName, bool hasParameter)
        {
            if (BuiltInActions.IsWaitTag(tagName))
                return TokenKind.Wait;

            if (BuiltInActions.IsMarkerTag(tagName))
                return TokenKind.Marker;

            if (BuiltInActions.TryGetCanonicalActionId(tagName, out _))
                return TokenKind.Action;

            if (TextStudioRegistry.Actions.TryGetQuiet(tagName, out _))
                return TokenKind.Action;

            if (ActionTagResolver != null && ActionTagResolver(tagName))
                return TokenKind.Action;

            if (TextStudioTagUtility.TryGetExtensionTag(tagName, hasParameter, out _))
                return TokenKind.ExtensionTag;

            // Default: treat as style/effect open tag
            return TokenKind.StyleOpen;
        }

        void AddToken(TokenKind kind, int start, int end, int nameHash, int depth)
        {
            AddToken(kind, start, end, nameHash, depth, start, end, end);
        }

        void AddToken(TokenKind kind, int start, int end, int nameHash, int depth, int outerStart, int outerEnd, int nameEnd)
        {
            EnsureCapacity();
            _tokens[_tokenCount++] = new TextToken
            {
                kind = kind,
                sourceStart = start,
                sourceEnd = end,
                nameHash = nameHash,
                nestingDepth = depth,
                outerStart = outerStart,
                outerEnd = outerEnd,
                nameEnd = nameEnd
            };
        }

        void EnsureCapacity()
        {
            if (_tokenCount >= _tokens.Length)
                Array.Resize(ref _tokens, _tokens.Length * 2);
        }

        static int ComputeHash(string source, int start, int end)
        {
            // FNV-1a hash, case-insensitive
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = start; i < end; i++)
                {
                    hash ^= char.ToLowerInvariant(source[i]);
                    hash *= 16777619;
                }
                return hash;
            }
        }
    }

    internal static class TextStudioPhaseTagUtility
    {
        public const string AppearTag = "appear";
        public const string DisappearTag = "disappear";

        public static bool TryGetPhase(string tagName, out AnimationPhase phase)
        {
            if (string.Equals(tagName, AppearTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, "appearance", StringComparison.OrdinalIgnoreCase))
            {
                phase = AnimationPhase.Appearance;
                return true;
            }

            if (string.Equals(tagName, DisappearTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, "disappearance", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, "hide", StringComparison.OrdinalIgnoreCase))
            {
                phase = AnimationPhase.Disappearance;
                return true;
            }

            phase = AnimationPhase.None;
            return false;
        }

        public static string GetTagName(AnimationPhase phase)
        {
            return phase == AnimationPhase.Disappearance ? DisappearTag : AppearTag;
        }

        public static bool IsPhaseTag(string tagName)
        {
            return TryGetPhase(tagName, out _);
        }

        public static bool TryResolveEffectId(string tagName, out string effectId)
        {
            if (BuiltInStyles.TryResolve(tagName, out effectId))
                return true;

            if (TextStudioRegistry.Effects.TryGetQuiet(tagName, out _))
            {
                effectId = tagName;
                return true;
            }

            effectId = string.Empty;
            return false;
        }
    }
}
