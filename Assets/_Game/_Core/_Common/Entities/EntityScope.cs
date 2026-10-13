using Aipocalypse.Core.Bootstrap;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.Entities
{
    // Child container on a unique entity prefab (player, dog, drone). Works in any scene:
    // parent is the scene's GameLifetimeScope if there is one, otherwise the root scope.
    // Runs after GameLifetimeScope (-5000) and before the entity's own components.
    [DefaultExecutionOrder(-4000)]
    public abstract class EntityScope : LifetimeScope
    {
        protected override LifetimeScope FindParent() => Find<GameLifetimeScope>(gameObject.scene);

        protected override void Configure(IContainerBuilder builder)
        {
            RegisterParts(builder);
            builder.RegisterBuildCallback(resolver => resolver.InjectGameObject(gameObject));
        }

        protected abstract void RegisterParts(IContainerBuilder builder);

        // A child registration would shadow the real service, so the default is only added when no ancestor provides one.
        protected void RegisterDefault<TService, TDefault>(IContainerBuilder builder) where TDefault : TService
        {
            if (Parent != null && Parent.Container.TryGetRegistration(typeof(TService), out _))
                return;

            if (Parent is GameLifetimeScope { Strict: true })
                throw new VContainerException(typeof(TService), $"{name}: no installer provides {typeof(TService).Name} (strict scene)");

            Debug.LogWarning($"{name}: using default {typeof(TDefault).Name} for {typeof(TService).Name}", this);
            builder.Register<TService, TDefault>(Lifetime.Singleton);
        }
    }
}
