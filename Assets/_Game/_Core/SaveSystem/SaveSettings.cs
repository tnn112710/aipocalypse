using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Save slots, autosave and the scenes the game moves between. One asset: Data/Save/SaveSettings.
    [CreateAssetMenu(menuName = "Aipocalypse/Save/Save Settings", fileName = "SaveSettings")]
    public sealed class SaveSettings : ScriptableObject
    {
        [Min(1), Tooltip("Save slots in the menus")] public int SlotCount = 5;
        [Tooltip("Folder under Application.persistentDataPath")] public string Folder = "Saves";

        [Header("Autosave")]
        [Tooltip("Off: only manual saves (pause menu, console)")] public bool Autosave = true;
        [Tooltip("Saves to the current slot when this phase starts")] public DayPhase AutosavePhase = DayPhase.Morning;
        [Min(1), Tooltip("No autosave before this day (day 1 starts fresh)")] public int FirstAutosaveDay = 2;

        [Header("Scenes")]
        [Tooltip("Build Settings name of the main menu scene")] public string MainMenuScene = "MainMenu";
        [Tooltip("Build Settings name of the world scene a new game (and the main menu's Prototype) starts in")] public string WorldScene = "WorldMap";

        [Header("Loading screen")]
        public TMP_FontAsset LoadingFont;
        [Tooltip("UI table")] public LocalizedString LoadingText = Loc.UI("loading");
    }
}
