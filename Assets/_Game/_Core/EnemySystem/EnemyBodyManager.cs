using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Advances the enemy bodies that are being knocked back or lying dead, on scaled time (pause freezes them). Bodies join
    // when hit and leave when they come to rest or disable themselves; idle enemies cost nothing here.
    public sealed class EnemyBodyManager : ITickable
    {
        static readonly ProfilerMarker React = new("Aipocalypse.EnemyBodyManager.React");
        readonly List<EnemyBody> _active = new(64);

        public int Count => _active.Count;

        public void Track(EnemyBody body)
        {
            if (body.Tracked)
                return;
            body.Tracked = true;
            _active.Add(body);
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            using (React.Auto())
            {
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    EnemyBody body = _active[i];
                    if (body != null && body.Advance(deltaTime))
                        continue;
                    if (body != null)
                        body.Tracked = false;
                    int last = _active.Count - 1;
                    _active[i] = _active[last];
                    _active.RemoveAt(last);
                }
            }
        }
    }
}
