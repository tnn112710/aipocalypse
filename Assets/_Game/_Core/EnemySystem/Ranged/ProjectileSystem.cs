using System.Collections.Generic;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.PlayerSystem;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    public struct ProjectileShot
    {
        public ProjectileDefinition Definition;
        public Vector3 Origin;
        public Vector3 Velocity;
        public float Damage;
        // Who threw it (DamageInfo.Source); nothing under it is hit.
        public GameObject Source;
        // Only targets of this kind are hurt; anything else just stops it.
        public TargetKind Hurts;
        // A lob: its landing point gets the definition's marker.
        public bool Lobbed;
        public Vector3 LandingPoint;
    }

    // Every thrown thing in flight as plain data (scene scope, with the enemies): moved under gravity and sphere-cast along
    // its path each frame on scaled time, one central tick, pooled visuals. It hurts only the kind of target it was thrown
    // at, sticks in what it lands on, and whistles (3D) just before it passes the active player.
    public sealed class ProjectileSystem : ITickable
    {
        struct Projectile
        {
            public ProjectileShot Shot;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public PooledEffect Visual;
            public int VisualSpawnId;
            public PooledEffect Marker;
            public int MarkerSpawnId;
            public bool Whistled;
        }

        static readonly ProfilerMarker AdvanceMarker = new("Aipocalypse.ProjectileSystem.Advance");

        readonly DamageService _damage;
        readonly EffectPool _effects;
        readonly AudioService _audio;
        readonly PlayerRegistry _players;
        readonly List<Projectile> _projectiles = new(32);
        readonly RaycastHit[] _hits = new RaycastHit[16];

        public ProjectileSystem(DamageService damage, EffectPool effects, AudioService audio, PlayerRegistry players)
        {
            _damage = damage;
            _effects = effects;
            _audio = audio;
            _players = players;
        }

        public int InFlight => _projectiles.Count;

        public void Launch(in ProjectileShot shot)
        {
            ProjectileDefinition definition = shot.Definition;
            if (definition == null || shot.Velocity == Vector3.zero)
                return;
            var projectile = new Projectile { Shot = shot, Position = shot.Origin, Velocity = shot.Velocity };
            if (definition.Visual != null)
            {
                _effects.Prewarm(definition.Visual, definition.Prewarm);
                projectile.Visual = _effects.Spawn(definition.Visual, shot.Origin, Quaternion.LookRotation(shot.Velocity), null, 0f);
                if (projectile.Visual != null)
                    projectile.VisualSpawnId = projectile.Visual.SpawnId;
            }
            if (shot.Lobbed && definition.LandingMarker != null)
            {
                projectile.Marker = _effects.Spawn(definition.LandingMarker, shot.LandingPoint, Quaternion.identity, null, 0f);
                if (projectile.Marker != null)
                    projectile.MarkerSpawnId = projectile.Marker.SpawnId;
            }
            _projectiles.Add(projectile);
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || _projectiles.Count == 0)
                return;
            using var _ = AdvanceMarker.Auto();
            IPlayer listener = _players.Current;
            Vector3 ear = listener != null ? (listener.View != null ? listener.View.position : listener.Transform.position) : default;
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _projectiles[i];
                if (Step(ref projectile, deltaTime, listener != null, ear))
                {
                    _projectiles[i] = projectile;
                    continue;
                }
                int last = _projectiles.Count - 1;
                _projectiles[i] = _projectiles[last];
                _projectiles.RemoveAt(last);
            }
        }

        // False once it has landed, hit something or flown too long.
        bool Step(ref Projectile projectile, float deltaTime, bool hasListener, Vector3 ear)
        {
            ProjectileDefinition definition = projectile.Shot.Definition;
            Vector3 velocity = projectile.Velocity + Vector3.down * (definition.Gravity * deltaTime);
            Vector3 step = (projectile.Velocity + velocity) * (0.5f * deltaTime);
            float distance = step.magnitude;
            projectile.Age += deltaTime;
            if (distance <= Mathf.Epsilon || projectile.Age > definition.MaxFlightTime)
            {
                End(projectile, false, default, null);
                return false;
            }

            Vector3 direction = step / distance;
            Transform ignore = projectile.Shot.Source != null ? projectile.Shot.Source.transform : null;
            int count = Physics.SphereCastNonAlloc(projectile.Position, definition.Radius, direction, _hits, distance, definition.HitLayers,
                QueryTriggerInteraction.Ignore);
            if (PhysicsQueries.Nearest(_hits, count, ignore, out RaycastHit hit))
            {
                // Already inside something at the start of the step: Unity reports no point.
                if (hit.distance <= 0f)
                    hit.point = projectile.Position;
                projectile.Position = hit.point;
                projectile.Velocity = velocity;
                Land(projectile, hit);
                return false;
            }

            projectile.Position += step;
            projectile.Velocity = velocity;
            if (OwnsVisual(projectile))
                projectile.Visual.transform.SetPositionAndRotation(projectile.Position, Quaternion.LookRotation(velocity));
            if (hasListener && !projectile.Whistled)
                Whistle(ref projectile, ear);
            return true;
        }

        // The warning: once its path will pass close to the listener within the lead time, it whistles from where it is now.
        void Whistle(ref Projectile projectile, Vector3 ear)
        {
            ProjectileDefinition definition = projectile.Shot.Definition;
            float sqrSpeed = projectile.Velocity.sqrMagnitude;
            if (sqrSpeed < 0.01f)
                return;
            float toClosest = Vector3.Dot(ear - projectile.Position, projectile.Velocity) / sqrSpeed;
            if (toClosest < 0f || toClosest > definition.FlyByLead)
                return;
            Vector3 closest = projectile.Position + projectile.Velocity * toClosest;
            if ((closest - ear).sqrMagnitude > definition.FlyByRadius * definition.FlyByRadius)
                return;
            projectile.Whistled = true;
            _audio.PlayAt(definition.FlyBy, projectile.Position);
        }

        void Land(in Projectile projectile, in RaycastHit hit)
        {
            ProjectileShot shot = projectile.Shot;
            ProjectileDefinition definition = shot.Definition;
            var target = hit.collider.GetComponentInParent<ITargetable>();
            bool hurts = target != null && target.Kind == shot.Hurts && !target.IsDead;
            if (hurts)
            {
                Vector3 force = projectile.Velocity.normalized * definition.Knockback;
                _damage.DealHit(hit, new DamageInfo(shot.Damage, DamageType.Projectile, hit.point, force, shot.Source));
                _audio.PlayAt(definition.Hit, hit.point);
            }
            else
                _audio.PlayAt(definition.Impact, hit.point);
            // It sticks in the world, not in something alive (it would hang in the air once that moves or dies).
            End(projectile, !hurts && target == null, hit.point, hit.collider.transform);
        }

        void End(in Projectile projectile, bool stick, Vector3 point, Transform parent)
        {
            if (projectile.Marker != null && projectile.Marker.IsSpawned && projectile.Marker.SpawnId == projectile.MarkerSpawnId)
                _effects.Return(projectile.Marker);
            if (!OwnsVisual(projectile))
                return;
            ProjectileDefinition definition = projectile.Shot.Definition;
            if (!stick || definition.StickSeconds <= 0f)
            {
                _effects.Return(projectile.Visual);
                return;
            }
            Transform visual = projectile.Visual.transform;
            Vector3 direction = projectile.Velocity.sqrMagnitude > 0.01f ? projectile.Velocity.normalized : visual.forward;
            visual.SetPositionAndRotation(point + direction * definition.StickDepth, Quaternion.LookRotation(direction));
            visual.SetParent(parent, true);
            _effects.ReturnAfter(projectile.Visual, definition.StickSeconds);
        }

        // A capped pool recycles its oldest effect: once the visual was handed to a newer projectile, this one leaves it alone.
        static bool OwnsVisual(in Projectile projectile) =>
            projectile.Visual != null && projectile.Visual.IsSpawned && projectile.Visual.SpawnId == projectile.VisualSpawnId;
    }
}
