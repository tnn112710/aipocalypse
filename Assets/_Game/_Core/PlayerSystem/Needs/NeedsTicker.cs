using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.SleepSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Scene service: advances the active player's needs by the game time that passes (the world clock's speed, a sleep skip
    // included; jumps don't count), telling sanity where the player is (darkness, the nearest safe light, comfort nearby).
    // Resets the hours awake when a sleep ends; PlayerComforted adds sanity. Without a WorldClock nothing drains.
    public sealed class NeedsTicker : ITickable, IDisposable
    {
        readonly PlayerRegistry _players;
        readonly IReadOnlyList<SafeLight> _lights;
        readonly ComfortSources _comforts;
        readonly WorldClock _clock;
        readonly SleepService _sleep;
        readonly IDisposable _subscription;
        SleepStage _lastStage;

        public NeedsTicker(PlayerRegistry players, IReadOnlyList<SafeLight> lights, ComfortSources comforts, ISubscriber<PlayerComforted> comforted,
            IObjectResolver resolver)
        {
            _players = players;
            _lights = lights;
            _comforts = comforts;
            resolver.TryResolve(out _clock);
            resolver.TryResolve(out _sleep);
            _subscription = comforted.Subscribe(OnComforted);
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            IPlayer player = _players.Current;
            PlayerNeeds needs = player?.Needs;
            SleepStage stage = _sleep != null ? _sleep.Stage : SleepStage.None;
            if (stage == SleepStage.Waking && _lastStage != SleepStage.Waking)
                needs?.OnWoke();
            _lastStage = stage;
            if (needs == null || player.IsDead || _clock == null || deltaTime <= 0f)
                return;
            needs.Advance(_clock.HoursPerSecond * deltaTime, stage == SleepStage.Asleep, Exposure(player));
        }

        public SanityInput Exposure(IPlayer player)
        {
            Vector3 position = player.Transform.position;
            float outside = SafeLight.DistanceOutside(position, _lights);
            return new SanityInput(player.Darkness, outside, outside <= 0f, _comforts.Near(position));
        }

        void OnComforted(PlayerComforted comfort) => _players.Current?.Needs?.Comfort(comfort.Sanity);

        public void Dispose() => _subscription.Dispose();
    }
}
