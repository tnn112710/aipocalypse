using System.Text;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using VContainer;
using VContainer.Unity;

// Play-mode probe for the survival needs: runs console commands (';' between them) and reports the player's meters.
// Enter Play in WorldMap first. Run: unity command script run --file AgentScripts/Needs/NeedsProbe.cs --entry NeedsProbe.Run
//   --args '["clock.set 8; cheat.meter hunger 40"]'
public static class NeedsProbe
{
    public static string Run(string commands)
    {
        LifetimeScope scope = LifetimeScope.Find<GameLifetimeScope>();
        if (scope == null || scope.Container == null)
            return "no GameLifetimeScope (enter Play in WorldMap)";
        IObjectResolver container = scope.Container;
        var log = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(commands))
        {
            if (!container.TryResolve(out CommandRegistry registry))
                return "no CommandRegistry";
            foreach (string command in commands.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(command))
                    continue;
                CommandResult result = registry.Execute(command.Trim());
                log.AppendLine($"> {command.Trim()}: {(result.Succeeded ? "ok" : "FAILED")} {result.Message}");
            }
        }
        log.Append(State(container));
        return log.ToString();
    }

    public static string State() => Run(null);

    // Reads the clock's shown text every frame for `seconds` and lists each distinct reading (sanity's lies show as wrong times).
    public static async System.Threading.Tasks.Task<string> WatchClock(float seconds = 20f)
    {
        var clock = UnityEngine.Object.FindAnyObjectByType<MB13.Aipocalypse.Core.UI.ClockHud>();
        if (clock == null)
            return "no ClockHud";
        var seen = new System.Collections.Generic.Dictionary<string, int>();
        float end = UnityEngine.Time.realtimeSinceStartup + seconds;
        while (UnityEngine.Time.realtimeSinceStartup < end)
        {
            var parts = new System.Text.StringBuilder();
            foreach (var text in clock.GetComponentsInChildren<TMPro.TMP_Text>())
                parts.Append(text.text).Append('|');
            string key = parts.ToString();
            seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
            await System.Threading.Tasks.Task.Yield();
        }
        var log = new StringBuilder();
        foreach (var pair in seen)
            log.AppendLine($"{pair.Value} frames: {pair.Key}");
        return log.ToString();
    }

    // Starts a sleep in the scene's first bed at its default wake-up time (as the bed's prompt and the sleep screen would).
    public static string Sleep()
    {
        LifetimeScope scope = LifetimeScope.Find<GameLifetimeScope>();
        if (scope == null || scope.Container == null || !scope.Container.TryResolve(out MB13.Aipocalypse.Core.SleepSystem.SleepService sleep))
            return "no SleepService";
        var bed = UnityEngine.Object.FindAnyObjectByType<MB13.Aipocalypse.Core.SleepSystem.Bed>();
        IPlayer player = scope.Container.Resolve<PlayerRegistry>().Current;
        if (bed == null || player == null)
            return "no bed or player";
        if (!sleep.Begin(bed, player))
            return "refused: " + sleep.Refusal;
        sleep.Confirm();
        return $"sleeping until {sleep.Wake} (stage {sleep.Stage})";
    }

    static string State(IObjectResolver container)
    {
        var log = new StringBuilder();
        if (container.TryResolve(out WorldClock clock))
            log.AppendLine($"clock {clock.Now} ({clock.HoursPerSecond:0.####} h/s)");
        IPlayer player = container.Resolve<PlayerRegistry>().Current;
        if (player == null)
            return log.Append("no player").ToString();
        log.AppendLine($"health {player.Health.Current:0.#}/{player.Health.Max:0.#}, stamina {player.Stamina.Current:0.#}/{player.Stamina.Max:0.#}");
        if (player.Needs == null)
            return log.Append("needs off").ToString();
        log.AppendLine($"hunger {player.Needs.Hunger.Current:0.##}/{player.Needs.Hunger.Max:0} ({player.Needs.Hunger.Severity}), " +
            $"thirst {player.Needs.Thirst.Current:0.##}/{player.Needs.Thirst.Max:0} ({player.Needs.Thirst.Severity}), " +
            $"sanity {player.Needs.Sanity.Current:0.##}/{player.Needs.Sanity.Max:0} ({player.Needs.Sanity.Severity}), " +
            $"awake {player.Needs.HoursAwake:0.##} h, frozen {player.Needs.Frozen}");
        if (container.TryResolve(out NeedsTicker ticker))
        {
            SanityInput input = ticker.Exposure(player);
            float rate = SanityExposure.RatePerHour(input, player.Needs.HoursAwake, player.Needs.Settings.SanityRules, player.Needs.Sanity.Settings.BaseMax);
            log.AppendLine($"sanity exposure: darkness {input.Darkness:0.##}, outside light {input.DistanceOutsideLight:0.#} m, in light {input.InLight}, " +
                $"comfort {input.NearComfort} → {rate:+0.##;-0.##;0} / game h; player at {player.Transform.position}");
        }
        log.AppendLine($"load {player.Load.Weight:0.0} / {player.Load.Max:0.#} kg ({player.Load.State})");
        return log.ToString();
    }
}
