using Ami.BroAudio;
using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEditor;

namespace MB13.Aipocalypse.Tests
{
    // Dog assets and wiring: settings, prefab, runtime bridge.
    public sealed class DogTests
    {
        const string DogsData = "Assets/_Game/Data/Dogs";

        [Test]
        public void Prefab_LokiHasNoMalbersOrRootMotion()
        {
            var loki = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Game/Content/Dogs/Loki/Loki.prefab");
            Assert.That(loki, Is.Not.Null, "Loki prefab");
            foreach (var component in loki.GetComponentsInChildren<UnityEngine.Component>(true))
                Assert.That(component.GetType().Namespace ?? "", Does.Not.StartWith("MalbersAnimations"), component.GetType().Name);
            Assert.That(loki.GetComponent<Dog>(), Is.Not.Null, "Dog component");
            var animator = loki.GetComponentInChildren<UnityEngine.Animator>(true);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.runtimeAnimatorController.name, Is.EqualTo("BorderCollie"));
            var agent = new SerializedObject(loki.GetComponent<Pathfinding.FollowerEntity>());
            Assert.That(agent.FindProperty("shape.radius").floatValue, Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void TakeDamage_FromPlayerYelpsAndAppliesNothing()
        {
            var dog = new UnityEngine.GameObject("Dog").AddComponent<Dog>();
            try
            {
                var info = new MB13.Aipocalypse.Core.Combat.DamageInfo(25f, MB13.Aipocalypse.Core.Combat.DamageType.Gunshot,
                    dog.transform.position + UnityEngine.Vector3.forward, source: new UnityEngine.GameObject("Player"));
                MB13.Aipocalypse.Core.Combat.DamageResult result = dog.TakeDamage(info);
                Assert.That(result.Applied, Is.EqualTo(0f));
                Assert.That(result.Killed, Is.False);
                Assert.That(dog.IsDead, Is.False);
                UnityEngine.Object.DestroyImmediate(info.Source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dog.gameObject);
            }
        }

        [Test]
        public void Dog_IsNeverDeadAndTargetsAsDog()
        {
            var dog = new UnityEngine.GameObject("Dog").AddComponent<Dog>();
            try
            {
                Assert.That(dog.IsDead, Is.False);
                Assert.That(dog.Kind, Is.EqualTo(MB13.Aipocalypse.Core.Entities.TargetKind.Dog));
                Assert.That(dog.IsTargetable, Is.False, "not active: nobody may target it");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dog.gameObject);
            }
        }

        [Test]
        public void JumpArc_PicksByRiseAndObstacle()
        {
            Assert.That(DogJumpArc.Pick(0f, 2f), Is.EqualTo(JumpKind.Scramble), "a 2 m fence, both sides level");
            Assert.That(DogJumpArc.Pick(1f, 0f), Is.EqualTo(JumpKind.ClimbUp));
            Assert.That(DogJumpArc.Pick(-1.2f, 0f), Is.EqualTo(JumpKind.DropDown));
            Assert.That(DogJumpArc.Pick(0.3f, 0f), Is.EqualTo(JumpKind.Hop));
            Assert.That(DogJumpArc.Pick(0.3f, 0.5f), Is.EqualTo(JumpKind.Hop), "a low log is hopped");
        }

        [Test]
        public void JumpArc_ScrambleClearsTheObstacle()
        {
            var start = new UnityEngine.Vector3(0f, 1f, 0f);
            var end = new UnityEngine.Vector3(0f, 1f, 2f);
            const float fence = 2f;
            float peak = DogJumpArc.Peak(JumpKind.Scramble, start, end, fence);
            Assert.That(peak, Is.GreaterThanOrEqualTo(start.y + fence + 0.1f - 1e-4f));
            Assert.That(DogJumpArc.Position(JumpKind.Scramble, start, end, peak, 0f), Is.EqualTo(start));
            Assert.That(DogJumpArc.Position(JumpKind.Scramble, start, end, peak, 1f), Is.EqualTo(end));
            float highest = float.MinValue;
            float overFence = float.MinValue;
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                UnityEngine.Vector3 p = DogJumpArc.Position(JumpKind.Scramble, start, end, peak, t);
                highest = UnityEngine.Mathf.Max(highest, p.y);
                if (UnityEngine.Mathf.Abs(p.z - 1f) < 0.05f)
                    overFence = UnityEngine.Mathf.Max(overFence, p.y);
            }
            Assert.That(highest, Is.EqualTo(peak).Within(0.02f));
            Assert.That(overFence, Is.GreaterThanOrEqualTo(start.y + fence), "over the fence line it is above the top");
            Assert.That(DogJumpArc.Duration(JumpKind.Scramble, 2f), Is.GreaterThan(DogJumpArc.Duration(JumpKind.Hop, 2f)));
        }

        [Test]
        public void Settings_EveryDogSoundHasAnId()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DogSettings>(DogsData + "/DogSettings.asset");
            Assert.That(settings, Is.Not.Null, "DogSettings asset");
            foreach (DogSound sound in System.Enum.GetValues(typeof(DogSound)))
            {
                Assert.That(settings.Sounds.TryGetValue(sound, out var id), Is.True, $"{sound} has an entry");
                Assert.That(id.IsValid(), Is.True, $"{sound} id is valid");
            }
        }
    }
}
