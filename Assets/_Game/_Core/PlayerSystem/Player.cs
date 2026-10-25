using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.Stats;
using Aipocalypse.Core.TimeSystem;
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
    // The player's view: builds its parts, turns input into an intent and ticks them in order (look → move → vitals → head
    // motion → interaction → held item → pose sequence → light). Life and death: PlayerLife; move sounds: PlayerFeedback.
    [RequireComponent(typeof(CharacterController))]
    public sealed class Player : MonoBehaviour, IPlayer, IEquipmentOwner, IGunUser
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
        [SerializeField, Tooltip("Optional: lights the dark around the player; a worn lantern takes it over. A child of the root")] Light _visionLight;
        [SerializeField, Tooltip("How dark it is for the vision light (Darkness source); the scene's director ticks it. None or no director = always day")]
        TimeOfDayWeight _darkness;

        InputService _input;
        TimeService _time;
        IStatProvider _stats;
        PlayerRegistry _registry;

        PlayerLife _life;
        PlayerMotor _motor;
        PlayerCameraRig _cameraRig;
        PlayerLook _look;
        PlayerHeadBob _headBob;
        HeldItemSway _heldSway;
        PlayerZoom _zoom;
        PlayerInteractor _interactor;
        HeldItemClearance _heldClearance;
        PlayerBreathing _breathing;
        PlayerBreathSound _breathSound;
        PlayerAim _aim;
        PoseSequencePlayer _sequence;
        PlayerFeedback _feedback;
        HeldItemDriver _held;

        [ShowInInspector, ReadOnly, HideInEditorMode] public Health Health => _life?.Health;
        [ShowInInspector, ReadOnly, HideInEditorMode] public Stamina Stamina { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public Fatigue Fatigue { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public PlayerVision Vision { get; private set; }
        public Equipment Equipment { get; private set; }
        public AmmoReserve Ammo { get; private set; }
        public PlayerInteractor Interactor => _interactor;
        public HeldItem HeldItem => _equipmentRig != null ? _equipmentRig.Current : null;
        public bool IsDead => _life != null && _life.IsDead;
        // A pose sequence (dying, sitting…) has the camera and the body until it's stopped.
        public bool InSequence => _sequence != null && _sequence.IsPlaying;
        public PoseSequence CurrentSequence => _sequence?.Current;
        public bool IsSprinting => _motor != null && _motor.IsSprinting;
        public Vector2 CrosshairPosition => _aim != null ? _aim.CrosshairPosition : default;
        public float CrosshairSpread => _aim != null ? _aim.CrosshairSpread : 0f;
        // Where the player entered the scene; respawns go back there.
        public Vector3 SpawnPosition => _life != null ? _life.SpawnPosition : transform.position;

        Transform IPlayerTarget.Transform => transform;
        Transform IWeaponUser.Eye => _cameraPitch;
        Transform IWeaponUser.Root => transform;
        AimOrigin IGunUser.AimFrom => _tuning.HeldItem.AimFrom;
        float IGunUser.AirSpreadScale => _stats.GetScale(StatId.AirSpreadScale);
        float IGunUser.FatigueRecoilScale => Fatigue.RecoilMultiplier;
        Quaternion IGunUser.AimOffset => _heldSway.AimRotation;
        float IWeaponUser.MeleePower => Fatigue.MeleePower;

        void IWeaponUser.Exert(float stamina) => Stamina.Spend(stamina * _stats.GetScale(StatId.AttackStaminaCostScale));

        [Inject]
        void Construct(InputService input, TimeService time, ControlSettings controls, IStatProvider stats, DamageService damage, AudioService audio,
            IPublisher<PlayerDied> died, IPublisher<PlayerDeathSequenceEnded> deathEnded, IPublisher<PlayerRevived> revived, PlayerRegistry registry,
            IObjectResolver resolver)
        {
            _input = input;
            _time = time;
            _stats = stats;
            _registry = registry;

            PlayerVitalsTuning vitals = _tuning.Vitals;
            _life = new PlayerLife(this, transform, stats.Get(StatId.MaxHealth, vitals.MaxHealth), vitals, damage, died, deathEnded, revived);
            Stamina = new Stamina(stats.Get(StatId.MaxStamina, vitals.MaxStamina), vitals.StaminaRegenDelay, vitals.ExhaustionRecoverFraction, vitals.WindedTime);
            Fatigue = new Fatigue(vitals.Fatigue);
            _breathing = new PlayerBreathing(vitals.Breathing);
            _breathSound = new PlayerBreathSound(audio, _tuning.Sounds);
            _motor = new PlayerMotor(_controller, _tuning.Movement, stats, Stamina);
            _cameraRig = new PlayerCameraRig(_cameraRoot, _cameraPitch, _tuning.Camera.EyeHeight);
            _look = new PlayerLook(transform, _cameraRig, _tuning.Camera);
            _headBob = new PlayerHeadBob(_cameraRig, _tuning.Camera);
            _heldSway = new HeldItemSway(_tuning.HeldItem.HeldSway, _tuning.HeldItem.AimSway, stats);
            _zoom = new PlayerZoom(_camera, _firstPersonView, controls);
            _aim = new PlayerAim(_equipmentRig, _zoom);
            _interactor = new PlayerInteractor(_cameraPitch, gameObject, _tuning.Interaction);
            _heldClearance = new HeldItemClearance(_cameraPitch, transform, _tuning.HeldItem, _equipmentRig);
            if (_visionLight != null && _tuning.Vision != null)
                Vision = new PlayerVision(_visionLight, _tuning.Vision);
            _sequence = new PoseSequencePlayer(transform, _cameraRig, audio);
            _feedback = new PlayerFeedback(_motor, _headBob, _heldSway, audio, _tuning.Sounds);

            _life.Died += OnDied;
            _life.Reviving += OnReviving;
            _sequence.Ended += OnSequenceEnded;
            _motor.Landed += _life.OnLanded;
            _stats.Changed += OnStatsChanged;
            Ammo = _ammoConfig != null ? _ammoConfig.CreateReserve() : new AmmoReserve();

            if (_equipmentConfig != null && _equipmentRig != null)
            {
                Equipment = _equipmentConfig.CreateEquipment();
                Equipment.EquipFirstImmediately();
                _equipmentRig.Bind(Equipment, this, resolver);
                _held = new HeldItemDriver(transform, Equipment, _equipmentRig, _equipmentConfig, _tuning, _motor, _look, _headBob, _zoom, _heldSway,
                    _aim, _heldClearance, _interactor, Stamina, Fatigue, _breathing, _hands);
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
            bool paused = _time != null && _time.IsPaused;
            _breathSound?.SetPaused(paused);
            _sequence?.SetPaused(paused);
            if (_motor == null || deltaTime <= 0f)
                return;

            bool inSequence = InSequence;
            bool canAct = !IsDead && !inSequence && _input.GameplayEnabled;
            PlayerIntent intent = canAct ? ReadIntent() : default;
            HeldItem held = HeldItem;
            if (intent.Sprint && held != null && held.StopsSprint(intent.Fire, intent.Aim))
                intent.Sprint = false;

            if (!inSequence)
                _look.Tick(intent.LookDegrees);
            _motor.Tick(intent, deltaTime);
            // A lethal landing kills inside the motor tick: the rest of this frame already belongs to the dead.
            if (canAct && (IsDead || InSequence))
            {
                canAct = false;
                inSequence = InSequence;
                intent = default;
            }
            Stamina.Tick(deltaTime, _stats.Get(StatId.StaminaRegen, _tuning.Vitals.StaminaRegenPerSecond), _motor.IsIdle);
            Fatigue.Tick(Stamina.Normalized, _stats.GetScale(StatId.FatiguePenaltyScale), deltaTime, Stamina.IsWinded);
            _breathing.Tick(Stamina.Normalized, Stamina.IsExhausted && !IsDead, deltaTime);
            _breathSound.Tick(_breathing, IsDead);
            if (!inSequence)
                _headBob.Tick(_motor, deltaTime);
            if (canAct)
                _interactor.Tick(intent.Interact);
            else
                _interactor.Clear();
            _held?.Tick(intent, ownsZoom: !inSequence, deltaTime);
            // After the held item, so the sequence's camera and field of view win over its kick and zoom.
            if (inSequence)
            {
                _sequence.Tick(deltaTime);
                _zoom.Tick(_sequence.FovScale);
            }
            TickVision(deltaTime);
        }

        void OnControllerColliderHit(ControllerColliderHit hit) => _motor?.OnControllerHit(hit);

        void OnDestroy()
        {
            _registry?.Unregister(this);
            if (_life != null)
            {
                _life.Died -= OnDied;
                _life.Reviving -= OnReviving;
                _motor.Landed -= _life.OnLanded;
                _stats.Changed -= OnStatsChanged;
                _feedback.Dispose();
            }
            _breathSound?.Stop();
            if (_sequence != null)
            {
                _sequence.Ended -= OnSequenceEnded;
                _sequence.Stop();
            }
            Equipment?.Dispose();
        }

        // Hurt through DamageService.Deal, never directly.
        DamageResult IDamageable.TakeDamage(in DamageInfo info) => _life != null ? _life.TakeDamage(info) : default;

        [Button, HideInEditorMode]
        public void Revive() => _life?.Revive();

        public void MoveToSpawn()
        {
            if (_life != null)
                _motor.Teleport(_life.SpawnPosition, _life.SpawnRotation);
        }

        // Hands the camera and body to a scripted moment (dying, resting…).
        public void PlaySequence(PoseSequence sequence)
        {
            if (_sequence == null || sequence == null)
                return;
            if (Equipment != null && sequence.LowerHands)
                Equipment.LowerHands();
            else if (Equipment != null && Equipment.HandsLowered)
                Equipment.RaiseHands();
            _sequence.Play(sequence, _look.Pitch);
        }

        // Gives the camera back, level, and raises the held item again.
        public void StopSequence()
        {
            if (InSequence)
            {
                _sequence.Stop();
                _look.ResetPitch(0f);
                _zoom.Tick(1f);
            }
            if (Equipment != null && Equipment.HandsLowered)
                Equipment.RaiseHands();
        }

        void OnDied(DamageType cause) => PlaySequence(_tuning.DeathSequence);

        void OnReviving()
        {
            Stamina.Restore();
            Fatigue.Reset();
            _breathing.Reset();
            StopSequence();
        }

        void OnSequenceEnded(PoseSequence sequence) => _life.OnDeathSequenceEnded();

        // A perk or buff changed: maxima are cached in the meters, so they're read again.
        void OnStatsChanged()
        {
            Health.SetMax(_stats.Get(StatId.MaxHealth, _tuning.Vitals.MaxHealth));
            Stamina.SetMax(_stats.Get(StatId.MaxStamina, _tuning.Vitals.MaxStamina));
        }

        [Button, HideInEditorMode]
        void AddAmmo(AmmoType type, int amount = 30) => Ammo?.Add(type, amount);

        // Look follows the zoom, so the crosshair sweeps the same share of the screen per mouse move when aiming.
        PlayerIntent ReadIntent() => new()
        {
            Move = _input.Move,
            LookDegrees = _input.ReadLookDegrees(Time.unscaledDeltaTime) * _zoom.LookScale,
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

        void TickVision(float deltaTime)
        {
            if (Vision == null)
                return;
            float darkness = _darkness != null ? Mathf.Max(0f, _darkness.Weight) : 0f;
            var lantern = Equipment?.Belt?.Definition as LanternDefinition;
            Vision.Tick(darkness, _stats.Get(StatId.VisionRadius, _tuning.Vision.Radius), lantern, Time.time, deltaTime);
        }

        void Reset() => _controller = GetComponent<CharacterController>();
    }
}
