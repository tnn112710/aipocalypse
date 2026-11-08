using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Where a request's group appears in its area: points sampled in the area, placed by the kind's medium (SpawnPlacer), then
    // chosen as the player sees them (SpawnPointScorer): a hidden one, or once the caller allows it the visible one farthest
    // from the view centre. A group's whole footprint must be hidden.
    public sealed class SpawnPointPicker
    {
        readonly SpawnPlacer _placer;
        readonly SpawnVisibility _visibility;
        readonly List<SpawnCandidate> _candidates = new();

        public SpawnPointPicker(SpawnPlacer placer, SpawnVisibility visibility)
        {
            _placer = placer;
            _visibility = visibility;
        }

        // Points that could be placed on the last pick (0 = nothing in the area fits the kind).
        public int LastCandidates => _candidates.Count;

        // A spot in `area` for a group spreading `footprint` metres round it, or false while none fits yet (ask again later).
        public bool TryPick(ISpawnArea area, SpawnPlacement placement, SpawnRules rules, float footprint, bool allowVisible,
            out Vector3 point)
        {
            point = default;
            _candidates.Clear();
            Vector3 origin = area.Origin;
            bool hasView = _visibility.View != null;
            for (int i = 0; i < rules.Candidates; i++)
            {
                Vector3 sample = area.Sample(Random.value, Random.value);
                if (!_placer.TryPlace(sample, placement, rules, true, origin, Random.value, out Vector3 placed, out _))
                    continue;
                if (!hasView)
                {
                    _candidates.Add(new SpawnCandidate(placed, float.PositiveInfinity, false, 180f));
                    continue;
                }
                bool seen = _visibility.SeesFootprint(placed, footprint, rules, out float angle);
                _candidates.Add(new SpawnCandidate(placed, _visibility.PlayerDistance(placed), seen, angle));
            }
            int chosen = SpawnPointScorer.Choose(_candidates, rules, allowVisible, Random.value);
            if (chosen < 0)
                return false;
            point = _candidates[chosen].Position;
            return true;
        }
    }
}
