using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    // Saves the master seed and where every named stream is, so a loaded game keeps rolling as the saved one would have.
    public sealed class RandomSaveSection : SaveSection
    {
        const string Key = "random";

        readonly RandomService _random;

        public RandomSaveSection(SaveRegistry registry, RandomService random) : base(registry) => _random = random;

        public override int Order => SaveOrder.Random;

        public override void Capture(SaveData save) =>
            save.Set(Key, new RandomSave { Seed = _random.Seed, Streams = _random.CaptureStreams() });

        public override void Restore(SaveData save)
        {
            if (save.TryGet(Key, out RandomSave saved) && saved != null)
                _random.Restore(saved.Seed, saved.Streams);
        }

        [Serializable]
        public sealed class RandomSave
        {
            public ulong Seed;
            public Dictionary<string, ulong> Streams = new();
        }
    }
}
