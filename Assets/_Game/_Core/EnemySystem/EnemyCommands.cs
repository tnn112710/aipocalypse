using Aipocalypse.Core.Debugging;

namespace Aipocalypse.Core.EnemySystem
{
    // Console commands on the scene's enemies (editor and development builds); they exist only in scenes with an EnemyInstaller.
    [CommandGroup("enemy")]
    public sealed class EnemyCommands : ConsoleCommands
    {
        readonly EnemyRegistry _enemies;

        public EnemyCommands(CommandRegistry registry, EnemyRegistry enemies) : base(registry) => _enemies = enemies;

        [Command("Enemies in play")]
        int Count() => _enemies.Count;
    }
}
