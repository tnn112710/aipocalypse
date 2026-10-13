using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.Bootstrap
{
    // Scene scope for World and for prototypes that need real systems. Must build before any EntityScope's Awake.
    [DefaultExecutionOrder(-5000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, Tooltip("On in World: an entity falling back to a default service is an error.")]
        bool _strict;

        public bool Strict => _strict;

        protected override void Configure(IContainerBuilder builder)
        {
        }
    }
}
