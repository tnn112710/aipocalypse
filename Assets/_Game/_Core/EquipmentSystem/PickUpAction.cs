namespace Aipocalypse.Core.EquipmentSystem
{
    public enum PickUpAction
    {
        None,  // can't take it: no free slot accepts it and the selected slot doesn't either
        Merge, // onto a stack of the same item already carried
        Store, // into the first free slot that accepts it
        Swap,  // replaces the selected item, which is dropped
    }
}
