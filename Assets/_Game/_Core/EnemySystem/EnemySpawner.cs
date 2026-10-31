using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // The one way code spawns enemies (raids, the console, later Blood Moons and wanderers): picks a card's variant, takes it
    // from the EnemyPool and keeps it on walkable ground. Groups arrive a few per frame (SpawnsPerFrame), since waking a pooled
    // enemy costs several milliseconds. Despawning hands it back to the pool.
    public sealed class EnemySpawner : ITickable
    {
        // Metres a spawn point may move to reach the navmesh.
        const float SnapDistance = 2f;
        // Metres between neighbours in a group (a sunflower spiral round the centre).
        const float GroupSpacing = 1.5f;
        const float GoldenAngle = 2.39996f;

        readonly EnemyPool _pool;
        readonly EnemyNavigation _navigation;
        readonly EnemySystemSettings _settings;
        readonly Queue<Member> _queue = new();

        public EnemySpawner(EnemyPool pool, EnemyNavigation navigation, EnemySystemSettings settings)
        {
            _pool = pool;
            _navigation = navigation;
            _settings = settings;
        }

        // Members queued by every group, cancelled ones included until their turn.
        public int Queued => _queue.Count;

        // One enemy of `card` exactly at `position` (already on the navmesh); null if the card has no variant.
        public EnemyBrain Spawn(EnemySpawnCard card, Vector3 position, Quaternion rotation)
        {
            EnemyBrain prefab = card != null ? card.PickVariant(Random.value) : null;
            if (prefab != null)
                return _pool.Get(prefab, position, rotation);
            Debug.LogWarning($"EnemySpawner: {(card != null ? card.name : "no card")} has no prefab variant to spawn.", card);
            return null;
        }

        // `count` enemies of `card` round `center`, facing `facing`, each snapped to the navmesh now and spawned over the next
        // frames; arrivals are added to `into`. The batch says how many were queued and how many are still to come.
        public SpawnBatch SpawnGroup(EnemySpawnCard card, Vector3 center, int count, Vector3 facing, List<EnemyBrain> into = null)
        {
            var batch = new SpawnBatch(into);
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            for (int i = 0; i < count; i++)
            {
                float radius = GroupSpacing * Mathf.Sqrt(i);
                float angle = i * GoldenAngle;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!_navigation.TrySnap(point, SnapDistance, out Vector3 snapped) && !_navigation.TrySnap(center, SnapDistance, out snapped))
                    continue;
                _queue.Enqueue(new Member(card, snapped, rotation, batch));
                batch.Queued();
            }
            return batch;
        }

        // Spawns the next queued members; none while paused.
        public void Tick()
        {
            if (Time.deltaTime <= 0f)
                return;
            int budget = Mathf.Max(1, _settings.SpawnsPerFrame);
            while (budget > 0 && _queue.Count > 0)
            {
                Member member = _queue.Dequeue();
                if (member.Batch.Cancelled)
                    continue;
                member.Batch.Arrived(Spawn(member.Card, member.Position, member.Rotation));
                budget--;
            }
        }

        // Drops every queued member (their batches end cancelled).
        public void CancelQueued()
        {
            foreach (Member member in _queue)
                member.Batch.Cancel();
            _queue.Clear();
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

        readonly struct Member
        {
            public readonly EnemySpawnCard Card;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly SpawnBatch Batch;

            public Member(EnemySpawnCard card, Vector3 position, Quaternion rotation, SpawnBatch batch)
            {
                Card = card;
                Position = position;
                Rotation = rotation;
                Batch = batch;
            }
        }
    }
}
