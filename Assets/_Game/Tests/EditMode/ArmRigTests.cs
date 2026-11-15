using MB13.Aipocalypse.Core.HandSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // The first-person arms: whatever the HandSet shows must reach its grips, and the arm prefabs must bend sensibly.
    public sealed class ArmRigTests
    {
        HandRig _rig;

        // Whichever arm variant the hand set shows.
        void Load(string side) =>
            _rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset")
                .Get(side == "Left" ? HandSide.Left : HandSide.Right));

        [TearDown]
        public void TearDown()
        {
            if (_rig != null)
                Object.DestroyImmediate(_rig.gameObject);
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void Place_PutsTheGripPointOnTheTarget_ElbowOnTheHintSide(string side)
        {
            Load(side);
            Assert.That(_rig.HasArm, Is.True);
            float sign = side == "Left" ? -1f : 1f;
            Vector3 shoulder = new(0.2f * sign, -0.4f, 0f);
            Vector3 elbow = new(0.45f * sign, -0.6f, 0.1f);
            var targets = new[]
            {
                (new Vector3(0.1f * sign, -0.2f, 0.28f), Quaternion.Euler(20f, 40f * sign, 10f)),
                (new Vector3(0.05f * sign, -0.3f, 0.25f), Quaternion.Euler(-30f, 0f, 80f * sign)),
            };
            Transform forearm = _rig.GetComponent<ArmRig>().Hand.parent;
            foreach ((Vector3 position, Quaternion rotation) in targets)
            {
                _rig.Place(position, rotation, shoulder, elbow);
                Assert.That(Vector3.Distance(_rig.GripPoint.position, position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(_rig.GripPoint.rotation, rotation), Is.LessThan(0.1f));
                // The elbow sits off the shoulder-to-wrist line, on the side the hint is.
                Vector3 line = _rig.GetComponent<ArmRig>().Hand.position - shoulder;
                Vector3 bend = Vector3.ProjectOnPlane(forearm.position - shoulder, line);
                Assert.That(Vector3.Dot(bend, Vector3.ProjectOnPlane(elbow - shoulder, line)), Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(forearm.parent.position, shoulder), Is.LessThan(0.001f), "in reach: the shoulder stays put");
            }
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void Place_OutOfReach_PullsTheShoulderInsteadOfStretching(string side)
        {
            Load(side);
            Vector3 shoulder = new(0f, -0.4f, 0f);
            Vector3 far = shoulder + Vector3.forward * 3f;
            Transform hand = _rig.GetComponent<ArmRig>().Hand;
            Transform upper = hand.parent.parent;
            float upperLength = Vector3.Distance(upper.position, hand.parent.position);
            float lowerLength = Vector3.Distance(hand.parent.position, hand.position);

            _rig.Place(far, Quaternion.identity, shoulder, shoulder + Vector3.down);

            Assert.That(Vector3.Distance(_rig.GripPoint.position, far), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(upper.position, shoulder), Is.GreaterThan(1f));
            Assert.That(Vector3.Distance(upper.position, hand.parent.position), Is.EqualTo(upperLength).Within(0.001f));
            Assert.That(Vector3.Distance(hand.parent.position, hand.position), Is.EqualTo(lowerLength).Within(0.001f));
        }

        [Test]
        public void HandSet_HandsShowOnEveryGripOfTheAk47()
        {
            var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            var gun = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Weapons/AK47/AK47.prefab");
            var hands = gun.GetComponent<ItemHands>();
            foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
            {
                _rig = Object.Instantiate(set.Get(side));
                HandGrip grip = hands.GetGrip(side);
                grip.Resolve(_rig, set.Elbow(side), out Vector3 position, out Quaternion rotation);
                _rig.Place(position, rotation, set.Shoulder(side), set.Elbow(side));
                Assert.That(Vector3.Distance(_rig.GripPoint.position, position), Is.LessThan(0.001f), side.ToString());
                Object.DestroyImmediate(_rig.gameObject);
            }
        }
    }
}
