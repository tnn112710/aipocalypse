using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Effects;
using Aipocalypse.Core.EquipmentSystem;
using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.WeaponSystem
{
    // A gun in hand: runs its Gun rules from the holder's input and shows them — bullets, aim / sprint poses and zoom,
    // recoil, the gun's clips (GunAnimation) and sounds. Jams and their repair are one part (GunJam).
    public sealed class HeldGun : HeldItem
    {
        [SerializeField, Tooltip("Moved to the active pose; parent of the recoil pivot")] Transform _posePivot;
        [SerializeField, Tooltip("Gun kick is applied here; parent of the model and the muzzle")] Transform _recoilPivot;
        [SerializeField, Tooltip("Bullets and the muzzle flash leave here, along its forward (blue axis)")] Transform _muzzle;
        [SerializeField, Tooltip("Optional: plays the gun's clips (equip, fire, reload, repair, pump) and drives its hand targets. " +
            "Without it the model only poses and kicks (a block-out from Gun_Base)")] GunAnimation _animation;

        [BoxGroup("Poses"), SerializeField, Tooltip("Siblings of the pose pivot; move them to pose the gun")] Transform _hipPose;
        [BoxGroup("Poses"), SerializeField] Transform _aimPose;
        [BoxGroup("Poses"), SerializeField] Transform _sprintPose;

        readonly GunJoltPlayer _jolt = new();
        readonly PoseBlender _pose = new();

        BulletSystem _bullets;
        EffectPool _effects;
        AudioService _audio;
        GunJamFactory _jams;

        IGunUser _user;
        GunDefinition _definition;
        Gun _gun;
        GunRecoil _recoil;
        GunAccuracy _accuracy;
        GunJam _jam;

        Vector3 _restRecoilPosition;
        Quaternion _restRecoilRotation;
        float _stateMultiplier = 1f;
        // 0 at the hip, 1 aiming down sights; eases with the aim pose and drives the zoom.
        float _aimWeight;
        // Shots from this frame's Gun.Tick, spawned in LateTick once the model and camera are in their final place.
        int _pendingShots;
        // Seconds until it can fire after a sprint.
        float _sprintRecoveryLeft;

        public Gun Gun => _gun;
        public override bool IsAiming => _gun != null && _gun.IsAiming;
        public override ViewKick ViewKick => _recoil != null ? new ViewKick(_recoil.ViewEuler, _recoil.ViewShake) : default;
        public override Vector2 LookTurn => _recoil != null ? _recoil.LookTurn : default;
        public override float Zoom => _gun != null ? Mathf.Lerp(1f, _definition.Handling.AimZoom, _aimWeight) : 1f;
        public override float AimWeight => _aimWeight;
        // Degrees the next shot may go off the aim from being airborne (on top of the gun's pellet spread).
        float AirSpread => _accuracy != null ? _accuracy.Spread : 0f;
        public override float Spread => _gun != null ? AirSpread + _definition.Fire.SpreadAngle : 0f;

        // Aiming or pulling the trigger ends a sprint.
        public override bool StopsSprint(bool useHeld, bool aimHeld) => _gun != null && (useHeld || aimHeld);

        [Inject]
        void Construct(BulletSystem bullets, EffectPool effects, AudioService audio, GunJamFactory jams)
        {
            _bullets = bullets;
            _effects = effects;
            _audio = audio;
            _jams = jams;
        }

        void Awake()
        {
            _restRecoilPosition = _recoilPivot.localPosition;
            _restRecoilRotation = _recoilPivot.localRotation;
        }

        protected override void OnBound()
        {
            _user = Owner as IGunUser;
            if (Item is not GunInstance item || _user == null)
            {
                Debug.LogError($"{name}: needs a GunInstance and a holder that is an IGunUser.", this);
                return;
            }
            if (_bullets == null)
            {
                Debug.LogError($"{name}: not injected; spawn it through the EquipmentRig.", this);
                return;
            }

            _definition = item.Gun;
            if (_definition.Magazine.AmmoType == null)
                Debug.LogWarning($"{_definition.name} has no ammo type and can't be reloaded.", _definition);

            _gun = new Gun(item, _user.Ammo);
            _recoil = new GunRecoil(_definition.Recoil);
            _accuracy = new GunAccuracy(_definition.Accuracy);
            _jam = _jams.Create(_gun, _definition, _animation, _jolt, this, Owner, Item);
            _gun.Fired += OnFired;
            _gun.EmptyFired += OnEmptyFired;
            _gun.ActionStarted += OnActionStarted;
            _gun.RoundLoaded += OnRoundLoaded;
            _gun.ActionEnded += OnActionEnded;
            if (_animation != null)
                _animation.Bind(_effects);

            if (_definition.Effects != null)
                _definition.Effects.Prewarm(_effects);
        }

        public override void Tick(in HeldItemInput input, float deltaTime)
        {
            if (_gun == null)
                return;

            RecoilSettings recoil = _definition.Recoil;
            _stateMultiplier = input.IsAirborne ? recoil.AirborneMultiplier : input.IsMoving ? recoil.MovingMultiplier : 1f;
            _sprintRecoveryLeft = input.IsSprinting ? _definition.Handling.SprintRecoveryTime : _sprintRecoveryLeft - deltaTime;

            _gun.Tick(new GunInput
            {
                TriggerHeld = input.UseHeld,
                TriggerPressed = input.UsePressed,
                Aim = input.AimHeld,
                Reload = input.ReloadPressed,
                IsReady = input.IsReady,
                CanFire = !input.IsSprinting,
                CanAim = !input.IsSprinting,
                Recovering = _sprintRecoveryLeft > 0f,
            }, deltaTime);

            _jam.Tick(input.IsReady, deltaTime);
            _jolt.Tick(deltaTime);
            _recoil.Tick(deltaTime, _aimWeight);
            UpdatePose(input.IsSprinting && input.IsReady, deltaTime);
            _accuracy.Tick(input.IsAirborne, _aimWeight, _user.AirSpreadScale, deltaTime);
            if (_animation != null)
                _animation.Tick(deltaTime, _gun.ActionProgress);
        }

        public override void LateTick()
        {
            for (; _pendingShots > 0; _pendingShots--)
            {
                Shoot();
                // After the shot leaves: its kick shows from the next frame, as the bullet is already out of the barrel.
                _recoil.Kick(_aimWeight, _stateMultiplier * _user.FatigueRecoilScale);
            }
        }

        protected override void OnShown()
        {
            if (_gun == null)
                return;
            Play(_definition.Sounds.Equip);
            _pose.SnapTo(_hipPose);
            ApplyPose();
            if (_animation != null)
                _animation.Show();
        }

        protected override void OnHidden()
        {
            _jolt.Stop();
            _aimWeight = 0f;
            _jam?.Hide();
            _sprintRecoveryLeft = 0f;
            _pendingShots = 0;
            _gun?.Interrupt();
            _recoil?.Reset();
            _accuracy?.Reset();
        }

        void OnDestroy()
        {
            if (_gun == null)
                return;
            _jam.Dispose();
            _gun.Fired -= OnFired;
            _gun.EmptyFired -= OnEmptyFired;
            _gun.ActionStarted -= OnActionStarted;
            _gun.RoundLoaded -= OnRoundLoaded;
            _gun.ActionEnded -= OnActionEnded;
        }

        void OnFired()
        {
            _pendingShots++;
            _user.Exert(_definition.Fire.StaminaPerShot);
            if (_animation != null)
                _animation.Fired();
            Play(_definition.Sounds.Fire);
        }

        void Shoot()
        {
            BulletEffects effects = _definition.Effects;
            if (effects != null)
            {
                PooledEffect flash = _effects.Spawn(effects.MuzzleFlash, _muzzle.position, _muzzle.rotation, _muzzle);
                // Pooled: only a fresh flash needs moving onto the first-person layer.
                if (flash != null && flash.gameObject.layer != gameObject.layer)
                    flash.transform.SetLayerRecursively(gameObject.layer);
            }

            FireSettings fire = _definition.Fire;
            BulletSettings bullet = _definition.Bullet;
            GunAim.Shot(_user, _muzzle, bullet.Range, bullet.HitLayers, _bullets, out Vector3 origin, out Vector3 direction);
            // Airborne spread moves the whole shot; pellets then spread around it.
            direction = BulletSpread.Apply(direction, _accuracy.Spread);
            for (int i = 0; i < fire.Pellets; i++)
            {
                _bullets.Fire(new BulletShot
                {
                    Origin = origin,
                    Direction = BulletSpread.Apply(direction, fire.SpreadAngle),
                    VisualOrigin = _user.AimFrom == AimOrigin.Camera ? _muzzle.position : origin,
                    Speed = bullet.Speed,
                    Range = bullet.Range,
                    GravityScale = bullet.GravityScale,
                    Damage = fire.Damage,
                    DamageType = bullet.DamageType,
                    Knockback = fire.Knockback,
                    Falloff = fire.Falloff,
                    Layers = bullet.HitLayers,
                    Ignore = _user.Root,
                    Source = _user.Root.gameObject,
                    Effects = effects,
                });
            }
        }

        void OnActionStarted(GunAction action)
        {
            _jam.ActionStarted();
            if (_animation != null)
                _animation.StartAction(action, _gun.Item.Rounds == 0, _gun.RepairIsStubborn);
            GunSounds sounds = _definition.Sounds;
            Play(action switch
            {
                GunAction.Reloading => sounds.ReloadStart,
                GunAction.ClearingJam => sounds.ClearJamStart,
                GunAction.Cycling => sounds.CycleStart,
                _ => default,
            });
        }

        void OnRoundLoaded() => Play(_definition.Sounds.ReloadInsert);

        void OnActionEnded(GunAction action, bool finished)
        {
            bool cleared = _jam.ActionEnded(action, finished);
            if (_animation != null)
            {
                if (cleared && _jam.QteSucceeded)
                    _animation.FinishRepair();
                else
                    _animation.EndAction();
            }
            if (!finished)
                return;

            if (cleared)
                _jam.Cleared();
            GunSounds sounds = _definition.Sounds;
            Play(action switch
            {
                GunAction.Reloading => sounds.ReloadEnd,
                GunAction.ClearingJam when !_jam.HasFeedback => sounds.ClearJamEnd,
                GunAction.Cycling => sounds.CycleEnd,
                _ => default,
            });
        }

        void OnEmptyFired()
        {
            GunSounds sounds = _definition.Sounds;
            Play(sounds.Empty.IsValid() ? sounds.Empty : sounds.DryFire);
        }

        void Play(SoundID sound) => _audio?.Play(sound, _definition.Sounds.Volume);

        void UpdatePose(bool sprinting, float deltaTime)
        {
            HandlingSettings handling = _definition.Handling;
            float aimBlend = Damping.ByRate(handling.AimSpeed, deltaTime);
            _aimWeight = Mathf.Lerp(_aimWeight, _gun.IsAiming && !sprinting ? 1f : 0f, aimBlend);
            Transform target = sprinting ? _sprintPose : _gun.IsAiming ? _aimPose : _hipPose;
            if (target == null)
                target = _hipPose;
            _pose.MoveTowards(target, sprinting ? handling.SprintPoseSpeed : handling.AimSpeed, deltaTime);
            ApplyPose();
        }

        void ApplyPose()
        {
            _pose.ApplyTo(_posePivot);
            Vector3 position = _restRecoilPosition + Vector3.back * _recoil.GunKickBack;
            Quaternion rotation = _restRecoilRotation * Quaternion.Euler(_recoil.GunEuler);
            _jolt.Apply(ref position, ref rotation);
            _recoilPivot.SetLocalPositionAndRotation(position, rotation);
        }

#if UNITY_EDITOR
        [Button("Jam gun (test)"), HideInEditorMode]
        void JamGun()
        {
            _gun?.Interrupt();
            _gun?.Item.SetJammed(true);
        }

        // Edit-mode preview in the prefab. In play the gun always starts at Hip.
        [ButtonGroup("Preview"), Button("Hip")] void PreviewHip() => PoseBlender.Preview(_posePivot, _hipPose);
        [ButtonGroup("Preview"), Button("Aim")] void PreviewAim() => PoseBlender.Preview(_posePivot, _aimPose);
        [ButtonGroup("Preview"), Button("Sprint")] void PreviewSprint() => PoseBlender.Preview(_posePivot, _sprintPose);
#endif
    }
}
