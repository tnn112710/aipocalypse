using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.WeaponSystem;

namespace Aipocalypse.Core.Events
{
    // A held gun's repair QTE began: what the HUD needs to show it, so it never reaches into the gun.
    public readonly struct GunRepairQteStarted
    {
        public readonly IEquipmentOwner Owner;
        public readonly ItemInstance Item;
        public readonly IQteSession Session;
        // The gun's repair presentation preset; null = none.
        public readonly RepairFeedbackSettings Feedback;

        public GunRepairQteStarted(IEquipmentOwner owner, ItemInstance item, IQteSession session, RepairFeedbackSettings feedback)
        {
            Owner = owner;
            Item = item;
            Session = session;
            Feedback = feedback;
        }
    }
}
