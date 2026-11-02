using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.Debugging
{
    // Root installer of the dev console. ScreenLog always (it does nothing without cheats); in the editor and cheat
    // builds also the command registry (systems register their ConsoleCommands there), the console and its overlay.
    public sealed class ConsoleInstaller : IInstaller
    {
        readonly ConsoleSettings _settings;
        readonly ConsoleView _overlay;

        public ConsoleInstaller(ConsoleSettings settings, ConsoleView overlay)
        {
            _settings = settings;
            _overlay = overlay;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ScreenLog>().AsSelf();
            if (!Cheats.Enabled)
                return;
            builder.Register<CommandRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<FpsCounterCommands>();
            if (_settings == null || _overlay == null)
            {
                Debug.LogWarning("Dev console off: ProjectLifetimeScope has no Console Settings or Console Overlay assigned");
                return;
            }
            builder.RegisterInstance(_settings);
            builder.Register<LogBuffer>(Lifetime.Singleton).WithParameter("capacity", _settings.LogCapacity);
            builder.RegisterEntryPoint<ConsoleService>().AsSelf();
            builder.RegisterEntryPoint<BuiltInCommands>();
            builder.RegisterComponentInNewPrefab(_overlay, Lifetime.Singleton).DontDestroyOnLoad();
            // Nothing injects the overlay, so it's created with the scope.
            builder.RegisterBuildCallback(resolver => resolver.Resolve<ConsoleView>());
        }
    }
}
