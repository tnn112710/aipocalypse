using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
{
    public class EnemyCombatTests
    {
        EnemyDefinition _definition;
        EnemyReactionSettings _reaction;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _definition.SetBaseStat(EnemyStat.MaxHealth, 100f);
            _definition.SetBaseStat(EnemyStat.MoveSpeed, 2f);
            _reaction = ScriptableObject.CreateInstance<EnemyReactionSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_reaction);
        }

        [Test]
        public void Stats_StartAtTheDefinitionsBaseValues()
        {
            var stats = new EnemyStats(_definition);
            Assert.That(stats.Get(EnemyStat.MaxHealth), Is.EqualTo(100f));
            Assert.That(stats.Get(EnemyStat.MoveSpeed), Is.EqualTo(2f));
        }

        [Test]
        public void Stats_AddThenMultiply_AndRemoveBySource()
        {
            var stats = new EnemyStats(_definition);
            var changed = new List<EnemyStat>();
            stats.Changed += changed.Add;
            object slow = new object(), elite = new object();
            stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, slow, multiply: 0.5f));
            stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, elite, add: 1f));
            stats.AddModifier(new EnemyStatModifier(EnemyStat.MaxHealth, elite, multiply: 2f));

            Assert.That(stats.Get(EnemyStat.MoveSpeed), Is.EqualTo((2f + 1f) * 0.5f).Within(1e-5f));
            Assert.That(stats.Get(EnemyStat.MaxHealth), Is.EqualTo(200f).Within(1e-5f));
            stats.RemoveModifiers(elite);
            Assert.That(stats.Get(EnemyStat.MoveSpeed), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(stats.Get(EnemyStat.MaxHealth), Is.EqualTo(100f).Within(1e-5f));
            Assert.That(stats.GetBase(EnemyStat.MoveSpeed), Is.EqualTo(2f));
            Assert.That(changed, Is.EqualTo(new[] { EnemyStat.MoveSpeed, EnemyStat.MoveSpeed, EnemyStat.MaxHealth, EnemyStat.MaxHealth, EnemyStat.MoveSpeed }));
        }

        [Test]
        public void Stats_NeverGoBelowZero()
        {
            var stats = new EnemyStats(_definition);
            stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, this, add: -5f));
            Assert.That(stats.Get(EnemyStat.MoveSpeed), Is.Zero);
        }

        [Test]
        public void Knockback_SlidesAboutSpeedOverDrag_AtAnyFrameRate()
        {
            foreach (float deltaTime in new[] { 1f / 30f, 1f / 144f })
            {
                var knockback = new Knockback(_reaction);
                knockback.Add(new Vector3(0f, 0f, 2f));
                float speed = knockback.Speed;
                Vector3 slid = Vector3.zero;
                for (int i = 0; i < 1000 && knockback.Active; i++)
                    slid += knockback.Advance(deltaTime);
                Assert.That(knockback.Active, Is.False, "comes to rest");
                Assert.That(slid.z, Is.EqualTo(speed / _reaction.KnockbackDrag).Within(0.01f), $"dt {deltaTime}");
            }
        }

        [Test]
        public void Knockback_IsHorizontal_AndCapped()
        {
            var knockback = new Knockback(_reaction);
            knockback.Add(new Vector3(0f, 10f, 0f));
            Assert.That(knockback.Active, Is.False, "an upward hit doesn't push");
            for (int i = 0; i < 8; i++)
                knockback.Add(new Vector3(3f, 0f, 0f));
            Assert.That(knockback.Speed, Is.EqualTo(_reaction.MaxKnockbackSpeed).Within(1e-4f));
        }

        [Test]
        public void EnemyDefinitions_HaveEveryStat_AndReactionSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(EnemyDefinition), new[] { "Assets/_Game/Content/Enemies" });
            Assert.That(guids, Is.Not.Empty);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                foreach (EnemyStat stat in Enum.GetValues(typeof(EnemyStat)))
                {
                    Assert.That(definition.HasBaseStat(stat), Is.True, $"{path}: no base {stat}");
                    // A kind that never swings deals no damage.
                    if (stat == EnemyStat.AttackDamage)
                        Assert.That(definition.BaseStat(stat), Is.GreaterThanOrEqualTo(0f), $"{path}: {stat} can't be negative");
                    else
                        Assert.That(definition.BaseStat(stat), Is.GreaterThan(0f), $"{path}: {stat} must be positive");
                }
                Assert.That(definition.Reaction, Is.Not.Null, $"{path}: no Reaction settings");
            }
        }

        [Test]
        public void EnemyPrefabs_CanBeHurt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies" });
            int enemies = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<EnemyBrain>() == null)
                    continue;
                enemies++;
                var body = prefab.GetComponent<EnemyBody>();
                Assert.That(body, Is.Not.Null, $"{path}: no EnemyBody");
                Assert.That(body.Visual != null && body.Visual.IsChildOf(prefab.transform) && body.Visual != prefab.transform, Is.True,
                    $"{path}: EnemyBody needs its visual child (the Model)");
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger), Is.True, $"{path}: nothing for hits to land on");
            }
            Assert.That(enemies, Is.GreaterThan(0));
        }
    

        [Test]
        public void Hitbox_MultipliesTheHit_AndMarksItCritical()
        {
            var info = new DamageInfo(20f, DamageType.Gunshot, Vector3.one, Vector3.forward);
            DamageInfo head = info.OnHitbox(2f, true);
            Assert.That(head.Amount, Is.EqualTo(40f));
            Assert.That(head.Critical, Is.True);
            Assert.That(head.Force, Is.EqualTo(info.Force), "knockback doesn't scale");
            Assert.That(info.OnHitbox(0.5f, false).Critical, Is.False);
        }

        [Test]
        public void AnimatedEnemyPrefabs_HaveACriticalHeadAboveTheBody()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var enemyAnimator = prefab.GetComponent<EnemyAnimator>();
                if (enemyAnimator == null)
                    continue;
                Transform head = enemyAnimator.Animator.GetBoneTransform(HumanBodyBones.Head);
                var hitbox = head.GetComponent<Hitbox>();
                Assert.That(hitbox != null && hitbox.Critical && hitbox.DamageMultiplier > 1f, Is.True, $"{path}: no critical Hitbox on the head bone");
                var sphere = head.GetComponent<SphereCollider>();
                var body = prefab.GetComponent<CapsuleCollider>();
                float headBottom = head.TransformPoint(sphere.center).y - sphere.radius * head.lossyScale.x;
                float bodyTop = prefab.transform.TransformPoint(body.center).y + body.height * 0.5f;
                Assert.That(headBottom, Is.GreaterThanOrEqualTo(bodyTop - 0.01f), $"{path}: the body collider swallows the head");
            }
        }
    }
}
