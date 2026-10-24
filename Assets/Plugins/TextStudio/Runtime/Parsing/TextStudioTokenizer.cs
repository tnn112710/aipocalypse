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
        const int NameCacheSize = 64;
        // FindTagEnd's result for a tag the end of the text left open, unlike a nested '<'.
        const int TagEndAtTextEnd = -2;

        TextToken[] _tokens;
        // The tag name of every open and close token, which the range ledger and style resolver read instead of copying it
        // again; null for every other token.
        string[] _names;
        // Plain text compiles without the tokenizer, so the first token allocates the buffer instead of every new pipeline.
        readonly int _initialCapacity;
        int _tokenCount;

        // Repeated tag names reuse one string from this direct-mapped cache, and each scan classifies a cached name once per
        // parameter shape: the registries a classification reads cannot change during one scan.
        string[] _nameCache;
        TokenKind[] _openKinds;
        int[] _openKindScans;
        TokenKind[] _closeKinds;
        int[] _closeKindScans;
        int _scan;

        // The first loop iteration of the last scan whose outcome read the end of its text. Every token before it is final
        // for any text that appends to that text, so an append scans again from there (TryResume).
        int _resumeSourceLength = -1;
        int _resumePosition;
        int _resumeTokenCount;
        int _resumeDepth;

        public ReadOnlySpan<TextToken> Tokens => new ReadOnlySpan<TextToken>(_tokens, 0, _tokenCount);
        public int TokenCount => _tokenCount;
        internal string[] Names => _names;
        public Func<string, bool> ActionTagResolver { get; set; }

        public TextStudioTokenizer(int initialCapacity = 128)
        {
            _initialCapacity = initialCapacity;
        }

        public void Tokenize(string source, TextStudioTextProjection projection = null)
        {
            _tokenCount = 0;
            _resumeSourceLength = -1;
            if (string.IsNullOrEmpty(source)) return;

            BeginClassificationScan();
            Scan(source, projection, 0, 0);
        }

        /// <summary>
        /// The resume point of the last scan for text appended at <paramref name="appendStart"/>: tokens before
        /// <paramref name="tokenIndex"/> stay exactly as they are, and a resumed scan replaces the rest.
        /// </summary>
        internal bool TryGetResumePoint(int appendStart, out int tokenIndex)
        {
            tokenIndex = _resumeTokenCount;
            return _resumeSourceLength >= 0 && appendStart == _resumeSourceLength;
        }

        /// <summary>
        /// Tokenizes <paramref name="source"/>, which appends to the text of the last scan from <paramref name="appendStart"/>
        /// on, by scanning again from the resume point. Text scanned with a projection never resumes.
        /// </summary>
        internal bool TryResume(string source, int appendStart)
        {
            if (!TryGetResumePoint(appendStart, out _) || source == null || source.Length < appendStart)
                return false;

            _tokenCount = _resumeTokenCount;
            int position = _resumePosition;
            int depth = _resumeDepth;
            _resumeSourceLength = -1;
            // The tokens before the resume point keep the classifications of the scan that produced them, which an append
            // takes only while the registries it read are unchanged, so appended tags reuse them too.
            Scan(source, null, position, depth);
            return true;
        }

        // Starts classifying names anew: a full tokenization reads the registries again.
        void BeginClassificationScan()
        {
            unchecked { _scan++; }
            if (_scan == 0)
            {
                // A wrapped counter could meet the scan of a cached classification again.
                if (_openKindScans != null)
                {
                    Array.Clear(_openKindScans, 0, _openKindScans.Length);
                    Array.Clear(_closeKindScans, 0, _closeKindScans.Length);
                }
                _scan = 1;
            }
        }

        void Scan(string source, TextStudioTextProjection projection, int start, int depth)
        {
            int i = start;
            int len = source.Length;
            int projectedSpanIndex = 0;
            int projectedSourceEnd = 0;
            int protectedStart = len;
            int protectedEnd = len;
            AdvanceProtectedSpan(projection, ref projectedSpanIndex, ref projectedSourceEnd, i, len, ref protectedStart, ref protectedEnd);
            bool resumeRecorded = projection != null;

            while (i < len)
            {
                if (i == protectedStart)
                {
                    AddToken(TokenKind.TextSpan, i, protectedEnd, 0, depth);
                    i = protectedEnd;
                    AdvanceProtectedSpan(projection, ref projectedSpanIndex, ref projectedSourceEnd, i, len, ref protectedStart, ref protectedEnd);
                    continue;
                }

                int iterationStart = i;
                int iterationTokenCount = _tokenCount;
                int iterationDepth = depth;
                i = ScanElement(source, i, len, protectedStart, ref depth, out bool readEnd);
                if (readEnd && !resumeRecorded)
                {
                    RecordResumePoint(len, iterationStart, iterationTokenCount, iterationDepth);
                    resumeRecorded = true;
                }
            }

            if (!resumeRecorded)
                RecordResumePoint(len, len, _tokenCount, depth);
        }

        void RecordResumePoint(int sourceLength, int position, int tokenCount, int depth)
        {
            _resumeSourceLength = sourceLength;
            _resumePosition = position;
            _resumeTokenCount = tokenCount;
            _resumeDepth = depth;
        }

        // Scans the element at i and returns the index after it. readEnd reports a decision that read the end of the text,
        // which text appended there could change: a run or search that stopped there, or a check that needed a character past it.
        int ScanElement(string source, int i, int len, int protectedStart, ref int depth, out bool readEnd)
        {
            readEnd = false;
            char c = source[i];

            // Escaped character: \< \> \\
            if (c == '\\')
            {
                if (i + 1 < len)
                {
                    char next = source[i + 1];
                    if (next == '<' || next == '>' || next == '\\')
                    {
                        AddToken(TokenKind.EscapedLiteral, i + 1, i + 2, 0, depth);
                        return i + 2;
                    }
                }
                else
                    readEnd = true;

                AddToken(TokenKind.TextSpan, i, i + 1, 0, depth);
                return i + 1;
            }

            // Tag: <...>
            if (c == '<')
            {
                // Compared in place: searching from each '<' scanned the rest of the text when it held no comment.
                if (i + 3 < len && source[i + 1] == '!' && source[i + 2] == '-' && source[i + 3] == '-')
                {
                    int commentEnd = source.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    readEnd = commentEnd < 0;
                    return commentEnd >= 0 ? commentEnd + 3 : source.Length;
                }
                // Appended characters could still complete a comment opening that the end of the text cut short.
                readEnd = i + 3 >= len && IsCommentOpeningPrefix(source, i + 1, len);
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
                            else
                            {
                                readEnd = true;
                                i = len;
                            }
                        }
                    }
                    return i;
                }

                readEnd |= tagEnd == TagEndAtTextEnd;
                AddToken(TokenKind.TextSpan, i, i + 1, 0, depth);
                return i + 1;
            }

            // Whitespace run
            if (char.IsWhiteSpace(c))
            {
                int wsStart = i;
                while (i < protectedStart && i < len && char.IsWhiteSpace(source[i])) i++;
                readEnd = i >= len;
                AddToken(TokenKind.Whitespace, wsStart, i, 0, depth);
                return i;
            }

            // Plain text
            int textStart = i;
            while (i < protectedStart && i < len && source[i] != '<' && source[i] != '\\' && !char.IsWhiteSpace(source[i]))
                i++;
            readEnd = i >= len;
            if (i > textStart)
                AddToken(TokenKind.TextSpan, textStart, i, 0, depth);
            return i;
        }

        // Whether the characters from start to the end of the text all match the start of "!--".
        static bool IsCommentOpeningPrefix(string source, int start, int len)
        {
            const string opening = "!--";
            for (int i = start; i < len && i - start < opening.Length; i++)
            {
                if (source[i] != opening[i - start])
                    return false;
            }

            return true;
        }

        static void AdvanceProtectedSpan(TextStudioTextProjection projection, ref int index, ref int sourceEnd,
            int sourceIndex, int length, ref int start, ref int end)
        {
            start = end = length;
            if (projection == null) return;
            while (projection.TryGetSpan(index, out TextStudioTextProjectionSpan span))
            {
                if (span.SourceLength == 0)
                {
                    index++;
                    continue;
                }
                // Native parsing also owns source syntax it removes, such as an empty formula.
                if (span.SourceStart > sourceEnd)
                {
                    start = Math.Max(sourceIndex, sourceEnd);
                    end = span.SourceStart;
                    sourceEnd = end;
                    if (start < end) return;
                }
                index++;
                sourceEnd = Math.Max(sourceEnd, span.SourceStart + span.SourceLength);
                if (!span.IsAtomic || sourceEnd <= sourceIndex) continue;
                start = Math.Max(sourceIndex, span.SourceStart);
                end = sourceEnd;
                return;
            }
            start = Math.Max(sourceIndex, sourceEnd);
            end = length;
            sourceEnd = length;
        }

        static int FindTagEnd(string source, int start, int len)
        {
            for (int i = start; i < len; i++)
            {
                if (source[i] == '>') return i;
                if (source[i] == '<') return -1; // Nested < means broken tag
            }
            return TagEndAtTextEnd;
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
                        AddToken(TokenKind.StyleClose, nameStart, nameStart, 0, depth, tagStart, innerEnd + 1, nameStart, string.Empty);
                    }
                    return;
                }
                string closeName = InternName(source, nameStart, nameEnd, out int closeSlot);
                int nameHash = ComputeHash(source, nameStart, nameEnd);
                TokenKind closeKind = ClassifyClose(closeName, closeSlot);
                if (closeKind != TokenKind.ExtensionClose && depth > 0)
                    depth--;
                AddToken(closeKind, nameStart, nameEnd, nameHash, depth, tagStart, innerEnd + 1, nameEnd, closeName);
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

            string tagName = InternName(source, trimmedStart, nameEndOpen, out int slot);
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
            TokenKind kind = ClassifyTag(tagName, paramStart > 0, slot);

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
            _names[_tokenCount] = tagName;
            _tokens[_tokenCount++] = token;

            // Only style/effect opens contribute to nesting; actions/waits/markers are
            // self-contained and don't wrap children.
            if (kind == TokenKind.StyleOpen || kind == TokenKind.EffectTag)
                depth++;
        }

        // Returns the name from the cache when the same characters were cached, so a repeated tag allocates no string.
        string InternName(string source, int start, int end, out int slot)
        {
            int length = end - start;
            uint hash = 2166136261;
            for (int i = start; i < end; i++)
                hash = unchecked((hash ^ source[i]) * 16777619);
            slot = (int)(hash & (NameCacheSize - 1));
            if (_nameCache == null)
            {
                _nameCache = new string[NameCacheSize];
                _openKinds = new TokenKind[NameCacheSize * 2];
                _openKindScans = new int[NameCacheSize * 2];
                _closeKinds = new TokenKind[NameCacheSize];
                _closeKindScans = new int[NameCacheSize];
            }

            string cached = _nameCache[slot];
            if (cached != null &&
                cached.Length == length &&
                string.CompareOrdinal(cached, 0, source, start, length) == 0)
            {
                return cached;
            }

            string name = source.Substring(start, length);
            _nameCache[slot] = name;
            // Classifications of the name this slot held belong to that name.
            _openKindScans[slot * 2] = 0;
            _openKindScans[slot * 2 + 1] = 0;
            _closeKindScans[slot] = 0;
            return name;
        }

        TokenKind ClassifyTag(string tagName, bool hasParameter, int slot)
        {
            int index = slot * 2 + (hasParameter ? 1 : 0);
            if (_openKindScans[index] == _scan)
                return _openKinds[index];

            TokenKind kind = ClassifyTag(tagName, hasParameter);
            _openKinds[index] = kind;
            _openKindScans[index] = _scan;
            return kind;
        }

        TokenKind ClassifyClose(string closeName, int slot)
        {
            if (_closeKindScans[slot] == _scan)
                return _closeKinds[slot];

            TokenKind kind = TextStudioTagUtility.TryGetExtensionTag(closeName, false, out _)
                ? TokenKind.ExtensionClose
                : TokenKind.StyleClose;
            _closeKinds[slot] = kind;
            _closeKindScans[slot] = _scan;
            return kind;
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
            AddToken(kind, start, end, nameHash, depth, start, end, end, null);
        }

        void AddToken(TokenKind kind, int start, int end, int nameHash, int depth, int outerStart, int outerEnd, int nameEnd, string name)
        {
            EnsureCapacity();
            _names[_tokenCount] = name;
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
            if (_tokens == null)
            {
                _tokens = new TextToken[_initialCapacity];
                _names = new string[_initialCapacity];
            }
            else if (_tokenCount >= _tokens.Length)
            {
                Array.Resize(ref _tokens, _tokens.Length * 2);
                Array.Resize(ref _names, _tokens.Length);
            }
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
