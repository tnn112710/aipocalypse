using System;
using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    // Root service: one master seed, one named RandomStream per system (Stream("loot"), Stream("raids")), so the number
    // of rolls in one system never changes another's. Same seed → same rolls. Visual noise can stay on UnityEngine.Random.
    public sealed class RandomService
    {
        readonly Dictionary<string, RandomStream> _streams = new();

        public ulong Seed { get; private set; }

        public RandomService() : this((ulong)DateTime.UtcNow.Ticks) { }

        public RandomService(ulong seed) => Seed = seed;

        // The stream called `name`; the same object every call, so cache it.
        public RandomStream Stream(string name)
        {
            if (_streams.TryGetValue(name, out RandomStream stream))
                return stream;
            stream = new RandomStream(StreamSeed(Seed, name));
            _streams.Add(name, stream);
            return stream;
        }

        // Restarts every stream from `seed` (loading a save, reproducing a run). Cached streams stay valid.
        public void Reseed(ulong seed)
        {
            Seed = seed;
            foreach (KeyValuePair<string, RandomStream> pair in _streams)
                pair.Value.Reset(StreamSeed(seed, pair.Key));
        }

        // Every stream's position, by name (saving).
        public Dictionary<string, ulong> CaptureStreams()
        {
            var states = new Dictionary<string, ulong>(_streams.Count);
            foreach (KeyValuePair<string, RandomStream> pair in _streams)
                states[pair.Key] = pair.Value.State;
            return states;
        }

        // Back to a saved seed and stream positions (loading); a stream the save doesn't list restarts from the seed.
        public void Restore(ulong seed, IReadOnlyDictionary<string, ulong> states)
        {
            Reseed(seed);
            if (states == null)
                return;
            foreach (KeyValuePair<string, ulong> pair in states)
                Stream(pair.Key).Reset(pair.Value);
        }

        // FNV-1a of the name mixed with the seed: string.GetHashCode isn't stable across runtimes.
        static ulong StreamSeed(ulong seed, string name)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in name)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return seed ^ hash;
        }
    }
}
