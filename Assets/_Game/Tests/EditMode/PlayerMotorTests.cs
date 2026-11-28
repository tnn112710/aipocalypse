using System.Collections.Generic;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Defaults;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // A real CharacterController moved through edit-mode physics: drops and landings as fall damage sees them.
    public class PlayerMotorTests
    {
        const float Frame = 1f / 60f;
        // Away from whatever scene is open, so its colliders can't get in the way.
        static readonly Vector3 Far = new(1000f, 1000f, 1000f);

        readonly List<Object> _created = new();
        readonly List<LandingInfo> _landings = new();
        PlayerMotor _motor;
        CharacterController _controller;

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
            _landings.Clear();
        }

        GameObject Box(Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetPositionAndRotation(Far + position, rotation);
            box.transform.localScale = scale;
            _created.Add(box);
            return box;
        }

        void CreatePlayer(Vector3 feet)
        {
            var body = new GameObject("Player");
            _created.Add(body);
            body.transform.position = Far + feet;
            _controller = body.AddComponent<CharacterController>();
            _controller.height = 1.8f;
            _controller.radius = 0.35f;
            _controller.center = new Vector3(0f, 0.9f, 0f);
            var tuning = ScriptableObject.CreateInstance<PlayerMovementTuning>();
            _created.Add(tuning);
            _motor = new PlayerMotor(_controller, tuning, new BaseStats(), new Stamina(100f, 1f, 0.3f));
            _motor.Landed += _landings.Add;
            body.AddComponent<ControllerHitRelay>().Motor = _motor;
            Physics.SyncTransforms();
        }

        void RunUntilStableGround(int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                _motor.Tick(default, Frame);
                if (i > 5 && _motor.IsGrounded && !_motor.IsOnSteepSlope && _landings.Count > 0)
                    return;
            }
        }

        [Test]
        public void Fall_LandsOnce_WithTheDropHeight()
        {
            Box(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), Quaternion.identity);
            CreatePlayer(new Vector3(0f, 6f, 0f));

            RunUntilStableGround(300);

            Assert.AreEqual(1, _landings.Count);
            Assert.AreEqual(6f, _landings[0].DropHeight, 0.2f);
        }

        [Test]
        public void SlidingDownASteepSlope_CountsAsAFall_ToTheFlatGroundBelow()
        {
            const float angle = 60f;
            Box(new Vector3(0f, -0.5f, -10f), new Vector3(30f, 1f, 40f), Quaternion.identity);
            // A ramp rising towards +z from the ground at z = 0, steeper than the controller's slope limit.
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 along = tilt * Vector3.forward;
            Vector3 normal = tilt * Vector3.up;
            Box(along * 10f - normal * 0.25f, new Vector3(6f, 0.5f, 20f), tilt);
            Vector3 surface = along * 14f;
            CreatePlayer(surface + normal * 0.3f);
            Assert.Greater(angle, _controller.slopeLimit);

            RunUntilStableGround(600);

            Assert.IsTrue(_motor.IsGrounded && !_motor.IsOnSteepSlope, "reached the flat ground");
            Assert.AreEqual(1, _landings.Count, "touching the slope isn't a landing; reaching flat ground is");
            Assert.Greater(_landings[0].DropHeight, surface.y - 1f, "the whole slide counts for fall damage");
        }

        // Ground under the origin and a player standing on it, settled.
        void StandOnGround()
        {
            Box(new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), Quaternion.identity);
            CreatePlayer(new Vector3(0f, 0.1f, 0f));
            RunUntilStableGround(120);
            Assert.IsTrue(_motor.IsGrounded, "settled on the ground");
        }

        void Run(PlayerIntent intent, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                _motor.Tick(intent, Frame);
                intent.Crouch = false;
                intent.Jump = false;
            }
        }

        static PlayerIntent Forward(bool sprint = false) => new() { Move = Vector2.up, Sprint = sprint };

        [Test]
        public void Crouch_ShrinksTheCapsuleFromTheTop_AndKeepsTheFeet()
        {
            StandOnGround();
            float feet = _controller.transform.position.y;

            Run(new PlayerIntent { Crouch = true }, 30);

            Assert.IsTrue(_motor.IsCrouching);
            Assert.Less(_controller.height, 1.8f);
            Assert.AreEqual(_controller.height * 0.5f, _controller.center.y, 0.001f, "capsule bottom stays at the feet");
            Assert.AreEqual(feet, _controller.transform.position.y, 0.05f);
            Assert.AreEqual(1.8f - _controller.height, _motor.CrouchEyeDrop, 0.001f, "eye fully down after the transition");

            Run(new PlayerIntent { Crouch = true }, 30);

            Assert.IsFalse(_motor.IsCrouching);
            Assert.AreEqual(1.8f, _controller.height, 0.001f);
            Assert.AreEqual(0f, _motor.CrouchEyeDrop, 0.001f);
        }

        [Test]
        public void Crouched_MovesAtCrouchSpeed_AndDoesNotSprint()
        {
            StandOnGround();
            Run(new PlayerIntent { Crouch = true }, 1);

            Run(Forward(), 120);

            Assert.IsTrue(_motor.IsCrouching);
            Assert.IsFalse(_motor.IsSprinting);
            Assert.AreEqual(_motor.CrouchSpeed, _motor.HorizontalVelocity.magnitude, 0.1f);
            Assert.Less(_motor.CrouchSpeed, _motor.WalkSpeed);
        }

        [Test]
        public void CrouchingWhileSprinting_StaysDown_UntilSprintIsPressedAgain()
        {
            StandOnGround();
            Run(Forward(sprint: true), 30);
            Assert.IsTrue(_motor.IsSprinting);

            PlayerIntent crouch = Forward(sprint: true);
            crouch.Crouch = true;
            Run(crouch, 30);
            Assert.IsTrue(_motor.IsCrouching, "the held sprint key doesn't stand it back up");
            Assert.IsFalse(_motor.IsSprinting);

            Run(Forward(), 2);
            Run(Forward(sprint: true), 30);

            Assert.IsFalse(_motor.IsCrouching, "a fresh sprint press stands up");
            Assert.IsTrue(_motor.IsSprinting);
        }

        [Test]
        public void Jump_WhileCrouched_OnlyStandsUp_AndTheNextPressJumps()
        {
            StandOnGround();
            Run(new PlayerIntent { Crouch = true }, 10);
            int jumps = 0;
            _motor.Jumped += () => jumps++;

            Run(new PlayerIntent { Jump = true }, 10);

            Assert.IsFalse(_motor.IsCrouching);
            Assert.AreEqual(0, jumps, "the press isn't kept in the jump buffer either");

            Run(new PlayerIntent { Jump = true }, 1);

            Assert.AreEqual(1, jumps);
        }

        [Test]
        public void UnderALowCeiling_CrouchStaysDown_AndJumpAndSprintDoNothing()
        {
            StandOnGround();
            Run(new PlayerIntent { Crouch = true }, 10);
            // A slab whose underside is 1.4 m up: room for the crouched capsule, not the standing one.
            Box(new Vector3(0f, 1.65f, 0f), new Vector3(6f, 0.5f, 6f), Quaternion.identity);
            Physics.SyncTransforms();
            int jumps = 0;
            _motor.Jumped += () => jumps++;

            Run(new PlayerIntent { Crouch = true }, 5);
            Run(new PlayerIntent { Jump = true }, 5);
            Run(Forward(sprint: true), 5);

            Assert.IsTrue(_motor.IsCrouching);
            Assert.AreEqual(0, jumps, "a blocked jump isn't kept for later either");
            Assert.IsFalse(_motor.IsSprinting);
            Assert.IsFalse(_motor.TryStand());
        }

        [Test]
        public void Overloaded_CrawlsAndNeitherSprintsNorJumps()
        {
            StandOnGround();
            _motor.Overloaded = true;
            _motor.LoadSpeedMultiplier = 0.15f;
            int jumps = 0;
            _motor.Jumped += () => jumps++;

            Run(new PlayerIntent { Jump = true }, 5);
            Run(Forward(sprint: true), 120);

            Assert.AreEqual(0, jumps);
            Assert.IsFalse(_motor.IsSprinting);
            Assert.AreEqual(_motor.WalkSpeed * 0.15f, _motor.HorizontalVelocity.magnitude, 0.05f, "below the slowdown floor: the crawl wins");
        }

        [Test]
        public void HeavyLoad_SlowsTheWalk()
        {
            StandOnGround();
            _motor.LoadSpeedMultiplier = 0.8f;

            Run(Forward(), 120);

            Assert.AreEqual(_motor.WalkSpeed * 0.8f, _motor.HorizontalVelocity.magnitude, 0.05f);
        }

        [Test]
        public void Crouch_InTheAir_IsIgnored()
        {
            CreatePlayer(new Vector3(0f, 20f, 0f));

            Run(new PlayerIntent { Crouch = true }, 1);

            Assert.IsFalse(_motor.IsCrouching);
        }

        [Test]
        public void Teleport_StandsUp()
        {
            StandOnGround();
            Run(new PlayerIntent { Crouch = true }, 30);

            _motor.Teleport(Far + new Vector3(5f, 0.1f, 5f), Quaternion.identity);

            Assert.IsFalse(_motor.IsCrouching);
            Assert.AreEqual(1.8f, _controller.height, 0.001f);
            Assert.AreEqual(0f, _motor.CrouchEyeDrop, 0.001f);
        }
    }
}
