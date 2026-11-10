using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(fileName = "AmmoConfig", menuName = "Aipocalypse/Weapons/Ammo Config")]
    public sealed class AmmoConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Reloading never runs the reserve down")]
        bool _infinite;

        public bool Infinite => _infinite;

        [SerializeField, Tooltip("Rounds of each type put in the backpack at spawn; the ammo crate tops up to these")]
        Dictionary<AmmoType, int> _starting = new();

        // Rounds of `type` a new reserve starts with.
        public int StartingAmount(AmmoType type) => type != null && _starting.TryGetValue(type, out int amount) ? amount : 0;

        // A reserve over `inventory`, with the starting rounds added to it.
        public AmmoReserve CreateReserve(Inventory inventory)
        {
            var reserve = new AmmoReserve(inventory, Infinite);
            foreach ((AmmoType type, int amount) in _starting)
                reserve.Add(type, amount);
            return reserve;
        }
    }
}
