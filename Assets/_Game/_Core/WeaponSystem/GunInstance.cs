using System;
using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // One gun the player owns. Rounds and jams stay with it through drop, pick up and swap.
    public sealed class GunInstance : ItemInstance
    {
        public GunInstance(GunDefinition definition) : base(definition)
        {
            Gun = definition;
            Rounds = definition.Magazine.Size;
        }

        // Rounds or jam changed.
        public event Action Changed;

        public GunDefinition Gun { get; }
        public int Rounds { get; private set; }
        public bool IsJammed { get; private set; }
        public bool IsFull => Rounds >= Gun.Magazine.Size;

        public void SetRounds(int rounds)
        {
            rounds = Mathf.Clamp(rounds, 0, Gun.Magazine.Size);
            if (rounds == Rounds)
                return;
            Rounds = rounds;
            Changed?.Invoke();
        }

        public void SetJammed(bool jammed)
        {
            if (jammed == IsJammed)
                return;
            IsJammed = jammed;
            Changed?.Invoke();
        }
    }
}
