using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Aipocalypse.Input
{
    // Single owner of gameplay input: action maps, map switching and cursor lock. Consumers read it, never enable actions.
    public sealed class InputService : IInitializable, IDisposable
    {
        const string KeyboardMouseGroup = "Keyboard&Mouse";

        readonly GameInputActions _actions = new();
        readonly ControlSettings _controls;
        readonly InputAction[] _slots;
        int _reloadConsumedFrame = -1;

        public InputService(ControlSettings controls)
        {
            _controls = controls;
            GameInputActions.PlayerActions player = _actions.Player;
            _slots = new[] { player.Slot1, player.Slot2, player.Slot3, player.Slot4, player.Slot5, player.Slot6, player.Slot7, player.Slot8, player.Slot9 };
        }

        public bool GameplayEnabled => _actions.Player.enabled;

        public Vector2 Move => _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintHeld => _actions.Player.Sprint.IsPressed();
        public bool JumpPressed => _actions.Player.Jump.WasPressedThisFrame();
        // On press, not when the action's Hold interaction completes.
        public bool InteractPressed => _actions.Player.Interact.WasPressedThisFrame();
        public bool DropPressed => _actions.Player.Drop.WasPressedThisFrame();
        public bool AttackHeld => _actions.Player.Attack.IsPressed();
        public bool AttackPressed => _actions.Player.Attack.WasPressedThisFrame();
        public bool AimHeld => _actions.Player.Aim.IsPressed();
        public bool ReloadPressed => _reloadConsumedFrame != Time.frameCount && _actions.Player.Reload.WasPressedThisFrame();
        public void ConsumeReloadPress() => _reloadConsumedFrame = Time.frameCount;

        // Debug map: enabled in the editor and development builds only, independent of the gameplay / UI switch.
        public bool DebugNextHourPressed => _actions.Debug.NextHour.WasPressedThisFrame();
        public bool DebugNextPhasePressed => _actions.Debug.NextPhase.WasPressedThisFrame();

        // Hotbar key pressed this frame (0 = Slot1), or -1.
        public int SlotPressed
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].WasPressedThisFrame())
                        return i;
                return -1;
            }
        }

        // +1 next slot, -1 previous slot (mouse wheel, d-pad), 0 none.
        public int CycleDirection =>
            _actions.Player.Next.WasPressedThisFrame() ? 1 :
            _actions.Player.Previous.WasPressedThisFrame() ? -1 : 0;

        // Key name for a hotbar slot ("1", "2"…), follows rebinding. Allocates: call at setup, not per frame.
        public string GetSlotKeyLabel(int index) =>
            index >= 0 && index < _slots.Length
                ? _slots[index].GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup))
                : string.Empty;

        // Key name for Interact ("E"), follows rebinding. Allocates: call at setup, not per frame.
        public string GetInteractKeyLabel() =>
            _actions.Player.Interact.GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup));

        public string GetReloadKeyLabel() =>
            _actions.Player.Reload.GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup));

        public void Initialize()
        {
            EnableGameplay();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _actions.Debug.Enable();
#endif
        }

        public void Dispose()
        {
            _actions.Disable();
            _actions.Dispose();
        }

        public void EnableGameplay()
        {
            _actions.UI.Disable();
            _actions.Player.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void EnableUI()
        {
            _actions.Player.Disable();
            _actions.UI.Enable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Degrees to turn this frame. Mouse delta is already per frame; a stick is a rate and needs deltaTime.
        public Vector2 ReadLookDegrees(float deltaTime)
        {
            InputAction look = _actions.Player.Look;
            Vector2 value = look.ReadValue<Vector2>();
            bool isStick = look.activeControl?.device is Gamepad or Joystick;

            Vector2 degrees = isStick
                ? value * (_controls.GamepadLookSpeed * deltaTime)
                : value * _controls.MouseSensitivity;

            if (_controls.InvertY)
                degrees.y = -degrees.y;

            return degrees;
        }
    }
}
