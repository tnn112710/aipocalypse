using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioSourceTagSpan
    {
        public TextStudioSourceTagSpan(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }
        public int End { get; }
        public int Length => End - Start;
    }

    internal sealed partial class TextStudioSourceHighlightBuilder
    {
#if UNITY_6000_7_OR_NEWER
        // The comparer is stateless and safe to share for the editor code-load lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        sealed partial class TagSpanComparer : IComparer<TextStudioSourceTagSpan>
        {
            public static readonly TagSpanComparer Instance = new TagSpanComparer();

            public int Compare(TextStudioSourceTagSpan left, TextStudioSourceTagSpan right)
            {
                int startComparison = left.Start.CompareTo(right.Start);
                return startComparison != 0
                    ? startComparison
                    : left.End.CompareTo(right.End);
            }
        }

        readonly TextStudioTokenizer _tokenizer = new TextStudioTokenizer();
        readonly List<TextStudioSourceTagSpan> _tagSpans =
            new List<TextStudioSourceTagSpan>(16);

        internal IReadOnlyList<TextStudioSourceTagSpan> TagSpans => _tagSpans;

        internal void Analyze(string source, TextStudioMarkupSyntax syntax)
        {
            source ??= string.Empty;
            _tagSpans.Clear();

            if (syntax == null || syntax.AcceptNativeAngleTags)
            {
                AddCompleteAngleTags(source);
                AddUnfinishedAngleTags(source);
            }

            TextStudioAuthoringProfile profile = TextStudioMarkupSyntax.ResolveProfile(syntax);
            if (profile == TextStudioAuthoringProfile.Compatibility ||
                profile == TextStudioAuthoringProfile.TextAnimator)
            {
                AddSupportedTextAnimatorBraceTags(source);
                AddUnfinishedTextAnimatorBraceTags(source);
            }

            if (profile == TextStudioAuthoringProfile.Custom && syntax != null)
            {
                char opening = syntax.OpeningDelimiter[0];
                char closing = syntax.ClosingDelimiter[0];
                if (opening != '<' || closing != '>')
                    AddCustomDelimiterTags(source, opening, closing);
            }

            NormalizeTagSpans(source.Length);
        }

        void AddCompleteAngleTags(string source)
        {
            _tokenizer.Tokenize(source);
            ReadOnlySpan<TextToken> tokens = _tokenizer.Tokens;
            for (int i = 0; i < tokens.Length; i++)
            {
                TextToken token = tokens[i];
                if (token.kind == TokenKind.TextSpan ||
                    token.kind == TokenKind.Whitespace ||
                    token.kind == TokenKind.EscapedLiteral ||
                    token.outerEnd <= token.outerStart)
                {
                    continue;
                }

                AddTagSpan(token.outerStart, token.outerEnd);
            }
        }

        void AddUnfinishedAngleTags(string source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != '<' || IsEscaped(source, i))
                    continue;

                int lineEnd = FindLineEnd(source, i + 1);
                int closing = IndexOfBefore(source, '>', i + 1, lineEnd);
                if (closing >= 0 || !IsPlausibleAngleTagStart(source, i + 1, lineEnd))
                    continue;

                AddTagSpan(i, lineEnd);
                i = lineEnd - 1;
            }
        }

        void AddSupportedTextAnimatorBraceTags(string source)
        {
            IReadOnlyList<TextAnimatorMarkupToken> tokens = TextAnimatorMarkupScanner.Scan(source);
            for (int i = 0; i < tokens.Count; i++)
            {
                TextAnimatorMarkupToken token = tokens[i];
                if (!token.IsBrace ||
                    IsEscaped(source, token.Start) ||
                    !TextStudioPhaseTagUtility.TryResolveEffectId(token.Name, out _))
                {
                    continue;
                }

                AddTagSpan(token.Start, token.End);
            }
        }

        void AddUnfinishedTextAnimatorBraceTags(string source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != '{' ||
                    IsEscaped(source, i) ||
                    (i > 0 && source[i - 1] == '{') ||
                    (i + 1 < source.Length && source[i + 1] == '{'))
                {
                    continue;
                }

                int lineEnd = FindLineEnd(source, i + 1);
                int closing = IndexOfBefore(source, '}', i + 1, lineEnd);
                if (closing >= 0 || !IsPlausibleBraceTagStart(source, i + 1, lineEnd))
                    continue;

                AddTagSpan(i, lineEnd);
                i = lineEnd - 1;
            }
        }

        void AddCustomDelimiterTags(string source, char opening, char closing)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != opening || IsEscaped(source, i))
                    continue;

                int lineEnd = FindLineEnd(source, i + 1);
                int close = IndexOfBefore(source, closing, i + 1, lineEnd);
                int fragmentEnd = close >= 0 ? close : lineEnd;
                if (!IsPlausibleCustomTagStart(source, i + 1, fragmentEnd))
                    continue;

                AddTagSpan(i, close >= 0 ? close + 1 : lineEnd);
                i = (close >= 0 ? close + 1 : lineEnd) - 1;
            }
        }

        void NormalizeTagSpans(int sourceLength)
        {
            if (_tagSpans.Count == 0)
                return;

            _tagSpans.Sort(TagSpanComparer.Instance);
            int writeIndex = 0;
            TextStudioSourceTagSpan current = Clamp(_tagSpans[0], sourceLength);
            for (int i = 1; i < _tagSpans.Count; i++)
            {
                TextStudioSourceTagSpan next = Clamp(_tagSpans[i], sourceLength);
                if (next.Start < current.End)
                {
                    current = new TextStudioSourceTagSpan(
                        current.Start,
                        Math.Max(current.End, next.End));
                    continue;
                }

                _tagSpans[writeIndex++] = current;
                current = next;
            }

            _tagSpans[writeIndex++] = current;
            if (writeIndex < _tagSpans.Count)
                _tagSpans.RemoveRange(writeIndex, _tagSpans.Count - writeIndex);
        }

        void AddTagSpan(int start, int end)
        {
            if (end > start)
                _tagSpans.Add(new TextStudioSourceTagSpan(start, end));
        }

        static TextStudioSourceTagSpan Clamp(TextStudioSourceTagSpan span, int sourceLength)
        {
            int start = Math.Max(0, Math.Min(span.Start, sourceLength));
            int end = Math.Max(start, Math.Min(span.End, sourceLength));
            return new TextStudioSourceTagSpan(start, end);
        }

        static int FindLineEnd(string source, int start)
        {
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '\r' || source[i] == '\n')
                    return i;
            }

            return source.Length;
        }

        static int IndexOfBefore(string source, char value, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (source[i] == value)
                    return i;
            }

            return -1;
        }

        static bool IsPlausibleAngleTagStart(string source, int start, int end)
        {
            if (start >= end)
                return false;

            if (source[start] == '/')
            {
                start++;
                if (start >= end)
                    return false;
            }

            bool allowLeadingDigit = false;
            char prefix = source[start];
            if (prefix == '+' || prefix == '-' || prefix == '!' || prefix == '?' || prefix == '#')
            {
                allowLeadingDigit = prefix == '#';
                start++;
                if (start >= end)
                    return false;
            }

            char first = source[start];
            return char.IsLetter(first) || first == '_' || (allowLeadingDigit && char.IsDigit(first));
        }

        static bool IsPlausibleBraceTagStart(string source, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            if (start < end && source[start] == '/')
            {
                start++;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
            }
            if (start < end && source[start] == '#')
            {
                start++;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
            }

            return start < end && (char.IsLetter(source[start]) || source[start] == '_');
        }

        static bool IsPlausibleCustomTagStart(string source, int start, int end)
        {
            if (start < end && source[start] == '/')
                start++;
            return start < end && (char.IsLetter(source[start]) || source[start] == '_');
        }

        static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }

    }

    internal sealed class TextStudioSourceHighlightElement : VisualElement
    {
        internal const string ElementName = "ts-source-content-highlight";
        internal const string TagColorClass = "ts-source-tag-color";

        readonly TextStudioSourceHighlightBuilder _builder =
            new TextStudioSourceHighlightBuilder();
        readonly TextElement _sourceTextElement;
#if UNITY_6000_2_OR_NEWER
        readonly List<bool> _tagGlyphMask = new List<bool>(128);
        readonly List<TextElement.Glyph> _renderedGlyphs = new List<TextElement.Glyph>(128);
        int _glyphMaskRefreshVersion = -1;
        int _glyphMaskGlyphCount = -1;
        Vector2 _glyphMaskContentSize = new Vector2(float.NaN, float.NaN);
        bool _glyphMaskMatches;
#endif

        string _lastSource;
        TextStudioMarkupSyntax _lastSyntax;
        int _lastSyntaxRevision;

        TextStudioSourceHighlightElement(TextElement sourceTextElement)
        {
            _sourceTextElement = sourceTextElement;
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            focusable = false;
            tabIndex = -1;
            AddToClassList(TagColorClass);
            style.position = Position.Absolute;
            style.display = DisplayStyle.None;
#if UNITY_6000_2_OR_NEWER
            _sourceTextElement.PostProcessTextVertices += TintTagGlyphs;
#endif
        }

        internal string Source => _lastSource ?? string.Empty;
        internal int RefreshVersion { get; private set; }
        internal bool IsHighlighting { get; private set; }
        internal IReadOnlyList<TextStudioSourceTagSpan> TagSpans => _builder.TagSpans;
#if UNITY_6000_2_OR_NEWER
        internal IReadOnlyList<bool> TagGlyphMask => _tagGlyphMask;
#endif

        internal static TextStudioSourceHighlightElement AttachTo(TextField sourceField)
        {
            if (sourceField == null)
                return null;

            TextElement sourceTextElement = sourceField.Q<TextElement>(
                className: TextElement.selectableUssClassName);
            if (sourceTextElement == null)
                return null;

            VisualElement overlayHost = sourceTextElement.parent;
            if (overlayHost == null)
                return null;

            TextStudioSourceHighlightElement existing =
                overlayHost.Q<TextStudioSourceHighlightElement>(ElementName);
            if (existing != null)
                return existing;

            TextStudioSourceHighlightElement overlay =
                new TextStudioSourceHighlightElement(sourceTextElement);
            overlayHost.Add(overlay);
            return overlay;
        }

        internal bool Refresh(string source, TextStudioMarkupSyntax syntax)
        {
            source ??= string.Empty;
            int syntaxRevision = syntax != null ? syntax.Revision : 0;
            if (string.Equals(_lastSource, source, StringComparison.Ordinal) &&
                ReferenceEquals(_lastSyntax, syntax) &&
                _lastSyntaxRevision == syntaxRevision)
            {
                return false;
            }

            _lastSource = source;
            _lastSyntax = syntax;
            _lastSyntaxRevision = syntaxRevision;
            _builder.Analyze(source, syntax);
#if UNITY_6000_2_OR_NEWER
            IsHighlighting = _builder.TagSpans.Count > 0;
            _glyphMaskRefreshVersion = -1;
            _sourceTextElement?.MarkDirtyRepaint();
#else
            IsHighlighting = false;
#endif
            RefreshVersion++;
            return true;
        }

#if UNITY_6000_2_OR_NEWER
        bool EnsureTagGlyphMask(int glyphCount)
        {
            Vector2 contentSize = _sourceTextElement.contentRect.size;
            if (_glyphMaskRefreshVersion == RefreshVersion &&
                _glyphMaskGlyphCount == glyphCount &&
                Mathf.Approximately(_glyphMaskContentSize.x, contentSize.x) &&
                Mathf.Approximately(_glyphMaskContentSize.y, contentSize.y))
            {
                return _glyphMaskMatches;
            }

            _tagGlyphMask.Clear();
            string source = Source;
            IReadOnlyList<TextStudioSourceTagSpan> spans = _builder.TagSpans;
            ITextSelection selection = _sourceTextElement.selection;
            int spanIndex = 0;
            for (int sourceIndex = 0; sourceIndex < source.Length;)
            {
                while (spanIndex < spans.Count && spans[spanIndex].End <= sourceIndex)
                    spanIndex++;

                bool isTag =
                    spanIndex < spans.Count &&
                    sourceIndex >= spans[spanIndex].Start &&
                    sourceIndex < spans[spanIndex].End;
                int scalarLength = GetUnicodeScalarLength(source, sourceIndex);
                Vector2 start = selection.GetCursorPositionFromStringIndex(sourceIndex);
                Vector2 end = selection.GetCursorPositionFromStringIndex(
                    sourceIndex + scalarLength);
                if (OccupiesGlyphSlot(source, sourceIndex, start, end))
                    _tagGlyphMask.Add(isTag);

                sourceIndex += scalarLength;
            }

            _glyphMaskRefreshVersion = RefreshVersion;
            _glyphMaskGlyphCount = glyphCount;
            _glyphMaskContentSize = contentSize;
            _glyphMaskMatches = _tagGlyphMask.Count == glyphCount;
            return _glyphMaskMatches;
        }

        void TintTagGlyphs(TextElement.GlyphsEnumerable glyphs)
        {
            if (!IsHighlighting)
                return;

            try
            {
                // Cursor queries can regenerate Unity's live text data. Snapshot the submitted glyphs before building the mask so enumeration stays aligned.
                foreach (TextElement.Glyph glyph in glyphs)
                    _renderedGlyphs.Add(glyph);

                if (_renderedGlyphs.Count != glyphs.Count ||
                    !EnsureTagGlyphMask(_renderedGlyphs.Count))
                {
                    return;
                }

                Color32 tagColor = resolvedStyle.color;
                for (int glyphIndex = 0; glyphIndex < _renderedGlyphs.Count; glyphIndex++)
                {
                    if (_tagGlyphMask[glyphIndex])
                        TintGlyph(_renderedGlyphs[glyphIndex], tagColor);
                }
            }
            finally
            {
                // Native vertex slices belong to this callback only.
                _renderedGlyphs.Clear();
            }
        }

        static void TintGlyph(TextElement.Glyph glyph, Color32 color)
        {
            NativeSlice<Vertex> vertices = glyph.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vertex vertex = vertices[i];
                vertex.tint = color;
                vertices[i] = vertex;
            }
        }

        static bool OccupiesGlyphSlot(
            string source,
            int sourceIndex,
            Vector2 start,
            Vector2 end)
        {
            if (Mathf.Abs(start.y - end.y) > 0.01f)
                return !char.IsWhiteSpace(source, sourceIndex);

            return Mathf.Abs(start.x - end.x) > 0.01f;
        }

        static int GetUnicodeScalarLength(string source, int sourceIndex)
        {
            return char.IsHighSurrogate(source[sourceIndex]) &&
                   sourceIndex + 1 < source.Length &&
                   char.IsLowSurrogate(source[sourceIndex + 1])
                ? 2
                : 1;
        }
#endif
    }
}
