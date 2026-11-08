using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Tests
{
    public sealed class CombatTests
    {
        sealed class FakeTarget : IDamageable
        {
            public readonly Health Health = new(50f);
            public int Hits;
            public bool IsDead => Health.IsDead;

            public DamageResult TakeDamage(in DamageInfo info)
            {
                Hits++;
                float applied = Health.Damage(info.Amount);
                return new DamageResult(applied, applied > 0f && Health.IsDead);
            }
        }

        IObjectResolver _resolver;
        DamageService _damage;
        DamageDealt? _last;
        int _published;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            new CombatInstaller().Install(builder);
            _resolver = builder.Build();
            _damage = _resolver.Resolve<DamageService>();
            _published = 0;
            _last = null;
            _resolver.Resolve<ISubscriber<DamageDealt>>().Subscribe(e =>
            {
                _published++;
                _last = e;
            });
        }

        [TearDown]
        public void TearDown() => _resolver.Dispose();

        [Test]
        public void Deal_AppliesAndPublishesWhatLanded_WithTheSource()
        {
            var target = new FakeTarget();
            var shooter = new GameObject("Shooter");
            try
            {
                DamageResult result = _damage.Deal(target, new DamageInfo(20f, DamageType.Gunshot, Vector3.one, default, shooter));

                Assert.That(result.Applied, Is.EqualTo(20f));
                Assert.That(result.Killed, Is.False);
                Assert.That(_published, Is.EqualTo(1));
                Assert.That(_last.Value.Info.Source, Is.SameAs(shooter));
                Assert.That(_last.Value.Result.Applied, Is.EqualTo(20f));
            }
            finally { Object.DestroyImmediate(shooter); }
        }

        [Test]
        public void Deal_ReportsTheKillAndCapsDamageAtWhatWasLeft()
        {
            var target = new FakeTarget();
            DamageResult result = _damage.Deal(target, new DamageInfo(80f, DamageType.Melee, Vector3.zero));

            Assert.That(result.Applied, Is.EqualTo(50f));
            Assert.That(result.Killed, Is.True);
            Assert.That(_last.Value.Result.Killed, Is.True);
        }

        [Test]
        public void Deal_IgnoresDeadTargetsNullTargetsAndZeroDamage()
        {
            var target = new FakeTarget();
            _damage.Deal(target, new DamageInfo(0f, DamageType.Generic, Vector3.zero));
            _damage.Deal(null, new DamageInfo(10f, DamageType.Generic, Vector3.zero));
            _damage.Deal(target, new DamageInfo(50f, DamageType.Generic, Vector3.zero));
            _damage.Deal(target, new DamageInfo(10f, DamageType.Generic, Vector3.zero));

            Assert.That(target.Hits, Is.EqualTo(1));
            Assert.That(_published, Is.EqualTo(1));
        }
    }
}
