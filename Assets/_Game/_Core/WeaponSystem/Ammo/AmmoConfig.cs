using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(fileName = "AmmoConfig", menuName = "Aipocalypse/Weapons/Ammo Config")]
    public sealed class AmmoConfig : ScriptableObject
    {
        [field: SerializeField, Tooltip("Reloading never runs the reserve down")]
        public bool Infinite { get; private set; }

        [SerializeField, Tooltip("Reserve at spawn until an inventory provides ammo")]
        Dictionary<AmmoType, int> _starting = new();

        public AmmoReserve CreateReserve()
        {
            var reserve = new AmmoReserve(Infinite);
            foreach ((AmmoType type, int amount) in _starting)
                reserve.Add(type, amount);
            return reserve;
        }
    }
}
