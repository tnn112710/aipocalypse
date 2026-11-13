using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // The stats parts (and later rarity and ammo) change, each by a percentage of the gun's own value.
    public enum GunStat
    {
        Damage,
        FireRate,
        MagazineSize,
        ReloadTime,
        // Bullet range and both damage falloff distances.
        Range,
        BulletSpeed,
        VerticalRecoil,
        HorizontalRecoil,
        // Time to raise the sights; lower is quicker.
        AimTime,
        // Pellet spread (shotguns) and airborne spread.
        Spread,
        // Walk and sprint speed while the gun is held.
        MoveSpeed,
        // The hip-fire cone (aiming shrinks it to the gun's aim spread). Added last: assets store the stat by index.
        HipSpread,
    }

    [Serializable]
    public struct GunStatModifier
    {
        public GunStat Stat;
        [Tooltip("Added to the stat's percentage: -10 = 10 % less. All of a gun's percentages add up before they apply")]
        public float Percent;

        public GunStatModifier(GunStat stat, float percent)
        {
            Stat = stat;
            Percent = percent;
        }
    }
}
