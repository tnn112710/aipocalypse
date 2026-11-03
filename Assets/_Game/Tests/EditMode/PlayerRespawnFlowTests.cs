using System;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Input;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Tests
{
    public class PlayerRespawnFlowTests
    {
        sealed class FakePlayer : IPlayer
        {
            public int MovedToSpawn;
            public bool IsDead { get; set; } = true;
            public Transform Transform => null;
            public Health Health => null;
            public Stamina Stamina => null;
            public Equipment Equipment => null;
            public AmmoReserve Ammo => null;
            public HeldItem HeldItem => null;
            public PlayerInteractor Interactor => null;
            public PlayerRepair Repair => null;
            public bool IsSprinting => false;
            public Vector2 CrosshairPosition => default;
            public float CrosshairSpread => 0f;
            public Vector3 SpawnPosition => default;
            public Transform View => null;
            public bool Invulnerable { get; set; }
            public bool IgnoredByEnemies { get; set; }
            public IPlayerIntentSource IntentSource { get; set; }
            public TargetKind Kind => TargetKind.Player;
            public bool IsTargetable => !IsDead;
            public DamageResult TakeDamage(in DamageInfo info) => default;
            public void MoveToSpawn() => MovedToSpawn++;
            public void Revive() => IsDead = false;
        }

        IObjectResolver _resolver;
        PlayerRespawnFlow _flow;
        InputService _input;
        FakePlayer _player;
        UniTaskCompletionSource _fade;
        IDisposable _fadeHandler;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(new ControlSettings());
            builder.Register<InputService>(Lifetime.Singleton);
            builder.Register<PlayerRegistry>(Lifetime.Singleton);
            builder.Register<PlayerRespawnFlow>(Lifetime.Singleton);
            _resolver = builder.Build();
            _input = _resolver.Resolve<InputService>();
            _flow = _resolver.Resolve<PlayerRespawnFlow>();
            _flow.Start();

            _player = new FakePlayer();
            _resolver.Resolve<PlayerRegistry>().Register(_player);
            // A death screen fade that ignores the cancellation token.
            _fade = new UniTaskCompletionSource();
            _fadeHandler = _resolver.Resolve<IAsyncSubscriber<PlayerRespawnFading>>().Subscribe((_, _) => _fade.Task);
        }

        [TearDown]
        public void TearDown()
        {
            _fadeHandler.Dispose();
            _resolver.Dispose();
        }

        void DeathSequenceEnds() => _resolver.Resolve<IPublisher<PlayerDeathSequenceEnded>>().Publish(new PlayerDeathSequenceEnded(Vector3.zero));

        [Test]
        public void AfterTheFade_TakesTheUiAndMovesTheBodyToSpawn()
        {
            DeathSequenceEnds();
            Assert.AreEqual(RespawnStage.Fading, _flow.Stage);

            _fade.TrySetResult();

            Assert.AreEqual(RespawnStage.Ready, _flow.Stage);
            Assert.AreEqual(1, _player.MovedToSpawn);
            Assert.IsTrue(_input.IsUIRequested);
        }

        [Test]
        public void ReviveDuringTheFade_EndsTheFlow_EvenIfTheFadeFinishesLater()
        {
            DeathSequenceEnds();

            _resolver.Resolve<IPublisher<PlayerRevived>>().Publish(new PlayerRevived(Vector3.zero));
            _fade.TrySetResult();

            Assert.AreEqual(RespawnStage.None, _flow.Stage);
            Assert.AreEqual(0, _player.MovedToSpawn, "a living player isn't teleported");
            Assert.IsFalse(_input.IsUIRequested, "gameplay input isn't left locked");
        }
    }
}
