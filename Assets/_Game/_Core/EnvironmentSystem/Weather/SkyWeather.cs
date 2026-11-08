namespace Aipocalypse.Core.EnvironmentSystem
{
    // What the sky does: steps of cloud cover up to rain, plus fog. Exactly one at a time; wind is its own layer (WindLevel).
    // Append new values at the end: settings and saves key on these.
    public enum SkyWeather
    {
        Clear,
        PartlyCloudy,
        Cloudy,
        Overcast,
        LightRain,
        MediumRain,
        HeavyRain,
        Fog,
    }
}
