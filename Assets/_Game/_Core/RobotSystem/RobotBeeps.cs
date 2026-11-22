using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Keeps the robots' beeps from piling up: one robot beeps at most once per robotGap, and all of them together at most
    // once per sharedGap.
    public sealed class RobotBeeps
    {
        readonly Dictionary<int, float> _lastByRobot = new();
        float _lastAny = float.NegativeInfinity;

        public bool TryPlay(int robot, float now, float robotGap, float sharedGap)
        {
            if (now - _lastAny < sharedGap)
                return false;
            if (_lastByRobot.TryGetValue(robot, out float last) && now - last < robotGap)
                return false;
            _lastByRobot[robot] = now;
            _lastAny = now;
            return true;
        }
    }
}
