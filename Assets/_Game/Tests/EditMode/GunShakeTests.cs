using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class GunShakeTests
    {
        GunShakeSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<GunShakeSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [TestCase(false, 3, 0)]
        [TestCase(true, 4, 2)]
        public void Tick_LandsEachShakeOnce_StubbornRepairsHaveHardOnes(bool stubborn, int expected, int expectedHard)
        {
            var shake = new GunShake();
            shake.Start(_settings, stubborn);
            int landed = 0, hard = 0;
            for (int i = 0; i <= 400; i++)
            {
                // Misses push the progress back: a shake that already landed doesn't land again.
                float progress = i == 200 ? 0.1f : i / 400f;
                if (!shake.Tick(progress, out bool isHard))
                    continue;
                landed++;
                if (isHard)
                    hard++;
            }
            Assert.That(landed, Is.EqualTo(expected));
            Assert.That(hard, Is.EqualTo(expectedHard));
        }

        [Test]
        public void Apply_TiltsWhileClearing_AndIsAtRestAtBothEnds()
        {
            var shake = new GunShake();
            shake.Start(_settings, false);
            foreach (float progress in new[] { 0f, 1f })
            {
                shake.Tick(progress, out _);
                Vector3 position = Vector3.zero;
                Quaternion rotation = Quaternion.identity;
                shake.Apply(ref position, ref rotation);
                Assert.That(position.magnitude, Is.LessThan(0.0001f), "progress " + progress);
                Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.LessThan(0.01f), "progress " + progress);
            }
            // Between two shakes the gun holds its tilt.
            float between = (_settings.StartAt(0, 3) + _settings.ShakeShare + _settings.StartAt(1, 3)) * 0.5f - _settings.ShakeShare * 0.2f;
            shake.Tick(between, out _);
            Vector3 held = Vector3.zero;
            Quaternion turn = Quaternion.identity;
            shake.Apply(ref held, ref turn);
            Assert.That(Quaternion.Angle(turn, Quaternion.Euler(_settings.TiltEuler)), Is.LessThan(3f));
        }

        [Test]
        public void Finish_EasesBackFromThePoseItWasCutAt()
        {
            var shake = new GunShake();
            shake.Start(_settings, false);
            shake.Tick(0.5f, out _);
            Vector3 before = Vector3.zero;
            Quaternion beforeTurn = Quaternion.identity;
            shake.Apply(ref before, ref beforeTurn);

            shake.Finish();
            Assert.That(shake.IsPlaying, Is.False);
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            shake.Apply(ref position, ref rotation);
            Assert.That(Vector3.Distance(position, before), Is.LessThan(0.0001f), "no jump when the repair ends");

            shake.TickReturn(_settings.ReturnTime * 0.5f);
            position = Vector3.zero;
            rotation = Quaternion.identity;
            shake.Apply(ref position, ref rotation);
            Assert.That(position.magnitude, Is.LessThan(before.magnitude));

            shake.TickReturn(_settings.ReturnTime);
            position = Vector3.zero;
            rotation = Quaternion.identity;
            shake.Apply(ref position, ref rotation);
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity));
        }
    }
}
