using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>Layout-bound scope membership, independent of paints, cameras, and material batching.</summary>
    internal sealed class TextStudio2DPainterScopeBinding
    {
        readonly List<Scope> _scopes = new List<Scope>();
        readonly int[] _firstUnits = new int[TextStudio2DHeroProgram.MaximumOperations];
        readonly int[] _scopeIndices = new int[TextStudio2DHeroProgram.MaximumOperations];
        readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
        int _unitStart, _unitCount;
        internal string Diagnostic { get; private set; } = string.Empty;

        internal int FirstUnit(int operationIndex) => _firstUnits[operationIndex];
        internal bool Contains(int operationIndex, int unit) => (uint)(unit - _unitStart) < (uint)_unitCount &&
            _scopes[_scopeIndices[operationIndex]].Members[unit - _unitStart];

        internal long ManagedPayloadBytes
        {
            get
            {
                long bytes = TextStudioAppearanceManagedMemory.ArrayBytes(_firstUnits) + TextStudioAppearanceManagedMemory.ArrayBytes(_scopeIndices) +
                    (long)_scopes.Capacity * IntPtr.Size;
                foreach (Scope scope in _scopes) bytes += TextStudioAppearanceManagedMemory.ArrayBytes(scope.Members);
                return bytes;
            }
        }

        internal bool Build(in TextStudioFrame frame, TextStudio2DHeroProgram program, IReadOnlyList<int> drawingUnits)
        {
            Diagnostic = string.Empty;
            _selectorPass.Begin();
            Array.Fill(_firstUnits, -1);
            _unitStart = 0;
            _unitCount = frame.VisualSnapshot.GlyphCount;
            bool scoped = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null;
            if (scoped)
            {
                TextStudioAppearanceRange domain = frame.AppearanceBinding.Plan.Occurrences[frame.AppearanceOccurrenceIndex].Domain;
                _unitStart = Math.Clamp(domain.Start, 0, _unitCount);
                _unitCount = Math.Clamp(domain.End, _unitStart, _unitCount) - _unitStart;
            }
            int count = 0;
            for (int index = 0; index < program.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref program.Style.OperationAt(index);
                if (operation.Kind != TextStudio2DLayerKind.Group) continue;
                if (count == _scopes.Count) _scopes.Add(new Scope());
                _scopeIndices[index] = count;
                Scope scope = _scopes[count++];
                scope.Order = program.GetPainterOrder(index);
                int unitCount = _unitCount;
                if (scope.Members.Length < unitCount)
                    scope.Members = new bool[TextStudioBufferCapacity.ForCount(unitCount)];
                Array.Clear(scope.Members, 0, scope.Members.Length);
                TextStudio2DCompiledSelector selector = program.Style.GetSelector(operation.SelectorIndex);
                for (int local = 0; local < unitCount; local++)
                {
                    int unit = _unitStart + local;
                    scope.Members[local] = scoped
                        ? frame.AppearanceBinding.IncludesOperation(frame.AppearanceOccurrenceIndex, index, unit)
                        : TextStudio2DSelectorEvaluator.Evaluate(in selector, in frame, unit, _selectorPass);
                }
                for (int draw = 0; draw < drawingUnits.Count; draw++)
                    if (Contains(index, drawingUnits[draw])) { _firstUnits[index] = drawingUnits[draw]; break; }
            }
            for (int first = 0; first < count; first++)
            {
                for (int second = first + 1; second < count; second++)
                {
                    Scope a = _scopes[first];
                    Scope b = _scopes[second];
                    if (a.Order == b.Order) continue;
                    bool overlap = false;
                    bool aOnly = false;
                    bool bOnly = false;
                    for (int unit = 0; unit < _unitCount; unit++)
                    {
                        overlap |= a.Members[unit] && b.Members[unit];
                        aOnly |= a.Members[unit] && !b.Members[unit];
                        bOnly |= b.Members[unit] && !a.Members[unit];
                    }
                    if (!overlap || !aOnly || !bOnly) continue;
                    Diagnostic = "Groups with different painter orders partially overlap. Make their selections disjoint, nest one range inside the other, or use the same painter order.";
                    return false;
                }
            }
            while (_scopes.Count > count) _scopes.RemoveAt(_scopes.Count - 1);
            return true;
        }

        sealed class Scope
        {
            internal TextStudio2DPainterOrder Order;
            internal bool[] Members = Array.Empty<bool>();
        }
    }
}
