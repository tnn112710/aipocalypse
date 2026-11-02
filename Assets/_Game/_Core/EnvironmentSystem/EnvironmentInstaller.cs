using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Scene system: the scene's EnvironmentDirector (injected, so it finds the clock), the optional GrassBendField and, with
    // the world clock installed, the time-of-day ambience.
    public sealed class EnvironmentInstaller : SceneInstaller
    {
        [SerializeField, Tooltip("The scene's time-of-day lighting. Follows the clock when there is one")] EnvironmentDirector _director;
        [SerializeField, Tooltip("Optional: bends Stylized Grass around enemies and animals")] GrassBendField _grassBending;
        [SerializeField, Tooltip("Optional: time-of-day sound beds and calls; needs the WorldTimeInstaller")] TimeOfDayAmbience _ambience;
        [SerializeField, Tooltip("Optional: keeps enemies readable at night (a light only they receive)")] NightHighlight _nightHighlight;

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
    }
}
