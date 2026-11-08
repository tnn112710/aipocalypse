using MB13.Aipocalypse.Core.Bootstrap;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Entities
{
    // Child container on a self-contained prefab (player, dog, drone, HUD), parented to the scene's GameLifetimeScope or else
    // the root scope, so it works in any scene. Runs after GameLifetimeScope (-5000), before the entity's own components.
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
            // TryGetRegistration only looks in that one container, so walk up to the root.
            for (LifetimeScope scope = Parent; scope != null; scope = scope.Parent)
                if (scope.Container != null && scope.Container.TryGetRegistration(typeof(TService), out _))
                    return;

            if (Parent is GameLifetimeScope { Strict: true })
                throw new VContainerException(typeof(TService), $"{name}: no installer provides {typeof(TService).Name} (strict scene)");

            Debug.LogWarning($"{name}: using default {typeof(TDefault).Name} for {typeof(TService).Name}", this);
            builder.Register<TService, TDefault>(Lifetime.Singleton);
        }
    }
}
