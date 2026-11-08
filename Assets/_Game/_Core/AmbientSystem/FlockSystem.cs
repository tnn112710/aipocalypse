using System.Collections.Generic;
using MB13.Aipocalypse.Core.PlayerSystem;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.AmbientSystem
{
    // Moves every spawned flock and school in one central tick, on scaled time (paused = frozen); members scatter from the
    // player.
    public sealed class FlockSystem : ITickable
    {
        static readonly ProfilerMarker SimulateMarker = new("Aipocalypse.FlockSystem.Simulate");

        readonly PlayerRegistry _players;
        readonly List<Flock> _flocks = new();
        float _time;

        public FlockSystem(PlayerRegistry players) => _players = players;

        public int Count => _flocks.Count;

        internal void Add(Flock flock)
        {
            if (!_flocks.Contains(flock))
                _flocks.Add(flock);
        }

        internal void Remove(Flock flock) => _flocks.Remove(flock);

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || _flocks.Count == 0)
                return;
            using var _ = SimulateMarker.Auto();
            _time += deltaTime;
            IPlayer player = _players.Current;
            Vector3 threat = player != null ? player.Transform.position : default;
            for (int i = _flocks.Count - 1; i >= 0; i--)
            {
                Flock flock = _flocks[i];
                if (flock == null)
                {
                    _flocks.RemoveAt(i);
                    continue;
                }
                flock.Simulate(deltaTime, threat, player != null, _time);
            }
        }
    }
}
