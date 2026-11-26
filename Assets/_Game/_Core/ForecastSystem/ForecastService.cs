using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // Tonight's forecast: the fight each period brings, from ForecastSettings, or a console override (not saved).
    // Doesn't drive the raids yet.
    public sealed class ForecastService
    {
        readonly ForecastSettings _settings;
        readonly IPublisher<ForecastChanged> _changed;
        readonly Dictionary<NightPeriod, FightSize> _overrides = new();

        public ForecastService(ForecastSettings settings, IPublisher<ForecastChanged> changed)
        {
            _settings = settings;
            _changed = changed;
        }

        public bool Enabled => _settings.Enabled;

        public FightSize Fight(NightPeriod period)
        {
            if (!_settings.Enabled)
                return FightSize.None;
            if (_overrides.TryGetValue(period, out FightSize fight))
                return fight;
            return _settings.Periods.TryGetValue(period, out ForecastPeriod data) ? data.Fight : FightSize.None;
        }

        public void Override(NightPeriod period, FightSize fight)
        {
            _overrides[period] = fight;
            _changed.Publish(default);
        }

        public void ClearOverrides()
        {
            _overrides.Clear();
            _changed.Publish(default);
        }
    }
}
