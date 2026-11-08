using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.SpawnSystem;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
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

        sealed class FakeBarrier : IBreachable
        {
            public FakeBarrier(Vector3 position, float health)
            {
                Transform = new GameObject("Barrier").transform;
                Transform.position = position;
                RemainingHealth = health;
            }

            public Transform Transform { get; }
            public TargetKind Kind => TargetKind.Fence;
            public bool IsTargetable { get; set; } = true;
            public bool IsDead => false;
            public float RemainingHealth { get; set; }
            public Vector3 Span => new(1f, 0f, 0f);
            public DamageResult TakeDamage(in DamageInfo info) => default;
        }

        sealed class FakeNavmesh : INavmeshQuery
        {
            public bool Reachable;
            public bool TrySnap(Vector3 point, float maxDistance, out Vector3 snapped)
            {
                snapped = point;
                return true;
            }

            public bool IsReachable(Vector3 from, Vector3 to) => Reachable;
        }

        static readonly Dictionary<TargetKind, float> BreakerWeights = new() { { TargetKind.Player, 1f }, { TargetKind.Fence, 1f } };
        // 5 candidates, finish at 30 HP, a 15 m detour breaks a new gap, a third keep breaking (at least 1), new gaps 6 m apart.
        static readonly BreachRules Rules = new(5, 30f, 15f, 0.34f, 1, 6f);
        static readonly BreachRules NoBreakers = new(5, 30f, 1000f, 0f, 0, 6f);

        readonly List<FakeTarget> _targets = new();
        readonly List<FakeBarrier> _barriers = new();
        EnemySystemSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<EnemySystemSettings>();

        [TearDown]
        public void TearDown()
        {
            foreach (FakeTarget target in _targets)
                Object.DestroyImmediate(target.Transform.gameObject);
            _targets.Clear();
            foreach (FakeBarrier barrier in _barriers)
                Object.DestroyImmediate(barrier.Transform.gameObject);
            _barriers.Clear();
            Object.DestroyImmediate(_settings);
        }

        FakeTarget Target(Vector3 position)
        {
            var target = new FakeTarget(position);
            _targets.Add(target);
            return target;
        }

        FakeBarrier Barrier(float x, float health)
        {
            var barrier = new FakeBarrier(new Vector3(x, 0f, 10f), health);
            _barriers.Add(barrier);
            return barrier;
        }

        // A straight fence along X at z = 10, 2 m segments from x = -20 to 20; the player behind it at z = 20.
        TargetRegistry Fence(out FakeTarget player)
        {
            var registry = new TargetRegistry(null);
            player = Target(new Vector3(0f, 0f, 20f));
            registry.Add(player);
            for (int x = -20; x <= 20; x += 2)
                registry.Add(Barrier(x, 300f));
            return registry;
        }

        FakeBarrier At(float x) => _barriers.Find(b => Mathf.Approximately(b.Transform.position.x, x));

        [Test]
        public void Selector_WithAPath_AndNoWallBetween_HuntsTheTarget()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(0f).IsTargetable = false;

            Assert.That(selector.Choose(new object(), new Vector3(5f, 0f, 15f), BreakerWeights, null, Rules), Is.SameAs(player), "already inside");
        }

        [Test]
        public void Selector_FenceShut_AttacksTheWeakestOfTheNearestBarriers()
        {
            var registry = new TargetRegistry(null);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = false });
            registry.Add(Target(new Vector3(0f, 0f, 30f)));
            // Nearest five at x 0..4 (the one at 3 is the weakest of them); the weakest of all at x 9 is too far to be compared.
            FakeBarrier weakestNear = null;
            for (int i = 0; i < 5; i++)
            {
                FakeBarrier barrier = Barrier(i, i == 3 ? 50f : 200f);
                registry.Add(barrier);
                if (i == 3)
                    weakestNear = barrier;
            }
            registry.Add(Barrier(9f, 1f));

            Assert.That(selector.Choose(new object(), Vector3.zero, BreakerWeights, null, Rules), Is.SameAs(weakestNear));
            weakestNear.IsTargetable = false;
            Assert.That(selector.ChooseBreach(Vector3.zero, 5), Is.Not.SameAs(weakestNear), "a broken or open one is skipped");
        }

        [Test]
        public void Selector_NearGap_GoesThroughIt()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(2f).IsTargetable = false;

            Assert.That(selector.Choose(new object(), new Vector3(0f, 0f, 5f), BreakerWeights, null, Rules), Is.SameAs(player));
        }

        [Test]
        public void Selector_FarGap_BreaksANewOne_AwayFromTheGap()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(20f).IsTargetable = false;

            ITargetable choice = selector.Choose(new object(), new Vector3(-10f, 0f, 5f), BreakerWeights, null, Rules);
            Assert.That(choice, Is.InstanceOf<IBreachable>(), "the gap is 30 m along the fence");
            Assert.That(Mathf.Abs(choice.Transform.position.x - 20f), Is.GreaterThanOrEqualTo(6f), "not next to the open gap");
            Assert.That(selector.Detour(new Vector3(-10f, 0f, 5f), player.Transform.position), Is.GreaterThan(15f));
        }

        [Test]
        public void Selector_WithAWayIn_LeavesAStrongBarrier_ButFinishesOneAboutToBreak()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(2f).IsTargetable = false;
            FakeBarrier hitting = At(0f);
            object goblin = new();

            Assert.That(selector.Choose(goblin, new Vector3(0f, 0f, 9f), BreakerWeights, hitting, NoBreakers), Is.SameAs(player), "still strong");
            hitting.RemainingHealth = 25f;
            Assert.That(selector.Choose(goblin, new Vector3(0f, 0f, 9f), BreakerWeights, hitting, NoBreakers), Is.SameAs(hitting), "two blows left");
        }

        [Test]
        public void Selector_KeepsAShareOfFenceAttackers_TheOnesFurthestFromTheGap()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var navmesh = new FakeNavmesh { Reachable = false };
            var selector = new TargetSelector(registry, navmesh);
            // Six goblins along the shut fence, each hitting the segment in front of it.
            float[] xs = { -10f, -8f, -6f, -4f, -2f, 0f };
            var goblins = new object[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                goblins[i] = new object();
                selector.Choose(goblins[i], new Vector3(xs[i], 0f, 9f), BreakerWeights, At(xs[i]), Rules);
            }

            // A gap opens at x = 4: each chooses again, in an order that isn't the ranking.
            At(4f).IsTargetable = false;
            navmesh.Reachable = true;
            var breakers = new List<float>();
            foreach (int i in new[] { 3, 0, 5, 1, 4, 2 })
                if (selector.Choose(goblins[i], new Vector3(xs[i], 0f, 9f), BreakerWeights, At(xs[i]), Rules) is IBreachable)
                    breakers.Add(xs[i]);

            breakers.Sort();
            Assert.That(breakers, Is.EqualTo(new[] { -10f, -8f }), "a third of six keep breaking: the two furthest from the gap");
        }

        [Test]
        public void Selector_LoneHunter_TakesTheGap_AndForgetDropsIt()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(4f).IsTargetable = false;
            object goblin = new();

            Assert.That(selector.Choose(goblin, new Vector3(-6f, 0f, 9f), BreakerWeights, At(-6f), Rules), Is.SameAs(player), "never all of them");
            selector.Forget(goblin);
            selector.Forget(null);
        }

        [Test]
        public void Registry_CountsBarrierChanges()
        {
            var registry = new TargetRegistry(null);
            int before = registry.BarrierVersion;
            registry.BarriersChanged();
            Assert.That(registry.BarrierVersion, Is.EqualTo(before + 1));
        }

        [Test]
        public void Selector_WithoutAPath_KeepsTheTarget_WhenItDoesNotBreakFences()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = false });

            var weights = new Dictionary<TargetKind, float> { { TargetKind.Player, 1f } };
            Assert.That(selector.Choose(new object(), new Vector3(0f, 0f, 5f), weights, null, Rules), Is.SameAs(player), "no Fence weight");
            Assert.That(selector.Choose(new object(), new Vector3(0f, 0f, 5f), BreakerWeights, null, default), Is.SameAs(player), "no breach rules");
        }

        [Test]
        public void Selector_WithoutAPath_AndNoBarrierStanding_KeepsTheTarget()
        {
            var registry = new TargetRegistry(null);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = false });
            FakeTarget player = Target(new Vector3(0f, 0f, 30f));
            registry.Add(player);
            FakeBarrier open = Barrier(0f, 10f);
            open.IsTargetable = false;
            registry.Add(open);

            Assert.That(selector.Choose(new object(), Vector3.zero, BreakerWeights, null, Rules), Is.SameAs(player));
        }

        [Test]
        public void Registry_KeepsBarriersApartFromHuntedTargets()
        {
            var registry = new TargetRegistry(null);
            registry.Add(Target(Vector3.zero));
            FakeBarrier barrier = Barrier(0f, 10f);
            registry.Add(barrier);
            registry.Add(barrier);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.Barriers.Count, Is.EqualTo(1));
            registry.Remove(barrier);
            Assert.That(registry.Barriers.Count, Is.EqualTo(0));
        }

        [Test]
        public void Selector_PicksTheNearestWantedTarget_AndSkipsUntargetableOnes()
        {
            var registry = new TargetRegistry(null);
            var selector = new TargetSelector(registry, null);
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
            var selector = new TargetSelector(registry, null);
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
