using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class NeedConditionsTests
    {
        StatModifiers _below;
        Need _hunger;
        NeedConditions _conditions;

        [SetUp]
        public void SetUp()
        {
            NeedSettings settings = NeedTests.HungerSettings();
            settings.Tiers[0].Changes = new[] { new StatChange(StatId.HealthRegen, add: 12f) };
            settings.Tiers[2].Changes = new[] { new StatChange(StatId.HeldItemSwayScale, multiply: 1.25f) };
            _below = new StatModifiers();
            _hunger = new Need(settings, 100f);
            _conditions = new NeedConditions(_below, 0.35f);
            _conditions.Track(_hunger, StatId.HungerPenaltyScale);
        }

        [TearDown]
        public void TearDown() => _conditions.Dispose();

        [Test]
        public void Get_AppliesTheActiveTiersChanges()
        {
            _hunger.Set(40f);
            _conditions.Tick(10f);

            Assert.AreEqual(1.25f, _conditions.GetScale(StatId.HeldItemSwayScale), 1e-4f);
            Assert.AreEqual(5f, _conditions.Get(StatId.WalkSpeed, 5f), "a stat no tier names is untouched");
        }

        [Test]
        public void Get_StacksTierChangesOverTheLayerBelow()
        {
            _below.Set("buff", StatId.HeldItemSwayScale, multiply: 1.1f);
            _hunger.Set(40f);
            _conditions.Tick(10f);

            Assert.AreEqual(1.1f * 1.25f, _conditions.GetScale(StatId.HeldItemSwayScale), 1e-4f);
        }

        [Test]
        public void Get_PenaltyScaleZeroRemovesPenaltiesNotBonuses()
        {
            _below.Set("upgrade", StatId.HungerPenaltyScale, multiply: 0f);

            _conditions.Tick(10f);
            Assert.AreEqual(12f, _conditions.Get(StatId.HealthRegen, 0f), 1e-4f, "the well-fed bonus stays");

            _hunger.Set(40f);
            _conditions.Tick(10f);
            Assert.AreEqual(1f, _conditions.GetScale(StatId.HeldItemSwayScale), 1e-4f);
        }

        [Test]
        public void Get_EasesIntoANewTier()
        {
            _hunger.Set(40f);
            _conditions.Tick(0.1f);
            float easing = _conditions.GetScale(StatId.HeldItemSwayScale);

            Assert.Greater(easing, 1f);
            Assert.Less(easing, 1.25f);
            _conditions.Tick(10f);
            Assert.AreEqual(1.25f, _conditions.GetScale(StatId.HeldItemSwayScale), 1e-4f);
        }

        [Test]
        public void Get_StartsAtTheCurrentTierWithoutEasing()
        {
            Assert.AreEqual(12f, _conditions.Get(StatId.HealthRegen, 0f), 1e-4f);
        }

        [Test]
        public void Get_DisabledNeedAppliesNothing()
        {
            _hunger.Settings.Enabled = false;
            _hunger.Set(40f);
            _conditions.Tick(10f);

            Assert.AreEqual(1f, _conditions.GetScale(StatId.HeldItemSwayScale), 1e-4f);
            Assert.AreEqual(0f, _conditions.Get(StatId.HealthRegen, 0f), 1e-4f);
        }

        [Test]
        public void Update_RaisesChangedOnlyWhenATierChanges()
        {
            int changes = 0;
            _conditions.Changed += () => changes++;
            _hunger.Set(50.0005f);
            _conditions.Tick(10f);
            changes = 0;

            for (int i = 0; i < 100; i++)
            {
                _hunger.Add(-0.00001f);
                _conditions.Tick(0.016f);
            }

            Assert.AreEqual(2, changes, "one crossing into Low: on the crossing and once when its ease ends, nothing per frame");
        }

        [Test]
        public void Changed_FiresAgainWhenTheEaseEnds_SoCachedStatsSeeTheNewRow()
        {
            _hunger.Settings.Tiers[2].Changes = new[] { new StatChange(StatId.MaxStamina, multiply: 0.8f) };
            float cached = _conditions.Get(StatId.MaxStamina, 100f);
            _conditions.Changed += () => cached = _conditions.Get(StatId.MaxStamina, 100f);

            _hunger.Set(40f);
            _conditions.Tick(10f);

            Assert.AreEqual(80f, cached, 1e-3f);

            _hunger.Set(60f);
            _conditions.Tick(10f);

            Assert.AreEqual(100f, cached, 1e-3f, "leaving the tier doesn't leave its row behind");
        }

        [Test]
        public void Changed_FollowsTheLayerBelow()
        {
            int changes = 0;
            _conditions.Changed += () => changes++;

            _below.Set("upgrade", StatId.MaxHunger, add: 50f);

            Assert.AreEqual(1, changes);
        }
    }
}
