using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
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

        [Command("Whether enemies' eyes glow at night")]
        CommandResult Eyes() => CommandResult.Ok(CommandParser.Format(_highlight.EyesOn) + (_highlight.EyesOverride.HasValue ? " (console)" : " (settings)"));

        [Command("Turn the enemies' glowing eyes on or off")]
        CommandResult Eyes(bool on)
        {
            _highlight.EyesOverride = on;
            return Eyes();
        }

        [Command("The light and eyes follow the settings asset again")]
        CommandResult Reset()
        {
            _highlight.LightOverride = null;
            _highlight.EyesOverride = null;
            return CommandResult.Ok($"light {Light().Message}, eyes {Eyes().Message}");
        }
    }
}
