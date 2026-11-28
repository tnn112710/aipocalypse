using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    // Stepping out of every safe light at night: a short look and a sound (docs/technicals/NEEDS.md).
    public sealed class LightEdgeCueTests
    {
        static SanityEffectSettings Effects() => new() { LeftLightDarkness = 0.5f, LeftLightRise = 0.4f, LeftLightFade = 3f, LeftLightCooldown = 10f };

        [Test]
        public void SteppingOutAtNight_CuesOnce()
        {
            var cue = new LightEdgeCue();
            SanityEffectSettings effects = Effects();

            Assert.IsFalse(cue.Advance(true, 1f, 0.1f, effects));
            Assert.IsTrue(cue.Advance(false, 1f, 0.1f, effects), "out of the light in the dark");
            Assert.IsFalse(cue.Advance(false, 1f, 0.1f, effects), "still out: once, not every frame");
        }

        [Test]
        public void ByDay_OrOnTheFirstLook_NoCue()
        {
            SanityEffectSettings effects = Effects();
            var day = new LightEdgeCue();
            day.Advance(true, 0.2f, 0.1f, effects);
            Assert.IsFalse(day.Advance(false, 0.2f, 0.1f, effects), "by day the dark doesn't drain");

            var spawned = new LightEdgeCue();
            Assert.IsFalse(spawned.Advance(false, 1f, 0.1f, effects), "starting outside (a load, a respawn) isn't stepping out");
        }

        [Test]
        public void WalkingAlongTheEdge_WaitsForTheCooldown()
        {
            var cue = new LightEdgeCue();
            SanityEffectSettings effects = Effects();
            cue.Advance(true, 1f, 0.1f, effects);
            Assert.IsTrue(cue.Advance(false, 1f, 0.1f, effects));
            cue.Advance(true, 1f, 2f, effects);
            Assert.IsFalse(cue.Advance(false, 1f, 0.1f, effects), "2 s later: inside the 10 s cooldown");
            cue.Advance(true, 1f, 9f, effects);
            Assert.IsTrue(cue.Advance(false, 1f, 0.1f, effects), "after the cooldown it cues again");
        }

        [Test]
        public void TheLook_RisesThenFades()
        {
            var cue = new LightEdgeCue();
            SanityEffectSettings effects = Effects();
            cue.Advance(true, 1f, 0.1f, effects);
            cue.Advance(false, 1f, 0.1f, effects);
            Assert.AreEqual(0f, cue.Strength, 1e-4f, "starts from nothing");

            cue.Advance(false, 1f, 0.2f, effects);
            Assert.AreEqual(0.5f, cue.Strength, 1e-3f, "half way up the 0.4 s rise");
            cue.Advance(false, 1f, 0.2f, effects);
            Assert.AreEqual(1f, cue.Strength, 1e-3f);
            cue.Advance(false, 1f, 1.5f, effects);
            Assert.AreEqual(0.5f, cue.Strength, 1e-3f, "half way down the 3 s fade");
            cue.Advance(false, 1f, 2f, effects);
            Assert.AreEqual(0f, cue.Strength, 1e-4f, "gone: it marks the moment, it doesn't stay on");
        }
    }
}
