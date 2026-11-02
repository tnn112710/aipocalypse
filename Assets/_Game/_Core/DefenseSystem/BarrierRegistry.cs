using System.Collections.Generic;

namespace Aipocalypse.Core.DefenseSystem
{
    // The scene's barriers (fence segments and gates) while enabled: for console commands and, later, the morning report.
    public sealed class BarrierRegistry
    {
        readonly List<BarrierSegment> _segments = new();

        public IReadOnlyList<BarrierSegment> Segments => _segments;

        public void Add(BarrierSegment segment)
        {
            if (segment != null && !_segments.Contains(segment))
                _segments.Add(segment);
        }

        public void Remove(BarrierSegment segment) => _segments.Remove(segment);
    }
}
