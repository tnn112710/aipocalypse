using System;
using System.Threading;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // Presents PlayerRespawnFlow: the eyes close, the death menu shows "Loading..." while the respawn is prepared (at least the
    // style's MinLoadingTime), "Click to continue" once it's ready, and fades away when the player is back.
    public sealed class DeathScreenHud : PlayerHudView
    {
        [SerializeField] DeathScreenView _view;

        IDisposable _subscriptions;
        bool _open;

        [Inject]
        void Construct(IAsyncSubscriber<PlayerRespawnFading> fading, IAsyncSubscriber<PlayerRespawning> respawning,
            ISubscriber<RespawnStageChanged> stageChanged)
        {
            _subscriptions = DisposableBag.Create(
                fading.Subscribe(OnFadingAsync),
                respawning.Subscribe(OnRespawningAsync),
                stageChanged.Subscribe(OnStageChanged));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _subscriptions?.Dispose();
        }

        protected override void Unbind(IPlayer player) => Close(fade: false);

        async UniTask OnFadingAsync(PlayerRespawnFading _, CancellationToken cancellation)
        {
            if (Player == null)
                return;
            _open = true;
            Tween eyes = _view.CloseEyes();
            await UniTask.WaitWhile(() => eyes.IsActive() && eyes.IsPlaying(), cancellationToken: cancellation);
            if (!_open || this == null)
                return;
            _view.SetStatus(_view.Style.LoadingText, pulse: false);
            _view.ShowMenu();
        }

        UniTask OnRespawningAsync(PlayerRespawning _, CancellationToken cancellation) => _open
            ? UniTask.Delay(TimeSpan.FromSeconds(_view.Style.MinLoadingTime), cancellationToken: cancellation)
            : UniTask.CompletedTask;

        void OnStageChanged(RespawnStageChanged change)
        {
            if (change.Stage == RespawnStage.Ready && _open)
                _view.SetStatus(_view.Style.ContinueText, pulse: true);
            else if (change.Stage == RespawnStage.None)
                Close(fade: true);
        }

        void Close(bool fade)
        {
            if (!_open)
                return;
            _open = false;
            if (fade)
                _view.HideMenu();
            else
                _view.HideAll();
        }
    }
}
