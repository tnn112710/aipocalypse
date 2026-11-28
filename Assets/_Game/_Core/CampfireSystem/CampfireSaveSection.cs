using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // Saves each campfire's fuel and whether it burns, and each wood pile's logs, by SaveId. A save without the entry keeps
    // the new-game state (pit full and unlit, pile full). Restored silently.
    public sealed class CampfireSaveSection : SaveSection
    {
        const string Key = "campfires";

        readonly CampfireRegistry _campfires;

        public CampfireSaveSection(SaveRegistry registry, CampfireRegistry campfires) : base(registry) => _campfires = campfires;

        public override int Order => SaveOrder.World;

        public override void Capture(SaveData save)
        {
            var saved = new CampfiresSave();
            foreach (Campfire fire in _campfires.Fires)
                if (fire != null && fire.TryGetComponent(out SaveId id) && id.HasId)
                    saved.Fires.Add(new FireSave { Id = id.Id, Fuel = fire.Fuel.Fuel, Lit = fire.Fuel.Lit });
            foreach (WoodPile pile in _campfires.Piles)
                if (pile != null && pile.TryGetComponent(out SaveId id) && id.HasId)
                    saved.Piles.Add(new PileSave { Id = id.Id, Stock = pile.Stock });
            save.Set(Key, saved);
        }

        public override void Restore(SaveData save)
        {
            if (!save.TryGet(Key, out CampfiresSave saved) || saved == null)
                return;
            foreach (Campfire fire in _campfires.Fires)
                if (fire != null && fire.TryGetComponent(out SaveId id) && id.HasId)
                    foreach (FireSave entry in saved.Fires)
                        if (entry?.Id == id.Id)
                            fire.RestoreSaved(entry.Fuel, entry.Lit);
            foreach (WoodPile pile in _campfires.Piles)
                if (pile != null && pile.TryGetComponent(out SaveId id) && id.HasId)
                    foreach (PileSave entry in saved.Piles)
                        if (entry?.Id == id.Id)
                            pile.SetStock(entry.Stock);
        }

        [Serializable]
        public sealed class CampfiresSave
        {
            public List<FireSave> Fires = new();
            public List<PileSave> Piles = new();
        }

        [Serializable]
        public sealed class FireSave
        {
            public string Id;
            public float Fuel;
            public bool Lit;
        }

        [Serializable]
        public sealed class PileSave
        {
            public string Id;
            public int Stock;
        }
    }
}
