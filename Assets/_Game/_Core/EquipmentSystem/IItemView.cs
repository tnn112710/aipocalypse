namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A model that shows an item copy's own state (a gun's fitted parts): on its world model (WorldItem) and in the
    // inventory's 3D view.
    public interface IItemView
    {
        void ShowItem(ItemInstance item);
    }
}
