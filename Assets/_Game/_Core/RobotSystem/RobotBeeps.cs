using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Keeps the robots' beeps from piling up: one robot beeps at most once per robotGap, and all of them together at most
    // once per sharedGap.
    public sealed class RobotBeeps
    {
        readonly float _robotGap;
        readonly float _sharedGap;
        readonly Dictionary<int, float> _lastByRobot = new();
        float _lastAny = float.NegativeInfinity;

        public RobotBeeps(float robotGap, float sharedGap)
        {
            _robotGap = robotGap;
            _sharedGap = sharedGap;
        }

        public bool TryPlay(int robot, float now)
        {
            if (now - _lastAny < _sharedGap)
                return false;
            if (_lastByRobot.TryGetValue(robot, out float last) && now - last < _robotGap)
                return false;
            _lastByRobot[robot] = now;
            _lastAny = now;
            return true;
        }
    }
}
