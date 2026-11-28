using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // Scene service: the campfires and wood piles in play (they add themselves when injected). Home = the first fire (the
    // yard's), which the HUD fuel bar shows.
    public sealed class CampfireRegistry
    {
        readonly List<Campfire> _fires = new();
        readonly List<WoodPile> _piles = new();

        public IReadOnlyList<Campfire> Fires => _fires;
        public IReadOnlyList<WoodPile> Piles => _piles;
        public Campfire Home => _fires.Count > 0 ? _fires[0] : null;

        public void Add(Campfire fire)
        {
            if (fire != null && !_fires.Contains(fire))
                _fires.Add(fire);
        }

        public void Remove(Campfire fire) => _fires.Remove(fire);

        public void Add(WoodPile pile)
        {
            if (pile != null && !_piles.Contains(pile))
                _piles.Add(pile);
        }

        public void Remove(WoodPile pile) => _piles.Remove(pile);
    }
}
