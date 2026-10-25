using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Input;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Tests
{
    public class QuickTimeServiceTests
    {
        IObjectResolver _resolver;
        QuickTimeService _qte;
        TimeService _time;
        TimingQteChallenge _challenge;
        readonly object _owner = new();
        QteResult? _result;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(new ControlSettings());
            builder.Register<InputService>(Lifetime.Singleton);
            builder.Register<TimeService>(Lifetime.Singleton);
            builder.Register<QuickTimeService>(Lifetime.Singleton);
            _resolver = builder.Build();
            _qte = _resolver.Resolve<QuickTimeService>();
            _time = _resolver.Resolve<TimeService>();

            // Zone 0.45–0.55 of a one-second fill.
            _challenge = ScriptableObject.CreateInstance<TimingQteChallenge>();
            _challenge.CycleDuration = 1f;
            _challenge.GreenZoneCenter = 0.5f;
            _challenge.GreenZoneWidth = 0.1f;
            _challenge.TotalDuration = 10f;
            _result = null;
        }

        [TearDown]
        public void TearDown()
        {
            _resolver.Dispose();
            Object.DestroyImmediate(_challenge);
        }

        void Start(bool ownerControlled = false) =>
            Assert.IsTrue(_qte.TryStart(_owner, _challenge, result => _result = result, ownerControlled));

        [Test]
        public void Press_IsJudgedWhereThePointerWasDrawn_NotAfterALongFrame()
        {
            Start();
            for (int i = 0; i < 10; i++)
                _qte.Advance(0.05f, false);

            // A hitch frame: moving first would carry the pointer past the zone before the press is judged.
            _qte.Advance(1f, true);

            Assert.AreEqual(QteResult.Success, _result);
            Assert.IsFalse(_qte.IsActive);
        }

        [Test]
        public void OneHitch_CannotTimeOutAStandaloneQte()
        {
            _challenge.TotalDuration = Time.maximumDeltaTime * 2f;
            Start();

            _qte.Advance(10f, false);

            Assert.IsTrue(_qte.IsActive);
            Assert.IsNull(_result);
        }

        [Test]
        public void StandaloneSlowMotion_IsReleasedWhenTheQteEnds()
        {
            _challenge.TimeScale = 0.5f;
            Start();
            Assert.AreEqual(0.5f, _time.Scale);

            _qte.Cancel(_owner);

            Assert.AreEqual(QteResult.Cancelled, _result);
            Assert.AreEqual(1f, _time.Scale);
        }

        [Test]
        public void OwnerControlledQte_KeepsTheGameAtFullSpeed()
        {
            _challenge.TimeScale = 0.5f;
            Start(ownerControlled: true);

            Assert.AreEqual(1f, _time.Scale);
        }

        [Test]
        public void PlayerDeath_CancelsTheRunningQte()
        {
            Start(ownerControlled: true);

            _resolver.Resolve<IPublisher<PlayerDied>>().Publish(new PlayerDied(Vector3.zero, DamageType.Fall));

            Assert.AreEqual(QteResult.Cancelled, _result);
            Assert.IsFalse(_qte.IsActive);
        }

        [Test]
        public void Cancel_FromAnotherOwner_IsIgnored()
        {
            Start();

            _qte.Cancel(new object());

            Assert.IsTrue(_qte.IsActiveFor(_owner));
        }
    }
}
