using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // Scene service: burns every lit campfire by the game time that passes (exactly what the clock moved, a sleep skip
    // included; jumps and loads don't count), faster in rain. Without a WorldClock nothing burns; without weather it's × 1.
    public sealed class CampfireTicker : ITickable, IDisposable
    {
        readonly CampfireRegistry _registry;
        readonly WorldClock _clock;
        readonly WeatherService _weather;
        readonly IDisposable _jumps;
        double _lastHours = double.NaN;

        public CampfireTicker(CampfireRegistry registry, IObjectResolver resolver)
        {
            _registry = registry;
            resolver.TryResolve(out _clock);
            resolver.TryResolve(out _weather);
            if (resolver.TryResolve(out ISubscriber<TimeJumped> jumps))
                _jumps = jumps.Subscribe(jump => Rebase(jump.To.TotalHours));
        }

        public void Dispose() => _jumps?.Dispose();

        public void Tick()
        {
            if (_clock != null)
                Observe(_clock.Now.TotalHours, _weather != null ? _weather.Sky : null);
        }

        // Burns the hours since the last look at the clock. The first look, and a clock that went back (a load), only note
        // the time.
        public void Observe(double nowHours, SkyWeather? sky)
        {
            double last = _lastHours;
            _lastHours = nowHours;
            if (double.IsNaN(last) || nowHours <= last)
                return;
            Step(_registry.Fires, (float)(nowHours - last), sky);
        }

        // The clock jumped (TimeJumped): those hours didn't pass for the fire.
        public void Rebase(double nowHours) => _lastHours = nowHours;

        public static void Step(IReadOnlyList<Campfire> fires, float hours, SkyWeather? sky)
        {
            if (hours <= 0f)
                return;
            for (int i = 0; i < fires.Count; i++)
            {
                Campfire fire = fires[i];
                if (fire == null || !fire.Fuel.Lit)
                    continue;
                fire.Burn(hours, sky.HasValue && fire.Settings != null ? fire.Settings.BurnScale(sky.Value) : 1f);
            }
        }
    }
}
