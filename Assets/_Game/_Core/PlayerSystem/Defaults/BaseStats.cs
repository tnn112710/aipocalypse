using Aipocalypse.Core.Stats;

namespace Aipocalypse.Core.PlayerSystem.Defaults
{
    // Used when no Progression/perk system is installed: every stat is its tuning value.
    public sealed class BaseStats : IStatProvider
    {
        public float Get(StatId stat, float baseValue) => baseValue;
    }
}
