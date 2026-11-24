using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.SurfaceSystem;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    public struct BulletShot
    {
        public Vector3 Origin;
        public Vector3 Direction;
        // Where the tracer appears (the muzzle), when the bullet itself starts elsewhere (the camera).
        public Vector3 VisualOrigin;
        public float Speed;
        public float Range;
        public float GravityScale;
        public float Damage;
        public DamageType DamageType;
        // Push (N·s) along the flight; falls off with the damage.
        public float Knockback;
        // Null = full damage at any range.
        public DamageFalloff Falloff;
        // What it can hit (BulletSettings.HitLayers).
        public LayerMask Layers;
        // Anything under this is never hit (the shooter, its gun).
        public Transform Ignore;
        // Who fired it, for DamageInfo.Source.
        public GameObject Source;
        public BulletEffects Effects;
        // A round between tracer rounds (BulletEffects.TracerEvery): flies and hits the same, unseen.
        public bool NoTracer;
        // 0 by day, 1 at night: the tracer's glow is brighter in the dark.
        public float Darkness;
    }

    // Every bullet in flight as plain data: moved and ray-tested each frame, no rigidbodies, one central tick.
    // Hits damage IDamageables through DamageService and spawn pooled impacts and bullet holes.
    public sealed class BulletSystem : ITickable, System.IDisposable
    {
        struct Bullet
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 VisualOffset;
            public float Travelled;
            public float Range;
            public float GravityScale;
            public float Damage;
            public DamageType DamageType;
            public float Knockback;
            public DamageFalloff Falloff;
            public int Layers;
            public Transform Ignore;
            public GameObject Source;
            public BulletEffects Effects;
            public PooledEffect Tracer;
            public int TracerSpawnId;
            public TracerStreak Streak;
        }

        static readonly ProfilerMarker AdvanceMarker = new("Aipocalypse.BulletSystem.Advance");

        readonly EffectPool _effects;
        readonly DamageService _damage;
        readonly SurfaceService _surfaces;
        readonly List<Bullet> _bullets = new(64);
        // Raycast hits come back unordered and cut at the buffer's size, so it's sized for long aim rays through crowds.
        readonly RaycastHit[] _hits = new RaycastHit[64];

        public BulletSystem(EffectPool effects, DamageService damage, SurfaceService surfaces)
        {
            _effects = effects;
            _damage = damage;
            _surfaces = surfaces;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Dispose() => SceneManager.sceneUnloaded -= OnSceneUnloaded;

        // Root service: bullets still in flight belong to the unloaded scene (their shooter, tracer and targets are gone).
        void OnSceneUnloaded(Scene scene) => _bullets.Clear();

        public void Fire(in BulletShot shot)
        {
            Vector3 direction = shot.Direction.normalized;
            var bullet = new Bullet
            {
                Position = shot.Origin,
                Velocity = direction * shot.Speed,
                VisualOffset = shot.VisualOrigin - shot.Origin,
                Range = shot.Range,
                GravityScale = shot.GravityScale,
                Damage = shot.Damage,
                DamageType = shot.DamageType,
                Knockback = shot.Knockback,
                Falloff = shot.Falloff,
                Layers = shot.Layers,
                Ignore = shot.Ignore,
                Source = shot.Source,
                Effects = shot.Effects,
            };

            if (shot.Effects != null && !shot.NoTracer)
            {
                bullet.Tracer = _effects.Spawn(shot.Effects.Tracer, shot.VisualOrigin, Quaternion.LookRotation(direction), null, 0f);
                if (bullet.Tracer != null)
                {
                    bullet.TracerSpawnId = bullet.Tracer.SpawnId;
                    if (bullet.Tracer.TryGetComponent(out bullet.Streak))
                        bullet.Streak.Begin(shot.Darkness);
                }
            }
            _bullets.Add(bullet);
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            using var _ = AdvanceMarker.Auto();
            Camera eye = Camera.main;
            bool hasEye = eye != null;
            Vector3 eyePosition = hasEye ? eye.transform.position : default;
            float eyeTan = hasEye ? Mathf.Tan(eye.fieldOfView * 0.5f * Mathf.Deg2Rad) : 1f;
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                Bullet bullet = _bullets[i];
                if (Step(ref bullet, deltaTime, hasEye, eyePosition, eyeTan))
                {
                    _bullets[i] = bullet;
                    continue;
                }

                // The tracer stops where the bullet ended and lingers so its trail reaches it, even on point-blank hits.
                if (OwnsTracer(bullet))
                {
                    bullet.Tracer.transform.position = bullet.Position;
                    if (bullet.Streak != null)
                        bullet.Streak.End();
                    _effects.ReturnAfter(bullet.Tracer, bullet.Effects.TracerLinger);
                }
                // Order doesn't matter: swap-remove.
                int last = _bullets.Count - 1;
                _bullets[i] = _bullets[last];
                _bullets.RemoveAt(last);
            }
        }

        // False when the bullet is done (hit something or ran out of range). The eye is the camera the tracer's width keeps up with.
        bool Step(ref Bullet bullet, float deltaTime, bool hasEye, Vector3 eyePosition, float eyeTan)
        {
            Vector3 velocity = bullet.Velocity + Physics.gravity * (bullet.GravityScale * deltaTime);
            Vector3 step = (bullet.Velocity + velocity) * (0.5f * deltaTime);
            float distance = step.magnitude;
            if (distance <= Mathf.Epsilon)
                return false;

            bool lastStep = bullet.Travelled + distance >= bullet.Range;
            if (lastStep)
            {
                float remaining = bullet.Range - bullet.Travelled;
                step *= remaining / distance;
                distance = remaining;
            }

            if (TryHit(bullet.Position, step / distance, distance, bullet.Layers, bullet.Ignore, out RaycastHit hit))
            {
                Impact(bullet, hit, bullet.Travelled + hit.distance);
                bullet.Position = hit.point;
                return false;
            }

            bullet.Position += step;
            bullet.Velocity = velocity;
            bullet.Travelled += distance;

            if (OwnsTracer(bullet))
            {
                bullet.VisualOffset *= 1f - Damping.ByTime(bullet.Effects.TracerConvergeTime, deltaTime);
                Vector3 position = bullet.Position + bullet.VisualOffset;
                bullet.Tracer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
                if (hasEye && bullet.Streak != null)
                    bullet.Streak.ScaleFor(Vector3.Distance(position, eyePosition), eyeTan);
            }
            return !lastStep;
        }

        // A capped pool recycles its oldest effect: once the tracer was handed to a newer bullet, this one stops moving it.
        static bool OwnsTracer(in Bullet bullet) =>
            bullet.Tracer != null && bullet.Tracer.IsSpawned && bullet.Tracer.SpawnId == bullet.TracerSpawnId;

        // Nearest hit that isn't the shooter. Triggers (pickups) are ignored. Also what aiming uses to find the crosshair point.
        public bool TryHit(Vector3 origin, Vector3 direction, float distance, int layers, Transform ignore, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, layers, QueryTriggerInteraction.Ignore);
            return PhysicsQueries.Nearest(_hits, count, ignore, out nearest);
        }

        void Impact(in Bullet bullet, in RaycastHit hit, float travelled)
        {
            Vector3 normal = hit.normal != Vector3.zero ? hit.normal : -bullet.Velocity.normalized;

            float share = bullet.Falloff != null ? bullet.Falloff.At(travelled, bullet.Range) : 1f;
            Vector3 force = bullet.Velocity.normalized * (bullet.Knockback * share);
            IDamageable target = _damage.DealHit(hit, new DamageInfo(bullet.Damage * share, bullet.DamageType, hit.point, force, bullet.Source));

            BulletEffects effects = bullet.Effects;
            if (effects == null)
                return;

            if (target is IShowsOwnHits)
                _surfaces.FleshImpact(hit.point);
            else
                _surfaces.BulletImpact(hit.collider, hit.point, normal, effects.Impact);

            // Things that take damage move and die; a hole would float in the air.
            if (target == null && (effects.BulletHoleLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                SpawnBulletHole(effects, hit, normal);
        }

        // URP decal projectors project along their forward, so the hole faces into the surface.
        // Parented to what it hit so it rides along when that moves.
        void SpawnBulletHole(BulletEffects effects, in RaycastHit hit, Vector3 normal)
        {
            Vector3 into = -normal;
            Vector3 up = Mathf.Abs(Vector3.Dot(into, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(into, up) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);

            PooledEffect hole = _effects.Spawn(effects.BulletHole, hit.point + normal * effects.BulletHoleOffset, rotation, hit.collider.transform);
            if (hole != null)
                hole.ScaleDecal(Random.Range(effects.BulletHoleScale.x, effects.BulletHoleScale.y));
        }
    }
}
