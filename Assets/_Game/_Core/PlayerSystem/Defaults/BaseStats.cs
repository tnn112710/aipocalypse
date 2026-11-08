using System;
using MB13.Aipocalypse.Core.Stats;

namespace MB13.Aipocalypse.Core.PlayerSystem.Defaults
{
    // Used when no Progression/perk system is installed: every stat is its tuning value.
    public sealed class BaseStats : IStatProvider
    {
        public event Action Changed
        {
            add { }
            remove { }
        }

        public float Get(StatId stat, float baseValue) => baseValue;
    }
}
