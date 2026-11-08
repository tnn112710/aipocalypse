namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // On the root of anything with a hotbar, so world items can hand themselves over without knowing who picks them up.
    public interface IEquipmentOwner
    {
        Equipment Equipment { get; }
    }
}
