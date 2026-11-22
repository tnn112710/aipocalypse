namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Watches the player go out of the yard and come back: Left on the frame they step out, Back on the frame they step
    // back in after at least `away` seconds outside. The first sighting only sets where they are.
    public sealed class RobotHomecoming
    {
        bool _known;
        bool _inside;
        float _leftAt;

        public YardCrossing Update(bool inside, float now, float away)
        {
            if (!_known)
            {
                _known = true;
                _inside = inside;
                _leftAt = now;
                return YardCrossing.None;
            }
            if (inside == _inside)
                return YardCrossing.None;
            _inside = inside;
            if (!inside)
            {
                _leftAt = now;
                return YardCrossing.Left;
            }
            return now - _leftAt >= away ? YardCrossing.Back : YardCrossing.None;
        }

        // The player is gone (dead, unloaded): start over.
        public void Forget() => _known = false;
    }
}
