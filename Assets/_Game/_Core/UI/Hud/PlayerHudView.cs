using System;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // Base for HUD presenters that show the active player. Bind runs when a player appears, Unbind when it leaves or is replaced,
    // so the HUD survives death, respawn and scenes without a player.
    public abstract class PlayerHudView : MonoBehaviour
    {
        PlayerRegistry _players;
        IDisposable _subscription;
        Player _bound;

        protected Player Player => _bound;

        [Inject]
        void ConstructPlayerHud(PlayerRegistry players, ISubscriber<ActivePlayerChanged> playerChanged)
        {
            _players = players;
            _subscription = playerChanged.Subscribe(OnPlayerChanged);
        }

        // Start, not injection: every scope in the scene has built by now, so a player already in the scene is registered.
        protected virtual void Start() => Rebind();

        protected virtual void OnDestroy()
        {
            _subscription?.Dispose();
            if (_bound is not null)
                Unbind(_bound);
            _bound = null;
        }

        protected virtual void Bind(Player player) { }
        protected virtual void Unbind(Player player) { }

        void OnPlayerChanged(ActivePlayerChanged _) => Rebind();

        void Rebind()
        {
            Player next = _players?.Current;
            if (ReferenceEquals(next, _bound))
                return;

            if (_bound is not null)
                Unbind(_bound);
            _bound = next;
            if (next != null)
                Bind(next);
        }
    }
}
