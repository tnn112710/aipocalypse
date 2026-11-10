using System;
using System.Collections.Generic;
using System.Text;
using MB13.Aipocalypse.Core.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using VContainer.Unity;

namespace MB13.Aipocalypse.Input
{
    // Gameplay buttons something else may claim for a frame (a QTE's confirm press).
    public enum GameButton
    {
        Reload,
        Interact,
        Attack,
        Jump,
    }

    // Editing keys of the dev console's line (Console map).
    public enum ConsoleButton
    {
        Submit,
        Close,
        Complete,
        HistoryPrevious,
        HistoryNext,
        CaretLeft,
        CaretRight,
        Home,
        End,
        Backspace,
        Delete,
        PageUp,
        PageDown,
        Paste,
    }

    // Single owner of gameplay input: action maps, map switching and cursor lock. Consumers read it, never enable actions.
    // Full-screen menus take the UI map with an owner-keyed request; gameplay comes back only when every owner has released it.
    // Text input (the dev console) beats both: while it's held only the Console map is on.
    public sealed class InputService : IInitializable, IDisposable
    {
        const string KeyboardMouseGroup = "Keyboard&Mouse";

        readonly GameInputActions _actions = new();
        readonly ControlSettings _controls;
        // Indexed by HotbarKey.
        readonly InputAction[] _hotbarKeys;
        readonly HashSet<object> _uiOwners = new();
        readonly HashSet<object> _textOwners = new();
        readonly StringBuilder _typed = new();
        readonly int[] _consumedFrame = new int[Enum.GetValues(typeof(GameButton)).Length];
        Keyboard _textKeyboard;
        bool _disposed;

        public InputService(ControlSettings controls)
        {
            _controls = controls;
            GameInputActions.PlayerActions player = _actions.Player;
            _hotbarKeys = new[] { player.Slot1, player.Slot2, player.Slot3, player.Slot4, player.Slot5, player.Slot6, player.Slot7, player.Slot8, player.Slot9, player.Melee };
            Array.Fill(_consumedFrame, -1);
        }

        public bool GameplayEnabled => _actions.Player.enabled;

        public Vector2 Move => _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintHeld => _actions.Player.Sprint.IsPressed();
        public bool JumpPressed => WasPressed(GameButton.Jump);
        public bool CrouchPressed => _actions.Player.Crouch.WasPressedThisFrame();
        // On press, not when the action's Hold interaction completes.
        public bool InteractPressed => WasPressed(GameButton.Interact);
        // The action's Hold interaction completed this frame (held past InputSettings' hold time).
        public bool InteractHoldPerformed => _consumedFrame[(int)GameButton.Interact] != Time.frameCount && _actions.Player.Interact.WasPerformedThisFrame();
        public bool InteractReleased => _actions.Player.Interact.WasReleasedThisFrame();
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
        // UI map: Cancel (Esc, gamepad east) closes a menu.
        public bool UiCancelPressed => _actions.UI.Cancel.WasPressedThisFrame();
        // The inventory key (Tab), in gameplay to open the inventory and in the UI to close it.
        // Gameplay map only: in a menu the UI map's Cancel (the same Esc) closes it instead.
        public bool PausePressed => _actions.Player.Pause.WasPressedThisFrame();
        public bool InventoryPressed =>_actions.Player.Inventory.WasPressedThisFrame() || _actions.UI.Inventory.WasPressedThisFrame();
        // Held modifiers while a menu is open: Shift moves half a stack.
        public bool UiShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        // Debug map: enabled in the editor and development builds only, independent of the gameplay / UI switch, but off
        // while text input is held (typing "k" must not kill the player).
        public bool DebugNextHourPressed => _actions.Debug.NextHour.WasPressedThisFrame();
        public bool DebugNextPhasePressed => _actions.Debug.NextPhase.WasPressedThisFrame();
        public bool DebugKillPlayerPressed => _actions.Debug.KillPlayer.WasPressedThisFrame();
        public bool DebugRevivePlayerPressed => _actions.Debug.RevivePlayer.WasPressedThisFrame();

        // Console map (editor and development builds): the toggle key (`, left of 1 on any layout) is always on; the editing
        // keys only while text input is held.
        public bool ConsoleTogglePressed => _actions.Console.Toggle.WasPressedThisFrame();
        public bool ConsoleToggleHeld => _actions.Console.Toggle.IsPressed();
        public bool ConsoleWordModifierHeld => _actions.Console.WordModifier.IsPressed();
        // Mouse wheel: > 0 up, < 0 down.
        public float ConsoleScroll => _actions.Console.Scroll.ReadValue<float>();
        public bool WasPressed(ConsoleButton button) => Action(button).WasPressedThisFrame();
        public bool IsHeld(ConsoleButton button) => Action(button).IsPressed();

        // Hotbar key pressed this frame (1-9, melee), or null.
        public HotbarKey? HotbarKeyPressed
        {
            get
            {
                for (int i = 0; i < _hotbarKeys.Length; i++)
                    if (_hotbarKeys[i].WasPressedThisFrame())
                        return (HotbarKey)i;
                return null;
            }
        }

        // +1 next slot, -1 previous slot (mouse wheel, d-pad), 0 none.
        public int CycleDirection =>
            _actions.Player.Next.WasPressedThisFrame() ? 1 :
            _actions.Player.Previous.WasPressedThisFrame() ? -1 : 0;

        // Key name for a hotbar key ("1", "V"…), follows rebinding. Allocates: call at setup, not per frame.
        public string GetKeyLabel(HotbarKey key) =>
            (int)key >= 0 && (int)key < _hotbarKeys.Length
                ? _hotbarKeys[(int)key].GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup))
                : string.Empty;

        // Points a uGUI input module at this service's UI map, so menus click and navigate only while a UI request holds
        // the map on. Call it while the module's GameObject is inactive (the module enables its actions when it wakes).
        public void ConfigureUiModule(InputSystemUIInputModule module)
        {
            GameInputActions.UIActions ui = _actions.UI;
            module.actionsAsset = _actions.asset;
            module.point = InputActionReference.Create(ui.Point);
            module.leftClick = InputActionReference.Create(ui.Click);
            module.rightClick = InputActionReference.Create(ui.RightClick);
            module.middleClick = InputActionReference.Create(ui.MiddleClick);
            module.scrollWheel = InputActionReference.Create(ui.ScrollWheel);
            module.move = InputActionReference.Create(ui.Navigate);
            module.submit = InputActionReference.Create(ui.Submit);
            module.cancel = InputActionReference.Create(ui.Cancel);
            ApplyMode();
        }

        // Key name for Interact ("E"), follows rebinding. Allocates: call at setup, not per frame.
        public string GetInteractKeyLabel() => GetKeyLabel(GameButton.Interact);

        // Key name for a button ("R"), follows rebinding. The key only: Interact's Hold interaction would read "Hold E".
        // Allocates: call when it's shown, not per frame.
        public string GetKeyLabel(GameButton button) =>
            Action(button).GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup), InputBinding.DisplayStringOptions.DontIncludeInteractions);

        InputAction Action(GameButton button) => button switch
        {
            GameButton.Interact => _actions.Player.Interact,
            GameButton.Attack => _actions.Player.Attack,
            GameButton.Jump => _actions.Player.Jump,
            GameButton.Reload => _actions.Player.Reload,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "no input action mapped"),
        };

        InputAction Action(ConsoleButton button) => button switch
        {
            ConsoleButton.Submit => _actions.Console.Submit,
            ConsoleButton.Close => _actions.Console.Close,
            ConsoleButton.Complete => _actions.Console.Complete,
            ConsoleButton.HistoryPrevious => _actions.Console.HistoryPrevious,
            ConsoleButton.HistoryNext => _actions.Console.HistoryNext,
            ConsoleButton.CaretLeft => _actions.Console.CaretLeft,
            ConsoleButton.CaretRight => _actions.Console.CaretRight,
            ConsoleButton.Home => _actions.Console.Home,
            ConsoleButton.End => _actions.Console.End,
            ConsoleButton.Backspace => _actions.Console.Backspace,
            ConsoleButton.Delete => _actions.Console.Delete,
            ConsoleButton.PageUp => _actions.Console.PageUp,
            ConsoleButton.PageDown => _actions.Console.PageDown,
            ConsoleButton.Paste => _actions.Console.Paste,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "no input action mapped"),
        };

        public void Initialize() => ApplyMode();

        public void Dispose()
        {
            _disposed = true;
            _uiOwners.Clear();
            _textOwners.Clear();
            ListenToText(false);
            _actions.Disable();
            // The generated Dispose calls Destroy, which edit mode (tests, editor tools) refuses.
            if (Application.isPlaying)
                _actions.Dispose();
            else
                UnityEngine.Object.DestroyImmediate(_actions.asset);
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

        public bool IsTextInputRequested => _textOwners.Count > 0;

        // Typing into a text line: turns the Player, UI and Debug maps off and the Console editing keys on until the same
        // owner releases it. The cursor keeps its state. Requests stack across owners.
        public void RequestTextInput(object owner)
        {
            if (owner == null || !_textOwners.Add(owner))
                return;
            if (_textOwners.Count == 1)
                ListenToText(true);
            ApplyMode();
        }

        public void ReleaseTextInput(object owner)
        {
            if (owner == null || !_textOwners.Remove(owner))
                return;
            if (_textOwners.Count == 0)
                ListenToText(false);
            ApplyMode();
        }

        // Moves the characters typed since the last call into text (only while text input is held; control characters dropped).
        public void TakeTypedText(StringBuilder text)
        {
            text.Append(_typed);
            _typed.Clear();
        }

        void ListenToText(bool listen)
        {
            _typed.Clear();
            if (_textKeyboard != null)
                _textKeyboard.onTextInput -= OnTextInput;
            _textKeyboard = listen ? Keyboard.current : null;
            if (_textKeyboard != null)
                _textKeyboard.onTextInput += OnTextInput;
        }

        void OnTextInput(char c)
        {
            if (!char.IsControl(c))
                _typed.Append(c);
        }

        void ApplyMode()
        {
            if (_disposed)
                return;
            bool text = _textOwners.Count > 0;
            bool ui = _uiOwners.Count > 0;
            // Disable before enable, so a key is never live in two maps at once.
            if (text || ui)
                _actions.Player.Disable();
            if (text || !ui)
                _actions.UI.Disable();
            if (!text && ui)
                _actions.UI.Enable();
            if (!text && !ui)
                _actions.Player.Enable();

            // Editor and cheat builds only.
            if (Cheats.Enabled)
            {
                if (text)
                {
                    _actions.Debug.Disable();
                    _actions.Console.Enable();
                }
                else
                {
                    _actions.Console.Disable();
                    _actions.Console.Toggle.Enable();
                    _actions.Debug.Enable();
                }
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
