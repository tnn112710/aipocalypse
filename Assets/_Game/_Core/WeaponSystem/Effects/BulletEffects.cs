using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // What shots look like. Lifetimes and caps are on each effect prefab's PooledEffect.
    [CreateAssetMenu(fileName = "BulletEffects", menuName = "Aipocalypse/Weapons/Bullet Effects")]
    public sealed class BulletEffects : ScriptableObject
    {
        [field: SerializeField] public PooledEffect MuzzleFlash { get; private set; }
        [field: SerializeField, Tooltip("Follows the bullet; returned when it hits or runs out of range")] public PooledEffect Tracer { get; private set; }
        [field: SerializeField] public PooledEffect Impact { get; private set; }
        [field: SerializeField, Tooltip("Decal left on surfaces; never on things that take damage")] public PooledEffect BulletHole { get; private set; }

        [field: SerializeField, Tooltip("Surfaces that get bullet holes")]
        public LayerMask BulletHoleLayers { get; private set; } = Physics.DefaultRaycastLayers;
        [field: SerializeField, Tooltip("Random scale of each hole")] public Vector2 BulletHoleScale { get; private set; } = new(0.9f, 1.1f);
        [field: SerializeField, Min(0f), Tooltip("Lift off the surface (m)")] public float BulletHoleOffset { get; private set; } = 0.005f;

        [field: SerializeField, Min(0f), Tooltip("Camera-origin shots: seconds the tracer takes to slide from the muzzle onto the bullet's real path")]
        public float TracerConvergeTime { get; private set; } = 0.08f;

        [field: Header("Prewarm (copies made when a gun is bound)"), SerializeField, Min(0)] public int MuzzleFlashPrewarm { get; private set; } = 4;
        [field: SerializeField, Min(0)] public int TracerPrewarm { get; private set; } = 16;
        [field: SerializeField, Min(0)] public int ImpactPrewarm { get; private set; } = 8;
        [field: SerializeField, Min(0)] public int BulletHolePrewarm { get; private set; } = 16;

        public void Prewarm(EffectPool pool)
        {
            pool.Prewarm(MuzzleFlash, MuzzleFlashPrewarm);
            pool.Prewarm(Tracer, TracerPrewarm);
            pool.Prewarm(Impact, ImpactPrewarm);
            pool.Prewarm(BulletHole, BulletHolePrewarm);
        }
    }
}
