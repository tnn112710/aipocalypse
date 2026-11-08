using System.Collections.Generic;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // Console commands on the scene's fences and gates (editor and development builds); only in scenes with a DefenseInstaller.
    [CommandGroup("fence")]
    public sealed class DefenseCommands : ConsoleCommands
    {
        readonly BarrierRegistry _barriers;
        readonly PlayerRegistry _players;
        readonly List<BarrierSegment> _sorted = new();

        public DefenseCommands(CommandRegistry registry, BarrierRegistry barriers, PlayerRegistry players) : base(registry)
        {
            _barriers = barriers;
            _players = players;
        }

        [Command("Segments and gates: how many, how many broken, the lowest health share")]
        string Status()
        {
            int broken = 0;
            float lowest = 1f;
            foreach (BarrierSegment segment in _barriers.Segments)
            {
                if (segment.IsBroken)
                    broken++;
                lowest = Mathf.Min(lowest, segment.HealthShare);
            }
            return $"{_barriers.Segments.Count} segments, {broken} broken, lowest {lowest:P0}";
        }

        [Command("Damage the `count` segments nearest the player by `amount` (not friendly fire: counts as the world's)")]
        CommandResult Damage(float amount, int count = 1)
        {
            if (!SortByDistance())
                return CommandResult.Fail("No player or no barriers in this scene");
            int hit = 0;
            for (int i = 0; i < _sorted.Count && hit < count; i++)
            {
                if (_sorted[i].IsBroken)
                    continue;
                _sorted[i].DealDamage(amount);
                hit++;
            }
            return CommandResult.Ok($"{hit} damaged by {amount}");
        }

        [Command("Break the `count` standing segments nearest the player")]
        CommandResult Breach(int count = 1) => Damage(float.MaxValue, count);

        [Command("Mend every segment and gate to full and stand the broken ones up")]
        CommandResult Repair()
        {
            foreach (BarrierSegment segment in _barriers.Segments)
                segment.Restore();
            return CommandResult.Ok($"{_barriers.Segments.Count} restored");
        }

        [Command("Upgrade every segment and gate one tier (BarrierDefinition.UpgradesTo)")]
        CommandResult Upgrade()
        {
            int upgraded = 0;
            foreach (BarrierSegment segment in _barriers.Segments)
                if (segment.Upgrade())
                    upgraded++;
            return upgraded > 0 ? CommandResult.Ok($"{upgraded} upgraded") : CommandResult.Fail("Nothing has a next tier");
        }

        [Command("Open or close every gate (closing skips a gate with someone in the doorway)")]
        CommandResult Gate(bool open)
        {
            int changed = 0;
            foreach (BarrierSegment segment in _barriers.Segments)
            {
                FenceGate gate = segment.Gate;
                if (gate == null || segment.IsBroken || gate.IsOpen == open)
                    continue;
                gate.Toggle(segment.transform.position + segment.transform.forward);
                if (gate.IsOpen == open)
                    changed++;
            }
            return CommandResult.Ok($"{changed} gate(s) {(open ? "opened" : "closed")}");
        }

        bool SortByDistance()
        {
            if (_players.Current is not { } player || _barriers.Segments.Count == 0)
                return false;
            Vector3 from = player.Transform.position;
            _sorted.Clear();
            _sorted.AddRange(_barriers.Segments);
            _sorted.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            return true;
        }
    }
}
