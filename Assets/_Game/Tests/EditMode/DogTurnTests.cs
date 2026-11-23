using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Turning to face something: a short walked arc for a big turn, on the spot for a small one.
    public sealed class DogTurnTests
    {
        [Test]
        public void Arc_OnlyForATurnPastTheArcAngle()
        {
            Assert.That(DogTurn.WantsArc(Vector3.forward, Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, 35f), Is.True);
            Assert.That(DogTurn.WantsArc(Vector3.forward, Quaternion.Euler(0f, 20f, 0f) * Vector3.forward, 35f), Is.False);
        }

        [Test]
        public void Arc_LandsAStepAheadInTheNewDirection()
        {
            Vector3 landing = DogTurn.Landing(new Vector3(10f, 2f, 10f), new Vector3(0f, 0.5f, 1f), 1.2f);
            Assert.That(landing.x, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(landing.z, Is.EqualTo(11.2f).Within(1e-4f));
            Assert.That(landing.y, Is.EqualTo(2f).Within(1e-4f), "flat");
        }

        [Test]
        public void Arc_KeepsNearItsSpotOnceAwayFromIt()
        {
            Vector3 spot = Vector3.zero;
            Assert.That(DogTurn.KeepsNear(new Vector3(0.5f, 0f, 0f), new Vector3(1.6f, 0f, 0f), spot), Is.True, "close to the spot: free to step");
            Assert.That(DogTurn.KeepsNear(new Vector3(3f, 0f, 0f), new Vector3(2f, 0f, 0.8f), spot), Is.True, "stepping back toward it");
            Assert.That(DogTurn.KeepsNear(new Vector3(3f, 0f, 0f), new Vector3(4.2f, 0f, 0f), spot), Is.False, "stepping further away");
        }
    }
}
