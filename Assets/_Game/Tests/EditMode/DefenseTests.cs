using Aipocalypse.Core.DefenseSystem;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.PlayerSystem;
using Ami.BroAudio;
using NUnit.Framework;
using Pathfinding;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class DefenseTests
    {
        const string FencePath = "Assets/_Game/Content/Structures/WoodFence/WoodFence.prefab";
        const string GatePath = "Assets/_Game/Content/Structures/WoodGate/WoodGate.prefab";
        const string DoubleGatePath = "Assets/_Game/Content/Structures/WoodGate/WoodGate_Double.prefab";
        const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";

        [Test]
        public void Health_BreaksAtZero_AndTakesNoMoreDamage()
        {
            var health = new BarrierHealth(100f);
            Assert.That(health.Damage(60f), Is.EqualTo(60f));
            Assert.That(health.IsBroken, Is.False);
            Assert.That(health.Damage(60f), Is.EqualTo(40f), "only what was left");
            Assert.That(health.IsBroken, Is.True);
            Assert.That(health.Damage(10f), Is.EqualTo(0f), "a broken barrier takes nothing");
        }

        [Test]
        public void Health_BrokenBarrier_StandsOnlyOnceRepairedToStandAt()
        {
            var health = new BarrierHealth(100f);
            health.Damage(100f);
            health.Repair(20f);
            Assert.That(health.CanStand(0.3f), Is.False);
            health.Repair(15f);
            Assert.That(health.CanStand(0.3f), Is.True);
            Assert.That(health.IsBroken, Is.True, "standing is the owner's call (nothing in the way)");
            health.Stand();
            Assert.That(health.IsBroken, Is.False);
            Assert.That(health.Repair(100f), Is.EqualTo(65f), "repairs stop at the maximum");
            Assert.That(health.IsWhole, Is.True);
        }

        [Test]
        public void Health_NewMaximum_KeepsTheShare()
        {
            var health = new BarrierHealth(200f);
            health.Damage(50f);
            health.SetMax(400f);
            Assert.That(health.Current, Is.EqualTo(300f).Within(0.001f));
            Assert.That(health.Normalized, Is.EqualTo(0.75f).Within(0.001f));
        }

        [TestCase(FencePath, false)]
        [TestCase(GatePath, true)]
        [TestCase(DoubleGatePath, true)]
        public void SegmentPrefab_IsWired(string path, bool isGate)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var segment = prefab.GetComponent<BarrierSegment>();
            Assert.That(segment, Is.Not.Null);
            Assert.That(segment.Definition, Is.Not.Null, "definition");
            Assert.That(segment.Look, Is.Not.Null, "look");
            Assert.That(segment.Definition.MaxHealth, Is.GreaterThan(0f));
            Assert.That(segment.Definition.HitSound.IsValid() && segment.Definition.BreakSound.IsValid() && segment.Definition.RepairSound.IsValid(),
                "hit, break and repair sounds");
            Assert.That(prefab.GetComponent<NavmeshCut>(), Is.Not.Null, "navmesh cut");

            Transform blockerNode = prefab.transform.Find("Blocker");
            Assert.That(blockerNode, Is.Not.Null, "climb blocker");
            var blocker = blockerNode.GetComponent<BoxCollider>();
            Assert.That(blocker.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")), "bullets and the interact cast pass the blocker");
            Assert.That(blocker.size.y, Is.GreaterThan(2f), "too tall to jump over");

            foreach (Collider collider in segment.Look.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.TryGetComponent(out RecastNavmeshModifier modifier)
                    && modifier.includeInScan == RecastNavmeshModifier.ScanInclusion.AlwaysExclude, Is.True,
                    $"{collider.name} must stay out of the navmesh bake (the cut stands for it)");

            // The look's colliders go off when it breaks: without this trigger the interact cast can't find it to rebuild.
            Transform reach = prefab.transform.Find(isGate ? "Doorway" : "Gap");
            Assert.That(reach != null && reach.TryGetComponent(out BoxCollider trigger) && trigger.isTrigger && trigger.enabled, Is.True,
                "a trigger over the opening, for the interact cast");

            Assert.That(prefab.GetComponent<FenceGate>() != null, Is.EqualTo(isGate));
            if (isGate)
            {
                Assert.That(segment.Look.Hinges, Is.Not.Empty, "the leaves' hinges");
                Assert.That(segment.Definition.OpenSound.IsValid() && segment.Definition.CloseSound.IsValid(), "open and close sounds");
            }
        }

        [Test]
        public void PlayerPrefab_HasTheRepairHammer()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var tool = player.GetComponentInChildren<RepairTool>(true);
            Assert.That(tool, Is.Not.Null, "repair hammer");
            Assert.That(tool.Hands, Is.Not.Null);
            Assert.That(tool.Hands.GetGrip(HandSide.Right), Is.Not.Null, "right-hand grip");
        }
    }
}
