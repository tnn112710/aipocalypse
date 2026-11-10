using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class StatModifierTests
    {
        [Test]
        public void Get_WithNoModifiers_ReturnsTheBaseValue()
        {
            var stats = new StatModifiers();

            Assert.AreEqual(5f, stats.Get(StatId.WalkSpeed, 5f));
            Assert.AreEqual(1f, stats.GetScale(StatId.AirSpreadScale));
        }

        [Test]
        public void Get_AddsThenMultipliesAcrossSources()
        {
            var stats = new StatModifiers();
            stats.Set("upgrade.boots", StatId.WalkSpeed, add: 1f);
            stats.Set("skill.runner", StatId.WalkSpeed, multiply: 1.5f);
            stats.Set("skill.sprinter", StatId.WalkSpeed, add: 1f, multiply: 2f);

            Assert.AreEqual((5f + 2f) * 3f, stats.Get(StatId.WalkSpeed, 5f), 1e-4f);
            Assert.AreEqual(4f, stats.Get(StatId.SprintSpeed, 4f), "another stat is untouched");
        }

        [Test]
        public void Set_ReplacesTheSameSourcesModifierOnThatStat()
        {
            var stats = new StatModifiers();
            stats.Set("upgrade.grip", StatId.HeldItemSwayScale, multiply: 0.9f);
            stats.Set("upgrade.grip", StatId.HeldItemSwayScale, multiply: 0.8f);

            Assert.AreEqual(1, stats.All.Count);
            Assert.AreEqual(0.8f, stats.GetScale(StatId.HeldItemSwayScale), 1e-4f);
        }

        [Test]
        public void Remove_DropsOnlyThatSource()
        {
            var stats = new StatModifiers();
            stats.Set("a", StatId.MaxHealth, add: 20f);
            stats.Set("a", StatId.MaxStamina, add: 10f);
            stats.Set("b", StatId.MaxHealth, add: 5f);

            Assert.AreEqual(2, stats.Remove("a"));

            Assert.AreEqual(105f, stats.Get(StatId.MaxHealth, 100f), 1e-4f);
            Assert.AreEqual(100f, stats.Get(StatId.MaxStamina, 100f), 1e-4f);
            Assert.IsFalse(stats.Remove("a", StatId.MaxHealth));
        }

        [Test]
        public void Changed_FiresOnEveryChangeAndNotOnAnEmptyOne()
        {
            var stats = new StatModifiers();
            int changes = 0;
            stats.Changed += () => changes++;

            stats.Set("a", StatId.MaxHealth, add: 20f);
            stats.Remove("missing");
            stats.Remove("a");
            stats.Clear();

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Restore_ReplacesEverything()
        {
            var stats = new StatModifiers();
            stats.Set("old", StatId.MaxHealth, add: 50f);

            stats.Restore(new[] { new StatModifier("saved", StatId.MaxHealth, 10f, 2f) });

            Assert.AreEqual(220f, stats.Get(StatId.MaxHealth, 100f), 1e-4f);
            stats.Restore(null);
            Assert.AreEqual(100f, stats.Get(StatId.MaxHealth, 100f), 1e-4f);
        }

        [Test]
        public void PlayerEffects_BuffsStackOnTopOfTheLayerAndFollowItsChanges()
        {
            var stats = new StatModifiers();
            var effects = new PlayerEffects(stats, 3f);
            int changes = 0;
            effects.Changed += () => changes++;

            stats.Set("upgrade", StatId.MaxStamina, add: 25f);

            Assert.AreEqual(125f, effects.Get(StatId.MaxStamina, 100f), 1e-4f);
            Assert.AreEqual(1, changes);
            effects.Dispose();
        }
    }
}
