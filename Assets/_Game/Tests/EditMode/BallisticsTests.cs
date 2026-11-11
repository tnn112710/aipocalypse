using MB13.Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class BallisticsTests
    {
        const float Gravity = 9.81f;

        static readonly Vector3 From = new(0f, 1.4f, 0f);

        [Test]
        public void LowAngle_LandsOnTheTarget_AtTheGivenSpeed()
        {
            var to = new Vector3(12f, 1.1f, 9f);

            Assert.IsTrue(Ballistics.TryLowAngle(From, to, 24f, Gravity, out Vector3 velocity, out float time));

            Assert.AreEqual(24f, velocity.magnitude, 0.01f);
            Assert.Less(Vector3.Distance(to, Ballistics.PointAt(From, velocity, Gravity, time)), 0.02f);
            Assert.Less(Mathf.Asin(velocity.normalized.y) * Mathf.Rad2Deg, 20f, "the flat one of the two angles");
        }

        [Test]
        public void LowAngle_OutOfReach_Fails()
        {
            Assert.IsFalse(Ballistics.TryLowAngle(From, new Vector3(200f, 0f, 0f), 24f, Gravity, out _, out _));
        }

        [Test]
        public void AtAngle_LandsOnTheTarget_LeavingAtThatAngle()
        {
            var to = new Vector3(-8f, 0.5f, 14f);

            Assert.IsTrue(Ballistics.TryAtAngle(From, to, 45f, Gravity, out Vector3 velocity, out float time));

            Assert.AreEqual(45f, Mathf.Asin(velocity.normalized.y) * Mathf.Rad2Deg, 0.05f);
            Assert.Less(Vector3.Distance(to, Ballistics.PointAt(From, velocity, Gravity, time)), 0.02f);
        }

        [Test]
        public void AtAngle_SteeperLobsFlyLonger()
        {
            var to = new Vector3(15f, 1f, 0f);
            Ballistics.TryAtAngle(From, to, 35f, Gravity, out _, out float shallow);
            Ballistics.TryAtAngle(From, to, 60f, Gravity, out _, out float steep);

            Assert.Greater(steep, shallow);
        }

        [Test]
        public void AtAngle_TargetAboveTheThrowLine_Fails()
        {
            // 10 m away and 12 m up: a 30 degree throw never gets there.
            Assert.IsFalse(Ballistics.TryAtAngle(From, new Vector3(10f, 13.4f, 0f), 30f, Gravity, out _, out _));
        }

        [Test]
        public void Aim_WithNoSpreadAndFullLead_IsWhereTheTargetWillBe()
        {
            RangedSettings settings = ScriptableObject.CreateInstance<RangedSettings>();
            settings.Spread = 0f;
            settings.Lead = Vector2.one;

            Vector3 aim = RangedAim.Aim(settings, From, new Vector3(10f, 1f, 0f), new Vector3(0f, 0f, 4f), 0.5f);

            Assert.Less(Vector3.Distance(new Vector3(10f, 1f, 2f), aim), 0.001f);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Aim_ErrorStaysInsideTheSpreadCone()
        {
            RangedSettings settings = ScriptableObject.CreateInstance<RangedSettings>();
            settings.Spread = 5f;
            settings.Lead = Vector2.zero;
            var point = new Vector3(20f, 1.4f, 0f);
            float limit = 20f * Mathf.Tan(5f * Mathf.Deg2Rad) + 0.01f;

            for (int i = 0; i < 50; i++)
                Assert.LessOrEqual(Vector3.Distance(point, RangedAim.Aim(settings, From, point, Vector3.zero, 1f)), limit);
            Object.DestroyImmediate(settings);
        }
    }
}
