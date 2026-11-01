namespace Aipocalypse.Core.SpawnSystem
{
    // A part of a spawnable prefab that resets per-life state when its Spawnable is spawned from or returned to its pool: a
    // pooled object never sees Awake or OnDestroy again.
    public interface ISpawnable
    {
        // After the instance is placed and active.
        void OnSpawned(in SpawnInfo info);

        // When it goes back to its pool (already inactive).
        void OnDespawned();
    }
}
