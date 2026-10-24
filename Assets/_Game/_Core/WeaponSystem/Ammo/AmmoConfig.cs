using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(fileName = "AmmoConfig", menuName = "Aipocalypse/Weapons/Ammo Config")]
    public sealed class AmmoConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Reloading never runs the reserve down")]
        bool _infinite;

        public bool Infinite => _infinite;

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
