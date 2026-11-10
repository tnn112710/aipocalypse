using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Ghosts;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.Stats;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // The player's view: builds its parts, turns input into an intent and ticks them in order (look → move → vitals → head
    // motion → interaction → held item → pose sequence → light). Life and death: PlayerLife; move sounds: PlayerFeedback.
    [RequireComponent(typeof(CharacterController))]
    public sealed class Player : MonoBehaviour, IPlayer, IEquipmentOwner, IGunUser, IRepairer, ILooter
    {
        // Metres below the eye that auto-collected items fly to (about the chest).
        const float MagnetPullDrop = 0.5f;

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
        PlayerRepair _repair;
        PlayerLooting _looting;
        ItemMagnet _magnet;

        [ShowInInspector, ReadOnly, HideInEditorMode] public Health Health => _life?.Health;
        [ShowInInspector, ReadOnly, HideInEditorMode] public Stamina Stamina { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public Fatigue Fatigue { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public PlayerVision Vision { get; private set; }
        public Equipment Equipment { get; private set; }
        public AmmoReserve Ammo { get; private set; }
        public PlayerInteractor Interactor => _interactor;
        public PlayerRepair Repair => _repair;
        public PlayerLooting Looting => _looting;
        public HeldItem HeldItem => _equipmentRig != null ? _equipmentRig.Current : null;
        public bool IsDead => _life != null && _life.IsDead;
        // A pose sequence (dying, sitting…) has the camera and the body until it's stopped.
        public bool InSequence => _sequence != null && _sequence.IsPlaying;
        public PoseSequence CurrentSequence => _sequence?.Current;
        public bool IsSprinting => _motor != null && _motor.IsSprinting;
        public bool IsCrouching => _motor != null && _motor.IsCrouching;
        public Vector2 CrosshairPosition => _aim != null ? _aim.CrosshairPosition : default;
        public float CrosshairSpread => _aim != null ? _aim.CrosshairSpread : 0f;
        // Where the player entered the scene; respawns go back there.
        public Vector3 SpawnPosition => _life != null ? _life.SpawnPosition : transform.position;
        public Transform View => _cameraPitch;
        public bool Invulnerable
        {
            get => _life != null && _life.Invulnerable;
            set
            {
                if (_life != null)
                    _life.Invulnerable = value;
            }
        }
        public bool IgnoredByEnemies { get; set; }
        public IPlayerIntentSource IntentSource { get; set; }
        public bool IsTargetable => !IsDead && !IgnoredByEnemies;

        Transform ITargetable.Transform => transform;
        TargetKind ITargetable.Kind => TargetKind.Player;
        Transform IWeaponUser.Eye => _cameraPitch;
        Transform IWeaponUser.Root => transform;
        AimOrigin IGunUser.AimFrom => _tuning.HeldItem.AimFrom;
        float IGunUser.AirSpreadScale => _stats.GetScale(StatId.AirSpreadScale);
        float IGunUser.FatigueRecoilScale => Fatigue.RecoilMultiplier;
        Quaternion IGunUser.AimOffset => _heldSway.AimRotation;
        float IWeaponUser.MeleePower => Fatigue.MeleePower;

        bool IRepairer.HasTool => _repair != null && _repair.HasTool;
        bool IRepairer.IsRepairing => _repair != null && _repair.IsBusy;

        void IWeaponUser.Exert(float stamina) => Stamina.Spend(stamina * _stats.GetScale(StatId.AttackStaminaCostScale));

        void ILooter.BeginLoot(LootContainer container)
        {
            if (!IsDead && !InSequence)
                _looting?.Begin(container);
        }

        // Throws an item that's already out of the inventory to the ground in front (the inventory screen's drop).
        public void ThrowAway(ItemInstance item)
        {
            if (item != null && !item.IsEmpty)
                _interactor?.Throw(item, transform.eulerAngles.y + _tuning.Interaction.DropYaw);
        }

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
            _feedback = new PlayerFeedback(_motor, _headBob, _heldSway, audio, _tuning.Sounds, _life);

            _life.Died += OnDied;
            _life.Reviving += OnReviving;
            _sequence.Ended += OnSequenceEnded;
            _motor.Landed += _life.OnLanded;
            _stats.Changed += OnStatsChanged;
            _looting = new PlayerLooting(_tuning.Interaction);

            if (_equipmentConfig != null && _equipmentRig != null)
            {
                Equipment = _equipmentConfig.CreateEquipment();
                // Before the rig binds: its guns reload from it.
                Ammo = _ammoConfig != null ? _ammoConfig.CreateReserve(Equipment.Inventory) : new AmmoReserve(Equipment.Inventory);
                _magnet = new ItemMagnet(transform, Equipment, _tuning.Interaction);
                _interactor.Thrown += _magnet.Ignore;
                Equipment.EquipFirstImmediately();
                _equipmentRig.Bind(Equipment, this, resolver);
                _held = new HeldItemDriver(transform, Equipment, _equipmentRig, _equipmentConfig, _tuning, _motor, _look, _headBob, _zoom, _heldSway,
                    _aim, _heldClearance, _interactor, Stamina, Fatigue, _breathing, _hands);
            }
            else
            {
                Debug.LogError($"{name}: Equipment Config or Equipment Rig not assigned; equipment is off.", this);
                Ammo = new AmmoReserve(new Inventory(new ItemContainer(0), new ItemContainer(0), 0, 0f));
            }

            _repair = new PlayerRepair(gameObject, _cameraPitch, Equipment, _equipmentRig, _interactor, _tuning.Interaction);

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
            PlayerIntent intent = canAct ? ReadIntent(deltaTime) : default;
            HeldItem held = HeldItem;
            if (intent.Sprint && held != null && held.StopsSprint(intent.Fire, intent.Aim))
                intent.Sprint = false;

            if (!inSequence)
                _look.Tick(intent.LookDegrees);
            _motor.Tick(intent, deltaTime);
            _cameraRig.SetCrouchDrop(_motor.CrouchEyeDrop);
            if (!IsDead)
                _magnet?.Tick(deltaTime, _cameraRoot.position + Vector3.down * MagnetPullDrop);
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
            // Repair work first, on last frame's focus: an interact press that stops it mustn't also use the focus.
            bool wasRepairing = _repair.IsBusy;
            if (canAct)
                _repair.Tick(intent, deltaTime);
            else
                _repair.Cancel();
            if (_repair.IsBusy || wasRepairing)
                intent = WithoutItemUse(intent);
            if (canAct && !_repair.IsBusy && !wasRepairing)
                _interactor.Tick(intent.Interact, intent.InteractHold, intent.InteractReleased);
            else
                _interactor.Clear();
            DrawGhost();
            _held?.Tick(intent, ownsZoom: !inSequence, deltaTime);
            // After the held item, so the sequence's camera and field of view win over its kick and zoom.
            if (inSequence)
            {
                _sequence.Tick(deltaTime);
                _zoom.Tick(_sequence.FovScale);
            }
            TickVision(deltaTime);
        }

        // What's being rebuilt, else what the player looks at, as it will be (a broken fence standing).
        void DrawGhost()
        {
            IGhostSource source = _repair.Target as IGhostSource ?? _interactor.Focus as IGhostSource;
            if (source != null && source.TryGetGhost(gameObject, out GhostView ghost))
                GhostDrawer.Draw(ghost, _tuning.Interaction.Ghost);
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
            _motor.TryStand();
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

        // Look follows the zoom, so the crosshair sweeps the same share of the screen per mouse move when aiming. An intent
        // source (dev autopilot) replaces the devices.
        PlayerIntent ReadIntent(float deltaTime) => IntentSource?.Read(deltaTime) ?? new PlayerIntent
        {
            Move = _input.Move,
            LookDegrees = _input.ReadLookDegrees(Time.unscaledDeltaTime) * _zoom.LookScale,
            Sprint = _input.SprintHeld,
            Jump = _input.JumpPressed,
            Crouch = _input.CrouchPressed,
            SelectSlot = SlotFor(_input.HotbarKeyPressed),
            CycleSlot = _input.CycleDirection,
            Interact = _input.InteractPressed,
            InteractHold = _input.InteractHoldPerformed,
            InteractReleased = _input.InteractReleased,
            Drop = _input.DropPressed,
            Fire = _input.AttackHeld,
            FirePressed = _input.AttackPressed,
            Aim = _input.AimHeld,
            Reload = _input.ReloadPressed,
        };

        // The hotbar slot a pressed key selects (EquipmentConfig), or null.
        int? SlotFor(HotbarKey? key)
        {
            if (key == null || Equipment == null)
                return null;
            int slot = Equipment.SlotOf(key.Value);
            return slot >= 0 ? slot : null;
        }

        // While the hammer knocks (and on the frame a key stopped it) its keys do nothing else. Switching and dropping still
        // work: they stop the repair.
        static PlayerIntent WithoutItemUse(PlayerIntent intent)
        {
            intent.Fire = false;
            intent.FirePressed = false;
            intent.Aim = false;
            intent.Reload = false;
            intent.Interact = false;
            intent.InteractHold = false;
            intent.InteractReleased = false;
            return intent;
        }

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
