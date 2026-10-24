using System;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // On the HUD root: fades the whole HUD out when the active player starts dying and back in when it revives. The death
    // screen ignores this group (its own CanvasGroup has ignoreParentGroups), so it stays visible.
    public sealed class HudVisibilityHud : PlayerHudView
    {
        [SerializeField] CanvasGroup _group;
        [SerializeField, Min(0f)] float _fadeOut = 0.35f;
        [SerializeField, Min(0f)] float _fadeIn = 0.5f;

        IDisposable _subscriptions;
        Tween _fade;

        [Inject]
        void Construct(ISubscriber<PlayerDied> died, ISubscriber<PlayerRevived> revived) =>
            _subscriptions = DisposableBag.Create(died.Subscribe(_ => Refresh()), revived.Subscribe(_ => Refresh()));

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _subscriptions?.Dispose();
            _fade?.Kill();
        }

        protected override void Bind(IPlayer player) => Show(!player.IsDead, instant: true);
        protected override void Unbind(IPlayer player) => Show(true, instant: true);

        void Refresh()
        {
            if (Player != null)
                Show(!Player.IsDead, instant: false);
        }

        void Show(bool visible, bool instant)
        {
            _fade?.Kill();
            float alpha = visible ? 1f : 0f;
            if (instant)
                _group.alpha = alpha;
            else
                _fade = _group.DOFade(alpha, visible ? _fadeIn : _fadeOut).SetLink(gameObject);
        }
    }
}
