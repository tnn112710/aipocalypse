namespace Aipocalypse.Core.SpawnSystem
{
    public enum SpawnState
    {
        // In its pool (or not spawned yet).
        Pooled,
        // Woken once off-stage while prewarming, parked next frame.
        Warming,
        // In play and counted as alive.
        Active,
        // Withdrawn: still acting until it despawns out of sight, no longer counted.
        Leaving,
        // Dead or used up: lies in the world until it disables itself, no longer counted.
        Retired,
    }
}
