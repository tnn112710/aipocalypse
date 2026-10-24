using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>Optional native parsing which replaces source syntax with semantic text before reveal and effects.</summary>
    public interface ITextStudioTextProjectionBackend
    {
        int TextProjectionRevision { get; }
        /// <summary>Gets the complete semantic text and source-span mapping for the displayed input.</summary>
        bool TryGetTextProjection(string displayText, out TextStudioTextProjection projection);
    }

    /// <summary>One owned mapping from projected UTF-16 text to its original input span.</summary>
    public readonly struct TextStudioTextProjectionSpan
    {
        public readonly int ResolvedStart;
        public readonly int ResolvedLength;
        public readonly int SourceStart;
        public readonly int SourceLength;
        public readonly bool IsAtomic;

        /// <summary>Maps one resolved UTF-16 range to its source range, optionally as one atomic semantic character.</summary>
        public TextStudioTextProjectionSpan(int resolvedStart, int resolvedLength,
            int sourceStart, int sourceLength, bool isAtomic = false)
        {
            ResolvedStart = resolvedStart;
            ResolvedLength = resolvedLength;
            SourceStart = sourceStart;
            SourceLength = sourceLength;
            IsAtomic = isAtomic;
        }
    }

    /// <summary>Immutable, complete native parse projection with original formula provenance.</summary>
    public sealed class TextStudioTextProjection
    {
        readonly TextStudioTextProjectionSpan[] _spans;
        public string SourceText { get; }
        public string ResolvedText { get; }
        public int SpanCount => _spans.Length;

        /// <summary>Copies and validates contiguous spans that cover all resolved text and refer to valid source ranges.</summary>
        public TextStudioTextProjection(string sourceText, string resolvedText,
            IReadOnlyList<TextStudioTextProjectionSpan> spans)
        {
            SourceText = sourceText ?? string.Empty;
            ResolvedText = resolvedText ?? string.Empty;
            if (spans == null) throw new ArgumentNullException(nameof(spans));
            _spans = new TextStudioTextProjectionSpan[spans.Count];
            int end = 0;
            for (int i = 0; i < spans.Count; i++)
            {
                TextStudioTextProjectionSpan span = spans[i];
                if (span.ResolvedStart != end || span.ResolvedLength <= 0 ||
                    span.ResolvedLength > ResolvedText.Length - end || span.SourceStart < 0 ||
                    span.SourceStart > SourceText.Length || span.SourceLength < 0 ||
                    span.SourceLength > SourceText.Length - span.SourceStart)
                    throw new ArgumentException("Projection spans must cover the resolved text and refer to valid source ranges.", nameof(spans));
                if (span.IsAtomic && (span.ResolvedLength != 1 || span.SourceLength == 0))
                    throw new ArgumentException("An atomic source span must project to one semantic character.", nameof(spans));
                _spans[i] = span;
                end += span.ResolvedLength;
            }
            if (end != ResolvedText.Length)
                throw new ArgumentException("Projection spans do not cover the complete resolved text.", nameof(spans));
        }

        /// <summary>Gets one source mapping by span index, returning false outside the stored range.</summary>
        public bool TryGetSpan(int index, out TextStudioTextProjectionSpan span)
        {
            span = index >= 0 && index < _spans.Length ? _spans[index] : default;
            return index >= 0 && index < _spans.Length;
        }
    }
}
