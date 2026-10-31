using System.Linq;
using Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class EnemyAnimationTests
    {
        static readonly string[] ContractParameters = { "Speed", "LookX", "LookY", "Lean", "Fidget", "FidgetIndex", "Hit", "HitIndex", "Dead", "DeathIndex", "Attack", "AttackIndex", "Combat", "Appear" };
        EnemyAnimationSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<EnemyAnimationSettings>();
            _settings.FidgetDelay = new Vector2(1f, 1f);
            _settings.FidgetChance = 1f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [Test]
        public void Walking_LooksAndLeansIntoTheTurn()
        {
            var motion = new LocomotionAnimation(_settings, 3, 1);
            motion.Advance(0.1f, 1.4f, 45f);
            Assert.That(motion.Moving, Is.True);
            Assert.That(motion.Look.x, Is.EqualTo(45f * _settings.LookIntoTurn).Within(1e-4f));
            Assert.That(motion.Look.y, Is.Zero);
            Assert.That(motion.Lean, Is.EqualTo(45f * _settings.LeanIntoTurn).Within(1e-4f));
        }

        [Test]
        public void Walking_ClampsLookAndLeanToTheFullPose()
        {
            var motion = new LocomotionAnimation(_settings, 0, 1);
            motion.Advance(0.1f, 3f, -1000f);
            Assert.That(motion.Look.x, Is.EqualTo(-1f));
            Assert.That(motion.Lean, Is.EqualTo(-1f));
        }

        [Test]
        public void Standing_GlancesWithinTheRangeAndStopsLeaning()
        {
            var motion = new LocomotionAnimation(_settings, 0, 7);
            motion.Advance(0.1f, 1.4f, 90f);
            for (int i = 0; i < 200; i++)
            {
                motion.Advance(0.1f, 0f, 0f);
                Assert.That(Mathf.Abs(motion.Look.x), Is.LessThanOrEqualTo(_settings.GlanceRange.x));
                Assert.That(Mathf.Abs(motion.Look.y), Is.LessThanOrEqualTo(_settings.GlanceRange.y));
                Assert.That(motion.Lean, Is.Zero);
            }
        }

        [Test]
        public void Standing_FidgetsOncePerStopAfterTheDelay()
        {
            var motion = new LocomotionAnimation(_settings, 3, 3);
            motion.Advance(0.5f, 1.4f, 0f);
            motion.Advance(0.5f, 0f, 0f);
            Assert.That(motion.TakeFidget(), Is.EqualTo(-1), "too early");
            motion.Advance(0.6f, 0f, 0f);
            int fidget = motion.TakeFidget();
            Assert.That(fidget, Is.InRange(0, 2));
            Assert.That(motion.TakeFidget(), Is.EqualTo(-1), "taken once");
            motion.Advance(5f, 0f, 0f);
            Assert.That(motion.TakeFidget(), Is.EqualTo(-1), "one roll per stop");

            motion.Advance(0.5f, 1.4f, 0f);
            motion.Advance(1.1f, 0f, 0f);
            motion.Advance(0.1f, 0f, 0f);
            Assert.That(motion.TakeFidget(), Is.InRange(0, 2), "next stop rolls again");
        }

        [Test]
        public void SettingOff_DropsADueFidget()
        {
            var motion = new LocomotionAnimation(_settings, 3, 3);
            motion.Advance(0.1f, 1.4f, 0f);
            motion.Advance(2f, 0f, 0f);
            motion.Advance(0.1f, 1.4f, 0f);
            Assert.That(motion.TakeFidget(), Is.EqualTo(-1));
        }

        [Test]
        public void NoFidgetStates_NeverFidgets()
        {
            var motion = new LocomotionAnimation(_settings, 0, 3);
            motion.Advance(0.1f, 1.4f, 0f);
            motion.Advance(10f, 0f, 0f);
            Assert.That(motion.TakeFidget(), Is.EqualTo(-1));
        }

        // Every enemy prefab with an EnemyAnimator: a humanoid Animator with a controller that has the parameter contract,
        // at least as many fidget-capable states as it asks for, no root motion, and definition animation settings.
        [Test]
        public void AnimatedEnemyPrefabs_AreWiredToTheParameterContract()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies" });
            int animated = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var enemyAnimator = prefab.GetComponent<EnemyAnimator>();
                if (enemyAnimator == null)
                    continue;
                animated++;
                Animator animator = enemyAnimator.Animator;
                Assert.That(animator, Is.Not.Null, $"{path}: no Animator assigned");
                Assert.That(animator.transform.IsChildOf(prefab.transform), Is.True, $"{path}: Animator outside the prefab");
                Assert.That(animator.avatar != null && animator.avatar.isHuman, Is.True, $"{path}: needs a humanoid avatar");
                Assert.That(animator.applyRootMotion, Is.False, $"{path}: movement belongs to the FollowerEntity");
                var controller = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(controller, Is.Not.Null, $"{path}: no AnimatorController");
                string[] parameters = controller.parameters.Select(p => p.name).ToArray();
                Assert.That(parameters, Is.SupersetOf(ContractParameters), $"{path}: controller misses contract parameters");
                var baseStates = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
                int deathStates = baseStates.Count(n => n.StartsWith("Death_"));
                int attackStates = baseStates.Count(n => n.StartsWith("Attack_"));
                int fidgetStates = baseStates.Count(n => n != "Locomotion" && n != "Combat") - deathStates - attackStates;
                var serialized = new SerializedObject(enemyAnimator);
                Assert.That(fidgetStates, Is.GreaterThanOrEqualTo(serialized.FindProperty("_fidgets").intValue), $"{path}: more fidgets than states");
                Assert.That(deathStates, Is.GreaterThanOrEqualTo(serialized.FindProperty("_deaths").intValue), $"{path}: more deaths than states");
                Assert.That(attackStates, Is.GreaterThanOrEqualTo(serialized.FindProperty("_attacks").intValue), $"{path}: more attacks than states");
                var definition = prefab.GetComponent<EnemyBrain>().Definition;
                Assert.That(definition != null && definition.Animation != null, Is.True, $"{path}: definition has no Animation settings");
            }
            Assert.That(animated, Is.GreaterThan(0), "no animated enemy prefab found");
        }
    }
}
