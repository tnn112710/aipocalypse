using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Defaults;
using MB13.Aipocalypse.Core.Stats;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class GunAccuracyTests
    {
        static AccuracySettings Settings() => new()
        {
            AirborneSpread = 2f, AimMultiplier = 0.25f, SettleSpeed = 6f, BloomPerShot = 0.5f, MaxBloom = 3f, BloomRecovery = 4f,
            BloomRecoveryDelay = 0.2f
        };

        static AccuracyInput Input() => new() { Cone = 3f, MoveScale = 1f, AirScale = 1f, Fatigue = 1f, Steadiness = 1f };

        static float SpreadAfter(GunAccuracy accuracy, AccuracyInput input)
        {
            accuracy.Tick(input, 0.016f);
            return accuracy.Spread;
        }

        [Test]
        public void Standing_IsTheCone() => Assert.AreEqual(3f, SpreadAfter(new GunAccuracy(Settings()), Input()), 0.001f);

        [Test]
        public void Moving_ScalesTheCone()
        {
            var input = Input();
            input.MoveScale = 1.5f;
            Assert.AreEqual(4.5f, SpreadAfter(new GunAccuracy(Settings()), input), 0.001f);
        }

        [Test]
        public void Airborne_AddsAirSpread_LessWhileAiming()
        {
            var input = Input();
            input.Cone = 0f;
            input.Airborne = true;
            Assert.AreEqual(2f, SpreadAfter(new GunAccuracy(Settings()), input), 0.001f);

            input.AimWeight = 1f;
            Assert.AreEqual(0.5f, SpreadAfter(new GunAccuracy(Settings()), input), 0.001f, "aiming keeps 25%");
        }

        [Test]
        public void Shots_AddBloom_UpToTheCap_ThenRecover()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(Input(), 0.016f);
            for (int i = 0; i < 10; i++)
                accuracy.AddShot();
            accuracy.Tick(Input(), 0.016f);
            Assert.AreEqual(6f, accuracy.Spread, 0.1f, "cone 3 + bloom capped at 3");

            for (int i = 0; i < 120; i++)
                accuracy.Tick(Input(), 1f / 60f);
            Assert.AreEqual(3f, accuracy.Spread, 0.05f);
        }

        [Test]
        public void Bloom_WaitsForAPause_BeforeRecovering()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(Input(), 0.016f);
            accuracy.AddShot();
            for (int i = 0; i < 6; i++)
                accuracy.Tick(Input(), 1f / 60f);
            Assert.AreEqual(0.5f, accuracy.Bloom, 0.001f, "0.1 s after the shot: still full, so full auto keeps building");

            for (int i = 0; i < 12; i++)
                accuracy.Tick(Input(), 1f / 60f);
            Assert.Less(accuracy.Bloom, 0.5f, "0.3 s after: recovering");
        }

        [Test]
        public void Aiming_RemovesBloom()
        {
            var accuracy = new GunAccuracy(Settings());
            for (int i = 0; i < 4; i++)
                accuracy.AddShot();
            var input = Input();
            input.Cone = 0f;
            input.AimWeight = 1f;
            Assert.AreEqual(0f, SpreadAfter(accuracy, input), 0.001f);
        }

        [Test]
        public void HalfAimed_IsBetweenHipAndAimed()
        {
            var accuracy = new GunAccuracy(Settings());
            var input = Input();
            input.Cone = Mathf.Lerp(3f, 0f, 0.5f);
            input.AimWeight = 0.5f;
            accuracy.Tick(input, 0.016f);
            accuracy.AddShot();
            accuracy.AddShot();
            Assert.That(SpreadAfter(accuracy, input), Is.InRange(1.5f, 2.5f));
        }

        [Test]
        public void Turning_AddsSpread_LessWhileAiming()
        {
            var input = Input();
            input.Turn = 1f;
            Assert.AreEqual(4f, SpreadAfter(new GunAccuracy(Settings()), input), 0.001f, "cone 3 + turning 1");

            input.Cone = 0f;
            input.AimWeight = 1f;
            Assert.AreEqual(0f, SpreadAfter(new GunAccuracy(Settings()), input), 0.001f, "aimed: the aim sway's look lag covers it");
        }

        [Test]
        public void Fatigue_AndSteadiness_Multiply()
        {
            var input = Input();
            input.Fatigue = 1.5f;
            input.Steadiness = 0.5f;
            Assert.AreEqual(2.25f, SpreadAfter(new GunAccuracy(Settings()), input), 0.001f);
        }

        [Test]
        public void Spread_SettlesOverTime_AfterLanding()
        {
            var accuracy = new GunAccuracy(Settings());
            var input = Input();
            input.Cone = 0f;
            input.Airborne = true;
            accuracy.Tick(input, 0.016f);

            input.Airborne = false;
            accuracy.Tick(input, 0.1f);
            Assert.That(accuracy.Spread, Is.InRange(0.01f, 1.99f), "doesn't snap back on landing");

            for (int i = 0; i < 60; i++)
                accuracy.Tick(input, 0.05f);
            Assert.AreEqual(0f, accuracy.Spread, 0.01f);
        }

        [Test]
        public void AddShot_ReturnsTheKick()
        {
            var accuracy = new GunAccuracy(Settings());
            var input = Input();
            accuracy.Tick(input, 0.016f);
            Assert.AreEqual(0.5f, accuracy.AddShot(), 0.001f);

            input.Fatigue = 2f;
            accuracy.Tick(input, 0.016f);
            Assert.AreEqual(1f, accuracy.AddShot(), 0.001f);

            input.AimWeight = 1f;
            accuracy.Tick(input, 0.016f);
            Assert.AreEqual(0f, accuracy.AddShot(), 0.001f);
        }

        [Test]
        public void Reset_ClearsBloomAndSpread()
        {
            var accuracy = new GunAccuracy(Settings());
            accuracy.Tick(Input(), 0.016f);
            accuracy.AddShot();
            accuracy.Tick(Input(), 0.016f);
            accuracy.Reset();
            Assert.AreEqual(3f, SpreadAfter(accuracy, Input()), 0.001f);
        }
    }

    public class RecoilStateTests
    {
        [Test]
        public void Standing_IsOne() => Assert.AreEqual(1f, new RecoilSettings().StateMultiplier(false, false, false), 0.001f);

        [Test]
        public void Crouching_KicksLess()
        {
            var recoil = new RecoilSettings { MovingMultiplier = 1.15f, CrouchMultiplier = 0.8f };
            Assert.AreEqual(0.8f, recoil.StateMultiplier(false, false, true), 0.001f);
            Assert.AreEqual(1.15f * 0.8f, recoil.StateMultiplier(false, true, true), 0.001f, "crouch-walking");
        }

        [Test]
        public void Airborne_WinsOverMoving() =>
            Assert.AreEqual(1.6f, new RecoilSettings { AirborneMultiplier = 1.6f }.StateMultiplier(true, true, false), 0.001f);
    }

    public class AimWhileFiringLevelTests
    {
        [Test]
        public void AimWhileFiringAt_LevelZeroIsLocked_HigherLevelsGetFasterAndSteadier_PastTheEndKeepsTheLast()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerHeldItemTuning>();
            Assert.IsFalse(tuning.AimWhileFiringAt(0).Allowed);
            Assert.IsFalse(tuning.AimWhileFiringAt(-1).Allowed);

            AimWhileFiring first = tuning.AimWhileFiringAt(1);
            AimWhileFiring second = tuning.AimWhileFiringAt(2);
            Assert.IsTrue(first.Allowed);
            Assert.Greater(second.AimSpeedScale, first.AimSpeedScale);
            Assert.Less(second.RecoilScale, first.RecoilScale);
            Assert.Less(second.WobbleScale, first.WobbleScale);
            Assert.AreEqual(tuning.AimWhileFiringAt(3).AimSpeedScale, tuning.AimWhileFiringAt(99).AimSpeedScale);
            Object.DestroyImmediate(tuning);
        }
    }

    public class MoveSpreadScaleTests
    {
        static float Scale(bool sprinting, bool moving, bool crouching)
        {
            var tuning = ScriptableObject.CreateInstance<PlayerHeldItemTuning>();
            float scale = tuning.MoveSpreadScale(sprinting, moving, crouching);
            Object.DestroyImmediate(tuning);
            return scale;
        }

        [Test]
        public void Standing_IsOne() => Assert.AreEqual(1f, Scale(false, false, false), 0.001f);

        [Test]
        public void Walking_IsWalkScale() => Assert.AreEqual(1.5f, Scale(false, true, false), 0.001f);

        [Test]
        public void Crouching_Reduces()
        {
            Assert.AreEqual(0.7f, Scale(false, false, true), 0.001f);
            Assert.AreEqual(1.05f, Scale(false, true, true), 0.001f, "crouch-walking");
        }

        [Test]
        public void Turning_AddsWobble_UpToTheCap()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerHeldItemTuning>();
            Assert.AreEqual(0f, tuning.TurnSpread(0f), 0.001f);
            Assert.AreEqual(1f, tuning.TurnSpread(200f), 0.001f, "0.5 per 100°/s");
            Assert.AreEqual(2f, tuning.TurnSpread(1000f), 0.001f, "capped");
            Assert.AreEqual(1f, tuning.TurnSpread(-200f), 0.001f, "either way");
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void TurnSpread_EasesIn_SoAFlickDoesNotSnap()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerHeldItemTuning>();
            float spread = tuning.SmoothTurnSpread(0f, 1000f, 1f / 60f);
            Assert.Less(spread, 0.5f, "one frame of a fast flick");
            for (int i = 0; i < 60; i++)
                spread = tuning.SmoothTurnSpread(spread, 1000f, 1f / 60f);
            Assert.AreEqual(2f, spread, 0.05f, "a held turn reaches the cap");
            for (int i = 0; i < 60; i++)
                spread = tuning.SmoothTurnSpread(spread, 0f, 1f / 60f);
            Assert.Less(spread, 0.05f, "and lets go");
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void Sprinting_Overrides() => Assert.AreEqual(2.5f, Scale(true, true, false), 0.001f);
    }

    public class HipWobbleTests
    {
        [Test]
        public void Crouching_CalmsTheSway()
        {
            float Largest(bool crouching)
            {
                var sway = Sway();
                var input = new HeldSwayInput { Tiredness = 1f, IsCrouching = crouching };
                for (int i = 0; i < 60; i++)
                    sway.Tick(input, Frame);
                float largest = 0f;
                for (int i = 0; i < 600; i++)
                {
                    sway.Tick(input, Frame);
                    largest = Mathf.Max(largest, sway.AimOffset.magnitude);
                }
                return largest;
            }

            float standing = Largest(false);
            Assert.That(Largest(true), Is.InRange(standing * 0.6f, standing * 0.8f), "crouched keeps about 70% of the sway");
        }

        const float Frame = 1f / 60f;

        static HeldItemSway Sway() => new(new HeldSwaySettings(), new AimSwaySettings(), new BaseStats());

        static HeldSwayInput Input(float wobble) => new() { Wobble = wobble };

        static Vector2 Wander(HeldItemSway sway) => sway.AimOffset - sway.RestOffset;

        [Test]
        public void ZeroWobble_GivesNoWander()
        {
            var sway = Sway();
            for (int i = 0; i < 600; i++)
            {
                sway.Tick(Input(0f), Frame);
                Assert.AreEqual(Vector2.zero, Wander(sway), $"tick {i}");
            }
        }

        [Test]
        public void Wander_StaysInsideTheEnvelope()
        {
            var sway = Sway();
            float total = 0f;
            for (int i = 0; i < 3600; i++)
            {
                sway.Tick(Input(3f), Frame);
                float magnitude = Wander(sway).magnitude;
                Assert.LessOrEqual(magnitude, 3f + 1e-4f, $"tick {i}");
                total += magnitude;
            }
            Assert.Greater(total / 3600f, 0.5f, "it actually wanders");
        }

        [Test]
        public void Wander_IsSlow()
        {
            var sway = Sway();
            sway.Tick(Input(3f), Frame);
            Vector2 last = Wander(sway);
            for (int i = 0; i < 3600; i++)
            {
                sway.Tick(Input(3f), Frame);
                Vector2 now = Wander(sway);
                Assert.Less((now - last).magnitude, 0.25f, $"tick {i}");
                last = now;
            }
        }

        // A hard limit at the envelope's edge pins the wander to the rim, then lets go with a jerk in its speed.
        [Test]
        public void Wander_SpeedChangesSmoothly_AndDoesNotRideTheRim()
        {
            var sway = Sway();
            for (int i = 0; i < 120; i++)
                sway.Tick(Input(3f), Frame);
            Vector2 last = Wander(sway), lastVelocity = Vector2.zero;
            int nearRim = 0;
            for (int i = 0; i < 3600; i++)
            {
                sway.Tick(Input(3f), Frame);
                Vector2 now = Wander(sway), velocity = (now - last) / Frame;
                if (i > 0)
                    Assert.Less((velocity - lastVelocity).magnitude / Frame, 30f, $"tick {i}: speed jerked");
                if (now.magnitude > 3f * 0.95f)
                    nearRim++;
                last = now;
                lastVelocity = velocity;
            }
            Assert.Less(nearRim, 3600 * 0.05f, "frames pressed against the envelope's edge");
        }

        [Test]
        public void Wander_StaysSmooth_WhenTheEnvelopeJumps()
        {
            var sway = Sway();
            for (int i = 0; i < 120; i++)
                sway.Tick(Input(3f), Frame);
            Vector2 last = Wander(sway);

            void Run(float wobble, string step)
            {
                for (int i = 0; i < 120; i++)
                {
                    sway.Tick(Input(wobble), Frame);
                    Vector2 now = Wander(sway);
                    Assert.Less((now - last).magnitude, 0.25f, $"{step}, tick {i}");
                    last = now;
                }
            }

            Run(4.5f, "start walking");
            Run(3f, "stop");
            sway.ResetWobble();
            Run(2f, "switching to a steadier gun");
        }

        [Test]
        public void Size_EasesIntoANewEnvelope_WithoutASuddenStart()
        {
            var sway = Sway();
            for (int i = 0; i < 180; i++)
                sway.Tick(Input(1f), Frame);
            Assert.AreEqual(1f, sway.WobbleSize, 0.01f);
            float last = sway.WobbleSize, first = 0f, largest = 0f;
            for (int i = 0; i < 120; i++)
            {
                sway.Tick(Input(4f), Frame);
                float step = sway.WobbleSize - last;
                if (i == 0)
                    first = step;
                largest = Mathf.Max(largest, step);
                Assert.LessOrEqual(sway.WobbleSize, 4f + 1e-4f, $"tick {i}: overshot");
                last = sway.WobbleSize;
            }
            Assert.Less(first, largest * 0.5f, "starts gently");
            Assert.AreEqual(4f, sway.WobbleSize, 0.1f, "arrives within 2 s");
        }

        // A wider envelope slows the noise, so the aim drifts at about the same degrees per second (heavy, not frantic).
        [Test]
        public void Wander_DriftsAtAboutTheSameSpeed_WhateverTheEnvelope()
        {
            float MeanSpeed(float wobble)
            {
                var sway = Sway();
                for (int i = 0; i < 300; i++)
                    sway.Tick(Input(wobble), Frame);
                Vector2 last = Wander(sway);
                float total = 0f;
                for (int i = 0; i < 7200; i++)
                {
                    sway.Tick(Input(wobble), Frame);
                    Vector2 now = Wander(sway);
                    total += (now - last).magnitude;
                    last = now;
                }
                return total / 120f;
            }

            float ratio = MeanSpeed(6f) / MeanSpeed(3f);
            Assert.That(ratio, Is.InRange(0.75f, 1.33f), "speed at 6° over speed at 3°");
        }

        [Test]
        public void Breathing_VariesFromBreathToBreath()
        {
            var sway = Sway();
            var peaks = new List<float>();
            var times = new List<float>();
            float before = 0f, previous = 0f;
            for (int i = 0; i < 3600; i++)
            {
                sway.Tick(default, Frame);
                float now = sway.RestOffset.y;
                if (i >= 2 && previous > before && previous >= now)
                {
                    peaks.Add(previous);
                    times.Add(i * Frame);
                }
                before = previous;
                previous = now;
            }
            var gaps = times.Zip(times.Skip(1), (a, b) => b - a).ToList();
            Assert.Greater((peaks.Max() - peaks.Min()) / peaks.Average(), 0.15f, "each breath's depth differs");
            Assert.Greater((gaps.Max() - gaps.Min()) / gaps.Average(), 0.1f, "each breath's pace differs");
        }

        [Test]
        public void Kick_JoltsThenSettles()
        {
            var sway = Sway();
            sway.Tick(Input(0f), Frame);
            sway.KickWobble(new Vector2(0f, 1f));
            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                sway.Tick(Input(0f), Frame);
                peak = Mathf.Max(peak, Wander(sway).magnitude);
            }
            Assert.That(peak, Is.InRange(0.5f, 1.5f));
            Assert.Less(Wander(sway).magnitude, 0.1f);
        }

        [Test]
        public void Paused_FreezesTheWander()
        {
            var sway = Sway();
            for (int i = 0; i < 30; i++)
                sway.Tick(Input(3f), Frame);
            Vector2 before = sway.AimOffset;
            sway.Tick(Input(3f), 0f);
            Assert.AreEqual(before, sway.AimOffset);
        }

        [Test]
        public void ResetWobble_ClearsTheKick()
        {
            var sway = Sway();
            sway.Tick(Input(0f), Frame);
            sway.KickWobble(new Vector2(0f, 1f));
            sway.Tick(Input(0f), Frame);
            sway.Tick(Input(0f), Frame);
            sway.ResetWobble();
            sway.Tick(Input(0f), Frame);
            Assert.AreEqual(Vector2.zero, Wander(sway));
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
            Vector3 position = SettledPosition(sway, new HeldSwayInput { LookRate = new Vector2(100f, 0f) });
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
            var input = new HeldSwayInput { LocalVelocity = new Vector3(3f, 0f, 3f), LookRate = new Vector2(1f, 0.5f) / Frame };
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
            Vector2 range = AimRange(sway, new HeldSwayInput { IsAirborne = true, LookRate = new Vector2(10f, 0f) / Frame }, 60);
            Assert.LessOrEqual(range.magnitude, 1.42f);
        }

        [Test]
        public void TheModel_OnlyRolls()
        {
            var sway = new HeldItemSway(new HeldSwaySettings(), new AimSwaySettings(), new BaseStats());
            Run(sway, new HeldSwayInput { LocalVelocity = new Vector3(3f, 0f, 3f), LookRate = new Vector2(2f, 1f) / Frame, BobPhase = 1f, BobIntensity = 1f }, 30);
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

        [Test]
        public void LensRatio_IsOneWhenTheGunZoomsWithTheWorld_AndBelowOneWhenItKeepsItsLens()
        {
            Assert.AreEqual(1f, PlayerZoom.LensRatio(70f * 0.67f, 70f * 0.67f), 0.0001f);
            Assert.AreEqual(Mathf.Tan(35f * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(35f * Mathf.Deg2Rad), PlayerZoom.LensRatio(35f, 70f), 0.0001f);
            Assert.Greater(PlayerZoom.LensRatio(70f * 0.67f * 1.03f, 70f * 0.67f), 1f, "a shot punch widens only the world");
        }
    }
}
