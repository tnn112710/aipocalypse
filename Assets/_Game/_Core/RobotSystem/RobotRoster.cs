using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Scene service: the robots in play (they add themselves while active), the yard, and what RobotManager last saw of the
    // world (night, the player) for their behaviour tasks.
    public sealed class RobotRoster
    {
        readonly List<Robot> _robots = new();

        public RobotRoster(RobotSettings settings) => Settings = settings;

        public RobotSettings Settings { get; }
        public IReadOnlyList<Robot> Robots => _robots;
        public RobotYard Yard { get; private set; }
        public bool Enabled => Settings != null && Settings.Enabled;
        public bool Night { get; set; }
        public bool HasPlayer { get; set; }
        // The player's feet and eyes.
        public Vector3 PlayerPosition { get; set; }
        public Vector3 PlayerEyes { get; set; }

        public void Bind(RobotYard yard) => Yard = yard;

        public void Add(Robot robot)
        {
            if (!_robots.Contains(robot))
                _robots.Add(robot);
        }

        public void Remove(Robot robot) => _robots.Remove(robot);
    }
}
