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
        readonly ForecastReveal _reveal;

        public ForecastCommands(CommandRegistry registry, ForecastService forecast, ForecastReveal reveal) : base(registry)
        {
            _forecast = forecast;
            _reveal = reveal;
        }

        [Command("Reveal today's forecast now, as the rope does (the plaques spin)")]
        CommandResult Reveal()
        {
            if (_reveal.IsRevealed)
                return CommandResult.Fail("Already revealed");
            _reveal.Reveal();
            return CommandResult.Ok(Status());
        }

        [Command("Hang today's forecast as '?' again (it stays shown past SelfRevealHour)")]
        CommandResult Hide()
        {
            _reveal.Hide();
            return _reveal.IsRevealed ? CommandResult.Fail("Past the self-reveal hour: it stays shown") : CommandResult.Ok(Status());
        }

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
            ? string.Join(", ", Periods.Select(p => $"{p}: {_forecast.Fight(p)}")) + (_reveal.IsRevealed ? " (shown)" : " (hidden: '?')")
            : "Forecast off (ForecastSettings.Enabled)";
    }
}
