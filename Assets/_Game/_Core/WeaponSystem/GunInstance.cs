using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // One gun the player owns. Rounds and jams stay with it through drop, pick up and swap.
    public sealed class GunInstance : ItemInstance
    {
        public GunInstance(GunDefinition definition) : base(definition)
        {
            Gun = definition;
            Rounds = definition.Magazine.Size;
        }

        public GunDefinition Gun { get; }
        public int Rounds { get; private set; }
        public bool IsJammed { get; private set; }
        public bool IsFull => Rounds >= Gun.Magazine.Size;
        public bool IsLow => Rounds <= Gun.Magazine.LowRounds;
        // Rounds in the magazine over the owner's reserve of its ammo; flagged when running low, empty or jammed.
        public override ItemBadge Badge => new(Rounds, IsLow || IsJammed, Gun.Magazine.AmmoType);

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

        public void SetRounds(int rounds)
        {
            rounds = Mathf.Clamp(rounds, 0, Gun.Magazine.Size);
            if (rounds == Rounds)
                return;
            Rounds = rounds;
            RaiseChanged();
        }

        public void SetJammed(bool jammed)
        {
            if (jammed == IsJammed)
                return;
            IsJammed = jammed;
            RaiseChanged();
        }
    }
}
