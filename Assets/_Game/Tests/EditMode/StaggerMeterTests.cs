using Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class StaggerMeterTests
    {
        StaggerProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<StaggerProfile>();
            _profile.Recovery = 4f;
            _profile.CriticalMultiplier = 2f;
            _profile.FlinchAt = 1f;
            _profile.StaggerAt = 6f;
            _profile.StaggerTime = 0.8f;
            _profile.StaggerCooldown = 2f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_profile);

        [Test]
        public void Add_LightHitTwitches_StrongerFlinches()
        {
            var meter = new StaggerMeter(_profile);
            Assert.That(meter.Add(0.5f, false, 0f), Is.EqualTo(HitReaction.Twitch));
            Assert.That(meter.Add(0.6f, false, 0f), Is.EqualTo(HitReaction.Flinch), "0.5 + 0.6 passes FlinchAt");
        }

        [Test]
        public void Add_SustainedFireBuildsUpToAStagger_ThenTheMeterEmpties()
        {
            var meter = new StaggerMeter(_profile);
            HitReaction reaction = HitReaction.Twitch;
            int shots = 0;
            // An AK: 1.5 per bullet, 10 a second.
            for (; shots < 20 && reaction != HitReaction.Stagger; shots++)
                reaction = meter.Add(1.5f, false, shots * 0.1f);
            Assert.That(reaction, Is.EqualTo(HitReaction.Stagger));
            Assert.That(shots, Is.InRange(5, 6));
            Assert.That(meter.Value(shots * 0.1f), Is.Zero);
        }

        [Test]
        public void Value_DrainsAtTheRecoveryRate()
        {
            var meter = new StaggerMeter(_profile);
            meter.Add(5f, false, 1f);
            Assert.That(meter.Value(1.5f), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(meter.Value(10f), Is.Zero);
            Assert.That(meter.Add(0.9f, false, 10f), Is.EqualTo(HitReaction.Twitch), "single taps fade away");
        }

        [Test]
        public void Add_CantStaggerAgainUntilTheCooldownIsOver_ButStillFlinches()
        {
            var meter = new StaggerMeter(_profile);
            Assert.That(meter.Add(10f, false, 0f), Is.EqualTo(HitReaction.Stagger));
            Assert.That(meter.Add(10f, false, 1f), Is.EqualTo(HitReaction.Flinch), "inside StaggerTime + StaggerCooldown");
            Assert.That(meter.Add(10f, false, 2.9f), Is.EqualTo(HitReaction.Stagger), "after 0.8 + 2 s");
        }

        [Test]
        public void Add_CriticalHitsCountByTheMultiplier()
        {
            var meter = new StaggerMeter(_profile);
            Assert.That(meter.Add(3f, true, 0f), Is.EqualTo(HitReaction.Stagger), "3 × 2 reaches StaggerAt");
        }

        [Test]
        public void Add_ImmuneTiersNeverTrigger()
        {
            _profile.Flinches = false;
            _profile.Staggers = false;
            var meter = new StaggerMeter(_profile);
            Assert.That(meter.Add(1000f, true, 0f), Is.EqualTo(HitReaction.Twitch));
        }

        [Test]
        public void WeightClasses_StaggerNoEarlierThanTheyFlinch()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:StaggerProfile", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<StaggerProfile>(path);
                Assert.That(profile.StaggerAt, Is.GreaterThanOrEqualTo(profile.FlinchAt), path);
                Assert.That(profile.FlinchSlow, Is.InRange(0f, 1f), path);
            }
        }
    }
}
