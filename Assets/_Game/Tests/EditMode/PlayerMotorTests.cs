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
    }
}
