using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.RandomSystem;
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
        [BoxGroup("Poses"), SerializeField, Tooltip("Optional: where the gun is held while reloading or clearing a jam at the hip, so the hands stay in view")]
        Transform _workPose;

        readonly GunJoltPlayer _jolt = new();
        // Jam clearing as a shake of the whole gun, for guns whose repair profile has one.
        readonly GunShake _shake = new();
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
        // Gameplay rolls (jams, pellets, spray pattern, wobble jolts): one saved, seeded stream for every gun.
        RandomStream _random;
        const string RandomStreamName = "guns";
        // This frame's shot jolts to the wobble (degrees).
        Vector2 _wobbleKick;
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
        // The aim pose through a fitted optic, its scope view offset and sight window.
        GunOpticAim _optic;
        // Rounds until the next one shows tracers (BulletEffects.TracerEvery); 0 = the next one does.
        int _roundsToTracer;
        // Optional (scenes without one are always day): how dark it is, for the tracer's glow.
        EnvironmentDirector _environment;

        public Gun Gun => _gun;
        public override bool IsAiming => _gun != null && _gun.IsAiming;
        public override ViewKick ViewKick => _recoil != null ? new ViewKick(_recoil.ViewEuler, _recoil.ViewShake) : default;
        public override Vector2 LookTurn => _recoil != null ? _recoil.LookTurn : default;
        public override float Zoom => _gun != null ? AimZoom * _recoil.FovScale : 1f;
        public override float AimZoom => _gun != null ? Mathf.Lerp(1f, _item.Stats.AimZoom, _aimWeight) : 1f;
        public override float AimWeight => _aimWeight;
        public override float Wobble => _accuracy != null ? _accuracy.Spread : 0f;
        public override Vector2 WobbleKick => _wobbleKick;
        public override float Spread => _gun != null ? Wobble + _item.Stats.SpreadAngle : 0f;
        public override float MoveSpeedScale => _item != null ? _item.Stats.MoveSpeedScale : 1f;

        // Live in the Inspector while playing (any scene), for tuning the hip feel: the envelope, its shot bloom, the stance's
        // recoil multiplier.
        [ShowInInspector, ReadOnly, HideInEditorMode, BoxGroup("Live"), LabelText("Wobble (°)")] float LiveWobble => Wobble;
        [ShowInInspector, ReadOnly, HideInEditorMode, BoxGroup("Live"), LabelText("Bloom (°)")] float LiveBloom => _accuracy?.Bloom ?? 0f;
        [ShowInInspector, ReadOnly, HideInEditorMode, BoxGroup("Live"), LabelText("Recoil ×")] float LiveRecoil => _stateMultiplier;

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
                Vector2 eyeOffset = weight > 0f && _user != null
                    ? GunOpticAim.EyeOffset(optic.Optic, _mounts != null ? _mounts.Sight : null, _user.Eye, _user.AimOffset)
                    : default;
                return new ScopeView(weight, optic.Optic.ScopeReticle, eyeOffset);
            }
        }

        // Aiming or pulling the trigger ends a sprint.
        public override bool StopsSprint(bool useHeld, bool aimHeld) => _gun != null && (useHeld || aimHeld);

        [Inject]
        void Construct(BulletSystem bullets, EffectPool effects, AudioService audio, GunJamFactory jams, IPublisher<ShotFired> shots,
            RandomService random, IObjectResolver resolver)
        {
            resolver.TryResolve(out _environment);
            _random = random.Stream(RandomStreamName);
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

            _gun = new Gun(item, _user.Ammo, () => _random.Value);
            _recoil = new GunRecoil(_definition.Recoil, _random);
            _optic = new GunOpticAim(_aimPose);
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

            _wobbleKick = default;
            RecoilSettings recoil = _definition.Recoil;
            _stateMultiplier = recoil.StateMultiplier(input.IsAirborne, input.IsMoving, input.IsCrouching);
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
            _shake.TickReturn(deltaTime);
            if (_shake.Tick(_gun.ActionProgress, out bool hardShake))
                _jam.Shaken(hardShake);
            _recoil.Tick(deltaTime, _aimWeight);
            UpdatePose(input.IsSprinting && input.IsReady, deltaTime);
            _accuracy.Tick(new AccuracyInput
            {
                Cone = _item.Stats.ConeAt(_aimWeight),
                MoveScale = input.MoveSpreadScale,
                Turn = input.TurnSpread,
                AimWeight = _aimWeight,
                Airborne = input.IsAirborne,
                AirScale = _user.AirSpreadScale * _item.Stats.SpreadScale,
                Fatigue = _user.FatigueRecoilScale,
                Steadiness = _user.SteadinessScale,
            }, deltaTime);
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
            _shake.Stop();
            _aimWeight = 0f;
            _optic?.ShowWindow(null, 0f);
            _jam?.Hide();
            _sprintRecoveryLeft = 0f;
            _tailIn = -1f;
            _pendingShots = 0;
            _roundsToTracer = 0;
            _gun?.Interrupt();
            _recoil?.Reset();
            _accuracy?.Reset();
            _wobbleKick = default;
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
            AttachmentDefinition optic = _item.Stats.Optic;
            _optic.Place(optic != null ? optic.Optic : null, _mounts != null ? _mounts.Sight : null,
                _mounts != null ? _mounts.IronSight : null, _mounts != null ? _mounts.transform : null, _user?.Eye);
        }

        void OnFired()
        {
            _pendingShots++;
            float angle = _random.Value * 2f * Mathf.PI;
            _wobbleKick += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _accuracy.AddShot();
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

        void OnActionStarted(GunAction action)
        {
            _jam.ActionStarted();
            if (action == GunAction.Cycling && _definition.Ejection.OnCycle)
                Eject();
            GunRepairProfile repair = action == GunAction.ClearingJam ? _definition.Jam.Repair : null;
            GunShakeSettings shake = repair != null && (_animation == null || Random.value < repair.ShakeChance) ? repair.Shake : null;
            if (shake != null)
                _shake.Start(shake, _gun.RepairIsStubborn);
            else if (_animation != null)
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
            bool shaken = _shake.IsPlaying;
            _shake.Finish();
            if (_animation != null && !shaken)
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
            Transform aim = _optic.AimPose;
            bool working = _workPose != null && _gun.Action is GunAction.Reloading or GunAction.ClearingJam;
            Transform target = sprinting ? _sprintPose : _gun.IsAiming ? aim : working ? _workPose : _hipPose;
            if (target == null)
                target = _hipPose;
            _pose.MoveTowards(target, sprinting ? handling.SprintPoseSpeed : aimSpeed, deltaTime);
            ApplyPose();
            _optic.ShowWindow(_mounts != null ? _mounts.SightWindow : null, _gun.IsAiming && !sprinting ? _aimWeight : 0f);
        }

        void ApplyPose()
        {
            _pose.ApplyTo(_posePivot);
            Vector3 position = _restRecoilPosition + Vector3.back * _recoil.GunKickBack;
            Quaternion rotation = _restRecoilRotation * Quaternion.Euler(_recoil.GunEuler);
            _jolt.Apply(ref position, ref rotation);
            _shake.Apply(ref position, ref rotation);
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
        [ButtonGroup("Preview"), Button("Work")] void PreviewWork() => PoseBlender.Preview(_posePivot, _workPose);
#endif
    }
}
