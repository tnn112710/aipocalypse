using System;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // Saves which day's forecast was revealed, so a load doesn't hide it again.
    public sealed class ForecastSaveSection : SaveSection
    {
        const string Key = "forecast";

        readonly ForecastReveal _reveal;

        public ForecastSaveSection(SaveRegistry registry, ForecastReveal reveal) : base(registry) => _reveal = reveal;

        public override int Order => SaveOrder.World;

        public override void Capture(SaveData save) => save.Set(Key, new ForecastSave { RevealedDay = _reveal.RevealedDay });

        public override void Restore(SaveData save)
        {
            if (save.TryGet(Key, out ForecastSave saved) && saved != null)
                _reveal.Restore(saved.RevealedDay);
        }

        [Serializable]
        public sealed class ForecastSave
        {
            public int RevealedDay = int.MinValue;
        }
    }
}
