using System.Text;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Console commands on the scene's robots (editor and cheat builds); only in scenes with a RobotInstaller.
    [CommandGroup("robot")]
    public sealed class RobotCommands : ConsoleCommands
    {
        readonly RobotRoster _roster;
        readonly RobotManager _manager;
        readonly PlayerRegistry _players;

        public RobotCommands(CommandRegistry registry, RobotRoster roster, RobotManager manager, PlayerRegistry players) : base(registry)
        {
            _roster = roster;
            _manager = manager;
            _players = players;
        }

        [Command("Each robot: where it is, what it's doing, its mood")]
        string Status()
        {
            if (_roster.Robots.Count == 0)
                return "No robots.";
            var text = new StringBuilder();
            float now = Time.time;
            foreach (Robot robot in _roster.Robots)
            {
                RobotState state = robot.State;
                string doing = state.Down(now) ? "knocked down" : state.Hit(now) ? "flinching" : _roster.Raid ? "hiding" :
                    robot.Call.Active ? (robot.Call.Arrived ? "waiting by you" : "coming") : state.Nervous(now) ? "nervous" :
                    state.Greeting(now) ? "greeting" : state.Cheering(now) ? state.Cheer.ToString().ToLowerInvariant() :
                    robot.Asleep ? "standby" : "home";
                float fromSpot = robot.Spot != null ? Vector3.Distance(robot.transform.position, robot.Spot.Center) : 0f;
                string sulk = state.Sulking(now) ? ", sulking" : "";
                text.AppendLine($"{robot.DisplayName}: {doing}{sulk}, {fromSpot:0.0} m from its spot, face {robot.Face?.Shown}");
            }
            return text.ToString().TrimEnd();
        }

        [Command("Call a robot to you as if whistled (its name, or the first one)")]
        string Call(string name = "")
        {
            IPlayer player = _players.Current;
            if (player == null)
                return "No player.";
            Robot robot = Find(name);
            if (robot == null)
                return "No robot called " + name + ".";
            return robot.TryWhistle(player.Transform.gameObject) ? robot.DisplayName + " answers." : robot.DisplayName + " can't hear you.";
        }

        [Command("Robots react to a raid: start (hide), win (cheer), lose (cry), stop")]
        string Raid(string what = "start")
        {
            switch (what)
            {
                case "start":
                    _manager.StartRaid();
                    return "Raid: robots hide.";
                case "win":
                    _manager.EndRaid(RaidEndReason.Cleared);
                    return "Raid won: robots cheer.";
                case "lose":
                    _manager.EndRaid(RaidEndReason.Defeat);
                    return "Raid lost: robots cry.";
                case "stop":
                    _manager.EndRaid(RaidEndReason.Stopped);
                    return "Raid stopped.";
                default:
                    return "start, win, lose or stop.";
            }
        }

        [Command("Robots welcome the player home as if back from a trip")]
        string Welcome()
        {
            _manager.WelcomeHome();
            return "Welcome home.";
        }

        [Command("Knock a robot over as if shot three times (its name, or the first one)")]
        string Knock(string name = "")
        {
            Robot robot = Find(name);
            if (robot == null)
                return "No robot called " + name + ".";
            var hit = new Combat.DamageInfo(1f, Combat.DamageType.Gunshot, robot.transform.position + robot.transform.forward + Vector3.up);
            for (int i = 0; i < _roster.Settings.KnockdownHits; i++)
                robot.TakeDamage(hit);
            return robot.DisplayName + " falls over.";
        }

        Robot Find(string name)
        {
            foreach (Robot robot in _roster.Robots)
                if (string.IsNullOrEmpty(name) || robot.DisplayName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return robot;
            return null;
        }
    }
}
