using Pathfinding;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    public sealed class EnemyInstaller
    {
        readonly EnemyBrain[] _enemies;
        readonly AstarPath _navigation;
        readonly WanderSettings _settings;

        public EnemyInstaller(EnemyBrain[] enemies, AstarPath navigation, WanderSettings settings)
        {
            _enemies = enemies;
            _navigation = navigation;
            _settings = settings;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_enemies);
            builder.RegisterInstance(_navigation);
            builder.RegisterInstance(_settings);
            builder.RegisterEntryPoint<EnemyManager>();
        }
    }
}
