using System;
using System.Collections.Generic;
using System.Text;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.WorldTimeSystem;
using VContainer;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // The weather by hand (editor and cheat builds). With the WeatherSchedule on, set / wind hold the weather until
    // weather.auto; force, bloodmoon, eclipse and forecast need it.
    [CommandGroup("weather")]
    public sealed class WeatherCommands : ConsoleCommands
    {
        const string NoSchedule = "no weather schedule (WeatherRules unset or off, or no world clock)";

        readonly WeatherService _weather;
        readonly WeatherSchedule _schedule;
        readonly WorldClock _clock;

        public WeatherCommands(CommandRegistry registry, WeatherService weather, IObjectResolver resolver) : base(registry)
        {
            _weather = weather;
            resolver.TryResolve(out _schedule);
            resolver.TryResolve(out _clock);
            if (_schedule != null && !_schedule.HasClock)
                _schedule = null;
        }

        [Command("The sky weather, its wind, the blend and who decides it")]
        string Show()
        {
            string wind = _weather.WindOverride.HasValue ? "set" : "the weather's default";
            string who = _schedule == null ? "by hand" : _schedule.IsHeld ? "held (weather.auto to release)" : $"auto, {_schedule.Period} table";
            return $"{_weather.Sky} + {_weather.Wind} wind ({wind}), blend {_weather.Progress:P0}, {who}";
        }

        [Command("Change the sky weather, with its default wind (held until weather.auto)")]
        string Set(SkyWeather sky) => Hold(sky, null, null);

        [Command("Change the sky weather and the wind (held until weather.auto)")]
        string Set(SkyWeather sky, WindLevel wind) => Hold(sky, wind, null);

        [Command("Change the sky weather and the wind over this many seconds, 0 = cut (held until weather.auto)")]
        string Set(SkyWeather sky, WindLevel wind, float seconds) => Hold(sky, wind, seconds);

        string Hold(SkyWeather sky, WindLevel? wind, float? seconds)
        {
            _schedule?.Hold(sky, wind);
            _weather.Set(sky, wind, seconds);
            return Show();
        }

        [Command("Set the wind level (Calm, Breezy, Windy, Gale), or auto for the weather's default (held until weather.auto)")]
        CommandResult Wind(string level)
        {
            WindLevel? wind;
            if (string.Equals(level, "auto", StringComparison.OrdinalIgnoreCase))
                wind = null;
            else if (Enum.TryParse(level, true, out WindLevel parsed) && Enum.IsDefined(typeof(WindLevel), parsed))
                wind = parsed;
            else
                return CommandResult.Fail($"unknown wind '{level}': Calm, Breezy, Windy, Gale or auto");
            _schedule?.Hold(_weather.Sky, wind);
            _weather.SetWind(wind);
            return CommandResult.Ok(Show());
        }

        [Command("Release a held weather: the rules decide again")]
        CommandResult Auto()
        {
            if (_schedule == null)
                return CommandResult.Fail(NoSchedule);
            _schedule.Release();
            return CommandResult.Ok(Show());
        }

        [Command("Force a weather from now for some game hours, its wind rolled")]
        CommandResult Force(SkyWeather sky, float hours) => ForceFrom(sky, null, hours, null);

        [Command("Force a weather and wind from the next time the clock shows this hour (20.5 = 20:30), for some game hours")]
        CommandResult Force(SkyWeather sky, WindLevel wind, float hour, float hours) => ForceFrom(sky, wind, hours, hour);

        CommandResult ForceFrom(SkyWeather sky, WindLevel? wind, float hours, float? hour)
        {
            if (_schedule == null)
                return CommandResult.Fail(NoSchedule);
            if (hours <= 0f || hour is < 0f or >= 24f)
                return CommandResult.Fail("hours above 0, hour from 0 to 24 (not included)");
            _schedule.Force(sky, wind, hours, hour);
            return Forecast();
        }

        [Command("Mark tonight a Blood Moon night (its weather table)")]
        CommandResult BloodMoon() => Mark(true, Tonight, true);

        [Command("Mark the night starting on this day a Blood Moon night, or not (on = false)")]
        CommandResult BloodMoon(int day, bool on) => Mark(true, day, on);

        [Command("Mark today an eclipse day (its weather table for the daytime)")]
        CommandResult Eclipse() => Mark(false, _clock != null ? _clock.Now.Day : 1, true);

        [Command("Mark this day an eclipse day, or not (on = false)")]
        CommandResult Eclipse(int day, bool on) => Mark(false, day, on);

        CommandResult Mark(bool bloodMoon, int day, bool on)
        {
            if (_schedule == null)
                return CommandResult.Fail(NoSchedule);
            if (bloodMoon)
                _schedule.MarkBloodMoon(day, on);
            else
                _schedule.MarkEclipse(day, on);
            return Forecast();
        }

        [Command("The planned weather for the next 24 game hours")]
        CommandResult Forecast()
        {
            if (_schedule == null)
                return CommandResult.Fail(NoSchedule);
            var text = new StringBuilder();
            WeatherPlanner planner = _schedule.Planner;
            IReadOnlyList<WeatherSpell> plan = _schedule.Forecast(24d);
            double until = plan.Count > 0 ? plan[0].Start + 24d : 0d;
            foreach (WeatherSpell spell in plan)
            {
                if (spell.Start >= until)
                    break;
                text.Append($"{new WorldTime(spell.Start)}  {spell.Sky} + {spell.Wind}, {spell.Hours:0.0} h  [{planner.PeriodAt(spell.Start)}]");
                if (spell.Forced)
                    text.Append(" forced");
                text.AppendLine();
            }
            return CommandResult.Ok(text.ToString().TrimEnd());
        }

        [Command("A lightning flash and its thunder now")]
        string Strike()
        {
            _weather.Strike();
            return "strike";
        }

        // The night running now, or the coming one in the daytime.
        int Tonight
        {
            get
            {
                if (_clock == null || _schedule == null)
                    return 1;
                double now = _clock.Now.TotalHours;
                int latest = (int)Math.Floor((now - _schedule.Planner.NightStart) / WorldTime.HoursPerDay) + 1;
                WeatherPeriod period = _schedule.Planner.PeriodAt(now);
                return period is WeatherPeriod.Night or WeatherPeriod.BloodMoon ? latest : latest + 1;
            }
        }
    }
}
