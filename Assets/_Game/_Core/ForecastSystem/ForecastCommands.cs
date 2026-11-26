using System.Linq;
using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // Console commands on the night forecast (editor and cheat builds); they exist only in scenes with a ForecastInstaller.
    [CommandGroup("forecast")]
    public sealed class ForecastCommands : ConsoleCommands
    {
        static readonly NightPeriod[] Periods = (NightPeriod[])System.Enum.GetValues(typeof(NightPeriod));

        readonly ForecastService _forecast;

        public ForecastCommands(CommandRegistry registry, ForecastService forecast) : base(registry) => _forecast = forecast;

        [Command("Set the fight a period brings until forecast.reset, e.g. forecast.set Evening Boss")]
        string Set(NightPeriod period, FightSize fight)
        {
            _forecast.Override(period, fight);
            return Status();
        }

        [Command("Back to the forecast in ForecastSettings")]
        string Reset()
        {
            _forecast.ClearOverrides();
            return Status();
        }

        [Command("Tonight's forecast per period")]
        string Status() => _forecast.Enabled
            ? string.Join(", ", Periods.Select(p => $"{p}: {_forecast.Fight(p)}"))
            : "Forecast off (ForecastSettings.Enabled)";
    }
}
