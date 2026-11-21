using System.Collections.Generic;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // The scene's barriers (fence segments and gates) while enabled: for Repair All, the repair highlight, console commands
    // and, later, the morning report.
    public sealed class BarrierRegistry
    {
        static readonly LocalizedString RepairAllName = Loc.Interaction("target.fences");
        static readonly List<string> RepairAllTargets = new();
        static Locale _repairAllLocale;

        readonly List<BarrierSegment> _segments = new();

        public IReadOnlyList<BarrierSegment> Segments => _segments;

        public void Add(BarrierSegment segment)
        {
            if (segment != null && !_segments.Contains(segment))
                _segments.Add(segment);
        }

        public void Remove(BarrierSegment segment) => _segments.Remove(segment);

        // Standing barriers that need repair: the ones Repair All mends.
        public int CountDamaged()
        {
            int count = 0;
            foreach (BarrierSegment segment in _segments)
                if (segment.NeedsRepair && !segment.IsBroken)
                    count++;
            return count;
        }

        // Mends every damaged standing barrier at once. Broken ones are rebuilt one at a time (R), so a fence that starts
        // down can't come back with one key. How many were mended.
        public int RepairAll()
        {
            int mended = 0;
            for (int i = 0; i < _segments.Count; i++)
                if (!_segments[i].IsBroken && _segments[i].RepairFully())
                    mended++;
            return mended;
        }

        // "Fences (5)", made once per count and language so a steady look allocates nothing.
        public static string RepairAllTarget(int damaged)
        {
            if (_repairAllLocale != Loc.Current)
            {
                RepairAllTargets.Clear();
                _repairAllLocale = Loc.Current;
            }
            while (RepairAllTargets.Count <= damaged)
                RepairAllTargets.Add(RepairAllName.Format(RepairAllTargets.Count));
            return RepairAllTargets[damaged];
        }
    }
}
