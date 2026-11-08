using System;
using System.Collections.Generic;
using Aipocalypse.Core.RandomSystem;
using Aipocalypse.Core.WorldTimeSystem;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Changes the weather on its own by WeatherRules: follows the world clock through a WeatherPlanner and shows the planned
    // weather on WeatherService. After a clock jump or a time skip it cuts to the weather of the new time instead of blending.
    // With it on, change the weather through Hold / Force here: WeatherService.Set alone is replaced on the next frame.
    public sealed class WeatherSchedule : ITickable
    {
        // A clock step longer than this in one frame is a jump: cut, don't blend.
        const double JumpHours = 0.5d;

        readonly WeatherService _weather;
        readonly WeatherSettings _settings;
        readonly WeatherPlanner _planner;
        readonly WorldClock _clock;
        double _last = double.NaN;
        bool _cut = true;

        public WeatherSchedule(WeatherRules rules, WeatherSettings settings, WeatherService weather, RandomService random, IObjectResolver resolver)
        {
            _settings = settings;
            _weather = weather;
            _planner = new WeatherPlanner(rules, random.Stream("weather"));
            resolver.TryResolve(out _clock);
        }

        public WeatherPlanner Planner => _planner;
        public bool HasClock => _clock != null;
        public bool IsHeld => _planner.IsHeld;
        public WeatherPeriod Period => _clock != null ? _planner.PeriodAt(_clock.Now.TotalHours) : WeatherPeriod.Day;

        public void Tick()
        {
            if (_clock == null)
                return;
            double now = _clock.Now.TotalHours;
            if (double.IsNaN(_last) || now < _last - 1e-6)
            {
                _planner.Restart(now, _weather.Sky, _weather.Wind);
                _cut = true;
            }
            else if (now - _last > JumpHours)
                _cut = true;
            _last = now;
            // A skip (sleeping) shows the weather it lands on, cut, when it ends.
            if (_clock.IsSkipping)
            {
                _cut = true;
                return;
            }
            Apply(now);
        }

        // Keeps this weather until Release; wind null = the sky's default wind. Blends to it now.
        public void Hold(SkyWeather sky, WindLevel? wind = null)
        {
            Sync();
            _planner.Force(sky, wind ?? DefaultWind(sky), Now, double.PositiveInfinity);
            ApplyNow();
        }

        // This weather from the next time the clock shows `hour` (or now with hour null) for `hours`; wind null = rolled.
        public void Force(SkyWeather sky, WindLevel? wind, double hours, float? hour = null)
        {
            Sync();
            double now = Now;
            double from = hour.HasValue ? NextTimeAt(now, hour.Value) : now;
            _planner.Force(sky, wind, from, hours);
            ApplyNow();
        }

        // Back to the rules from the weather showing now.
        public void Release()
        {
            Sync();
            _planner.Release();
            ApplyNow();
        }

        public void MarkBloodMoon(int day, bool on = true)
        {
            Sync();
            _planner.MarkBloodMoon(day, on);
            ApplyNow();
        }

        public void MarkEclipse(int day, bool on = true)
        {
            Sync();
            _planner.MarkEclipse(day, on);
            ApplyNow();
        }

        // Plans at least `hours` ahead and returns the plan (from the weather showing now).
        public IReadOnlyList<WeatherSpell> Forecast(double hours)
        {
            Sync();
            _planner.Extend(Now + hours);
            return _planner.Plan;
        }

        double Now => _clock != null ? _clock.Now.TotalHours : 0d;

        // Before changing the plan from outside Tick: make sure it has started and is at the clock's time.
        void Sync()
        {
            if (double.IsNaN(_last))
            {
                _last = Now;
                _planner.Restart(_last, _weather.Sky, _weather.Wind);
            }
            _planner.At(Math.Max(Now, _last));
        }

        void ApplyNow()
        {
            if (_clock != null && !_clock.IsSkipping)
                Apply(Now);
        }

        void Apply(double now)
        {
            WeatherSpell spell = _planner.At(now);
            bool changed = spell.Sky != _weather.Sky || spell.Wind != _weather.Wind;
            if (changed || (_cut && _weather.Progress < 1f))
                _weather.Set(spell.Sky, spell.Wind, _cut ? 0f : null);
            _cut = false;
        }

        WindLevel DefaultWind(SkyWeather sky) =>
            _settings.Skies.TryGetValue(sky, out WeatherSettings.Sky value) ? value.DefaultWind : WindLevel.Breezy;

        static double NextTimeAt(double now, float hour)
        {
            double target = Math.Floor(now / WorldTime.HoursPerDay) * WorldTime.HoursPerDay + hour;
            return target < now ? target + WorldTime.HoursPerDay : target;
        }
    }
}
