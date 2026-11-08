namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Why an instance came into play, handed to its ISpawnable parts.
    public readonly struct SpawnInfo
    {
        // Null when a prefab was spawned without a card.
        public readonly SpawnCard Card;
        // Null for single spawns outside a request.
        public readonly SpawnHandle Handle;
        // Its slot in the group (0 = the leader) and the group's size.
        public readonly int Slot;
        public readonly int GroupSize;
        // The group's leader once it's in play; null for the leader itself and for single spawns.
        public readonly Spawnable Leader;

        public SpawnInfo(SpawnCard card, SpawnHandle handle, int slot, int groupSize, Spawnable leader)
        {
            Card = card;
            Handle = handle;
            Slot = slot;
            GroupSize = groupSize;
            Leader = leader;
        }
    }
}
