using System;
using System.Collections.Generic;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Aipocalypse.Core.SpawnSystem
{
    // Console commands on spawning (editor and development builds); they exist only in scenes with a SpawnInstaller.
    [CommandGroup("spawn")]
    public sealed class SpawnCommands : ConsoleCommands
    {
        // Metres in front of the player a console spawn lands, off the crosshair.
        const float SpawnDistance = 15f;
        const float SpawnSideOffset = 4f;
        // Seconds the test points stay drawn (Scene view, or the Game view with Gizmos on).
        const float TestDrawTime = 30f;

        readonly SpawnService _spawns;
        readonly SpawnPlacer _placer;
        readonly PopulationDirector _population;
        readonly SpawnSystemSettings _settings;
        readonly PlayerRegistry _players;
        readonly List<SpawnHandle> _handles = new();
        readonly Dictionary<FormationShape, Formation> _formations = new();
        SpawnRules _nearRules;

        public SpawnCommands(CommandRegistry registry, SpawnService spawns, SpawnPlacer placer, PopulationDirector population,
            SpawnSystemSettings settings, PlayerRegistry players) : base(registry)
        {
            _spawns = spawns;
            _placer = placer;
            _population = population;
            _settings = settings;
            _players = players;
        }

        [Command("Alive by category, queued members, requests still placing, withdrawn ones leaving, awake population areas")]
        string Stats()
        {
            var parts = new List<string>();
            foreach (SpawnCategory category in Enum.GetValues(typeof(SpawnCategory)))
                parts.Add($"{category} {_spawns.CountAlive(category)}");
            return $"{string.Join(", ", parts)} | total {_spawns.AliveTotal}, queued {_spawns.Queued}, placing {_spawns.Placing}, " +
                $"leaving {_spawns.Leaving} | areas {_population.ActiveAreas}/{_population.Areas} awake";
        }

        [Command("Spawn `count` of a card (SpawnSystemSettings.Cards, by name) in front of the player, as `formation` " +
            "(scatter, line, column, wedge, ring, clusters)")]
        CommandResult Card(string card, int count = 1, string formation = "scatter")
        {
            SpawnCard found = Find(card);
            if (found == null)
                return NoCard(card);
            if (_players.Current is not { } player)
                return CommandResult.Fail("No player in this scene");
            if (!Enum.TryParse(formation, true, out FormationShape shape))
                return CommandResult.Fail($"No formation '{formation}'");
            Transform body = player.Transform;
            Vector3 center = body.position + body.forward * SpawnDistance + body.right * SpawnSideOffset;
            SpawnRequest request = SpawnRequest.At(found, center, Mathf.Max(1, count)).Facing(body.position - center);
            if (shape != FormationShape.Scatter)
                request = request.With(Layout(shape));
            SpawnHandle handle = _spawns.Spawn(request);
            _handles.Add(handle);
            return handle.Cancelled
                ? CommandResult.Fail($"{found.name} can't stand there ({found.Placement.Medium})")
                : CommandResult.Ok($"{handle.Count} {found.name} ({shape})");
        }

        [Command("Spawn `count` of a card anywhere it can stand within `radius` m of the player, seen or not (finds water or sky " +
            "for fish and birds)")]
        CommandResult Near(string card, int count = 1, float radius = 60f)
        {
            SpawnCard found = Find(card);
            if (found == null)
                return NoCard(card);
            if (_players.Current is not { } player)
                return CommandResult.Fail("No player in this scene");
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.Group(found, new RingArea(player.Transform.position, 5f, radius), count)
                .WithRules(NearRules()));
            _handles.Add(handle);
            return CommandResult.Ok($"{handle.Count} {found.name} coming within {radius:0} m ({found.Placement.Medium})");
        }

        [Command("Place `count` test points for a medium (ground, air, water) within `radius` m of the player and draw them for 30 s: " +
            "how many fit and their heights")]
        CommandResult Test(string medium, int count = 40, float radius = 80f)
        {
            if (!Enum.TryParse(medium, true, out SpawnMedium parsed))
                return CommandResult.Fail($"No medium '{medium}' (ground, air, water)");
            if (_players.Current is not { } player)
                return CommandResult.Fail("No player in this scene");
            var placement = new SpawnPlacement
            {
                Medium = parsed,
                Navmesh = NavmeshRule.Ignore,
                Height = parsed == SpawnMedium.Air ? new Vector2(12f, 25f) : new Vector2(0.5f, 3f),
            };
            var area = new RingArea(player.Transform.position, 5f, radius);
            int placed = 0;
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                if (!_placer.TryPlace(area.Sample(Random.value, Random.value), placement, _settings.DefaultRules, false, default,
                        Random.value, out Vector3 point, out _))
                    continue;
                placed++;
                low = Mathf.Min(low, point.y);
                high = Mathf.Max(high, point.y);
                Debug.DrawLine(point, point + Vector3.up * 2f, parsed == SpawnMedium.Water ? Color.cyan : Color.yellow, TestDrawTime);
            }
            return CommandResult.Ok(placed > 0 ? $"{placed}/{count} placed, heights {low:0.0}–{high:0.0} m" : $"0/{count} placed");
        }

        [Command("Despawn what the console spawned (and drop what's still to come)")]
        CommandResult Clear()
        {
            int alive = 0;
            foreach (SpawnHandle handle in _handles)
            {
                alive += handle.Alive;
                handle.Despawn(DespawnMode.Now);
            }
            _handles.Clear();
            return CommandResult.Ok($"{alive} despawned");
        }

        Formation Layout(FormationShape shape)
        {
            if (_formations.TryGetValue(shape, out Formation formation))
                return formation;
            formation = ScriptableObject.CreateInstance<Formation>();
            formation.Shape = shape;
            return _formations[shape] = formation;
        }

        // The default rules, but anywhere: no distance from the player, no waiting for a hidden spot.
        SpawnRules NearRules()
        {
            if (_nearRules != null)
                return _nearRules;
            SpawnRules rules = _settings.DefaultRules;
            return _nearRules = new SpawnRules
            {
                Candidates = 24,
                SnapDistance = rules.SnapDistance,
                MinPlayerDistance = 0f,
                MaxHiddenWait = 0f,
                SeenLock = 0f,
                GroundMask = rules.GroundMask,
                SightBlockers = rules.SightBlockers,
                ObstacleMask = rules.ObstacleMask,
            };
        }

        CommandResult NoCard(string card) =>
            CommandResult.Fail($"No card '{card}'. Cards: {string.Join(", ", _settings.Cards.ConvertAll(c => c != null ? c.name : "-"))}");

        SpawnCard Find(string name)
        {
            foreach (SpawnCard card in _settings.Cards)
                if (card != null && card.name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return card;
            return null;
        }
    }
}
