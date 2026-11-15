using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Tests
{
    public sealed class GunAnimationTests
    {
        const string PrefabPath = "Assets/_Game/Content/Weapons/AK47/AK47.prefab";
        GameObject _item;
        GunAnimation _animation;
        Transform _root;

        [SetUp]
        public void SetUp()
        {
            _item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            _animation = _item.GetComponentInChildren<GunAnimation>();
            _root = _animation.transform;
        }

        [TearDown]
        public void TearDown()
        {
            _animation.Stop();
            Object.DestroyImmediate(_item);
        }

        [Test]
        public void LargerHandsKeepTheirGripPointOnTheAnimatedContact()
        {
            var hands = _item.GetComponent<ItemHands>();
            var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            var hand = Object.Instantiate(set.Get(HandSide.Right));
            try
            {
                hand.transform.localScale *= 1.12f;
                _animation.Show();
                var grip = hands.GetGrip(HandSide.Right);
                grip.Resolve(hand, Vector3.zero, out var position, out var rotation);
                hand.Place(position, rotation);
                Assert.That(Vector3.Distance(hand.GripPoint.position, position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(hand.GripPoint.rotation, rotation), Is.LessThan(0.1f));
            }
            finally { Object.DestroyImmediate(hand.gameObject); }
        }

        [Test]
        public void AutomaticAnimatorUpdateDoesNotOverwriteTheReloadPose()
        {
            _animation.StartAction(GunAction.Reloading, false);
            _animation.Tick(1f / 60f, 0.24f);
            var magazine = _root.Find("Motion/Weapon/Magazine");
            var motion = _root.Find("Motion");
            Vector3 magazinePose = magazine.localPosition;
            Quaternion weaponRotation = motion.localRotation;
            var animator = _animation.GetComponent<Animator>();
            animator.Update(1f / 60f);
            Assert.That(Vector3.Distance(magazine.localPosition, magazinePose), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(motion.localRotation, weaponRotation), Is.LessThan(0.1f));
        }

        [Test]
        public void StoppingRestoresTheControllerForReequipAndEditorPreview()
        {
            var animator = _animation.GetComponent<Animator>();
            var overrides = animator.runtimeAnimatorController;
            _animation.Show();
            _animation.Stop();
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(overrides));
            _animation.Show();
            _animation.StartAction(GunAction.Reloading, true);
            _animation.Tick(1f / 60f, 0.5f);
            Assert.That(_root.Find("Motion/Weapon/Magazine").GetComponent<Renderer>().enabled, Is.False);
        }

        [Test]
        public void EveryOverrideAndCurveBindsToTheWeapon()
        {
            var animator = _animation.GetComponent<Animator>();
            var overrides = animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.That(overrides, Is.Not.Null);
            foreach (string state in new[] { "Equipped", "Idle", "Firing", "Reloading", "ReloadingEmpty", "Repairing", "RepairingStubborn", "RepairEnd" })
            {
                AnimationClip clip = overrides[state];
                Assert.That(clip, Is.Not.Null, state);
                Assert.That(clip.length, Is.GreaterThan(0f), state);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    Assert.That(AnimationUtility.GetAnimatedObject(_root.gameObject, binding), Is.Not.Null,
                        state + ": " + binding.path + "/" + binding.propertyName);
            }
        }

        // The reloads are retargeted clips: the left hand rides its free control the whole way, so what must hold is
        // that it leaves and rejoins the forend contact without a jump.
        [TestCase(false)]
        [TestCase(true)]
        public void ReloadStartsAndEndsOnTheForendContact(bool empty)
        {
            var target = _root.Find("Motion/HandTargets/Left");
            var forend = _root.Find("Motion/Weapon/Handguard/LeftGrip");
            foreach (float progress in new[] { 0f, 1f })
            {
                _animation.StartAction(GunAction.Reloading, empty);
                _animation.Tick(1f / 60f, progress);
                Assert.That(Vector3.Distance(target.position, forend.position), Is.LessThan(0.0005f), "progress " + progress);
                Assert.That(Quaternion.Angle(target.rotation, forend.rotation), Is.LessThan(0.5f), "progress " + progress);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacementMagazineTakesOverWhereTheOldOneHidesThenSeats(bool empty)
        {
            var magazine = _root.Find("Motion/Weapon/Magazine");
            var replacement = _root.Find("Motion/Weapon/ReplacementMagazine");
            var shown = magazine.GetComponent<MeshRenderer>();
            var replacementShown = replacement.GetComponent<MeshRenderer>();
            Vector3 rest = magazine.localPosition;
            Quaternion restRotation = magazine.localRotation;
            _animation.StartAction(GunAction.Reloading, empty);
            int swap = -1;
            const int steps = 240;
            for (int i = 0; i <= steps; i++)
            {
                _animation.Tick(1f / 60f, i / (float)steps);
                Assert.That(shown.enabled, Is.Not.EqualTo(replacementShown.enabled), "one magazine shows at " + i);
                if (swap >= 0 || shown.enabled)
                    continue;
                swap = i;
                // A thrown magazine is moving fast here: the two only meet exactly on the swap key.
                Assert.That(Vector3.Distance(magazine.position, replacement.position), Is.LessThan(0.08f));
            }
            Assert.That(swap, Is.GreaterThan(steps / 20), "the old magazine leaves the gun before it hides");
            Assert.That(Vector3.Distance(replacement.localPosition, rest), Is.LessThan(0.0005f));
            Assert.That(Quaternion.Angle(replacement.localRotation, restRotation), Is.LessThan(0.5f));
        }

        [Test]
        public void ReloadHandCarriesTheNewMagazineToTheWell()
        {
            var target = _root.Find("Motion/HandTargets/Left");
            var replacement = _root.Find("Motion/Weapon/ReplacementMagazine");
            var shown = replacement.GetComponent<MeshRenderer>();
            _animation.StartAction(GunAction.Reloading, false);
            float swap = 0f;
            for (; swap < 1f && !shown.enabled; swap += 0.01f)
                _animation.Tick(1f / 60f, swap);
            Assert.That(swap, Is.LessThan(0.7f));
            _animation.Tick(1f / 60f, swap + 0.08f);
            Vector3 held = replacement.InverseTransformPoint(target.position);
            Assert.That(Vector3.Distance(replacement.localPosition, _root.Find("Motion/Weapon/Magazine").localPosition), Is.GreaterThan(0.05f),
                "still on its way to the well");
            _animation.Tick(1f / 60f, swap + 0.2f);
            Assert.That(Vector3.Distance(replacement.InverseTransformPoint(target.position), held), Is.LessThan(0.02f));
        }

        [Test]
        public void InterruptionRestoresMagazineBoltAndDefaultHandContact()
        {
            var magazine = _root.Find("Motion/Weapon/Magazine");
            var bolt = _root.Find("Motion/Weapon/Bolt");
            Vector3 magazineRest = magazine.localPosition;
            Vector3 boltRest = bolt.localPosition;
            _animation.StartAction(GunAction.Reloading, true);
            _animation.Tick(0.1f, 0.81f);
            _animation.EndAction();
            _animation.Tick(1f / 60f, 0f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
            Assert.That(Vector3.Distance(magazine.localPosition, magazineRest), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(bolt.localPosition, boltRest), Is.LessThan(0.0001f));
            Assert.That(magazine.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(_root.Find("Motion/Weapon/ReplacementMagazine").GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(Vector3.Distance(_root.Find("Motion/HandTargets/Left").position,
                _root.Find("Motion/Weapon/Handguard/LeftGrip").position), Is.LessThan(0.0001f));
        }

        [Test]
        public void EquippedAndFiringReturnToIdleAndPauseDoesNotAdvance()
        {
            _animation.Show();
            Assert.That(_animation.State, Is.EqualTo(GunClip.Equipped));
            _animation.Tick(0f, 0f);
            Assert.That(_animation.Progress, Is.Zero);
            _animation.Tick(0.5f, 0f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
            _animation.Fired();
            _animation.Tick(0.02f, 0f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Firing));
            _animation.Tick(0.2f, 0f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
        }

        [Test]
        public void RepairingUsesTheJamActionWithoutDisplacingTheMagazine()
        {
            var magazine = _root.Find("Motion/Weapon/Magazine");
            Vector3 rest = magazine.localPosition;
            var bolt = _root.Find("Motion/Weapon/Bolt");
            Vector3 boltRest = bolt.localPosition;
            _animation.StartAction(GunAction.ClearingJam, false);
            _animation.Tick(0.1f, 0.33f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Repairing));
            Assert.That(Vector3.Distance(magazine.localPosition, rest), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(_root.Find("Motion/HandTargets/Left").position,
                _root.Find("Motion/Controls/LeftFree").position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(bolt.localPosition, boltRest), Is.LessThan(0.0001f));
        }

        [Test]
        public void PerfectRepairReturnsToGripThenIdle()
        {
            _animation.StartAction(GunAction.ClearingJam, false);
            _animation.Tick(0.01f, 0.5f);
            var hand = _root.Find("Motion/HandTargets/Left");
            Vector3 start = hand.position;
            _animation.FinishRepair();
            _animation.Tick(0.001f, 0f);
            Assert.That(Vector3.Distance(hand.position, start), Is.LessThan(0.001f));
            _animation.Tick(0.4f, 0f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
        }

        [TestCase(0.64f)]
        [TestCase(0.9f)]
        public void EarlyCompletionReturnsFromTheCurrentRepairPose(float progress)
        {
            _animation.StartAction(GunAction.ClearingJam, false);
            _animation.Tick(0.01f, progress);
            var hand = _root.Find("Motion/HandTargets/Left");
            Vector3 start = hand.position;
            _animation.FinishRepair();
            _animation.Tick(0.001f, 0f);
            Assert.That(Vector3.Distance(hand.position, start), Is.LessThan(0.005f));
            _animation.Tick(0.4f, 0f);
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
        }

        [TestCase(false, 3)]
        [TestCase(true, 4)]
        public void RepairImpactsNotifyOncePerSlapEvenWhenFramesSkip(bool stubborn, int expected)
        {
            int impacts = 0;
            _animation.RepairSlapped += () => impacts++;
            _animation.StartAction(GunAction.ClearingJam, false, stubborn);
            _animation.Tick(0.1f, 0.1f);
            Assert.That(impacts, Is.Zero);
            _animation.Tick(0f, 0.9f);
            Assert.That(impacts, Is.Zero);
            _animation.Tick(0.1f, 0.5f);
            Assert.That(impacts, Is.EqualTo(2));
            _animation.Tick(0.1f, 0.5f);
            Assert.That(impacts, Is.EqualTo(2));
            _animation.Tick(0.1f, 0.95f);
            Assert.That(impacts, Is.EqualTo(expected));
            _animation.StartAction(GunAction.ClearingJam, false, stubborn);
            _animation.Tick(0.1f, 0.95f);
            Assert.That(impacts, Is.EqualTo(expected * 2));
        }

        [Test]
        public void SuccessfulQteSkipsRemainingSlapsAndSounds()
        {
            int impacts = 0;
            _animation.RepairSlapped += () => impacts++;
            _animation.StartAction(GunAction.ClearingJam, false);
            _animation.Tick(0.1f, 0.3f);
            Assert.That(impacts, Is.EqualTo(1));
            _animation.FinishRepair();
            for (int i = 0; i < 30; i++)
                _animation.Tick(1f / 60f, 0f);
            Assert.That(impacts, Is.EqualTo(1));
            Assert.That(_animation.State, Is.EqualTo(GunClip.Idle));
        }
    }
}
