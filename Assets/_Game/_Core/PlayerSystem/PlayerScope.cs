using Aipocalypse.Core.Entities;
using Aipocalypse.Core.PlayerSystem.Defaults;
using Aipocalypse.Core.Stats;
using VContainer;

namespace Aipocalypse.Core.PlayerSystem
{
    public sealed class PlayerScope : EntityScope
    {
        protected override void RegisterParts(IContainerBuilder builder)
        {
            RegisterDefault<IStatProvider, BaseStats>(builder);
            // Instance only, no injection: InjectGameObject already injects it. Lets views in the hierarchy (HUD) read this player.
            builder.RegisterInstance(GetComponent<Player>());
        }
    }
}
