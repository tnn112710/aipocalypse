using Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Everything that makes one gun model shoot the way it does. Per-copy state (rounds, jam) lives in GunInstance.
    [CreateAssetMenu(fileName = "Gun", menuName = "Aipocalypse/Items/Gun")]
    public sealed class GunDefinition : EquippableDefinition
    {
        [SerializeField, FoldoutGroup("Fire", true), HideLabel, InlineProperty]
        FireSettings _fire = new();

        [SerializeField, FoldoutGroup("Bullet", true), HideLabel, InlineProperty]
        BulletSettings _bullet = new();

        [SerializeField, FoldoutGroup("Magazine", true), HideLabel, InlineProperty]
        MagazineSettings _magazine = new();

        [SerializeField, FoldoutGroup("Jam", true), HideLabel, InlineProperty]
        JamSettings _jam = new();

        [SerializeField, FoldoutGroup("Recoil"), HideLabel, InlineProperty]
        RecoilSettings _recoil = new();

        [SerializeField, FoldoutGroup("Accuracy"), HideLabel, InlineProperty]
        AccuracySettings _accuracy = new();

        [SerializeField, FoldoutGroup("Handling"), HideLabel, InlineProperty]
        HandlingSettings _handling = new();

        [SerializeField, FoldoutGroup("Sounds"), HideLabel, InlineProperty]
        GunSounds _sounds = new();

        [SerializeField, Tooltip("Muzzle flash, tracer, impact and bullet hole; usually shared by many guns")]
        BulletEffects _effects;

        public FireSettings Fire => _fire;
        public BulletSettings Bullet => _bullet;
        public MagazineSettings Magazine => _magazine;
        public JamSettings Jam => _jam;
        public RecoilSettings Recoil => _recoil;
        public AccuracySettings Accuracy => _accuracy;
        public HandlingSettings Handling => _handling;
        public GunSounds Sounds => _sounds;
        public BulletEffects Effects => _effects;

        public override ItemCategory Category => ItemCategory.Gun;

        public override ItemInstance CreateInstance() => new GunInstance(this);
    }
}
