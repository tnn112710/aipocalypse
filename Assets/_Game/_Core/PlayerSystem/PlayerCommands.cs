using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Console commands that read the active player (editor and development builds); the ones that change it are cheats
    // (PlayerCheatCommands).
    [CommandGroup("player")]
    public sealed class PlayerCommands : ConsoleCommands
    {
        readonly PlayerRegistry _players;

        public PlayerCommands(CommandRegistry registry, PlayerRegistry players) : base(registry) => _players = players;

        [Command("Where the player stands")]
        CommandResult Pos() => _players.Current is { } player
            ? CommandResult.Ok(CommandParser.Format(player.Transform.position))
            : CommandResult.Fail("No player in this scene");

        [Command("Whether the movement readout is on")]
        CommandResult Speed() => _players.Current is { } player
            ? CommandResult.Ok(CommandParser.Format(player.ShowMovementReadout))
            : CommandResult.Fail("No player in this scene");

        [Command("Show the speed and every part that sets it (direction, mobility, aim, fatigue…) on screen")]
        CommandResult Speed(bool on)
        {
            if (_players.Current is not { } player)
                return CommandResult.Fail("No player in this scene");
            player.ShowMovementReadout = on;
            return Speed();
        }
    }
}
