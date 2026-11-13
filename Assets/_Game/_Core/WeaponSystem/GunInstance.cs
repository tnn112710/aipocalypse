using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // One gun the player owns. Rounds, jams and fitted parts stay with it through drop, pick up and swap; its stat sheet
    // (Stats) is rebuilt from the model and its parts whenever a part changes.
    public sealed class GunInstance : ItemInstance
    {
        readonly ItemInstance[] _attachments = new ItemInstance[GunSlots.Count];

        public GunInstance(GunDefinition definition) : base(definition)
        {
            Gun = definition;
            Stats = new GunStats(definition, _attachments);
            Rounds = Stats.MagazineSize;
        }

        // A part was fitted or taken off (the stats, the model on the gun and the magazine size may differ).
        public event Action AttachmentsChanged;

        public GunDefinition Gun { get; }
        public GunStats Stats { get; private set; }
        public int Rounds { get; private set; }
        public bool IsJammed { get; private set; }
        // Its light part shines while it's in hand (switched with the gun light key); on until switched off.
        public bool LightOn { get; private set; } = true;
        public bool IsFull => Rounds >= Stats.MagazineSize;
        public bool IsLow => Rounds <= Gun.Magazine.LowRoundsOf(Stats.MagazineSize);
        // Rounds in the magazine over the owner's reserve of its ammo; flagged when running low, empty or jammed.
        public override ItemBadge Badge => new(Rounds, IsLow || IsJammed, Gun.Magazine.AmmoType);
        public override float Weight => Stats.Weight;
        public override IReadOnlyList<ItemInstance> Attachments => _attachments;

        public override long Value
        {
            get
            {
                long value = base.Value;
                foreach (ItemInstance part in _attachments)
                    if (part != null)
                        value += part.Value;
                return value;
            }
        }

        public ItemInstance AttachmentIn(GunSlot slot) => _attachments[(int)slot];

        public bool Fits(ItemInstance part) => part != null && part.Definition is AttachmentDefinition attachment && attachment.Fits(Gun);

        // Fits `part` in its slot; `previous` is the part it replaced (or null). False (nothing changed) if it doesn't fit.
        public bool TryAttach(ItemInstance part, out ItemInstance previous)
        {
            previous = null;
            if (!Fits(part) || Array.IndexOf(_attachments, part) >= 0)
                return false;
            int slot = (int)((AttachmentDefinition)part.Definition).Slot;
            previous = _attachments[slot];
            _attachments[slot] = part;
            OnAttachmentsChanged();
            return true;
        }

        // Takes the part out of `slot`; null if it was empty.
        public ItemInstance Detach(GunSlot slot)
        {
            ItemInstance part = _attachments[(int)slot];
            if (part == null)
                return null;
            _attachments[(int)slot] = null;
            OnAttachmentsChanged();
            return part;
        }

        // The stat sheet as it would be with `part` in `slot` (null: the slot emptied), for previews and checks.
        public GunStats StatsWith(GunSlot slot, ItemInstance part)
        {
            var parts = (ItemInstance[])_attachments.Clone();
            parts[(int)slot] = part;
            return new GunStats(Gun, parts);
        }

        // Rounds over the magazine's size (a bigger magazine came off), taken out for the caller to put back as ammo.
        public int TakeExcessRounds()
        {
            int excess = Rounds - Stats.MagazineSize;
            if (excess <= 0)
                return 0;
            Rounds = Stats.MagazineSize;
            RaiseChanged();
            return excess;
        }

        public override void WriteSave(ItemSave save)
        {
            save.Rounds = Rounds;
            save.Jammed = IsJammed;
        }

        public override void ReadSave(ItemSave save)
        {
            SetRounds(save.Rounds);
            SetJammed(save.Jammed);
        }

        protected internal override void RestoreAttachment(ItemInstance attachment) => TryAttach(attachment, out _);

        public void SetRounds(int rounds)
        {
            rounds = Mathf.Clamp(rounds, 0, Stats.MagazineSize);
            if (rounds == Rounds)
                return;
            Rounds = rounds;
            RaiseChanged();
        }

        public void ToggleLight() => LightOn = !LightOn;

        public void SetJammed(bool jammed)
        {
            if (jammed == IsJammed)
                return;
            IsJammed = jammed;
            RaiseChanged();
        }

        void OnAttachmentsChanged()
        {
            Stats = new GunStats(Gun, _attachments);
            AttachmentsChanged?.Invoke();
            RaiseChanged();
        }
    }
}
