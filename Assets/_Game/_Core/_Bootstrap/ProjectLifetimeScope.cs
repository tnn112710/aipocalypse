using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Input;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.Bootstrap
{
    // Root scope: VContainerSettings instantiates this prefab before any scene loads, so every scene has these services.
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] ControlSettings _controls = new();

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(options =>
            {
#if UNITY_EDITOR
                options.EnableCaptureStackTrace = true;
#endif
            });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Only for the MessagePipe Diagnostics window; game code never uses GlobalMessagePipe.
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
#endif

            new InputInstaller(_controls).Install(builder);
            new TimeInstaller().Install(builder);
            new QuickTimeInstaller().Install(builder);
            new PlayerInstaller().Install(builder);
            new WeaponInstaller().Install(builder);
        }
    }
}
