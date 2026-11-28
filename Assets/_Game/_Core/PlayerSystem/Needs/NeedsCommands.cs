using System;
using System.Text;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.Stats;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Cheats on the player's meters and needs (editor and cheat builds): set any meter, freeze the drains, set the hours
    // awake, run the drain hook. Registered by PlayerInstaller, so cheat.meter works in every scene with a player.
    [CommandGroup("cheat")]
    public sealed class NeedsCommands : ConsoleCommands
    {
        static readonly CommandResult NoPlayer = CommandResult.Fail("No player in this scene");
        static readonly CommandResult NoNeeds = CommandResult.Fail("The player has no needs (PlayerTuning.Needs is empty)");
        static readonly PlayerMeterId[] Meters = (PlayerMeterId[])Enum.GetValues(typeof(PlayerMeterId));

        readonly PlayerRegistry _players;

        public NeedsCommands(CommandRegistry registry, PlayerRegistry players) : base(registry) => _players = players;

        IPlayer Current => _players.Current?.Health != null ? _players.Current : null;

        [Command("Fill a meter (health, stamina, hunger, thirst, sanity) or all of them: cheat.meter hunger")]
        CommandResult Meter(string meter) => Meter(meter, 100f);

        [Command("Set a meter (health, stamina, hunger, thirst, sanity, all) to a percent of its max: cheat.meter thirst 10")]
        CommandResult Meter(string meter, float percent)
        {
            if (Current is not { } player)
                return NoPlayer;
            float share = Math.Clamp(percent, 0f, 100f) / 100f;
            var log = new StringBuilder();
            if (string.Equals(meter, "all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (PlayerMeterId id in Meters)
                    Set(player, id, share, log);
                return CommandResult.Ok(log.ToString().TrimEnd());
            }
            if (!TryParse(meter, out PlayerMeterId found))
                return CommandResult.Fail($"No meter '{meter}': {string.Join(", ", Meters)} or all");
            if (player.Meter(found) == null)
                return NoNeeds;
            if (!CanSet(found, share, player.IsDead))
                return CommandResult.Fail(player.IsDead ? "Dead: use cheat.revive" : "Health 0 is death: use cheat.kill");
            Set(player, found, share, log);
            return CommandResult.Ok(log.ToString().TrimEnd());
        }

        [Command("Every meter: value, max and the needs' tiers, hours awake, frozen")]
        CommandResult Needs()
        {
            if (Current is not { } player)
                return NoPlayer;
            var log = new StringBuilder();
            foreach (PlayerMeterId id in Meters)
                Describe(player, id, log);
            if (player.Needs != null)
                log.Append($"awake {player.Needs.HoursAwake:0.#} h, drains {(player.Needs.Frozen ? "frozen" : "running")}");
            return CommandResult.Ok(log.ToString().TrimEnd());
        }

        [Command("Whether the needs' drains are frozen", Name = "cheat.needsfreeze")]
        CommandResult Freeze() =>
            Current is not { } player ? NoPlayer : player.Needs == null ? NoNeeds : CommandResult.Ok(CommandParser.Format(player.Needs.Frozen));

        [Command("Freeze every need drain (refills and cheats still work)", Name = "cheat.needsfreeze")]
        CommandResult Freeze(bool on)
        {
            if (Current is not { } player)
                return NoPlayer;
            if (player.Needs == null)
                return NoNeeds;
            player.Needs.Frozen = on;
            return Freeze();
        }

        [Command("Hours the player has been awake (lack of sleep drains sanity later)")]
        CommandResult Awake() =>
            Current is not { } player ? NoPlayer : player.Needs == null ? NoNeeds : CommandResult.Ok($"{player.Needs.HoursAwake:0.##} h");

        [Command("Set the hours awake: cheat.awake 16")]
        CommandResult Awake(float hours)
        {
            if (Current is not { } player)
                return NoPlayer;
            if (player.Needs == null)
                return NoNeeds;
            player.Needs.HoursAwake = Math.Max(0f, hours);
            return Awake();
        }

        [Command("Drain a need through the hook special enemies use (scaled by its drain stat): cheat.drain sanity 20")]
        CommandResult Drain(string meter, float amount)
        {
            if (Current is not { } player)
                return NoPlayer;
            if (player.Needs == null)
                return NoNeeds;
            if (!TryParse(meter, out PlayerMeterId found) || player.Needs.Need(found) == null)
                return CommandResult.Fail($"No need '{meter}': hunger, thirst or sanity");
            player.Needs.Drain(found, amount, "console");
            var log = new StringBuilder();
            Describe(player, found, log);
            return CommandResult.Ok(log.ToString().TrimEnd());
        }

        [Command("What the player carries: kg, the Comfortable and Max limits, the state and its speed / stamina scales")]
        CommandResult Carry()
        {
            if (Current is not { } player)
                return NoPlayer;
            CarryLoad load = player.Load;
            return CommandResult.Ok($"{load.Weight:0.0} kg (+{player.ExtraLoad:0.#} cheat) of {load.Comfortable:0.#} comfortable / {load.Max:0.#} max: " +
                $"{load.State}, speed × {load.SpeedMultiplier:0.##}, stamina × {load.CostMultiplier:0.##}");
        }

        [Command("Add kg to what the player carries, for testing the slowdown (0 = none): cheat.carry 15")]
        CommandResult Carry(float kg)
        {
            if (Current is not { } player)
                return NoPlayer;
            player.ExtraLoad = kg;
            return Carry();
        }

        static void Set(IPlayer player, PlayerMeterId id, float share, StringBuilder log)
        {
            IMeter meter = player.Meter(id);
            if (meter == null || !CanSet(id, share, player.IsDead))
                return;
            meter.Set(share * meter.Max);
            Describe(player, id, log);
        }

        static void Describe(IPlayer player, PlayerMeterId id, StringBuilder log)
        {
            IMeter meter = player.Meter(id);
            if (meter == null)
                return;
            log.Append($"{id} {meter.Current:0.#} / {meter.Max:0.#}");
            Need need = player.Needs?.Need(id);
            if (need != null)
                log.Append($" ({need.Severity})");
            log.Append('\n');
        }

        // Health is never set on the dead (a living value would skip the revive flow) nor to 0 (that's death: cheat.kill).
        public static bool CanSet(PlayerMeterId id, float share, bool dead) => id != PlayerMeterId.Health || (!dead && share > 0f);

        // A meter by any unique start of its name, ignoring case (hun, th, san).
        static bool TryParse(string text, out PlayerMeterId id)
        {
            id = default;
            int matches = 0;
            foreach (PlayerMeterId candidate in Meters)
            {
                if (!candidate.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    id = candidate;
                    return true;
                }
                id = candidate;
                matches++;
            }
            return matches == 1;
        }
    }
}
