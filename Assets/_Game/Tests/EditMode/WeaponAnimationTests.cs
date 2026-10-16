using System.Linq;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public sealed class WeaponAnimationTests
    {
        GameObject _item;
        GunAnimation _animation;
        Transform _root;

        [TestCase("MP5", false, 3)]
        [TestCase("MP5", true, 4)]
        [TestCase("Pistol", false, 3)]
        [TestCase("Pistol", true, 4)]
        [TestCase("PumpShotgun", false, 3)]
        [TestCase("PumpShotgun", true, 4)]
        [TestCase("DoubleBarrel", false, 3)]
        [TestCase("DoubleBarrel", true, 4)]
        public void RepairSlapsAndEarlyReturnWorkForEachWeapon(string name, bool stubborn, int expected)
        {
            Load(name);
            int impacts = 0;
            _animation.RepairSlapped += () => impacts++;
            _animation.StartAction(GunAction.ClearingJam, false, stubborn);
            for (int i = 0; i <= 240; i++)
                _animation.Tick(1f / 60f, i / 240f);
            Assert.That(impacts, Is.EqualTo(expected));
            _animation.StartAction(GunAction.ClearingJam, false, stubborn);
            _animation.Tick(0.1f, 0.5f);
            Assert.That(impacts, Is.EqualTo(expected + 2));
            var hand = _root.Find("Motion/HandTargets/Left");
            Vector3 start = hand.position;
            _animation.FinishRepair();
            _animation.Tick(0.001f, 0f);
            Assert.That(Vector3.Distance(hand.position, start), Is.LessThan(0.005f));
            _animation.Tick(0.4f, 0f);
            Assert.That(impacts, Is.EqualTo(expected + 2));
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
        }

        void Load(string name)
        {
            _item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/" + name + ".prefab"));
            _animation = _item.GetComponentInChildren<GunAnimation>();
            _root = _animation.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_animation != null) _animation.Stop();
            if (_item != null) Object.DestroyImmediate(_item);
        }

        [TestCase("MP5")]
        [TestCase("Pistol")]
        [TestCase("PumpShotgun")]
        [TestCase("DoubleBarrel")]
        public void AllClipsBindAndHandsKeepTheSameScale(string name)
        {
            Load(name);
            var overrides = _animation.GetComponent<Animator>().runtimeAnimatorController as AnimatorOverrideController;
            Assert.That(overrides.runtimeAnimatorController, Is.SameAs(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Game/_Core/WeaponSystem/Animations/Shared/Gun.controller")));
            foreach (string state in new[] { "Equipped", "Idle", "Firing", "Reloading", "ReloadingEmpty", "Repairing", "Cycling", "RepairingStubborn", "RepairFocus", "RepairEnd" })
            {
                var clip = overrides[state];
                Assert.That(clip.name, Is.EqualTo(name + "_" + state));
                Assert.That(clip.length, Is.GreaterThan(0f));
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    Assert.That(AnimationUtility.GetAnimatedObject(_root.gameObject, binding), Is.Not.Null,
                        state + ": " + binding.path + "/" + binding.propertyName);
            }
            var hands = _item.GetComponent<ItemHands>();
            Assert.That(hands.HandScale, Is.EqualTo(1.12f).Within(0.001f));
            var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            _animation.Show();
            foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
            {
                var hand = Object.Instantiate(set.Get(side));
                try
                {
                    hand.transform.localScale *= hands.HandScale;
                    var grip = hands.GetDefault(side);
                    grip.Resolve(hand, Vector3.zero, out var p, out var r);
                    hand.Place(p, r);
                    Assert.That(Vector3.Distance(hand.GripPoint.position, p), Is.LessThan(0.0001f));
                    Assert.That(Quaternion.Angle(hand.GripPoint.rotation, r), Is.LessThan(0.1f));
                }
                finally { Object.DestroyImmediate(hand.gameObject); }
            }
        }

        [TestCase("MP5", false, 0.2f, "Magazine")]
        [TestCase("MP5", false, 0.5f, "ReplacementMagazine")]
        [TestCase("MP5", true, 0.81f, "Bolt")]
        [TestCase("Pistol", false, 0.2f, "Magazine")]
        [TestCase("Pistol", false, 0.5f, "ReplacementMagazine")]
        [TestCase("Pistol", true, 0.81f, "Slide")]
        [TestCase("PumpShotgun", false, 0.5f, "Shells")]
        [TestCase("DoubleBarrel", false, 0.5f, "Hinge/Shells")]
        public void ReloadHandFollowsItsPartAfterTheAutomaticAnimatorPass(string name, bool empty, float progress, string part)
        {
            Load(name);
            _animation.StartAction(GunAction.Reloading, empty);
            _animation.Tick(1f / 60f, progress);
            var motion = _root.Find("Motion");
            var position = motion.localPosition;
            var rotation = motion.localRotation;
            _animation.GetComponent<Animator>().Update(1f / 60f);
            Assert.That(Vector3.Distance(motion.localPosition, position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(motion.localRotation, rotation), Is.LessThan(0.1f));
            AssertContact("Motion/Weapon/" + part + "/LeftGrip");
        }

        [TestCase("MP5")]
        [TestCase("Pistol")]
        [TestCase("PumpShotgun")]
        [TestCase("DoubleBarrel")]
        public void SlapAndInterruptedReloadRestoreTheMechanicalParts(string name)
        {
            Load(name);
            var parts = _root.Find("Motion/Weapon").GetComponentsInChildren<Transform>(true);
            var positions = parts.Select(p => p.localPosition).ToArray();
            var rotations = parts.Select(p => p.localRotation).ToArray();
            var renderers = _root.GetComponentsInChildren<MeshRenderer>(true);
            var visibility = renderers.Select(r => r.enabled).ToArray();
            _animation.StartAction(GunAction.ClearingJam, false);
            _animation.Tick(0.01f, 0.43f);
            AssertContact("Motion/Controls/LeftFree");
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.That(Vector3.Distance(parts[i].localPosition, positions[i]), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(parts[i].localRotation, rotations[i]), Is.LessThan(0.1f));
            }
            _animation.StartAction(GunAction.Reloading, true);
            _animation.Tick(0.01f, 0.52f);
            _animation.EndAction();
            _animation.Tick(0.001f, 0f);
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.That(Vector3.Distance(parts[i].localPosition, positions[i]), Is.LessThan(0.0001f), parts[i].name);
                Assert.That(Quaternion.Angle(parts[i].localRotation, rotations[i]), Is.LessThan(0.1f), parts[i].name);
            }
            for (int i = 0; i < renderers.Length; i++)
                Assert.That(renderers[i].enabled, Is.EqualTo(visibility[i]), renderers[i].name);
        }

        [Test]
        public void PumpCycleKeepsContactAndPerRoundReloadRepeatsWithoutDroppingATube()
        {
            Load("PumpShotgun");
            var pump = _root.Find("Motion/Weapon/Pump");
            var rest = pump.localPosition;
            _animation.StartAction(GunAction.Cycling, false);
            _animation.Tick(0.01f, 0.5f);
            Assert.That(pump.localPosition.z, Is.LessThan(rest.z - 0.08f));
            AssertContact("Motion/Weapon/Pump/LeftGrip");
            var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/PumpShotgun.asset");
            var item = new GunInstance(definition);
            item.SetRounds(definition.Magazine.Size - 2);
            var ammo = new AmmoReserve();
            ammo.Add(definition.Magazine.AmmoType, 2);
            var gun = new Gun(item, ammo, () => 1f);
            gun.ActionStarted += a => _animation.StartAction(a, item.Rounds == 0);
            gun.ActionEnded += (_, _) => _animation.EndAction();
            var input = new GunInput { IsReady = true, CanFire = true, CanAim = true, Reload = true };
            gun.Tick(input, 0f);
            input.Reload = false;
            for (int shell = 0; shell < 2; shell++)
            {
                gun.Tick(input, definition.Magazine.ReloadTime * 0.5f);
                _animation.Tick(0.01f, gun.ActionProgress);
                AssertContact("Motion/Weapon/Shells/LeftGrip");
                Assert.That(_root.Find("Motion/Weapon/Shells/Shell0").GetComponent<Renderer>().enabled, Is.True);
                gun.Tick(input, definition.Magazine.ReloadTime * 0.5f);
                _animation.Tick(0.01f, gun.ActionProgress);
                AssertContact("Motion/Weapon/Pump/LeftGrip");
                Assert.That(_root.Find("Motion/Weapon/Shells/Shell0").GetComponent<Renderer>().enabled, Is.False);
            }
            Assert.That(item.IsFull, Is.True);
            Assert.That(gun.Action, Is.EqualTo(GunAction.None));
            Assert.That(Vector3.Distance(pump.localPosition, rest), Is.LessThan(0.0001f));
            Assert.That(_root.Find("Motion/Weapon/MagazineTube").GetComponent<Renderer>().enabled, Is.True);
        }

        [Test]
        public void DoubleBarrelOpensForShellsAndClosesBeforeReturningToIdle()
        {
            Load("DoubleBarrel");
            var hinge = _root.Find("Motion/Weapon/Hinge");
            var rest = hinge.localRotation;
            _animation.StartAction(GunAction.Reloading, true);
            _animation.Tick(0.01f, 0.5f);
            Assert.That(Quaternion.Angle(hinge.localRotation, rest), Is.GreaterThan(30f));
            Assert.That(hinge.Find("Shells").GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True);
            _animation.Tick(0.01f, 0.97f);
            Assert.That(Quaternion.Angle(hinge.localRotation, rest), Is.LessThan(0.1f));
            Assert.That(hinge.Find("Shells").GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.False);
        }

        [TestCase("MP5", false)]
        [TestCase("MP5", true)]
        [TestCase("Pistol", false)]
        [TestCase("Pistol", true)]
        [TestCase("PumpShotgun", false)]
        [TestCase("DoubleBarrel", false)]
        public void SwitchingHandContactsDoesNotJump(string name, bool empty)
        {
            Load(name);
            bool shells = name == "PumpShotgun" || name == "DoubleBarrel";
            float[] switches = shells ? new[] { 0.02f, 0.28f, 0.73f, 1f } : empty ?
                new[] { 0.02f, 0.145f, 0.3f, 0.42f, 0.665f, 0.77f, 0.85f, 0.93f } :
                new[] { 0.02f, 0.145f, 0.3f, 0.42f, 0.785f, 0.9f };
            _animation.StartAction(GunAction.Reloading, empty);
            var target = _root.Find("Motion/HandTargets/Left");
            foreach (float t in switches)
            {
                _animation.Tick(0.001f, t - 0.00001f);
                var position = target.position;
                var rotation = target.rotation;
                _animation.Tick(0.001f, t + (t < 1f ? 0.00001f : 0f));
                Assert.That(Vector3.Distance(target.position, position), Is.LessThan(0.002f), "position at " + t);
                Assert.That(Quaternion.Angle(target.rotation, rotation), Is.LessThan(1f), "rotation at " + t);
            }
        }

        void AssertContact(string path)
        {
            var target = _root.Find("Motion/HandTargets/Left");
            var contact = _root.Find(path);
            Assert.That(contact, Is.Not.Null, path);
            Assert.That(Vector3.Distance(target.position, contact.position), Is.LessThan(0.0001f), path);
            Assert.That(Quaternion.Angle(target.rotation, contact.rotation), Is.LessThan(0.1f), path);
        }
    }
}
