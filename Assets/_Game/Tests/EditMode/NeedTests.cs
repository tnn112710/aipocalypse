using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class NeedTests
    {
        // The spec's Hunger tiers, without their stat changes.
        internal static NeedSettings HungerSettings() => new()
        {
            BaseMax = 100f,
            MaxStat = StatId.MaxHunger,
            DrainStat = StatId.HungerDrain,
            PenaltyScaleStat = StatId.HungerPenaltyScale,
            Tiers = new[]
            {
                new NeedTier { Severity = NeedSeverity.Good, From = 0.75f },
                new NeedTier { Severity = NeedSeverity.Normal, From = 0.5f },
                new NeedTier { Severity = NeedSeverity.Low, From = 0.25f },
                new NeedTier { Severity = NeedSeverity.VeryLow, From = 0f },
                new NeedTier { Severity = NeedSeverity.Empty, From = 0f },
            },
        };

        static Need Hunger() => new(HungerSettings(), 100f);

        [Test]
        public void New_StartsFull()
        {
            Need need = Hunger();

            Assert.AreEqual(1f, need.Normalized);
            Assert.AreEqual(NeedSeverity.Good, need.Severity);
        }

        [Test]
        public void Tier_PicksTheRowCoveringTheShare()
        {
            Need need = Hunger();

            need.Set(80f);
            Assert.AreEqual(NeedSeverity.Good, need.Severity);
            need.Set(50f);
            Assert.AreEqual(NeedSeverity.Normal, need.Severity);
            need.Set(49.9f);
            Assert.AreEqual(NeedSeverity.Low, need.Severity);
            need.Set(0.5f);
            Assert.AreEqual(NeedSeverity.VeryLow, need.Severity);
            need.Set(0f);
            Assert.AreEqual(NeedSeverity.Empty, need.Severity);
        }

        [Test]
        public void TierChanged_FiresOnlyOnACrossing()
        {
            Need need = Hunger();
            int changes = 0;
            need.TierChanged += _ => changes++;

            need.Set(70f);
            need.Set(65f);

            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SetMax_KeepsTheShare()
        {
            Need need = Hunger();
            need.Set(50f);

            need.SetMax(150f);

            Assert.AreEqual(150f, need.Max);
            Assert.AreEqual(75f, need.Current, 1e-4f);
        }

        [Test]
        public void Add_ClampsBetweenZeroAndMax()
        {
            Need need = Hunger();

            need.Add(-500f);
            Assert.AreEqual(0f, need.Current);
            need.Add(500f);
            Assert.AreEqual(100f, need.Current);
        }

        [Test]
        public void NoTiers_HasNoTierAndReadsNormal()
        {
            var need = new Need(new NeedSettings(), 100f);

            Assert.IsNull(need.Tier);
            Assert.AreEqual(NeedSeverity.Normal, need.Severity);
        }
    }
}
