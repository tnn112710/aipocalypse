using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.Stats;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Input;
using MessagePipe;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Aipocalypse.Core.PlayerSystem
{
    // The player's view: turns input into an intent and drives look → movement → stamina → fatigue and breathing → head motion → interaction → equipment
    // (switching, then the item in hand: fire, aim, reload) → recoil turn, view kick and zoom → sway and aim (crosshair, model turn), in that order.
    [RequireComponent(typeof(CharacterController))]
    public sealed class Player : MonoBehaviour, IDamageable, IEquipmentOwner, IGunUser
    {
        [SerializeField] PlayerTuning _tuning;
        [SerializeField] CharacterController _controller;
        [SerializeField, Tooltip("At eye height; head bob and landing dip move it")] Transform _cameraRoot;
        [SerializeField, Tooltip("Child of the camera root; pitch rotates it and the Cinemachine camera tracks it")] Transform _cameraPitch;
        [SerializeField, Tooltip("Its lens narrows while the held item zooms (aiming down sights)")] CinemachineCamera _camera;
        [SerializeField, FormerlySerializedAs("_weaponCamera"), Tooltip("Keeps the held model's field of view while the world zooms")] FirstPersonView _firstPersonView;
        [SerializeField] EquipmentConfig _equipmentConfig;
        [SerializeField] EquipmentRig _equipmentRig;
        [SerializeField, Tooltip("Optional: floating hands on the held item's grips")] FirstPersonHands _hands;
        [SerializeField, Tooltip("Starting reserve ammo, or infinite")] AmmoConfig _ammoConfig;

        InputService _input;
        IStatProvider _stats;
        DamageService _damage;
        AudioService _audio;
        IPublisher<PlayerDied> _died;
        PlayerRegistry _registry;

        PlayerMotor _motor;
        PlayerLook _look;
        PlayerHeadBob _headBob;
        HeldItemSway _heldSway;
        PlayerZoom _zoom;
        PlayerInteractor _interactor;
        HeldItemClearance _heldClearance;
        PlayerBreathing _breathing;
        PlayerBreathSound _breathSound;
        PlayerAim _aim;

        [ShowInInspector, ReadOnly, HideInEditorMode] public Health Health { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public Stamina Stamina { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public Fatigue Fatigue { get; private set; }
        public Equipment Equipment { get; private set; }
        public AmmoReserve Ammo { get; private set; }
        public PlayerInteractor Interactor => _interactor;
        public HeldItem HeldItem => _equipmentRig != null ? _equipmentRig.Current : null;
        public bool IsDead => Health != null && Health.IsDead;
        public bool IsSprinting => _motor != null && _motor.IsSprinting;
        // Where the crosshair sits this frame, off the screen centre, in units of half the screen height (x = right, y = up).
        public Vector2 CrosshairPosition => _aim != null ? _aim.CrosshairPosition : default;
        // Radius of the held item's spread cone on screen, in the same units.
        public float CrosshairSpread => _aim != null ? _aim.CrosshairSpread : 0f;

        Transform IWeaponUser.Eye => _cameraPitch;
        Transform IWeaponUser.Root => transform;
        AimOrigin IGunUser.AimFrom => _tuning.HeldItem.AimFrom;
        float IGunUser.AirSpreadScale => _stats.Get(StatId.AirSpread, 1f);
        float IGunUser.FatigueRecoilScale => Fatigue.RecoilMultiplier;
        Quaternion IGunUser.AimOffset => _heldSway.AimRotation;
        float IWeaponUser.MeleePower => Fatigue.MeleePower;

        void IWeaponUser.Exert(float stamina) => Stamina.Spend(stamina * _stats.Get(StatId.AttackStaminaCost, 1f), delaysRegen: false);

        [Inject]
        void Construct(InputService input, IStatProvider stats, DamageService damage, AudioService audio, IPublisher<PlayerDied> died,
            PlayerRegistry registry, IObjectResolver resolver)
        {
            _input = input;
            _registry = registry;
            _stats = stats;
            _damage = damage;
            _audio = audio;
            _died = died;

            Health = new Health(stats.Get(StatId.MaxHealth, _tuning.Vitals.MaxHealth));
            Stamina = new Stamina(stats.Get(StatId.MaxStamina, _tuning.Vitals.MaxStamina), _tuning.Vitals.StaminaRegenDelay,
                _tuning.Vitals.ExhaustionRecoverFraction, _tuning.Vitals.WindedTime);
            Fatigue = new Fatigue(_tuning.Vitals.Fatigue);
            _breathing = new PlayerBreathing(_tuning.Sounds.Breathing);
            _breathSound = new PlayerBreathSound(audio, _tuning.Sounds);
            _motor = new PlayerMotor(_controller, _tuning.Movement, stats, Stamina);
            _look = new PlayerLook(transform, _cameraPitch, _tuning.Camera);
            _headBob = new PlayerHeadBob(_cameraRoot, _tuning.Camera);
            _heldSway = new HeldItemSway(_tuning.HeldItem.HeldSway, _tuning.HeldItem.AimSway, stats);
            _zoom = new PlayerZoom(_camera, _firstPersonView);
            _aim = new PlayerAim(_equipmentRig, _zoom);
            _interactor = new PlayerInteractor(_cameraPitch, gameObject, _tuning.Interaction);
            _heldClearance = new HeldItemClearance(_cameraPitch, transform, _tuning.HeldItem, _equipmentRig);

            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _headBob.Stepped += OnStepped;
            Ammo = _ammoConfig != null ? _ammoConfig.CreateReserve() : new AmmoReserve();

            if (_equipmentConfig != null && _equipmentRig != null)
            {
                Equipment = new Equipment(_equipmentConfig.CreateHotbar());
                Equipment.EquipFirstImmediately();
                _equipmentRig.Bind(Equipment, this, resolver);
            }
            else
            {
                Debug.LogError($"{name}: Equipment Config or Equipment Rig not assigned; equipment is off.", this);
            }

            // Last, so views outside the prefab (HUD) see a fully built player.
            _registry.Register(this);
        }

        void Start()
        {
            if (_motor == null)
                Debug.LogError($"{name}: not injected. The Player prefab needs PlayerScope on its root.", this);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            _breathSound?.SetPaused(deltaTime <= 0f);
            if (_motor == null || deltaTime <= 0f)
                return;

            bool canAct = !IsDead && _input.GameplayEnabled;
            PlayerIntent intent = canAct ? ReadIntent(deltaTime) : default;
            HeldItem held = HeldItem;
            if (intent.Sprint && held != null && held.StopsSprint(intent.Fire, intent.Aim))
                intent.Sprint = false;

            _look.Tick(intent.LookDegrees);
            _motor.Tick(intent, deltaTime);
            Stamina.Tick(deltaTime, _stats.Get(StatId.StaminaRegen, _tuning.Vitals.StaminaRegenPerSecond));
            Fatigue.Tick(Stamina.Normalized, _stats.Get(StatId.FatiguePenalty, 1f), deltaTime, Stamina.IsWinded);
            _breathing.Tick(Stamina.Normalized, Stamina.IsExhausted && !IsDead, deltaTime);
            _breathSound.Tick(_breathing, IsDead);
            _headBob.Tick(_motor, deltaTime);
            if (canAct)
                _interactor.Tick(intent.Interact);
            else
                _interactor.Clear();
            TickEquipment(intent, deltaTime);
        }

        void OnControllerColliderHit(ControllerColliderHit hit) => _motor?.OnControllerHit(hit);

        void OnDestroy()
        {
            _registry?.Unregister(this);
            if (_motor != null)
            {
                _motor.Jumped -= OnJumped;
                _motor.Landed -= OnLanded;
                _headBob.Stepped -= OnStepped;
            }
            _breathSound?.Stop();
            Equipment?.Dispose();
        }

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (Health == null)
                return default;
            float applied = Health.Damage(info.Amount);
            if (applied > 0f && Health.IsDead)
                _died.Publish(new PlayerDied(transform.position, info.Type));
            return new DamageResult(applied, applied > 0f && Health.IsDead);
        }

        [Button, HideInEditorMode]
        public void Revive()
        {
            Health?.Restore();
            Stamina?.Restore();
            Fatigue?.Reset();
            _breathing?.Reset();
        }

        [Button, HideInEditorMode]
        void AddAmmo(AmmoType type, int amount = 30) => Ammo?.Add(type, amount);

        // Look follows the zoom, so the crosshair sweeps the same share of the screen per mouse move when aiming.
        PlayerIntent ReadIntent(float deltaTime) => new()
        {
            Move = _input.Move,
            LookDegrees = _input.ReadLookDegrees(deltaTime) * _zoom.LookScale,
            Sprint = _input.SprintHeld,
            Jump = _input.JumpPressed,
            SelectSlot = _input.SlotPressed is var slot and >= 0 ? slot : null,
            CycleSlot = _input.CycleDirection,
            Interact = _input.InteractPressed,
            Drop = _input.DropPressed,
            Fire = _input.AttackHeld,
            FirePressed = _input.AttackPressed,
            Aim = _input.AimHeld,
            Reload = _input.ReloadPressed,
        };

        void TickEquipment(in PlayerIntent intent, float deltaTime)
        {
            if (Equipment == null)
                return;

            if (intent.Drop)
                DropSelected();
            else if (intent.SelectSlot is int slot)
                Equipment.Select(slot);
            else if (intent.CycleSlot != 0)
                Equipment.Cycle(intent.CycleSlot);

            Equipment.Tick(deltaTime, _equipmentConfig.SwitchSpeed);
            _equipmentRig.Tick();
            _equipmentRig.TickHeld(new HeldItemInput
            {
                UseHeld = intent.Fire,
                UsePressed = intent.FirePressed,
                AimHeld = intent.Aim,
                ReloadPressed = intent.Reload,
                IsSprinting = _motor.IsSprinting,
                IsMoving = _motor.HorizontalVelocity.sqrMagnitude > 0.25f,
                IsAirborne = !_motor.IsGrounded,
            }, deltaTime);

            HeldItem held = _equipmentRig.Current;
            if (held != null)
                _look.Turn(held.LookTurn);
            _look.SetKick(held != null ? held.ViewKick : default);
            // Before the sway: the model's aim turn depends on this frame's zoom.
            _zoom.Tick(held != null ? held.Zoom : 1f);
            TickHeldSway(intent, held, deltaTime);
            _motor.SpeedMultiplier = (held != null && held.IsAiming ? _tuning.HeldItem.AimMoveSpeedMultiplier : 1f) * Fatigue.MoveSpeed;
            // Apply the obstacle pose before shots use the final muzzle position.
            _heldClearance.Tick(held, deltaTime);
            if (held != null)
                held.LateTick();
            if (_hands != null)
                _hands.Tick(held != null ? held.Hands : null);
        }

        void TickHeldSway(in PlayerIntent intent, HeldItem held, float deltaTime)
        {
            Vector3 velocity = transform.InverseTransformDirection(_motor.HorizontalVelocity);
            velocity.y = _motor.IsGrounded ? 0f : _motor.VerticalVelocity;
            _heldSway.Tick(new HeldSwayInput
            {
                LookDegrees = intent.LookDegrees,
                LocalVelocity = velocity,
                IsAirborne = !_motor.IsGrounded,
                BobPhase = _headBob.Phase,
                BobIntensity = _headBob.Intensity,
                SprintBlend = _headBob.SprintBlend,
                AimWeight = held != null ? held.AimWeight : 0f,
                IsAiming = held != null && held.IsAiming,
                Tiredness = _breathing.Tiredness,
                IsWinded = Stamina.IsWinded,
            }, deltaTime);
            _equipmentRig.ApplyHandMotion(_heldSway.Position, _heldSway.Rotation);
            _aim.Tick(_heldSway.AimRotation, held != null ? held.Spread : 0f);
        }

        void DropSelected()
        {
            ItemInstance item = Equipment.DropSelected();
            if (item != null)
                WorldItem.Spawn(item, _interactor.FindDropPoint(), Quaternion.Euler(0f, transform.eulerAngles.y + 90f, 0f), null);
        }

        void OnStepped() => _audio.Play(_tuning.Sounds.FootstepSound, _motor.IsSprinting ? 1f : _tuning.Sounds.WalkStepVolume);

        void OnJumped()
        {
            _heldSway.OnJumped();
            _audio.Play(_tuning.Sounds.JumpSound);
        }

        void OnLanded(LandingInfo landing)
        {
            _headBob.OnLanded(landing.ImpactSpeed);
            _heldSway.OnLanded(landing.ImpactSpeed);
            if (landing.ImpactSpeed >= _tuning.Sounds.LandingSoundMinSpeed)
                _audio.Play(_tuning.Sounds.LandSound, Mathf.Clamp01(landing.ImpactSpeed / _tuning.Sounds.LandingSoundFullSpeed));

            float damage = FallDamage.For(landing.DropHeight, _tuning.Vitals.SafeFallHeight, _tuning.Vitals.LethalFallHeight, Health.Max);
            if (damage > 0f)
                _damage.Deal(this, new DamageInfo(damage, DamageType.Fall, landing.Point, default, gameObject));
        }

        void Reset() => _controller = GetComponent<CharacterController>();
    }
}
