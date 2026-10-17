using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.PlayerSystem.Defaults;
using Aipocalypse.Core.Stats;
using Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class GunAccuracyTests
    {
        static AccuracySettings Settings() => new() { MovingSpread = 2f, AirborneSpread = 5f, AimMultiplier = 0.25f, SettleSpeed = 6f };

        [Test]
        public void StandingStill_HasNoSpread()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(0f, false, 0f, 1f, 0.016f);
            Assert.AreEqual(0f, accuracy.Spread);
        }

        [Test]
        public void Spread_ScalesWithSpeed_AndIsWorstInTheAir()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(0.5f, false, 0f, 1f, 0.016f);
            Assert.AreEqual(1f, accuracy.Spread, 0.001f);

            accuracy.Tick(1f, false, 0f, 1f, 0.016f);
            Assert.AreEqual(2f, accuracy.Spread, 0.001f);

            accuracy.Tick(1f, true, 0f, 1f, 0.016f);
            Assert.AreEqual(5f, accuracy.Spread, 0.001f);
        }

        [Test]
        public void Aiming_AndSteadiness_ReduceSpread()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(1f, false, 1f, 1f, 0.016f);
            Assert.AreEqual(0.5f, accuracy.Spread, 0.001f, "aiming keeps 25%");

            accuracy.Reset();
            accuracy.Tick(1f, false, 0f, 0.5f, 0.016f);
            Assert.AreEqual(1f, accuracy.Spread, 0.001f, "a steadier holder halves it");
        }

        [Test]
        public void Spread_SettlesOverTime_AfterStopping()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(1f, true, 0f, 1f, 0.016f);

            accuracy.Tick(0f, false, 0f, 1f, 0.1f);
            Assert.That(accuracy.Spread, Is.InRange(1f, 5f), "doesn't snap back on landing");

            for (int i = 0; i < 60; i++)
                accuracy.Tick(0f, false, 0f, 1f, 0.05f);
            Assert.AreEqual(0f, accuracy.Spread, 0.01f);
        }
    }

    public class HeldItemSwayTests
    {
        sealed class FixedSway : IStatProvider
        {
            readonly float _sway;
            public FixedSway(float sway) => _sway = sway;
            public float Get(StatId stat, float baseValue) => stat == StatId.HeldItemSway ? baseValue * _sway : baseValue;
        }

        const float Frame = 1f / 60f;

        static Vector3 SettledPosition(HeldItemSway sway, HeldSwayInput input)
        {
            for (int i = 0; i < 120; i++)
                sway.Tick(input, Frame);
            return sway.Position;
        }

        static HeldSwaySettings NoBreathing() => new() { BreathPosition = Vector3.zero, BreathEuler = Vector3.zero };

        [Test]
        public void TurningRight_LeavesItemBehindToTheLeft()
        {
            var sway = new HeldItemSway(NoBreathing(), new BaseStats());
            Vector3 position = SettledPosition(sway, new HeldSwayInput { LookDegrees = new Vector2(100f * Frame, 0f) });
            Assert.Less(position.x, -0.001f);
        }

        [Test]
        public void WalkingForward_PullsItemBack_AndStopsWhenStill()
        {
            var sway = new HeldItemSway(NoBreathing(), new BaseStats());
            Vector3 walking = SettledPosition(sway, new HeldSwayInput { LocalVelocity = new Vector3(0f, 0f, 3.5f) });
            Assert.Less(walking.z, -0.005f);

            Vector3 still = SettledPosition(sway, default);
            Assert.AreEqual(0f, still.magnitude, 0.0005f);
        }

        [Test]
        public void Aiming_CalmsTheSway()
        {
            var input = new HeldSwayInput { LocalVelocity = new Vector3(3f, 0f, 3f), LookDegrees = new Vector2(1f, 0.5f) };
            float hip = SettledPosition(new HeldItemSway(NoBreathing(), new BaseStats()), input).magnitude;
            input.AimWeight = 1f;
            float aimed = SettledPosition(new HeldItemSway(NoBreathing(), new BaseStats()), input).magnitude;
            Assert.Less(aimed, hip * 0.5f);
        }

        [Test]
        public void SwayStat_ScalesAllMotion()
        {
            var input = new HeldSwayInput { LocalVelocity = new Vector3(0f, 0f, 3.5f), BobPhase = 1f, BobIntensity = 1f };
            Vector3 normal = SettledPosition(new HeldItemSway(new HeldSwaySettings(), new BaseStats()), input);
            Vector3 steady = SettledPosition(new HeldItemSway(new HeldSwaySettings(), new FixedSway(0.5f)), input);
            Assert.AreEqual(normal.magnitude * 0.5f, steady.magnitude, 0.0005f);

            Vector3 none = SettledPosition(new HeldItemSway(new HeldSwaySettings(), new FixedSway(0f)), input);
            Assert.AreEqual(Vector3.zero, none);
        }

        [Test]
        public void Landing_KicksTheItemDown_ThenSettles()
        {
            var sway = new HeldItemSway(NoBreathing(), new BaseStats());
            sway.OnLanded(8f);
            sway.Tick(default, Frame);
            sway.Tick(default, Frame);
            Assert.Less(sway.Position.y, 0f);

            Assert.AreEqual(0f, SettledPosition(sway, default).magnitude, 0.0005f);
        }
    }
}
