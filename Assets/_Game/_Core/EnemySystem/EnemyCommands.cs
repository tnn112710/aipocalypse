using System;
using System.Collections.Generic;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Console commands on the scene's enemies (editor and development builds); they exist only in scenes with an EnemyInstaller.
    [CommandGroup("enemy")]
    public sealed class EnemyCommands : ConsoleCommands
    {
        // Metres in front of the player a console spawn lands, off the crosshair.
        const float SpawnDistance = 15f;
        const float SpawnSideOffset = 4f;

        readonly EnemyRegistry _enemies;
        readonly EnemySpawner _spawner;
        readonly EnemySystemSettings _settings;
        readonly PlayerRegistry _players;
        readonly List<EnemyBrain> _gone = new();
        readonly List<SpawnBatch> _batches = new();

        public EnemyCommands(CommandRegistry registry, EnemyRegistry enemies, EnemySpawner spawner, EnemySystemSettings settings,
            PlayerRegistry players) : base(registry)
        {
            _enemies = enemies;
            _spawner = spawner;
            _settings = settings;
            _players = players;
        }

        [Command("Enemies in play")]
        int Count() => _enemies.Count;

        [Command("Spawn enemies of a card (EnemySystemSettings.Cards, by name: goblin) in front of the player")]
        CommandResult Spawn(string card, int count = 1)
        {
            EnemySpawnCard found = Find(card);
            if (found == null)
                return CommandResult.Fail($"No card '{card}'. Cards: {string.Join(", ", _settings.Cards.ConvertAll(c => c != null ? c.name : "-"))}");
            if (_players.Current is not { } player)
                return CommandResult.Fail("No player in this scene");
            Transform body = player.Transform;
            Vector3 center = body.position + body.forward * SpawnDistance + body.right * SpawnSideOffset;
            SpawnBatch batch = _spawner.SpawnGroup(found, center, Mathf.Max(1, count), body.position - center);
            _batches.Add(batch);
            int made = batch.Count;
            return made > 0 ? CommandResult.Ok($"{made} {found.name}") : CommandResult.Fail("No walkable ground there");
        }

        [Command("Despawn every enemy in play (and drop the ones still to spawn)")]
        CommandResult Clear()
        {
            // Only the console's own queued spawns: a raid's are its own business.
            foreach (SpawnBatch batch in _batches)
                batch.Cancel();
            _batches.Clear();
            _gone.Clear();
            for (int i = 0; i < _enemies.Count; i++)
                _gone.Add(_enemies[i]);
            foreach (EnemyBrain enemy in _gone)
                _spawner.Despawn(enemy);
            return CommandResult.Ok($"{_gone.Count} despawned");
        }

        EnemySpawnCard Find(string name)
        {
            foreach (EnemySpawnCard card in _settings.Cards)
                if (card != null && card.name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return card;
            return null;
        }
    }
}
