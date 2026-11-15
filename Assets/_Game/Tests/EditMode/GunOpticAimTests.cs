using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class GunOpticAimTests
    {
        GameObject _root;
        Transform _eye, _aimPose, _model, _iron, _sight;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Rig");
            _eye = Child("Eye", _root.transform, Vector3.zero);
            _aimPose = Child("Aim", _root.transform, new Vector3(0f, -0.06f, 0.30f));
            _model = Child("Model", _root.transform, Vector3.zero);
            _iron = Child("IronSight", _model, new Vector3(0f, 0.06f, 0.10f));
            _sight = Child("OpticSight", _model, new Vector3(0f, 0.10f, 0.02f));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        static Transform Child(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        // Where the optic's sight point lands, in the eye's space, with the gun at the given pose.
        Vector3 SightFromEye(Transform pose) =>
            _eye.InverseTransformPoint(_root.transform.TransformPoint(pose.localPosition + pose.localRotation * _sight.localPosition));

        [Test]
        public void Place_PutsTheSightOnTheIronLine_AtItsEyeDistance()
        {
            var aim = new GunOpticAim(_aimPose);
            aim.Place(new OpticSettings { EyeDistance = 0.22f }, _sight, _iron, _model, _eye);

            Assert.AreNotSame(_aimPose, aim.AimPose, "aims through the optic");
            Vector3 sight = SightFromEye(aim.AimPose);
            Vector3 iron = _eye.InverseTransformPoint(_root.transform.TransformPoint(_aimPose.localPosition + _iron.localPosition));
            Assert.AreEqual(iron.x, sight.x, 1e-4f, "on the iron sights' line");
            Assert.AreEqual(iron.y, sight.y, 1e-4f);
            Assert.AreEqual(0.22f, sight.z, 1e-4f, "its EyeDistance from the eye");
        }

        [Test]
        public void Place_WithoutAnOptic_AimsThroughTheIrons()
        {
            var aim = new GunOpticAim(_aimPose);
            aim.Place(new OpticSettings(), _sight, _iron, _model, _eye);
            aim.Place(null, null, _iron, _model, _eye);
            Assert.AreSame(_aimPose, aim.AimPose);
        }

        [Test]
        public void Window_ShowsOnlyNearFullAim()
        {
            var aim = new GunOpticAim(_aimPose);
            var window = new GameObject("Window").AddComponent<MeshRenderer>();
            window.transform.SetParent(_root.transform);
            window.enabled = false;

            aim.ShowWindow(window, 0.5f);
            Assert.IsFalse(window.enabled, "the tube's walls show while raising");
            aim.ShowWindow(window, 0.9f);
            Assert.IsTrue(window.enabled);
            aim.ShowWindow(window, 0f);
            Assert.IsFalse(window.enabled);
        }
    }
}
