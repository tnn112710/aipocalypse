using System;

namespace TextStudio
{
    /// <summary>An authored appearance invocation, retained even when its definition cannot resolve.</summary>
    public readonly struct TextStudioAppearanceSpan
    {
        public string Id { get; }
        public TextStudioTagParameters Parameters { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        /// <summary>First semantic text unit covered by this occurrence.</summary>
        public int Start { get; }
        public int Length { get; }
        /// <summary>Index of the enclosing appearance occurrence, or -1.</summary>
        public int ParentIndex { get; }
        public int OccurrenceId { get; }
        public bool IsAdHoc { get; }

        /// <summary>Creates an authored appearance invocation with source and semantic ranges, parent linkage, parameters, and nonnegative lengths.</summary>
        public TextStudioAppearanceSpan(string id, TextStudioTagParameters parameters, int sourceStart, int sourceLength,
            int start, int length, int parentIndex, int occurrenceId, bool isAdHoc)
        {
            Id = id ?? string.Empty;
            Parameters = parameters;
            SourceStart = sourceStart;
            SourceLength = Math.Max(0, sourceLength);
            Start = start;
            Length = Math.Max(0, length);
            ParentIndex = parentIndex;
            OccurrenceId = occurrenceId;
            IsAdHoc = isAdHoc;
        }

        internal TextStudioAppearanceSpan Close(int sourceEnd, int end) => new TextStudioAppearanceSpan(
            Id, Parameters, SourceStart, sourceEnd - SourceStart, Start, end - Start, ParentIndex, OccurrenceId, IsAdHoc);
    }
}
