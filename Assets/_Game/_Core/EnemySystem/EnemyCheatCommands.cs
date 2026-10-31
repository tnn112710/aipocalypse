using System.Collections.Generic;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Debugging;

namespace Aipocalypse.Core.EnemySystem
{
    // Enemy cheats (editor and development builds), next to the player's in the cheat group.
    [CommandGroup("cheat")]
    public sealed class EnemyCheatCommands : ConsoleCommands
    {
        readonly EnemyRegistry _enemies;
        readonly DamageService _damage;
        readonly List<EnemyBrain> _targets = new();

        public EnemyCheatCommands(CommandRegistry registry, EnemyRegistry enemies, DamageService damage) : base(registry)
        {
            _enemies = enemies;
            _damage = damage;
        }

        [Command("Kill every enemy in play with a real hit")]
        CommandResult KillAll()
        {
            _targets.Clear();
            for (int i = 0; i < _enemies.Count; i++)
                _targets.Add(_enemies[i]);
            int killed = 0;
            foreach (EnemyBrain enemy in _targets)
            {
                if (enemy.Body == null || enemy.IsDead)
                    continue;
                _damage.Deal(enemy.Body, new DamageInfo(enemy.Body.Health.Current, DamageType.Generic, enemy.transform.position));
                killed++;
            }
            return CommandResult.Ok($"{killed} killed");
        }
    }
}
