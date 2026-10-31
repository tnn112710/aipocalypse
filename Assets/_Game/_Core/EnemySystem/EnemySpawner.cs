using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // The one way code spawns enemies (raids, the console, later Blood Moons and wanderers): picks a card's variant, takes it
    // from the EnemyPool and keeps it on walkable ground. Despawning hands it back to the pool.
    public sealed class EnemySpawner
    {
        // Metres a spawn point may move to reach the navmesh.
        const float SnapDistance = 2f;
        // Metres between neighbours in a group (a sunflower spiral round the centre).
        const float GroupSpacing = 1.5f;
        const float GoldenAngle = 2.39996f;

        readonly EnemyPool _pool;
        readonly EnemyNavigation _navigation;

        public EnemySpawner(EnemyPool pool, EnemyNavigation navigation)
        {
            _pool = pool;
            _navigation = navigation;
        }

        // One enemy of `card` exactly at `position` (already on the navmesh); null if the card has no variant.
        public EnemyBrain Spawn(EnemySpawnCard card, Vector3 position, Quaternion rotation)
        {
            EnemyBrain prefab = card != null ? card.PickVariant(Random.value) : null;
            if (prefab != null)
                return _pool.Get(prefab, position, rotation);
            Debug.LogWarning($"EnemySpawner: {(card != null ? card.name : "no card")} has no prefab variant to spawn.", card);
            return null;
        }

        // `count` enemies of `card` round `center`, facing `facing`, each snapped to the navmesh; adds them to `spawned`.
        // Returns how many spawned.
        public int SpawnGroup(EnemySpawnCard card, Vector3 center, int count, Vector3 facing, List<EnemyBrain> spawned = null)
        {
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                float radius = GroupSpacing * Mathf.Sqrt(i);
                float angle = i * GoldenAngle;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!_navigation.TrySnap(point, SnapDistance, out Vector3 snapped) && !_navigation.TrySnap(center, SnapDistance, out snapped))
                    continue;
                EnemyBrain enemy = Spawn(card, snapped, rotation);
                if (enemy == null)
                    continue;
                spawned?.Add(enemy);
                made++;
            }
            return made;
        }

        // Back to the pool (enemies placed in the scene just turn off).
        public void Despawn(EnemyBrain enemy)
        {
            if (enemy != null && enemy.gameObject.activeSelf)
                enemy.gameObject.SetActive(false);
        }

        // Creates enough instances of the card's variants, by weight, for `count` enemies alive at once.
        public void Prewarm(EnemySpawnCard card, int count)
        {
            if (card == null)
                return;
            float total = 0f;
            foreach ((EnemyBrain prefab, float weight) in card.Variants)
                if (prefab != null && weight > 0f)
                    total += weight;
            if (total <= 0f)
                return;
            foreach ((EnemyBrain prefab, float weight) in card.Variants)
                if (prefab != null && weight > 0f)
                    _pool.Prewarm(prefab, Mathf.CeilToInt(count * weight / total));
        }
    }
}
