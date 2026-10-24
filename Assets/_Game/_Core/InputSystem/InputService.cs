using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Aipocalypse.Input
{
    // Gameplay buttons something else may claim for a frame (a QTE's confirm press).
    public enum GameButton
    {
        Reload,
        Interact,
        Attack,
        Jump,
    }

    // Single owner of gameplay input: action maps, map switching and cursor lock. Consumers read it, never enable actions.
    // Full-screen menus take the UI map with an owner-keyed request; gameplay comes back only when every owner has released it.
    public sealed class InputService : IInitializable, IDisposable
    {
        const string KeyboardMouseGroup = "Keyboard&Mouse";

        readonly GameInputActions _actions = new();
        readonly ControlSettings _controls;
        readonly InputAction[] _slots;
        readonly HashSet<object> _uiOwners = new();
        readonly int[] _consumedFrame = { -1, -1, -1, -1 };
        bool _disposed;

        public InputService(ControlSettings controls)
        {
            _controls = controls;
            GameInputActions.PlayerActions player = _actions.Player;
            _slots = new[] { player.Slot1, player.Slot2, player.Slot3, player.Slot4, player.Slot5, player.Slot6, player.Slot7, player.Slot8, player.Slot9 };
        }

        public bool GameplayEnabled => _actions.Player.enabled;

        public Vector2 Move => _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintHeld => _actions.Player.Sprint.IsPressed();
        public bool JumpPressed => WasPressed(GameButton.Jump);
        // On press, not when the action's Hold interaction completes.
        public bool InteractPressed => WasPressed(GameButton.Interact);
        public bool DropPressed => _actions.Player.Drop.WasPressedThisFrame();
        public bool AttackHeld => _actions.Player.Attack.IsPressed();
        public bool AttackPressed => WasPressed(GameButton.Attack);
        public bool AimHeld => _actions.Player.Aim.IsPressed();
        public bool ReloadPressed => WasPressed(GameButton.Reload);

        // Pressed this frame and not claimed by Consume.
        public bool WasPressed(GameButton button) => _consumedFrame[(int)button] != Time.frameCount && Action(button).WasPressedThisFrame();

        // Claims this frame's press so later readers (the player, the held gun) don't see it. Only works for readers that run
        // after the consumer in the frame: VContainer ticks (QuickTimeService) run before MonoBehaviour Updates (Player).
        public void Consume(GameButton button) => _consumedFrame[(int)button] = Time.frameCount;

        // UI map (while a UI request is held): a click, or Submit (Enter, gamepad south), to confirm a full-screen menu.
        public bool UiConfirmPressed => _actions.UI.Click.WasPressedThisFrame() || _actions.UI.Submit.WasPressedThisFrame();

        // Debug map: enabled in the editor and development builds only, independent of the gameplay / UI switch.
        public bool DebugNextHourPressed => _actions.Debug.NextHour.WasPressedThisFrame();
        public bool DebugNextPhasePressed => _actions.Debug.NextPhase.WasPressedThisFrame();
        public bool DebugKillPlayerPressed => _actions.Debug.KillPlayer.WasPressedThisFrame();
        public bool DebugRevivePlayerPressed => _actions.Debug.RevivePlayer.WasPressedThisFrame();

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

        // Key name for a button ("R"), follows rebinding. Allocates: call when it's shown, not per frame.
        public string GetKeyLabel(GameButton button) => Action(button).GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup));

        InputAction Action(GameButton button) => button switch
        {
            GameButton.Interact => _actions.Player.Interact,
            GameButton.Attack => _actions.Player.Attack,
            GameButton.Jump => _actions.Player.Jump,
            _ => _actions.Player.Reload,
        };

        public void Initialize()
        {
            ApplyMode();
            // Editor and development builds only.
            if (Debug.isDebugBuild)
                _actions.Debug.Enable();
        }

        public void Dispose()
        {
            _disposed = true;
            _uiOwners.Clear();
            _actions.Disable();
            _actions.Dispose();
        }

        public bool IsUIRequested => _uiOwners.Count > 0;

        // Switches to the UI map and frees the cursor until the same owner releases it. Requests stack across owners.
        public void RequestUI(object owner)
        {
            if (owner != null && _uiOwners.Add(owner))
                ApplyMode();
        }

        public void ReleaseUI(object owner)
        {
            if (owner != null && _uiOwners.Remove(owner))
                ApplyMode();
        }

        void ApplyMode()
        {
            if (_disposed)
                return;
            bool ui = _uiOwners.Count > 0;
            if (ui)
            {
                _actions.Player.Disable();
                _actions.UI.Enable();
            }
            else
            {
                _actions.UI.Disable();
                _actions.Player.Enable();
            }
            Cursor.lockState = ui ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ui;
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
