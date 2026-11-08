using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Bootstrap
{
    // One scene system's settings and registrations, as a component next to the GameLifetimeScope. A scene gets a system by
    // adding its installer and drops it by removing the component; the scope installs every one it finds.
    [RequireComponent(typeof(GameLifetimeScope))]
    public abstract class SceneInstaller : MonoBehaviour, IInstaller
    {
        // What's missing for it to install (a reference not assigned), or null when it's ready.
        public abstract string Problem { get; }

        public abstract void Install(IContainerBuilder builder);
    }
}
