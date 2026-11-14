using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // A gun part: an item carried in the backpack and fitted to a gun's slot from the inventory screen. It changes the
    // gun's stats by percentages and shows its Model on the gun's mount for that slot.
    [CreateAssetMenu(fileName = "Attachment", menuName = "Aipocalypse/Weapons/Attachment")]
    public sealed class AttachmentDefinition : ItemDefinition
    {
        [SerializeField] GunSlot _slot;

        [SerializeField, Tooltip("Gun families it fits (a generic part)")] GunFamily _families;

        [SerializeField, Tooltip("Exact guns it fits whatever their family (a gun-specific part)")]
        List<GunDefinition> _guns = new();

        [SerializeField, Tooltip("Only guns that load this ammo (magazines are made for one calibre). Empty = any")]
        AmmoType _ammo;

        [SerializeField, Tooltip("Percentages added to the gun's stats")] List<GunStatModifier> _modifiers = new();

        [SerializeField, Tooltip("Shown on the gun's mount for this slot; its pivot sits on the mount")]
        AttachmentModel _model;

        [SerializeField, ShowIf(nameof(IsOptic)), InlineProperty, HideLabel, BoxGroup("Optic")]
        OpticSettings _optic = new();

        [SerializeField, ShowIf(nameof(IsMagazine)), Tooltip("Scales the gun's magazine model (no model of its own): a longer box")]
        Vector3 _magazineScale = Vector3.one;

        [SerializeField, ShowIf(nameof(IsMuzzle)), Tooltip("No muzzle flash (suppressors)")] bool _hidesFlash;

        [SerializeField, ShowIf(nameof(IsMuzzle)), Tooltip("Replaces the gun's shot sound (suppressors). Unset = the gun's own")]
        SoundID _fireSound;

        [SerializeField, ShowIf(nameof(IsMuzzle)), Tooltip("The shot's tail while Fire Sound replaces the shot. Unset = no tail")]
        SoundID _fireTailSound;

        [SerializeField, ShowIf(nameof(IsMuzzle)), Range(0f, 2f), Tooltip("How far shots carry, as a share of the gun's own " +
            "(who reacts to them: animals' scare radius). 1 = unchanged")]
        float _loudness = 1f;

        [SerializeField, ShowIf(nameof(IsTactical)), Tooltip("Played when the gun light key switches its light or laser")]
        SoundID _switchSound;

        public GunSlot Slot => _slot;
        public GunFamily Families => _families;
        public IReadOnlyList<GunDefinition> Guns => _guns;
        public AmmoType Ammo => _ammo;
        public IReadOnlyList<GunStatModifier> Modifiers => _modifiers;
        public AttachmentModel Model => _model;
        public OpticSettings Optic => _optic;
        public Vector3 MagazineScale => _magazineScale;
        public bool HidesFlash => _hidesFlash;
        public SoundID FireSound => _fireSound;
        public SoundID FireTailSound => _fireTailSound;
        public float Loudness => _loudness;
        public SoundID SwitchSound => _switchSound;

        public override ItemCategory Category => ItemCategory.Attachment;

        bool IsOptic => _slot == GunSlot.Optic;
        bool IsMagazine => _slot == GunSlot.Magazine;
        bool IsMuzzle => _slot == GunSlot.Muzzle;
        bool IsTactical => _slot == GunSlot.Tactical;

        public bool Fits(GunDefinition gun)
        {
            if (gun == null || !gun.HasSlot(_slot))
                return false;
            if (_guns.Contains(gun))
                return true;
            return (_families & gun.Family) != 0 && (_ammo == null || _ammo == gun.Magazine.AmmoType);
        }
    }

    [Serializable]
    public sealed class OpticSettings
    {
        [Min(1f), Tooltip("1 = no magnification (the gun's own aim zoom); 2 = the world view halves")]
        public float Magnification = 1f;

        [Tooltip("Metres the gun sits further from the eye while aiming through it (negative = closer)")]
        public float EyeRelief;

        [Range(0.1f, 1f), Tooltip("Aim and model sway while aimed through it: magnification shows sway larger, so a scope calms it")]
        public float SwayScale = 1f;

        [Tooltip("A full scope: near full aim the scope view fades in over the screen and the held gun is hidden")]
        public bool FullScope;

        [ShowIf(nameof(FullScope)), Tooltip("Drawn in the middle of the scope view")] public Sprite ScopeReticle;

        [ShowIf(nameof(FullScope)), Min(0.001f), Tooltip("Metres the scope's sight line can be off the eye's (sway, kick) before " +
            "the scope view's edge goes fully dark on that side")]
        public float EyeBox = 0.012f;

        // How much of the scope view shows at this aim weight (0 hip .. 1 aimed): only at the very end of raising it.
        public float ScopeWeight(float aimWeight) => FullScope ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 0.98f, aimWeight)) : 0f;
    }
}
