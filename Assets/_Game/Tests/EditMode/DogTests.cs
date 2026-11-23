using System.Linq;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEditor;

namespace MB13.Aipocalypse.Tests
{
    // Dog assets and wiring: settings, prefab, runtime bridge.
    public sealed class DogTests
    {
        const string DogsData = "Assets/_Game/Data/Dogs";

        [Test]
        public void Prefab_LokiHasNoMalbersOrRootMotion()
        {
            var loki = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Game/Content/Dogs/Loki/Loki.prefab");
            Assert.That(loki, Is.Not.Null, "Loki prefab");
            foreach (var component in loki.GetComponentsInChildren<UnityEngine.Component>(true))
                Assert.That(component.GetType().Namespace ?? "", Does.Not.StartWith("MalbersAnimations"), component.GetType().Name);
            Assert.That(loki.GetComponent<Dog>(), Is.Not.Null, "Dog component");
            var animator = loki.GetComponentInChildren<UnityEngine.Animator>(true);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.runtimeAnimatorController.name, Is.EqualTo("BorderCollie"));
            var agent = new SerializedObject(loki.GetComponent<Pathfinding.FollowerEntity>());
            // 0.3 m at the model's 1×, scaled with it (DogSetup.ModelScale 2).
            Assert.That(agent.FindProperty("shape.radius").floatValue, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void TakeDamage_FromPlayerYelpsAndAppliesNothing()
        {
            var dog = new UnityEngine.GameObject("Dog").AddComponent<Dog>();
            try
            {
                var info = new MB13.Aipocalypse.Core.Combat.DamageInfo(25f, MB13.Aipocalypse.Core.Combat.DamageType.Gunshot,
                    dog.transform.position + UnityEngine.Vector3.forward, source: new UnityEngine.GameObject("Player"));
                MB13.Aipocalypse.Core.Combat.DamageResult result = dog.TakeDamage(info);
                Assert.That(result.Applied, Is.EqualTo(0f));
                Assert.That(result.Killed, Is.False);
                Assert.That(dog.IsDead, Is.False);
                UnityEngine.Object.DestroyImmediate(info.Source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dog.gameObject);
            }
        }

        [Test]
        public void Dog_IsNeverDeadAndTargetsAsDog()
        {
            var dog = new UnityEngine.GameObject("Dog").AddComponent<Dog>();
            try
            {
                Assert.That(dog.IsDead, Is.False);
                Assert.That(dog.Kind, Is.EqualTo(MB13.Aipocalypse.Core.Entities.TargetKind.Dog));
                Assert.That(dog.IsTargetable, Is.False, "not active: nobody may target it");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dog.gameObject);
            }
        }

        [Test]
        public void JumpArc_PicksByRiseAndObstacle()
        {
            Assert.That(DogJumpArc.Pick(0f, 2f, 3f), Is.EqualTo(JumpKind.Leap), "a 2 m fence: leapt over");
            Assert.That(DogJumpArc.Pick(0f, 3f, 3f), Is.EqualTo(JumpKind.Leap), "up to the leap height");
            Assert.That(DogJumpArc.Pick(0f, 3.5f, 3f), Is.EqualTo(JumpKind.Scramble), "a taller wall: climbed");
            Assert.That(DogJumpArc.Pick(1.5f, 0f, 3f), Is.EqualTo(JumpKind.Leap), "up onto a block");
            Assert.That(DogJumpArc.Pick(3.5f, 0f, 3f), Is.EqualTo(JumpKind.ClimbUp));
            Assert.That(DogJumpArc.Pick(-1.2f, 0f, 3f), Is.EqualTo(JumpKind.DropDown));
            Assert.That(DogJumpArc.Pick(0.3f, 0f, 3f), Is.EqualTo(JumpKind.Hop));
            Assert.That(DogJumpArc.Pick(0.3f, 0.5f, 3f), Is.EqualTo(JumpKind.Hop), "a low log is hopped");
        }

        [Test]
        public void JumpArc_LeapWaitsForTakeOffThenClearsAndLands()
        {
            var start = new UnityEngine.Vector3(0f, 1f, 0f);
            const float fence = 2f;
            foreach (var end in new[] { new UnityEngine.Vector3(0f, 1f, 4f), new UnityEngine.Vector3(0f, 3.5f, 4f) })
            {
                float peak = DogJumpArc.Peak(JumpKind.Leap, start, end, end.y > start.y ? 0f : fence);
                float takeOff = DogJumpArc.LeapTakeOff / DogJumpArc.Duration(JumpKind.Leap, 4f);
                Assert.That(DogJumpArc.Position(JumpKind.Leap, start, end, peak, takeOff * 0.9f), Is.EqualTo(start), "still crouching");
                float overMiddle = float.MinValue;
                for (float t = 0f; t <= 1f; t += 0.005f)
                {
                    UnityEngine.Vector3 p = DogJumpArc.Position(JumpKind.Leap, start, end, peak, t);
                    if (UnityEngine.Mathf.Abs(p.z - 2f) < 0.05f)
                        overMiddle = UnityEngine.Mathf.Max(overMiddle, p.y);
                }
                float top = end.y > start.y ? end.y : start.y + fence;
                Assert.That(overMiddle, Is.GreaterThan(top), "above the fence or the block edge at the middle");
                Assert.That(DogJumpArc.Position(JumpKind.Leap, start, end, peak, 1f), Is.EqualTo(end), "lands on the far side or on top");
            }
        }

        [Test]
        public void JumpArc_LedgeClipIsPickedForTheModelSize()
        {
            Assert.That(DogJumpArc.AnimatorKind(JumpKind.ClimbUp, 1f), Is.EqualTo(3), "1×: the 1.0 m ledge clip");
            Assert.That(DogJumpArc.AnimatorKind(JumpKind.ClimbUp, 1f, 2f), Is.EqualTo(2), "2×: a 1 m step is the 0.5 m clip");
            Assert.That(DogJumpArc.AnimatorKind(JumpKind.ClimbUp, 4f, 2f), Is.EqualTo(5));
        }

        // The leap's body path follows its clip: takeoff and touchdown where the vendor take leaves and meets the ground
        // (its Root height), and JumpKind 7 starts it.
        [Test]
        public void Controller_LeapClipMatchesTheBodyPath()
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Game/Content/Dogs/BorderCollie/BorderCollie.controller");
            var leap = controller.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == "Leap");
            Assert.That(controller.layers[0].stateMachine.anyStateTransitions.Any(t => t.destinationState == leap
                && t.conditions.Any(c => c.parameter == "JumpKind" && c.threshold == 7f)));
            var vendor = AssetDatabase.LoadAllAssetsAtPath("Assets/_StoreAssets/Characters/Malbers Animations/Animals Packs/04 Pets/Dog (Border Collie)/Animations/Jump/Dog_Jump_Walk.fbx")
                .OfType<UnityEngine.AnimationClip>().First(c => c.name == "Dog_Jump_Walk");
            var y = AnimationUtility.GetEditorCurve(vendor, EditorCurveBinding.FloatCurve("Root", typeof(UnityEngine.Transform), "m_LocalPosition.y"));
            float off = -1f, down = -1f;
            for (float t = 0f; t <= vendor.length; t += 0.005f)
            {
                if (off < 0f && y.Evaluate(t) > 0.02f)
                    off = t;
                if (off >= 0f && down < 0f && y.Evaluate(t) < 0.02f)
                    down = t;
            }
            Assert.That(((UnityEngine.AnimationClip)leap.motion).length, Is.EqualTo(vendor.length).Within(0.01f));
            Assert.That(DogJumpArc.LeapTakeOff, Is.EqualTo(off / leap.speed).Within(0.05f), "takeoff");
            Assert.That(DogJumpArc.LeapTakeOff + DogJumpArc.LeapFlight, Is.EqualTo(down / leap.speed).Within(0.05f), "touchdown");
        }

        // The fence scramble's clips, as the controller plays them, take the time the body's path over the fence does.
        [Test]
        public void Controller_ScrambleClipsMatchTheBodyPath()
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Game/Content/Dogs/BorderCollie/BorderCollie.controller");
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToDictionary(s => s.name);
            float Played(string name)
            {
                var state = states[name];
                var exit = state.transitions.First(t => t.hasExitTime);
                return ((UnityEngine.AnimationClip)state.motion).length * exit.exitTime / state.speed;
            }
            float up = Played("ScrambleGrab") + Played("ScrambleClimb");
            Assert.That(up, Is.EqualTo(DogJumpArc.ScrambleUpTime).Within(0.1f), "grab + climb");
            Assert.That(Played("FromEdge"), Is.EqualTo(DogJumpArc.ScrambleDownTime).Within(0.1f), "drop off the far side");
            Assert.That(states["ScrambleClimb"].transitions.Any(t => t.destinationState == states["FromEdge"]));
        }

        // Crossings along a fence on z = 0 (the dog at z = -3, the player at z = +10): near end, far end, whether it stands.
        static (UnityEngine.Vector3 Near, UnityEngine.Vector3 Far, bool Standing) Crossing(float x, bool standing) =>
            (new UnityEngine.Vector3(x, 0f, -2f), new UnityEngine.Vector3(x, 0f, 2f), standing);

        static readonly UnityEngine.Vector3 DogAt = new(0f, 0f, -3f);
        static readonly UnityEngine.Vector3 PlayerAt = new(0f, 0f, 10f);

        [Test]
        public void ScrambleOver_NeverPicksAFallenSegment()
        {
            var ways = new[] { Crossing(0f, false), Crossing(6f, true) };
            Assert.That(DogRoster.ScrambleOver(DogAt, PlayerAt, ways, false, 0f), Is.EqualTo(1), "no way through: over the standing one");
            Assert.That(DogRoster.ScrambleOver(DogAt, PlayerAt, new[] { Crossing(0f, false) }, false, 0f), Is.EqualTo(-1), "nothing standing");
        }

        [Test]
        public void ScrambleOver_WalksThroughAGapCloseBy()
        {
            var ways = new[] { Crossing(0f, true), Crossing(4f, false) };
            Assert.That(DogRoster.ScrambleOver(DogAt, PlayerAt, ways, true, 10f), Is.EqualTo(-1), "a gap 4 m along: walked through");
            Assert.That(DogRoster.ScrambleOver(DogAt, PlayerAt, new[] { Crossing(0f, false) }, true, 10f), Is.EqualTo(-1), "the fence isn't built");
        }

        [Test]
        public void ScrambleOver_ClimbsWhenTheGapIsFar()
        {
            var ways = new[] { Crossing(0f, true), Crossing(30f, false) };
            Assert.That(DogRoster.ScrambleOver(DogAt, PlayerAt, ways, true, 10f), Is.EqualTo(0));
            Assert.That(DogRoster.ScrambleOver(DogAt, PlayerAt, new[] { Crossing(0f, true) }, true, 10f), Is.EqualTo(0), "no gap at all");
        }

        [Test]
        public void JumpArc_ScrambleClearsTheObstacle()
        {
            var start = new UnityEngine.Vector3(0f, 1f, 0f);
            var end = new UnityEngine.Vector3(0f, 1f, 2f);
            const float fence = 2f;
            float peak = DogJumpArc.Peak(JumpKind.Scramble, start, end, fence);
            Assert.That(peak, Is.GreaterThanOrEqualTo(start.y + fence + 0.1f - 1e-4f));
            Assert.That(DogJumpArc.Position(JumpKind.Scramble, start, end, peak, 0f), Is.EqualTo(start));
            Assert.That(DogJumpArc.Position(JumpKind.Scramble, start, end, peak, 1f), Is.EqualTo(end));
            float highest = float.MinValue;
            float overFence = float.MinValue;
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                UnityEngine.Vector3 p = DogJumpArc.Position(JumpKind.Scramble, start, end, peak, t);
                highest = UnityEngine.Mathf.Max(highest, p.y);
                if (UnityEngine.Mathf.Abs(p.z - 1f) < 0.05f)
                    overFence = UnityEngine.Mathf.Max(overFence, p.y);
            }
            Assert.That(highest, Is.EqualTo(peak).Within(0.02f));
            Assert.That(overFence, Is.GreaterThanOrEqualTo(start.y + fence), "over the fence line it is above the top");
            Assert.That(DogJumpArc.Duration(JumpKind.Scramble, 2f), Is.GreaterThan(DogJumpArc.Duration(JumpKind.Hop, 2f)));
        }

        [Test]
        public void Settings_EveryDogSoundHasAnId()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DogSettings>(DogsData + "/DogSettings.asset");
            Assert.That(settings, Is.Not.Null, "DogSettings asset");
            foreach (DogSound sound in System.Enum.GetValues(typeof(DogSound)))
            {
                Assert.That(settings.Sounds.TryGetValue(sound, out var id), Is.True, $"{sound} has an entry");
                Assert.That(id.IsValid(), Is.True, $"{sound} id is valid");
            }
        }
    }
}
