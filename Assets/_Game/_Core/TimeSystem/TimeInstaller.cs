using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.TimeSystem
{
    public sealed class TimeInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TimeService>().AsSelf();
            // Editor and development builds only.
            if (Debug.isDebugBuild)
                builder.RegisterEntryPoint<TimeCommands>();
        }
    }
}
