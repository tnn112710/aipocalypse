using System.Collections.Generic;
using System.Reflection;
using MB13.Aipocalypse.Core.CampfireSystem;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Tests
{
    public sealed class CampfireTests
    {
        const string WoodPath = "Assets/_Game/Content/Items/Wood/Wood.asset";
        const string CatalogPath = "Assets/_Game/Data/Equipment/ItemCatalog.asset";

        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }

        MaterialDefinition Wood(float fuelHours = 2f)
        {
            var wood = Track(TestItems.Create<MaterialDefinition>(maxStack: 20, weight: 1f));
            var so = new SerializedObject(wood);
            so.FindProperty("_fuelHours").floatValue = fuelHours;
            so.ApplyModifiedPropertiesWithoutUndo();
            return wood;
        }

        CampfireSettings Settings()
        {
            var settings = Track(ScriptableObject.CreateInstance<CampfireSettings>());
            settings.RainBurn[SkyWeather.LightRain] = 1.25f;
            settings.RainBurn[SkyWeather.MediumRain] = 1.5f;
            settings.RainBurn[SkyWeather.HeavyRain] = 2f;
            return settings;
        }

        static CampfireFuel Fuel(float fuel = 8f, bool lit = false, float max = 8f, float shrinkBelow = 0.25f)
        {
            var campfire = new CampfireFuel(max, shrinkBelow, fuel);
            if (lit)
                campfire.Light();
            return campfire;
        }

        // ---------------------------------------------------------------- fuel rules

        [Test]
        public void Lit_BurnsAnHourPerGameHour()
        {
            CampfireFuel fire = Fuel(6f, lit: true);

            fire.Advance(1.5f, 1f);

            Assert.AreEqual(4.5f, fire.Fuel, 1e-4f);
            Assert.IsTrue(fire.Lit);
        }

        [Test]
        public void Unlit_DoesNotBurn()
        {
            CampfireFuel fire = Fuel(6f);

            fire.Advance(3f, 2f);

            Assert.AreEqual(6f, fire.Fuel, 1e-4f);
        }

        [TestCase(SkyWeather.Clear, 1f)]
        [TestCase(SkyWeather.LightRain, 1.25f)]
        [TestCase(SkyWeather.MediumRain, 1.5f)]
        [TestCase(SkyWeather.HeavyRain, 2f)]
        [TestCase(SkyWeather.Fog, 1f)]
        public void Rain_BurnsFaster(SkyWeather sky, float scale)
        {
            Assert.AreEqual(scale, Settings().BurnScale(sky), 1e-4f, "user: × 1.25 / 1.5 / 2 in light / medium / heavy rain");
            CampfireFuel fire = Fuel(8f, lit: true);

            fire.Advance(2f, Settings().BurnScale(sky));

            Assert.AreEqual(8f - 2f * scale, fire.Fuel, 1e-4f);
        }

        [Test]
        public void RunsOut_GoesOutOnceAndStaysAtZero()
        {
            CampfireFuel fire = Fuel(2f, lit: true);
            int wentOut = 0;
            fire.WentOut += () => wentOut++;

            bool out1 = fire.Advance(7f, 1f);     // a sleep skip burns hours in one step
            bool out2 = fire.Advance(1f, 1f);

            Assert.IsTrue(out1);
            Assert.IsFalse(out2);
            Assert.AreEqual(1, wentOut);
            Assert.AreEqual(0f, fire.Fuel);
            Assert.IsFalse(fire.Lit);
        }

        [Test]
        public void Add_TakesOnlyWhatFits()
        {
            CampfireFuel fire = Fuel(6f);

            Assert.IsTrue(fire.Add(2f), "6 + 2 = 8 fits");
            Assert.AreEqual(8f, fire.Fuel, 1e-4f);
            fire.Advance(0f, 1f);
            CampfireFuel nearlyFull = Fuel(7f);
            Assert.IsFalse(nearlyFull.CanAdd(2f), "7 + 2 passes the max: a whole log doesn't fit");
            Assert.IsFalse(nearlyFull.Add(2f));
            Assert.AreEqual(7f, nearlyFull.Fuel, 1e-4f);
        }

        [Test]
        public void Light_NeedsFuel_PutOutKeepsIt()
        {
            CampfireFuel empty = Fuel(0f);
            Assert.IsFalse(empty.Light());
            Assert.IsFalse(empty.Lit);

            CampfireFuel fire = Fuel(5f);
            Assert.IsTrue(fire.Light());
            fire.PutOut();
            Assert.IsFalse(fire.Lit);
            Assert.AreEqual(5f, fire.Fuel, 1e-4f);
        }

        [TestCase(8f, 1f)]
        [TestCase(4f, 1f)]
        [TestCase(2f, 1f)]
        [TestCase(1f, 0.5f)]
        [TestCase(0f, 0f)]
        public void Strength_ConstantAboveAQuarterThenShrinks(float fuel, float strength)
        {
            Assert.AreEqual(strength, Fuel(fuel, lit: fuel > 0f).Strength, 1e-4f, "user: constant above 25 %, shrinking below");
        }

        [Test]
        public void Strength_IsZeroWhileUnlit()
        {
            Assert.AreEqual(0f, Fuel(8f).Strength);
        }

        // ---------------------------------------------------------------- campfire and pile components

        GameObject Go(string name) => Track(new GameObject(name));

        Campfire MakeCampfire(out SafeLight safe) => MakeCampfire(out safe, out _, out _);

        // With a core light and four logs in the pit.
        Campfire MakeCampfire(out SafeLight safe, out Light core, out GameObject[] logs)
        {
            GameObject root = Go("Campfire");
            var campfire = root.AddComponent<Campfire>();
            var safeGo = new GameObject("Safe");
            safeGo.transform.SetParent(root.transform, false);
            safe = safeGo.AddComponent<SafeLight>();
            var coreGo = new GameObject("Core Light");
            coreGo.transform.SetParent(root.transform, false);
            core = coreGo.AddComponent<Light>();
            var fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(root.transform, false);
            var fill = fillGo.AddComponent<Light>();
            fill.shadows = LightShadows.Soft;
            logs = new GameObject[4];
            var so = new SerializedObject(campfire);
            so.FindProperty("_settings").objectReferenceValue = Settings();
            so.FindProperty("_safeLight").objectReferenceValue = safe;
            so.FindProperty("_core").objectReferenceValue = core;
            so.FindProperty("_fill").objectReferenceValue = fill;
            SerializedProperty logList = so.FindProperty("_logs");
            logList.arraySize = logs.Length;
            for (int i = 0; i < logs.Length; i++)
            {
                logs[i] = new GameObject("Log " + i);
                logs[i].transform.SetParent(root.transform, false);
                logList.GetArrayElementAtIndex(i).objectReferenceValue = logs[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return campfire;
        }

        // What the scene installer does (VContainer calls the private [Inject] method).
        static void Install(Campfire campfire, CampfireRegistry registry) =>
            typeof(Campfire).GetMethod("Construct", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(campfire, new object[] { registry, null, null });

        // A log thrown at the pit, still in the air, hitting its feed trigger.
        WorldItem ThrownInto(Campfire campfire, MaterialDefinition wood, int count)
        {
            GameObject thrown = Go("Thrown");
            Collider hit = thrown.AddComponent<BoxCollider>();
            var item = thrown.AddComponent<WorldItem>();
            var so = new SerializedObject(item);
            so.FindProperty("_definition").objectReferenceValue = wood;
            so.FindProperty("_count").intValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            item.Throw(Vector3.zero);
            typeof(Campfire).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(campfire, new object[] { hit });
            return item;
        }

        static Inventory Carrying(ItemDefinition item, int count)
        {
            Inventory inventory = TestItems.Inventory();
            if (count > 0)
                inventory.TryAdd(item.CreateInstance(count), out _);
            return inventory;
        }

        [Test]
        public void NewCampfire_IsFullAndUnlit()
        {
            Campfire campfire = MakeCampfire(out _);

            Assert.AreEqual(8f, campfire.Fuel.Fuel, 1e-4f, "user: a new game's pit is full");
            Assert.IsFalse(campfire.Fuel.Lit, "the player lights it");
        }

        [Test]
        public void Feed_BurnsOnlyWholeLogsThatFit()
        {
            Campfire campfire = MakeCampfire(out _);
            campfire.RestoreSaved(3f, false);
            ItemInstance thrown = Wood().CreateInstance(3);

            int burned = campfire.Feed(thrown);

            Assert.AreEqual(2, burned, "3 + 2 + 2 = 7; a third log would pass 8");
            Assert.AreEqual(1, thrown.Count, "the rest stays a pickup");
            Assert.AreEqual(7f, campfire.Fuel.Fuel, 1e-4f);
        }

        [Test]
        public void Feed_IgnoresWhatDoesNotBurn()
        {
            Campfire campfire = MakeCampfire(out _);
            campfire.RestoreSaved(0f, false);

            Assert.AreEqual(0, campfire.Feed(Wood(fuelHours: 0f).CreateInstance(2)));
            Assert.AreEqual(0, campfire.Feed(Track(TestItems.Create<CurrencyDefinition>(unlimited: true)).CreateInstance(5)));
        }

        [Test]
        public void Tap_AddsALogWhenCarryingWoodElseLights()
        {
            Campfire campfire = MakeCampfire(out _);
            campfire.RestoreSaved(4f, false);
            MaterialDefinition wood = Wood();
            Inventory inventory = Carrying(wood, 3);

            Assert.AreEqual(CampfireAction.AddWood, campfire.ActionFor(inventory));
            campfire.Tap(inventory);
            Assert.AreEqual(6f, campfire.Fuel.Fuel, 1e-4f);
            Assert.AreEqual(2, inventory.CountOf(wood), "one log left the inventory");
            Assert.IsFalse(campfire.Fuel.Lit, "adding wood doesn't light it");

            campfire.Tap(inventory);
            Assert.AreEqual(8f, campfire.Fuel.Fuel, 1e-4f);
            Assert.AreEqual(CampfireAction.Light, campfire.ActionFor(inventory), "full: the tap lights it");
            campfire.Tap(inventory);
            Assert.IsTrue(campfire.Fuel.Lit);
            Assert.AreEqual(1, inventory.CountOf(wood));
            Assert.AreEqual(CampfireAction.None, campfire.ActionFor(inventory), "lit and full: nothing on a tap");
        }

        [Test]
        public void Tap_OnAnEmptyPitWithoutWood_DoesNothing()
        {
            Campfire campfire = MakeCampfire(out _);
            campfire.RestoreSaved(0f, false);
            Inventory inventory = Carrying(Wood(), 0);

            Assert.AreEqual(CampfireAction.None, campfire.ActionFor(inventory));
            campfire.Tap(inventory);
            Assert.IsFalse(campfire.Fuel.Lit);
        }

        [Test]
        public void Hold_PutsOutAndKeepsFuel()
        {
            Campfire campfire = MakeCampfire(out _);
            campfire.RestoreSaved(5f, true);
            GameObject actor = Go("Actor");

            Assert.IsTrue(campfire.TryGetHoldPrompt(actor, out _));
            campfire.InteractHold(actor);

            Assert.IsFalse(campfire.Fuel.Lit);
            Assert.AreEqual(5f, campfire.Fuel.Fuel, 1e-4f);
            Assert.IsFalse(campfire.TryGetHoldPrompt(actor, out _), "nothing to put out");
        }

        [Test]
        public void SafeLight_OnlyWhileLit_AndOffWhenItBurnsOut()
        {
            Campfire campfire = MakeCampfire(out SafeLight safe);
            campfire.Refresh(0f);
            Assert.IsFalse(safe.enabled, "unlit: no safety");

            campfire.Fuel.Light();
            campfire.Refresh(0f);
            Assert.IsTrue(safe.enabled);
            Assert.AreEqual(22.5f, safe.Radius, 1e-4f, "user: safe within 1.5 × the 15 m lit area");

            campfire.Burn(9f, 1f);
            campfire.Refresh(0f);
            Assert.IsFalse(safe.enabled, "burned out");
        }

        [Test]
        public void Strength_ScalesTheSafeRadius()
        {
            Campfire campfire = MakeCampfire(out SafeLight safe);
            campfire.RestoreSaved(1f, true);   // 1 of 8 h = half of the 25 % shrink band

            campfire.Refresh(0f);

            Assert.AreEqual(11.25f, safe.Radius, 1e-4f);
        }

        [Test]
        public void Paused_TheLooksHoldInsteadOfJumping()
        {
            Campfire campfire = MakeCampfire(out _, out Light core, out _);
            campfire.Refresh(0f, snap: true);
            Assert.IsFalse(core.enabled);

            campfire.Fuel.Light();
            campfire.Refresh(0f);   // a paused frame
            Assert.IsFalse(core.enabled, "paused: the flare-up waits instead of snapping on");

            campfire.Refresh(0.6f);   // half of FadeSeconds 1.2
            Assert.IsTrue(core.enabled);
            Assert.AreEqual(4f, core.range, 1e-3f, "half way: CoreRange 8 × 0.5");
        }

        [TestCase(8f, 4)]
        [TestCase(7.2f, 4)]
        [TestCase(5f, 3)]
        [TestCase(1f, 1)]
        [TestCase(0f, 0)]
        public void Logs_ShowTheFuelLeft(float fuel, int shown)
        {
            Campfire campfire = MakeCampfire(out _, out _, out GameObject[] logs);

            campfire.RestoreSaved(fuel, false);

            Assert.AreEqual(shown, System.Array.FindAll(logs, log => log.activeSelf).Length, "a log for every started quarter of the max");
        }

        [Test]
        public void ThrownWood_BurnsOnlyWithTheInstaller()
        {
            MaterialDefinition wood = Wood();
            Campfire prop = MakeCampfire(out _);
            prop.RestoreSaved(2f, false);
            WorldItem missed = ThrownInto(prop, wood, 2);
            Assert.AreEqual(2, missed.Item.Count, "no installer: the pit is a prop and the logs land as pickups");
            Assert.AreEqual(2f, prop.Fuel.Fuel, 1e-4f);

            Campfire fire = MakeCampfire(out _);
            fire.RestoreSaved(2f, false);
            Install(fire, new CampfireRegistry());
            // The burned item is removed with Destroy, which edit mode refuses (in Play it just goes).
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
            ThrownInto(fire, wood, 2);
            Assert.AreEqual(6f, fire.Fuel.Fuel, 1e-4f, "both logs burned");
        }

        [Test]
        public void AddingALog_FlaresTheLightsThenSettles()
        {
            Campfire campfire = MakeCampfire(out _, out Light core, out _);
            campfire.Settings.FlickerAmount = 0f;   // a test instance: keeps the noise out of the comparison
            campfire.RestoreSaved(4f, true);
            float steady = core.intensity;
            Inventory inventory = Carrying(Wood(), 1);

            campfire.Tap(inventory);
            campfire.Refresh(0.05f);
            Assert.Greater(core.intensity, steady * 1.3f, "a log goes on: the fire flares up");

            campfire.Refresh(2f);
            Assert.AreEqual(steady, core.intensity, steady * 0.01f, "then settles back");
        }

        [Test]
        public void OnlyTheHomeFireCastsShadows()
        {
            var registry = new CampfireRegistry();
            Campfire home = MakeCampfire(out _), other = MakeCampfire(out _);
            Install(home, registry);
            Install(other, registry);
            Light homeFill = home.transform.Find("Fill Light").GetComponent<Light>();
            Light otherFill = other.transform.Find("Fill Light").GetComponent<Light>();

            home.Refresh(0f, snap: true);
            other.Refresh(0f, snap: true);
            Assert.AreEqual(LightShadows.Soft, homeFill.shadows);
            Assert.AreEqual(LightShadows.None, otherFill.shadows, "each shadowed fill costs ~0.4 ms of GPU: one is enough");

            registry.Remove(home);
            other.Refresh(0f, snap: true);
            Assert.AreEqual(LightShadows.Soft, otherFill.shadows, "the next fire becomes home and takes the shadows");
        }

        [Test]
        public void Prefab_CatchesThrownWoodWideAndLow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Structures/Campfire/Campfire.prefab");
            var box = prefab.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, "a catch box on the root (its trigger messages reach the Campfire)");
            Assert.IsTrue(box.isTrigger);
            Assert.GreaterOrEqual(box.size.x * 0.5f, 1.6f, "throws aimed at the pit from 4.5 m land ~1.4 m short");
            Assert.GreaterOrEqual(box.size.z * 0.5f, 1.6f);
            Assert.LessOrEqual(box.center.y + box.size.y * 0.5f, 1f, "low: the interact ray reaches the fire from only a little farther");
            Assert.AreEqual(0, prefab.layer, "Default: Ignore Raycast ignores collisions with thrown items");
            var campfire = new SerializedObject(prefab.GetComponent<Campfire>());
            Assert.AreEqual(4, campfire.FindProperty("_logs").arraySize, "four logs in the pit");
            Assert.IsTrue(campfire.FindProperty("_sputter._entity").objectReferenceValue != null, "the dying-fire sputter");
        }

        [Test]
        public void Ticker_BurnsWhatTheClockMoved_NotJumpsOrRewinds()
        {
            Campfire fire = MakeCampfire(out _);
            fire.RestoreSaved(8f, true);
            var registry = new CampfireRegistry();
            registry.Add(fire);
            var ticker = new CampfireTicker(registry, new ContainerBuilder().Build());

            ticker.Observe(100d, null);
            Assert.AreEqual(8f, fire.Fuel.Fuel, 1e-4f, "the first look only notes the time");
            ticker.Observe(101.5d, null);
            Assert.AreEqual(6.5f, fire.Fuel.Fuel, 1e-4f, "exactly what the clock moved, a skip frame included");
            ticker.Rebase(110d);   // a jump (TimeJumped)
            ticker.Observe(110.5d, null);
            Assert.AreEqual(6f, fire.Fuel.Fuel, 1e-4f, "a jump doesn't burn");
            ticker.Observe(105d, null);   // backwards (a load)
            ticker.Observe(105.5d, SkyWeather.HeavyRain);
            Assert.AreEqual(5f, fire.Fuel.Fuel, 1e-4f, "after going back it carries on from there; 0.5 h × 2 in heavy rain");
        }

        [Test]
        public void Ticker_BurnsEveryFireWithTheRain()
        {
            Campfire a = MakeCampfire(out _), b = MakeCampfire(out _);
            a.RestoreSaved(8f, true);
            b.RestoreSaved(8f, false);

            CampfireTicker.Step(new[] { a, b }, 2f, SkyWeather.HeavyRain);

            Assert.AreEqual(4f, a.Fuel.Fuel, 1e-4f, "2 h × 2 in heavy rain");
            Assert.AreEqual(8f, b.Fuel.Fuel, 1e-4f, "unlit");
            CampfireTicker.Step(new[] { a }, 1f, null);
            Assert.AreEqual(3f, a.Fuel.Fuel, 1e-4f, "no weather service: × 1");
        }

        WoodPile MakePile(MaterialDefinition wood)
        {
            var pile = Go("Pile").AddComponent<WoodPile>();
            var so = new SerializedObject(pile);
            so.FindProperty("_settings").objectReferenceValue = Settings();
            so.FindProperty("_wood").objectReferenceValue = wood;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pile;
        }

        [Test]
        public void Pile_GivesWhatFitsAndRefillsAtDawn()
        {
            MaterialDefinition wood = Wood();
            WoodPile pile = MakePile(wood);
            Assert.AreEqual(8, pile.Stock, "user: 8 logs");

            var equipment = new Equipment(TestItems.Inventory());
            Assert.AreEqual(1, pile.Take(equipment, 1), "a tap: one log");
            Assert.AreEqual(7, pile.Stock);
            Assert.AreEqual(7, pile.Take(equipment, pile.Stock), "a hold: the rest");
            Assert.AreEqual(0, pile.Stock);
            Assert.AreEqual(8, equipment.Inventory.CountOf(wood));

            pile.OnDayPhase(DayPhase.Evening);
            Assert.AreEqual(0, pile.Stock, "only the morning refills it");
            pile.OnDayPhase(DayPhase.Dawn);
            Assert.AreEqual(8, pile.Stock);
        }

        [Test]
        public void Pile_LeavesWhatDoesNotFit()
        {
            MaterialDefinition wood = Wood();
            WoodPile pile = MakePile(wood);
            Inventory full = TestItems.Inventory(backpackSlots: 1);
            full.TryAdd(wood.CreateInstance(15), out _);   // one slot of 20: room for 5

            Assert.AreEqual(5, pile.Take(new Equipment(full), pile.Stock));
            Assert.AreEqual(3, pile.Stock);
        }

        // ---------------------------------------------------------------- save

        static T WithSaveId<T>(T component, string id) where T : Component
        {
            var saveId = component.gameObject.AddComponent<SaveId>();
            var so = new SerializedObject(saveId);
            so.FindProperty("_id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        [Test]
        public void Save_RoundTripsFuelLitAndPileStock()
        {
            var saveSettings = Track(ScriptableObject.CreateInstance<SaveSettings>());
            saveSettings.Folder = "CampfireTests_" + System.Guid.NewGuid().ToString("N");
            var saves = new SaveService(saveSettings);
            var registry = new CampfireRegistry();
            Campfire fire = WithSaveId(MakeCampfire(out SafeLight safe), "fire");
            WoodPile pile = WithSaveId(MakePile(Wood()), "pile");
            registry.Add(fire);
            registry.Add(pile);
            var section = new CampfireSaveSection(new SaveRegistry(), registry);
            fire.RestoreSaved(3.5f, true);
            pile.SetStock(2);
            SaveData data = saves.Create();

            section.Capture(data);
            fire.RestoreSaved(8f, false);
            pile.SetStock(8);
            section.Restore(data);

            Assert.AreEqual(3.5f, fire.Fuel.Fuel, 1e-4f);
            Assert.IsTrue(fire.Fuel.Lit);
            Assert.IsTrue(safe.enabled, "a loaded lit fire keeps the player safe at once");
            Assert.AreEqual(2, pile.Stock);
        }

        [Test]
        public void Save_WithoutAnEntry_KeepsTheNewGameState()
        {
            var saveSettings = Track(ScriptableObject.CreateInstance<SaveSettings>());
            saveSettings.Folder = "CampfireTests_" + System.Guid.NewGuid().ToString("N");
            var registry = new CampfireRegistry();
            Campfire fire = WithSaveId(MakeCampfire(out _), "fire");
            registry.Add(fire);

            new CampfireSaveSection(new SaveRegistry(), registry).Restore(new SaveService(saveSettings).Create());

            Assert.AreEqual(8f, fire.Fuel.Fuel, 1e-4f);
            Assert.IsFalse(fire.Fuel.Lit);
        }

        // ---------------------------------------------------------------- console

        [Test]
        public void Commands_SetFuelLightPutOutAndPile()
        {
            var registry = new CampfireRegistry();
            Campfire fire = MakeCampfire(out _);
            WoodPile pile = MakePile(Wood());
            registry.Add(fire);
            registry.Add(pile);
            var console = new MB13.Aipocalypse.Core.Debugging.CommandRegistry();
            var commands = new CampfireCommands(console, registry);
            commands.Initialize();

            Assert.IsTrue(console.Execute("campfire.fuel 3").Succeeded);
            Assert.AreEqual(3f, fire.Fuel.Fuel, 1e-4f);
            Assert.IsTrue(console.Execute("campfire.light").Succeeded);
            Assert.IsTrue(fire.Fuel.Lit);
            Assert.IsTrue(console.Execute("campfire.out").Succeeded);
            Assert.IsFalse(fire.Fuel.Lit);
            Assert.IsTrue(console.Execute("campfire.pile 3").Succeeded);
            Assert.AreEqual(3, pile.Stock);
            StringAssert.Contains("3.0", console.Execute("campfire.status").Message);
            commands.Dispose();
        }

        // ---------------------------------------------------------------- HUD fuel bar

        [TestCase(true, true, 0.9f, 1f, true, true, false, TestName = "FuelBar_HiddenWhileLitAndWellFed")]
        [TestCase(true, true, 0.3f, 1f, true, true, true, TestName = "FuelBar_ShowsWhenLitAndLow")]
        [TestCase(true, true, 0.3f, 1f, false, true, true, TestName = "FuelBar_ShowsWhenLitAndLowEvenFarAway")]
        [TestCase(true, false, 1f, 1f, true, true, true, TestName = "FuelBar_ShowsWhenOutAtNightNearHome")]
        [TestCase(true, false, 1f, 1f, false, true, false, TestName = "FuelBar_HiddenWhenOutAtNightFarAway")]
        [TestCase(true, false, 0f, 1f, true, false, false, TestName = "FuelBar_HiddenWhenOutWithNoWoodAnywhere")]
        [TestCase(true, false, 1f, 0f, true, true, false, TestName = "FuelBar_HiddenWhenOutByDay")]
        [TestCase(false, false, 0f, 1f, true, true, false, TestName = "FuelBar_HiddenWithoutAFire")]
        public void FuelBar_ShowsWhenLowOrOutAtNightNearHome(bool hasFire, bool lit, float share, float darkness, bool nearFire, bool canAct, bool shown)
        {
            Assert.AreEqual(shown, CampfireFuelHud.ShouldShow(hasFire, lit, share, 0.35f, darkness, nearFire, canAct));
        }

        [TestCase(7.2f, 1, 7.2f)]
        [TestCase(7.21f, 1, 7.3f)]
        [TestCase(0.01f, 1, 0.1f)]
        [TestCase(0f, 1, 0f)]
        [TestCase(7.2f, 0, 8f)]
        public void StatBar_RoundsUpToTheStylesDecimals(float value, int decimals, float shown)
        {
            Assert.AreEqual(shown, StatBar.Rounded(value, decimals), 1e-4f, "rounded up: a sliver never reads as 0");
        }

        [Test]
        public void FuelBarStyle_ShowsTenthsOfAnHour()
        {
            var style = AssetDatabase.LoadAssetAtPath<StatBarStyle>("Assets/_Game/Data/UI/CampfireFuel.asset");
            Assert.AreEqual(1, style.Decimals, "7.2 h reads 7.2, not 8");
        }

        [Test]
        public void Hud_HasTheFuelBarWired()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/UI/Prefabs/HUD.prefab");
            Transform node = hud.transform.Find("SafeFrame/BottomLeft/FireBar");
            Assert.IsNotNull(node, "SafeFrame/BottomLeft/FireBar");
            var presenter = node.GetComponent<CampfireFuelHud>();
            Assert.IsNotNull(presenter);
            var so = new SerializedObject(presenter);
            Assert.IsNotNull(so.FindProperty("_group").objectReferenceValue, "group");
            Assert.IsNotNull(so.FindProperty("_bar").objectReferenceValue, "bar");
            Assert.IsNotNull(node.GetComponent<Canvas>(), "its own canvas: the bar changes every frame while burning");
        }

        // ---------------------------------------------------------------- wood

        [Test]
        public void Wood_GoesToTheBackpackNotTheHotbar()
        {
            var inventory = new Inventory(
                new ItemContainer(new[] { ItemCategory.Gun, ItemCategory.Consumable | ItemCategory.Tool }),
                new ItemContainer(new[] { ItemCategory.Backpack }), 4, 25f);

            inventory.TryAdd(Wood().CreateInstance(5), out int added);

            Assert.AreEqual(5, added);
            Assert.AreEqual(5, inventory.Backpack.CountOf(inventory.Backpack[0].Definition));
            Assert.IsNull(inventory.Hotbar[1], "wood is never held, so it stays out of the quick slots");
        }

        [Test]
        public void WoodAsset_IsFuelAndWeighsOneKilo()
        {
            var wood = AssetDatabase.LoadAssetAtPath<MaterialDefinition>(WoodPath);

            Assert.IsNotNull(wood, WoodPath);
            Assert.AreEqual(2f, wood.FuelHours, 1e-4f, "a log is 2 h of fire (user)");
            Assert.AreEqual(1f, wood.Weight, 1e-4f, "1 kg a piece (user)");
            Assert.AreEqual(SelectBehaviour.None, wood.Selecting);
            Assert.Greater(wood.MaxStack, 8, "the whole yard pile fits in one stack");
            Assert.IsNotNull(wood.Icon, "icon");
            Assert.IsNotNull(wood.Prefab, "prefab");
            Assert.IsNotNull(wood.Prefab.WorldModel, "the prefab's world model");
            Assert.IsNotNull(wood.Prefab.GetComponent<BoxCollider>(), "pickup box");
            Assert.IsFalse(string.IsNullOrEmpty(wood.DisplayName), "display name");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            Assert.IsNotNull(catalog.IdOf(wood), "in the item catalog (saves, cheat.give)");
        }
    }
}
