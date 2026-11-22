using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using Ami.BroAudio;
using NUnit.Framework;
using Pathfinding;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class DefenseTests
    {
        const string FencePath = "Assets/_Game/Content/Structures/WoodFence/WoodFence.prefab";
        const string GatePath = "Assets/_Game/Content/Structures/WoodGate/WoodGate.prefab";
        const string DoubleGatePath = "Assets/_Game/Content/Structures/WoodGate/WoodGate_Double.prefab";
        const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
        const string HammerPath = "Assets/_Game/Content/Tools/Hammer/Hammer.asset";
        const string ConfigPath = "Assets/_Game/Data/Equipment/EquipmentConfig.asset";
        const string HighlightStylePath = "Assets/_Game/Data/Defense/RepairHighlightStyle.asset";

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

        [Test]
        public void Health_StartAt_ZeroIsDebris_ElseStandsAtTheShare()
        {
            var debris = new BarrierHealth(300f);
            debris.StartAt(0f);
            Assert.That(debris.IsBroken, Is.True);
            Assert.That(debris.Current, Is.EqualTo(0f));

            var worn = new BarrierHealth(300f);
            worn.StartAt(0.4f);
            Assert.That(worn.IsBroken, Is.False);
            Assert.That(worn.Current, Is.EqualTo(120f).Within(0.001f));

            var whole = new BarrierHealth(300f);
            whole.StartAt(1f);
            Assert.That(whole.IsWhole, Is.True);
        }

        [Test]
        public void FenceLook_HasDebrisForWhenItsDown()
        {
            var segment = AssetDatabase.LoadAssetAtPath<GameObject>(FencePath).GetComponent<BarrierSegment>();
            GameObject debris = segment.Look.Debris;
            Assert.That(debris, Is.Not.Null, "debris");
            Assert.That(debris.activeSelf, Is.False, "hidden while it stands");
            Assert.That(debris.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(debris.GetComponentsInChildren<Collider>(true), Is.Empty, "nothing to snag on in the gap");
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
        public void Hammer_IsAToolItem_HeldWithTheRightHand()
        {
            var item = AssetDatabase.LoadAssetAtPath<RepairToolDefinition>(HammerPath);
            Assert.That(item, Is.Not.Null, HammerPath);
            Assert.That(item.Category, Is.EqualTo(ItemCategory.Tool));
            Assert.That(item.Icon, Is.Not.Null, "hotbar icon");
            Assert.That(item.HeldPrefab, Is.InstanceOf<RepairTool>(), "the held item repairs");
            var hands = item.HeldPrefab.GetComponent<ItemHands>();
            Assert.That(hands != null && hands.GetGrip(HandSide.Right) != null, Is.True, "right-hand grip");

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Assert.That(player.GetComponentInChildren<RepairTool>(true), Is.Null, "the player holds the hammer item, no built-in one");
        }

        [Test]
        public void Hammer_HasEveryPoseMarker()
        {
            var item = AssetDatabase.LoadAssetAtPath<RepairToolDefinition>(HammerPath);
            var tool = new SerializedObject(item.HeldPrefab);
            foreach (string field in new[] { "_posePivot", "_idlePose", "_windUpPose", "_strikePose", "_sprintPose" })
                Assert.That(tool.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }

        [Test]
        public void RepairTool_HoldsOffASprintOnlyWhileKnocking()
        {
            var go = new GameObject("RepairTool");
            try
            {
                var tool = go.AddComponent<RepairTool>();
                Assert.That(tool.StopsSprint(false, false), Is.False, "resting");
                tool.Cycle = 0.3f;
                Assert.That(tool.StopsSprint(false, false), Is.True, "mid-knock");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EquipmentConfig_HasASlotForTheHammer()
        {
            ItemContainer hotbar = AssetDatabase.LoadAssetAtPath<EquipmentConfig>(ConfigPath).CreateInventory().Hotbar;
            Assert.That(hotbar.Count, Is.GreaterThanOrEqualTo(4));
            var hammer = AssetDatabase.LoadAssetAtPath<RepairToolDefinition>(HammerPath);
            Assert.That(hotbar.FindFreeSlot(hammer.CreateInstance()), Is.GreaterThanOrEqualTo(0), "a slot takes tools");
        }

        [Test]
        public void RepairHighlight_FadesUpClose()
        {
            var style = ScriptableObject.CreateInstance<RepairHighlightStyle>();
            try
            {
                Assert.That(style.OpacityAt(style.FadeFrom + 5f), Is.EqualTo(1f));
                Assert.That(style.OpacityAt(0f), Is.EqualTo(style.NearOpacity));
                float mid = style.OpacityAt((style.FadeFrom + style.FadeTo) * 0.5f);
                Assert.That(mid, Is.GreaterThan(style.NearOpacity).And.LessThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void RepairAllTarget_NamesTheCount_WithoutNewStringsEachTime()
        {
            Assert.That(BarrierRegistry.RepairAllTarget(5), Is.EqualTo(Loc.Interaction("target.fences").Format(5)));
            Assert.That(BarrierRegistry.RepairAllTarget(5), Is.SameAs(BarrierRegistry.RepairAllTarget(5)));
        }

        [Test]
        public void RepairHighlightStyle_DrawsThroughWalls()
        {
            var style = AssetDatabase.LoadAssetAtPath<RepairHighlightStyle>(HighlightStylePath);
            Assert.That(style != null && style.Look != null, Is.True, HighlightStylePath);
            Assert.That(style.Look.shader.name, Is.EqualTo("Aipocalypse/Ghosts/GhostXray"), "seen through walls");
        }
    }
}
