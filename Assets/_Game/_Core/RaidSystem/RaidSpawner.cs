using System.Collections.Generic;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.SpawnSystem;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.RaidSystem
{
    // The world side of raids: groups from the scene's SpawnZones through the SpawnService (out of the player's view by the
    // raid's SpawnRules), each card's arrival cue where its group appears, and the raiders still alive (members still to come
    // count). Withdrawn raiders despawn at once, or each as soon as the player can't see it; members still to come are dropped.
    public sealed class RaidSpawner : IRaidSpawner, ITickable
    {
        readonly SpawnService _spawns;
        readonly AudioService _audio;
        readonly RaidSettings _settings;
        readonly IReadOnlyList<SpawnZone> _zones;
        readonly List<SpawnHandle> _groups = new();
        readonly List<(SpawnHandle Group, EnemySpawnCard Card)> _announcing = new();
        readonly Dictionary<EnemySpawnCard, int> _planned = new();
        readonly HashSet<string> _unknownZones = new();

        public RaidSpawner(SpawnService spawns, AudioService audio, RaidSettings settings, IReadOnlyList<SpawnZone> zones)
        {
            _spawns = spawns;
            _audio = audio;
            _settings = settings;
            _zones = zones;
        }

        public int Alive
        {
            get
            {
                int alive = 0;
                for (int i = _groups.Count - 1; i >= 0; i--)
                {
                    int left = _groups[i].Alive;
                    if (left == 0 && _groups[i].IsDone)
                        _groups.RemoveAt(i);
                    alive += left;
                }
                return alive;
            }
        }

        public void Prepare(RaidPlan plan, int instances)
        {
            if (plan == null)
                return;
            _planned.Clear();
            // Cards of the last plan that this one doesn't bring are let go (their pooled copies trimmed).
            _spawns.Release(this);
            foreach (RaidWave wave in plan.Waves)
                foreach (RaidWaveEntry entry in wave.Entries)
                    if (entry.Card != null && entry.Count > 0)
                        _planned[entry.Card] = (_planned.TryGetValue(entry.Card, out int sum) ? sum : 0) + entry.Count;
            foreach ((EnemySpawnCard card, int count) in _planned)
                _spawns.Prewarm(card, Mathf.Min(count, instances), this);
        }

        public int SpawnGroup(EnemySpawnCard card, int count, string zone, bool announce)
        {
            SpawnZone from = FindZone(zone);
            if (card == null || count <= 0 || from == null)
                return 0;
            SpawnHandle group = _spawns.Spawn(SpawnRequest.Group(card, from, count).WithRules(_settings.Spawning));
            if (group.Count == 0)
                return 0;
            _groups.Add(group);
            if (announce)
                _announcing.Add((group, card));
            return group.Count;
        }

        public void Withdraw(bool now)
        {
            foreach (SpawnHandle group in _groups)
                group.Despawn(now ? DespawnMode.Now : DespawnMode.WhenUnseen);
            _groups.Clear();
            _announcing.Clear();
        }

        // Each announced group's cue plays where it appears, once its spot is found.
        public void Tick()
        {
            for (int i = _announcing.Count - 1; i >= 0; i--)
            {
                (SpawnHandle group, EnemySpawnCard card) = _announcing[i];
                if (!group.IsPlaced && !group.Cancelled)
                    continue;
                if (group.IsPlaced)
                    _audio.PlayAt(card.ArrivalCue, group.Anchor);
                _announcing.RemoveAt(i);
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
    }
}
