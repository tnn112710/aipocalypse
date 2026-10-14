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

        public void Prewarm(EffectPool pool)
        {
            pool.Prewarm(MuzzleFlash, 4);
            pool.Prewarm(Tracer, 16);
            pool.Prewarm(Impact, 8);
            pool.Prewarm(BulletHole, 16);
        }
    }
}
