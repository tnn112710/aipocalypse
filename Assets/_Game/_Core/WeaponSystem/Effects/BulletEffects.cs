using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // What shots look like. Lifetimes and caps are on each effect prefab's PooledEffect.
    [CreateAssetMenu(fileName = "BulletEffects", menuName = "Aipocalypse/Weapons/Bullet Effects")]
    public sealed class BulletEffects : ScriptableObject
    {
        [SerializeField] PooledEffect _muzzleFlash;
        [SerializeField, Tooltip("Follows the bullet; returned when it hits or runs out of range")] PooledEffect _tracer;
        [SerializeField] PooledEffect _impact;
        [SerializeField, Tooltip("Decal left on surfaces; never on things that take damage")] PooledEffect _bulletHole;

        [SerializeField, Tooltip("Surfaces that get bullet holes")]
        LayerMask _bulletHoleLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Random scale of each hole")] Vector2 _bulletHoleScale = new(0.9f, 1.1f);
        [SerializeField, Min(0f), Tooltip("Lift off the surface (m)")] float _bulletHoleOffset = 0.005f;

        [SerializeField, Min(0f), Tooltip("Camera-origin shots: seconds the tracer takes to slide from the muzzle onto the bullet's real path")]
        float _tracerConvergeTime = 0.08f;

        [Header("Prewarm (copies made when a gun is bound)"), SerializeField, Min(0)] int _muzzleFlashPrewarm = 4;
        [SerializeField, Min(0)] int _tracerPrewarm = 16;
        [SerializeField, Min(0)] int _impactPrewarm = 8;
        [SerializeField, Min(0)] int _bulletHolePrewarm = 16;

        public PooledEffect MuzzleFlash => _muzzleFlash;
        public PooledEffect Tracer => _tracer;
        public PooledEffect Impact => _impact;
        public PooledEffect BulletHole => _bulletHole;
        public LayerMask BulletHoleLayers => _bulletHoleLayers;
        public Vector2 BulletHoleScale => _bulletHoleScale;
        public float BulletHoleOffset => _bulletHoleOffset;
        public float TracerConvergeTime => _tracerConvergeTime;
        public int MuzzleFlashPrewarm => _muzzleFlashPrewarm;
        public int TracerPrewarm => _tracerPrewarm;
        public int ImpactPrewarm => _impactPrewarm;
        public int BulletHolePrewarm => _bulletHolePrewarm;

        public void Prewarm(EffectPool pool)
        {
            pool.Prewarm(MuzzleFlash, MuzzleFlashPrewarm);
            pool.Prewarm(Tracer, TracerPrewarm);
            pool.Prewarm(Impact, ImpactPrewarm);
            pool.Prewarm(BulletHole, BulletHolePrewarm);
        }
    }
}
