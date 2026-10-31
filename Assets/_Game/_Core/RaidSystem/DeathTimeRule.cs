using System;
using System.Threading;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.WorldTimeSystem;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer.Unity;

namespace Aipocalypse.Core.RaidSystem
{
    // A death costs time, applied behind the death screen (PlayerRespawning): during a raid the raid is lost and the player
    // wakes at the next DefeatWakeHour; any other death moves the clock on HoursLostOnDeath. TimeJumped lets the world resync.
    public sealed class DeathTimeRule : IStartable, IDisposable
    {
        const double Epsilon = 1e-9;

        readonly IAsyncSubscriber<PlayerRespawning> _respawning;
        readonly RaidDirector _director;
        readonly WorldClock _clock;
        readonly RaidSettings _settings;
        IDisposable _subscription;

        public DeathTimeRule(IAsyncSubscriber<PlayerRespawning> respawning, RaidDirector director, WorldClock clock, RaidSettings settings)
        {
            _respawning = respawning;
            _director = director;
            _clock = clock;
            _settings = settings;
        }

        public void Start() => _subscription = _respawning.Subscribe(OnRespawningAsync);

        public void Dispose() => _subscription?.Dispose();

        // When the player is back. A raid death goes to the next time the clock shows DefeatWakeHour, exactly as
        // WorldClock.JumpTo does (built from the day start in double; now itself if it shows that hour).
        public static WorldTime WakeTime(WorldTime now, bool raidRunning, RaidSettings settings)
        {
            if (!raidRunning)
                return now.AddHours(settings.HoursLostOnDeath);
            double target = Math.Floor(now.TotalHours / WorldTime.HoursPerDay) * WorldTime.HoursPerDay + settings.DefeatWakeHour;
            if (target < now.TotalHours - Epsilon)
                target += WorldTime.HoursPerDay;
            return new WorldTime(Math.Max(target, now.TotalHours));
        }

        UniTask OnRespawningAsync(PlayerRespawning _, CancellationToken cancellation)
        {
            bool raidRunning = _director.IsRunning;
            if (raidRunning)
                _director.EndRaid(RaidEndReason.Defeat);
            _clock.SetTime(WakeTime(_clock.Now, raidRunning, _settings));
            return UniTask.CompletedTask;
        }
    }
}
