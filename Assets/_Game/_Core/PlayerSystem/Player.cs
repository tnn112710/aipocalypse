using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.Stats;
using Aipocalypse.Input;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.PlayerSystem
{
    // The player's view: turns input into an intent and drives look → movement → stamina → head motion → equipment in that order.
    [RequireComponent(typeof(CharacterController))]
    public sealed class Player : MonoBehaviour, IDamageable
    {
        [SerializeField] PlayerTuning _tuning;
        [SerializeField] CharacterController _controller;
        [SerializeField, Tooltip("At eye height; head bob and landing dip move it")] Transform _cameraRoot;
        [SerializeField, Tooltip("Child of the camera root; pitch rotates it and the Cinemachine camera tracks it")] Transform _cameraPitch;
        [SerializeField] EquipmentConfig _equipmentConfig;
        [SerializeField] EquipmentRig _equipmentRig;

        InputService _input;
        IStatProvider _stats;
        IPublisher<DamageDealt> _damageDealt;
        IPublisher<PlayerDied> _died;

        PlayerMotor _motor;
        PlayerLook _look;
        PlayerHeadBob _headBob;

        [ShowInInspector, ReadOnly, HideInEditorMode] public Health Health { get; private set; }
        [ShowInInspector, ReadOnly, HideInEditorMode] public Stamina Stamina { get; private set; }
        public Equipment Equipment { get; private set; }
        public PlayerMotor Motor => _motor;
        public bool IsDead => Health != null && Health.IsDead;

        [Inject]
        void Construct(InputService input, IStatProvider stats, IPublisher<DamageDealt> damageDealt, IPublisher<PlayerDied> died)
        {
            _input = input;
            _stats = stats;
            _damageDealt = damageDealt;
            _died = died;

            Health = new Health(stats.Get(StatId.MaxHealth, _tuning.MaxHealth));
            Stamina = new Stamina(stats.Get(StatId.MaxStamina, _tuning.MaxStamina), _tuning.StaminaRegenDelay, _tuning.ExhaustionRecoverFraction);
            _motor = new PlayerMotor(_controller, _tuning, stats, Stamina);
            _look = new PlayerLook(transform, _cameraPitch, _tuning);
            _headBob = new PlayerHeadBob(_cameraRoot, _tuning);

            _motor.Landed += OnLanded;

            if (_equipmentConfig == null || _equipmentRig == null)
            {
                Debug.LogError($"{name}: Equipment Config or Equipment Rig not assigned; equipment is off.", this);
                return;
            }
            Equipment = new Equipment(_equipmentConfig.CreateHotbar());
            Equipment.EquipFirstImmediately();
            _equipmentRig.Bind(Equipment);
        }

        void Start()
        {
            if (_motor == null)
                Debug.LogError($"{name}: not injected. The Player prefab needs PlayerScope on its root.", this);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (_motor == null || deltaTime <= 0f)
                return;

            PlayerIntent intent = IsDead || !_input.GameplayEnabled ? default : ReadIntent(deltaTime);

            _look.Tick(intent.LookDegrees);
            _motor.Tick(intent, deltaTime);
            Stamina.Tick(deltaTime, _stats.Get(StatId.StaminaRegen, _tuning.StaminaRegenPerSecond));
            _headBob.Tick(_motor, deltaTime);
            TickEquipment(intent, deltaTime);
        }

        void OnControllerColliderHit(ControllerColliderHit hit) => _motor?.OnControllerHit(hit);

        void OnDestroy()
        {
            if (_motor != null)
                _motor.Landed -= OnLanded;
            Equipment?.Dispose();
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (Health == null || Health.Damage(info.Amount) <= 0f)
                return;

            _damageDealt.Publish(new DamageDealt(this, info));
            if (Health.IsDead)
                _died.Publish(new PlayerDied(transform.position, info.Type));
        }

        [Button, HideInEditorMode]
        public void Revive()
        {
            Health?.Restore();
            Stamina?.Restore();
        }

        PlayerIntent ReadIntent(float deltaTime) => new()
        {
            Move = _input.Move,
            LookDegrees = _input.ReadLookDegrees(deltaTime),
            Sprint = _input.SprintHeld,
            Jump = _input.JumpPressed,
            SelectSlot = _input.SlotPressed is var slot and >= 0 ? slot : null,
            CycleSlot = _input.CycleDirection,
        };

        void TickEquipment(in PlayerIntent intent, float deltaTime)
        {
            if (Equipment == null)
                return;

            if (intent.SelectSlot is int slot)
                Equipment.Select(slot);
            else if (intent.CycleSlot != 0)
                Equipment.Cycle(intent.CycleSlot);

            Equipment.Tick(deltaTime, _equipmentConfig.SwitchSpeed);
            _equipmentRig.Tick();
        }

        void OnLanded(LandingInfo landing)
        {
            _headBob.OnLanded(landing.ImpactSpeed);

            float damage = FallDamage.For(landing.DropHeight, _tuning.SafeFallHeight, _tuning.LethalFallHeight, Health.Max);
            if (damage > 0f)
                TakeDamage(new DamageInfo(damage, DamageType.Fall, landing.Point));
        }

        void Reset() => _controller = GetComponent<CharacterController>();
    }
}
