using MB13.Aipocalypse.Core.Entities;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    // Walking away from a one-off clip (a dog's act, a robot's emote).
    public sealed class WalkOffTests
    {
        [Test]
        public void Due_OnlyWhenMovingFasterThanTheLeaveSpeed()
        {
            Assert.That(WalkOff.Due(moving: 2f, leaveSpeed: 1.5f, secondsIn: 1f, settle: 0.5f), Is.True);
            Assert.That(WalkOff.Due(moving: 1f, leaveSpeed: 1.5f, secondsIn: 1f, settle: 0.5f), Is.False);
        }

        [Test]
        public void Due_NotWhileStillSlowingDownIntoTheClip()
        {
            Assert.That(WalkOff.Due(moving: 3f, leaveSpeed: 1.5f, secondsIn: 0.2f, settle: 0.5f), Is.False);
        }
    }
}
