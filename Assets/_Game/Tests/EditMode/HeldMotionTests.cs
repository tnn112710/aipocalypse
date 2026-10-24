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
        static AccuracySettings Settings() => new() { AirborneSpread = 2f, AimMultiplier = 0.25f, SettleSpeed = 6f };

        [Test]
        public void OnTheGround_HasNoSpread()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(false, 0f, 1f, 0.016f);
            Assert.AreEqual(0f, accuracy.Spread);
        }

        [Test]
        public void Airborne_Spreads()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(true, 0f, 1f, 0.016f);
            Assert.AreEqual(2f, accuracy.Spread, 0.001f);
        }

        [Test]
        public void Aiming_AndSteadiness_ReduceSpread()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(true, 1f, 1f, 0.016f);
            Assert.AreEqual(0.5f, accuracy.Spread, 0.001f, "aiming keeps 25%");

            accuracy.Reset();
            accuracy.Tick(true, 0f, 0.5f, 0.016f);
            Assert.AreEqual(1f, accuracy.Spread, 0.001f, "a steadier holder halves it");
        }

        [Test]
        public void Spread_SettlesOverTime_AfterLanding()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(true, 0f, 1f, 0.016f);

            accuracy.Tick(false, 0f, 1f, 0.1f);
            Assert.That(accuracy.Spread, Is.InRange(0.4f, 2f), "doesn't snap back on landing");

            for (int i = 0; i < 60; i++)
                accuracy.Tick(false, 0f, 1f, 0.05f);
            Assert.AreEqual(0f, accuracy.Spread, 0.01f);
        }
    }

    public class HeldItemSwayTests
    {
        sealed class FixedSway : IStatProvider
        {
            readonly float _sway;
            public FixedSway(float sway) => _sway = sway;
            public event System.Action Changed
            {
                add { }
                remove { }
            }

            public float Get(StatId stat, float baseValue) => stat == StatId.HeldItemSwayScale ? baseValue * _sway : baseValue;
        }

        const float Frame = 1f / 60f;

        static Vector3 SettledPosition(HeldItemSway sway, HeldSwayInput input)
        {
            Run(sway, input, 120);
            return sway.Position;
        }

        static void Run(HeldItemSway sway, HeldSwayInput input, int frames)
        {
            for (int i = 0; i < frames; i++)
                sway.Tick(input, Frame);
        }

        static HeldSwaySettings NoBreathing() => new() { BreathPosition = Vector3.zero, BreathRoll = 0f };
        static AimSwaySettings NoAimBreathing() => new() { Breath = Vector2.zero };
        static HeldItemSway Calm() => new(NoBreathing(), NoAimBreathing(), new BaseStats());

        // Largest sideways / up-down aim offset over a stretch of frames.
        static Vector2 AimRange(HeldItemSway sway, HeldSwayInput input, int frames, float phasePerFrame = 0f)
        {
            Vector2 range = Vector2.zero;
            for (int i = 0; i < frames; i++)
            {
                input.BobPhase = (input.BobPhase + phasePerFrame) % (2f * Mathf.PI);
                sway.Tick(input, Frame);
                range = Vector2.Max(range, new Vector2(Mathf.Abs(sway.AimOffset.x), Mathf.Abs(sway.AimOffset.y)));
            }
            return range;
        }

        [Test]
        public void TurningRight_LeavesItemBehindToTheLeft()
        {
            var sway = Calm();
            Vector3 position = SettledPosition(sway, new HeldSwayInput { LookDegrees = new Vector2(100f * Frame, 0f) });
            Assert.Less(position.x, -0.001f);
        }

        [Test]
        public void WalkingForward_PullsItemBack_AndStopsWhenStill()
        {
            var sway = Calm();
            Vector3 walking = SettledPosition(sway, new HeldSwayInput { LocalVelocity = new Vector3(0f, 0f, 3.5f) });
            Assert.Less(walking.z, -0.005f);

            Vector3 still = SettledPosition(sway, default);
            Assert.AreEqual(0f, still.magnitude, 0.0005f);
        }

        [Test]
        public void Aiming_CalmsTheSway_ButKeepsMostOfTheMoveLag()
        {
            var input = new HeldSwayInput { LocalVelocity = new Vector3(3f, 0f, 3f), LookDegrees = new Vector2(1f, 0.5f) };
            float hip = SettledPosition(Calm(), input).magnitude;
            input.AimWeight = 1f;
            float aimed = SettledPosition(Calm(), input).magnitude;
            Assert.Less(aimed, hip * 0.9f);
            Assert.Greater(aimed, hip * 0.4f, "moving while aiming still sways the model");
        }

        [Test]
        public void SwayStat_ScalesAllMotion()
        {
            var input = new HeldSwayInput { LocalVelocity = new Vector3(0f, 0f, 3.5f), BobPhase = 1f, BobIntensity = 1f };
            Vector3 normal = SettledPosition(new HeldItemSway(new HeldSwaySettings(), new AimSwaySettings(), new BaseStats()), input);
            Vector3 steady = SettledPosition(new HeldItemSway(new HeldSwaySettings(), new AimSwaySettings(), new FixedSway(0.5f)), input);
            Assert.AreEqual(normal.magnitude * 0.5f, steady.magnitude, 0.0005f);

            Vector3 none = SettledPosition(new HeldItemSway(new HeldSwaySettings(), new AimSwaySettings(), new FixedSway(0f)), input);
            Assert.AreEqual(Vector3.zero, none);
        }

        [Test]
        public void Landing_KicksTheItemDown_ThenSettles()
        {
            var sway = Calm();
            sway.OnLanded(8f);
            sway.Tick(default, Frame);
            sway.Tick(default, Frame);
            Assert.Less(sway.Position.y, 0f);

            Assert.AreEqual(0f, SettledPosition(sway, default).magnitude, 0.0005f);
        }

        [Test]
        public void AimOffset_IsZero_WhenStillAndCalm()
        {
            var sway = Calm();
            Run(sway, default, 60);
            Assert.AreEqual(Vector2.zero, sway.AimOffset);
            Assert.AreEqual(Quaternion.identity, sway.AimRotation);
        }

        [Test]
        public void Breathing_MovesTheAim_AndHarderOutOfBreath()
        {
            float calm = AimRange(new HeldItemSway(NoBreathing(), new AimSwaySettings(), new BaseStats()), default, 900).magnitude;
            Assert.Greater(calm, 0.1f, "the crosshair is alive at rest");

            var tired = new HeldSwayInput { Tiredness = 1f };
            float gasping = AimRange(new HeldItemSway(NoBreathing(), new AimSwaySettings(), new BaseStats()), tired, 900).magnitude;
            Assert.Greater(gasping, calm * 2f);

            tired.IsWinded = true;
            float winded = AimRange(new HeldItemSway(NoBreathing(), new AimSwaySettings(), new BaseStats()), tired, 900).magnitude;
            Assert.Greater(winded, gasping * 1.2f, "winded sways harder still");
        }

        [Test]
        public void WalkingForward_BobsMostlyUpAndDown_StrafingMostlySideways()
        {
            // Tiny velocity: only the direction matters here, not the move drift.
            var forward = new HeldSwayInput { LocalVelocity = new Vector3(0f, 0f, 0.2f), BobIntensity = 1f };
            Vector2 walk = AimRange(Calm(), forward, 240, Mathf.PI * 2f * Frame);
            Assert.Greater(walk.y, walk.x * 1.5f);

            var strafe = new HeldSwayInput { LocalVelocity = new Vector3(0.2f, 0f, 0f), BobIntensity = 1f };
            HeldItemSway strafing = Calm();
            Run(strafing, strafe, 60);
            Vector2 side = AimRange(strafing, strafe, 240, Mathf.PI * 2f * Frame);
            Assert.Greater(side.x, side.y * 1.5f);
        }

        [Test]
        public void Strafing_DriftsTheAimAgainstTheMove_ThenSettles()
        {
            var sway = Calm();
            Run(sway, new HeldSwayInput { LocalVelocity = new Vector3(3.5f, 0f, 0f) }, 120);
            Assert.Less(sway.AimOffset.x, -0.1f, "strafing right trails the aim left");

            Run(sway, default, 180);
            Assert.AreEqual(0f, sway.AimOffset.magnitude, 0.01f);
        }

        [Test]
        public void Landing_KicksTheAimDown_HarderFromHigher_ThenSettles()
        {
            var soft = Calm();
            soft.OnLanded(4f);
            Vector2 small = AimRange(soft, default, 30);

            var hard = Calm();
            hard.OnLanded(12f);
            Vector2 big = AimRange(hard, default, 30);
            Assert.Greater(big.y, small.y);
            Assert.That(big.y, Is.InRange(2.5f, 4.5f), "about the max dip");

            Run(hard, default, 120);
            Assert.AreEqual(0f, hard.AimOffset.magnitude, 0.02f);
        }

        [Test]
        public void SlowLanding_LeavesTheAimAlone()
        {
            var sway = Calm();
            sway.OnLanded(1f);
            Assert.AreEqual(0f, AimRange(sway, default, 30).magnitude, 0.0001f);
        }

        [Test]
        public void Airborne_DriftsTheAim()
        {
            Vector2 air = AimRange(Calm(), new HeldSwayInput { IsAirborne = true }, 240);
            Assert.Greater(air.magnitude, 0.5f);
        }

        [Test]
        public void Aiming_KeepsTheWalkBob_AndBreathing()
        {
            var input = new HeldSwayInput { LocalVelocity = new Vector3(0f, 0f, 0.2f), BobIntensity = 1f };
            float hip = AimRange(new HeldItemSway(NoBreathing(), new AimSwaySettings(), new BaseStats()), input, 600, 0.2f).magnitude;
            input.AimWeight = 1f;
            float aimed = AimRange(new HeldItemSway(NoBreathing(), new AimSwaySettings(), new BaseStats()), input, 600, 0.2f).magnitude;
            Assert.Greater(aimed, hip * 0.8f, "the sights bob while walking, like at the hip");

            float breathing = AimRange(new HeldItemSway(NoBreathing(), new AimSwaySettings(), new BaseStats()), new HeldSwayInput { AimWeight = 1f }, 900).magnitude;
            Assert.Greater(breathing, 0.05f, "the sights still breathe");
        }

        [Test]
        public void RaisingTheSights_OvershootsUp_ThenSettles()
        {
            var sway = Calm();
            var aiming = new HeldSwayInput { AimWeight = 1f, IsAiming = true };
            Vector2 kick = AimRange(sway, aiming, 20);
            Assert.Greater(kick.y, 0.2f);

            Run(sway, aiming, 120);
            Assert.AreEqual(0f, sway.AimOffset.magnitude, 0.01f);
        }

        [Test]
        public void AimOffset_IsClamped()
        {
            var sway = new HeldItemSway(NoBreathing(), new AimSwaySettings { MaxOffset = 1f }, new BaseStats());
            sway.OnLanded(30f);
            Vector2 range = AimRange(sway, new HeldSwayInput { IsAirborne = true, LookDegrees = new Vector2(10f, 0f) }, 60);
            Assert.LessOrEqual(range.magnitude, 1.42f);
        }

        [Test]
        public void TheModel_OnlyRolls()
        {
            var sway = new HeldItemSway(new HeldSwaySettings(), new AimSwaySettings(), new BaseStats());
            Run(sway, new HeldSwayInput { LocalVelocity = new Vector3(3f, 0f, 3f), LookDegrees = new Vector2(2f, 1f), BobPhase = 1f, BobIntensity = 1f }, 30);
            Vector3 forward = sway.Rotation * Vector3.forward;
            Assert.AreEqual(1f, forward.z, 0.00001f, "pitch and yaw come from the aim offset, so the barrel stays on the crosshair");
        }
    }

    public class PlayerAimTests
    {
        static readonly float Tan30 = Mathf.Tan(30f * Mathf.Deg2Rad);

        [Test]
        public void ScreenPosition_CentreIsZero_AndTheFovEdgeIsOne()
        {
            Assert.AreEqual(Vector2.zero, PlayerAim.ScreenPosition(Vector3.forward, Tan30));

            Vector3 edgeUp = Quaternion.Euler(-30f, 0f, 0f) * Vector3.forward;
            Assert.AreEqual(1f, PlayerAim.ScreenPosition(edgeUp, Tan30).y, 0.0001f);
        }

        [Test]
        public void ScreenRadius_MatchesTheAngle_AndGrowsWithZoom()
        {
            Assert.AreEqual(1f, PlayerAim.ScreenRadius(30f, Tan30), 0.0001f);
            Assert.AreEqual(0f, PlayerAim.ScreenRadius(0f, Tan30));
            Assert.Greater(PlayerAim.ScreenRadius(5f, Tan30 * 0.8f), PlayerAim.ScreenRadius(5f, Tan30), "zoomed in, the same cone covers more screen");
        }

        [Test]
        public void ModelRotation_TurnsFurtherWhenZoomed()
        {
            Vector3 aim = Quaternion.Euler(-1f, 2f, 0f) * Vector3.forward;

            float unzoomed = Quaternion.Angle(Quaternion.identity, PlayerAim.ModelRotation(aim, 1f));
            float zoomed = Quaternion.Angle(Quaternion.identity, PlayerAim.ModelRotation(aim, 0.8f));

            Assert.AreEqual(Vector3.Angle(Vector3.forward, aim), unzoomed, 0.001f);
            Assert.Greater(zoomed, unzoomed);
        }
    }
}
