using System;
using System.IO;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
{
    // The needs in the player's save, through the real save file.
    public sealed class PlayerNeedsSaveTests
    {
        SaveSettings _saveSettings;
        SaveService _saves;
        PlayerNeedsSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _saveSettings = ScriptableObject.CreateInstance<SaveSettings>();
            _saveSettings.Folder = "PlayerNeedsSaveTests_" + Guid.NewGuid().ToString("N");
            _saveSettings.SlotCount = 1;
            _saves = new SaveService(_saveSettings);
            _settings = ScriptableObject.CreateInstance<PlayerNeedsSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saves.FolderPath))
                Directory.Delete(_saves.FolderPath, true);
            Object.DestroyImmediate(_saveSettings);
            Object.DestroyImmediate(_settings);
        }

        PlayerNeeds Needs(IStatProvider stats = null) =>
            new(_settings, stats ?? new StatModifiers(), new Health(100f), new Stamina(100f, 1f, 0.3f));

        PlayerSaveSection.PlayerSave WriteAndRead(PlayerSaveSection.PlayerSave saved)
        {
            SaveData data = _saves.Create();
            data.Set("player", saved);
            _saves.WriteAsync(1, data).Wait();
            Assert.IsTrue(_saves.TryRead(1, out SaveData read, out string problem), problem);
            Assert.IsTrue(read.TryGet("player", out PlayerSaveSection.PlayerSave loaded));
            return loaded;
        }

        [Test]
        public void PlayerSave_FromBeforeNeeds_StartsTheMetersFull()
        {
            PlayerSaveSection.PlayerSave loaded = WriteAndRead(new PlayerSaveSection.PlayerSave { Health = 50f });
            PlayerNeeds needs = Needs();
            needs.HoursAwake = 3f;

            PlayerSaveSection.RestoreNeeds(loaded, needs);

            Assert.AreEqual(100f, needs.Hunger.Current);
            Assert.AreEqual(100f, needs.Thirst.Current);
            Assert.AreEqual(100f, needs.Sanity.Current);
            Assert.AreEqual(3f, needs.HoursAwake, "an old save leaves the fresh state alone");
            needs.Dispose();
        }

        [Test]
        public void PlayerSave_RoundTripsTheMeters()
        {
            PlayerNeeds before = Needs();
            before.Hunger.Set(40f);
            before.Thirst.Set(60f);
            before.Sanity.Set(70f);
            before.HoursAwake = 5f;
            var saved = new PlayerSaveSection.PlayerSave();
            PlayerSaveSection.CaptureNeeds(saved, before);

            PlayerNeeds after = Needs();
            PlayerSaveSection.RestoreNeeds(WriteAndRead(saved), after);

            Assert.AreEqual(40f, after.Hunger.Current, 1e-3f);
            Assert.AreEqual(60f, after.Thirst.Current, 1e-3f);
            Assert.AreEqual(70f, after.Sanity.Current, 1e-3f);
            Assert.AreEqual(5f, after.HoursAwake, 1e-3f);
            before.Dispose();
            after.Dispose();
        }

        [Test]
        public void PlayerSave_RestoresAboveTheBaseMaxWhenAnUpgradeIsSaved()
        {
            var stats = new StatModifiers();
            stats.Set("upgrade", StatId.MaxHunger, add: 50f);
            PlayerNeeds before = Needs(stats);
            before.Hunger.Set(130f);
            var saved = new PlayerSaveSection.PlayerSave();
            PlayerSaveSection.CaptureNeeds(saved, before);

            // Stats restore before the player (SaveOrder.Stats < SaveOrder.Player), so the max is already 150.
            PlayerNeeds after = Needs(stats);
            PlayerSaveSection.RestoreNeeds(WriteAndRead(saved), after);

            Assert.AreEqual(130f, after.Hunger.Current, 1e-3f);
            before.Dispose();
            after.Dispose();
        }
    }
}
