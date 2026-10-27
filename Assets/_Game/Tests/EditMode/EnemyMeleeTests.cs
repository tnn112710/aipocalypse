using System.Linq;
using Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public class EnemyMeleeTests
    {
        const string MeleeTree = "Assets/_Game/Content/Enemies/_Shared/Melee.asset";

        [Test]
        public void InReach_NoticesInsideDetectRadius_AndKeepsUntilPastLoseRadius()
        {
            var melee = ScriptableObject.CreateInstance<MeleeSettings>();
            melee.DetectRadius = 5f;
            melee.LoseRadius = 8f;
            try
            {
                Assert.That(melee.InReach(false, 4.9f * 4.9f), Is.True, "noticed inside the detect radius");
                Assert.That(melee.InReach(false, 6f * 6f), Is.False, "not noticed between the radii");
                Assert.That(melee.InReach(true, 6f * 6f), Is.True, "kept between the radii while engaged");
                Assert.That(melee.InReach(true, 8.1f * 8.1f), Is.False, "given up past the lose radius");
                melee.LoseRadius = 2f;
                Assert.That(melee.InReach(true, 4f * 4f), Is.True, "a lose radius below the detect radius never drops a noticed player");
            }
            finally
            {
                Object.DestroyImmediate(melee);
            }
        }

        [Test]
        public void MeleeDefinitions_ReachBeforeTheyLose()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EnemyDefinition), new[] { "Assets/_Game/Content/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MeleeSettings melee = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path).Melee;
                if (melee == null)
                    continue;
                Assert.That(melee.LoseRadius, Is.GreaterThan(melee.DetectRadius), $"{path}: lose radius must exceed the detect radius");
                Assert.That(melee.AttackRange, Is.LessThan(melee.DetectRadius), $"{path}: swings start farther than it notices");
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
