namespace MB13.Aipocalypse.Core.Events
{
    // A save was written to `Slot` (by hand from the pause menu or the console, or the morning autosave).
    public readonly struct GameSaved
    {
        public readonly int Slot;
        public readonly bool Autosave;

        public GameSaved(int slot, bool autosave)
        {
            Slot = slot;
            Autosave = autosave;
        }
    }
}
