namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A reaction that mustn't repeat too soon (a wave, a nervous look).
    public struct RobotCooldown
    {
        float _readyAt;

        public bool Ready(float now) => now >= _readyAt;
        public void Use(float now, float seconds) => _readyAt = now + seconds;
    }
}
