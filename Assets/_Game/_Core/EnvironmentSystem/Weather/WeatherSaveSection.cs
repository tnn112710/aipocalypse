using System;
using MB13.Aipocalypse.Core.SaveSystem;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // Saves the weather showing (sky and wind override); a loaded game starts under it, cut, and the schedule plans on from it.
    public sealed class WeatherSaveSection : SaveSection
    {
        const string Key = "weather";

        readonly WeatherService _weather;

        public WeatherSaveSection(SaveRegistry registry, WeatherService weather) : base(registry) => _weather = weather;

        public override int Order => SaveOrder.Weather;

        public override void Capture(SaveData save) => save.Set(Key, new WeatherSave
        {
            Sky = _weather.Sky,
            HasWind = _weather.WindOverride.HasValue,
            Wind = _weather.WindOverride ?? default,
        });

        public override void Restore(SaveData save)
        {
            if (save.TryGet(Key, out WeatherSave saved) && saved != null)
                _weather.Restore(new WeatherState(saved.Sky, saved.HasWind ? saved.Wind : null));
        }

        [Serializable]
        public sealed class WeatherSave
        {
            public SkyWeather Sky;
            public bool HasWind;
            public WindLevel Wind;
        }
    }
}
