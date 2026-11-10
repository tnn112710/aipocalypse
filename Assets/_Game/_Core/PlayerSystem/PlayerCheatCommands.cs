using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Cheats on the active player (editor and development builds). A kill is a real hit through DamageService, as with the
    // K key, and gets through god mode. Enemy cheats (cheat.killall) live with the enemy system.
    [CommandGroup("cheat")]
    public sealed class PlayerCheatCommands : ConsoleCommands
    {
        static readonly CommandResult NoPlayer = CommandResult.Fail("No player in this scene");

        readonly PlayerRegistry _players;
        readonly DamageService _damage;
        readonly List<AmmoType> _ammoTypes = new();

        public PlayerCheatCommands(CommandRegistry registry, PlayerRegistry players, DamageService damage) : base(registry)
        {
            _players = players;
            _damage = damage;
        }

        IPlayer Current => _players.Current?.Health != null ? _players.Current : null;

        // A lethal hit through DamageService (hit feedback and death flow as usual), even in god mode.
        internal static void Kill(IPlayer player, DamageService damage)
        {
            bool god = player.Invulnerable;
            player.Invulnerable = false;
            try
            {
                damage.Deal(player, new DamageInfo(player.Health.Current, DamageType.Generic, player.Transform.position));
            }
            finally
            {
                player.Invulnerable = god;
            }
        }

        [Command("Kill the player with a real hit")]
        CommandResult Kill()
        {
            if (Current is not { } player)
                return NoPlayer;
            if (player.Health.IsDead)
                return CommandResult.Fail("Already dead");
            Kill(player, _damage);
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
        CommandResult Hp() => Hp(float.MaxValue);

        [Command("Heal by an amount")]
        CommandResult Hp(float amount)
        {
            if (Current is not { } player)
                return NoPlayer;
            Health health = player.Health;
            if (health.IsDead)
                return CommandResult.Fail("Dead: use cheat.revive");
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
            HotbarAmmo.TypesInUse(player.Equipment?.Hotbar, _ammoTypes);
            if (_ammoTypes.Count == 0)
                return CommandResult.Fail("No gun in the hotbar");
            foreach (AmmoType type in _ammoTypes)
                player.Ammo.Add(type, amount);
            return CommandResult.Ok($"+{amount} {string.Join(", ", _ammoTypes.ConvertAll(t => t.name))}");
        }

        [Command("Whether reloading uses up the reserve", Name = "cheat.infammo")]
        CommandResult InfiniteAmmo() => Current is { } player ? CommandResult.Ok(CommandParser.Format(player.Ammo.Infinite)) : NoPlayer;

        [Command("Reloads never use up the reserve", Name = "cheat.infammo")]
        CommandResult InfiniteAmmo(bool on)
        {
            if (Current is not { } player)
                return NoPlayer;
            player.Ammo.Infinite = on;
            return InfiniteAmmo();
        }

        [Command("Whether hits hurt the player")]
        CommandResult God() => Current is { } player ? CommandResult.Ok(CommandParser.Format(player.Invulnerable)) : NoPlayer;

        [Command("Hits apply nothing to the player")]
        CommandResult God(bool on)
        {
            if (Current is not { } player)
                return NoPlayer;
            player.Invulnerable = on;
            return God();
        }

        [Command("Whether enemies ignore the player")]
        CommandResult NoTarget() => Current is { } player ? CommandResult.Ok(CommandParser.Format(player.IgnoredByEnemies)) : NoPlayer;

        [Command("Enemies don't choose the player as a target (they wander instead)")]
        CommandResult NoTarget(bool on)
        {
            if (Current is not { } player)
                return NoPlayer;
            player.IgnoredByEnemies = on;
            return NoTarget();
        }

        [Command("Put an item in the inventory where a pickup would go (cheat.give tokens 500, cheat.give medic). Matches the start of " +
            "any loaded item's asset or display name")]
        CommandResult Give(string item, int count = 1)
        {
            if (Current is not { } player || player.Equipment == null)
                return NoPlayer;
            ItemDefinition found = null;
            foreach (ItemDefinition definition in Resources.FindObjectsOfTypeAll<ItemDefinition>())
            {
                if (definition.name.StartsWith(item, StringComparison.OrdinalIgnoreCase)
                    || (definition.DisplayName ?? string.Empty).StartsWith(item, StringComparison.OrdinalIgnoreCase))
                {
                    found = definition;
                    break;
                }
            }
            if (found == null)
                return CommandResult.Fail($"No loaded item called {item}");
            int added = player.Equipment.Inventory.AddNew(found, Math.Max(1, count));
            return added > 0
                ? CommandResult.Ok($"+{added} {found.DisplayName}{(added < count ? " (no room for the rest)" : "")}")
                : CommandResult.Fail($"No room for {found.DisplayName}");
        }

        [Command("Pick up the first item lying in the scene whose name starts with this (cheat.take ak47), wherever it is")]
        CommandResult Take(string item)
        {
            if (Current is not { } player)
                return NoPlayer;
            GameObject actor = player.Transform.gameObject;
            foreach (WorldItem lying in UnityEngine.Object.FindObjectsByType<WorldItem>())
            {
                if (!lying.name.StartsWith(item, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!lying.TryGetPrompt(actor, out _))
                    return CommandResult.Fail($"Can't pick up {lying.name}");
                lying.Interact(actor);
                return CommandResult.Ok($"Took {lying.name}");
            }
            return CommandResult.Fail($"No {item} lying in this scene");
        }
    }
}
