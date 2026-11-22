namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Counts things that happen close together (hits for a knockdown, whistles for annoyance): keeps the latest times.
    public sealed class RobotStreak
    {
        readonly float[] _times;
        int _count;

        public RobotStreak(int capacity) => _times = new float[capacity];

        // Adds one at `now`; returns how many of the kept ones (this one included) fall within `window` seconds.
        public int Add(float now, float window)
        {
            if (_count == _times.Length)
            {
                System.Array.Copy(_times, 1, _times, 0, _times.Length - 1);
                _count--;
            }
            _times[_count++] = now;
            int recent = 0;
            for (int i = 0; i < _count; i++)
                if (now - _times[i] <= window)
                    recent++;
            return recent;
        }

        public void Clear() => _count = 0;
    }
}
