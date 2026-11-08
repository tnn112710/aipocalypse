namespace Aipocalypse.Core.EnvironmentSystem
{
    // Which weight table of WeatherRules applies: daytime or night, and the Blood Moon night / eclipse day versions.
    // Append new values at the end: the rules key on these.
    public enum WeatherPeriod
    {
        Day,
        Night,
        BloodMoon,
        Eclipse,
    }
}
