using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.GraphicsSystem;
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
            // Only for the MessagePipe Diagnostics window (editor and development builds); game code never uses GlobalMessagePipe.
            if (Debug.isDebugBuild)
                builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));

            new InputInstaller(_controls).Install(builder);
            new AudioInstaller().Install(builder);
            new CombatInstaller().Install(builder);
            new TimeInstaller().Install(builder);
            new GraphicsInstaller().Install(builder);
            new QuickTimeInstaller().Install(builder);
            new PlayerInstaller().Install(builder);
            new WeaponInstaller().Install(builder);
        }
    }
}
