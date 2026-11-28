using System;
using MB13.Aipocalypse.Core.UI;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class SanityGlitchTests
    {
        const float Step = 1f / 60f;

        [Test]
        public void ClockLies_NeverAboveTheThreshold()
        {
            for (float sanity = 0.41f; sanity <= 1f; sanity += 0.1f)
                for (float t = 0f; t < 120f; t += Step)
                    Assert.IsFalse(SanityGlitch.ClockLies(sanity, t, 0.4f, out _, out _), $"sanity {sanity} at {t}");
        }

        [Test]
        public void ClockLies_HappensAndLastsAtMostSixTenthsOfASecond()
        {
            float run = 0f;
            float longest = 0f;
            int lies = 0;
            bool was = false;
            for (float t = 0f; t < 120f; t += Step)
            {
                bool lies_ = SanityGlitch.ClockLies(0f, t, 0.4f, out int hour, out int minute);
                if (lies_)
                {
                    Assert.That(hour, Is.InRange(0, 23));
                    Assert.That(minute, Is.InRange(0, 59));
                    run += Step;
                    if (!was)
                        lies++;
                }
                else
                    run = 0f;
                longest = Mathf.Max(longest, run);
                was = lies_;
            }
            Assert.Greater(lies, 3, "an empty mind sees the clock lie now and then");
            Assert.LessOrEqual(longest, 0.6f + Step * 1.5f);
        }

        [Test]
        public void FakeBlips_NoneAboveTheThresholdAndAtMostThreeInsideTheDisc()
        {
            Span<Vector2> blips = stackalloc Vector2[8];
            for (float t = 0f; t < 30f; t += 0.1f)
            {
                Assert.AreEqual(0, SanityGlitch.FakeBlips(0.25f, t, 0.2f, blips));
                int count = SanityGlitch.FakeBlips(0f, t, 0.2f, blips);
                Assert.That(count, Is.InRange(0, 3));
                for (int i = 0; i < count; i++)
                    Assert.LessOrEqual(blips[i].magnitude, 1f);
            }
            Assert.Greater(SanityGlitch.FakeBlips(0f, 5f, 0.2f, blips), 0);
        }

        [Test]
        public void CompassSwapped_NeverAboveTheThresholdAndAtMostASecond()
        {
            float run = 0f;
            float longest = 0f;
            for (float t = 0f; t < 120f; t += Step)
            {
                Assert.IsFalse(SanityGlitch.CompassSwapped(0.3f, t, 0.2f));
                run = SanityGlitch.CompassSwapped(0f, t, 0.2f) ? run + Step : 0f;
                longest = Mathf.Max(longest, run);
            }
            Assert.Greater(longest, 0f);
            Assert.LessOrEqual(longest, 1f + Step * 1.5f);
        }

        [Test]
        public void Flicker_IsOneAboveTheThresholdAndNeverBelowTheFloor()
        {
            float lowest = 1f;
            for (float t = 0f; t < 60f; t += Step)
            {
                Assert.AreEqual(1f, SanityGlitch.Flicker(0.5f, t, 0.4f));
                float alpha = SanityGlitch.Flicker(0f, t, 0.4f);
                Assert.GreaterOrEqual(alpha, 0.35f);
                Assert.LessOrEqual(alpha, 1f);
                lowest = Mathf.Min(lowest, alpha);
            }
            Assert.Less(lowest, 0.9f, "it does flicker");
        }
    }
}
