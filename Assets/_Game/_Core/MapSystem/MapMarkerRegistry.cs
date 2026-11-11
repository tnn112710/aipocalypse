using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.MapSystem
{
    // Every enabled, injected MapMarker, for the minimap to read.
    public sealed class MapMarkerRegistry
    {
        readonly List<MapMarker> _markers = new();

        public IReadOnlyList<MapMarker> Markers => _markers;

        public void Add(MapMarker marker)
        {
            if (marker != null && !_markers.Contains(marker))
                _markers.Add(marker);
        }

        public void Remove(MapMarker marker) => _markers.Remove(marker);
    }
}
