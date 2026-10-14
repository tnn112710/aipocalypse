using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(fileName = "AmmoConfig", menuName = "Aipocalypse/Weapons/Ammo Config")]
    public sealed class AmmoConfig : ScriptableObject
    {
        [Serializable]
        public struct Stack
        {
            public AmmoType Type;
            [Min(0)] public int Amount;
        }

        [field: SerializeField, Tooltip("Reloading never runs the reserve down")]
        public bool Infinite { get; private set; }

        [SerializeField, Tooltip("Reserve at spawn until an inventory provides ammo")]
        List<Stack> _starting = new();

        public AmmoReserve CreateReserve()
        {
            var reserve = new AmmoReserve(Infinite);
            foreach (Stack stack in _starting)
                reserve.Add(stack.Type, stack.Amount);
            return reserve;
        }
    }
}
