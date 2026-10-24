using System;
using System.Threading;
using Aipocalypse.Core.Events;
using Aipocalypse.Input;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer.Unity;

namespace Aipocalypse.Core.PlayerSystem
{
    // Root service bringing the active player back after its death sequence: Fading → Preparing (body to spawn, PlayerRespawning
    // subscribers reset) → Ready (waits for confirm on the UI map) → revive. Works without a HUD; any revive ends it early.
    public sealed class PlayerRespawnFlow : IStartable, IDisposable
    {
        readonly PlayerRegistry _registry;
        readonly InputService _input;
        readonly IAsyncPublisher<PlayerRespawnFading> _fading;
        readonly IAsyncPublisher<PlayerRespawning> _respawning;
        readonly IPublisher<RespawnStageChanged> _stageChanged;
        readonly ISubscriber<PlayerDeathSequenceEnded> _deathEnded;
        readonly ISubscriber<PlayerRevived> _revived;
        readonly ISubscriber<ActivePlayerChanged> _playerChanged;
        IDisposable _subscriptions;
        CancellationTokenSource _run;
        IPlayer _player;

        public PlayerRespawnFlow(PlayerRegistry registry, InputService input, IAsyncPublisher<PlayerRespawnFading> fading,
            IAsyncPublisher<PlayerRespawning> respawning, IPublisher<RespawnStageChanged> stageChanged,
            ISubscriber<PlayerDeathSequenceEnded> deathEnded, ISubscriber<PlayerRevived> revived, ISubscriber<ActivePlayerChanged> playerChanged)
        {
            _registry = registry;
            _input = input;
            _fading = fading;
            _respawning = respawning;
            _stageChanged = stageChanged;
            _deathEnded = deathEnded;
            _revived = revived;
            _playerChanged = playerChanged;
        }

        public RespawnStage Stage { get; private set; }

        public void Start() => _subscriptions = DisposableBag.Create(
            _deathEnded.Subscribe(OnDeathSequenceEnded),
            _revived.Subscribe(_ => End()),
            _playerChanged.Subscribe(OnPlayerChanged));

        public void Dispose()
        {
            _subscriptions?.Dispose();
            Cancel();
        }

        void OnDeathSequenceEnded(PlayerDeathSequenceEnded _)
        {
            IPlayer player = _registry.Current;
            if (Stage != RespawnStage.None || player == null || !player.IsDead)
                return;
            _player = player;
            _run = new CancellationTokenSource();
            RunAsync(player, _run.Token).Forget();
        }

        void OnPlayerChanged(ActivePlayerChanged _)
        {
            if (_player != null && !ReferenceEquals(_registry.Current, _player))
                End();
        }

        async UniTaskVoid RunAsync(IPlayer player, CancellationToken cancellation)
        {
            SetStage(RespawnStage.Fading);
            await _fading.PublishAsync(new PlayerRespawnFading(), cancellation);

            _input.RequestUI(this);
            SetStage(RespawnStage.Preparing);
            player.MoveToSpawn();
            await _respawning.PublishAsync(new PlayerRespawning(player.SpawnPosition), cancellation);

            SetStage(RespawnStage.Ready);
            await UniTask.WaitUntil(() => _input.UiConfirmPressed, cancellationToken: cancellation);
            // Revive publishes PlayerRevived, which ends the flow.
            player.Revive();
        }

        void End()
        {
            if (Stage == RespawnStage.None)
                return;
            Cancel();
            _player = null;
            _input.ReleaseUI(this);
            SetStage(RespawnStage.None);
        }

        void Cancel()
        {
            if (_run == null)
                return;
            _run.Cancel();
            _run.Dispose();
            _run = null;
        }

        void SetStage(RespawnStage stage)
        {
            Stage = stage;
            _stageChanged.Publish(new RespawnStageChanged(stage));
        }
    }
}
