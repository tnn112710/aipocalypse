using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Where the dog wants to be relative to the player: player at the origin facing +Z.
    public sealed class DogFollowTests
    {
        const float Frame = 0.1f;

        DogSettings _settings;
        DogFollow _follow;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<DogSettings>();
            _follow = new DogFollow(_settings, 7);
            _follow.Reset(0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        static FollowInput At(Vector3 dog, float now, Vector3 velocity = default, bool sprinting = false,
            bool aiming = false, bool hasPath = true, Vector3 player = default, bool separated = false) =>
            new(player, velocity, Vector3.forward, sprinting, aiming, player + Vector3.up * 1.6f, Vector3.forward, dog,
                hasPath, now, separated);

        // Plans every frame from `from` to `to` (inclusive) with the same input apart from the time; returns the last plan.
        FollowPlan Run(float from, float to, Vector3 dog, Vector3 velocity = default, bool sprinting = false,
            bool aiming = false, bool hasPath = true)
        {
            FollowPlan plan = default;
            for (float t = from; t <= to + 1e-4f; t += Frame)
                plan = _follow.Plan(At(dog, t, velocity, sprinting, aiming, hasPath));
            return plan;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [Test]
        public void Follow_InsideTheZoneIsFree()
        {
            var plan = Run(0f, 2f, new Vector3(4f, 0f, 3f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Free));
        }

        [Test]
        public void Follow_PastOuterReturnsAfterTheReactionDelay()
        {
            var dog = new Vector3(10f, 0f, 0f);
            Assert.That(_follow.Plan(At(dog, 0f)).Mode, Is.EqualTo(FollowMode.Free), "not on the same frame");
            var plan = Run(0.1f, _settings.ReactionDelay.y + 0.1f, dog);
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Return));
            Assert.That(plan.Gait, Is.EqualTo(DogGait.Trot));
            Assert.That(Flat(plan.Goal, Vector3.zero), Is.LessThanOrEqualTo(_settings.InnerRadius + 1f));
            Assert.That(plan.Goal.x, Is.GreaterThan(0f), "on the dog's side");
        }

        [Test]
        public void Follow_PlayerStopNeverStopsTheDogTheSameFrame()
        {
            var dog = new Vector3(2f, 0f, 1f);
            var walking = new Vector3(0f, 0f, 1.5f);
            var plan = Run(0f, 4f, dog, walking);
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Scout), "scouting while the player walks on");
            plan = _follow.Plan(At(dog, 4.1f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Scout), "still scouting on the frame the player stops");
            plan = Run(4.2f, 4.1f + _settings.ReactionDelay.y + 0.1f, dog);
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Free), "free again after the delay");
        }

        [Test]
        public void Follow_ReturnKeepsUpWithAWalkingPlayer()
        {
            var plan = Run(0f, 1.5f, new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 3f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Return));
            Assert.That(plan.Gait, Is.EqualTo(DogGait.Canter), "a trot is slower than the player's walk");
        }

        [Test]
        public void Follow_FarBehindAWalkingPlayersGoalItSprints()
        {
            // Walking steadily: it scouts ahead; with the goal more than 8 m off a canter barely gains on a walker.
            var plan = Run(0f, 4.5f, new Vector3(1f, 0f, -6f), new Vector3(0f, 0f, 3f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Scout));
            Assert.That(plan.Gait, Is.EqualTo(DogGait.Sprint));
        }

        [Test]
        public void Follow_SeparatedByTheFenceComesBackEvenClose()
        {
            var dog = new Vector3(5f, 0f, 0f);
            FollowPlan plan = default;
            for (float t = 0f; t <= _settings.ReactionDelay.y + 0.2f; t += Frame)
                plan = _follow.Plan(At(dog, t, separated: true));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Return), "5 m away but the fence is between them");
        }

        [Test]
        public void Follow_PastSprintRadiusSprints()
        {
            var plan = Run(0f, 2.5f, new Vector3(20f, 0f, 0f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Sprint));
            Assert.That(plan.Gait, Is.EqualTo(DogGait.Sprint));
        }

        [Test]
        public void Follow_CatchesUpWhenFarOrPathless()
        {
            Assert.That(_follow.Plan(At(new Vector3(45f, 0f, 0f), 0f)).Mode, Is.EqualTo(FollowMode.CatchUp), "at once when far");

            _follow.Reset(10f);
            var dog = new Vector3(5f, 0f, 0f);
            var plan = Run(10f, 12.9f, dog, hasPath: false);
            Assert.That(plan.Mode, Is.Not.EqualTo(FollowMode.CatchUp), "not before NoPathTime");
            plan = Run(13f, 13.2f, dog, hasPath: false);
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.CatchUp));
            float behind = Flat(plan.Goal, Vector3.zero);
            Assert.That(behind, Is.InRange(_settings.ReappearMin, _settings.ReappearMax));
            Assert.That(plan.Goal.z, Is.LessThan(0f), "behind the player");
        }

        [Test]
        public void Follow_SteadyWalkScoutsAhead()
        {
            var plan = Run(0f, 2f + _settings.ReactionDelay.y + 0.2f, new Vector3(2f, 0f, 1f), new Vector3(0f, 0f, 1.5f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Scout));
            Assert.That(plan.Goal.z, Is.InRange(_settings.Lead.x, _settings.Lead.y));
        }

        [Test]
        public void Follow_TurningResetsTheSteadyTimer()
        {
            var dog = new Vector3(2f, 0f, 1f);
            Run(0f, 1.5f, dog, new Vector3(0f, 0f, 1.5f));
            var plan = Run(1.6f, 3f, dog, new Vector3(1.5f, 0f, 0f));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Free), "a 90° turn started the timer again");
        }

        [Test]
        public void Follow_PlayerSprintingRunsAlongside()
        {
            var plan = Run(0f, 2.5f, new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, 6f), sprinting: true);
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.Alongside));
            Assert.That(plan.Gait, Is.EqualTo(DogGait.Sprint));
            Assert.That(plan.Goal.x, Is.GreaterThan(0.5f), "beside the player on its side");
        }

        [Test]
        public void Follow_MatchesASprintOnlyAfterTheSprintReaction()
        {
            var dog = new Vector3(3f, 0f, 0f);
            var running = new Vector3(0f, 0f, 6f);
            Assert.That(Run(0f, _settings.SprintReaction.x - 0.1f, dog, running, sprinting: true).Mode, Is.EqualTo(FollowMode.Free), "not yet");
            Assert.That(Run(_settings.SprintReaction.x, _settings.SprintReaction.y + 0.1f, dog, running, sprinting: true).Mode, Is.EqualTo(FollowMode.Alongside));
        }

        [Test]
        public void Follow_SpeedsUpOneGaitAtATime()
        {
            // Far out: a sprint back, reached a gait per GaitRamp from a standing start (trot first).
            var dog = new Vector3(20f, 0f, 0f);
            float start = _settings.ReactionDelay.y + 0.1f;
            Assert.That(Run(0f, start, dog).Gait, Is.EqualTo(DogGait.Trot));
            Assert.That(Run(start + Frame, start + _settings.GaitRamp, dog).Gait, Is.EqualTo(DogGait.Canter));
            Assert.That(Run(start + _settings.GaitRamp + Frame, start + 2f * _settings.GaitRamp, dog).Gait, Is.EqualTo(DogGait.Sprint));
        }

        [Test]
        public void Follow_SlowsDownAtOnce()
        {
            var far = new Vector3(20f, 0f, 0f);
            Assert.That(Run(0f, 3f, far).Gait, Is.EqualTo(DogGait.Sprint));
            // Back inside the zone: once the mode changes (the reaction delay) the gait drops straight down.
            Assert.That(Run(3.1f, 3.1f + _settings.ReactionDelay.y + 0.1f, new Vector3(2f, 0f, 0f)).Gait, Is.EqualTo(DogGait.Still));
        }

        [Test]
        public void Follow_LeavesTheAimCone()
        {
            var plan = Run(0f, 1.5f, new Vector3(0f, 0f, 5f), aiming: true);
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.ClearAim));
            float along = plan.Goal.z;
            float needed = Mathf.Tan(_settings.AimCone * Mathf.Deg2Rad) * Mathf.Max(0f, along) + _settings.AimClearance;
            Assert.That(Mathf.Abs(plan.Goal.x), Is.GreaterThanOrEqualTo(needed - 1e-3f));
        }

        [Test]
        public void Follow_LeavesTheAimConeAtOnceAndQuickly()
        {
            var plan = _follow.Plan(At(new Vector3(0f, 0f, 3f), 0f, aiming: true));
            Assert.That(plan.Mode, Is.EqualTo(FollowMode.ClearAim), "no reaction delay out of the line of fire");
            Assert.That(plan.Gait, Is.EqualTo(DogGait.Canter).Or.EqualTo(DogGait.Sprint), "never a walk out of the aim");
        }

        [Test]
        public void Follow_SideSwapsWithinItsWindow()
        {
            var dog = new Vector3(4f, 0f, 0f);
            _follow.Plan(At(dog, 0f));
            int side = _follow.Side;
            float swappedAt = -1f;
            for (float t = 0f; t <= 41f; t += 0.5f)
            {
                _follow.Plan(At(dog, t));
                if (_follow.Side != side)
                {
                    swappedAt = t;
                    break;
                }
            }
            Assert.That(swappedAt, Is.InRange(_settings.SideSwap.x, _settings.SideSwap.y + 0.5f));
        }
    }
}
