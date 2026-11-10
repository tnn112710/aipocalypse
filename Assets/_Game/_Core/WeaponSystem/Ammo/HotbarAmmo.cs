using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    public static class HotbarAmmo
    {
        // The ammo types the guns in the hotbar load, each once, into `types` (cleared first).
        public static void TypesInUse(ItemContainer hotbar, List<AmmoType> types)
        {
            types.Clear();
            if (hotbar == null)
                return;
            for (int i = 0; i < hotbar.Count; i++)
                if (hotbar[i] is GunInstance gun && gun.Gun.Magazine.AmmoType != null && !types.Contains(gun.Gun.Magazine.AmmoType))
                    types.Add(gun.Gun.Magazine.AmmoType);
        }
    }
}
