using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class SanityExposureTests
    {
        const float Max = 100f;
        static readonly SanitySettings Rules = new();
        const float AwakeRate = 100f / 10f; // empty at 18 h awake after 8 h of grace

        static SanityInput Night(float distance, bool inLight = false, bool comfort = false) =>
            new(darkness: 1f, distanceOutsideLight: distance, inLight: inLight, nearComfort: comfort);

        static SanityInput Day(bool inLight = false) => new(darkness: 0f, distanceOutsideLight: inLight ? 0f : 150f, inLight: inLight, nearComfort: false);

        static float Rate(in SanityInput input, float hoursAwake) => SanityExposure.RatePerHour(input, hoursAwake, Rules, Max);

        [Test]
        public void Awake_NoDrainForEightHours()
        {
            Assert.AreEqual(0f, Rate(Day(), 7.9f), 1e-4f);
        }

        [Test]
        public void Awake_EmptiesAtEighteenHours()
        {
            float lost = 0f;
            for (float h = 0f; h < 18f; h += 0.01f)
                lost -= Rate(Day(), h) * 0.01f;

            Assert.AreEqual(100f, lost, 0.5f);
        }

        [Test]
        public void Darkness_NothingByDay()
        {
            Assert.AreEqual(Rate(Day(), 10f), -AwakeRate, 1e-3f, "only the lack of sleep");
        }

        [Test]
        public void Darkness_FasterFartherFromLight()
        {
            float near = Rate(Night(10f), 0f);
            float mid = Rate(Night(75f), 0f);
            float far = Rate(Night(150f), 0f);

            Assert.Less(near, 0f);
            Assert.Less(mid, near);
            Assert.Less(far, mid);
            Assert.AreEqual(-25f, far, 1e-3f);
            Assert.AreEqual(-25f, Rate(Night(400f), 0f), 1e-3f, "full rate past the full-drain distance");
        }

        [Test]
        public void Darkness_NothingAtTheLightsEdge()
        {
            Assert.AreEqual(0f, Rate(Night(0f), 0f), 1e-4f);
        }

        [Test]
        public void Darkness_FasterThanLackOfSleep()
        {
            SanityExposure.Rates(Night(150f), 10f, Rules, Max, out float drain, out _);
            float awakeOnly = -Rate(Day(), 10f);

            Assert.Greater(drain - awakeOnly, awakeOnly, "the darkness part outweighs the lack of sleep");
        }

        [Test]
        public void Light_RecoversDayAndNight()
        {
            Assert.AreEqual(3.3f, Rate(Night(0f, inLight: true), 2f), 1e-3f);
            Assert.AreEqual(3.3f, Rate(Day(inLight: true), 2f), 1e-3f);
        }

        [Test]
        public void Light_OnlySlowsTheLossLateAtNight()
        {
            Assert.AreEqual(3.3f - AwakeRate, Rate(Night(0f, inLight: true), 10f), 1e-3f);
        }

        [Test]
        public void Comfort_SlowsOnlyTheDarknessDrain()
        {
            float alone = Rate(Night(150f), 10f);
            float withDog = Rate(Night(150f, comfort: true), 10f);
            float awake = AwakeRate;

            Assert.AreEqual(-(25f * 0.6f + awake), withDog, 1e-3f);
            Assert.AreEqual(-(25f + awake), alone, 1e-3f);
        }
    }
}
