using UnityEngine;
using UnityEngine.InputSystem;

namespace TextStudio.Examples.Whisperwood.InputSystem
{
    internal static class TextStudioWhisperwoodInputSystem
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => TextStudioWhisperwoodInput.Register(ReadKeyboard);

        static TextStudioWhisperwoodKeyboardState ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.added || !keyboard.enabled)
                return default;

            Vector2 movement = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) movement.x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) movement.x += 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) movement.y += 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) movement.y -= 1f;

            return new TextStudioWhisperwoodKeyboardState(keyboard.escapeKey.wasPressedThisFrame,
                keyboard.enterKey.wasPressedThisFrame, keyboard.spaceKey.wasPressedThisFrame,
                keyboard.jKey.wasPressedThisFrame, keyboard.eKey.wasPressedThisFrame, movement);
        }
    }
}
