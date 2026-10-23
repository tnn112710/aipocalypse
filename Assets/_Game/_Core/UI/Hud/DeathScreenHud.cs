using System;
using System.Threading;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Input;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // When the active player's death sequence ends: eyes close, the death menu shows "Loading..." while the respawn is
    // prepared, then "Click to continue" revives. Any revive (continue, a cheat) puts the screen away.
    public sealed class DeathScreenHud : PlayerHudView
    {
        [SerializeField] DeathScreenView _view;

        InputService _input;
        PlayerRespawnService _respawn;
        IDisposable _subscriptions;
        CancellationTokenSource _flow;
        bool _menuInput;

        [Inject]
        void Construct(InputService input, PlayerRespawnService respawn, ISubscriber<PlayerDeathSequenceEnded> deathEnded,
            ISubscriber<PlayerRevived> revived)
        {
            _input = input;
            _respawn = respawn;
            _subscriptions = DisposableBag.Create(deathEnded.Subscribe(OnDeathSequenceEnded), revived.Subscribe(OnRevived));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _subscriptions?.Dispose();
            Cancel();
        }

        protected override void Unbind(Player player) => Close(fade: false);

        void OnDeathSequenceEnded(PlayerDeathSequenceEnded _)
        {
            if (Player == null || !Player.IsDead || _flow != null)
                return;
            _flow = new CancellationTokenSource();
            RunAsync(Player, _flow.Token).Forget();
        }

        void OnRevived(PlayerRevived _) => Close(fade: true);

        async UniTaskVoid RunAsync(Player player, CancellationToken cancellation)
        {
            DeathScreenStyle style = _view.Style;
            Tween eyes = _view.CloseEyes();
            await UniTask.WaitWhile(() => eyes.IsActive() && eyes.IsPlaying(), cancellationToken: cancellation);

            _view.SetStatus(style.LoadingText, pulse: false);
            _view.ShowMenu();
            _input.EnableUI();
            _menuInput = true;
            await UniTask.WhenAll(
                _respawn.PrepareAsync(player, cancellation),
                UniTask.Delay(TimeSpan.FromSeconds(style.MinLoadingTime), cancellationToken: cancellation));

            _view.SetStatus(style.ContinueText, pulse: true);
            await UniTask.WaitUntil(() => _input.UiConfirmPressed, cancellationToken: cancellation);
            // Revive publishes PlayerRevived, which closes the screen.
            _respawn.Continue(player);
        }

        void Close(bool fade)
        {
            bool open = _flow != null;
            Cancel();
            if (_menuInput)
            {
                _input.EnableGameplay();
                _menuInput = false;
            }
            if (!open)
                return;
            if (fade)
                _view.HideMenu();
            else
                _view.HideAll();
        }

        void Cancel()
        {
            if (_flow == null)
                return;
            _flow.Cancel();
            _flow.Dispose();
            _flow = null;
        }
    }
}
