namespace Aipocalypse.Core.EnvironmentSystem
{
    // One stretch of the planned weather, in world-clock hours (WorldTime.TotalHours): a sky weather and its wind.
    // Forced = put there by WeatherSchedule.Force / Hold, not rolled.
    public readonly struct WeatherSpell
    {
        public readonly double Start;
        public readonly double End;
        public readonly SkyWeather Sky;
        public readonly WindLevel Wind;
        public readonly bool Forced;

        public WeatherSpell(double start, double end, SkyWeather sky, WindLevel wind, bool forced)
        {
            Start = start;
            End = end;
            Sky = sky;
            Wind = wind;
            Forced = forced;
        }

        public double Hours => End - Start;

        public WeatherSpell WithEnd(double end) => new(Start, end, Sky, Wind, Forced);
    }
}
