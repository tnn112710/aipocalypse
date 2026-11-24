using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.Stats;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
{
    public sealed class SaveTests
    {
        const string CatalogPath = "Assets/_Game/Data/Equipment/ItemCatalog.asset";

        [Serializable]
        public sealed class TestEntry
        {
            public int Number;
            public string Text;
            public List<int> List = new();
        }

        sealed class WritingSection : SaveSection
        {
            readonly string _key;
            readonly bool _fails;

            public WritingSection(SaveRegistry registry, string key, bool fails = false) : base(registry)
            {
                _key = key;
                _fails = fails;
                Initialize();
            }

            public override int Order => 0;

            public override void Capture(SaveData save)
            {
                if (_fails)
                    throw new InvalidOperationException("broken capture (expected by the test)");
                save.Set(_key, new TestEntry { Number = 5 });
            }

            public override void Restore(SaveData save) { }
        }

        sealed class OrderedSection : SaveSection
        {
            readonly int _order;
            readonly List<int> _restored;
            readonly bool _fails;

            public OrderedSection(SaveRegistry registry, int order, List<int> restored, bool fails = false) : base(registry)
            {
                _order = order;
                _restored = restored;
                _fails = fails;
                Initialize();
            }

            public override int Order => _order;
            public override void Capture(SaveData save) { }

            public override void Restore(SaveData save)
            {
                if (_fails)
                    throw new InvalidOperationException("broken section (expected by the test)");
                _restored.Add(_order);
            }
        }

        SaveSettings _settings;
        SaveService _saves;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SaveSettings>();
            _settings.Folder = "SaveTests_" + Guid.NewGuid().ToString("N");
            _settings.SlotCount = 3;
            _saves = new SaveService(_settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saves.FolderPath))
                Directory.Delete(_saves.FolderPath, true);
            Object.DestroyImmediate(_settings);
        }

        SaveData Saved(int number, long savedAt = 1)
        {
            SaveData data = _saves.Create();
            data.Set("test", new TestEntry { Number = number, Text = "hello", List = new List<int> { 1, 2, 3 } });
            data.Header.Day = 4;
            data.Header.Hour = 7.5f;
            data.Header.SavedAtUtcTicks = savedAt;
            return data;
        }

        // The write runs on the thread pool; tests wait for it.
        string Write(int slot, SaveData data) => _saves.WriteAsync(slot, data).Result;

        [Test]
        public void Write_ThenTryRead_GivesBackEveryEntryAndTheHeader()
        {
            string problem = Write(2, Saved(42));
            Assert.IsNull(problem, problem);

            Assert.IsTrue(_saves.TryRead(2, out SaveData read, out problem), problem);
            Assert.IsTrue(read.TryGet("test", out TestEntry entry));
            Assert.AreEqual(42, entry.Number);
            Assert.AreEqual("hello", entry.Text);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, entry.List);
            Assert.AreEqual(4, read.Header.Day);
            Assert.AreEqual(SaveService.Version, read.Header.Version);
            Assert.IsFalse(read.Has("_header"), "the header is not one of the entries");
        }

        [Test]
        public void TryRead_MissingKey_LeavesTheSystemOnItsNewGameState()
        {
            Write(1, Saved(1));
            _saves.TryRead(1, out SaveData read, out _);

            Assert.IsFalse(read.TryGet("missing", out TestEntry entry));
            Assert.IsNull(entry);
        }

        [Test]
        public void TryRead_DamagedFile_FallsBackToThePreviousSave()
        {
            Write(1, Saved(1));
            Write(1, Saved(2));
            File.WriteAllText(_saves.PathOf(1), "{ not json at all");

            Assert.IsTrue(_saves.TryRead(1, out SaveData read, out string problem, out bool fromBackup), problem);
            Assert.IsTrue(fromBackup);
            read.TryGet("test", out TestEntry entry);
            Assert.AreEqual(1, entry.Number, "the backup is the save before the last one");
        }

        [Test]
        public void Write_OverADamagedFile_KeepsTheLastGoodBackup()
        {
            Write(1, Saved(1));
            Write(1, Saved(2));
            File.WriteAllText(_saves.PathOf(1), "garbage");
            Write(1, Saved(3));
            File.WriteAllText(_saves.PathOf(1), "garbage again");

            _saves.TryRead(1, out SaveData read, out _);
            read.TryGet("test", out TestEntry entry);
            Assert.AreEqual(1, entry.Number);
        }

        [Test]
        public void TryRead_SaveFromAnotherVersion_IsRefused_NotSwappedForItsBackup()
        {
            Write(1, Saved(1));
            SaveData data = Saved(2);
            Write(1, data);
            string text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(_saves.PathOf(1)),
                $@"""Version""\s*:\s*{SaveService.Version}\b", "\"Version\" : 999");
            Assert.AreNotEqual(File.ReadAllText(_saves.PathOf(1)), text, "the version was found in the file");
            File.WriteAllText(_saves.PathOf(1), text);

            Assert.IsTrue(File.Exists(_saves.PathOf(1) + ".bak"), "a readable older backup exists");
            Assert.IsFalse(_saves.TryRead(1, out _, out string problem));
            Assert.AreEqual(SaveService.NewerVersion.Text(), problem);
        }

        [Test]
        public void Write_OverASaveFromAnotherVersion_KeepsItBesideTheSlot()
        {
            Write(1, Saved(1));
            string path = _saves.PathOf(1);
            string other = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path),
                $@"""Version""\s*:\s*{SaveService.Version}\b", "\"Version\" : 999");
            File.WriteAllText(path, other);

            Assert.IsNull(Write(1, Saved(2)));
            Assert.IsNull(Write(1, Saved(3)));

            string kept = SaveService.OtherVersionPath(path, 999);
            Assert.IsTrue(File.Exists(kept), "the other version's save survives the overwrite");
            Assert.AreEqual(other, File.ReadAllText(kept), "and later saves don't replace it");
            Assert.IsTrue(_saves.TryRead(1, out SaveData read, out string problem), problem);
            read.TryGet("test", out TestEntry entry);
            Assert.AreEqual(3, entry.Number);
        }

        [Test]
        public void Info_IsKeptUntilTheSlotsFileChanges()
        {
            Write(1, Saved(1, savedAt: 10));
            SaveSlotInfo first = _saves.Info(1);
            Assert.AreSame(first.Header, _saves.Info(1).Header, "an unchanged file isn't parsed again");

            Write(1, Saved(2, savedAt: 20));
            Assert.AreEqual(20, _saves.Info(1).Header.SavedAtUtcTicks, "a new write is read again");
            _saves.Delete(1, out _);
            Assert.IsTrue(_saves.Info(1).IsEmpty);
        }

        [Test]
        public void StartFresh_DropsTheLastGamesStatModifiers()
        {
            var registry = new SaveRegistry();
            var stats = new StatModifiers();
            var section = new StatSaveSection(registry, stats);
            section.Initialize();
            stats.Set("upgrade", StatId.WalkSpeed, multiply: 2f);

            registry.StartFresh();

            Assert.IsEmpty(stats.All, "a new game doesn't inherit the loaded game's upgrades");
            section.Dispose();
        }

        [Test]
        public void Create_CarriesEntriesNoSectionWroteThisTime()
        {
            Write(1, Saved(7));
            _saves.TryRead(1, out SaveData loaded, out _);

            SaveData next = _saves.Create(loaded);
            next.Set("other", new TestEntry { Number = 9 });

            Assert.IsTrue(next.TryGet("test", out TestEntry carried));
            Assert.AreEqual(7, carried.Number);
            Assert.IsTrue(next.Has("other"));
        }

        [Test]
        public void InfoAndLatestSlot_DescribeTheSlots()
        {
            Write(1, Saved(1, savedAt: 100));
            Write(3, Saved(3, savedAt: 300));

            Assert.IsTrue(_saves.Info(2).IsEmpty);
            Assert.AreEqual(4, _saves.Info(1).Header.Day);
            Assert.AreEqual(3, _saves.LatestSlot());
            Assert.IsFalse(_saves.TryRead(2, out _, out string problem));
            Assert.AreEqual(SaveService.EmptySlot.Text(), problem);
        }

        [Test]
        public void Delete_RemovesTheSaveAndItsBackup()
        {
            Write(2, Saved(1));
            Write(2, Saved(2));

            Assert.IsTrue(_saves.Delete(2, out _));
            Assert.IsFalse(_saves.Exists(2));
            Assert.IsFalse(File.Exists(_saves.PathOf(2) + ".bak"));
            Assert.IsFalse(_saves.Delete(2, out _));
        }

        [Test]
        public void Write_SlotOutOfRange_IsRefused()
        {
            Assert.IsNotNull(Write(0, Saved(1)));
            Assert.IsNotNull(Write(_settings.SlotCount + 1, Saved(1)));
        }

        [Test]
        public void WriteAsync_TakesTheBytesAtOnce_AndWritesOneAtATime()
        {
            SaveData data = Saved(1);
            var first = _saves.WriteAsync(1, data);
            data.Set("test", new TestEntry { Number = 5 });
            var second = _saves.WriteAsync(1, Saved(2));
            Assert.IsNull(first.Result);
            Assert.IsNull(second.Result);

            _saves.TryRead(1, out SaveData read, out _);
            read.TryGet("test", out TestEntry entry);
            Assert.AreEqual(2, entry.Number, "the last write wins");
            File.WriteAllText(_saves.PathOf(1), "garbage");
            _saves.TryRead(1, out read, out _);
            read.TryGet("test", out entry);
            Assert.AreEqual(1, entry.Number, "the backup holds the first write as it was when it started");
        }

        [Test]
        public void EditorSlot_IsListedAfterThePlayersSlots_InTheEditor()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3, SaveService.EditorSlot }, new List<int>(_saves.Slots));
            StringAssert.EndsWith("slot_editor.es3", _saves.PathOf(SaveService.EditorSlot));
            Assert.AreEqual(SaveFormat.EditorSlotName.Text(), SaveFormat.SlotName(SaveService.EditorSlot));

            Write(1, Saved(1, savedAt: 100));
            Write(SaveService.EditorSlot, Saved(9, savedAt: 200));
            Assert.AreEqual(SaveService.EditorSlot, _saves.LatestSlot());
            Assert.IsTrue(_saves.Delete(SaveService.EditorSlot, out _));
            Assert.AreEqual(1, _saves.LatestSlot());
        }

        [Test]
        public void Registry_RestoresInOrder_AndSkipsASectionThatFails()
        {
            var registry = new SaveRegistry();
            var restored = new List<int>();
            new OrderedSection(registry, SaveOrder.Player, restored);
            new OrderedSection(registry, SaveOrder.Clock, restored);
            new OrderedSection(registry, SaveOrder.World, restored, fails: true);
            new OrderedSection(registry, SaveOrder.Random, restored);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Loading OrderedSection failed"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("broken section"));
            int failed = registry.Restore(_saves.Create());

            Assert.AreEqual(1, failed);
            CollectionAssert.AreEqual(new[] { SaveOrder.Random, SaveOrder.Clock, SaveOrder.Player }, restored);
        }

        [Test]
        public void Registry_Capture_SkipsASectionThatFails_AndKeepsItsCarriedEntry()
        {
            Write(1, Saved(7));
            _saves.TryRead(1, out SaveData loaded, out _);
            var registry = new SaveRegistry();
            new WritingSection(registry, "test", fails: true);
            new WritingSection(registry, "other");
            SaveData next = _saves.Create(loaded);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Saving WritingSection failed"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("broken capture"));
            Assert.AreEqual(1, registry.Capture(next));

            Assert.IsTrue(next.TryGet("test", out TestEntry kept));
            Assert.AreEqual(7, kept.Number, "the failed section's last saved entry is kept");
            Assert.IsTrue(next.Has("other"));
        }

        [Test]
        public void RandomService_Restore_ContinuesEveryStreamWhereItWas()
        {
            var random = new RandomService(1234);
            RandomStream loot = random.Stream("loot");
            loot.NextULong();
            loot.NextULong();
            Dictionary<string, ulong> states = random.CaptureStreams();
            ulong next = loot.NextULong();

            var loaded = new RandomService(999);
            loaded.Stream("loot").NextULong();
            loaded.Restore(1234, states);

            Assert.AreEqual(next, loaded.Stream("loot").NextULong());
            Assert.AreEqual(new RandomService(1234).Stream("weather").NextULong(), loaded.Stream("weather").NextULong(),
                "a stream the save doesn't list restarts from the seed");
        }

        // ---- items ----

        static ItemCatalog Catalog() =>
            AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath) ?? throw new AssertionException("missing " + CatalogPath);

        [Test]
        public void ItemCatalog_ListsEveryItemDefinition_ByItsAssetGuid()
        {
            ItemCatalog catalog = Catalog();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.AreSame(item, catalog.Find(guid),
                    $"{AssetDatabase.GUIDToAssetPath(guid)} is missing from the Item Catalog: select it and press Collect");
                Assert.AreEqual(guid, catalog.IdOf(item));
            }
        }

        [Test]
        public void ItemSaves_KeepAGunsRoundsAndJam()
        {
            ItemCatalog catalog = Catalog();
            GunDefinition gun = catalog.Items.Values.OfType<GunDefinition>().FirstOrDefault(g => g.Magazine.Size > 1);
            Assert.IsNotNull(gun, "the catalog has no gun");
            var instance = (GunInstance)gun.CreateInstance();
            instance.SetRounds(1);
            instance.SetJammed(true);

            var copy = (GunInstance)ItemSaves.Read(ItemSaves.Write(instance, catalog), catalog);

            Assert.AreSame(gun, copy.Definition);
            Assert.AreEqual(1, copy.Rounds);
            Assert.IsTrue(copy.IsJammed);
        }

        [Test]
        public void ItemSaves_KeepAConsumablesUsesLeft()
        {
            ItemCatalog catalog = Catalog();
            ConsumableDefinition kit = catalog.Items.Values.OfType<ConsumableDefinition>().FirstOrDefault(c => c.Uses > 1);
            Assert.IsNotNull(kit, "the catalog has no consumable with several uses");
            var instance = (ConsumableInstance)kit.CreateInstance();
            instance.UseOnce();

            var copy = (ConsumableInstance)ItemSaves.Read(ItemSaves.Write(instance, catalog), catalog);

            Assert.AreEqual(kit.Uses - 1, copy.UsesLeft);
        }

        [Test]
        public void ItemSaves_ContainerSlots_ComeBackInTheirSlots()
        {
            ItemCatalog catalog = Catalog();
            ItemDefinition stackable = catalog.Items.Values.First(i => i.MaxStack > 5);
            var container = new ItemContainer(6);
            container.TryPlace(4, stackable.CreateInstance(5));
            container.TryPlace(1, stackable.CreateInstance(2));
            List<SlotSave> saved = ItemSaves.WriteSlots(container, catalog);

            var loaded = new ItemContainer(6);
            loaded.TryPlace(0, stackable.CreateInstance(1));
            List<ItemInstance> leftOver = ItemSaves.ReadSlots(loaded, saved, catalog);

            Assert.IsEmpty(leftOver);
            Assert.IsNull(loaded[0], "the slots are emptied first");
            Assert.AreEqual(2, loaded[1].Count);
            Assert.AreEqual(5, loaded[4].Count);
        }

        [Test]
        public void ItemSaves_UnknownId_IsDroppedWithAWarning()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no longer exists"));
            Assert.IsNull(ItemSaves.Read(new ItemSave { Id = "not-an-item", Count = 1 }, Catalog()));
        }
    }
}
