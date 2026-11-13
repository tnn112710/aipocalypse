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

        public int CountDamaged()
        {
            int count = 0;
            foreach (BarrierSegment segment in _segments)
                if (segment.NeedsRepair)
                    count++;
            return count;
        }

        // Mends every damaged barrier at once; a broken one with something alive in its line stays down. How many were mended.
        public int RepairAll()
        {
            int mended = 0;
            // By index: a restored barrier publishes BarrierRestored, and listeners mustn't break the loop.
            for (int i = 0; i < _segments.Count; i++)
                if (_segments[i].RepairFully())
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
