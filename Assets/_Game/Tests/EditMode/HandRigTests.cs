using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.WeaponSystem;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public sealed class HandRigTests
    {
        HandRig _rig;

        void Load(string side) => _rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/_Core/HandSystem/Prefabs/FPHand_" + side + ".prefab")).GetComponent<HandRig>();

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig.gameObject);
        }

        static HandShape Shape(float curl, float spread = 0f, float tip = 0f)
        {
            var finger = new FingerShape(curl, spread, tip);
            return new HandShape { Thumb = finger, Index = finger, Middle = finger, Ring = finger, Little = finger };
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void OrdinaryGripRotationsKeepTheirExistingCalibration(string side)
        {
            Load(side);
            var fingers = new SerializedObject(_rig).FindProperty("_fingers");
            foreach (float curl in new[] { 0f, 0.35f, 0.8f, 1f })
            {
                var shape = Shape(curl, 0.3f, -0.2f);
                _rig.Apply(shape);
                for (int f = 0; f < fingers.arraySize; f++)
                {
                    var finger = fingers.GetArrayElementAtIndex(f);
                    var joints = finger.FindPropertyRelative("Joints");
                    for (int j = 0; j < joints.arraySize; j++)
                    {
                        var open = finger.FindPropertyRelative("Open").GetArrayElementAtIndex(j).quaternionValue;
                        var closed = finger.FindPropertyRelative("Closed").GetArrayElementAtIndex(j).quaternionValue;
                        var spread = finger.FindPropertyRelative("Spread").GetArrayElementAtIndex(j).quaternionValue;
                        var expected = Quaternion.SlerpUnclamped(open, closed, shape[f].JointCurl(j)) *
                            Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Inverse(open) * spread, 0.3f);
                        var joint = (Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue;
                        Assert.That(Quaternion.Angle(expected, joint.localRotation), Is.LessThan(0.06f));
                    }
                }
            }
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void FistUsesBoundedCalibrationAndBlendsContinuouslyFromGrip(string side)
        {
            Load(side);
            var fingers = new SerializedObject(_rig).FindProperty("_fingers");
            foreach (float curl in new[] { 1f, 1.0001f, 1.4f, 1.8f, 5f })
            {
                _rig.Apply(Shape(curl));
                for (int f = 0; f < fingers.arraySize; f++)
                {
                    var finger = fingers.GetArrayElementAtIndex(f);
                    var joints = finger.FindPropertyRelative("Joints");
                    var fist = finger.FindPropertyRelative("Fist");
                    Assert.That(fist.arraySize, Is.EqualTo(joints.arraySize));
                    for (int j = 0; j < joints.arraySize; j++)
                    {
                        var closed = finger.FindPropertyRelative("Closed").GetArrayElementAtIndex(j).quaternionValue;
                        var target = fist.GetArrayElementAtIndex(j).quaternionValue;
                        var expected = Quaternion.Slerp(closed, target, Mathf.InverseLerp(1f, 1.8f, curl));
                        var joint = (Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue;
                        Assert.That(Quaternion.Angle(expected, joint.localRotation), Is.LessThan(0.06f));
                        if (curl == 1.0001f)
                            Assert.That(Quaternion.Angle(closed, joint.localRotation), Is.LessThan(0.1f));
                        if (f > 0 && j > 1)
                        {
                            var open = finger.FindPropertyRelative("Open").GetArrayElementAtIndex(j).quaternionValue;
                            Assert.That(Quaternion.Angle(open, target), Is.LessThanOrEqualTo(j == 2 ? 86f : 16f));
                        }
                    }
                }
            }
        }

        [Test]
        public void UncalibratedFistFallsBackToGripWithoutOvercurling()
        {
            Load("Left");
            var serialized = new SerializedObject(_rig);
            var fingers = serialized.FindProperty("_fingers");
            for (int f = 0; f < fingers.arraySize; f++)
                fingers.GetArrayElementAtIndex(f).FindPropertyRelative("Fist").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _rig.Apply(Shape(FingerShape.MaxCurl));
            for (int f = 0; f < fingers.arraySize; f++)
            {
                var finger = fingers.GetArrayElementAtIndex(f);
                var joints = finger.FindPropertyRelative("Joints");
                for (int j = 0; j < joints.arraySize; j++)
                {
                    var joint = (Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue;
                    var expected = finger.FindPropertyRelative("Closed").GetArrayElementAtIndex(j).quaternionValue;
                    Assert.That(Quaternion.Angle(expected, joint.localRotation), Is.LessThan(0.06f));
                }
            }
        }

        [TestCase("AK47", false)]
        [TestCase("AK47", true)]
        [TestCase("MP5", false)]
        [TestCase("MP5", true)]
        [TestCase("Pistol", false)]
        [TestCase("Pistol", true)]
        [TestCase("PumpShotgun", false)]
        [TestCase("PumpShotgun", true)]
        [TestCase("DoubleBarrel", false)]
        [TestCase("DoubleBarrel", true)]
        public void PunchContactsUseFullFistAndThumbClosesAfterFingers(string weapon, bool stubborn)
        {
            var state = stubborn ? "RepairingStubborn" : "Repairing";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Game/Content/Weapons/" + weapon + "/Animations/" + weapon + "_" + state + ".anim");
            var bindings = AnimationUtility.GetCurveBindings(clip);
            AnimationCurve Curve(string property) => AnimationUtility.GetEditorCurve(clip, bindings.Single(b => b.propertyName == property &&
                (b.type == typeof(GunAnimation) || b.path.EndsWith("/Left"))));
            var impacts = Curve("_repairImpactCount");
            var punch = Curve("_repairPunch");
            int punches = 0;
            float previousCount = 0f;
            foreach (var impact in impacts.keys)
            {
                bool newImpact = impact.value > previousCount;
                previousCount = impact.value;
                if (!newImpact || punch.Evaluate(impact.time) < 0.5f) continue;
                punches++;
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    Assert.That(Curve("_animatedShape." + finger + ".Curl").Evaluate(impact.time), Is.EqualTo(FingerShape.MaxCurl).Within(0.001f));
                Assert.That(Curve("_animatedShape.Thumb.Spread").Evaluate(impact.time), Is.EqualTo(0f).Within(0.001f));
            }
            Assert.That(punches, Is.EqualTo(stubborn ? 2 : 1));
            float thumbClosed = Curve("_animatedShape.Thumb.Curl").keys.First(k => k.value > 1.79f).time;
            float indexClosed = Curve("_animatedShape.Index.Curl").keys.First(k => k.value > 1.79f).time;
            Assert.That(thumbClosed - indexClosed, Is.GreaterThan(0.04f));
        }
    }
}
