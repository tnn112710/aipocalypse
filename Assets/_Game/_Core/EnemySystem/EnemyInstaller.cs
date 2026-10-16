using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Scene-scope enemy services. Enemies placed in the scene are injected when the scope builds; spawners inject their own.
    public sealed class EnemyInstaller : IInstaller
    {
        readonly EnemyBrain[] _sceneEnemies;
        readonly AstarPath _navigation;
        readonly WanderSettings _settings;

        public EnemyInstaller(EnemyBrain[] sceneEnemies, AstarPath navigation, WanderSettings settings)
        {
            _sceneEnemies = sceneEnemies;
            _navigation = navigation;
            _settings = settings;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_navigation);
            builder.RegisterInstance(_settings);
            builder.Register<EnemyRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EnemyManager>();
            builder.RegisterBuildCallback(resolver =>
            {
                foreach (EnemyBrain enemy in _sceneEnemies)
                    if (enemy != null)
                        resolver.InjectGameObject(enemy.gameObject);
            });
        }
    }
}
