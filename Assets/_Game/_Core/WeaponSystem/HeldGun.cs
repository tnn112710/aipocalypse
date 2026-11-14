using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using Ami.BroAudio;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // A gun in hand: runs its Gun rules from the holder's input and shows them — bullets, aim / sprint poses and zoom,
    // recoil, the gun's clips (GunAnimation) and sounds. Jams and their repair are one part (GunJam). Its numbers come from
    // the copy's stat sheet (GunStats: the model plus its parts); fitted parts show on its mounts (GunMounts).
    public sealed class HeldGun : HeldItem
    {
        [SerializeField, Tooltip("Moved to the active pose; parent of the recoil pivot")] Transform _posePivot;
        [SerializeField, Tooltip("Gun kick is applied here; parent of the model and the muzzle")] Transform _recoilPivot;
        [SerializeField, Tooltip("Bullets and the muzzle flash leave here, along its forward (blue axis)")] Transform _muzzle;
        [SerializeField, Tooltip("Optional: plays the gun's clips (equip, fire, reload, repair, pump) and drives its hand targets. " +
            "Without it the model only poses and kicks (a block-out from Gun_Base)")] GunAnimation _animation;
        [SerializeField, Tooltip("Optional: spent cases leave here (the gun's Ejection settings), thrown along its right (red axis)")]
        Transform _ejectionPort;
        [SerializeField, Tooltip("Optional: the model's part mounts (on Weapon). Without them fitted parts change the stats only")]
        GunMounts _mounts;

        [BoxGroup("Poses"), SerializeField, Tooltip("Siblings of the pose pivot; move them to pose the gun")] Transform _hipPose;
        [BoxGroup("Poses"), SerializeField] Transform _aimPose;
        [BoxGroup("Poses"), SerializeField] Transform _sprintPose;

        readonly GunJoltPlayer _jolt = new();
        readonly PoseBlender _pose = new();

        BulletSystem _bullets;
        IPublisher<ShotFired> _shots;
        EffectPool _effects;
        AudioService _audio;
        GunJamFactory _jams;

        IGunUser _user;
        GunInstance _item;
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
        // Seconds until the shot's tail sounds (pushed back by every shot of a spray); negative = none waiting.
        float _tailIn = -1f;
        // Where shots and the flash leave: a fitted muzzle part's tip, else the gun's own muzzle.
        Transform _shotMuzzle;
        // The aim pose moved so a fitted optic's sight line sits where the iron sights' did.
        Transform _opticAim;
        bool _aimThroughOptic;
        // The sight window shown now (a tube optic's, near full aim).
        Renderer _shownWindow;
        const float SightWindowFrom = 0.6f;

        public Gun Gun => _gun;
        public override bool IsAiming => _gun != null && _gun.IsAiming;
        public override ViewKick ViewKick => _recoil != null ? new ViewKick(_recoil.ViewEuler, _recoil.ViewShake) : default;
        public override Vector2 LookTurn => _recoil != null ? _recoil.LookTurn : default;
        public override float Zoom => _gun != null ? AimZoom * _recoil.FovScale : 1f;
        public override float AimZoom => _gun != null ? Mathf.Lerp(1f, _item.Stats.AimZoom, _aimWeight) : 1f;
        public override float AimWeight => _aimWeight;
        // Degrees the next shot may go off the aim from being airborne (on top of the gun's pellet spread).
        float AirSpread => _accuracy != null ? _accuracy.Spread * _item.Stats.SpreadScale : 0f;
        // The hip-fire cone, shrinking as the sights come up.
        float HipCone => _item != null ? _item.Stats.ConeAt(_aimWeight) : 0f;
        public override float Spread => _gun != null ? AirSpread + HipCone + _item.Stats.SpreadAngle : 0f;
        public override float MoveSpeedScale => _item != null ? _item.Stats.MoveSpeedScale : 1f;

        // A scope's own sway calming, eased in with the aim.
        public override float SwayScale
        {
            get
            {
                AttachmentDefinition optic = _item?.Stats.Optic;
                return optic != null ? Mathf.Lerp(1f, optic.Optic.SwayScale, _aimWeight) : 1f;
            }
        }

        public override ScopeView Scope
        {
            get
            {
                AttachmentDefinition optic = _item?.Stats.Optic;
                if (optic == null)
                    return default;
                float weight = optic.Optic.ScopeWeight(_aimWeight);
                return new ScopeView(weight, optic.Optic.ScopeReticle, weight > 0f ? EyeOffset(optic.Optic) : default);
            }
        }

        // How far the optic's sight line is off the eye's aim line, in shares of its eye box: the sight point's offset plus
        // the scope's tilt over a short lever (model sway, gun kick, the last of raising it).
        Vector2 EyeOffset(OpticSettings optic)
        {
            const float tiltLever = 0.1f;
            Transform sight = _mounts != null ? _mounts.Sight : null;
            if (sight == null || _user?.Eye == null)
                return default;
            Transform eye = _user.Eye;
            Quaternion toAim = Quaternion.Inverse(eye.rotation * _user.AimOffset);
            Vector3 point = toAim * (sight.position - eye.position);
            Vector3 along = toAim * sight.forward;
            var offset = new Vector2(point.x + along.x * tiltLever, point.y + along.y * tiltLever);
            return Vector2.ClampMagnitude(offset / optic.EyeBox, 2f);
        }

        // Aiming or pulling the trigger ends a sprint.
        public override bool StopsSprint(bool useHeld, bool aimHeld) => _gun != null && (useHeld || aimHeld);

        [Inject]
        void Construct(BulletSystem bullets, EffectPool effects, AudioService audio, GunJamFactory jams, IPublisher<ShotFired> shots)
        {
            _shots = shots;
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

            _item = item;
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
            if (_ejectionPort != null)
                _effects.Prewarm(_definition.Ejection.Shell, _definition.Ejection.Prewarm);
            _item.AttachmentsChanged += OnAttachmentsChanged;
            OnAttachmentsChanged();
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

            if (input.LightPressed && input.IsReady && _mounts != null && _mounts.HasLights)
            {
                _item.ToggleLight();
                _mounts.SetLights(_item.LightOn);
                AttachmentDefinition part = _mounts.SwitchedPart;
                if (part != null)
                    Play(part.SwitchSound);
            }
            _jam.Tick(input.IsReady, deltaTime);
            _jolt.Tick(deltaTime);
            _recoil.Tick(deltaTime, _aimWeight);
            UpdatePose(input.IsSprinting && input.IsReady, deltaTime);
            _accuracy.Tick(input.IsAirborne, _aimWeight, _user.AirSpreadScale, deltaTime);
            if (_tailIn >= 0f && (_tailIn -= deltaTime) < 0f)
                Play(_item.Stats.FireTailSound);
            if (_animation != null)
                _animation.Tick(deltaTime, _gun.ActionProgress);
        }

        public override void LateTick()
        {
            for (; _pendingShots > 0; _pendingShots--)
            {
                Shoot();
                // After the shot leaves: its kick shows from the next frame, as the bullet is already out of the barrel.
                _recoil.Kick(_aimWeight, _stateMultiplier * _user.FatigueRecoilScale, _item.Stats.VerticalRecoilScale,
                    _item.Stats.HorizontalRecoilScale);
            }
            AimLaser();
        }

        // A fitted laser's dot sits where the next shot goes (the holder's aim rule from the laser, before spread), so it
        // agrees with the crosshair whatever way the hip pose holds the barrel.
        void AimLaser()
        {
            AttachmentModel laser = _mounts != null ? _mounts.Laser : null;
            if (laser == null)
                return;
            int layers = _definition.Bullet.HitLayers;
            GunAim.Shot(_user, laser.LaserOrigin, _item.Stats.Range, layers, _bullets, out Vector3 origin, out Vector3 direction);
            laser.AimLaser(origin, direction, layers, _user.Eye);
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
            ShowSightWindow(false);
            _jam?.Hide();
            _sprintRecoveryLeft = 0f;
            _tailIn = -1f;
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
            _item.AttachmentsChanged -= OnAttachmentsChanged;
        }

        // A part was fitted or taken off (in the inventory, maybe while this gun is in hand): the model, the muzzle and the
        // aim pose follow at once.
        void OnAttachmentsChanged()
        {
            if (_mounts != null)
            {
                _mounts.Show(_item, held: true);
                RefreshRenderers();
            }
            Transform tip = _mounts != null ? _mounts.MuzzleTip : null;
            _shotMuzzle = tip != null ? tip : _muzzle;
            PlaceOpticAim();
        }

        // Moves a copy of the aim pose so the optic's sight point lands on the iron sights' line, its EyeDistance from the eye
        // (whatever the gun's own aim pose: guns with far-forward irons are held closer). Measured in the model's space, which
        // is the pose pivot's at rest.
        void PlaceOpticAim()
        {
            AttachmentDefinition optic = _item.Stats.Optic;
            Transform sight = _mounts != null ? _mounts.Sight : null;
            _aimThroughOptic = optic != null && sight != null && _mounts.IronSight != null && sight != _mounts.IronSight && _aimPose != null;
            if (!_aimThroughOptic)
                return;

            Transform space = _mounts.transform;
            Vector3 sightPoint = space.InverseTransformPoint(sight.position);
            Vector3 iron = space.InverseTransformPoint(_mounts.IronSight.position);
            Vector3 along = space.InverseTransformDirection(_mounts.IronSight.forward);
            if (Mathf.Abs(along.z) > 0.01f)
                iron += along * ((sightPoint.z - iron.z) / along.z);
            Vector3 offset = iron - sightPoint;
            offset.z = 0f;
            Transform eye = _user?.Eye;
            if (eye != null)
            {
                // Depth from the eye is linear in offset.z: solve for the optic's distance.
                Matrix4x4 eyeFromPoses = eye.worldToLocalMatrix * _aimPose.parent.localToWorldMatrix;
                Quaternion rotation = _aimPose.localRotation;
                float depth = eyeFromPoses.MultiplyPoint(_aimPose.localPosition + rotation * (offset + sightPoint)).z;
                float perMetre = eyeFromPoses.MultiplyVector(rotation * Vector3.forward).z;
                if (perMetre > 0.1f)
                    offset.z = (optic.Optic.EyeDistance - depth) / perMetre;
            }

            if (_opticAim == null)
            {
                _opticAim = new GameObject("AimOptic").transform;
                _opticAim.SetParent(_aimPose.parent, false);
            }
            _opticAim.SetLocalPositionAndRotation(_aimPose.localPosition + _aimPose.localRotation * offset, _aimPose.localRotation);
        }

        void OnFired()
        {
            _pendingShots++;
            _user.Exert(_definition.Fire.StaminaPerShot);
            if (_animation != null)
                _animation.Fired();
            Play(_item.Stats.FireSound);

            // A spray's tail waits for the gap after its last shot; a semi-auto's follows each shot.
            FireSettings fire = _definition.Fire;
            _tailIn = fire.Mode == FireMode.SemiAuto ? 0f : fire.SecondsPerShot * 1.3f;
            int low = _definition.Magazine.LowRoundsOf(_item.Stats.MagazineSize);
            int rounds = _gun.Item.Rounds;
            if (low > 0 && rounds <= low)
                _audio?.Play(_definition.Sounds.LowAmmo, _definition.Sounds.Volume * Mathf.Lerp(1f, 0.45f, rounds / (float)low));
            if (!_definition.Ejection.OnCycle)
                Eject();
        }

        void Eject()
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

        void Shoot()
        {
            GunStats stats = _item.Stats;
            Transform muzzle = _shotMuzzle;
            BulletEffects effects = _definition.Effects;
            if (effects != null && !stats.HidesFlash)
            {
                PooledEffect flash = _effects.Spawn(effects.MuzzleFlash, muzzle.position, muzzle.rotation, muzzle);
                // Pooled: only a fresh flash needs moving onto the first-person layer.
                if (flash != null && flash.gameObject.layer != gameObject.layer)
                    flash.transform.SetLayerRecursively(gameObject.layer);
            }

            FireSettings fire = _definition.Fire;
            BulletSettings bullet = _definition.Bullet;
            GunAim.Shot(_user, muzzle, stats.Range, bullet.HitLayers, _bullets, out Vector3 origin, out Vector3 direction);
            // Hip and airborne spread move the whole shot; pellets then spread around it.
            direction = BulletSpread.Apply(direction, AirSpread + HipCone);
            for (int i = 0; i < fire.Pellets; i++)
            {
                _bullets.Fire(new BulletShot
                {
                    Origin = origin,
                    Direction = BulletSpread.Apply(direction, stats.SpreadAngle),
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
                });
            }
            _shots?.Publish(new ShotFired(muzzle.position, _user.Root.gameObject, stats.Loudness));
        }

        void OnActionStarted(GunAction action)
        {
            _jam.ActionStarted();
            if (action == GunAction.Cycling && _definition.Ejection.OnCycle)
                Eject();
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
            float aimSpeed = _item.Stats.AimSpeed;
            float aimBlend = Damping.ByRate(aimSpeed, deltaTime);
            _aimWeight = Mathf.Lerp(_aimWeight, _gun.IsAiming && !sprinting ? 1f : 0f, aimBlend);
            Transform aim = _aimThroughOptic ? _opticAim : _aimPose;
            Transform target = sprinting ? _sprintPose : _gun.IsAiming ? aim : _hipPose;
            if (target == null)
                target = _hipPose;
            _pose.MoveTowards(target, sprinting ? handling.SprintPoseSpeed : aimSpeed, deltaTime);
            ApplyPose();
            ShowSightWindow(_gun.IsAiming && !sprinting && _aimWeight > SightWindowFrom);
        }

        // A tube optic's window hides its inside walls only once the eye is nearly behind it: from further off the walls
        // are what the eye would really see.
        void ShowSightWindow(bool on)
        {
            Renderer window = on && _mounts != null ? _mounts.SightWindow : null;
            if (window == _shownWindow)
                return;
            if (_shownWindow != null)
                _shownWindow.enabled = false;
            _shownWindow = window;
            if (window != null)
                window.enabled = true;
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
