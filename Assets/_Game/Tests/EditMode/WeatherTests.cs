using System;
using System.Collections.Generic;
using System.Linq;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.Events;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public class WeatherTests
    {
        const string SettingsPath = "Assets/_Game/Data/Environment/Weather/Weather.asset";
        const float Transition = 10f;

        IObjectResolver _resolver;
        WeatherSettings _settings;
        WeatherService _weather;
        int _changes;
        IDisposable _subscription;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<WeatherSettings>();
            _settings.StartSky = SkyWeather.Clear;
            _settings.TransitionSeconds = Transition;
            _settings.Skies = new Dictionary<SkyWeather, WeatherSettings.Sky>
            {
                [SkyWeather.Clear] = new() { DefaultWind = WindLevel.Calm, FogDensityScale = 1f, NatureVolume = 1f },
                [SkyWeather.HeavyRain] = new() { DefaultWind = WindLevel.Gale, FogDensityScale = 3f, NatureVolume = 0f, Lightning = true },
            };
            _settings.Winds = new Dictionary<WindLevel, WeatherSettings.Wind>
            {
                [WindLevel.Calm] = new() { Strength = 0.5f },
                [WindLevel.Breezy] = new() { Strength = 1f },
                [WindLevel.Gale] = new() { Strength = 3f },
            };

            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(_settings);
            builder.Register<AudioService>(Lifetime.Singleton);
            builder.Register<WeatherService>(Lifetime.Singleton);
            _resolver = builder.Build();
            _weather = _resolver.Resolve<WeatherService>();
            _changes = 0;
            _subscription = _resolver.Resolve<ISubscriber<WeatherChanged>>().Subscribe(_ => _changes++);
            _weather.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _subscription.Dispose();
            _resolver.Dispose();
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Start_CutsToTheStartWeather()
        {
            Assert.That(_weather.Sky, Is.EqualTo(SkyWeather.Clear));
            Assert.That(_weather.Wind, Is.EqualTo(WindLevel.Calm));
            Assert.That(_weather.Progress, Is.EqualTo(1f));
            Assert.That(_weather.Mix.Strength, Is.EqualTo(0.5f));
        }

        [Test]
        public void Set_UsesTheSkysDefaultWind_AndAnOverrideWinsUntilCleared()
        {
            _weather.Set(SkyWeather.HeavyRain);
            Assert.That(_weather.Wind, Is.EqualTo(WindLevel.Gale));

            _weather.SetWind(WindLevel.Breezy);
            Assert.That(_weather.Wind, Is.EqualTo(WindLevel.Breezy));
            Assert.That(_weather.WindOverride, Is.EqualTo(WindLevel.Breezy));

            _weather.SetWind(null);
            Assert.That(_weather.Wind, Is.EqualTo(WindLevel.Gale));
            Assert.That(_weather.WindOverride, Is.Null);
        }

        [Test]
        public void Change_BlendsOverTheTransition_AndHoldsWhilePaused()
        {
            _weather.Set(SkyWeather.HeavyRain);
            Assert.That(_weather.Mix.FogScale, Is.EqualTo(1f));

            _weather.Advance(Transition / 2f);
            Assert.That(_weather.Mix.FogScale, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(_weather.Mix.Strength, Is.EqualTo(1.75f).Within(1e-4f));

            _weather.Advance(0f);
            Assert.That(_weather.Progress, Is.EqualTo(0.5f).Within(1e-4f));

            _weather.Advance(Transition);
            Assert.That(_weather.Progress, Is.EqualTo(1f));
            Assert.That(_weather.Mix.FogScale, Is.EqualTo(3f));
            Assert.That(_weather.Mix.Nature, Is.EqualTo(0f));
            Assert.That(_weather.Mix.Lightning, Is.EqualTo(1f));
        }

        [Test]
        public void ChangeMidBlend_StartsFromTheBlendedValues()
        {
            _weather.Set(SkyWeather.HeavyRain);
            _weather.Advance(Transition / 2f);
            float halfway = _weather.Mix.FogScale;

            _weather.Set(SkyWeather.Clear);
            Assert.That(_weather.Mix.FogScale, Is.EqualTo(halfway));
            _weather.Advance(Transition);
            Assert.That(_weather.Mix.FogScale, Is.EqualTo(1f));
        }

        [Test]
        public void Restore_CutsToTheSavedState()
        {
            _weather.Set(SkyWeather.HeavyRain, WindLevel.Breezy);
            WeatherState saved = _weather.State;
            _weather.Set(SkyWeather.Clear);

            _weather.Restore(saved);
            Assert.That(_weather.Sky, Is.EqualTo(SkyWeather.HeavyRain));
            Assert.That(_weather.WindOverride, Is.EqualTo(WindLevel.Breezy));
            Assert.That(_weather.Progress, Is.EqualTo(1f));
            Assert.That(_weather.Mix.FogScale, Is.EqualTo(3f));
            Assert.That(_weather.Mix.Strength, Is.EqualTo(1f));
        }

        [Test]
        public void WeatherChanged_IsPublishedOnlyWhenTheWeatherChanges()
        {
            _changes = 0;
            _weather.Set(SkyWeather.Clear);
            Assert.That(_changes, Is.EqualTo(0));
            _weather.Set(SkyWeather.HeavyRain);
            _weather.SetWind(WindLevel.Calm);
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void SettingsAsset_HasEveryWeatherAndWindLevel()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WeatherSettings>(SettingsPath);
            Assert.IsNotNull(settings, SettingsPath);
            foreach (SkyWeather sky in Enum.GetValues(typeof(SkyWeather)))
            {
                Assert.IsTrue(settings.Skies.TryGetValue(sky, out WeatherSettings.Sky entry), $"no entry for {sky}");
                Assert.IsNotNull(entry.Cozy, $"{sky} has no COZY profile");
                Assert.That(entry.Cozy.GetType().Name, Is.EqualTo("WeatherProfile"), sky.ToString());
            }
            foreach (WindLevel level in Enum.GetValues(typeof(WindLevel)))
                Assert.IsTrue(settings.Winds.ContainsKey(level), $"no entry for {level}");
        }

        [Test]
        public void CozyProfileCopies_PlayNoCozyAudioOrThunder()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WeatherSettings>(SettingsPath);
            Assert.IsNotNull(settings, SettingsPath);
            foreach (KeyValuePair<SkyWeather, WeatherSettings.Sky> pair in settings.Skies)
            {
                string path = AssetDatabase.GetAssetPath(pair.Value.Cozy);
                StringAssert.StartsWith("Assets/_Game/Data/Environment/Cozy/Weather/", path, $"{pair.Key} must use our copy");
                SerializedProperty fx = new SerializedObject(pair.Value.Cozy).FindProperty("FX");
                var kinds = Enumerable.Range(0, fx.arraySize)
                    .Select(i => fx.GetArrayElementAtIndex(i).objectReferenceValue)
                    .Where(o => o != null)
                    .Select(o => o.GetType().Name);
                Assert.That(kinds, Has.None.EqualTo("AudioFX").And.None.EqualTo("ThunderFX"), pair.Key.ToString());
            }
        }
    }
}
