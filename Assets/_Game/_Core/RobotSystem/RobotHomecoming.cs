namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Watches the player go out of the yard and come back: true on the frame they step back in after at least `away`
    // seconds outside. The first sighting only sets where they are.
    public sealed class RobotHomecoming
    {
        bool _known;
        bool _inside;
        float _leftAt;

        public bool Update(bool inside, float now, float away)
        {
            if (!_known)
            {
                _known = true;
                _inside = inside;
                _leftAt = now;
                return false;
            }
            if (inside == _inside)
                return false;
            _inside = inside;
            if (!inside)
            {
                _leftAt = now;
                return false;
            }
            return now - _leftAt >= away;
        }

        // The player is gone (dead, unloaded): start over.
        public void Forget() => _known = false;
    }
}
