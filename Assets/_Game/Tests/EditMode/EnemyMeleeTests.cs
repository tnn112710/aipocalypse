using System.Linq;
using MB13.Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class EnemyMeleeTests
    {
        const string MeleeTree = "Assets/_Game/Content/Enemies/_Shared/Melee.asset";

        [Test]
        public void Lands_InsideReachAndArc_Only()
        {
            Vector3 forward = Vector3.forward;
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(0f, 0f, 1.9f), 2f, 90f), Is.True, "straight ahead in reach");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(0f, 0f, 2.1f), 2f, 90f), Is.False, "past the reach");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(1f, 0f, 1.1f), 2f, 90f), Is.True, "inside the 45 degree half-arc");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(1.2f, 0f, 0.8f), 2f, 90f), Is.False, "outside the half-arc");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(0f, 0f, -1f), 2f, 90f), Is.False, "behind");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(0f, 0f, -1f), 2f, 360f), Is.True, "a full circle hits behind");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(0f, 1.5f, 1f), 2f, 90f), Is.True, "a small step up still reaches");
            Assert.That(MeleeHit.Lands(Vector3.zero, forward, new Vector3(0f, 5f, 1f), 2f, 90f), Is.False, "a target on a roof doesn't");
        }

        [Test]
        public void MeleeDefinitions_HoldOutsideTheSwing_AndHitWithinIt()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EnemyDefinition), new[] { "Assets/_Game/Content/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MeleeSettings melee = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path).Melee;
                if (melee == null)
                    continue;
                Assert.That(melee.AttackRange, Is.LessThan(melee.HoldDistance), $"{path}: enemies waiting for a slot would stand in swing range");
                Assert.That(melee.HoldDistance, Is.LessThanOrEqualTo(melee.EngageRadius), $"{path}: they'd wait before engaging");
                Assert.That(melee.HitTime, Is.LessThanOrEqualTo(melee.AttackDuration), $"{path}: the blow lands after the swing ended");
                Assert.That(melee.HitReach, Is.GreaterThanOrEqualTo(melee.AttackRange), $"{path}: a swing started in range can't reach");
            }
        }

        // Prefabs on the Melee subtree need Melee settings; armed ones hold their sword in the right hand without a collider.
        [Test]
        public void MeleeEnemyPrefabs_HaveSettings_AndAColliderlessSwordInHand()
        {
            var tree = AssetDatabase.LoadAssetAtPath<ScriptableObject>(MeleeTree);
            Assert.That(tree, Is.Not.Null, "no Melee subtree");
            int fighters = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var brain = prefab.GetComponent<EnemyBrain>();
                // The test assembly doesn't reference Opsive: read the BehaviorTree's subtree by its serialized name.
                Component behavior = brain != null ? prefab.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "BehaviorTree") : null;
                if (behavior == null || new SerializedObject(behavior).FindProperty("m_Subtree")?.objectReferenceValue != tree)
                    continue;
                fighters++;
                Assert.That(brain.Definition.Melee, Is.Not.Null, $"{path}: Melee subtree without Melee settings");
                var enemyAnimator = prefab.GetComponent<EnemyAnimator>();
                if (enemyAnimator == null)
                    continue;
                Transform hand = enemyAnimator.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform sword = hand.Find("Sword");
                Assert.That(sword, Is.Not.Null, $"{path}: no Sword in the right hand");
                Assert.That(sword.GetComponentsInChildren<Collider>(true), Is.Empty, $"{path}: the sword would catch the hits meant for the body");
            }
            Assert.That(fighters, Is.GreaterThan(0), "no enemy prefab uses the Melee subtree");
        }
    }
}
