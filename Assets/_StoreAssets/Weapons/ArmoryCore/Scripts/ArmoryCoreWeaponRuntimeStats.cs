using System.Collections.Generic;
using UnityEngine;

namespace ArmoryCore
{
    public readonly struct ArmoryCoreWeaponRuntimeStats
    {
        public readonly float MassKilograms;
        public readonly float RoundsPerMinute;
        public readonly float MuzzleVelocityMetersPerSecond;
        public readonly float AccuracyMinutesOfAngle;
        public readonly float VerticalRecoilDegrees;
        public readonly float HorizontalRecoilDegrees;
        public readonly float AimTimeSeconds;
        public readonly float TacticalReloadSeconds;
        public readonly float EmptyReloadSeconds;
        public readonly float MeanRoundsBetweenFailures;
        public readonly bool Suppressed;
        public readonly float SoundPressureMultiplier;
        public readonly float MuzzleFlashMultiplier;
        public readonly float Magnification;
        public readonly float ZeroDistanceMeters;

        private ArmoryCoreWeaponRuntimeStats(
            float massKilograms,
            float roundsPerMinute,
            float muzzleVelocityMetersPerSecond,
            float accuracyMinutesOfAngle,
            float verticalRecoilDegrees,
            float horizontalRecoilDegrees,
            float aimTimeSeconds,
            float tacticalReloadSeconds,
            float emptyReloadSeconds,
            float meanRoundsBetweenFailures,
            bool suppressed,
            float soundPressureMultiplier,
            float muzzleFlashMultiplier,
            float magnification,
            float zeroDistanceMeters)
        {
            MassKilograms = massKilograms;
            RoundsPerMinute = roundsPerMinute;
            MuzzleVelocityMetersPerSecond = muzzleVelocityMetersPerSecond;
            AccuracyMinutesOfAngle = accuracyMinutesOfAngle;
            VerticalRecoilDegrees = verticalRecoilDegrees;
            HorizontalRecoilDegrees = horizontalRecoilDegrees;
            AimTimeSeconds = aimTimeSeconds;
            TacticalReloadSeconds = tacticalReloadSeconds;
            EmptyReloadSeconds = emptyReloadSeconds;
            MeanRoundsBetweenFailures = meanRoundsBetweenFailures;
            Suppressed = suppressed;
            SoundPressureMultiplier = soundPressureMultiplier;
            MuzzleFlashMultiplier = muzzleFlashMultiplier;
            Magnification = magnification;
            ZeroDistanceMeters = zeroDistanceMeters;
        }

        public static ArmoryCoreWeaponRuntimeStats Build(
            ArmoryCoreWeaponDefinition weapon,
            IReadOnlyList<ArmoryCoreAttachmentDefinition> attachments)
        {
            if (weapon == null)
                return default;

            var mass = weapon.weaponMassKilograms;
            var velocityMultiplier = weapon.muzzleVelocityMultiplier;
            var spreadMultiplier = 1f;
            var recoilMultiplier = 1f;
            var reloadMultiplier = 1f;
            var aimMultiplier = 1f;
            var reliabilityMultiplier = 1f;
            var suppressed = false;
            var soundMultiplier = 1f;
            var flashMultiplier = 1f;
            var magnification = 1f;
            var zeroDistance = 100f;

            if (attachments != null)
            {
                for (var i = 0; i < attachments.Count; i++)
                {
                    var attachment = attachments[i];
                    if (attachment == null)
                        continue;
                    mass += attachment.massKilograms;
                    velocityMultiplier *= attachment.muzzleVelocityMultiplier;
                    spreadMultiplier *= attachment.spreadMultiplier;
                    recoilMultiplier *= attachment.recoilMultiplier;
                    reloadMultiplier *= attachment.reloadTimeMultiplier;
                    aimMultiplier *= attachment.aimTimeMultiplier;
                    reliabilityMultiplier *= attachment.reliabilityMultiplier;
                    suppressed |= attachment.suppressesMuzzleBlast;
                    soundMultiplier *= attachment.soundPressureMultiplier;
                    flashMultiplier *= attachment.muzzleFlashMultiplier;
                    if (attachment.slot == ArmoryCoreAttachmentSlot.Sights
                        && attachment.GameplayMaximumMagnification > magnification)
                    {
                        magnification = attachment.GameplayMaximumMagnification;
                        zeroDistance = attachment.defaultZeroDistanceMeters;
                    }
                }
            }

            var ammunitionVelocity = weapon.ammunition == null
                ? 1f
                : weapon.ammunition.muzzleVelocityMetersPerSecond;
            return new ArmoryCoreWeaponRuntimeStats(
                Mathf.Max(0.01f, mass),
                Mathf.Max(1f, weapon.roundsPerMinute),
                ammunitionVelocity * velocityMultiplier,
                Mathf.Max(0.01f, weapon.accuracyMinutesOfAngle * spreadMultiplier),
                weapon.verticalRecoilDegrees * recoilMultiplier,
                weapon.horizontalRecoilDegrees * recoilMultiplier,
                weapon.aimDownSightsTime * aimMultiplier,
                weapon.tacticalReloadTime * reloadMultiplier,
                weapon.emptyReloadTime * reloadMultiplier,
                weapon.meanRoundsBetweenFailures * reliabilityMultiplier,
                suppressed,
                Mathf.Max(0f, soundMultiplier),
                Mathf.Max(0f, flashMultiplier),
                magnification,
                zeroDistance);
        }

        public static ArmoryCoreWeaponRuntimeStats Build(
            ArmoryCoreWeaponDefinition weapon,
            IReadOnlyList<ArmoryCoreAttachmentModule> modules)
        {
            if (weapon == null)
                return default;

            var mass = weapon.weaponMassKilograms;
            var roundsPerMinuteMultiplier = 1f;
            var velocityMultiplier = weapon.muzzleVelocityMultiplier;
            var spreadMultiplier = 1f;
            var verticalRecoilMultiplier = 1f;
            var horizontalRecoilMultiplier = 1f;
            var reloadMultiplier = 1f;
            var aimMultiplier = 1f;
            var reliabilityMultiplier = 1f;
            var suppressed = false;
            var soundMultiplier = 1f;
            var flashMultiplier = 1f;
            var magnification = 1f;
            var zeroDistance = 100f;

            if (modules != null)
            {
                for (var i = 0; i < modules.Count; i++)
                {
                    var module = modules[i];
                    if (module == null)
                        continue;
                    var attachment = module.Definition;
                    if (attachment != null)
                    {
                        mass += attachment.massKilograms;
                        velocityMultiplier *= attachment.muzzleVelocityMultiplier;
                        spreadMultiplier *= attachment.spreadMultiplier;
                        verticalRecoilMultiplier *= attachment.recoilMultiplier;
                        horizontalRecoilMultiplier *= attachment.recoilMultiplier;
                        reloadMultiplier *= attachment.reloadTimeMultiplier;
                        aimMultiplier *= attachment.aimTimeMultiplier;
                        reliabilityMultiplier *= attachment.reliabilityMultiplier;
                        suppressed |= attachment.suppressesMuzzleBlast;
                        soundMultiplier *= attachment.soundPressureMultiplier;
                        flashMultiplier *= attachment.muzzleFlashMultiplier;
                        if (attachment.slot == ArmoryCoreAttachmentSlot.Sights
                            && attachment.GameplayMaximumMagnification > magnification)
                        {
                            magnification = attachment.GameplayMaximumMagnification;
                            zeroDistance = attachment.defaultZeroDistanceMeters;
                        }
                    }

                    mass += module.AdditionalMassKilograms;
                    roundsPerMinuteMultiplier *= module.RoundsPerMinuteMultiplier;
                    velocityMultiplier *= module.MuzzleVelocityMultiplier;
                    spreadMultiplier *= module.SpreadMultiplier;
                    verticalRecoilMultiplier *= module.VerticalRecoilMultiplier;
                    horizontalRecoilMultiplier *= module.HorizontalRecoilMultiplier;
                    reloadMultiplier *= module.ReloadTimeMultiplier;
                    aimMultiplier *= module.AimTimeMultiplier;
                    reliabilityMultiplier *= module.ReliabilityMultiplier;
                    soundMultiplier *= module.SoundPressureMultiplier;
                    flashMultiplier *= module.MuzzleFlashMultiplier;
                    suppressed |= module.ForcesSuppressedShot;
                    if (module.OverridesOptic)
                    {
                        magnification = Mathf.Max(1f, module.CurrentMagnification);
                        zeroDistance = Mathf.Max(1f, module.CurrentZeroDistanceMeters);
                    }
                }
            }

            var ammunitionVelocity = weapon.ammunition == null
                ? 1f
                : weapon.ammunition.muzzleVelocityMetersPerSecond;
            return new ArmoryCoreWeaponRuntimeStats(
                Mathf.Max(0.01f, mass),
                Mathf.Max(1f, weapon.roundsPerMinute * roundsPerMinuteMultiplier),
                ammunitionVelocity * velocityMultiplier,
                Mathf.Max(0.01f, weapon.accuracyMinutesOfAngle * spreadMultiplier),
                weapon.verticalRecoilDegrees * verticalRecoilMultiplier,
                weapon.horizontalRecoilDegrees * horizontalRecoilMultiplier,
                weapon.aimDownSightsTime * aimMultiplier,
                weapon.tacticalReloadTime * reloadMultiplier,
                weapon.emptyReloadTime * reloadMultiplier,
                weapon.meanRoundsBetweenFailures * reliabilityMultiplier,
                suppressed,
                Mathf.Max(0f, soundMultiplier),
                Mathf.Max(0f, flashMultiplier),
                magnification,
                zeroDistance);
        }
    }
}
