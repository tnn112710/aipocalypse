using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Scene system: the scene's EnvironmentDirector (injected, so it finds the clock), the optional GrassBendField, the
    // optional weather and, with the world clock installed, the time-of-day ambience.
    public sealed class EnvironmentInstaller : SceneInstaller
    {
        [SerializeField, Tooltip("The scene's time-of-day lighting. Follows the clock when there is one")] EnvironmentDirector _director;
        [SerializeField, Tooltip("Optional: bends Stylized Grass around enemies and animals")] GrassBendField _grassBending;
        [SerializeField, Tooltip("Optional: time-of-day sound beds and calls; needs the WorldTimeInstaller")] TimeOfDayAmbience _ambience;
        [SerializeField, Tooltip("Optional: keeps enemies readable at night (a light only they receive)")] NightHighlight _nightHighlight;
        [SerializeField, Tooltip("Optional: sky weathers and wind levels (WeatherService). Off in the asset = no weather")] WeatherSettings _weather;
        [SerializeField, Tooltip("Optional: the scene wind the weather scales")] WeatherWind _wind;
        [SerializeField, Tooltip("Optional: the lightning flash volume")] LightningFlash _lightning;

        public override string Problem => _director == null ? "no Environment Director assigned" : null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(_director);
            if (_grassBending != null)
                builder.RegisterComponent(_grassBending);
            if (_nightHighlight != null)
            {
                builder.RegisterComponent(_nightHighlight);
                // Editor and cheat builds only.
                if (Cheats.Enabled)
                    builder.RegisterEntryPoint<NightHighlightCommands>();
            }
            InstallWeather(builder);
            if (_ambience == null)
                return;
            if (!TryGetComponent(out WorldTimeInstaller clock) || clock.Problem != null)
            {
                Debug.LogWarning($"{name}: the ambience follows the world clock; add a WorldTimeInstaller to play it.", this);
                return;
            }
            builder.RegisterInstance(_ambience);
            builder.RegisterEntryPoint<AmbienceService>().AsSelf();
        }

        void InstallWeather(IContainerBuilder builder)
        {
            if (_weather == null || !_weather.Enabled)
                return;
            builder.RegisterInstance(_weather);
            if (_wind != null)
                builder.RegisterComponent(_wind);
            if (_lightning != null)
                builder.RegisterComponent(_lightning);
            builder.RegisterEntryPoint<WeatherService>().AsSelf();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<WeatherCommands>();
        }
    }
}
