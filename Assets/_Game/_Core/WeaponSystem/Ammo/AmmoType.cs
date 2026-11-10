using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // A kind of round, carried as stacks in the backpack. Guns that take the same type reload from the same stacks
    // (AmmoReserve). Its Prefab is the ammo box lying in the world.
    [CreateAssetMenu(fileName = "Ammo", menuName = "Aipocalypse/Weapons/Ammo Type")]
    public sealed class AmmoType : ItemDefinition
    {
        public override ItemCategory Category => ItemCategory.Ammo;
    }
}
