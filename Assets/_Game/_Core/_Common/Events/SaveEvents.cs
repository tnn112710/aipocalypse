namespace MB13.Aipocalypse.Core.Events
{
    // A save to `Slot` finished (by hand from the pause menu or the console, or the morning autosave): written (`Ok`), or
    // not, with what to tell the player in `Message`.
    public readonly struct GameSaved
    {
        public readonly int Slot;
        public readonly bool Autosave;
        public readonly bool Ok;
        public readonly string Message;

        public GameSaved(int slot, bool autosave, bool ok, string message)
        {
            Slot = slot;
            Autosave = autosave;
            Ok = ok;
            Message = message;
        }
    }
}
