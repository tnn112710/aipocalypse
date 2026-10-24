using System;
using System.Collections.Generic;
using Aipocalypse.Core.EnvironmentSystem;
using Aipocalypse.Core.UI;
using Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Tests
{
    // Per-phase data lives in dictionaries keyed by DayPhase; adding or renaming a phase must not leave an asset without it.
    public class PhaseDataTests
    {
        [Test]
        public void EveryClockSettingsAsset_HasEveryPhase() => AssertEveryPhase<ClockSettings>(asset => asset.Phases.Keys);

        [Test]
        public void EveryClockStyleAsset_HasEveryPhase() => AssertEveryPhase<ClockStyle>(asset => asset.Phases.Keys);

        [Test]
        public void EveryTimeOfDayProfile_HasEveryPhaseLook() => AssertEveryPhase<TimeOfDayProfile>(asset => asset.PhaseLooks.Keys);

        static void AssertEveryPhase<T>(Func<T, IEnumerable<DayPhase>> keys) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/_Game" });
            Assert.That(guids, Is.Not.Empty, $"no {typeof(T).Name} asset found");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var present = new HashSet<DayPhase>(keys(AssetDatabase.LoadAssetAtPath<T>(path)));
                foreach (DayPhase phase in Enum.GetValues(typeof(DayPhase)))
                    Assert.That(present, Does.Contain(phase), $"{path} has no entry for {phase}");
            }
        }
    }
}
