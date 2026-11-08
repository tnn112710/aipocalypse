using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class MeleeTests
    {
        const float Frame = 1f / 60f;

        SwingSettings _swing;
        int _swings;
        int _hits;

        [SetUp]
        public void SetUp()
        {
            _swing = new SwingSettings { WindUp = 0.2f, Recovery = 0.3f };
            _swings = 0;
            _hits = 0;
        }

        MeleeAttack NewAttack()
        {
            var attack = new MeleeAttack(_swing);
            attack.SwingStarted += _ => _swings++;
            attack.Struck += () => _hits++;
            return attack;
        }

        static MeleeInput Attack(bool held, bool pressed = false) => new()
        {
            AttackHeld = held,
            AttackPressed = pressed,
            IsReady = true,
            CanAttack = true,
        };

        static void Run(MeleeAttack attack, MeleeInput input, float seconds, float deltaTime = Frame)
        {
            int frames = Mathf.RoundToInt(seconds / deltaTime);
            for (int i = 0; i < frames; i++)
                attack.Tick(input, deltaTime);
        }

        [Test]
        public void Click_HitsAfterTheWindUp_Once()
        {
            MeleeAttack attack = NewAttack();

            attack.Tick(Attack(true, true), Frame);
            Run(attack, Attack(false), 0.15f);
            Assert.AreEqual(0, _hits, "still winding up");

            Run(attack, Attack(false), 1f);
            Assert.AreEqual(1, _swings);
            Assert.AreEqual(1, _hits);
            Assert.AreEqual(SwingPhase.None, attack.Phase);
        }

        [Test]
        public void Holding_KeepsSwingingAtItsRate_AtAnyFrameRate()
        {
            Run(NewAttack(), Attack(true), 2f);
            Assert.AreEqual(4, _hits, 1);

            _hits = 0;
            Run(NewAttack(), Attack(true), 2f, 1f / 7f);
            Assert.AreEqual(4, _hits, 1);
        }

        [Test]
        public void ClickDuringASwing_QueuesOneMore()
        {
            MeleeAttack attack = NewAttack();

            attack.Tick(Attack(true, true), Frame);
            attack.Tick(Attack(true, true), Frame);
            attack.Tick(Attack(true, true), Frame);
            Run(attack, Attack(false), 2f);

            Assert.AreEqual(2, _swings);
            Assert.AreEqual(2, _hits);
        }

        [Test]
        public void SwingsInARow_AlternateSides_AndStartOverAfterAPause()
        {
            MeleeAttack attack = NewAttack();

            Run(attack, Attack(true), 1.2f);
            Assert.AreEqual(2, attack.SwingIndex);

            Run(attack, Attack(false), 1.5f);
            attack.Tick(Attack(true, true), Frame);
            Assert.AreEqual(0, attack.SwingIndex);
        }

        [Test]
        public void AfterSwitching_TheNextSwingStartsOnTheFirstSide()
        {
            MeleeAttack attack = NewAttack();
            Run(attack, Attack(true), 0.6f);
            Assert.AreEqual(1, attack.SwingIndex);

            attack.Tick(new MeleeInput { IsReady = false }, Frame);
            attack.Tick(Attack(true, true), Frame);

            Assert.AreEqual(0, attack.SwingIndex);
        }

        [Test]
        public void Switching_CancelsTheSwingBeforeItLands()
        {
            MeleeAttack attack = NewAttack();
            attack.Tick(Attack(true, true), Frame);

            attack.Tick(new MeleeInput { IsReady = false }, 1f);

            Assert.AreEqual(0, _hits);
            Assert.AreEqual(SwingPhase.None, attack.Phase);
        }

        [Test]
        public void Sprinting_StartsNoSwing_ButLetsTheCurrentOneFinish()
        {
            MeleeAttack attack = NewAttack();
            Run(attack, new MeleeInput { AttackHeld = true, AttackPressed = true, IsReady = true }, 0.5f);
            Assert.AreEqual(0, _swings);

            attack.Tick(Attack(true, true), Frame);
            Run(attack, new MeleeInput { AttackHeld = true, IsReady = true }, 1f);
            Assert.AreEqual(1, _swings);
            Assert.AreEqual(1, _hits);
        }
    }
}
