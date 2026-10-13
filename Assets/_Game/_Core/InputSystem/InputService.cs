using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Aipocalypse.Input
{
    // Single owner of gameplay input: action maps, map switching and cursor lock. Consumers read it, never enable actions.
    public sealed class InputService : IInitializable, IDisposable
    {
        readonly GameInputActions _actions = new();
        readonly ControlSettings _controls;

        public InputService(ControlSettings controls) => _controls = controls;

        public bool GameplayEnabled => _actions.Player.enabled;

        public Vector2 Move => _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintHeld => _actions.Player.Sprint.IsPressed();
        public bool JumpPressed => _actions.Player.Jump.WasPressedThisFrame();

        public void Initialize() => EnableGameplay();

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
