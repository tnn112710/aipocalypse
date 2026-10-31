using System.Collections.Generic;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.EnemySystem;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.RaidSystem
{
    // The world side of raids: groups from the scene's SpawnZones with each card's arrival cue, and the raiders still alive.
    // Withdrawn raiders despawn at once, or each as soon as the player can't see it.
    public sealed class RaidSpawner : IRaidSpawner, ITickable
    {
        readonly EnemySpawner _spawner;
        readonly SpawnPointPicker _picker;
        readonly AudioService _audio;
        readonly IReadOnlyList<SpawnZone> _zones;
        readonly List<EnemyBrain> _raiders = new();
        readonly List<EnemyBrain> _leaving = new();
        readonly List<EnemyBrain> _group = new();
        readonly Dictionary<EnemySpawnCard, int> _planned = new();
        readonly HashSet<string> _unknownZones = new();

        public RaidSpawner(EnemySpawner spawner, SpawnPointPicker picker, AudioService audio, IReadOnlyList<SpawnZone> zones)
        {
            _spawner = spawner;
            _picker = picker;
            _audio = audio;
            _zones = zones;
        }

        public int Alive
        {
            get
            {
                Prune(_raiders);
                return _raiders.Count;
            }
        }

        public void Prepare(RaidPlan plan, int aliveCap)
        {
            _picker.ResetWaits();
            if (plan == null)
                return;
            _planned.Clear();
            foreach (RaidWave wave in plan.Waves)
                foreach (RaidWaveEntry entry in wave.Entries)
                    if (entry.Card != null && entry.Count > 0)
                        _planned[entry.Card] = (_planned.TryGetValue(entry.Card, out int sum) ? sum : 0) + entry.Count;
            foreach ((EnemySpawnCard card, int count) in _planned)
                _spawner.Prewarm(card, Mathf.Min(count, aliveCap));
        }

        public int SpawnGroup(EnemySpawnCard card, int count, string zone, bool announce)
        {
            SpawnZone from = FindZone(zone);
            if (card == null || count <= 0 || from == null || !_picker.TryPick(from, out Vector3 point))
                return 0;
            // Pruned first: a raider despawned this frame may come straight back from the pool as one of this group.
            Prune(_raiders);
            _group.Clear();
            int made = _spawner.SpawnGroup(card, point, count, from.Origin - point, _group);
            _raiders.AddRange(_group);
            if (made > 0 && announce)
                _audio.PlayAt(card.ArrivalCue, point);
            return made;
        }

        public void Withdraw(bool now)
        {
            Prune(_raiders);
            _leaving.AddRange(_raiders);
            _raiders.Clear();
            if (!now)
                return;
            foreach (EnemyBrain enemy in _leaving)
                _spawner.Despawn(enemy);
            _leaving.Clear();
        }

        // Withdrawn raiders keep hunting while the player sees them and vanish once out of sight.
        public void Tick()
        {
            if (_leaving.Count == 0)
                return;
            Prune(_leaving);
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                EnemyBrain enemy = _leaving[i];
                if (_picker.IsSeen(enemy.transform.position))
                    continue;
                _spawner.Despawn(enemy);
                _leaving.RemoveAt(i);
            }
        }

        // An unknown id falls back to the first zone (warned once), so a typo in a plan doesn't stall the raid.
        SpawnZone FindZone(string id)
        {
            SpawnZone first = null;
            for (int i = 0; i < _zones.Count; i++)
            {
                SpawnZone zone = _zones[i];
                if (zone == null)
                    continue;
                if (zone.Id == id)
                    return zone;
                if (first == null)
                    first = zone;
            }
            if (first != null && _unknownZones.Add(id ?? string.Empty))
                Debug.LogWarning($"RaidSpawner: no SpawnZone '{id}'; using {first.name}.", first);
            return first;
        }

        // Drops dead, despawned and destroyed enemies.
        static void Prune(List<EnemyBrain> enemies)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead)
                    continue;
                int last = enemies.Count - 1;
                enemies[i] = enemies[last];
                enemies.RemoveAt(last);
            }
        }
    }
}
