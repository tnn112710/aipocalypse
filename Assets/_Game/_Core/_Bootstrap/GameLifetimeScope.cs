using UnityEngine;
using VContainer;
using VContainer.Unity;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.WorldTimeSystem;
using Pathfinding;

namespace Aipocalypse.Core.Bootstrap
{
    // Scene scope for World and for prototypes that need real systems. Must build before any EntityScope's Awake.
    [DefaultExecutionOrder(-5000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, Tooltip("On in World: an entity falling back to a default service is an error.")]
        bool _strict;
        [SerializeField] AstarPath _navigation;
        [SerializeField] WanderSettings _wanderSettings;
        [SerializeField, Tooltip("Enemies placed in this scene, injected when the scope builds. Spawned enemies are injected by their spawner")]
        EnemyBrain[] _enemies = System.Array.Empty<EnemyBrain>();
        [SerializeField, Tooltip("Runs the world clock in this scene. Empty: no clock (the HUD clock hides, the sky keeps its preview hour)")]
        ClockSettings _clock;
        [SerializeField, Tooltip("The scene's time-of-day lighting. Follows the clock when there is one")]
        EnvironmentDirector _environment;
        [SerializeField, Tooltip("Time-of-day sound beds and calls; needs the clock")]
        TimeOfDayAmbience _ambience;

        public bool Strict => _strict;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_navigation != null && _wanderSettings != null)
                new EnemyInstaller(_enemies, _navigation, _wanderSettings).Install(builder);
            if (_clock != null)
                new WorldTimeInstaller(_clock).Install(builder);
            if (_environment != null)
                new EnvironmentInstaller(_environment, _clock != null ? _ambience : null).Install(builder);
        }
    }
}
