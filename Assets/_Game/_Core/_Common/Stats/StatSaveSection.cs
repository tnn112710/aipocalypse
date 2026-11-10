using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.Stats
{
    // Saves every stat modifier with its source id, so upgrades and skills bought in a game are still applied after a load.
    public sealed class StatSaveSection : SaveSection
    {
        const string Key = "stats";

        readonly StatModifiers _stats;

        public StatSaveSection(SaveRegistry registry, StatModifiers stats) : base(registry) => _stats = stats;

        public override int Order => SaveOrder.Stats;

        public override void Capture(SaveData save) =>
            save.Set(Key, new StatSave { Modifiers = new List<StatModifier>(_stats.All) });

        // A save from before this section holds no modifiers: a load still drops the ones of the game being left.
        public override void Restore(SaveData save)
        {
            save.TryGet(Key, out StatSave saved);
            _stats.Restore(saved?.Modifiers);
        }

        [Serializable]
        public sealed class StatSave
        {
            public List<StatModifier> Modifiers = new();
        }
    }
}
