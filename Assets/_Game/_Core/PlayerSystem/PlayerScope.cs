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
        }
    }
}
