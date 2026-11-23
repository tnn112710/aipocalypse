using System.Collections.Generic;
using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // The dog's fighting rules: the upgradable stat sheet, toughness and knock-down, takedown rolls, target choice.
    public sealed class DogCombatTests
    {
        sealed class Upgrade : IDogUpgradeSource
        {
            readonly Dictionary<DogStat, float> _modifiers = new();

            public Upgrade Add(DogStat stat, float amount)
            {
                _modifiers[stat] = amount;
                return this;
            }

            public float Modifier(DogStat stat) => _modifiers.TryGetValue(stat, out float amount) ? amount : 0f;
        }

        DogStatBlock _block;
        DogStatSheet _stats;

        [SetUp]
        public void SetUp()
        {
            _block = ScriptableObject.CreateInstance<DogStatBlock>();
            _block.SetBase(DogStat.BiteDamage, 12f);
            _block.SetBase(DogStat.MaxBiteClass, 0f);
            _block.SetBase(DogStat.Toughness, 100f);
            _block.SetBase(DogStat.RegenPerSecond, 2f);
            _block.SetBase(DogStat.HelpUpToughness, 0.3f);
            _block.SetBase(DogStat.LimpTime, 30f);
            _block.SetBase(DogStat.RetreatAt, 0.25f);
            _block.SetBase(DogStat.TakedownChanceBase, 0f);
            _block.SetBase(DogStat.TakedownChanceBonus, 0f);
            _block.SetBase(DogStat.MaxTakedownClass, 0f);
            _block.SetBase(DogStat.TakedownCooldown, 20f);
            _stats = new DogStatSheet(_block, 4);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_block);

        DogState NewState()
        {
            var settings = ScriptableObject.CreateInstance<DogSettings>();
            settings.RegenDelay = 10f;
            settings.SelfRiseTime = 90f;
            var state = new DogState(settings);
            state.ResetToughness(_stats);
            return state;
        }

        [Test]
        public void Toughness_HitsDownAtZero()
        {
            DogState state = NewState();
            state.Hurt(60f, 1f, _stats);
            Assert.That(state.Down, Is.False);
            Assert.That(state.Toughness, Is.EqualTo(40f));
            state.Hurt(60f, 2f, _stats);
            Assert.That(state.Down, Is.True);
            Assert.That(state.Toughness, Is.EqualTo(0f));
        }

        [Test]
        public void Toughness_RegensOnlyAfterTenSecondsUnhurt()
        {
            DogState state = NewState();
            state.Hurt(50f, 0f, _stats);
            state.Tick(5f, 5f, _stats);
            Assert.That(state.Toughness, Is.EqualTo(50f), "not yet");
            state.Tick(5f, 15f, _stats);
            Assert.That(state.Toughness, Is.EqualTo(60f).Within(0.01f), "2 per second for the 5 s past the delay");
            state.Tick(100f, 115f, _stats);
            Assert.That(state.Toughness, Is.EqualTo(100f), "capped");
        }

        [Test]
        public void Toughness_SelfRisesAfter90Seconds()
        {
            DogState state = NewState();
            state.Hurt(200f, 0f, _stats);
            state.Tick(1f, 89f, _stats);
            Assert.That(state.Down, Is.True);
            state.Tick(1f, 90.5f, _stats);
            Assert.That(state.Down, Is.False);
            Assert.That(state.Limping(91f), Is.True);
        }

        [Test]
        public void HelpUp_SetsThirtyPercentAndLimps()
        {
            DogState state = NewState();
            state.Hurt(200f, 0f, _stats);
            state.HelpUp(5f, _stats);
            Assert.That(state.Down, Is.False);
            Assert.That(state.Toughness, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(state.Limping(34.9f), Is.True);
            Assert.That(state.Limping(35.1f), Is.False);
        }

        [Test]
        public void Retreat_AtQuarterToughness()
        {
            DogState state = NewState();
            state.Hurt(74f, 0f, _stats);
            Assert.That(state.Retreating(_stats), Is.False);
            state.Hurt(2f, 0f, _stats);
            Assert.That(state.Retreating(_stats), Is.True);
        }

        [Test]
        public void Takedown_OffAtStartStats()
        {
            DogState state = NewState();
            Assert.That(state.TryTakedown(0.01f, 0, 0f, 0f, _stats), Is.False, "chance 0 even on a dying light enemy");
        }

        [Test]
        public void Takedown_ChanceFollowsMissingHealth()
        {
            _block.SetBase(DogStat.TakedownChanceBase, 0.05f);
            _block.SetBase(DogStat.TakedownChanceBonus, 0.25f);
            Assert.That(DogState.TakedownChance(0.2f, _stats), Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(NewState().TryTakedown(0.2f, 0, 0f, 0.24f, _stats), Is.True);
            Assert.That(NewState().TryTakedown(0.2f, 0, 0f, 0.26f, _stats), Is.False);
        }

        [Test]
        public void Takedown_ClassAboveMaxNever()
        {
            _block.SetBase(DogStat.TakedownChanceBase, 1f);
            Assert.That(NewState().TryTakedown(0.5f, 1, 0f, 0f, _stats), Is.False, "Medium with max class Light");
        }

        [Test]
        public void Takedown_CooldownBlocks()
        {
            _block.SetBase(DogStat.TakedownChanceBase, 1f);
            DogState state = NewState();
            Assert.That(state.TryTakedown(0.5f, 0, 0f, 0f, _stats), Is.True);
            Assert.That(state.TryTakedown(0.5f, 0, 10f, 0f, _stats), Is.False, "20 s cooldown");
            Assert.That(state.TryTakedown(0.5f, 0, 20.5f, 0f, _stats), Is.True);
        }

        static FoeView Foe(int id, Vector3 at, bool aware = true, bool ranged = false, bool attackingPlayer = false, int cls = 0,
            Vector3 forward = default, bool swinging = false) =>
            new(id, at, forward == default ? Vector3.forward : forward, ranged, aware, attackingPlayer, swinging, 1f, cls, true);

        DogStatSheet CombatStats()
        {
            _block.SetBase(DogStat.EngageRange, 15f);
            _block.SetBase(DogStat.SentChaseRange, 30f);
            return _stats;
        }

        [Test]
        public void Combat_ActiveIgnoresUnawareEnemies()
        {
            var foes = new[] { Foe(1, new Vector3(5f, 0f, 0f), aware: false) };
            Assert.That(DogCombat.PickTarget(foes, Vector3.zero, DogStance.Active, -1, false, CombatStats()), Is.EqualTo(-1));
        }

        [Test]
        public void Combat_PrefersRangedThenAttackingPlayer()
        {
            var foes = new[]
            {
                Foe(1, new Vector3(3f, 0f, 0f)),
                Foe(2, new Vector3(9f, 0f, 0f), attackingPlayer: true),
                Foe(3, new Vector3(12f, 0f, 0f), ranged: true),
            };
            Assert.That(DogCombat.PickTarget(foes, Vector3.zero, DogStance.Active, -1, false, CombatStats()), Is.EqualTo(3), "ranged first");
            var noRanged = new[] { foes[0], foes[1] };
            Assert.That(DogCombat.PickTarget(noRanged, Vector3.zero, DogStance.Active, -1, false, CombatStats()), Is.EqualTo(2), "then attacking the player");
            var plain = new[] { Foe(1, new Vector3(6f, 0f, 0f)), Foe(4, new Vector3(3f, 0f, 0f)) };
            Assert.That(DogCombat.PickTarget(plain, Vector3.zero, DogStance.Active, -1, false, CombatStats()), Is.EqualTo(4), "then the nearest to the player");
        }

        [Test]
        public void Combat_PassiveNeverPicks()
        {
            var foes = new[] { Foe(1, new Vector3(3f, 0f, 0f), attackingPlayer: true) };
            Assert.That(DogCombat.PickTarget(foes, Vector3.zero, DogStance.Passive, -1, false, CombatStats()), Is.EqualTo(-1));
        }

        [Test]
        public void Combat_SentOverridesAndRespectsChaseRange()
        {
            var foes = new[] { Foe(1, new Vector3(3f, 0f, 0f), ranged: true), Foe(7, new Vector3(25f, 0f, 0f), aware: false) };
            Assert.That(DogCombat.PickTarget(foes, Vector3.zero, DogStance.Passive, 7, false, CombatStats()), Is.EqualTo(7), "sent, even unaware and passive");
            var far = new[] { Foe(7, new Vector3(35f, 0f, 0f)) };
            Assert.That(DogCombat.PickTarget(far, Vector3.zero, DogStance.Active, 7, false, CombatStats()), Is.EqualTo(-1), "past the chase range");
        }

        [Test]
        public void Combat_AboveClassCannotBite()
        {
            Assert.That(DogCombat.CanBite(Foe(1, Vector3.zero, cls: 0), CombatStats()), Is.True);
            Assert.That(DogCombat.CanBite(Foe(1, Vector3.zero, cls: 1), CombatStats()), Is.False);
        }

        [Test]
        public void Combat_OpeningWhenFacingAwayOrWaitedLong()
        {
            Vector3 dog = new(0f, 0f, -3f);
            FoeView facingDog = Foe(1, Vector3.zero, forward: Vector3.back);
            FoeView facingAway = Foe(1, Vector3.zero, forward: Vector3.forward);
            FoeView swingingAtPlayer = Foe(1, Vector3.zero, forward: Vector3.back, swinging: true);
            Assert.That(DogCombat.IsOpening(facingDog, dog, 1f, 8f, 60f, 1.5f), Is.False);
            Assert.That(DogCombat.IsOpening(facingAway, dog, 1f, 8f, 60f, 1.5f), Is.True);
            Assert.That(DogCombat.IsOpening(swingingAtPlayer, dog, 1f, 8f, 60f, 1.5f), Is.True, "busy swinging at someone else");
            Assert.That(DogCombat.IsOpening(facingDog, dog, 12.1f, 8f, 60f, 1.5f), Is.True, "circled 1.5 × the cooldown");
        }

        [Test]
        public void Combat_HomeDefenceEngagesRaidersInTheYardOnlyWhenActive()
        {
            var raider = new FoeView(5, new Vector3(30f, 0f, 0f), Vector3.forward, false, false, false, false, 1f, 0, true, inYard: true);
            var outside = new FoeView(6, new Vector3(31f, 0f, 0f), Vector3.forward, false, false, false, false, 1f, 0, true, inYard: false);
            var foes = new[] { outside, raider };
            Assert.That(DogCombat.PickTarget(foes, Vector3.zero, DogStance.Active, -1, true, CombatStats()), Is.EqualTo(5), "in the yard, unaware, far from the player");
            Assert.That(DogCombat.PickTarget(foes, Vector3.zero, DogStance.Passive, -1, true, CombatStats()), Is.EqualTo(-1));
            Assert.That(DogCombat.PickTarget(new[] { outside }, Vector3.zero, DogStance.Active, -1, true, CombatStats()), Is.EqualTo(-1), "outside the fence: only barked at");
        }

        [Test]
        public void Hit_FromEnemyReducesToughness()
        {
            DogState state = NewState();
            Assert.That(state.Hit(30f, fromPlayer: false, escaping: false, roll: 0.9f, now: 1f, _stats), Is.EqualTo(DogHit.Flinched));
            Assert.That(state.Toughness, Is.EqualTo(70f));
            Assert.That(state.Hit(80f, false, false, 0.9f, 2f, _stats), Is.EqualTo(DogHit.KnockedDown));
        }

        [Test]
        public void Hit_WhileDownIgnored()
        {
            DogState state = NewState();
            state.Hurt(500f, 0f, _stats);
            Assert.That(state.Hit(30f, false, false, 0.9f, 1f, _stats), Is.EqualTo(DogHit.Ignored));
        }

        [Test]
        public void Hit_FromPlayerOnlyYelps()
        {
            DogState state = NewState();
            Assert.That(state.Hit(30f, fromPlayer: true, false, 0.9f, 1f, _stats), Is.EqualTo(DogHit.Yelped));
            Assert.That(state.Toughness, Is.EqualTo(100f));
        }

        [Test]
        public void Hit_EscapingMayDodge()
        {
            _block.SetBase(DogStat.DodgeChance, 0.35f);
            DogState state = NewState();
            Assert.That(state.Hit(30f, false, escaping: true, roll: 0.3f, 1f, _stats), Is.EqualTo(DogHit.Dodged));
            Assert.That(state.Toughness, Is.EqualTo(100f));
            Assert.That(state.Hit(30f, false, escaping: true, roll: 0.4f, 1f, _stats), Is.EqualTo(DogHit.Flinched));
            Assert.That(state.Hit(30f, false, escaping: false, roll: 0.1f, 2f, _stats), Is.EqualTo(DogHit.Flinched), "only while escaping");
        }

        [Test]
        public void Stats_EveryStatHasABase()
        {
            var block = AssetDatabase.LoadAssetAtPath<DogStatBlock>("Assets/_Game/Data/Dogs/DogStatBlock.asset");
            Assert.That(block, Is.Not.Null, "DogStatBlock asset");
            foreach (DogStat stat in System.Enum.GetValues(typeof(DogStat)))
                Assert.That(block.Has(stat), Is.True, stat + " has a base value");
            Assert.That(block.Base(DogStat.BiteCooldown), Is.EqualTo(8f));
            Assert.That(block.Base(DogStat.TakedownChanceBase), Is.EqualTo(0f), "takedowns are off until upgraded");
        }

        [Test]
        public void Stats_UpgradeSourcesAdd()
        {
            Assert.That(_stats.Get(DogStat.BiteDamage), Is.EqualTo(12f));
            var upgrade = new Upgrade().Add(DogStat.BiteDamage, 6f);
            _stats.Add(upgrade);
            _stats.Add(new Upgrade().Add(DogStat.BiteDamage, 2f));
            Assert.That(_stats.Get(DogStat.BiteDamage), Is.EqualTo(20f));
            _stats.Remove(upgrade);
            Assert.That(_stats.Get(DogStat.BiteDamage), Is.EqualTo(14f));
        }

        [Test]
        public void Stats_ClassIsClampedToTheList()
        {
            _stats.Add(new Upgrade().Add(DogStat.MaxBiteClass, 1.7f));
            Assert.That(_stats.GetClass(DogStat.MaxBiteClass), Is.EqualTo(1), "rounded down");
            _stats.Add(new Upgrade().Add(DogStat.MaxBiteClass, 9f));
            Assert.That(_stats.GetClass(DogStat.MaxBiteClass), Is.EqualTo(3), "four classes: 0..3");
        }
    }
}
