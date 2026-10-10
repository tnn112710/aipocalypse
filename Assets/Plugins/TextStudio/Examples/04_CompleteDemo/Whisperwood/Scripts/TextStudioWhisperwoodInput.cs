using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("TextStudio.Examples.Whisperwood.InputSystem")]

namespace TextStudio.Examples.Whisperwood
{
    internal readonly struct TextStudioWhisperwoodKeyboardState
    {
        internal readonly bool CancelPressed;
        internal readonly bool EnterPressed;
        internal readonly bool SpacePressed;
        internal readonly bool JournalPressed;
        internal readonly bool InteractPressed;
        internal readonly Vector2 Movement;

        internal TextStudioWhisperwoodKeyboardState(bool cancelPressed, bool enterPressed, bool spacePressed,
            bool journalPressed, bool interactPressed, Vector2 movement)
        {
            CancelPressed = cancelPressed;
            EnterPressed = enterPressed;
            SpacePressed = spacePressed;
            JournalPressed = journalPressed;
            InteractPressed = interactPressed;
            Movement = movement;
        }
    }

    internal static class TextStudioWhisperwoodInput
    {
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        // A stateless code registration; no device or per-play keyboard state is retained.
        static Func<TextStudioWhisperwoodKeyboardState> s_read;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => s_read = null;

        internal static void Register(Func<TextStudioWhisperwoodKeyboardState> read) => s_read = read;
        internal static TextStudioWhisperwoodKeyboardState ReadKeyboard() => s_read != null ? s_read() : default;
    }
}
