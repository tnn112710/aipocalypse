using Ami.BroAudio;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RobotSystem;
using MessagePipe;
using NUnit.Framework;
using VContainer;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MB13.Aipocalypse.Tests
{
    public sealed class RobotTests
    {
        const string ActionsPath = "Assets/_Game/Settings/InputSystem_Actions.inputactions";

        [Test]
        public void Input_WhistleIsBoundToQ()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            InputAction whistle = actions.FindAction("Player/Whistle");

            Assert.IsNotNull(whistle, "Player/Whistle is missing");
            Assert.IsTrue(HasBinding(whistle, "<Keyboard>/q"), "Whistle isn't bound to Q");
            foreach (InputAction other in actions.FindActionMap("Player").actions)
                if (other != whistle)
                    Assert.IsFalse(HasBinding(other, "<Keyboard>/q"), $"{other.name} also uses Q");
        }

        [Test]
        public void Whistle_CallsATargetWithinRange()
        {
            using var scene = new WhistleScene();
            WhistleTargetStub target = scene.Target(30f);

            Assert.AreSame(target, scene.Whistle.Tick(true, 0f));
            Assert.AreEqual(1, target.Calls);
        }

        [Test]
        public void Whistle_IgnoresTargetsBeyondRange()
        {
            using var scene = new WhistleScene();
            WhistleTargetStub target = scene.Target(60f);

            Assert.IsNull(scene.Whistle.Tick(true, 0f));
            Assert.AreEqual(0, target.Calls);
        }

        [Test]
        public void Whistle_IsBlockedByWalls()
        {
            using var scene = new WhistleScene();
            WhistleTargetStub target = scene.Target(30f);
            scene.Wall(10f);

            Assert.IsNull(scene.Whistle.Tick(true, 0f));
            Assert.AreEqual(0, target.Calls);
        }

        [Test]
        public void Whistle_RespectsCooldown()
        {
            using var scene = new WhistleScene();
            WhistleTargetStub target = scene.Target(30f);

            scene.Whistle.Tick(true, 0f);
            scene.Whistle.Tick(true, 0.1f);

            Assert.AreEqual(1, target.Calls);
        }

        [Test]
        public void Whistle_IgnoresTheWhistlersOwnBody()
        {
            using var scene = new WhistleScene();
            WhistleTargetStub target = scene.Target(30f);
            scene.Actor.AddComponent<CapsuleCollider>();

            Assert.AreSame(target, scene.Whistle.Tick(true, 0f));
        }

        [Test]
        public void Call_LeavesAfterTheWaitOnceArrived()
        {
            var call = new RobotCall();
            call.Begin(Vector3.zero, Vector3.zero, 0f);
            call.MarkArrived(10f, Vector3.zero);

            Assert.IsFalse(call.ShouldLeave(29.9f, Vector3.zero, waitSeconds: 20f, leaveDistance: 6f));
            Assert.IsTrue(call.ShouldLeave(30f, Vector3.zero, waitSeconds: 20f, leaveDistance: 6f));
        }

        [Test]
        public void Call_LeavesWhenThePlayerWalksAwayFromWhereTheyWhistled()
        {
            var call = new RobotCall();
            var whistledFrom = new Vector3(20f, 0f, 0f);
            call.Begin(Vector3.zero, whistledFrom, 0f);
            call.MarkArrived(1f, whistledFrom);

            Assert.IsFalse(call.ShouldLeave(2f, whistledFrom + new Vector3(0f, 3f, 6f), waitSeconds: 20f, leaveDistance: 6f));
            Assert.IsTrue(call.ShouldLeave(2f, whistledFrom + new Vector3(0f, 0f, 6.1f), waitSeconds: 20f, leaveDistance: 6f));
        }

        [Test]
        public void Call_WaitsForAPlayerWhoStaysOutsideTheYard()
        {
            // Whistled from 20 m beyond the fence: the robot waits at the fence, far from the player, and doesn't leave.
            var call = new RobotCall();
            var whistledFrom = new Vector3(20f, 0f, 0f);
            call.Begin(Vector3.zero, whistledFrom, 0f);
            call.MarkArrived(1f, whistledFrom);

            Assert.IsFalse(call.ShouldLeave(5f, whistledFrom, waitSeconds: 20f, leaveDistance: 6f));
        }

        [Test]
        public void Call_DoesNotLeaveBeforeArriving()
        {
            var call = new RobotCall();
            call.Begin(Vector3.zero, Vector3.zero, 0f);

            Assert.IsFalse(call.ShouldLeave(100f, new Vector3(50f, 0f, 0f), waitSeconds: 20f, leaveDistance: 6f));
        }

        static readonly Vector3 YardHalf = new(10f, 2f, 5f);

        [Test]
        public void MeetPoint_StopsArriveDistanceFromThePlayerOnTheRobotsSide()
        {
            Vector3 meet = RobotCall.PlanMeetPoint(new Vector3(8f, 0f, 0f), Vector3.zero, 1.8f, Matrix4x4.identity, YardHalf, 1.2f);

            Assert.AreEqual(new Vector3(1.8f, 0f, 0f), meet);
        }

        [Test]
        public void MeetPoint_StaysTheMarginInsideTheYardWhenThePlayerIsOutside()
        {
            Vector3 meet = RobotCall.PlanMeetPoint(new Vector3(0f, 0f, 0f), new Vector3(30f, 0f, 0f), 1.8f, Matrix4x4.identity, YardHalf, 1.2f);

            Assert.AreEqual(10f - 1.2f, meet.x, 1e-4f);
            Assert.IsTrue(YardMath.Contains(Matrix4x4.identity, YardHalf, meet, 1.2f - 1e-4f));
        }

        [Test]
        public void MeetPoint_StaysTheMarginInsideForAPlayerAtTheFence()
        {
            // The player stands inside, against the fence, with the robot further along the fence: the point mustn't
            // sit on or past the fence.
            Vector3 meet = RobotCall.PlanMeetPoint(new Vector3(0f, 0f, 4.9f), new Vector3(5f, 0f, 4.9f), 1.8f, Matrix4x4.identity, YardHalf, 1.2f);

            Assert.LessOrEqual(meet.z, 5f - 1.2f + 1e-4f);
        }

        [Test]
        public void MeetPoint_OutsideTheYardIsAtTheRobotsHeight()
        {
            // The ground beyond the fence can be metres lower than the yard: the point inside must be on the yard's level.
            Vector3 meet = RobotCall.PlanMeetPoint(new Vector3(0f, 30f, 0f), new Vector3(30f, 24f, 0f), 1.8f, Matrix4x4.identity, YardHalf, 1.2f);

            Assert.AreEqual(30f, meet.y, 1e-4f);
        }

        [Test]
        public void MeetPoint_InsideTheYardIsAtThePlayersHeight()
        {
            Vector3 meet = RobotCall.PlanMeetPoint(new Vector3(5f, 30f, 0f), new Vector3(0f, 31f, 0f), 1.8f, Matrix4x4.identity, YardHalf, 1.2f);

            Assert.AreEqual(31f, meet.y, 1e-4f);
        }

        [Test]
        public void Yard_ContainsWithAMargin()
        {
            Assert.IsTrue(YardMath.Contains(Matrix4x4.identity, YardHalf, new Vector3(8.5f, 0f, 0f), 1.2f));
            Assert.IsFalse(YardMath.Contains(Matrix4x4.identity, YardHalf, new Vector3(9.5f, 0f, 0f), 1.2f));
        }

        [Test]
        public void Call_MeasuresWalkingAwayFromWhereThePlayerStoodOnArrival()
        {
            // The player walked 10 m toward the robot after whistling: arriving next to them isn't "walked away".
            var call = new RobotCall();
            call.Begin(new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, 10f), 0f);
            call.MarkArrived(5f, Vector3.zero);

            Assert.IsFalse(call.ShouldLeave(6f, Vector3.zero, waitSeconds: 20f, leaveDistance: 8f));
            Assert.IsTrue(call.ShouldLeave(6f, new Vector3(0f, 0f, -8.1f), waitSeconds: 20f, leaveDistance: 8f));
        }

        [Test]
        public void Call_RetargetMovesTheMeetingPoint()
        {
            var call = new RobotCall();
            call.Begin(Vector3.zero, Vector3.zero, 0f);

            call.Retarget(new Vector3(3f, 0f, 0f));

            Assert.AreEqual(new Vector3(3f, 0f, 0f), call.MeetPoint);
            Assert.IsTrue(call.Active);
        }

        [Test]
        public void TravelTimeout_GrowsWithTheDistance()
        {
            // 40 m at 1.3 m/s takes 31 s: the walk mustn't give up before that.
            Assert.Greater(RobotCall.TravelTimeout(40f, 1.3f, 3f), 40f / 1.3f);
            Assert.Greater(RobotCall.TravelTimeout(40f, 1.3f, 3f), RobotCall.TravelTimeout(10f, 1.3f, 3f));
            Assert.GreaterOrEqual(RobotCall.TravelTimeout(0f, 1.3f, 3f), 3f);
        }

        [Test]
        public void Call_EndStopsIt()
        {
            var call = new RobotCall();
            call.Begin(new Vector3(1f, 2f, 3f), Vector3.zero, 0f);
            Assert.IsTrue(call.Active);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), call.MeetPoint);

            call.End();

            Assert.IsFalse(call.Active);
        }

        [Test]
        public void Beeps_KeepAGapPerRobot()
        {
            var beeps = new RobotBeeps(robotGap: 1.5f, sharedGap: 0f);

            Assert.IsTrue(beeps.TryPlay(0, 0f));
            Assert.IsFalse(beeps.TryPlay(0, 1f));
            Assert.IsTrue(beeps.TryPlay(0, 1.6f));
        }

        [Test]
        public void Beeps_KeepASharedGap()
        {
            var beeps = new RobotBeeps(robotGap: 1.5f, sharedGap: 0.5f);

            Assert.IsTrue(beeps.TryPlay(0, 0f));
            Assert.IsFalse(beeps.TryPlay(1, 0.3f));
            Assert.IsTrue(beeps.TryPlay(1, 0.6f));
        }

        [Test]
        public void Cooldown_ReadyAfterItsTime()
        {
            var cooldown = new RobotCooldown();
            Assert.IsTrue(cooldown.Ready(0f));

            cooldown.Use(0f, 8f);

            Assert.IsFalse(cooldown.Ready(7.9f));
            Assert.IsTrue(cooldown.Ready(8f));
        }

        [Test]
        public void Personality_StaysWithinTheSpread()
        {
            var temper = new RobotSettings.Temper { StrollChance = 0.5f, WaveChance = 0.5f, FlourishChance = 0.5f, Patience = 1f };

            RobotPersonality low = RobotPersonality.Roll(temper, 0.25f, 0f, 0f, 0f, 0f);
            RobotPersonality high = RobotPersonality.Roll(temper, 0.25f, 1f, 1f, 1f, 1f);

            Assert.AreEqual(0.375f, low.StrollChance, 1e-5f);
            Assert.AreEqual(0.625f, high.WaveChance, 1e-5f);
            Assert.AreEqual(0.75f, low.Patience, 1e-5f);
            Assert.AreEqual(1.25f, high.Patience, 1e-5f);
        }

        [Test]
        public void Personality_ClampsChances()
        {
            var temper = new RobotSettings.Temper { StrollChance = 0.9f, WaveChance = 0.9f, FlourishChance = 0.9f, Patience = 0.1f };

            RobotPersonality high = RobotPersonality.Roll(temper, 0.5f, 1f, 1f, 1f, 0f);

            Assert.AreEqual(1f, high.FlourishChance, 1e-5f);
            Assert.GreaterOrEqual(high.Patience, 0.1f);
        }

        [Test]
        public void Yard_ClosestInside_PullsAPointOutsideToTheFenceMinusMargin()
        {
            Vector3 inside = YardMath.ClosestInside(Matrix4x4.identity, new Vector3(10f, 1f, 5f), new Vector3(20f, 3f, 0f), 1f);

            Assert.AreEqual(new Vector3(9f, 3f, 0f), inside);
        }

        [Test]
        public void Yard_ClosestInside_KeepsAPointInside()
        {
            var point = new Vector3(2f, 0f, -1f);

            Assert.AreEqual(point, YardMath.ClosestInside(Matrix4x4.identity, new Vector3(10f, 1f, 5f), point, 1f));
        }

        [Test]
        public void Yard_ClosestInside_FollowsTheYardsTurn()
        {
            // A yard 20 × 10 m turned 90° about Y: its long side runs along world Z.
            Matrix4x4 worldToLocal = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Vector3.one).inverse;

            Vector3 inside = YardMath.ClosestInside(worldToLocal, new Vector3(10f, 1f, 5f), new Vector3(0f, 0f, 20f), 1f);

            Assert.AreEqual(0f, inside.x, 1e-4f);
            Assert.AreEqual(9f, inside.z, 1e-4f);
        }

        [Test]
        public void Yard_Contains()
        {
            Assert.IsTrue(YardMath.Contains(Matrix4x4.identity, new Vector3(10f, 1f, 5f), new Vector3(9f, 50f, -4f)));
            Assert.IsFalse(YardMath.Contains(Matrix4x4.identity, new Vector3(10f, 1f, 5f), new Vector3(11f, 0f, 0f)));
        }

        const string RobotsContent = "Assets/_Game/Content/Robots";
        const string SettingsPath = "Assets/_Game/Data/Robots/RobotSettings.asset";
        const string ServicesFolder = "Assets/_Game/Data/Robots/Services";

        static readonly string[] RobotPrefabs = { RobotsContent + "/Robot_Shop.prefab", RobotsContent + "/Robot_Upgrades.prefab" };
        static readonly string[] VendorScripts = { "Rob15Ctrl", "Rob11Ctrl", "RobotColorManager", "Rob11ColorManager", "EmotionChanger", "PushObjects", "PushDetection", "TextureScroller", "CamHolder", "RobotLift" };
        static readonly string[] AnimatorParameters = { "Speed", "Turn", "Emote", "EmoteIndex", "Hit", "HitIndex", "Standby" };

        [Test]
        public void RobotTree_IsBuiltAndAssigned()
        {
            var tree = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RobotsContent + "/_Shared/Robot.asset");
            Assert.IsNotNull(tree, "build it: TreeKit.Build AgentScripts/Robots/Robot.tree.txt");
            foreach (string path in RobotPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                // The test assembly doesn't reference Opsive: read the BehaviorTree's subtree by its serialized name.
                Component behavior = System.Array.Find(prefab.GetComponents<Component>(), c => c != null && c.GetType().Name == "BehaviorTree");
                Assert.IsNotNull(behavior, path + ": BehaviorTree");
                Assert.AreSame(tree, new SerializedObject(behavior).FindProperty("m_Subtree").objectReferenceValue, path + " runs another tree");
            }
        }

        [Test]
        public void RobotPrefabs_AreWired()
        {
            int layer = LayerMask.NameToLayer("Robot");
            Assert.GreaterOrEqual(layer, 0, "no Robot layer");
            foreach (string path in RobotPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                var robot = prefab.GetComponent<Robot>();
                Assert.IsNotNull(robot, path + ": Robot");
                Assert.IsNotNull(robot.Service, path + ": service");
                Assert.IsNotNull(robot.Face, path + ": face");
                Assert.IsNotNull(robot.Face.Set, path + ": face set");
                Assert.IsNotNull(robot.Head, path + ": head bone");
                Assert.IsNotNull(prefab.GetComponent<MB13.Aipocalypse.Core.EnemySystem.EnemyMover>(), path + ": EnemyMover");
                Assert.IsNotNull(prefab.GetComponent<RobotFaceGlow>(), path + ": night glow");
                var body = prefab.GetComponent<Collider>();
                Assert.IsNotNull(body, path + ": body collider");
                Assert.AreEqual(layer, prefab.layer, path + ": layer");
                Assert.IsNull(prefab.GetComponentInChildren<CharacterController>(true), path + ": vendor CharacterController");
                foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.IsFalse(behaviour == null || System.Array.IndexOf(VendorScripts, behaviour.GetType().Name) >= 0,
                        $"{path}: vendor or missing script {(behaviour == null ? "(missing)" : behaviour.GetType().Name)}");

                Animator animator = robot.Animator;
                Assert.IsNotNull(animator, path + ": animator");
                Assert.IsFalse(animator.applyRootMotion, path + ": root motion");
                var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                Assert.IsNotNull(controller, path + ": controller");
                foreach (string parameter in AnimatorParameters)
                    Assert.IsTrue(System.Array.Exists(controller.parameters, p => p.name == parameter), $"{path}: no {parameter} parameter");

                Bounds bounds = default;
                bool first = true;
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>())
                {
                    if (first)
                        bounds = renderer.bounds;
                    else
                        bounds.Encapsulate(renderer.bounds);
                    first = false;
                }
                Assert.That(bounds.size.y, Is.InRange(1.2f, 1.45f), path + ": about 1.3 m tall");
            }
        }

        [Test]
        public void RobotPrefabs_LookDifferent()
        {
            var materials = new System.Collections.Generic.HashSet<Material>();
            foreach (string path in RobotPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                Renderer body = System.Array.Find(prefab.GetComponentsInChildren<Renderer>(true), r => r.name == "Rob15_LOD0");
                Assert.IsNotNull(body, path + ": Rob15_LOD0");
                materials.Add(body.sharedMaterial);
            }
            Assert.AreEqual(RobotPrefabs.Length, materials.Count, "each robot has its own tint");
        }

        [Test]
        public void FaceSet_HasAFrameForEveryExpression()
        {
            string[] guids = AssetDatabase.FindAssets("t:RobotFaceSet", new[] { RobotsContent });
            Assert.IsNotEmpty(guids, "no RobotFaceSet under " + RobotsContent);
            foreach (string guid in guids)
            {
                var set = AssetDatabase.LoadAssetAtPath<RobotFaceSet>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsFalse(string.IsNullOrEmpty(set.EyesRenderer) || string.IsNullOrEmpty(set.MouthRenderer), set.name + ": renderer names");
                foreach (RobotExpression expression in System.Enum.GetValues(typeof(RobotExpression)))
                {
                    Assert.IsTrue(set.Frames.TryGetValue(expression, out RobotFaceSet.Frame frame), $"{set.name}: no frame for {expression}");
                    Assert.That(frame.Eyes, Is.InRange(0, set.Columns - 1), $"{set.name} {expression} eyes");
                    Assert.That(frame.Mouth, Is.InRange(0, set.Columns - 1), $"{set.name} {expression} mouth");
                }
            }
        }

        [Test]
        public void Settings_HasASoundForEverySound()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RobotSettings>(SettingsPath);
            Assert.IsNotNull(settings, SettingsPath);
            foreach (RobotSound sound in System.Enum.GetValues(typeof(RobotSound)))
                Assert.IsTrue(settings.Sounds.TryGetValue(sound, out SoundID id) && id.IsValid(), $"no sound for {sound}");
        }

        [Test]
        public void Services_HaveNameAndVerb()
        {
            string[] guids = AssetDatabase.FindAssets("t:RobotService", new[] { ServicesFolder });
            Assert.GreaterOrEqual(guids.Length, 2, "the Shop and Upgrades services");
            foreach (string guid in guids)
            {
                var service = AssetDatabase.LoadAssetAtPath<RobotService>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsFalse(service.DisplayName.IsEmpty, service.name + ": DisplayName");
                Assert.IsFalse(service.Verb.IsEmpty, service.name + ": Verb");
            }
        }

        [Test]
        public void TakeDamage_ReactsAndAppliesNothing()
        {
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            new CombatInstaller().Install(builder);
            using IObjectResolver resolver = builder.Build();
            int published = 0;
            using System.IDisposable subscription = resolver.Resolve<ISubscriber<DamageDealt>>().Subscribe(_ => published++);
            var go = new GameObject("Robot");
            try
            {
                var robot = go.AddComponent<Robot>();

                DamageResult result = resolver.Resolve<DamageService>().Deal(robot, new DamageInfo(25f, DamageType.Gunshot, new Vector3(0f, 1f, 2f)));

                Assert.AreEqual(0f, result.Applied);
                Assert.AreEqual(0, published, "a hit on a robot shows no hit marker");
                Assert.IsTrue(robot.Hit, "it reacts");
                Assert.IsFalse(robot.IsDead);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Robot_IsNotShowsOwnHits() =>
            Assert.IsFalse(typeof(IShowsOwnHits).IsAssignableFrom(typeof(Robot)), "robots don't bleed: bullets use the surface impact");

        [Test]
        public void Spot_AllowsInsideItsRadiusOnly()
        {
            var go = new GameObject("Spot");
            try
            {
                var spot = go.AddComponent<RobotSpot>();
                spot.Setup(3f, System.Array.Empty<Bounds>());

                Assert.IsTrue(spot.Allows(new Vector3(2f, 0f, 0f)));
                Assert.IsFalse(spot.Allows(new Vector3(3.5f, 0f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Spot_DisallowsItsNoGoBoxes()
        {
            var go = new GameObject("Spot");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                var spot = go.AddComponent<RobotSpot>();
                // A doorway 1 m ahead of the spot (local +Z), 2 m wide.
                spot.Setup(4f, new[] { new Bounds(new Vector3(0f, 0f, 1f), new Vector3(2f, 4f, 1f)) });

                Assert.IsFalse(spot.Allows(new Vector3(11f, 0f, 0f)), "inside the doorway box");
                Assert.IsTrue(spot.Allows(new Vector3(9f, 0f, 0f)), "behind the spot");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // An eye at the origin looking along +Z, the whistler's body around it, and whatever a test places in front.
        sealed class WhistleScene : System.IDisposable
        {
            readonly System.Collections.Generic.List<Object> _made = new();
            readonly PlayerInteractionTuning _tuning = ScriptableObject.CreateInstance<PlayerInteractionTuning>();

            public GameObject Actor { get; }
            public PlayerWhistle Whistle { get; }

            public WhistleScene()
            {
                Actor = Make("Whistler");
                var eye = Make("Eye");
                eye.transform.SetParent(Actor.transform, false);
                Whistle = new PlayerWhistle(eye.transform, Actor, _tuning, null, default);
                _made.Add(_tuning);
            }

            public WhistleTargetStub Target(float distance)
            {
                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _made.Add(target);
                target.transform.position = new Vector3(0f, 0f, distance);
                Physics.SyncTransforms();
                return target.AddComponent<WhistleTargetStub>();
            }

            public void Wall(float distance)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _made.Add(wall);
                wall.transform.position = new Vector3(0f, 0f, distance);
                wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
                Physics.SyncTransforms();
            }

            GameObject Make(string name)
            {
                var made = new GameObject(name);
                _made.Add(made);
                return made;
            }

            public void Dispose()
            {
                foreach (Object made in _made)
                    if (made != null)
                        Object.DestroyImmediate(made);
            }
        }

        static bool HasBinding(InputAction action, string path)
        {
            foreach (InputBinding binding in action.bindings)
                if (binding.path == path)
                    return true;
            return false;
        }
    }
}
