using Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Root service: the player currently in play, for things that live outside the Player prefab (HUD, later enemies).
    // Players register themselves; listeners react to ActivePlayerChanged and read Current.
    public sealed class PlayerRegistry
    {
        readonly IPublisher<ActivePlayerChanged> _changed;

        public PlayerRegistry(IPublisher<ActivePlayerChanged> changed) => _changed = changed;

        public Player Current { get; private set; }

        public void Register(Player player)
        {
            if (ReferenceEquals(Current, player))
                return;
            if (Current != null)
                Debug.LogWarning($"PlayerRegistry: {player.name} replaces {Current.name} as the active player.", player);

            Current = player;
            _changed.Publish(default);
        }

        public void Unregister(Player player)
        {
            if (!ReferenceEquals(Current, player))
                return;

            Current = null;
            _changed.Publish(default);
        }
    }
}
