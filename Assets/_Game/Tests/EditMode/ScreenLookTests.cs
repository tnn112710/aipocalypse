using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
{
    public sealed class ScreenLookTests
    {
        const string UrpLooks = "Assets/_Game/Data/Graphics/ScreenLooks/UrpLooks.asset";
        const string ScLooks = "Assets/_Game/Data/Graphics/ScreenLooks/ScLooks.asset";

        sealed class Recorder : IScreenLookBackend
        {
            public readonly Dictionary<ScreenLookId, float> Last = new();
            public bool Disposed;

            public void Apply(ScreenLookId look, float strength) => Last[look] = strength;
            public void Dispose() => Disposed = true;
        }

        sealed class RecorderAsset : ScreenLookBackendAsset
        {
            public Recorder Made;

            public override IScreenLookBackend Create(Transform parent) => Made = new Recorder();
        }

        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        T Make<T>() where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _created.Add(made);
            return made;
        }

        ScreenLookSettings Settings(ScreenLookBackendAsset backend, bool enabled = true)
        {
            var settings = Make<ScreenLookSettings>();
            settings.Backend = backend;
            settings.Enabled = enabled;
            return settings;
        }

        [Test]
        public void Request_ReachesTheBackendOnTick()
        {
            var factory = Make<RecorderAsset>();
            var service = new ScreenLookService(Settings(factory));

            service.Request(ScreenLookId.SanityLow, 0.7f);
            service.Advance();

            Assert.AreEqual(0.7f, factory.Made.Last[ScreenLookId.SanityLow], 1e-4f);
            service.Dispose();
            Assert.IsTrue(factory.Made.Disposed);
        }

        [Test]
        public void NoBackend_DoesNothingAndDoesNotThrow()
        {
            var service = new ScreenLookService(Settings(null));

            Assert.DoesNotThrow(() =>
            {
                service.Request(ScreenLookId.SanityCritical, 1f);
                service.Advance();
                service.Dispose();
            });
        }

        [Test]
        public void Disabled_AppliesZeroStrength()
        {
            var factory = Make<RecorderAsset>();
            var service = new ScreenLookService(Settings(factory, enabled: false));

            service.Request(ScreenLookId.SanityUneasy, 1f);
            service.Advance();

            Assert.IsTrue(factory.Made == null || factory.Made.Last.GetValueOrDefault(ScreenLookId.SanityUneasy) == 0f);
            service.Dispose();
        }

        [Test]
        public void SanityLooks_MapTheShareToTheThreeLooks()
        {
            var effects = new SanityEffectSettings();

            Assert.AreEqual(0.5f, effects.Strength(ScreenLookId.SanityUneasy, 0.5f), 1e-4f);
            Assert.AreEqual(0f, effects.Strength(ScreenLookId.SanityLow, 0.5f), 1e-4f);
            Assert.AreEqual(0f, effects.Strength(ScreenLookId.SanityCritical, 0.5f), 1e-4f);
            Assert.AreEqual(1f, effects.Strength(ScreenLookId.SanityUneasy, 0.1f), 1e-4f);
            Assert.AreEqual(1f, effects.Strength(ScreenLookId.SanityLow, 0.1f), 1e-4f);
            Assert.AreEqual(0.5f, effects.Strength(ScreenLookId.SanityCritical, 0.1f), 1e-4f);
            Assert.AreEqual(0f, effects.Strength(ScreenLookId.SanityUneasy, 0.9f), 1e-4f);
        }

        [Test]
        public void BackendAssets_HaveAProfileForEveryLook()
        {
            foreach (string path in new[] { UrpLooks, ScLooks })
            {
                var asset = AssetDatabase.LoadAssetAtPath<VolumeLookBackendAsset>(path);
                Assert.IsNotNull(asset, path);
                foreach (ScreenLookId look in Enum.GetValues(typeof(ScreenLookId)))
                {
                    Assert.IsTrue(asset.Looks.TryGetValue(look, out VolumeLook entry), $"{path}: {look}");
                    Assert.IsNotNull(entry.Profile, $"{path}: {look} profile");
                }
            }
        }
    }
}
