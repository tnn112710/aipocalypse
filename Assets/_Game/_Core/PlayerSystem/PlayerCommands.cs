using Aipocalypse.Core.Debugging;

namespace Aipocalypse.Core.PlayerSystem
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
    }
}
