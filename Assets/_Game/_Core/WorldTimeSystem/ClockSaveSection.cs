using System;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.WorldTimeSystem
{
    // Saves the world clock's time (one number) and puts the day and hour in the slot list's summary.
    public sealed class ClockSaveSection : SaveSection
    {
        const string Key = "clock";

        readonly WorldClock _clock;

        public ClockSaveSection(SaveRegistry registry, WorldClock clock) : base(registry) => _clock = clock;

        public override int Order => SaveOrder.Clock;

        public override void Capture(SaveData save)
        {
            WorldTime now = _clock.Now;
            save.Set(Key, new ClockSave { TotalHours = now.TotalHours });
            save.Header.Day = now.Day;
            save.Header.Hour = now.Hour;
        }

        public override void Restore(SaveData save)
        {
            if (save.TryGet(Key, out ClockSave saved) && saved != null)
                _clock.SetTime(new WorldTime(saved.TotalHours));
        }

        [Serializable]
        public sealed class ClockSave
        {
            public double TotalHours;
        }
    }
}
