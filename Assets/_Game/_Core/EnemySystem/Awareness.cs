namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Whether an enemy knows the player is there: it spotted them (in range with a clear line, told by its brain each sense),
    // was hit, or was alarmed by a neighbour. It forgets after LoseSightTime without a sight.
    public sealed class Awareness
    {
        float _lastSight;

        public bool Alerted { get; private set; }

        public void Reset() => Alerted = false;

        // A hit or an alarm: it knows, and keeps looking for at least LoseSightTime. True when it didn't know before.
        public bool Notice(float now)
        {
            _lastSight = now;
            if (Alerted)
                return false;
            Alerted = true;
            return true;
        }

        // One sense: `sees` = the player is in range with a clear line. Returns -1 when it just lost them, 1 when it just
        // noticed them, 0 otherwise.
        public int Sense(bool sees, float now, float loseSightTime)
        {
            if (sees)
                return Notice(now) ? 1 : 0;
            if (!Alerted || now - _lastSight < loseSightTime)
                return 0;
            Alerted = false;
            return -1;
        }
    }
}
