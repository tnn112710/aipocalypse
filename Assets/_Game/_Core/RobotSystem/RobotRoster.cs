using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Scene service: the robots in play (they add themselves while active), the yard, and what RobotManager last saw of the
    // world (night, a raid, the player) for their behaviour tasks.
    public sealed class RobotRoster
    {
        readonly List<Robot> _robots = new();

        public RobotRoster(RobotSettings settings) => Settings = settings;

        public RobotSettings Settings { get; }
        public IReadOnlyList<Robot> Robots => _robots;
        public RobotYard Yard { get; private set; }
        public bool Enabled => Settings != null && Settings.Enabled;
        public bool Night { get; set; }
        // A raid is on: robots hide.
        public bool Raid { get; set; }
        // Counts raids, so a robot is brave once per raid.
        public int RaidNumber { get; set; }
        public bool HasPlayer { get; set; }
        // The player's feet.
        public Vector3 PlayerPosition { get; set; }

        public void Bind(RobotYard yard) => Yard = yard;

        public void Add(Robot robot)
        {
            if (!_robots.Contains(robot))
                _robots.Add(robot);
        }

        public void Remove(Robot robot) => _robots.Remove(robot);
    }
}
