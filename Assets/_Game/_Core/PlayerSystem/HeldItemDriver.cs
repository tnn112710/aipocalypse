using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.HandSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Runs the item in hand each frame in dependency order: input → switching/rig pose → item acts → recoil/view kick → zoom →
    // sway and aim → move speed → obstacle pose → LateTick (shots leave the drawn muzzle) → hands on grips.
    public sealed class HeldItemDriver
    {
        readonly Transform _body;
        readonly Equipment _equipment;
        readonly EquipmentRig _rig;
        readonly EquipmentConfig _config;
        readonly PlayerTuning _tuning;
        readonly PlayerMotor _motor;
        readonly PlayerLook _look;
        readonly PlayerHeadBob _headBob;
        readonly PlayerZoom _zoom;
        readonly HeldItemSway _sway;
        readonly PlayerAim _aim;
        readonly HeldItemClearance _clearance;
        readonly PlayerInteractor _interactor;
        readonly Stamina _stamina;
        readonly Fatigue _fatigue;
        readonly PlayerBreathing _breathing;
        readonly FirstPersonHands _hands;

        public HeldItemDriver(Transform body, Equipment equipment, EquipmentRig rig, EquipmentConfig config, PlayerTuning tuning,
            PlayerMotor motor, PlayerLook look, PlayerHeadBob headBob, PlayerZoom zoom, HeldItemSway sway, PlayerAim aim,
            HeldItemClearance clearance, PlayerInteractor interactor, Stamina stamina, Fatigue fatigue, PlayerBreathing breathing,
            FirstPersonHands hands)
        {
            _body = body;
            _equipment = equipment;
            _rig = rig;
            _config = config;
            _tuning = tuning;
            _motor = motor;
            _look = look;
            _headBob = headBob;
            _zoom = zoom;
            _sway = sway;
            _aim = aim;
            _clearance = clearance;
            _interactor = interactor;
            _stamina = stamina;
            _fatigue = fatigue;
            _breathing = breathing;
            _hands = hands;
        }

        // `ownsZoom` false while a pose sequence sets the field of view.
        public void Tick(in PlayerIntent intent, bool ownsZoom, float deltaTime)
        {
            if (intent.Drop)
                DropHeld();
            else if (intent.SelectSlot is int slot)
                _equipment.Select(slot);
            else if (intent.CycleSlot != 0)
                _equipment.Cycle(intent.CycleSlot);

            PlayerHeldItemTuning tuning = _tuning.HeldItem;
            _equipment.Tick(deltaTime, _config.SwitchSpeed);
            _rig.Tick();
            _rig.TickHeld(new HeldItemInput
            {
                UseHeld = intent.Fire,
                UsePressed = intent.FirePressed,
                AimHeld = intent.Aim,
                ReloadPressed = intent.Reload,
                IsSprinting = _motor.IsSprinting,
                IsMoving = _motor.HorizontalVelocity.sqrMagnitude > tuning.MovingSpeed * tuning.MovingSpeed,
                IsAirborne = !_motor.IsGrounded,
            }, deltaTime);

            HeldItem held = _rig.Current;
            HoldFeel feel = held != null ? held.Item?.Equippable?.HoldFeel : null;
            if (held != null)
                _look.Turn(held.LookTurn);
            _look.SetKick(held != null ? held.ViewKick : default);
            if (ownsZoom)
                _zoom.Tick(held != null ? held.Zoom : 1f);
            TickSway(intent, held, feel, deltaTime);
            float aimSpeed = held != null && held.IsAiming ? tuning.AimMoveSpeedMultiplier * (feel?.AimMoveSpeedScale ?? 1f) : 1f;
            _motor.SpeedMultiplier = aimSpeed * (held != null ? held.MoveSpeedScale : 1f) * _fatigue.MoveSpeed;
            _clearance.Tick(held, deltaTime);
            if (held != null)
                held.LateTick();
            if (_hands != null)
                _hands.Tick(held != null ? held.Hands : null);
        }

        void TickSway(in PlayerIntent intent, HeldItem held, HoldFeel feel, float deltaTime)
        {
            Vector3 velocity = _body.InverseTransformDirection(_motor.HorizontalVelocity);
            velocity.y = _motor.IsGrounded ? 0f : _motor.VerticalVelocity;
            _sway.ItemScale = feel?.SwayScale ?? 1f;
            _sway.Tick(new HeldSwayInput
            {
                LookRate = Time.unscaledDeltaTime > 0f ? intent.LookDegrees / Time.unscaledDeltaTime : Vector2.zero,
                LocalVelocity = velocity,
                IsAirborne = !_motor.IsGrounded,
                BobPhase = _headBob.Phase,
                BobIntensity = _headBob.Intensity,
                SprintBlend = _headBob.SprintBlend,
                AimWeight = held != null ? held.AimWeight : 0f,
                IsAiming = held != null && held.IsAiming,
                Tiredness = _breathing.Tiredness,
                IsWinded = _stamina.IsWinded,
            }, deltaTime);
            _rig.ApplyHandMotion(_sway.Position, _sway.Rotation);
            _aim.Tick(_sway.AimRotation, held != null ? held.Spread : 0f);
        }

        // G throws the item in hand (a whole stack) forward.
        void DropHeld()
        {
            ItemInstance item = _equipment.DropHeld();
            if (item != null)
                _interactor.Throw(item, _body.eulerAngles.y + _tuning.Interaction.DropYaw);
        }
    }
}
