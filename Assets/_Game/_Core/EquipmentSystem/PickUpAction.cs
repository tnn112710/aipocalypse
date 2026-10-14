namespace Aipocalypse.Core.EquipmentSystem
{
    public enum PickUpAction
    {
        None,  // can't take it: no free slot accepts it and the selected slot doesn't either
        Store, // into the first free slot that accepts it
        Swap,  // replaces the selected item, which is dropped
    }
}
