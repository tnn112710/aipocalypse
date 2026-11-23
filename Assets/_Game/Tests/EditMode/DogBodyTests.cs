using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // What the dog looks at and when it pants (DogLook, DogPant).
    public sealed class DogBodyTests
    {
        static readonly Vector3 Head = new(0f, 1f, 0f);
        static readonly Vector3 Ahead = Vector3.forward;
        static readonly Vector3 PlayerHead = new(0f, 1.6f, 5f);

        static FoeView Foe(int id, Vector3 at) => new(id, at, Vector3.back, false, true, false, false, 1f, 0, true, false);

        [Test]
        public void Look_PrefersItsFightTargetThenAFoeThenThePlayer()
        {
            var foes = new[] { Foe(1, new Vector3(3f, 0f, 6f)), Foe(2, new Vector3(-2f, 0f, 4f)) };
            Assert.That(DogLook.Pick(Head, Ahead, 1, foes, true, PlayerHead, 0f, out Vector3 at, 10f, 2.5f, 100f), Is.True);
            Assert.That(at.x, Is.EqualTo(3f), "its fight target");
            DogLook.Pick(Head, Ahead, -1, foes, true, PlayerHead, 0f, out at, 10f, 2.5f, 100f);
            Assert.That(at.x, Is.EqualTo(-2f), "the nearest foe");
            DogLook.Pick(Head, Ahead, -1, new FoeView[0], true, PlayerHead, 0f, out at, 10f, 2.5f, 100f);
            Assert.That(at, Is.EqualTo(PlayerHead), "the player");
        }

        [Test]
        public void Look_IgnoresThePlayerWhileRunningFarOrBehind()
        {
            var none = new FoeView[0];
            Assert.That(DogLook.Pick(Head, Ahead, -1, none, true, PlayerHead, 6f, out _, 10f, 2.5f, 100f), Is.False, "running");
            Assert.That(DogLook.Pick(Head, Ahead, -1, none, true, new Vector3(0f, 1.6f, 30f), 0f, out _, 10f, 2.5f, 100f), Is.False, "far");
            Assert.That(DogLook.Pick(Head, Ahead, -1, none, true, new Vector3(0f, 1.6f, -5f), 0f, out _, 10f, 2.5f, 100f), Is.False, "behind");
            Assert.That(DogLook.Pick(Head, Ahead, -1, none, false, PlayerHead, 0f, out _, 10f, 2.5f, 100f), Is.False, "no player");
        }

        [Test]
        public void Pant_StartsAfterARunAndFadesAfterItsTime()
        {
            var pant = new DogPant();
            float now = 0f;
            const float dt = 0.1f;
            for (; now < 3f; now += dt)
                pant.Tick(6f, now, dt, 5f, 4f, 12f);
            Assert.That(pant.Level, Is.EqualTo(0f), "3 s of running: not yet");
            for (; now < 5f; now += dt)
                pant.Tick(6f, now, dt, 5f, 4f, 12f);
            for (int i = 0; i < 20; i++, now += dt)
                pant.Tick(0f, now, dt, 5f, 4f, 12f);
            Assert.That(pant.Level, Is.EqualTo(1f), "stopped after a 5 s run: panting");
            for (; now < 20f; now += dt)
                pant.Tick(0f, now, dt, 5f, 4f, 12f);
            Assert.That(pant.Level, Is.EqualTo(0f), "rested");
        }

        [Test]
        public void Pant_ShortDashesDoNotAddUp()
        {
            var pant = new DogPant();
            float now = 0f;
            const float dt = 0.1f;
            for (int dash = 0; dash < 5; dash++)
            {
                for (int i = 0; i < 20; i++, now += dt)
                    pant.Tick(6f, now, dt, 5f, 4f, 12f);
                for (int i = 0; i < 60; i++, now += dt)
                    pant.Tick(0f, now, dt, 5f, 4f, 12f);
            }
            Assert.That(pant.Level, Is.EqualTo(0f), "2 s dashes with 6 s rests");
        }
    }
}
