using System.Collections.Generic;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class TimeOfDayTests
    {
        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        LightingPreset Look(float sunIntensity)
        {
            var look = ScriptableObject.CreateInstance<LightingPreset>();
            look.SunIntensity = sunIntensity;
            _created.Add(look);
            return look;
        }

        TimeOfDayProfile Profile(params (float hour, LightingPreset look, KeyBlend blend)[] keys)
        {
            var profile = ScriptableObject.CreateInstance<TimeOfDayProfile>();
            _created.Add(profile);
            foreach (var (hour, look, blend) in keys)
                profile.Keys.Add(new TimeOfDayKey { Hour = hour, Look = look, Blend = blend });
            profile.SortKeys();
            return profile;
        }

        [Test]
        public void TimeWindow_WrapsPastMidnight_WithFadesInside()
        {
            var window = new TimeWindow(18f, 6f, 1f);

            Assert.AreEqual(0f, window.Weight(12f));
            Assert.AreEqual(0f, window.Weight(18f), "fade starts at On");
            Assert.AreEqual(0.5f, window.Weight(18.5f), 1e-4f);
            Assert.AreEqual(1f, window.Weight(23f));
            Assert.AreEqual(1f, window.Weight(2f));
            Assert.AreEqual(0.5f, window.Weight(5.5f), 1e-4f);
            Assert.AreEqual(0f, window.Weight(6f));
        }

        [Test]
        public void Profile_BlendsLinearlyBetweenKeys()
        {
            var profile = Profile((6f, Look(0f), KeyBlend.Linear), (12f, Look(3f), KeyBlend.Linear));

            Assert.IsTrue(profile.Evaluate(9f, out AtmosphereState state, out var segment));
            Assert.AreEqual(1.5f, state.SunIntensity, 1e-4f);
            Assert.AreEqual(0, segment.From);
            Assert.AreEqual(1, segment.To);
        }

        [Test]
        public void Profile_WrapsFromLastKeyToFirstAcrossMidnight()
        {
            var profile = Profile((4f, Look(2f), KeyBlend.Linear), (20f, Look(0f), KeyBlend.Linear));

            // 20:00 → 04:00 is 8 h; midnight is halfway.
            profile.Evaluate(0f, out AtmosphereState midnight, out var segment);
            Assert.AreEqual(1f, midnight.SunIntensity, 1e-4f);
            Assert.AreEqual(1, segment.From);
            Assert.AreEqual(0, segment.To);

            profile.Evaluate(22f, out AtmosphereState late, out _);
            Assert.AreEqual(0.5f, late.SunIntensity, 1e-4f);
        }

        [Test]
        public void Profile_HoldKeepsTheKeyUntilTheNext()
        {
            var profile = Profile((6f, Look(1f), KeyBlend.Hold), (12f, Look(3f), KeyBlend.Linear));

            profile.Evaluate(11.9f, out AtmosphereState state, out _);
            Assert.AreEqual(1f, state.SunIntensity, 1e-4f);
        }

        [Test]
        public void SunPath_RisesAndSetsAtItsHours_HighestAtMidday()
        {
            var path = new SunPath { Sunrise = 6f, Sunset = 18f, NoonElevation = 50f };

            path.Sun(6f, out float rise);
            path.Sun(12f, out float noon);
            path.Sun(18f, out float set);
            path.Sun(0f, out float midnight);
            path.Moon(0f, out float moonMidnight);

            Assert.AreEqual(0f, rise, 1e-3f);
            Assert.AreEqual(50f, noon, 1e-3f);
            Assert.AreEqual(0f, set, 1e-3f);
            Assert.Less(midnight, 0f);
            Assert.Greater(moonMidnight, 0f);
        }

        [Test]
        public void SkyboxPlusSun_SinksFasterBelowDusk_KeepingItsBearing()
        {
            Vector3 high = SunPath.Direction(120f, 30f);
            Vector3 low = SunPath.Direction(250f, -4f);

            Vector3 keptHigh = EnvironmentDirector.SkyboxPlusSun(high, 6f, 3.5f);
            Vector3 sunk = EnvironmentDirector.SkyboxPlusSun(low, 6f, 3.5f);
            Vector3 midnight = EnvironmentDirector.SkyboxPlusSun(SunPath.Direction(0f, -30f), 6f, 3.5f);

            Assert.AreEqual(0f, Vector3.Angle(high, keptHigh), 1e-3f, "above dusk the sky sun is the real sun");
            Assert.AreEqual(6f - 10f * 3.5f, Mathf.Asin(sunk.y) * Mathf.Rad2Deg, 1e-2f);
            Assert.AreEqual(250f, Mathf.Atan2(sunk.x, sunk.z) * Mathf.Rad2Deg + 360f, 1e-2f);
            Assert.AreEqual(-85f, Mathf.Asin(midnight.y) * Mathf.Rad2Deg, 1e-2f, "clamped short of straight down");
        }

        [Test]
        public void Reactor_KeepsFading_AtHighFrameRates()
        {
            // Inactive, so the director's edit-mode hooks stay quiet.
            var go = new GameObject("Director");
            go.SetActive(false);
            _created.Add(go);
            var director = go.AddComponent<EnvironmentDirector>();
            var reactor = go.AddComponent<TimeOfDayEmission>();
            var hour = typeof(EnvironmentDirector).GetProperty(nameof(EnvironmentDirector.Hour))!;

            hour.SetValue(director, 0f);
            reactor.React(director, 0.002f);
            Assert.AreEqual(1f, reactor.Weight, "the first reaction snaps to the night weight");

            hour.SetValue(director, 12f);
            for (int i = 0; i < 150; i++)
                reactor.React(director, 0.002f);

            Assert.AreEqual(1f - 0.3f / 1.5f, reactor.Weight, 0.01f, "0.3 s of a 1.5 s fade at 500 fps");
        }
    }
}
