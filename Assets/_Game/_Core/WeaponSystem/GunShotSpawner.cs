using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.RandomSystem;
using MessagePipe;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // What leaves a held gun: bullets with their flash and tracers, spent cases and a fitted laser's dot. Owned by HeldGun,
    // which says when; the model's transforms stay serialized there.
    public sealed class GunShotSpawner
    {
        readonly BulletSystem _bullets;
        readonly EffectPool _effects;
        readonly IPublisher<ShotFired> _shots;
        readonly RandomStream _random;
        // Optional (scenes without one are always day): how dark it is, for the tracer's glow.
        readonly EnvironmentDirector _environment;
        readonly IGunUser _user;
        readonly GunInstance _item;
        readonly GunDefinition _definition;
        readonly Gun _gun;
        // The gun's own object: a fresh muzzle flash moves onto its (first-person) layer.
        readonly GameObject _view;
        readonly Transform _ejectionPort;
        readonly GunMounts _mounts;
        // Rounds until the next one shows tracers (BulletEffects.TracerEvery); 0 = the next one does.
        int _roundsToTracer;

        public GunShotSpawner(BulletSystem bullets, EffectPool effects, IPublisher<ShotFired> shots, RandomStream random,
            EnvironmentDirector environment, IGunUser user, GunInstance item, Gun gun, GameObject view, Transform ejectionPort,
            GunMounts mounts)
        {
            _bullets = bullets;
            _effects = effects;
            _shots = shots;
            _random = random;
            _environment = environment;
            _user = user;
            _item = item;
            _definition = item.Gun;
            _gun = gun;
            _view = view;
            _ejectionPort = ejectionPort;
            _mounts = mounts;
        }

        // Put away: the first round after the next equip is a tracer again.
        public void ResetTracer() => _roundsToTracer = 0;

        // A fitted laser's dot sits where the next shot goes (the holder's aim rule from the laser, before spread), so it
        // agrees with the crosshair whatever way the hip pose holds the barrel.
        public void AimLaser()
        {
            AttachmentModel laser = _mounts != null ? _mounts.Laser : null;
            if (laser == null)
                return;
            int layers = _definition.Bullet.HitLayers;
            GunAim.Shot(_user, laser.LaserOrigin, _item.Stats.Range, layers, _bullets, out Vector3 origin, out Vector3 direction);
            laser.AimLaser(origin, direction, layers, _user.Eye);
        }

        public void Eject()
        {
            EjectionSettings ejection = _definition.Ejection;
            if (_ejectionPort == null || ejection.Shell == null)
                return;
            PooledEffect shell = _effects.Spawn(ejection.Shell, _ejectionPort.position, _ejectionPort.rotation * Random.rotation);
            if (shell == null || !shell.TryGetComponent(out Rigidbody body))
                return;
            float jitter = 1f + Random.Range(-ejection.VelocityJitter, ejection.VelocityJitter);
            body.linearVelocity = _ejectionPort.TransformDirection(ejection.Velocity) * jitter;
            body.angularVelocity = Random.onUnitSphere * (ejection.Spin * Mathf.Deg2Rad);
            body.WakeUp();
        }

        public void Shoot(Transform muzzle)
        {
            GunStats stats = _item.Stats;
            BulletEffects effects = _definition.Effects;
            if (effects != null && !stats.HidesFlash)
            {
                PooledEffect flash = _effects.Spawn(effects.MuzzleFlash, muzzle.position, muzzle.rotation, muzzle);
                // Pooled: only a fresh flash needs moving onto the first-person layer.
                int layer = _view.layer;
                if (flash != null && flash.gameObject.layer != layer)
                    flash.transform.SetLayerRecursively(layer);
            }

            FireSettings fire = _definition.Fire;
            BulletSettings bullet = _definition.Bullet;
            GunAim.Shot(_user, muzzle, stats.Range, bullet.HitLayers, _bullets, out Vector3 origin, out Vector3 direction);
            // The first round after equipping is a tracer round, and so are a magazine's last few; a round's pellets all
            // show theirs or none.
            bool traced = --_roundsToTracer <= 0;
            if (traced && effects != null)
                _roundsToTracer = effects.NextTracerGap(Random.value);
            if (effects != null && effects.IsLastRound(_gun.Item.Rounds))
                traced = true;
            float darkness = _environment != null ? 1f - _environment.Daylight : 0f;
            // No hidden cone: hip inaccuracy is the visible wobble the aim already follows. Pellets spread around it.
            for (int i = 0; i < fire.Pellets; i++)
            {
                _bullets.Fire(new BulletShot
                {
                    Origin = origin,
                    Direction = BulletSpread.Apply(direction, stats.SpreadAngle, _random.InsideUnitCircle),
                    VisualOrigin = _user.AimFrom == AimOrigin.Camera ? muzzle.position : origin,
                    Speed = stats.BulletSpeed,
                    Range = stats.Range,
                    GravityScale = bullet.GravityScale,
                    Damage = stats.Damage,
                    DamageType = bullet.DamageType,
                    Knockback = fire.Knockback,
                    Falloff = stats.Falloff,
                    Layers = bullet.HitLayers,
                    Ignore = _user.Root,
                    Source = _user.Root.gameObject,
                    Effects = effects,
                    NoTracer = !traced,
                    Darkness = darkness,
                });
            }
            _shots?.Publish(new ShotFired(muzzle.position, _user.Root.gameObject, stats.Loudness));
        }
    }
}
