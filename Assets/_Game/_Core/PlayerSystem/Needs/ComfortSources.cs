using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Something whose company slows the darkness drain on sanity (Loki; other companions later).
    public interface IComfortSource
    {
        Vector3 Position { get; }
        float Radius { get; }
        bool IsActive { get; }
    }

    // Scene service: the comfort sources present (registered by their own systems, e.g. the dog manager). NeedsTicker asks
    // whether the player is near one.
    public sealed class ComfortSources
    {
        readonly List<IComfortSource> _sources = new();

        public void Add(IComfortSource source)
        {
            if (source != null && !_sources.Contains(source))
                _sources.Add(source);
        }

        public void Remove(IComfortSource source) => _sources.Remove(source);

        // Flat distance, inside an active source's radius.
        public bool Near(Vector3 position)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                IComfortSource source = _sources[i];
                if (!source.IsActive)
                    continue;
                Vector3 offset = position - source.Position;
                if (offset.x * offset.x + offset.z * offset.z <= source.Radius * source.Radius)
                    return true;
            }
            return false;
        }
    }
}
