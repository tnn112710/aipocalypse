using Aipocalypse.Core.Debugging;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Switches the enemies' night light for this session (editor and development builds); the settings asset keeps its own
    // switch.
    [CommandGroup("highlight")]
    public sealed class NightHighlightCommands : ConsoleCommands
    {
        readonly NightHighlight _highlight;

        public NightHighlightCommands(CommandRegistry registry, NightHighlight highlight) : base(registry) => _highlight = highlight;

        [Command("Whether enemies get their own light at night")]
        CommandResult Light() => CommandResult.Ok(CommandParser.Format(_highlight.LightOn) + (_highlight.LightOverride.HasValue ? " (console)" : " (settings)"));

        [Command("Turn the enemies' night light on or off")]
        CommandResult Light(bool on)
        {
            _highlight.LightOverride = on;
            return Light();
        }

        [Command("The light follows the settings asset again")]
        CommandResult Reset()
        {
            _highlight.LightOverride = null;
            return Light();
        }
    }
}
