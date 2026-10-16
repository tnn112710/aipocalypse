using System;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.QuickTimeSystem;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.WeaponSystem
{
    // A gun in hand: runs its Gun rules from the holder's input and shows them — bullets, aim / sprint poses, recoil,
    // the gun's clips (GunAnimation), sounds, and the jam repair's QTE and feedback.
    public sealed class HeldGun : HeldItem
    {
        [SerializeField, Tooltip("Moved to the active pose; parent of the recoil pivot")] Transform _posePivot;
        [SerializeField, Tooltip("Gun kick is applied here; parent of the model and the muzzle")] Transform _recoilPivot;
        [SerializeField, Tooltip("Bullets and the muzzle flash leave here, along its forward (blue axis)")] Transform _muzzle;
        [SerializeField, Tooltip("2D source for the gun's own sounds")] AudioSource _audio;
        [SerializeField, Tooltip("Plays the gun's clips (equip, fire, reload, repair, pump) and drives its hand targets")] GunAnimation _animation;

        [BoxGroup("Poses"), SerializeField, Tooltip("Siblings of the pose pivot; move them to pose the gun")] Transform _hipPose;
        [BoxGroup("Poses"), SerializeField] Transform _aimPose;
        [BoxGroup("Poses"), SerializeField] Transform _sprintPose;

        readonly GunJoltPlayer _jolt = new();

        BulletSystem _bullets;
        EffectPool _effects;
        QuickTimeService _qte;
        IPublisher<GunRepairCompleted> _repairCompleted;
        IPublisher<GunJamBeat> _beats;
        IDisposable _qteAttempts;
        Action<QteResult> _repairQteCompleted;

        IGunUser _user;
        GunDefinition _definition;
        Gun _gun;
        GunRecoil _recoil;

        Vector3 _restRecoilPosition;
        Quaternion _restRecoilRotation;
        Vector3 _posePosition;
        Quaternion _poseRotation;
        float _stateMultiplier = 1f;
        // Shots from this frame's Gun.Tick, spawned in LateTick once the model and camera are in their final place.
        int _pendingShots;
        bool _qteRequested;
        bool _repairQteSucceeded;
        // Seconds until the "action snaps home" sound after a QTE success; negative = none due.
        float _mechanismIn = -1f;

        public Gun Gun => _gun;
        public override bool IsAiming => _gun != null && _gun.IsAiming;
        public override ViewKick ViewKick => _recoil != null ? new ViewKick(_recoil.ViewEuler, _recoil.ViewShake) : default;

        RepairFeedbackSettings Feedback => _definition.Jam.Feedback;

        [Inject]
        void Construct(BulletSystem bullets, EffectPool effects, QuickTimeService qte,
            IPublisher<GunRepairCompleted> repairCompleted, IPublisher<GunJamBeat> beats, ISubscriber<QteAttempted> qteAttempts)
        {
            _bullets = bullets;
            _effects = effects;
            _qte = qte;
            _repairCompleted = repairCompleted;
            _beats = beats;
            _qteAttempts = qteAttempts.Subscribe(OnQteAttempted);
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
            if (_animation == null)
            {
                Debug.LogError($"{name}: no GunAnimation assigned.", this);
                return;
            }

            _definition = item.Gun;
            if (_definition.Magazine.AmmoType == null)
                Debug.LogWarning($"{_definition.name} has no ammo type and can't be reloaded.", _definition);

            _gun = new Gun(item, _user.Ammo);
            _recoil = new GunRecoil(_definition.Recoil);
            _repairQteCompleted = OnRepairQteCompleted;
            _gun.Fired += OnFired;
            _gun.Jammed += OnJammed;
            _gun.DryFired += OnDryFired;
            _gun.ActionStarted += OnActionStarted;
            _gun.RoundLoaded += OnRoundLoaded;
            _gun.ActionEnded += OnActionEnded;
            _animation.RepairSlapped += OnRepairSlapped;
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

            _gun.Tick(new GunInput
            {
                TriggerHeld = input.UseHeld,
                TriggerPressed = input.UsePressed,
                Aim = input.AimHeld,
                Reload = input.ReloadPressed,
                IsReady = input.IsReady,
                CanFire = !input.IsSprinting,
                CanAim = !input.IsSprinting,
            }, deltaTime);

            if (input.IsReady)
                TryStartRepairQte();
            TickMechanismSound(deltaTime);
            _jolt.Tick(deltaTime);
            _recoil.Tick(deltaTime, _gun.IsAiming);
            UpdatePose(input.IsSprinting && input.IsReady, deltaTime);
            _animation.Tick(deltaTime, _gun.ActionProgress);
        }

        public override void LateTick()
        {
            for (; _pendingShots > 0; _pendingShots--)
            {
                Shoot();
                // After the shot leaves: its kick shows from the next frame, as the bullet is already out of the barrel.
                _recoil.Kick(_gun.IsAiming, _stateMultiplier);
            }
        }

        protected override void OnShown()
        {
            if (_gun == null)
                return;
            Play(_definition.Sounds.Equip);
            if (_hipPose != null)
            {
                _posePosition = _hipPose.localPosition;
                _poseRotation = _hipPose.localRotation;
            }
            ApplyPose();
            _animation.Show();
        }

        protected override void OnHidden()
        {
            _jolt.Stop();
            _mechanismIn = -1f;
            _pendingShots = 0;
            _gun?.Interrupt();
            _recoil?.Reset();
        }

        void OnDestroy()
        {
            _qteAttempts?.Dispose();
            _qte?.Cancel(this);
            if (_gun == null)
                return;
            _gun.Fired -= OnFired;
            _gun.Jammed -= OnJammed;
            _gun.DryFired -= OnDryFired;
            _gun.ActionStarted -= OnActionStarted;
            _gun.RoundLoaded -= OnRoundLoaded;
            _gun.ActionEnded -= OnActionEnded;
            if (_animation != null)
                _animation.RepairSlapped -= OnRepairSlapped;
        }

        void OnFired()
        {
            _pendingShots++;
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
            AimShot(bullet.Range, out Vector3 origin, out Vector3 direction);
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
                    Ignore = _user.Root,
                    Effects = effects,
                });
            }
        }

        void AimShot(float range, out Vector3 origin, out Vector3 direction)
        {
            Transform eye = _user.Eye;
            Vector3 toMuzzle = _muzzle.position - eye.position;
            // The view model draws over cover, but a barrel beyond cover must still hit its near face.
            if (_user.AimFrom != AimOrigin.Camera && toMuzzle.sqrMagnitude > 0.000001f &&
                _bullets.TryHit(eye.position, toMuzzle.normalized, toMuzzle.magnitude, _user.Root, out _))
            {
                direction = toMuzzle.normalized;
                origin = eye.position;
                return;
            }
            switch (_user.AimFrom)
            {
                case AimOrigin.Camera:
                    origin = eye.position;
                    direction = eye.forward;
                    return;

                case AimOrigin.MuzzleToCrosshair:
                    Vector3 aimPoint = _bullets.TryHit(eye.position, eye.forward, range, _user.Root, out RaycastHit hit)
                        ? hit.point
                        : eye.position + eye.forward * range;
                    origin = _muzzle.position;
                    direction = GunAim.MuzzleToPoint(origin, eye.forward, aimPoint);
                    return;

                default:
                    origin = _muzzle.position;
                    direction = _muzzle.forward;
                    return;
            }
        }

        void OnActionStarted(GunAction action)
        {
            _qteRequested = false;
            _repairQteSucceeded = false;
            _animation.StartAction(action, _gun.Item.Rounds == 0, _gun.RepairIsStubborn);
            GunSounds sounds = _definition.Sounds;
            Play(action switch
            {
                GunAction.Reloading => sounds.ReloadStart,
                GunAction.ClearingJam => sounds.ClearJamStart,
                GunAction.Cycling => sounds.CycleStart,
                _ => null,
            });
        }

        void OnRoundLoaded() => Play(_definition.Sounds.ReloadInsert);

        void OnActionEnded(GunAction action, bool finished)
        {
            _qte?.Cancel(this);
            bool repaired = finished && action == GunAction.ClearingJam;
            if (repaired && _repairQteSucceeded)
                _animation.FinishRepair();
            else
                _animation.EndAction();
            if (!finished)
                return;

            if (repaired)
                OnRepaired(_repairQteSucceeded);
            GunSounds sounds = _definition.Sounds;
            Play(action switch
            {
                GunAction.Reloading => sounds.ReloadEnd,
                GunAction.ClearingJam when Feedback == null => sounds.ClearJamEnd,
                GunAction.Cycling => sounds.CycleEnd,
                _ => null,
            });
        }

        void OnJammed()
        {
            Play(_definition.Sounds.Jam);
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
                _jolt.Play(feedback.JamJolt);
            Beat(GunJamBeatKind.Jammed);
        }

        void OnDryFired()
        {
            Play(_definition.Sounds.DryFire);
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
                _jolt.Play(feedback.DryFireJolt);
            Beat(GunJamBeatKind.DryFire);
        }

        // A slap or punch keyed in the repair clip landed.
        void OnRepairSlapped()
        {
            bool punch = _animation.ImpactIsPunch;
            GunSounds sounds = _definition.Sounds;
            if (punch && sounds.RepairPunches is { Length: > 0 })
                PlayAny(sounds.RepairPunches, sounds.RepairPunchVolume);
            else
                PlayAny(sounds.RepairSlaps, sounds.RepairSlapVolume);

            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
                _jolt.Play(punch ? feedback.PunchJolt : feedback.SlapJolt);
            Beat(punch ? GunJamBeatKind.Punch : GunJamBeatKind.Slap);
        }

        void TryStartRepairQte()
        {
            GunRepairProfile repair = _definition.Jam.Repair;
            if (_qteRequested || _qte == null || _gun.Action != GunAction.ClearingJam || repair == null || repair.Qte == null ||
                _gun.ActionProgress < repair.QteStartProgress)
                return;
            _qteRequested = true;
            _qte.TryStart(this, repair.Qte, _repairQteCompleted, ownerControlled: true);
        }

        void OnRepairQteCompleted(QteResult result)
        {
            if (_gun == null || _gun.Action != GunAction.ClearingJam)
                return;
            if (result == QteResult.Success)
            {
                _repairQteSucceeded = true;
                _gun.CompleteRepair();
            }
            else if (result == QteResult.Cancelled)
                _gun.Interrupt();
        }

        void OnQteAttempted(QteAttempted attempt)
        {
            if (_gun == null || attempt.Result != QteAttemptResult.Miss || !_qte.IsActiveFor(this))
                return;
            GunRepairProfile repair = _definition.Jam.Repair;
            _gun.AddRepairTime(repair.MissPenalty, repair.MaxMissPenalty);
            RepairFeedbackSettings feedback = repair.Feedback;
            if (feedback != null)
            {
                Play(feedback.MissSound, feedback.MissVolume);
                Play(feedback.MissAccent, feedback.MissAccentVolume);
                _jolt.Play(feedback.MissJolt);
            }
            Beat(GunJamBeatKind.Miss);
        }

        void OnRepaired(bool qteSuccess)
        {
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null && qteSuccess)
            {
                Play(feedback.SuccessSound, feedback.SuccessVolume);
                Play(feedback.SuccessAccent, feedback.AccentVolume);
                _mechanismIn = feedback.MechanismDelay;
                _jolt.Play(feedback.SuccessJolt);
            }
            else if (feedback != null)
                Play(feedback.NaturalSound, feedback.NaturalVolume);
            _repairCompleted?.Publish(new GunRepairCompleted(Owner, Item, qteSuccess));
        }

        void TickMechanismSound(float deltaTime)
        {
            if (_mechanismIn < 0f)
                return;
            _mechanismIn -= deltaTime;
            RepairFeedbackSettings feedback = Feedback;
            if (_mechanismIn < 0f && feedback != null)
                Play(feedback.SuccessMechanism, feedback.MechanismVolume);
        }

        void Beat(GunJamBeatKind kind) => _beats?.Publish(new GunJamBeat(Owner, Item, kind));

        // `volume` scales the gun's own sound volume.
        void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip, _definition.Sounds.Volume * volume);
        }

        void PlayAny(AudioClip[] clips, float volume)
        {
            if (clips is { Length: > 0 })
                Play(clips[UnityEngine.Random.Range(0, clips.Length)], volume);
        }

        void UpdatePose(bool sprinting, float deltaTime)
        {
            HandlingSettings handling = _definition.Handling;
            Transform target = sprinting ? _sprintPose : _gun.IsAiming ? _aimPose : _hipPose;
            if (target == null)
                target = _hipPose;
            if (target != null)
            {
                float speed = sprinting ? handling.SprintPoseSpeed : handling.AimSpeed;
                float t = speed > 0f ? 1f - Mathf.Exp(-speed * deltaTime) : 1f;
                _posePosition = Vector3.Lerp(_posePosition, target.localPosition, t);
                _poseRotation = Quaternion.Slerp(_poseRotation, target.localRotation, t);
            }
            ApplyPose();
        }

        void ApplyPose()
        {
            _posePivot.SetLocalPositionAndRotation(_posePosition, _poseRotation);
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
        [ButtonGroup("Preview"), Button("Hip")] void PreviewHip() => Preview(_hipPose);
        [ButtonGroup("Preview"), Button("Aim")] void PreviewAim() => Preview(_aimPose);
        [ButtonGroup("Preview"), Button("Sprint")] void PreviewSprint() => Preview(_sprintPose);

        void Preview(Transform pose)
        {
            if (pose == null || _posePivot == null)
                return;
            UnityEditor.Undo.RecordObject(_posePivot, "Preview gun pose");
            _posePivot.SetLocalPositionAndRotation(pose.localPosition, pose.localRotation);
        }
#endif
    }
}
