namespace TextStudio
{
    /// <summary>Mapping between one semantic reveal unit and its resolved, display, source, and visible-glyph ranges.</summary>
    public readonly struct TextStudioTextUnit
    {
        public readonly int unitIndex;
        public readonly int resolvedStart;
        public readonly int resolvedLength;
        public readonly int sourceStart;
        public readonly int sourceLength;
        public readonly bool isSourceAtom;

        public int ResolvedEnd => resolvedStart + resolvedLength;
        public int SourceEnd => sourceStart + sourceLength;

        /// <summary>Creates a semantic-unit mapping with explicit UTF-16 and glyph range lengths.</summary>
        public TextStudioTextUnit(
            int unitIndex,
            int resolvedStart,
            int resolvedLength,
            int sourceStart,
            int sourceLength,
            bool isSourceAtom = false)
        {
            this.unitIndex = unitIndex;
            this.resolvedStart = resolvedStart;
            this.resolvedLength = resolvedLength;
            this.sourceStart = sourceStart;
            this.sourceLength = sourceLength;
            this.isSourceAtom = isSourceAtom;
        }
    }
}
