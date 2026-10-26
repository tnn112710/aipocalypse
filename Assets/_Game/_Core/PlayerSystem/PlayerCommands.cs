using System.Collections.Generic;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.WeaponSystem;

namespace Aipocalypse.Core.PlayerSystem
{
    // Console commands on the active player (editor and development builds). A kill is a real hit through DamageService,
    // as with the K cheat.
    [CommandGroup("player")]
    public sealed class PlayerCommands : ConsoleCommands
    {
        static readonly CommandResult NoPlayer = CommandResult.Fail("No player in this scene");

        readonly PlayerRegistry _players;
        readonly DamageService _damage;

        public PlayerCommands(CommandRegistry registry, PlayerRegistry players, DamageService damage) : base(registry)
        {
            _players = players;
            _damage = damage;
        }

        IPlayer Current => _players.Current?.Health != null ? _players.Current : null;

        [Command("Kill the player with a real hit")]
        CommandResult Kill()
        {
            if (Current is not { } player)
                return NoPlayer;
            if (player.Health.IsDead)
                return CommandResult.Fail("Already dead");
            _damage.Deal(player, new DamageInfo(player.Health.Current, DamageType.Generic, player.Transform.position));
            return CommandResult.Ok();
        }

        [Command("Bring the player back to life")]
        CommandResult Revive()
        {
            if (Current is not { } player)
                return NoPlayer;
            player.Revive();
            return CommandResult.Ok();
        }

        [Command("Heal to full")]
        CommandResult Heal() => Heal(float.MaxValue);

        [Command("Heal by an amount")]
        CommandResult Heal(float amount)
        {
            if (Current is not { } player)
                return NoPlayer;
            Health health = player.Health;
            if (health.IsDead)
                return CommandResult.Fail("Dead: use player.revive");
            health.Heal(amount);
            return CommandResult.Ok($"Health {health.Current:0.#} / {health.Max:0.#}");
        }

        [Command("Refill stamina")]
        CommandResult Stamina()
        {
            if (Current is not { } player)
                return NoPlayer;
            player.Stamina.Restore();
            return CommandResult.Ok();
        }

        [Command("Add rounds of each ammo type the guns in the hotbar use")]
        CommandResult Ammo(int amount = 100)
        {
            if (Current is not { } player)
                return NoPlayer;
            var types = new List<AmmoType>();
            Hotbar hotbar = player.Equipment.Hotbar;
            for (int i = 0; i < hotbar.Count; i++)
                if (hotbar[i] is GunInstance gun && gun.Gun.Magazine.AmmoType != null && !types.Contains(gun.Gun.Magazine.AmmoType))
                    types.Add(gun.Gun.Magazine.AmmoType);
            if (types.Count == 0)
                return CommandResult.Fail("No gun in the hotbar");
            foreach (AmmoType type in types)
                player.Ammo.Add(type, amount);
            return CommandResult.Ok($"+{amount} {string.Join(", ", types.ConvertAll(t => t.name))}");
        }

        [Command("Whether reloading uses up the reserve", Name = "player.infammo")]
        CommandResult InfiniteAmmo() => Current is { } player ? CommandResult.Ok(CommandParser.Format(player.Ammo.Infinite)) : NoPlayer;

        [Command("Reloads never use up the reserve", Name = "player.infammo")]
        CommandResult InfiniteAmmo(bool on)
        {
            if (Current is not { } player)
                return NoPlayer;
            player.Ammo.Infinite = on;
            return InfiniteAmmo();
        }

        [Command("Where the player stands")]
        CommandResult Pos() => Current is { } player ? CommandResult.Ok(CommandParser.Format(player.Transform.position)) : NoPlayer;
    }
}
