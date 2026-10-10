namespace TextStudio
{
    /// <summary>Nested phrase-local ranges, ordered from enclosing to enclosed binding.</summary>
    internal sealed class TextStudioRangeScopeChain
    {
        internal readonly TextStudioTextRange Range;
        internal readonly TextStudioRangeScopeChain Child;

        internal TextStudioRangeScopeChain(TextStudioTextRange range, TextStudioRangeScopeChain child)
        {
            Range = range;
            Child = child;
        }

        internal void Resolve(ref int start, ref int count)
        {
            TextStudioRangeScopeChain current = this;
            while (current != null)
            {
                current.Range.Resolve(count, out int localStart, out int localEnd);
                start += localStart;
                count = localEnd - localStart;
                current = current.Child;
            }
        }
    }
}
