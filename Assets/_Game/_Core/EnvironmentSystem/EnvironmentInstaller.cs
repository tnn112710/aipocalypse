using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Scene scope: the scene's EnvironmentDirector (injected, so it finds the clock) and, with a clock, the time-of-day ambience.
    public sealed class EnvironmentInstaller : IInstaller
    {
        readonly EnvironmentDirector _director;
        readonly TimeOfDayAmbience _ambience;

        public EnvironmentInstaller(EnvironmentDirector director, TimeOfDayAmbience ambience)
        {
            _director = director;
            _ambience = ambience;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterComponent(_director);
            if (_ambience == null)
                return;
            builder.RegisterInstance(_ambience);
            builder.RegisterEntryPoint<AmbienceService>().AsSelf();
        }
    }
}
