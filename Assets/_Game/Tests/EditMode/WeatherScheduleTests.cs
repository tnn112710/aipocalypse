using System;
using System.Collections.Generic;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.RandomSystem;
using Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public class WeatherScheduleTests
    {
        const string RulesPath = "Assets/_Game/Data/Environment/Weather/WeatherRules.asset";
        static readonly SkyWeather[] Skies = (SkyWeather[])Enum.GetValues(typeof(SkyWeather));

        WeatherRules _rules;

        [SetUp]
        public void SetUp() => _rules = Rules();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rules);

        // Like the shipped table: every weather by day and night, only the light ones on Blood Moons and eclipses.
        static WeatherRules Rules()
        {
            var rules = ScriptableObject.CreateInstance<WeatherRules>();
            rules.DurationHours = new List<float> { 1f, 2f, 4f, 6f };
            float[] day = { 30f, 30f, 15f, 8f, 5f, 3f, 1.5f, 4f };
            foreach (SkyWeather sky in Skies)
            {
                bool light = sky is SkyWeather.Clear or SkyWeather.PartlyCloudy or SkyWeather.Cloudy;
                rules.Skies[sky] = new WeatherRules.SkyRule
                {
                    Day = day[(int)sky], Night = day[(int)sky] * 0.5f, BloodMoon = light ? 10f : 0f, Eclipse = light ? 10f : 0f,
                    Durations = new List<float> { 1f, 4f, 2f, 1f },
                    Calm = 1f, Breezy = 4f, Windy = 2f, Gale = sky == SkyWeather.HeavyRain ? 5f : 0f,
                };
            }
            return rules;
        }

        WeatherPlanner Planner(ulong seed = 1) => new(_rules, new RandomStream(seed));

        static List<WeatherSpell> Run(WeatherPlanner planner, int days)
        {
            planner.Restart(0d, SkyWeather.PartlyCloudy, WindLevel.Breezy);
            planner.Extend(days * 24d);
            return new List<WeatherSpell>(planner.Plan);
        }

        static double Hours(int day, float hour) => WorldTime.At(day, hour).TotalHours;

        [Test]
        public void Planner_SameSeed_PlansTheSameWeather()
        {
            List<WeatherSpell> a = Run(Planner(7), 30);
            List<WeatherSpell> b = Run(Planner(7), 30);
            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
                Assert.That((a[i].Start, a[i].Sky, a[i].Wind), Is.EqualTo((b[i].Start, b[i].Sky, b[i].Wind)));
        }

        [Test]
        public void Planner_NeverShowsAWeatherWherePeriodForbidsIt([Values(1UL, 2UL, 3UL)] ulong seed)
        {
            WeatherPlanner planner = Planner(seed);
            for (int day = 1; day <= 600; day++)
            {
                if (day % 3 == 0)
                    planner.MarkBloodMoon(day);
                if (day % 5 == 0)
                    planner.MarkEclipse(day);
            }
            foreach (WeatherSpell spell in Run(planner, 600))
                for (double t = spell.Start; t < spell.End; t += 0.05d)
                    Assert.That(planner.Allows(spell.Sky, t), $"{spell.Sky} at {new WorldTime(t)} ({planner.PeriodAt(t)})");
        }

        [Test]
        public void Planner_ChangesOneStepOfCloudCoverAtATime_AndTheWindOneLevel()
        {
            List<WeatherSpell> plan = Run(Planner(), 300);
            for (int i = 1; i < plan.Count; i++)
            {
                Assert.That(WeatherPlanner.Steps(plan[i - 1].Sky, plan[i].Sky), Is.Zero, $"{plan[i - 1].Sky} → {plan[i].Sky}");
                Assert.That(Math.Abs(plan[i].Wind - plan[i - 1].Wind), Is.LessThanOrEqualTo(1), $"{plan[i - 1].Wind} → {plan[i].Wind}");
                Assert.That(plan[i].Start, Is.EqualTo(plan[i - 1].End).Within(1e-6));
            }
        }

        [Test]
        public void Planner_Durations_ComeFromTheWeathersTable()
        {
            // Only Clear and Partly Cloudy, next to each other (no steps): every Clear lasts 4 h, every Partly Cloudy 2 h.
            foreach (SkyWeather sky in Skies)
                _rules.Skies[sky] = new WeatherRules.SkyRule { Durations = new List<float> { 0f, 0f, 0f, 0f }, Breezy = 1f };
            _rules.Skies[SkyWeather.Clear] = new WeatherRules.SkyRule { Day = 1f, Night = 1f, Durations = new List<float> { 0f, 0f, 1f, 0f }, Breezy = 1f };
            _rules.Skies[SkyWeather.PartlyCloudy] = new WeatherRules.SkyRule { Day = 1f, Night = 1f, Durations = new List<float> { 0f, 1f, 0f, 0f }, Breezy = 1f };
            _rules.DurationJitter = 0f;

            List<WeatherSpell> plan = Run(Planner(), 20);
            for (int i = 1; i < plan.Count - 1; i++)
                Assert.That(plan[i].Hours, Is.EqualTo(plan[i].Sky == SkyWeather.Clear ? 4d : 2d).Within(1e-6), $"{plan[i].Sky} #{i}");
        }

        [Test]
        public void Planner_ForceLater_WalksThereOnTime_AndTheRulesTakeOverAfter()
        {
            // Heavy rain is never rolled by day here: the force alone brings it, after the walk up through medium rain.
            _rules.Skies[SkyWeather.HeavyRain].Day = 0f;
            WeatherPlanner planner = Planner();
            planner.Restart(Hours(1, 7f), SkyWeather.Clear, WindLevel.Calm);
            planner.Force(SkyWeather.HeavyRain, WindLevel.Gale, Hours(1, 13f), 3d);

            WeatherSpell before = planner.At(Hours(1, 12.99f));
            Assert.That(before.Sky, Is.EqualTo(SkyWeather.MediumRain));
            WeatherSpell during = planner.At(Hours(1, 13.01f));
            Assert.That((during.Sky, during.Forced), Is.EqualTo((SkyWeather.HeavyRain, true)));
            Assert.That(planner.At(Hours(1, 15.9f)).Sky, Is.EqualTo(SkyWeather.HeavyRain));
            WeatherSpell after = planner.At(Hours(1, 16.01f));
            Assert.That((after.Sky, after.Forced), Is.EqualTo((SkyWeather.MediumRain, false)));
        }

        [Test]
        public void Planner_HoldCutsToTheWeatherNow_UntilReleased()
        {
            WeatherPlanner planner = Planner();
            planner.Restart(Hours(1, 7f), SkyWeather.Clear, WindLevel.Calm);
            planner.At(Hours(1, 8f));
            planner.Force(SkyWeather.Fog, WindLevel.Calm, Hours(1, 8f), double.PositiveInfinity);

            WeatherSpell now = planner.At(Hours(1, 8f));
            Assert.That((now.Sky, now.Wind, planner.IsHeld), Is.EqualTo((SkyWeather.Fog, WindLevel.Calm, true)));
            Assert.That(planner.At(Hours(4, 8f)).Sky, Is.EqualTo(SkyWeather.Fog));

            planner.Release();
            Assert.That(planner.IsHeld, Is.False);
            Assert.That(planner.At(Hours(4, 8f)).Forced, Is.False);
            Assert.That(planner.At(Hours(4, 20f)).Forced, Is.False);
        }

        [Test]
        public void Planner_BloodMoonMarkedDuringAStorm_ClearsItBeforeTheNight()
        {
            WeatherPlanner planner = Planner();
            planner.Restart(Hours(1, 10f), SkyWeather.HeavyRain, WindLevel.Gale);
            planner.At(Hours(1, 10f));
            planner.MarkBloodMoon(1);
            planner.Extend(Hours(3, 0f));
            foreach (WeatherSpell spell in planner.Plan)
                for (double t = spell.Start; t < spell.End; t += 0.05d)
                    Assert.That(planner.Allows(spell.Sky, t), $"{spell.Sky} at {new WorldTime(t)}");
            Assert.That(planner.PeriodAt(Hours(1, 20f)), Is.EqualTo(WeatherPeriod.BloodMoon));
            Assert.That(planner.PeriodAt(Hours(2, 3f)), Is.EqualTo(WeatherPeriod.BloodMoon));
            Assert.That(planner.PeriodAt(Hours(2, 12f)), Is.EqualTo(WeatherPeriod.Day));
        }

        [Test]
        public void RulesAsset_HasEverySkyWeather_ADurationWeightPerDuration_AndWeightsInEveryPeriod()
        {
            var rules = AssetDatabase.LoadAssetAtPath<WeatherRules>(RulesPath);
            Assert.That(rules, Is.Not.Null);
            foreach (SkyWeather sky in Skies)
            {
                WeatherRules.SkyRule rule = rules.Rule(sky);
                Assert.That(rule, Is.Not.Null, sky.ToString());
                Assert.That(rule.Durations.Count, Is.EqualTo(rules.DurationHours.Count), sky.ToString());
            }
            foreach (WeatherPeriod period in Enum.GetValues(typeof(WeatherPeriod)))
            {
                float total = 0f;
                foreach (SkyWeather sky in Skies)
                    total += rules.Weight(period, sky);
                Assert.That(total, Is.GreaterThan(0f), period.ToString());
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<WeatherSettings>("Assets/_Game/Data/Environment/Weather/Weather.asset").Rules, Is.SameAs(rules));
        }

        [Test]
        public void Schedule_ShowsThePlannedWeather_CutsAfterAJump_AndHoldsUntilReleased()
        {
            var clockSettings = ScriptableObject.CreateInstance<ClockSettings>();
            clockSettings.StartHour = 7f;
            var settings = ScriptableObject.CreateInstance<WeatherSettings>();
            settings.StartSky = SkyWeather.Clear;
            settings.TransitionSeconds = 10f;
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(clockSettings);
            builder.RegisterInstance(settings);
            builder.RegisterInstance(_rules);
            builder.Register(_ => new RandomService(3), Lifetime.Singleton);
            builder.Register<AudioService>(Lifetime.Singleton);
            builder.Register<WorldClock>(Lifetime.Singleton);
            builder.Register<WeatherService>(Lifetime.Singleton);
            builder.Register<WeatherSchedule>(Lifetime.Singleton);
            IObjectResolver resolver = builder.Build();
            try
            {
                var clock = resolver.Resolve<WorldClock>();
                var weather = resolver.Resolve<WeatherService>();
                var schedule = resolver.Resolve<WeatherSchedule>();
                weather.Start();
                schedule.Tick();
                Assert.That(weather.Sky, Is.EqualTo(SkyWeather.Clear));

                clock.SetTime(WorldTime.At(4, 15f));
                schedule.Tick();
                WeatherSpell planned = schedule.Planner.At(clock.Now.TotalHours);
                Assert.That((weather.Sky, weather.Wind), Is.EqualTo((planned.Sky, planned.Wind)));
                Assert.That(weather.Progress, Is.EqualTo(1f), "a jump cuts");

                schedule.Hold(SkyWeather.Fog);
                clock.SetTime(WorldTime.At(5, 15f));
                schedule.Tick();
                Assert.That((weather.Sky, schedule.IsHeld), Is.EqualTo((SkyWeather.Fog, true)));

                schedule.Release();
                Assert.That(schedule.IsHeld, Is.False);
            }
            finally
            {
                resolver.Dispose();
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(clockSettings);
            }
        }
    }
}
