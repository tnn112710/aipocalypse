using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TextStudio.InputSystem
{
    public sealed class InputSystemTextStudioInputProvider : ITextStudioKeyedInputProvider
    {
        public bool WasAnyAdvancePressed()
        {
            if (!Application.isFocused) return false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            foreach (Gamepad gamepad in Gamepad.all)
                foreach (InputControl control in gamepad.allControls)
                    if (control is ButtonControl button && !(button.parent is StickControl) && button.wasPressedThisFrame) return true;
            Touchscreen touch = Touchscreen.current;
            if (touch != null)
                foreach (TouchControl contact in touch.touches)
                    if (contact.press.wasPressedThisFrame) return true;
            return false;
        }

        public bool WasAdvancePressed(string inputKey)
        {
            if (!Application.isFocused) return false;
            if (string.IsNullOrWhiteSpace(inputKey) ||
                string.Equals(inputKey, "any", StringComparison.OrdinalIgnoreCase))
            {
                return WasAnyAdvancePressed();
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (MatchesAny(inputKey, "mouse0", "leftMouse", "leftButton") && mouse.leftButton.wasPressedThisFrame)
                    return true;
                if (MatchesAny(inputKey, "mouse1", "rightMouse", "rightButton") && mouse.rightButton.wasPressedThisFrame)
                    return true;
                if (MatchesAny(inputKey, "mouse2", "middleMouse", "middleButton") && mouse.middleButton.wasPressedThisFrame)
                    return true;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !Enum.TryParse(inputKey, true, out Key key) || key == Key.None || !Enum.IsDefined(typeof(Key), key))
                return false;

            return keyboard[key].wasPressedThisFrame;
        }

        internal static bool RecognizesKey(string inputKey)
        {
            return string.IsNullOrWhiteSpace(inputKey) || string.Equals(inputKey, "any", StringComparison.OrdinalIgnoreCase) ||
                MatchesAny(inputKey, "mouse0", "leftMouse", "leftButton") || MatchesAny(inputKey, "mouse1", "rightMouse", "rightButton") ||
                MatchesAny(inputKey, "mouse2", "middleMouse", "middleButton") ||
                (Enum.TryParse(inputKey, true, out Key key) && key != Key.None && Enum.IsDefined(typeof(Key), key));
        }

        static bool MatchesAny(string value, string first, string second, string third)
        {
            return string.Equals(value, first, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, second, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, third, StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum InputSystemWaitInputMode
    {
        TextStudioInputProvider,
        DefaultInputAction,
        KeyedInputActions
    }

    [Serializable]
    public sealed class InputSystemWaitInputMapping
    {
        [SerializeField] string _inputKey;
        [SerializeField] InputActionReference _action;

        public string InputKey => _inputKey ?? string.Empty;
        public InputActionReference Action => _action;
    }

}
