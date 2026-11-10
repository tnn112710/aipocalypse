using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // Saves each fence segment's and gate's health, broken state and whether the gate stands open, by its SaveId.
    // Upgrade tiers aren't saved yet (only the console upgrades today).
    public sealed class DefenseSaveSection : SaveSection
    {
        const string Key = "defense";

        readonly BarrierRegistry _barriers;

        public DefenseSaveSection(SaveRegistry registry, BarrierRegistry barriers) : base(registry) => _barriers = barriers;

        public override int Order => SaveOrder.World;

        public override void Capture(SaveData save)
        {
            var saved = new List<BarrierSave>();
            foreach (BarrierSegment segment in _barriers.Segments)
                if (segment != null && segment.TryGetComponent(out SaveId id) && id.HasId)
                    saved.Add(new BarrierSave
                    {
                        Id = id.Id,
                        Health = segment.RemainingHealth,
                        Broken = segment.IsBroken,
                        GateOpen = segment.Gate != null && segment.Gate.IsOpen,
                    });
            save.Set(Key, saved);
        }

        public override void Restore(SaveData save)
        {
            if (!save.TryGet(Key, out List<BarrierSave> saved) || saved == null)
                return;
            var byId = new Dictionary<string, BarrierSave>(saved.Count);
            foreach (BarrierSave entry in saved)
                if (entry?.Id != null)
                    byId[entry.Id] = entry;
            foreach (BarrierSegment segment in _barriers.Segments)
                if (segment != null && segment.TryGetComponent(out SaveId id) && id.HasId && byId.TryGetValue(id.Id, out BarrierSave entry))
                    segment.RestoreSaved(entry.Health, entry.Broken, entry.GateOpen);
        }

        [Serializable]
        public sealed class BarrierSave
        {
            public string Id;
            public float Health;
            public bool Broken;
            public bool GateOpen;
        }
    }
}
