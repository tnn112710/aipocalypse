using System.Text;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Console commands on the scene's service robots (editor and cheat builds); only in scenes with a RobotInstaller.
    [CommandGroup("robot")]
    public sealed class RobotCommands : ConsoleCommands
    {
        readonly RobotRoster _roster;
        readonly PlayerRegistry _players;

        public RobotCommands(CommandRegistry registry, RobotRoster roster, PlayerRegistry players) : base(registry)
        {
            _roster = roster;
            _players = players;
        }

        [Command("Each robot: where it is, what it's doing, its mood")]
        string Status()
        {
            if (_roster.Robots.Count == 0)
                return "No robots.";
            var text = new StringBuilder();
            foreach (Robot robot in _roster.Robots)
            {
                string doing = robot.Hit ? "flinching" : robot.Call.Active ? (robot.Call.Arrived ? "waiting by you" : "coming") :
                    robot.Greeting ? "greeting" : robot.Nervous ? "nervous" : robot.Asleep ? "standby" : "home";
                float fromSpot = robot.Spot != null ? UnityEngine.Vector3.Distance(robot.transform.position, robot.Spot.Center) : 0f;
                text.AppendLine($"{Name(robot)}: {doing}, {fromSpot:0.0} m from its spot, mood {robot.Face?.Mood}");
            }
            return text.ToString().TrimEnd();
        }

        [Command("Call a robot to you as if whistled (its name, or the first one)")]
        string Call(string name = "")
        {
            IPlayer player = _players.Current;
            if (player == null)
                return "No player.";
            foreach (Robot robot in _roster.Robots)
                if (string.IsNullOrEmpty(name) || Name(robot).Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return robot.TryWhistle(player.Transform.gameObject) ? Name(robot) + " is coming." : Name(robot) + " can't reach you.";
            return "No robot called " + name + ".";
        }

        [Command("Raid reactions (pass 2)")]
        string Raid() => "Not built yet: comes with the robots' pass 2.";

        [Command("Welcome home reaction (pass 2)")]
        string Welcome() => "Not built yet: comes with the robots' pass 2.";

        static string Name(Robot robot) => robot.Service != null ? robot.Service.DisplayName.Text() : robot.name;
    }
}
