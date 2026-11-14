using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // One gun copy's final stat sheet: the model's values changed by its parts, Duckov-style (final = base × (1 + Σ% / 100),
    // so two -20 % parts make -40 %). Rebuilt when its parts change, never per frame. Rarity and ammo add their own
    // percentages here later.
    public sealed class GunStats
    {
        public const int StatCount = (int)GunStat.HipSpread + 1;
        const float MinPercent = -90f;
        const float MaxPercent = 300f;
        // Seconds to about 95 % of the aim pose at an aim speed of 1 (the pose eases by Damping.ByRate).
        const float AimTimePerRate = 3f;

        readonly float[] _percent = new float[StatCount];

        public GunStats(GunDefinition gun, IReadOnlyList<ItemInstance> parts)
        {
            Gun = gun;
            float partsWeight = 0f;
            float loudness = 1f;
            if (parts != null)
            {
                foreach (ItemInstance part in parts)
                {
                    if (part?.Definition is not AttachmentDefinition attachment)
                        continue;
                    partsWeight += part.Weight;
                    foreach (GunStatModifier modifier in attachment.Modifiers)
                        _percent[(int)modifier.Stat] += modifier.Percent;
                    if (attachment.Slot == GunSlot.Optic)
                        Optic = attachment;
                    else if (attachment.Slot == GunSlot.Muzzle)
                        Muzzle = attachment;
                    HidesFlash |= attachment.HidesFlash;
                    loudness *= attachment.Loudness;
                }
            }

            Damage = Get(GunStat.Damage);
            RoundsPerMinute = Get(GunStat.FireRate);
            MagazineSize = Mathf.RoundToInt(Get(GunStat.MagazineSize));
            ReloadTime = Get(GunStat.ReloadTime);
            float range = Scale(GunStat.Range);
            Range = gun.Bullet.Range * range;
            Falloff = new DamageFalloff { Start = gun.Fire.Falloff.Start * range, End = gun.Fire.Falloff.End * range, MinShare = gun.Fire.Falloff.MinShare };
            BulletSpeed = Get(GunStat.BulletSpeed);
            VerticalRecoilScale = Scale(GunStat.VerticalRecoil);
            HorizontalRecoilScale = Scale(GunStat.HorizontalRecoil);
            AimSpeed = gun.Handling.AimSpeed / Scale(GunStat.AimTime);
            SpreadScale = Scale(GunStat.Spread);
            SpreadAngle = gun.Fire.SpreadAngle * SpreadScale;
            MoveSpeedScale = Scale(GunStat.MoveSpeed);
            HipSpread = Get(GunStat.HipSpread);
            AimSpread = gun.Accuracy.AimSpread;
            AimZoom = Optic != null ? Mathf.Min(gun.Handling.AimZoom, 1f / Optic.Optic.Magnification) : gun.Handling.AimZoom;
            Weight = gun.Weight + partsWeight;
            Loudness = loudness;
        }

        public GunDefinition Gun { get; }
        // The fitted optic, or null (iron sights).
        public AttachmentDefinition Optic { get; }
        // The fitted muzzle part, or null.
        public AttachmentDefinition Muzzle { get; }
        public bool HidesFlash { get; }
        // How far its shots carry, as a share of the bare gun's (ShotFired.Loudness).
        public float Loudness { get; }

        // The shot sound and its tail: a suppressor's, else the gun's own.
        public SoundID FireSound => Muzzle != null && Muzzle.FireSound.IsValid() ? Muzzle.FireSound : Gun.Sounds.Fire;
        public SoundID FireTailSound => Muzzle != null && Muzzle.FireSound.IsValid() ? Muzzle.FireTailSound : Gun.Sounds.FireTail;

        public float Damage { get; }
        public float RoundsPerMinute { get; }
        public int MagazineSize { get; }
        public float ReloadTime { get; }
        public float Range { get; }
        public DamageFalloff Falloff { get; }
        public float BulletSpeed { get; }
        public float VerticalRecoilScale { get; }
        public float HorizontalRecoilScale { get; }
        // How fast the gun moves into the aim pose (the asset's Handling.AimSpeed, slowed by aim-time parts).
        public float AimSpeed { get; }
        public float AimTime => Get(GunStat.AimTime);
        // Multiplies pellet spread and airborne spread.
        public float SpreadScale { get; }
        public float SpreadAngle { get; }
        public float MoveSpeedScale { get; }
        // The hip wobble's cone (degrees) from the hip, and fully aimed (GunAccuracy).
        public float HipSpread { get; }
        public float AimSpread { get; }
        // Field of view multiplier when fully aimed: the gun's own, or the optic's magnification if that zooms further.
        public float AimZoom { get; }
        // The gun and its parts, in kg.
        public float Weight { get; }

        // The sum of every percentage on the stat, clamped so no combination can zero or explode it.
        public float Percent(GunStat stat) => Mathf.Clamp(_percent[(int)stat], MinPercent, MaxPercent);

        public float Scale(GunStat stat) => 1f + Percent(stat) / 100f;

        // The cone at this aim weight (0 hip .. 1 aimed).
        public float ConeAt(float aimWeight) => Mathf.Lerp(HipSpread, AimSpread, aimWeight);

        // The stat as the inspector shows it, with the parts; GetBase without them.
        public float Get(GunStat stat) => Value(Gun, stat, Scale(stat));
        public float GetBase(GunStat stat) => Value(Gun, stat, 1f);

        public static bool HigherIsBetter(GunStat stat) => stat is GunStat.Damage or GunStat.FireRate or GunStat.MagazineSize
            or GunStat.Range or GunStat.BulletSpeed or GunStat.MoveSpeed;

        static float Value(GunDefinition gun, GunStat stat, float scale) => stat switch
        {
            GunStat.Damage => gun.Fire.Damage * scale,
            GunStat.FireRate => gun.Fire.RoundsPerMinute * scale,
            GunStat.MagazineSize => Mathf.Max(1, Mathf.RoundToInt(gun.Magazine.Size * scale)),
            GunStat.ReloadTime => gun.Magazine.ReloadTime * scale,
            // Shown as the distance of full damage.
            GunStat.Range => gun.Fire.Falloff.Start * scale,
            GunStat.BulletSpeed => gun.Bullet.Speed * scale,
            // Degrees per shot at the hip.
            GunStat.VerticalRecoil => gun.Recoil.Hip.Vertical * scale,
            GunStat.HorizontalRecoil => gun.Recoil.Hip.Horizontal * scale,
            GunStat.AimTime => gun.Handling.AimSpeed > 0f ? AimTimePerRate * scale / gun.Handling.AimSpeed : 0f,
            GunStat.Spread => gun.Fire.SpreadAngle * scale,
            // Percent of normal walking speed.
            GunStat.MoveSpeed => 100f * scale,
            GunStat.HipSpread => gun.Accuracy.HipSpread * scale,
            _ => 0f,
        };
    }
}
