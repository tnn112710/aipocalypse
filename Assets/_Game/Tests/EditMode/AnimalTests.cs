using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class AnimalTests
    {
        static readonly TimeWindow Day = new(6f, 20f);
        const float DayLength = 14f;

        [Test]
        public void Earn_OverAWholeWakingDay_GivesTheDayRate()
        {
            var earnings = new AnimalEarnings();

            earnings.Earn(DayLength, DayLength, perDay: 20f, cap: 80f);

            Assert.AreEqual(20f, earnings.Held, 1e-4f);
        }

        [Test]
        public void Earn_StopsAtTheCap()
        {
            var earnings = new AnimalEarnings();

            earnings.Earn(DayLength * 5f, DayLength, perDay: 40f, cap: 80f);

            Assert.AreEqual(80f, earnings.Held, 1e-4f);
        }

        [Test]
        public void Collect_TakesWholeTokensAndKeepsTheFraction()
        {
            var earnings = new AnimalEarnings();
            earnings.Set(7.6f);

            int collected = earnings.Collect();

            Assert.AreEqual(7, collected);
            Assert.AreEqual(0.6f, earnings.Held, 1e-4f);
            Assert.AreEqual(0, earnings.Collect(), "nothing whole is left");
        }

        [Test]
        public void AwakeHours_CountsOnlyTheWakingWindow()
        {
            double from = WorldTime.At(1, 4f).TotalHours;

            Assert.AreEqual(2d, AnimalEarnings.AwakeHours(from, WorldTime.At(1, 8f).TotalHours, Day), 1e-6, "04:00 to 08:00: awake from 06:00");
            Assert.AreEqual(0d, AnimalEarnings.AwakeHours(WorldTime.At(1, 21f).TotalHours, WorldTime.At(2, 5f).TotalHours, Day), 1e-6, "a night");
            Assert.AreEqual(14d, AnimalEarnings.AwakeHours(WorldTime.At(1, 0f).TotalHours, WorldTime.At(2, 0f).TotalHours, Day), 1e-6, "a whole day");
            Assert.AreEqual(14d + 3d, AnimalEarnings.AwakeHours(WorldTime.At(1, 19f).TotalHours, WorldTime.At(3, 8f).TotalHours, Day), 1e-6,
                "a jump over two nights: 1 h + a day + 2 h");
        }

        [Test]
        public void AwakeHours_WithAWindowPastMidnight_CountsBothSidesOfIt()
        {
            var late = new TimeWindow(18f, 2f);

            Assert.AreEqual(8f, AnimalEarnings.DayLength(late), 1e-6f);
            Assert.AreEqual(3d, AnimalEarnings.AwakeHours(WorldTime.At(1, 23f).TotalHours, WorldTime.At(2, 5f).TotalHours, late), 1e-6);
            Assert.AreEqual(2d, AnimalEarnings.AwakeHours(WorldTime.At(1, 0f).TotalHours, WorldTime.At(1, 10f).TotalHours, late), 1e-6,
                "the window that began the day before");
        }

        [Test]
        public void Orders_OpeningTheGateReleases_TheBellCallsHome()
        {
            var orders = new HerdOrders();
            Assert.IsFalse(orders.Released);

            orders.OpenGate();
            Assert.IsTrue(orders.Released);

            Assert.IsTrue(orders.Call());
            Assert.IsFalse(orders.Released);
            Assert.IsTrue(orders.Called);
            Assert.IsFalse(orders.Call(), "it already rang");

            orders.CloseGate();
            orders.OpenGate();
            Assert.IsTrue(orders.Released, "opening the gate again forgets the bell");
        }

        [Test]
        public void Orders_TheBellDoesNothingWhileTheGateIsShut()
        {
            var orders = new HerdOrders();

            Assert.IsFalse(orders.Call());
            Assert.IsFalse(orders.Called);
        }

        [Test]
        public void Orders_EveryChangeBumpsTheVersion()
        {
            var orders = new HerdOrders();
            int version = orders.Version;

            orders.OpenGate();
            Assert.Greater(orders.Version, version);
            version = orders.Version;
            orders.OpenGate();
            Assert.AreEqual(version, orders.Version, "already released: nothing new");
            orders.Call();
            Assert.Greater(orders.Version, version);
        }

        [Test]
        public void ArcVelocity_LandsOnTheTargetAfterTheFlightTime()
        {
            var from = new Vector3(1f, 2f, 3f);
            var to = new Vector3(3f, 0.2f, 2f);
            const float time = 0.9f;

            Vector3 velocity = AnimalManager.ArcVelocity(from, to, time);
            Vector3 landed = from + velocity * time + 0.5f * time * time * Physics.gravity;

            Assert.Less(Vector3.Distance(to, landed), 1e-3f);
            Assert.Greater(velocity.y, 0f, "it rises first: the coins pop up before they fall to the player");
        }

        [Test]
        public void AnimalPrefabs_AreWired()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Animals" });
            int animals = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab.TryGetComponent(out Animal animal))
                    continue;
                animals++;
                var serialized = new SerializedObject(animal);
                var definition = serialized.FindProperty("_definition").objectReferenceValue as AnimalDefinition;
                var animator = serialized.FindProperty("_animator").objectReferenceValue as Animator;
                Assert.IsNotNull(definition, $"{path}: no definition");
                Assert.IsNotNull(animator, $"{path}: no animator");
                Assert.IsNotNull(animator.runtimeAnimatorController, $"{path}: the animator has no controller");
                Assert.IsFalse(animator.applyRootMotion, $"{path}: root motion must be off (the mover moves it)");
                Assert.IsNotNull(serialized.FindProperty("_marker").objectReferenceValue, $"{path}: no coin marker");
                Assert.IsNotNull(prefab.GetComponentInChildren<Collider>(), $"{path}: no collider to look at (Hurry)");
                Assert.Greater(definition.HurrySpeed, definition.WalkSpeed, $"{definition.name}: hurrying must be faster than walking");
                Assert.Greater(definition.TokensPerDayRoaming, definition.TokensPerDayPenned, $"{definition.name}: roaming must pay more");
                Assert.LessOrEqual(definition.CollectAt, definition.MaxHeld, $"{definition.name}: it could never reach CollectAt");
            }
            Assert.Greater(animals, 0, "no animal prefab under Content/Animals");
        }
    }
}
