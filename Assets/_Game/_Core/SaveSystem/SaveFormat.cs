using System;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // How saves read in the menus and the console.
    public static class SaveFormat
    {
        public static readonly LocalizedString EditorSlotName = Loc.UI("save.editor_slot");
        static readonly LocalizedString SlotFormat = Loc.UI("save.slot");
        static readonly LocalizedString HoursFormat = Loc.UI("save.playtime_hours");
        static readonly LocalizedString MinutesFormat = Loc.UI("save.playtime_minutes");
        static readonly LocalizedString ProgressFormat = Loc.UI("save.progress");
        static readonly LocalizedString BackupTag = Loc.UI("save.backup_tag");
        static readonly LocalizedString Empty = Loc.UI("slots.empty");

        // "Save 2", or "Editor Save" for the editor's slot.
        public static string SlotName(int slot) => slot == SaveService.EditorSlot ? EditorSlotName.Text() : SlotFormat.Format(slot);

        // "2h 05m", or "12m" under an hour.
        public static string PlayTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return time.TotalHours >= 1d ? HoursFormat.Format((int)time.TotalHours, time.Minutes) : MinutesFormat.Format(time.Minutes);
        }

        // "Day 3  07:30  ·  2h 05m"
        public static string Progress(SaveHeader header)
        {
            int minutes = (int)(header.Hour * 60f);
            return ProgressFormat.Format(header.Day, $"{minutes / 60:00}:{minutes % 60:00}", PlayTime(header.PlayTimeSeconds));
        }

        // "2026/11/10 22:40"
        public static string SavedAt(SaveHeader header) => header.SavedAtLocal.ToString("yyyy/MM/dd HH:mm");

        public static string Summary(SaveSlotInfo info) =>
            info.Header != null ? $"{Progress(info.Header)}  ({SavedAt(info.Header)}){(info.FromBackup ? BackupTag.Text() : "")}"
            : info.Problem ?? Empty.Text();
    }
}
