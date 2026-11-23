using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Enemies fight back at the dog only when provoked, only within reach, and only for a short window (no chasing).
    public sealed class EnemyRetaliationTests
    {
        static readonly Vector3 Self = Vector3.zero;

        [Test]
        public void Retaliate_WithoutWindowNeverTargetsDog()
        {
            Assert.That(EnemyBrain.KeepsRetaliating(Self, new Vector3(1f, 0f, 0f), 2f, 5f, float.NegativeInfinity, true), Is.False);
        }

        [Test]
        public void Retaliate_InReachKeepsTheDog()
        {
            Assert.That(EnemyBrain.KeepsRetaliating(Self, new Vector3(1.5f, 0f, 0f), 2f, 1f, 2.5f, true), Is.True);
        }

        [Test]
        public void Retaliate_DogOutOfReachDropsTheWindow()
        {
            Assert.That(EnemyBrain.KeepsRetaliating(Self, new Vector3(4f, 0f, 0f), 2f, 1f, 2.5f, true), Is.False, "it ran off: no chase");
        }

        [Test]
        public void Retaliate_WindowExpires()
        {
            Assert.That(EnemyBrain.KeepsRetaliating(Self, new Vector3(1f, 0f, 0f), 2f, 2.6f, 2.5f, true), Is.False);
            Assert.That(EnemyBrain.KeepsRetaliating(Self, new Vector3(1f, 0f, 0f), 2f, 1f, 2.5f, false), Is.False, "down: not targetable");
        }

        [Test]
        public void TargetKind_DogIsAppendedLast()
        {
            Assert.That((int)TargetKind.Dog, Is.EqualTo(3));
        }
    }
}
