namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Plays the player instead of the devices while set on IPlayer.IntentSource (dev autopilot, tests): Player reads it in place
    // of InputService, only while it can act, and runs the intent as usual.
    public interface IPlayerIntentSource
    {
        // This frame's intent. LookDegrees turn now (x = right, y = up), not scaled by the zoom; `deltaTime` is scaled.
        PlayerIntent Read(float deltaTime);
    }
}
