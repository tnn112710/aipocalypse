using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.PlayerSystem.Defaults;
using MB13.Aipocalypse.Core.Stats;
using VContainer;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    public sealed class PlayerScope : EntityScope
    {
        protected override void RegisterParts(IContainerBuilder builder)
        {
            RegisterDefault<IStatProvider, BaseStats>(builder);
        }
    }
}
