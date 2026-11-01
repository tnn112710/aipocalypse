using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.SpawnSystem;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    // SpawnService on a flat ground with plain prefabs (no navmesh, no player). Edit mode runs no OnDisable, so returning to the
    // pool is checked in play (AgentScripts/Spawning/SpawnPlayCheck.cs), not here.
    public sealed class SpawnServiceTests
    {
        readonly List<Object> _created = new();
        SpawnSystemSettings _settings;
        SpawnService _spawns;
        PrefabPool _pool;
        SpawnCard _coin;
        SpawnCard _chest;
        IObjectResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            _created.Add(ground);
            Physics.SyncTransforms();

            _settings = Track(ScriptableObject.CreateInstance<SpawnSystemSettings>());
            _settings.SpawnBudgetMs = 1000f;
            _settings.PrewarmPerFrame = 2;
            _coin = Card("Coin", SpawnCategory.Item);
            _chest = Card("Chest", SpawnCategory.Prop);

            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.Register<PlayerRegistry>(Lifetime.Singleton);
            builder.RegisterInstance(_settings);
            builder.Register<SpawnVisibility>(Lifetime.Singleton);
            builder.Register<SpawnPlacer>(Lifetime.Singleton);
            builder.Register<SpawnPointPicker>(Lifetime.Singleton);
            builder.Register<PrefabPool>(Lifetime.Singleton);
            builder.Register<SpawnService>(Lifetime.Singleton);
            _resolver = builder.Build();
            _spawns = _resolver.Resolve<SpawnService>();
            _pool = _resolver.Resolve<PrefabPool>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Spawnable instance in Object.FindObjectsByType<Spawnable>(FindObjectsInactive.Include))
                if (instance.Prefab != null)
                    Object.DestroyImmediate(instance.gameObject);
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
            (_resolver as System.IDisposable)?.Dispose();
        }

        [Test]
        public void Group_CountsMembersStillToComeAsAlive_ThenItsMembers()
        {
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, Vector3.zero, 3));
            Assert.That(handle.IsPlaced, Is.True);
            Assert.That(handle.Count, Is.EqualTo(3));
            Assert.That(handle.Pending, Is.EqualTo(3));
            Assert.That(handle.Alive, Is.EqualTo(3), "members still to come count");
            Assert.That(_spawns.CountAlive(_coin), Is.Zero);

            _spawns.Advance(0.02f);
            Assert.That(handle.Pending, Is.Zero);
            Assert.That(handle.Alive, Is.EqualTo(3));
            Assert.That(handle.IsDone, Is.True);
            Assert.That(_spawns.CountAlive(_coin), Is.EqualTo(3));
            Assert.That(_spawns.CountAlive(SpawnCategory.Item), Is.EqualTo(3));
            Assert.That(_spawns.AliveTotal, Is.EqualTo(3));
        }

        [Test]
        public void Members_StandOnTheGround_FacingTheWayAsked()
        {
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, new Vector3(5f, 20f, 5f), 4).Facing(Vector3.right));
            _spawns.Advance(0.02f);
            var members = new List<Spawnable>();
            handle.GetAlive(members);
            Assert.That(members.Count, Is.EqualTo(4));
            foreach (Spawnable member in members)
            {
                Assert.That(member.transform.position.y, Is.EqualTo(0f).Within(1e-3f), "on the ground, not where it was asked");
                Assert.That(Vector3.Angle(member.transform.forward, Vector3.right), Is.LessThan(1f));
            }
        }

        [Test]
        public void Retire_StopsCountingAsAlive()
        {
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, Vector3.zero, 2));
            _spawns.Advance(0.02f);
            var members = new List<Spawnable>();
            handle.GetAlive(members);
            members[0].Retire();
            members[0].Retire();
            Assert.That(members[0].State, Is.EqualTo(SpawnState.Retired));
            Assert.That(handle.Alive, Is.EqualTo(1));
            Assert.That(_spawns.CountAlive(_coin), Is.EqualTo(1), "retiring twice counts once");
        }

        [Test]
        public void Cancel_DropsMembersStillToCome()
        {
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, Vector3.zero, 3));
            handle.Cancel();
            _spawns.Advance(0.02f);
            Assert.That(handle.Alive, Is.Zero);
            Assert.That(handle.IsDone, Is.True);
            Assert.That(_spawns.CountAlive(_coin), Is.Zero);
        }

        [Test]
        public void DespawnWhenUnseen_StopsCountingAtOnce_AndGoesOnceUnseen()
        {
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, Vector3.zero, 2));
            _spawns.Advance(0.02f);
            var members = new List<Spawnable>();
            handle.GetAlive(members);
            _spawns.Despawn(members[0], DespawnMode.WhenUnseen);
            Assert.That(members[0].State, Is.EqualTo(SpawnState.Leaving));
            Assert.That(_spawns.CountAlive(_coin), Is.EqualTo(1));
            Assert.That(members[0].gameObject.activeSelf, Is.True, "still here this frame");
            _spawns.Advance(0.02f);
            Assert.That(members[0].gameObject.activeSelf, Is.False, "no player sees it, so it went");
        }

        [Test]
        public void Squad_FillsSlotsInOrder_FollowersKnowTheirLeader()
        {
            var squad = Track(ScriptableObject.CreateInstance<SpawnSquad>());
            squad.Members.Add(new SquadMember(_chest, 1));
            squad.Members.Add(new SquadMember(_coin, 2));
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.Of(squad, new RingArea(Vector3.zero, 0f, 5f)));
            _spawns.Advance(0.02f);
            var members = new List<Spawnable>();
            handle.GetAlive(members);
            Assert.That(members.Count, Is.EqualTo(3));
            Assert.That(members[0].Card, Is.SameAs(_chest));
            Assert.That(members[0].Info.Slot, Is.Zero);
            Assert.That(members[1].Card, Is.SameAs(_coin));
            Assert.That(members[2].Info.Leader, Is.SameAs(members[0]));
        }

        [Test]
        public void Table_PickingNothing_SpawnsNothing()
        {
            var table = Track(ScriptableObject.CreateInstance<SpawnTable>());
            table.NothingWeight = 1f;
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.From(table, SpawnConditions.Any, new RingArea(Vector3.zero, 0f, 5f), 3));
            Assert.That(handle.Card, Is.Null);
            Assert.That(handle.Count, Is.Zero);
            Assert.That(handle.IsDone, Is.True);
        }

        [Test]
        public void At_WhereTheKindCantStand_EndsCancelled()
        {
            _coin.Placement.Medium = SpawnMedium.Water;
            LogAssert.Expect(LogType.Warning, new Regex("can't stand"));
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, Vector3.zero, 2));
            Assert.That(handle.Cancelled, Is.True);
            Assert.That(handle.Alive, Is.Zero);
        }

        [Test]
        public void Prewarm_CreatesAFewAFrame_ByVariantWeight()
        {
            _spawns.Prewarm(_coin, 5);
            Spawnable prefab = _coin.PickVariant(0f);
            _pool.Tick();
            Assert.That(_pool.Made(prefab), Is.EqualTo(2));
            Assert.That(_pool.Warming, Is.True);
            _pool.Tick();
            _pool.Tick();
            Assert.That(_pool.Made(prefab), Is.EqualTo(5));
        }

        [Test]
        public void Prewarm_WithOwners_KeepsTheirReservationsAddedUp_UntilReleased()
        {
            Spawnable prefab = _coin.PickVariant(0f);
            var raid = new object();
            var camp = new object();
            _spawns.Prewarm(_coin, 6, raid);
            _spawns.Prewarm(_coin, 4, camp);
            Assert.That(_pool.Kept(prefab), Is.EqualTo(10));
            _spawns.Prewarm(_coin, 3, raid);
            Assert.That(_pool.Kept(prefab), Is.EqualTo(7), "an owner's new reservation replaces its old one");
            _spawns.Release(raid);
            Assert.That(_pool.Kept(prefab), Is.EqualTo(4));
            _spawns.Prewarm(_coin, 2);
            Assert.That(_pool.Kept(prefab), Is.EqualTo(4), "a prewarm without an owner reserves nothing");
        }

        [TestCase(10, 6, 4, 0f, 60f, false, 2, 0, TestName = "ToTrim_NothingWhileRecentlyUsed")]
        [TestCase(10, 6, 4, 61f, 60f, false, 2, 2, TestName = "ToTrim_IdleSurplus_ByTheFrameBudget")]
        [TestCase(10, 6, 4, 0f, 60f, true, 9, 6, TestName = "ToTrim_Released_AtOnce_OnlyFreeCopies")]
        [TestCase(10, 6, 8, 0f, 60f, true, 9, 2, TestName = "ToTrim_NeverBelowTheReservations")]
        [TestCase(10, 6, 0, 1000f, 0f, false, 9, 0, TestName = "ToTrim_NoDelay_NeverIdleTrims")]
        public void ToTrim(int made, int free, int kept, float idleFor, float delay, bool released, int budget, int expected) =>
            Assert.That(PrefabPool.ToTrim(made, free, kept, idleFor, delay, released, budget), Is.EqualTo(expected));

        [Test]
        public void MaxAlive_Refuse_DropsWhatDoesntFit()
        {
            _coin.MaxAlive = 3;
            _coin.WhenFull = SpawnCapRule.Refuse;
            SpawnHandle handle = _spawns.Spawn(SpawnRequest.At(_coin, Vector3.zero, 5));
            _spawns.Advance(0.02f);
            Assert.That(handle.Count, Is.EqualTo(3));
            Assert.That(handle.IsDone, Is.True);
            Assert.That(_spawns.CountAlive(_coin), Is.EqualTo(3));
            Assert.That(_spawns.Spawn(_coin, Vector3.zero, Quaternion.identity), Is.Null);
        }

        [Test]
        public void MaxAlive_RecycleOldest_TakesBackTheOldestToMakeRoom()
        {
            _coin.MaxAlive = 2;
            _coin.WhenFull = SpawnCapRule.RecycleOldest;
            Spawnable first = _spawns.Spawn(_coin, Vector3.zero, Quaternion.identity);
            Spawnable second = _spawns.Spawn(_coin, Vector3.right, Quaternion.identity);
            Spawnable third = _spawns.Spawn(_coin, Vector3.left, Quaternion.identity);
            Assert.That(third, Is.Not.Null);
            Assert.That(first.gameObject.activeSelf, Is.False, "the oldest went back");
            Assert.That(first.IsAlive, Is.False);
            Assert.That(second.IsAlive && third.IsAlive, Is.True);
            Assert.That(_spawns.CountAlive(_coin), Is.EqualTo(2));
        }

        SpawnCard Card(string name, SpawnCategory category)
        {
            var prefab = new GameObject(name, typeof(Spawnable));
            _created.Add(prefab);
            var card = Track(ScriptableObject.CreateInstance<SpawnCard>());
            card.name = name;
            card.SetVariant(prefab.GetComponent<Spawnable>(), 1f);
            card.Category = category;
            card.Placement.Navmesh = NavmeshRule.Ignore;
            return card;
        }

        T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
