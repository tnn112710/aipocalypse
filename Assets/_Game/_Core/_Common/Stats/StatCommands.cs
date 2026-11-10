using System.Text;
using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.Stats
{
    // Console commands on the stat modifier layer (editor and cheat builds), under the source id "console".
    [CommandGroup("stat")]
    public sealed class StatCommands : ConsoleCommands
    {
        const string Source = "console";

        readonly StatModifiers _stats;

        public StatCommands(CommandRegistry registry, StatModifiers stats) : base(registry) => _stats = stats;

        [Command("Every stat modifier in play: source, stat, add, multiply")]
        string List()
        {
            if (_stats.All.Count == 0)
                return "No modifiers";
            var text = new StringBuilder();
            foreach (StatModifier modifier in _stats.All)
                text.Append(modifier.Source).Append("  ").Append(modifier.Stat).Append("  +").Append(modifier.Add)
                    .Append("  x").Append(modifier.Multiply).Append('\n');
            return text.ToString().TrimEnd();
        }

        [Command("Set the console's modifier on a stat: stat.set <stat> <multiply> [add] (stat.set SprintSpeed 1.5)")]
        string Set(StatId stat, float multiply, float add = 0f)
        {
            _stats.Set(Source, stat, add, multiply);
            return $"{stat}: +{add} x{multiply}";
        }

        [Command("Remove the console's modifier on a stat")]
        string Remove(StatId stat) => _stats.Remove(Source, stat) ? $"{stat} removed" : $"No console modifier on {stat}";

        [Command("Remove every modifier the console set")]
        string Clear() => $"{_stats.Remove(Source)} removed";
    }
}
