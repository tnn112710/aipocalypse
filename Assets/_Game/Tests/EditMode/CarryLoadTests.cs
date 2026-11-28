using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class CarryLoadTests
    {
        static readonly CarryLoadSettings Settings = new();

        static CarryLoad At(float weight) => CarryLoad.Evaluate(weight, 25f, 35f, Settings);

        [Test]
        public void Evaluate_IsLightUnderEightyPercentOfComfortable()
        {
            CarryLoad load = At(19f);

            Assert.AreEqual(LoadState.Light, load.State);
            Assert.AreEqual(1f, load.SpeedMultiplier);
            Assert.AreEqual(1f, load.CostMultiplier);
        }

        [Test]
        public void Evaluate_IsNearFromEightyPercentUpToComfortable()
        {
            Assert.AreEqual(LoadState.NearComfortable, At(21f).State);
            Assert.AreEqual(LoadState.NearComfortable, At(25f).State);
            Assert.AreEqual(1f, At(25f).SpeedMultiplier);
        }

        [Test]
        public void Evaluate_SlowsLinearlyBetweenTheLimits()
        {
            CarryLoad load = At(30f);

            Assert.AreEqual(LoadState.Heavy, load.State);
            Assert.AreEqual(0.8f, load.SpeedMultiplier, 1e-4f);
            Assert.AreEqual(1.25f, load.CostMultiplier, 1e-4f);
        }

        [Test]
        public void Evaluate_CrawlsAtAndAboveMax()
        {
            foreach (float weight in new[] { 35f, 60f })
            {
                CarryLoad load = At(weight);
                Assert.AreEqual(LoadState.Overloaded, load.State, weight.ToString());
                Assert.AreEqual(0.15f, load.SpeedMultiplier, 1e-4f);
                Assert.AreEqual(1.5f, load.CostMultiplier, 1e-4f);
            }
        }

        [Test]
        public void Limits_UpgradesMoveBothLimits()
        {
            var stats = new StatModifiers();
            stats.Set("upgrade.strong", StatId.ComfortableWeight, add: 5f);

            CarryLoad.Limits(stats, 25f, Settings, out float comfortable, out float max);

            Assert.AreEqual(30f, comfortable, 1e-4f);
            Assert.AreEqual(40f, max, 1e-4f);
            Assert.AreEqual(LoadState.NearComfortable, CarryLoad.Evaluate(30f, comfortable, max, Settings).State);
        }

        [Test]
        public void Limits_MarginUpgradeMovesOnlyMax()
        {
            var stats = new StatModifiers();
            stats.Set("upgrade.frame", StatId.WeightMargin, add: 5f);

            CarryLoad.Limits(stats, 25f, Settings, out float comfortable, out float max);

            Assert.AreEqual(25f, comfortable, 1e-4f);
            Assert.AreEqual(40f, max, 1e-4f);
        }

        [Test]
        public void Evaluate_DisabledIsAlwaysLight()
        {
            var off = new CarryLoadSettings { Enabled = false };

            CarryLoad load = CarryLoad.Evaluate(80f, 25f, 35f, off);

            Assert.AreEqual(LoadState.Light, load.State);
            Assert.AreEqual(1f, load.SpeedMultiplier);
        }
    }
}
