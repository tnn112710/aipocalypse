using System;
using MB13.Aipocalypse.Core.EquipmentSystem;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Spare rounds the holder carries: the AmmoType stacks in its inventory. Guns reload from it.
    public sealed class AmmoReserve
    {
        readonly Inventory _inventory;
        bool _infinite;

        // The type whose count changed; null when every count may have (anything carried changed, infinite toggled).
        public event Action<AmmoType> Changed;

        public AmmoReserve(Inventory inventory, bool infinite = false)
        {
            _inventory = inventory;
            _infinite = infinite;
            _inventory.Changed += OnInventoryChanged;
        }

        // Reloading never runs the reserve down.
        public bool Infinite
        {
            get => _infinite;
            set
            {
                if (value == _infinite)
                    return;
                _infinite = value;
                Changed?.Invoke(null);
            }
        }

        public int Count(AmmoType type) => type != null ? _inventory.CountOf(type) : 0;

        public bool Has(AmmoType type) => type != null && (_infinite || Count(type) > 0);

        // Puts new rounds in the inventory. Returns how many fit.
        public int Add(AmmoType type, int amount) => type == null || amount <= 0 ? 0 : _inventory.AddNew(type, amount);

        // Returns how many were taken (all of them when infinite).
        public int Take(AmmoType type, int amount)
        {
            if (type == null || amount <= 0)
                return 0;
            return _infinite ? amount : _inventory.Take(type, amount);
        }

        void OnInventoryChanged() => Changed?.Invoke(null);
    }
}
