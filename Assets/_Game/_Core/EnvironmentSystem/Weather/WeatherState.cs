namespace Aipocalypse.Core.EnvironmentSystem
{
    // What a save keeps of the weather. WindOverride null = the sky weather's default wind.
    public readonly struct WeatherState
    {
        public readonly SkyWeather Sky;
        public readonly WindLevel? WindOverride;

        public WeatherState(SkyWeather sky, WindLevel? windOverride = null)
        {
            Sky = sky;
            WindOverride = windOverride;
        }
    }
}
