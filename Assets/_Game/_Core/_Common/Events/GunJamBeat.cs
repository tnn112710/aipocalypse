using Aipocalypse.Core.EquipmentSystem;

namespace Aipocalypse.Core.Events
{
    public enum GunJamBeatKind { Jammed, DryFire, Slap, Punch, Miss }

    // Presentation beats of a jam and its repair, for camera and screen feedback.
    public readonly struct GunJamBeat
    {
        public readonly IEquipmentOwner Owner;
        public readonly ItemInstance Item;
        public readonly GunJamBeatKind Kind;

        public GunJamBeat(IEquipmentOwner owner, ItemInstance item, GunJamBeatKind kind)
        {
            Owner = owner;
            Item = item;
            Kind = kind;
        }
    }
}
