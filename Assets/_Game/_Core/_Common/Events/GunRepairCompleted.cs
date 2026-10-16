using Aipocalypse.Core.EquipmentSystem;

namespace Aipocalypse.Core.Events
{
    public readonly struct GunRepairCompleted
    {
        public readonly IEquipmentOwner Owner;
        public readonly ItemInstance Item;
        public readonly bool QteSuccess;

        public GunRepairCompleted(IEquipmentOwner owner, ItemInstance item, bool qteSuccess)
        {
            Owner = owner;
            Item = item;
            QteSuccess = qteSuccess;
        }
    }
}
