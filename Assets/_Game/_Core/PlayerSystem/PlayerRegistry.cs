using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Root service: the player currently in play, for things that live outside the Player prefab (HUD, later enemies).
    // Players register themselves; listeners react to ActivePlayerChanged and read Current.
    public sealed class PlayerRegistry
    {
        readonly IPublisher<ActivePlayerChanged> _changed;

        public PlayerRegistry(IPublisher<ActivePlayerChanged> changed) => _changed = changed;

        public IPlayer Current { get; private set; }

        public void Register(IPlayer player)
        {
            if (ReferenceEquals(Current, player))
                return;
            if (Current != null)
                Debug.LogWarning($"PlayerRegistry: {player.Transform.name} replaces {Current.Transform.name} as the active player.", player.Transform);

            Current = player;
            _changed.Publish(default);
        }

        public void Unregister(IPlayer player)
        {
            if (!ReferenceEquals(Current, player))
                return;

            Current = null;
            _changed.Publish(default);
        }
    }
}
