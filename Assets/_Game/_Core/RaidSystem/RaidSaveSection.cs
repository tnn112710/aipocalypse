using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.RaidSystem
{
    // Saves which nights' raids are done, so loading a save made after a raid doesn't run it again. A raid itself is never
    // saved: GameSession refuses to save while one runs.
    public sealed class RaidSaveSection : SaveSection
    {
        const string Key = "raids";

        readonly RaidDirector _director;

        public RaidSaveSection(SaveRegistry registry, RaidDirector director) : base(registry) => _director = director;

        public override int Order => SaveOrder.Raids;

        public override void Capture(SaveData save) => save.Set(Key, new RaidSave { HandledNights = new List<int>(_director.HandledNights) });

        public override void Restore(SaveData save)
        {
            if (save.TryGet(Key, out RaidSave saved) && saved?.HandledNights != null)
                _director.RestoreHandledNights(saved.HandledNights);
        }

        [Serializable]
        public sealed class RaidSave
        {
            public List<int> HandledNights = new();
        }
    }
}
