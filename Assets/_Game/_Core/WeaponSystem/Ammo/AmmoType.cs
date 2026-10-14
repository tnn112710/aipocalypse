using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // A kind of round. Guns that take the same type share one pool in the holder's AmmoReserve.
    [CreateAssetMenu(fileName = "Ammo", menuName = "Aipocalypse/Items/Ammo Type")]
    public sealed class AmmoType : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(64)] public Sprite Icon { get; private set; }
    }
}
