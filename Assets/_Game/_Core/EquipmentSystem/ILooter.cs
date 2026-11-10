namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Whoever can search a LootContainer (the player): its key press hands the container over.
    public interface ILooter
    {
        void BeginLoot(LootContainer container);
    }
}
