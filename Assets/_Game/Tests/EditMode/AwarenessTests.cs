using MB13.Aipocalypse.Core.EnemySystem;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public class AwarenessTests
    {
        [Test]
        public void Notice_AlertsOnce()
        {
            var awareness = new Awareness();
            Assert.That(awareness.Alerted, Is.False);
            Assert.That(awareness.Notice(10f), Is.True, "the first time");
            Assert.That(awareness.Notice(11f), Is.False, "already knows");
            Assert.That(awareness.Alerted, Is.True);
        }

        [Test]
        public void Sense_SeeingAlerts_AndNotSeeingForLoseSightTimeForgets()
        {
            var awareness = new Awareness();
            Assert.That(awareness.Sense(false, 0f, 6f), Is.Zero, "nothing to forget");
            Assert.That(awareness.Sense(true, 1f, 6f), Is.EqualTo(1), "spotted");
            Assert.That(awareness.Sense(true, 2f, 6f), Is.Zero, "still in sight");
            Assert.That(awareness.Sense(false, 7.9f, 6f), Is.Zero, "lost it 5.9 s ago");
            Assert.That(awareness.Alerted, Is.True);
            Assert.That(awareness.Sense(false, 8.1f, 6f), Is.EqualTo(-1), "6 s without a sight");
            Assert.That(awareness.Alerted, Is.False);
        }

        [Test]
        public void AHit_KeepsItLookingForLoseSightTime()
        {
            var awareness = new Awareness();
            awareness.Notice(0f);
            Assert.That(awareness.Sense(false, 5f, 6f), Is.Zero, "never saw the player, still looking");
            awareness.Notice(5f);
            Assert.That(awareness.Sense(false, 10f, 6f), Is.Zero, "hit again at 5 s");
            Assert.That(awareness.Sense(false, 11.5f, 6f), Is.EqualTo(-1));
        }

        [Test]
        public void Reset_Forgets()
        {
            var awareness = new Awareness();
            awareness.Notice(0f);
            awareness.Reset();
            Assert.That(awareness.Alerted, Is.False);
        }
    }
}
