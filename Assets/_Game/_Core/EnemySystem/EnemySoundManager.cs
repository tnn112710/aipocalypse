using Aipocalypse.Core.PlayerSystem;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Footsteps of the registered enemies, once a frame on scaled time: each walking enemy within its kind's FootstepRange of
    // the player steps per stride, all of them together at most MaxFootstepsPerSecond (the nearest crowd still sounds busy).
    public sealed class EnemySoundManager : ITickable
    {
        static readonly ProfilerMarker Steps = new("Aipocalypse.EnemySoundManager.Steps");
        readonly EnemyRegistry _enemies;
        readonly EnemySystemSettings _settings;
        readonly PlayerRegistry _players;
        float _allowance;

        public EnemySoundManager(EnemyRegistry enemies, EnemySystemSettings settings, PlayerRegistry players)
        {
            _enemies = enemies;
            _settings = settings;
            _players = players;
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            IPlayer player = _players.Current;
            Vector3 listener = player != null ? player.Transform.position : default;
            _allowance = Mathf.Min(_allowance + _settings.MaxFootstepsPerSecond * deltaTime, _settings.MaxFootstepsPerSecond);
            using (Steps.Auto())
            {
                for (int i = 0; i < _enemies.Count; i++)
                {
                    EnemyVoice voice = _enemies[i].Voice;
                    EnemySoundSet sounds = voice?.Sounds;
                    if (sounds == null)
                        continue;
                    float range = sounds.FootstepRange;
                    bool audible = player != null && MeleeHit.SqrFlatDistance(_enemies[i].transform.position, listener) <= range * range;
                    if (!voice.Walk(audible) || _allowance < 1f)
                        continue;
                    _allowance -= 1f;
                    voice.Step();
                }
            }
        }
    }
}
