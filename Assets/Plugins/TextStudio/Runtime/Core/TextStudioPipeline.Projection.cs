using System;
using System.Collections.Generic;

namespace TextStudio
{
    internal sealed partial class TextStudioPipeline
    {
        ITextStudioTextProjectionBackend _projectionBackend;
        int _projectionRevision;

        internal bool SynchronizeTextProjectionBackend(ITextStudioTextProjectionBackend backend)
        {
            int revision = backend?.TextProjectionRevision ?? 0;
            if (ReferenceEquals(_projectionBackend, backend) && _projectionRevision == revision)
                return false;
            _projectionBackend = backend;
            _projectionRevision = revision;
            Invalidate();
            return true;
        }

        TextStudioTextProjection GetTextProjection(string text)
        {
            return _projectionBackend != null &&
                _projectionBackend.TryGetTextProjection(text, out TextStudioTextProjection projection) &&
                projection != null && string.Equals(projection.SourceText, text, StringComparison.Ordinal)
                ? projection : null;
        }

        void ApplyTextProjection()
        {
            TextStudioTextProjection projection = GetTextProjection(_plan.DisplayText);
            if (projection == null || string.Equals(projection.ResolvedText, _plan.ResolvedText, StringComparison.Ordinal))
                return;

            int oldLength = _plan.ResolvedText.Length;
            int displayLength = _plan.DisplayText.Length;
            int[] displayMap = new int[displayLength];
            int[] boundaryMap = new int[oldLength + 1];
            Array.Fill(displayMap, -1);
            Array.Fill(boundaryMap, -1);
            List<int> sourceIndices = new List<int>(projection.ResolvedText.Length);
            List<TextStudioTextUnit> atoms = new List<TextStudioTextUnit>();

            for (int i = 0; i < projection.SpanCount; i++)
            {
                projection.TryGetSpan(i, out TextStudioTextProjectionSpan span);
                GetProjectedSourceRange(span, out int sourceStart, out int sourceLength);
                for (int character = 0; character < span.ResolvedLength; character++)
                    sourceIndices.Add(sourceStart + (span.SourceLength == span.ResolvedLength && !span.IsAtomic ? character : 0));

                for (int character = 0; character < span.SourceLength; character++)
                {
                    int displayIndex = span.SourceStart + character;
                    int projectedIndex = span.ResolvedStart +
                        (span.SourceLength == span.ResolvedLength && !span.IsAtomic ? character : 0);
                    displayMap[displayIndex] = projectedIndex;
                    int resolvedIndex = _plan.GetResolvedIndexForDisplayChar(displayIndex);
                    if (resolvedIndex >= 0 && resolvedIndex < oldLength)
                        boundaryMap[resolvedIndex] = projectedIndex;
                }
                if (span.IsAtomic || span.SourceLength == 0)
                    atoms.Add(new TextStudioTextUnit(0, span.ResolvedStart, span.ResolvedLength,
                        sourceStart, sourceLength, span.IsAtomic));
            }

            // Removed delimiters/indentation inherit the next boundary; the final boundary remains the complete text.
            int nextBoundary = projection.ResolvedText.Length;
            for (int i = oldLength; i >= 0; i--)
            {
                if (boundaryMap[i] < 0) boundaryMap[i] = nextBoundary;
                else nextBoundary = boundaryMap[i];
            }

            _plan.ResolvedText = projection.ResolvedText;
            _plan.UnprojectedResolvedCharToResolvedBoundary = boundaryMap;
            _plan.IdentityUnitCount = 0;
            _resolvedSourceIndices.Clear();
            _resolvedSourceIndices.AddRange(sourceIndices);
            _displayResolvedIndices.Clear();
            _displayResolvedIndices.AddRange(displayMap);
            TextStudioTextUnitBuilder.Build(_plan, _resolvedSourceIndices);
            foreach (TextStudioTextUnit atom in atoms)
            {
                int unit = _plan.GetUnitIndexForResolvedChar(atom.resolvedStart);
                if (unit >= 0 && unit < _plan.TextUnitCount)
                    _plan.TextUnits[unit] = new TextStudioTextUnit(unit, atom.resolvedStart,
                        atom.resolvedLength, atom.sourceStart, atom.sourceLength, atom.isSourceAtom);
            }
            BuildDisplayCharacterMap();
        }

        void GetProjectedSourceRange(TextStudioTextProjectionSpan span, out int start, out int length)
        {
            start = -1;
            int end = -1;
            for (int i = span.SourceStart; i < span.SourceStart + span.SourceLength; i++)
            {
                int resolved = _plan.GetResolvedIndexForDisplayChar(i);
                if (resolved < 0 || resolved >= _plan.ResolvedText.Length) continue;
                int unitIndex = _plan.GetUnitIndexForResolvedChar(resolved);
                if (unitIndex < 0 || unitIndex >= _plan.TextUnitCount) continue;
                TextStudioTextUnit unit = _plan.TextUnits[unitIndex];
                int source = resolved < _resolvedSourceIndices.Count ? _resolvedSourceIndices[resolved] : resolved;
                if (start < 0) start = source;
                end = Math.Max(end, unit.isSourceAtom ? unit.SourceEnd : source + 1);
            }
            if (start < 0)
            {
                int resolved = span.SourceStart < _plan.DisplayText.Length
                    ? _plan.GetResolvedIndexForDisplayChar(span.SourceStart) : _plan.ResolvedText.Length;
                start = resolved >= 0 && resolved < _resolvedSourceIndices.Count
                    ? _resolvedSourceIndices[resolved] : _plan.CanonicalText.Length;
                end = start;
            }
            length = Math.Max(0, end - start);
        }
    }
}
