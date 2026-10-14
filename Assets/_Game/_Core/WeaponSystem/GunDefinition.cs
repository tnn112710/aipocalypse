using Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Everything that makes one gun model shoot the way it does. Per-copy state (rounds, jam) lives in GunInstance.
    [CreateAssetMenu(fileName = "Gun", menuName = "Aipocalypse/Items/Gun")]
    public sealed class GunDefinition : EquippableDefinition
    {
        [field: SerializeField, FoldoutGroup("Fire", true), HideLabel, InlineProperty]
        public FireSettings Fire { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Bullet", true), HideLabel, InlineProperty]
        public BulletSettings Bullet { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Magazine", true), HideLabel, InlineProperty]
        public MagazineSettings Magazine { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Jam", true), HideLabel, InlineProperty]
        public JamSettings Jam { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Recoil"), HideLabel, InlineProperty]
        public RecoilSettings Recoil { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Handling"), HideLabel, InlineProperty]
        public HandlingSettings Handling { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Sounds"), HideLabel, InlineProperty]
        public GunSounds Sounds { get; private set; } = new();

        [field: SerializeField, Tooltip("Muzzle flash, tracer, impact and bullet hole; usually shared by many guns")]
        public BulletEffects Effects { get; private set; }

        public override ItemCategory Category => ItemCategory.Gun;

        public override ItemInstance CreateInstance() => new GunInstance(this);
    }
}
