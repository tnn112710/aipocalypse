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
            // The plain two-bone solve: nothing gives way for the wrist.
            var plain = new SerializedObject(_rig.GetComponent<ArmRig>());
            plain.FindProperty("_wristShare").floatValue = 0f;
            plain.FindProperty("_shoulderGive").floatValue = 0f;
            plain.FindProperty("_turnToArm").vector2Value = Vector2.zero;
            plain.ApplyModifiedPropertiesWithoutUndo();
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
            var so = new SerializedObject(_rig.GetComponent<ArmRig>());
            so.FindProperty("_turnToArm").vector2Value = Vector2.zero;
            so.ApplyModifiedPropertiesWithoutUndo();

            _rig.Place(far, Quaternion.identity, shoulder, shoulder + Vector3.down);

            Assert.That(Vector3.Distance(_rig.GripPoint.position, far), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(upper.position, shoulder), Is.GreaterThan(1f));
            Assert.That(Vector3.Distance(upper.position, hand.parent.position), Is.EqualTo(upperLength).Within(0.001f));
            Assert.That(Vector3.Distance(hand.parent.position, hand.position), Is.EqualTo(lowerLength).Within(0.001f));
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void Place_WristShare_SwingsTheElbowToStraightenTheWrist(string side)
        {
            Load(side);
            float sign = side == "Left" ? -1f : 1f;
            Vector3 shoulder = new(0.2f * sign, -0.3f, 0f);
            Vector3 elbow = new(0.5f * sign, -0.5f, 0.1f);
            var arm = _rig.GetComponent<ArmRig>();
            var so = new SerializedObject(arm);
            Quaternion rest = so.FindProperty("_handRest").quaternionValue;
            // The swing alone: this hold wants the elbow tucked in, which the inward cap would stop.
            so.FindProperty("_elbowInward").floatValue = 1f;

            float Bend(float share)
            {
                so.FindProperty("_wristShare").floatValue = share;
                so.ApplyModifiedPropertiesWithoutUndo();
                _rig.Place(new Vector3(0.05f * sign, -0.35f, 0.3f), Quaternion.Euler(60f, -50f * sign, 30f), shoulder, elbow);
                return Quaternion.Angle(arm.Hand.localRotation, rest);
            }

            Assert.That(Bend(1f), Is.LessThan(Bend(0f) - 5f));
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void Place_TurnToArm_StraightensTheWristTurningOnlyRoundThePartAndFlatOnThePalm(string side)
        {
            Load(side);
            float sign = side == "Left" ? -1f : 1f;
            Vector3 shoulder = new(0.25f * sign, -0.27f, -0.08f);
            Vector3 elbow = new(0.3f * sign, -0.6f, 0.2f);
            Vector3 target = new(-0.08f * sign, -0.28f, 0.39f);
            var arm = _rig.GetComponent<ArmRig>();
            var so = new SerializedObject(arm);
            Quaternion rest = so.FindProperty("_handRest").quaternionValue;
            so.FindProperty("_wristShare").floatValue = 0f;
            so.FindProperty("_shoulderGive").floatValue = 0f;
            // A forend hold whose straight wrist wants the forearm along the barrel, as the retargeted holds do.
            Vector3 axis = arm.Hand.localPosition.normalized;
            Quaternion handRotation = Quaternion.FromToRotation(Quaternion.Inverse(rest) * axis, new Vector3(0.1f * -sign, 0f, 1f).normalized);
            Quaternion grip = handRotation * (Quaternion.Inverse(arm.Hand.rotation) * _rig.GripPoint.rotation);

            float Bend(Vector2 turn)
            {
                so.FindProperty("_turnToArm").vector2Value = turn;
                so.ApplyModifiedPropertiesWithoutUndo();
                _rig.Place(target, grip, shoulder, elbow);
                // Forearm against where a straight wrist would put it (its twist is the twist bone's job).
                return Vector3.Angle(arm.Hand.position - arm.Hand.parent.position, arm.Hand.rotation * Quaternion.Inverse(rest) * axis);
            }

            float held = Bend(Vector2.zero);
            float turned = Bend(new Vector2(30f, 30f));
            Assert.That(turned, Is.LessThan(held - 10f));
            // Rolled round the part's middle line, turned flat on the palm: the grip point stays that far from the line.
            Vector3 middle = target - grip * Vector3.up * so.FindProperty("_turnRadius").floatValue;
            Vector3 fromLine = Vector3.ProjectOnPlane(_rig.GripPoint.position - middle, grip * Vector3.forward);
            Assert.That(fromLine.magnitude, Is.EqualTo(so.FindProperty("_turnRadius").floatValue).Within(0.001f));
            Assert.That(Vector3.Dot(_rig.GripPoint.position - target, grip * Vector3.forward), Is.EqualTo(0f).Within(0.001f));
            // The palm stays on the part (its normal square to the part's axis), rolled round it no more than allowed.
            Vector3 part = grip * Vector3.forward;
            Assert.That(Vector3.Angle(_rig.GripPoint.up, part), Is.EqualTo(90f).Within(0.5f));
            Assert.That(Vector3.Angle(_rig.GripPoint.up, grip * Vector3.up), Is.LessThanOrEqualTo(30.5f));
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void Place_ShoulderGive_StraightensTheWristWithoutRisingIntoView(string side)
        {
            Load(side);
            float sign = side == "Left" ? -1f : 1f;
            Vector3 shoulder = new(0.2f * sign, -0.3f, 0f);
            Vector3 elbow = new(0.5f * sign, -0.5f, 0.1f);
            var arm = _rig.GetComponent<ArmRig>();
            var so = new SerializedObject(arm);
            Quaternion rest = so.FindProperty("_handRest").quaternionValue;
            so.FindProperty("_wristShare").floatValue = 0f;
            so.FindProperty("_maxGive").floatValue = 0.15f;
            so.FindProperty("_giveIntoView").vector2Value = new Vector2(0.02f, 0.02f);
            so.FindProperty("_turnToArm").vector2Value = Vector2.zero;
            Transform upper = arm.Hand.parent.parent;
            // A hand that wants its forearm to come from above and in front: the give that would help most is capped.
            var targets = new[] { Quaternion.Euler(60f, -50f * sign, 30f), Quaternion.Euler(-70f, 20f * sign, 0f), Quaternion.Euler(10f, 170f, 40f * sign) };
            foreach (Quaternion rotation in targets)
            {
                float Bend(float give)
                {
                    so.FindProperty("_shoulderGive").floatValue = give;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    _rig.Place(new Vector3(0.05f * sign, -0.35f, 0.3f), rotation, shoulder, elbow);
                    return Quaternion.Angle(arm.Hand.localRotation, rest);
                }

                Bend(1f);
                Vector3 moved = upper.position - shoulder;
                Assert.That(moved.magnitude, Is.LessThanOrEqualTo(0.151f));
                Assert.That(moved.y, Is.LessThanOrEqualTo(0.021f));
                Assert.That(moved.z, Is.LessThanOrEqualTo(0.021f));
                Assert.That(Vector3.Distance(_rig.GripPoint.position, new Vector3(0.05f * sign, -0.35f, 0.3f)), Is.LessThan(0.001f));
            }

            // A hand whose forearm should come from lower and farther back than the shoulder point: the shoulder may go there.
            // Built backwards: an arm hung from that lower shoulder, and the hand pose its straight wrist would have.
            Vector3 axis = arm.Hand.localPosition.normalized;
            float upperLength = Vector3.Distance(upper.position, arm.Hand.parent.position);
            float lowerLength = Vector3.Distance(arm.Hand.parent.position, arm.Hand.position);
            Vector3 from = new Vector3(-0.5f * sign, 0.2f, 0.8f).normalized;
            Vector3 at = shoulder + new Vector3(0f, -0.1f, -0.1f) + new Vector3(0.3f * sign, -0.6f, 0.5f).normalized * upperLength + from * lowerLength;
            Quaternion handRotation = Quaternion.FromToRotation(Quaternion.Inverse(rest) * axis, from);
            // The elbow has to swing as well, or the forearm still comes from the elbow point.
            so.FindProperty("_wristShare").floatValue = 1f;
            so.FindProperty("_elbowIntoView").vector2Value = Vector2.one;
            float Swing(float give)
            {
                so.FindProperty("_shoulderGive").floatValue = give;
                so.ApplyModifiedPropertiesWithoutUndo();
                Quaternion grip = handRotation * (Quaternion.Inverse(arm.Hand.rotation) * _rig.GripPoint.rotation);
                _rig.Place(at, grip, shoulder, elbow);
                Vector3 wanted = arm.Hand.rotation * Quaternion.Inverse(rest) * axis;
                return Vector3.Angle(wanted, arm.Hand.position - arm.Hand.parent.position);
            }

            Assert.That(Swing(1f), Is.LessThan(Swing(0f) - 3f));
        }

        [Test]
        public void Carry_MovesShouldersWithTheGunsPose()
        {
            var gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Weapons/AK47/AK47.prefab"));
            try
            {
                var hands = gun.GetComponent<ItemHands>();
                var held = new SerializedObject(gun.GetComponent<MB13.Aipocalypse.Core.WeaponSystem.HeldGun>());
                var pivot = (Transform)held.FindProperty("_posePivot").objectReferenceValue;
                var hip = (Transform)held.FindProperty("_hipPose").objectReferenceValue;
                var aim = (Transform)held.FindProperty("_aimPose").objectReferenceValue;
                Vector3 shoulder = new(0.3f, -0.2f, -0.2f);

                pivot.SetLocalPositionAndRotation(hip.localPosition, hip.localRotation);
                Assert.That(Vector3.Distance(hands.Carry(shoulder), shoulder), Is.LessThan(0.0001f), "hip: where the hand set puts it");

                pivot.SetLocalPositionAndRotation(aim.localPosition, aim.localRotation);
                Assert.That(Vector3.Distance(hands.Carry(hip.position), aim.position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(hands.Carry(shoulder), shoulder), Is.GreaterThan(0.05f), "aimed: the shoulder comes along");
            }
            finally { Object.DestroyImmediate(gun); }
        }

        [Test]
        public void Carry_FollowsThePosesMoveButNotItsTurn()
        {
            var gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Weapons/AK47/AK47.prefab"));
            try
            {
                var hands = gun.GetComponent<ItemHands>();
                var held = new SerializedObject(gun.GetComponent<MB13.Aipocalypse.Core.WeaponSystem.HeldGun>());
                var pivot = (Transform)held.FindProperty("_posePivot").objectReferenceValue;
                var hip = (Transform)held.FindProperty("_hipPose").objectReferenceValue;
                Vector3 shoulder = new(0.3f, -0.2f, -0.2f);

                // Tipping the muzzle down (sprint) must not swing the shoulder behind the grip up into view.
                pivot.SetLocalPositionAndRotation(hip.localPosition, hip.localRotation * Quaternion.Euler(40f, -45f, -30f));
                Assert.That(Vector3.Distance(hands.Carry(shoulder), shoulder), Is.LessThan(0.0001f));
            }
            finally { Object.DestroyImmediate(gun); }
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void HandSet_LimitShoulder_CapsInwardUpAndForward(HandSide side)
        {
            var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            Vector3 cap = new SerializedObject(set).FindProperty("_shoulderCarry").vector3Value;
            Vector3 rest = set.Shoulder(side);
            float inward = side == HandSide.Left ? 1f : -1f;

            Vector3 far = set.LimitShoulder(side, rest + new Vector3(inward, 1f, 1f));
            Assert.That((far - rest).x * inward, Is.EqualTo(cap.x).Within(0.0001f));
            Assert.That(far.y - rest.y, Is.EqualTo(cap.y).Within(0.0001f));
            Assert.That(far.z - rest.z, Is.EqualTo(cap.z).Within(0.0001f));
            // Out, down and back are free: that keeps the arm off screen.
            Vector3 away = rest + new Vector3(-inward * 0.3f, -0.3f, -0.3f);
            Assert.That(Vector3.Distance(set.LimitShoulder(side, away), away), Is.LessThan(0.0001f));
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
                // On the grip, or rolled round the held part's middle line by no more than the arm allows.
                var arm = new SerializedObject(_rig.GetComponent<ArmRig>());
                float roll = arm.FindProperty("_turnToArm").vector2Value.x;
                float slack = 2f * arm.FindProperty("_turnRadius").floatValue * Mathf.Sin(roll * 0.5f * Mathf.Deg2Rad);
                Assert.That(Vector3.Distance(_rig.GripPoint.position, position), Is.LessThan(0.001f + slack), side.ToString());
                Object.DestroyImmediate(_rig.gameObject);
            }
        }
    }
}
