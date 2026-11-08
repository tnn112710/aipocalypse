using UnityEngine;

namespace MB13.Aipocalypse.Core.Debugging
{
    // How the dev console behaves. Its look is ConsoleStyle, on the overlay prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/Debug/Console Settings", fileName = "ConsoleSettings")]
    public sealed class ConsoleSettings : ScriptableObject
    {
        [Min(16), Tooltip("Lines the scrollback keeps")] public int LogCapacity = 1000;
        [Min(1), Tooltip("Lines kept in the Up / Down history, saved between sessions")] public int HistorySize = 100;
        [Tooltip("Pause the game (TimeService) while the console is open")] public bool PauseWhileOpen;

        [Tooltip("Unity logs shown on screen and in the scrollback at start (log.screen.* change it while playing). Off: the console shows only commands")]
        public ScreenLogLevel ScreenLogLevel = ScreenLogLevel.Off;
        [Min(0f), Tooltip("Seconds a mirrored log stays on screen")] public float ScreenLogSeconds = 5f;

        [Min(0f), Tooltip("Seconds a held editing key (Backspace, arrows…) waits before repeating")] public float RepeatDelay = 0.4f;
        [Min(0.01f), Tooltip("Seconds between repeats of a held editing key")] public float RepeatInterval = 0.035f;
        [Min(1), Tooltip("Lines one mouse wheel step scrolls")] public int WheelLines = 3;
    }
}
