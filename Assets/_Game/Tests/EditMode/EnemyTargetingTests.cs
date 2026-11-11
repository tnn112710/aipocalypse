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
        // 5 candidates, finish at 30 HP, a 15 m detour breaks a new gap, a third keep breaking (at least 1), new gaps 6 m apart,
        // a gap within 8 m is always taken, a 1 m wide body (a goblin; the fence's segments are 2 m).
        static readonly BreachRules Rules = new(5, 30f, 15f, 0.34f, 1, 6f, 8f, 1f);
        static readonly BreachRules NoBreakers = new(5, 30f, 1000f, 0f, 0, 6f, 8f, 1f);
        // A body 3 m wide: one broken segment is too narrow for it, two side by side let it through.
        static readonly BreachRules Large = new(5, 30f, 15f, 0.34f, 1, 6f, 8f, 3f);

        readonly List<FakeTarget> _targets = new();
        readonly List<FakeBarrier> _barriers = new();
        readonly List<TargetRegistry> _registries = new();
        EnemySystemSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<EnemySystemSettings>();

        [TearDown]
        public void TearDown()
        {
            foreach (TargetRegistry registry in _registries)
                registry.Dispose();
            _registries.Clear();
            foreach (FakeTarget target in _targets)
                Object.DestroyImmediate(target.Transform.gameObject);
            _targets.Clear();
            foreach (FakeBarrier barrier in _barriers)
                Object.DestroyImmediate(barrier.Transform.gameObject);
            _barriers.Clear();
            Object.DestroyImmediate(_settings);
        }

        // Disposed after the test (a registry with barriers owns the Farm's object).
        TargetRegistry Registry()
        {
            var registry = new TargetRegistry(null);
            _registries.Add(registry);
            return registry;
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
            TargetRegistry registry = Registry();
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
            TargetRegistry registry = Registry();
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

            // A gap opens at x = 4: each chooses again, in an order that isn't the ranking. The ones within 8 m of it (x -2..0)
            // go through whatever the ranking says.
            At(4f).IsTargetable = false;
            registry.BarriersChanged();
            navmesh.Reachable = true;
            var breakers = new List<float>();
            foreach (int i in new[] { 3, 0, 5, 1, 4, 2 })
                if (selector.Choose(goblins[i], new Vector3(xs[i], 0f, 9f), BreakerWeights, At(xs[i]), Rules) is IBreachable)
                    breakers.Add(xs[i]);

            breakers.Sort();
            Assert.That(breakers, Is.EqualTo(new[] { -10f, -8f }), "a third of six keep breaking: the two furthest from the gap");
        }

        [Test]
        public void Selector_HuntersNextToAGap_AllGoThroughIt()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var navmesh = new FakeNavmesh { Reachable = false };
            var selector = new TargetSelector(registry, navmesh);
            // Six goblins hitting the fence on both sides of the segment about to fall, all within 7 m of it.
            float[] xs = { -2f, 0f, 2f, 6f, 8f, 10f };
            var goblins = new object[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                goblins[i] = new object();
                selector.Choose(goblins[i], new Vector3(xs[i], 0f, 9f), BreakerWeights, At(xs[i]), Rules);
            }

            At(4f).IsTargetable = false;
            registry.BarriersChanged();
            navmesh.Reachable = true;
            for (int i = 0; i < xs.Length; i++)
                Assert.That(selector.Choose(goblins[i], new Vector3(xs[i], 0f, 9f), BreakerWeights, At(xs[i]), Rules), Is.SameAs(player),
                    $"the goblin at x {xs[i]} is next to the gap");
        }

        [Test]
        public void Selector_GapTooNarrowForItsBody_WidensItByTheWeakerNeighbour_ThenGoesThrough()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(0f).IsTargetable = false;
            registry.BarriersChanged();
            At(2f).RemainingHealth = 100f;
            var from = new Vector3(0f, 0f, 5f);

            Assert.That(selector.Choose(new object(), from, BreakerWeights, null, Rules), Is.SameAs(player), "a goblin fits a 2 m gap");
            Assert.That(selector.Choose(new object(), from, BreakerWeights, null, Large), Is.SameAs(At(2f)),
                "too narrow for a 3 m body, even straight through it: the weaker barrier beside it");

            At(2f).IsTargetable = false;
            registry.BarriersChanged();
            Assert.That(selector.Choose(new object(), from, BreakerWeights, null, Large), Is.SameAs(player), "4 m wide now");
        }

        [Test]
        public void Selector_Newcomers_ShareTheBreaking_TheOnesFurthestFromTheGap()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            // A gap at x = 12; four goblins arriving outside at z = 5, none at the fence yet, all beyond 8 m of the gap and with
            // it less than 15 m out of their way. Each chooses twice: the second choice knows the others.
            At(12f).IsTargetable = false;
            registry.BarriersChanged();
            float[] xs = { 4f, 2f, 0f, -1f };
            var goblins = new object[xs.Length];
            for (int i = 0; i < xs.Length; i++)
                goblins[i] = new object();
            var breakers = new List<float>();
            for (int pass = 0; pass < 2; pass++)
            {
                breakers.Clear();
                for (int i = 0; i < xs.Length; i++)
                    if (selector.Choose(goblins[i], new Vector3(xs[i], 0f, 5f), BreakerWeights, null, Rules) is IBreachable)
                        breakers.Add(xs[i]);
            }

            Assert.That(breakers, Is.EqualTo(new[] { -1f }), "a third of four (at least one): the newcomer furthest from the gap");
            Assert.That(selector.Choose(goblins[0], new Vector3(4f, 0f, 5f), BreakerWeights, null, Rules), Is.SameAs(player), "the rest go for the gap");
        }

        [Test]
        public void Selector_Unaware_GoesForItsObjective_NeverThePlayer()
        {
            TargetRegistry registry = Fence(out FakeTarget player);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = true });
            At(0f).IsTargetable = false;
            registry.BarriersChanged();
            var from = new Vector3(0f, 0f, 5f);

            Assert.That(selector.Choose(new object(), from, BreakerWeights, null, Rules, false, registry.Farm), Is.SameAs(registry.Farm), "the farm, through the gap");
            Assert.That(selector.Choose(new object(), from, BreakerWeights, null, Rules, false, null), Is.Null, "no objective: nothing to hunt");
            Assert.That(selector.Choose(new object(), from, BreakerWeights, null, Rules, true, registry.Farm), Is.SameAs(player), "once it knows");
        }

        [Test]
        public void Selector_Unaware_FenceShut_BreaksInTowardTheFarm()
        {
            TargetRegistry registry = Fence(out _);
            var selector = new TargetSelector(registry, new FakeNavmesh { Reachable = false });
            At(0f).RemainingHealth = 50f;

            Assert.That(selector.Choose(new object(), new Vector3(0f, 0f, 5f), BreakerWeights, null, Rules, false, registry.Farm), Is.SameAs(At(0f)));
        }

        [Test]
        public void Registry_FarmIsTheMiddleOfItsBarriers_AndGoesWithThem()
        {
            TargetRegistry registry = Registry();
            Assert.That(registry.Farm, Is.Null, "no barriers, no farm");
            registry.Add(Barrier(-10f, 10f));
            registry.Add(Barrier(10f, 10f));
            Assert.That(registry.FarmPosition, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(registry.Farm.Kind, Is.EqualTo(TargetKind.Farm));
            Assert.That(registry.Count, Is.Zero, "not among the hunted targets");
            registry.Remove(_barriers[1]);
            Assert.That(registry.FarmPosition.x, Is.EqualTo(-10f));
            registry.Remove(_barriers[0]);
            Assert.That(registry.Farm, Is.Null);
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
            TargetRegistry registry = Registry();
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
            TargetRegistry registry = Registry();
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
            TargetRegistry registry = Registry();
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
            TargetRegistry registry = Registry();
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
            TargetRegistry registry = Registry();
            var selector = new TargetSelector(registry, null);
            registry.Add(Target(new Vector3(5f, 0f, 0f)));

            Assert.That(selector.Choose(Vector3.zero, new Dictionary<TargetKind, float>()), Is.Null, "no weight, no hunt");
            Assert.That(selector.Choose(Vector3.zero, new Dictionary<TargetKind, float> { { TargetKind.Player, 0f } }), Is.Null, "zero weight");
            Assert.That(selector.Choose(Vector3.zero, null), Is.Null, "no table");
        }

        [Test]
        public void Registry_AddsATargetOnce_AndRemovesIt()
        {
            TargetRegistry registry = Registry();
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
