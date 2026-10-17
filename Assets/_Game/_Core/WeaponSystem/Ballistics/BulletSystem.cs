using System.Collections.Generic;
using Aipocalypse.Core.Combat;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.WeaponSystem
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
        // What it can hit (BulletSettings.HitLayers).
        public LayerMask Layers;
        // Anything under this is never hit (the shooter, its gun).
        public Transform Ignore;
        // Who fired it, for DamageInfo.Source.
        public GameObject Source;
        public BulletEffects Effects;
    }

    // Every bullet in flight as plain data: moved and ray-tested each frame, no rigidbodies, one central tick.
    // Hits damage IDamageables through DamageService and spawn pooled impacts and bullet holes.
    public sealed class BulletSystem : ITickable
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
            public int Layers;
            public Transform Ignore;
            public GameObject Source;
            public BulletEffects Effects;
            public PooledEffect Tracer;
        }

        readonly EffectPool _effects;
        readonly DamageService _damage;
        readonly List<Bullet> _bullets = new(64);
        // Raycast hits come back unordered and cut at the buffer's size, so it's sized for long aim rays through crowds.
        readonly RaycastHit[] _hits = new RaycastHit[64];

        public BulletSystem(EffectPool effects, DamageService damage)
        {
            _effects = effects;
            _damage = damage;
        }

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
                Layers = shot.Layers,
                Ignore = shot.Ignore,
                Source = shot.Source,
                Effects = shot.Effects,
            };

            if (shot.Effects != null)
                bullet.Tracer = _effects.Spawn(shot.Effects.Tracer, shot.VisualOrigin, Quaternion.LookRotation(direction), null, 0f);
            _bullets.Add(bullet);
        }

        public void Tick()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                Bullet bullet = _bullets[i];
                if (Step(ref bullet, deltaTime))
                {
                    _bullets[i] = bullet;
                    continue;
                }

                _effects.Return(bullet.Tracer);
                // Order doesn't matter: swap-remove.
                int last = _bullets.Count - 1;
                _bullets[i] = _bullets[last];
                _bullets.RemoveAt(last);
            }
        }

        // False when the bullet is done (hit something or ran out of range).
        bool Step(ref Bullet bullet, float deltaTime)
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
                Impact(bullet, hit);
                return false;
            }

            bullet.Position += step;
            bullet.Velocity = velocity;
            bullet.Travelled += distance;

            if (bullet.Tracer != null)
            {
                float converge = bullet.Effects.TracerConvergeTime;
                bullet.VisualOffset *= converge > 0f ? Mathf.Exp(-deltaTime / converge) : 0f;
                bullet.Tracer.transform.SetPositionAndRotation(bullet.Position + bullet.VisualOffset, Quaternion.LookRotation(velocity));
            }
            return !lastStep;
        }

        // Nearest hit that isn't the shooter. Triggers (pickups) are ignored. Also what aiming uses to find the crosshair point.
        public bool TryHit(Vector3 origin, Vector3 direction, float distance, int layers, Transform ignore, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, layers, QueryTriggerInteraction.Ignore);
            nearest = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance >= nearestDistance || (ignore != null && hit.collider.transform.IsChildOf(ignore)))
                    continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            return nearestDistance < float.MaxValue;
        }

        void Impact(in Bullet bullet, in RaycastHit hit)
        {
            Vector3 normal = hit.normal != Vector3.zero ? hit.normal : -bullet.Velocity.normalized;

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            _damage.Deal(target, new DamageInfo(bullet.Damage, DamageType.Gunshot, hit.point, default, bullet.Source));

            BulletEffects effects = bullet.Effects;
            if (effects == null)
                return;

            _effects.Spawn(effects.Impact, hit.point, Quaternion.LookRotation(normal));

            // Things that take damage move and die; a hole would float in the air.
            if (target == null && (effects.BulletHoleLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                SpawnBulletHole(effects, hit.point, normal);
        }

        // URP decal projectors project along their forward, so the hole faces into the surface.
        void SpawnBulletHole(BulletEffects effects, Vector3 point, Vector3 normal)
        {
            Vector3 into = -normal;
            Vector3 up = Mathf.Abs(Vector3.Dot(into, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(into, up) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);

            PooledEffect hole = _effects.Spawn(effects.BulletHole, point + normal * effects.BulletHoleOffset, rotation);
            if (hole != null)
                hole.transform.localScale = Vector3.one * Random.Range(effects.BulletHoleScale.x, effects.BulletHoleScale.y);
        }
    }
}
