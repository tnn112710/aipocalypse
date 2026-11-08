using System.Collections.Generic;
using MB13.Aipocalypse.Core.SpawnSystem;
using NUnit.Framework;
using UnityEditor;

namespace MB13.Aipocalypse.Tests
{
    // Every spawn card in the game: its variants resolve on disk (a prefab whose Spawnable wasn't saved reads as missing), it
    // can spawn something, and the console can spawn it by name.
    public sealed class SpawnCardTests
    {
        const string SettingsPath = "Assets/_Game/Data/Spawning/SpawnSystemSettings.asset";

        static IEnumerable<SpawnCard> Cards()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SpawnCard", new[] { "Assets/_Game" }))
                yield return AssetDatabase.LoadAssetAtPath<SpawnCard>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [Test]
        public void EveryCard_HasNoSetupProblem()
        {
            var problems = new List<string>();
            foreach (SpawnCard card in Cards())
                if (card.Problem is { } problem)
                    problems.Add($"{AssetDatabase.GetAssetPath(card)}: {problem}");
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void EveryCard_IsListedForTheConsole()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SpawnSystemSettings>(SettingsPath);
            Assert.That(settings, Is.Not.Null, SettingsPath);
            var missing = new List<string>();
            foreach (SpawnCard card in Cards())
                if (!settings.Cards.Contains(card))
                    missing.Add(AssetDatabase.GetAssetPath(card));
            Assert.That(missing, Is.Empty, "add them to SpawnSystemSettings.Cards");
        }
    }
}
