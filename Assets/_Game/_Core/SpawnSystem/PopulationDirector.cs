using System.Collections.Generic;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Open-world population: every PopulationInterval it looks at the scene's SpawnAreas round the player, wakes the near ones,
    // tops them up (out of view, within the category and global caps) and lets the far ones go once unseen. Killed or used up
    // members come back after their area's RespawnDelay, also across visits. Its tick is scaled: none while paused.
    public sealed class PopulationDirector : IStartable, ITickable
    {
        static readonly ProfilerMarker PopulateMarker = new("Aipocalypse.PopulationDirector.Populate");

        readonly SpawnService _spawns;
        readonly SpawnSystemSettings _settings;
        readonly PlayerRegistry _players;
        readonly WorldClock _clock;
        readonly List<AreaState> _areas = new();
        float _wait;
        float _time;

        public PopulationDirector(SpawnService spawns, SpawnSystemSettings settings, PlayerRegistry players,
            IReadOnlyList<SpawnArea> areas, IObjectResolver resolver)
        {
            _spawns = spawns;
            _settings = settings;
            _players = players;
            resolver.TryResolve(out _clock);
            foreach (SpawnArea area in areas)
                if (area != null)
                    _areas.Add(new AreaState(area));
        }

        public int Areas => _areas.Count;

        public int ActiveAreas
        {
            get
            {
                int active = 0;
                foreach (AreaState state in _areas)
                    if (state.Active)
                        active++;
                return active;
            }
        }

        public void Start()
        {
            foreach (AreaState state in _areas)
            {
                SpawnArea area = state.Area;
                if (!area.Prewarm)
                    continue;
                if (area.Card != null)
                    _spawns.Prewarm(area.Card, area.Capacity, area);
                if (area.Table == null)
                    continue;
                foreach (SpawnTableEntry entry in area.Table.Entries)
                    _spawns.Prewarm(entry.Card, area.Capacity, area);
            }
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || _areas.Count == 0)
                return;
            _time += deltaTime;
            _wait -= deltaTime;
            if (_wait > 0f)
                return;
            _wait = _settings.PopulationInterval;
            using var _ = PopulateMarker.Auto();
            IPlayer player = _players.Current;
            Vector3 eye = player != null ? player.Transform.position : default;
            foreach (AreaState state in _areas)
            {
                if (state.Area == null)
                    continue;
                float distance = player != null ? Vector3.Distance(eye, state.Area.Zone.Origin) : float.PositiveInfinity;
                Populate(state, distance);
            }
        }

        void Populate(AreaState state, float distance)
        {
            SpawnArea area = state.Area;
            state.CountSpent(area.RespawnDelay > 0f ? _time + area.RespawnDelay : float.PositiveInfinity);
            PopulationRules.ReleaseDue(state.RespawnAt, _time);

            bool active = PopulationRules.IsActive(state.Active, distance, area.ActivateRadius, area.DeactivateRadius);
            if (state.Active && !active)
                state.LetGo();
            state.Active = active;
            if (!active)
                return;

            SpawnCard kind = area.Card != null ? area.Card : FirstCard(area.Table);
            if (kind == null)
                return;
            int categoryRoom = _settings.CapOf(kind.Category) - _spawns.CountAlive(kind.Category);
            int globalRoom = _settings.GlobalCap - _spawns.AliveTotal;
            int count = PopulationRules.ToSpawn(area.Capacity, state.Alive, state.RespawnAt.Count, area.GroupSize, categoryRoom, globalRoom);
            if (count <= 0)
                return;
            SpawnRequest request = area.Table != null
                ? SpawnRequest.From(area.Table, SpawnConditions.Of(_clock), area.Zone, count)
                : SpawnRequest.Group(area.Card, area.Zone, count);
            SpawnHandle group = _spawns.Spawn(request.With(area.Formation).WithRules(area.Rules));
            if (group.Count > 0)
                state.Add(group);
        }

        static SpawnCard FirstCard(SpawnTable table)
        {
            if (table == null)
                return null;
            foreach (SpawnTableEntry entry in table.Entries)
                if (entry.Card != null)
                    return entry.Card;
            return null;
        }

        // One area's groups in play and its killed members waiting to come back.
        sealed class AreaState
        {
            public readonly SpawnArea Area;
            public readonly List<float> RespawnAt = new();
            readonly List<SpawnHandle> _groups = new();
            readonly List<int> _spentSeen = new();

            public AreaState(SpawnArea area) => Area = area;

            public bool Active { get; set; }

            public int Alive
            {
                get
                {
                    int alive = 0;
                    for (int i = _groups.Count - 1; i >= 0; i--)
                    {
                        int left = _groups[i].Alive;
                        if (left == 0 && _groups[i].IsDone && _spentSeen[i] == _groups[i].Spent)
                        {
                            _groups.RemoveAt(i);
                            _spentSeen.RemoveAt(i);
                        }
                        alive += left;
                    }
                    return alive;
                }
            }

            public void Add(SpawnHandle group)
            {
                _groups.Add(group);
                _spentSeen.Add(0);
            }

            // Each member killed or used up since the last look waits until `until` to come back.
            public void CountSpent(float until)
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    for (int k = _spentSeen[i]; k < _groups[i].Spent; k++)
                        RespawnAt.Add(until);
                    _spentSeen[i] = _groups[i].Spent;
                }
            }

            // The player went away: the members leave once unseen (not counted as killed).
            public void LetGo()
            {
                foreach (SpawnHandle group in _groups)
                    group.Despawn(DespawnMode.WhenUnseen);
                _groups.Clear();
                _spentSeen.Clear();
            }
        }
    }
}
