using UnityEngine;

namespace MB13.Aipocalypse.Core.Events
{
    // A player registered or unregistered (spawn, death and respawn, scene unload). Read PlayerRegistry.Current for the new one.
    public readonly struct ActivePlayerChanged
    {
    }

    // Async (IAsyncPublisher): the respawn begins (the death sequence has ended). Presenters fade the view out (the death
    // screen closes the eyes) and finish before the body is moved.
    public readonly struct PlayerRespawnFading
    {
    }

    // Async (IAsyncPublisher): the dead player has been moved to its spawn point behind the death screen. Systems that reset
    // for a respawn (loot grave, enemies…) subscribe and finish their work before the player is offered "continue".
    public readonly struct PlayerRespawning
    {
        public readonly Vector3 SpawnPosition;

        public PlayerRespawning(Vector3 spawnPosition) => SpawnPosition = spawnPosition;
    }

    public enum RespawnStage
    {
        None,
        Fading,
        Preparing,
        // Waiting for the player to confirm (a click on the death screen).
        Ready,
    }

    // PlayerRespawnFlow moved on. None: the respawn is over (revived, or the player left).
    public readonly struct RespawnStageChanged
    {
        public readonly RespawnStage Stage;

        public RespawnStageChanged(RespawnStage stage) => Stage = stage;
    }

    // The player is back on its feet (continue on the death screen, or a revive cheat).
    public readonly struct PlayerRevived
    {
        public readonly Vector3 Position;

        public PlayerRevived(Vector3 position) => Position = position;
    }

    // Something calmed the player (petting the dog; comfort food later): sanity points to add, and what did it (for logs).
    public readonly struct PlayerComforted
    {
        public readonly float Sanity;
        public readonly string Source;

        public PlayerComforted(float sanity, string source)
        {
            Sanity = sanity;
            Source = source;
        }
    }
}
