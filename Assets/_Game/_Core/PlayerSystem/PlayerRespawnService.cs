using System.Threading;
using Aipocalypse.Core.Events;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Aipocalypse.Core.PlayerSystem
{
    // Brings a dead player back, in two steps the death screen drives: Prepare while "Loading…" shows (the body goes back to
    // its spawn point, then every PlayerRespawning subscriber finishes its reset), Continue when the player clicks (revive).
    public sealed class PlayerRespawnService
    {
        readonly IAsyncPublisher<PlayerRespawning> _respawning;

        public PlayerRespawnService(IAsyncPublisher<PlayerRespawning> respawning) => _respawning = respawning;

        public async UniTask PrepareAsync(Player player, CancellationToken cancellation)
        {
            player.MoveToSpawn();
            await _respawning.PublishAsync(new PlayerRespawning(player.SpawnPosition), cancellation);
        }

        public void Continue(Player player) => player.Revive();
    }
}
