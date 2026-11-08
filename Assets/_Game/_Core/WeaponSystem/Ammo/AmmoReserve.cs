using System;
using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Spare rounds the holder carries, by ammo type. Guns reload from it.
    public sealed class AmmoReserve
    {
        readonly Dictionary<AmmoType, int> _counts = new();
        bool _infinite;

        // The type whose count changed; null when every count may have (infinite toggled).
        public event Action<AmmoType> Changed;

        public AmmoReserve(bool infinite = false) => _infinite = infinite;

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

        public int Count(AmmoType type) => type != null && _counts.TryGetValue(type, out int count) ? count : 0;

        public bool Has(AmmoType type) => type != null && (_infinite || Count(type) > 0);

        public void Add(AmmoType type, int amount)
        {
            if (type == null || amount <= 0)
                return;
            _counts[type] = Count(type) + amount;
            Changed?.Invoke(type);
        }

        // Returns how many were taken (all of them when infinite).
        public int Take(AmmoType type, int amount)
        {
            if (type == null || amount <= 0)
                return 0;
            if (_infinite)
                return amount;

            int taken = Math.Min(amount, Count(type));
            if (taken == 0)
                return 0;
            _counts[type] -= taken;
            Changed?.Invoke(type);
            return taken;
        }
    }
}
