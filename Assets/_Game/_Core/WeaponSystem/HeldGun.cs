using Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.WeaponSystem
{
    // A gun in hand: runs its Gun rules from the holder's input and shows them — bullets from the muzzle, recoil,
    // hip / aim / sprint poses, procedural reload and jam-clearing motion, sounds.
    // Prefab layout: Poses (Hip, Aim, Sprint markers) and PosePivot > RecoilPivot > Model + Muzzle.
    public sealed class HeldGun : HeldItem
    {
        [SerializeField, Tooltip("Moved to the active pose; parent of the recoil pivot")] Transform _posePivot;
        [SerializeField, Tooltip("Gun kick is applied here; parent of the model and the muzzle")] Transform _recoilPivot;
        [SerializeField, Tooltip("Bullets and the muzzle flash leave here, along its forward (blue axis)")] Transform _muzzle;
        [SerializeField, Tooltip("2D source for the gun's own sounds")] AudioSource _audio;

        [BoxGroup("Poses"), SerializeField, Tooltip("Siblings of the pose pivot; move them to pose the gun")] Transform _hipPose;
        [BoxGroup("Poses"), SerializeField] Transform _aimPose;
        [BoxGroup("Poses"), SerializeField] Transform _sprintPose;

        // Temporary procedural motion until there are reload / jam animations.
        [BoxGroup("Action motion"), SerializeField, Tooltip("Gun offset while reloading")] Vector3 _reloadOffset = new(-0.02f, -0.06f, -0.02f);
        [BoxGroup("Action motion"), SerializeField] Vector3 _reloadEuler = new(-12f, -10f, 35f);
        [BoxGroup("Action motion"), SerializeField, Tooltip("Dip as each round goes in (per-round reload)")] Vector3 _roundInsertOffset = new(0f, -0.025f, 0f);
        [BoxGroup("Action motion"), SerializeField, Tooltip("Gun offset while clearing a jam")] Vector3 _clearJamOffset = new(0.02f, -0.04f, -0.03f);
        [BoxGroup("Action motion"), SerializeField] Vector3 _clearJamEuler = new(-5f, 25f, -50f);
        [BoxGroup("Action motion"), SerializeField, Tooltip("Pulling the action back while clearing a jam (m)")] float _rackDistance = 0.05f;
        [BoxGroup("Action motion"), SerializeField, Min(0f), Tooltip("Racks per jam clear")] int _racks = 2;
        [BoxGroup("Action motion"), SerializeField, Min(0f)] float _actionBlendSpeed = 12f;

        BulletSystem _bullets;
        EffectPool _effects;
        IGunUser _user;
        GunDefinition _definition;
        Gun _gun;
        GunRecoil _recoil;

        Vector3 _restRecoilPosition;
        Quaternion _restRecoilRotation;
        Vector3 _posePosition;
        Quaternion _poseRotation;
        float _stateMultiplier = 1f;
        float _actionWeight;
        GunAction _shownAction;
        float _insertBump;

        public Gun Gun => _gun;
        public override bool IsAiming => _gun != null && _gun.IsAiming;
        public override ViewKick ViewKick => _recoil != null ? new ViewKick(_recoil.ViewEuler, _recoil.ViewShake) : default;

        [Inject]
        void Construct(BulletSystem bullets, EffectPool effects)
        {
            _bullets = bullets;
            _effects = effects;
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
                Debug.LogError($"{name}: not injected; spawn it through the EquipmentRig.", this);

            _definition = item.Gun;
            if (_definition.Magazine.AmmoType == null)
                Debug.LogWarning($"{_definition.name} has no ammo type and can't be reloaded.", _definition);

            _gun = new Gun(item, _user.Ammo);
            _recoil = new GunRecoil(_definition.Recoil);
            _gun.Fired += OnFired;
            _gun.Jammed += OnJammed;
            _gun.ActionStarted += OnActionStarted;
            _gun.RoundLoaded += OnRoundLoaded;
            _gun.ActionEnded += OnActionEnded;

            if (_definition.Effects != null && _effects != null)
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

            _recoil.Tick(deltaTime, _gun.IsAiming);
            UpdatePose(input.IsSprinting && input.IsReady, deltaTime);
        }

        protected override void OnShown()
        {
            if (_definition != null)
                Play(_definition.Sounds.Equip);
            SnapTo(_hipPose);
            _actionWeight = 0f;
            _insertBump = 0f;
            ApplyPose();
        }

        protected override void OnHidden()
        {
            _gun?.Interrupt();
            _recoil?.Reset();
        }

        void OnDestroy()
        {
            if (_gun == null)
                return;
            _gun.Fired -= OnFired;
            _gun.Jammed -= OnJammed;
            _gun.ActionStarted -= OnActionStarted;
            _gun.RoundLoaded -= OnRoundLoaded;
            _gun.ActionEnded -= OnActionEnded;
        }

        void OnFired()
        {
            _recoil.Kick(_gun.IsAiming, _stateMultiplier);
            Play(_definition.Sounds.Fire);

            BulletEffects effects = _definition.Effects;
            if (effects != null)
                _effects.Spawn(effects.MuzzleFlash, _muzzle.position, _muzzle.rotation, _muzzle);

            Transform from = _user.AimFrom == AimOrigin.Camera ? _user.Eye : _muzzle;
            FireSettings fire = _definition.Fire;
            BulletSettings bullet = _definition.Bullet;
            for (int i = 0; i < fire.Pellets; i++)
            {
                _bullets.Fire(new BulletShot
                {
                    Origin = from.position,
                    Direction = BulletSpread.Apply(from.forward, fire.SpreadAngle),
                    VisualOrigin = _muzzle.position,
                    Speed = bullet.Speed,
                    Range = bullet.Range,
                    GravityScale = bullet.GravityScale,
                    Damage = fire.Damage,
                    Ignore = _user.Root,
                    Effects = effects,
                });
            }
        }

        void OnJammed() => Play(_definition.Sounds.Jam);

        void OnActionStarted(GunAction action)
        {
            _shownAction = action;
            Play(action == GunAction.Reloading ? _definition.Sounds.ReloadStart : _definition.Sounds.ClearJamStart);
        }

        void OnRoundLoaded()
        {
            _insertBump = 1f;
            Play(_definition.Sounds.ReloadInsert);
        }

        void OnActionEnded(GunAction action, bool finished)
        {
            if (finished)
                Play(action == GunAction.Reloading ? _definition.Sounds.ReloadEnd : _definition.Sounds.ClearJamEnd);
        }

        void Play(AudioClip clip)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip, _definition.Sounds.Volume);
        }

        void UpdatePose(bool sprinting, float deltaTime)
        {
            HandlingSettings handling = _definition.Handling;
            Transform target = sprinting ? _sprintPose : _gun.IsAiming ? _aimPose : _hipPose;
            if (target == null)
                target = _hipPose;
            float speed = sprinting ? handling.SprintPoseSpeed : handling.AimSpeed;
            float t = speed > 0f ? 1f - Mathf.Exp(-speed * deltaTime) : 1f;
            if (target != null)
            {
                _posePosition = Vector3.Lerp(_posePosition, target.localPosition, t);
                _poseRotation = Quaternion.Slerp(_poseRotation, target.localRotation, t);
            }

            float targetWeight = _gun.Action != GunAction.None ? 1f : 0f;
            _actionWeight = Mathf.Lerp(_actionWeight, targetWeight, 1f - Mathf.Exp(-_actionBlendSpeed * deltaTime));
            _insertBump = Mathf.Lerp(_insertBump, 0f, 1f - Mathf.Exp(-15f * deltaTime));
            ApplyPose();
        }

        void ApplyPose()
        {
            Vector3 offset;
            Vector3 euler;
            if (_shownAction == GunAction.ClearingJam)
            {
                // Racks the action back and forth while tilted.
                float progress = _gun != null && _gun.Action == GunAction.ClearingJam ? _gun.ActionProgress : 0f;
                float rack = Mathf.Sin(progress * Mathf.PI * Mathf.Max(1, _racks));
                offset = _clearJamOffset + Vector3.back * (_rackDistance * Mathf.Abs(rack));
                euler = _clearJamEuler;
            }
            else
            {
                offset = _reloadOffset + _roundInsertOffset * _insertBump;
                euler = _reloadEuler;
            }

            _posePivot.localPosition = _posePosition + offset * _actionWeight;
            _posePivot.localRotation = _poseRotation * Quaternion.Euler(euler * _actionWeight);

            if (_recoil != null)
            {
                _recoilPivot.localPosition = _restRecoilPosition + Vector3.back * _recoil.GunKickBack;
                _recoilPivot.localRotation = _restRecoilRotation * Quaternion.Euler(_recoil.GunEuler);
            }
        }

        void SnapTo(Transform pose)
        {
            if (pose == null)
                return;
            _posePosition = pose.localPosition;
            _poseRotation = pose.localRotation;
        }

#if UNITY_EDITOR
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
