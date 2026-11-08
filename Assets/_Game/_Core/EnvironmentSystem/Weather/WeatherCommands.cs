using System;
using Aipocalypse.Core.Debugging;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Changes the weather by hand (editor and cheat builds): the only trigger until weather rules exist.
    [CommandGroup("weather")]
    public sealed class WeatherCommands : ConsoleCommands
    {
        readonly WeatherService _weather;

        public WeatherCommands(CommandRegistry registry, WeatherService weather) : base(registry) => _weather = weather;

        [Command("The sky weather, its wind and how far the change has blended")]
        string Show() =>
            $"{_weather.Sky} + {_weather.Wind} wind ({(_weather.WindOverride.HasValue ? "set" : "the weather's default")}), blend {_weather.Progress:P0}";

        [Command("Change the sky weather, with its default wind")]
        string Set(SkyWeather sky)
        {
            _weather.Set(sky);
            return Show();
        }

        [Command("Change the sky weather and the wind")]
        string Set(SkyWeather sky, WindLevel wind)
        {
            _weather.Set(sky, wind);
            return Show();
        }

        [Command("Change the sky weather and the wind over this many seconds (0 = cut)")]
        string Set(SkyWeather sky, WindLevel wind, float seconds)
        {
            _weather.Set(sky, wind, seconds);
            return Show();
        }

        [Command("Set the wind level (Calm, Breezy, Windy, Gale), or auto for the weather's default")]
        CommandResult Wind(string level)
        {
            if (string.Equals(level, "auto", StringComparison.OrdinalIgnoreCase))
                _weather.SetWind(null);
            else if (Enum.TryParse(level, true, out WindLevel wind) && Enum.IsDefined(typeof(WindLevel), wind))
                _weather.SetWind(wind);
            else
                return CommandResult.Fail($"unknown wind '{level}': Calm, Breezy, Windy, Gale or auto");
            return CommandResult.Ok(Show());
        }

        [Command("A lightning flash and its thunder now")]
        string Strike()
        {
            _weather.Strike();
            return "strike";
        }
    }
}
