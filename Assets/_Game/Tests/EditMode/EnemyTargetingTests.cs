using System.Collections.Generic;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.Entities;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public class EnemyTargetingTests
    {
        sealed class FakeTarget : ITargetable
        {
            public FakeTarget(Vector3 position, TargetKind kind = TargetKind.Player)
            {
                Transform = new GameObject("Target").transform;
                Transform.position = position;
                Kind = kind;
            }

            public Transform Transform { get; }
            public TargetKind Kind { get; }
            public bool IsTargetable { get; set; } = true;
            public bool IsDead => false;
            public DamageResult TakeDamage(in DamageInfo info) => default;
        }

        readonly List<FakeTarget> _targets = new();
        EnemySystemSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<EnemySystemSettings>();

        [TearDown]
        public void TearDown()
        {
            foreach (FakeTarget target in _targets)
                Object.DestroyImmediate(target.Transform.gameObject);
            _targets.Clear();
            Object.DestroyImmediate(_settings);
        }

        FakeTarget Target(Vector3 position)
        {
            var target = new FakeTarget(position);
            _targets.Add(target);
            return target;
        }

        [Test]
        public void Selector_PicksTheNearestWantedTarget_AndSkipsUntargetableOnes()
        {
            var registry = new TargetRegistry(null);
            var selector = new TargetSelector(registry);
            var weights = new Dictionary<TargetKind, float> { { TargetKind.Player, 1f } };
            FakeTarget far = Target(new Vector3(30f, 0f, 0f));
            FakeTarget near = Target(new Vector3(10f, 0f, 0f));
            registry.Add(far);
            registry.Add(near);

            Assert.That(selector.Choose(Vector3.zero, weights), Is.SameAs(near));
            near.IsTargetable = false;
            Assert.That(selector.Choose(Vector3.zero, weights), Is.SameAs(far), "an untargetable target is skipped");
            far.IsTargetable = false;
            Assert.That(selector.Choose(Vector3.zero, weights), Is.Null, "nothing left to hunt");
        }

        [Test]
        public void Selector_IgnoresKindsWithoutWeight()
        {
            var registry = new TargetRegistry(null);
            var selector = new TargetSelector(registry);
            registry.Add(Target(new Vector3(5f, 0f, 0f)));

            Assert.That(selector.Choose(Vector3.zero, new Dictionary<TargetKind, float>()), Is.Null, "no weight, no hunt");
            Assert.That(selector.Choose(Vector3.zero, new Dictionary<TargetKind, float> { { TargetKind.Player, 0f } }), Is.Null, "zero weight");
            Assert.That(selector.Choose(Vector3.zero, null), Is.Null, "no table");
        }

        [Test]
        public void Registry_AddsATargetOnce_AndRemovesIt()
        {
            var registry = new TargetRegistry(null);
            FakeTarget target = Target(Vector3.zero);
            registry.Add(target);
            registry.Add(target);
            Assert.That(registry.Count, Is.EqualTo(1));
            registry.Remove(target);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void Slots_CapAttackersPerTarget_AndFreeOnRelease()
        {
            _settings.SetAttackersPerTarget(TargetKind.Player, 2);
            var slots = new AttackSlots(_settings);
            FakeTarget target = Target(Vector3.zero);
            object a = new(), b = new(), c = new();

            Assert.That(slots.TryTake(a, target), Is.True);
            Assert.That(slots.TryTake(a, target), Is.True, "taking again keeps the same slot");
            Assert.That(slots.TryTake(b, target), Is.True);
            Assert.That(slots.TryTake(c, target), Is.False, "full at two");
            Assert.That(slots.Taken(target), Is.EqualTo(2));
            slots.Release(a);
            Assert.That(slots.Holds(a, target), Is.False);
            Assert.That(slots.TryTake(c, target), Is.True, "a freed slot goes to the next");
        }

        [Test]
        public void Slots_TakingAnotherTarget_FreesThePreviousOne()
        {
            _settings.SetAttackersPerTarget(TargetKind.Player, 1);
            var slots = new AttackSlots(_settings);
            FakeTarget first = Target(Vector3.zero);
            FakeTarget second = Target(Vector3.one);
            object a = new(), b = new();

            Assert.That(slots.TryTake(a, first), Is.True);
            Assert.That(slots.TryTake(a, second), Is.True);
            Assert.That(slots.Taken(first), Is.EqualTo(0), "the first target's slot was freed");
            Assert.That(slots.TryTake(b, first), Is.True);
        }

        [Test]
        public void Slots_ZeroCapacity_RefusesEveryone()
        {
            _settings.SetAttackersPerTarget(TargetKind.Player, 0);
            var slots = new AttackSlots(_settings);
            Assert.That(slots.TryTake(new object(), Target(Vector3.zero)), Is.False);
        }

        [Test]
        public void Slots_NoTarget_TakesNothing()
        {
            var slots = new AttackSlots(_settings);
            object holder = new();
            Assert.That(slots.TryTake(holder, null), Is.False);
            Assert.That(slots.Holds(holder, null), Is.False);
            slots.Release(holder);
        }
    }
}
