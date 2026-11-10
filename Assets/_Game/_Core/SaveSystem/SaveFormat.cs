using System;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // How saves read in the menus and the console.
    public static class SaveFormat
    {
        // "Save 2", or "Editor Save" for the editor's slot.
        public static string SlotName(int slot) => slot == SaveService.EditorSlot ? "Editor Save" : $"Save {slot}";

        // "2h 05m", or "12m" under an hour.
        public static string PlayTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return time.TotalHours >= 1d ? $"{(int)time.TotalHours}h {time.Minutes:00}m" : $"{time.Minutes}m";
        }

        // "Day 3  07:30  ·  2h 05m"
        public static string Progress(SaveHeader header)
        {
            int minutes = (int)(header.Hour * 60f);
            return $"Day {header.Day}  {minutes / 60:00}:{minutes % 60:00}  ·  {PlayTime(header.PlayTimeSeconds)}";
        }

        // "2026/11/10 22:40"
        public static string SavedAt(SaveHeader header) => header.SavedAtLocal.ToString("yyyy/MM/dd HH:mm");

        public static string Summary(SaveSlotInfo info) =>
            info.Header != null ? $"{Progress(info.Header)}  ({SavedAt(info.Header)}){(info.FromBackup ? " [backup]" : "")}"
            : info.Problem ?? "Empty";
    }
}
