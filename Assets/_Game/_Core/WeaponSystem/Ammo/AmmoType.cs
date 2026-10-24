using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // A kind of round. Guns that take the same type share one pool in the holder's AmmoReserve.
    [CreateAssetMenu(fileName = "Ammo", menuName = "Aipocalypse/Weapons/Ammo Type")]
    public sealed class AmmoType : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField, PreviewField(64)] Sprite _icon;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
    }
}
