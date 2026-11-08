using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using Ami.BroAudio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
{
    public class EnemySpawningTests
    {
        const string Enemies = "Assets/_Game/Content/Enemies";

        static Spawnable[] GoblinRoots()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "/Goblin/Goblin.prefab").GetComponent<Spawnable>();
            var b = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "/Goblin/Goblin_02.prefab").GetComponent<Spawnable>();
            return new[] { a, b };
        }

        [Test]
        public void Card_PicksVariantsByWeight()
        {
            Spawnable[] brains = GoblinRoots();
            var card = ScriptableObject.CreateInstance<EnemySpawnCard>();
            try
            {
                Assert.That(card.PickVariant(0.5f), Is.Null, "no variant yet");
                card.SetVariant(brains[0], 3f);
                card.SetVariant(brains[1], 1f);
                Assert.That(card.PickVariant(0f), Is.SameAs(brains[0]));
                Assert.That(card.PickVariant(0.74f), Is.SameAs(brains[0]), "the first three quarters");
                Assert.That(card.PickVariant(0.76f), Is.SameAs(brains[1]), "the last quarter");
                Assert.That(card.PickVariant(1f), Is.SameAs(brains[1]), "a roll of 1 still picks one");
                card.SetVariant(brains[1], 0f);
                Assert.That(card.PickVariant(0.99f), Is.SameAs(brains[0]), "a zero weight is never picked");
            }
            finally
            {
                Object.DestroyImmediate(card);
            }
        }

        [Test]
        public void Card_GroupSize_StaysInItsRange()
        {
            var card = ScriptableObject.CreateInstance<EnemySpawnCard>();
            try
            {
                card.GroupSize = new Vector2Int(2, 3);
                Assert.That(card.PickGroupSize(0f), Is.EqualTo(2));
                Assert.That(card.PickGroupSize(0.49f), Is.EqualTo(2));
                Assert.That(card.PickGroupSize(0.51f), Is.EqualTo(3));
                Assert.That(card.PickGroupSize(1f), Is.EqualTo(3));
                card.GroupSize = new Vector2Int(0, -1);
                Assert.That(card.PickGroupSize(0.5f), Is.EqualTo(1), "never less than one");
            }
            finally
            {
                Object.DestroyImmediate(card);
            }
        }

        [Test]
        public void Voice_StepsOncePerStride_OnlyWhileAudible()
        {
            var body = new GameObject("Enemy").transform;
            var sounds = ScriptableObject.CreateInstance<EnemySoundSet>();
            try
            {
                sounds.StrideLength = 1f;
                var voice = new EnemyVoice(new AudioService(), body);
                voice.Reset(sounds);
                int steps = 0;
                for (int i = 1; i <= 10; i++)
                {
                    body.position = new Vector3(i * 0.25f, 0f, 0f);
                    if (voice.Walk(true))
                        steps++;
                }
                Assert.That(steps, Is.EqualTo(2), "2.5 m walked at 1 m per stride");

                body.position += Vector3.right * 0.9f;
                Assert.That(voice.Walk(false), Is.False, "out of earshot");
                body.position += Vector3.right * 0.9f;
                Assert.That(voice.Walk(true), Is.False, "walking out of earshot doesn't count");
                body.position += Vector3.right * 10f;
                Assert.That(voice.Walk(true), Is.False, "a teleport isn't a step");
            }
            finally
            {
                Object.DestroyImmediate(body.gameObject);
                Object.DestroyImmediate(sounds);
            }
        }

        [Test]
        public void SpawnCards_HaveEnemyVariants_WithDefinitions_AndACue()
        {
            int cards = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EnemySpawnCard), new[] { Enemies }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<EnemySpawnCard>(path);
                cards++;
                Assert.That(card.Variants.Count, Is.GreaterThan(0), $"{path}: no variant");
                Assert.That(card.PickVariant(0.5f), Is.Not.Null, $"{path}: no variant with a weight");
                foreach ((Spawnable prefab, float weight) in card.Variants)
                {
                    Assert.That(prefab, Is.Not.Null, $"{path}: empty variant");
                    Assert.That(prefab.TryGetComponent(out EnemyBrain brain), Is.True, $"{path}: {prefab.name} is no enemy");
                    Assert.That(brain.Definition, Is.Not.Null, $"{path}: {prefab.name} has no definition");
                    Assert.That(weight, Is.GreaterThanOrEqualTo(0f), $"{path}: negative weight");
                }
                Assert.That(card.GroupSize.x, Is.GreaterThanOrEqualTo(1), $"{path}: groups of nothing");
                Assert.That(card.ArrivalCue.IsValid(), Is.True, $"{path}: no arrival cue");
            }
            Assert.That(cards, Is.GreaterThan(0), "no spawn card");
        }

        [Test]
        public void FightingDefinitions_HaveSounds_AndDamage()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EnemyDefinition), new[] { Enemies }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                if (definition.Melee == null)
                    continue;
                Assert.That(definition.BaseStat(EnemyStat.AttackDamage), Is.GreaterThan(0f), $"{path}: its blows hurt nothing");
                Assert.That(definition.Targets.Count, Is.GreaterThan(0), $"{path}: hunts nothing");
                EnemySoundSet sounds = definition.Sounds;
                Assert.That(sounds, Is.Not.Null, $"{path}: silent");
                Assert.That(sounds.Attack.IsValid() && sounds.Swing.IsValid() && sounds.Impact.IsValid() && sounds.Hurt.IsValid()
                    && sounds.Death.IsValid() && sounds.Footstep.IsValid(), Is.True, $"{path}: a sound is unset");
            }
        }

        [Test]
        public void SystemSettings_ListEverySpawnCard()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EnemySystemSettings>("Assets/_Game/Data/Enemies/EnemySystemSettings.asset");
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EnemySpawnCard), new[] { Enemies }))
            {
                var card = AssetDatabase.LoadAssetAtPath<EnemySpawnCard>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(settings.Cards, Does.Contain(card), $"{card.name} can't be spawned or prewarmed by name");
            }
        }
    }
}
