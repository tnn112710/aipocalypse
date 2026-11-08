using System.Reflection;
using MB13.Aipocalypse.Core.PlayerSystem;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class PoseSequenceTests
    {
        GameObject _body;
        Transform _root;
        Transform _pivot;
        PoseSequencePlayer _player;
        PoseSequence _sequence;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _root = new GameObject("CameraRoot").transform;
            _root.SetParent(_body.transform, false);
            _root.localPosition = new Vector3(0f, 1.65f, 0f);
            _pivot = new GameObject("CameraPitch").transform;
            _pivot.SetParent(_root, false);
            _player = new PoseSequencePlayer(_body.transform, new PlayerCameraRig(_root, _pivot, 0f), null);
            _sequence = ScriptableObject.CreateInstance<PoseSequence>();
            Set("_beats", new[] { Beat(1f, 0.3f, new Vector2(0.5f, -1f), -20f, 0f, 1.2f) });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_sequence);
        }

        static PoseBeat Beat(float duration, float height, Vector2 offset, float pitch, float hold, float fov = 1f) => new()
        {
            Duration = duration, Ease = Ease.Linear, Height = height, Offset = offset, Pitch = pitch, Hold = hold, FovScale = fov,
        };

        void Set(string field, object value) =>
            typeof(PoseSequence).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_sequence, value);

        [Test]
        public void StartsWhereTheCameraWas_WithTheLookPitchFoldedIn()
        {
            _player.Play(_sequence, pitch: 10f);

            Assert.IsTrue(_player.IsPlaying);
            Assert.AreEqual(new Vector3(0f, 1.65f, 0f), _root.localPosition);
            Assert.AreEqual(10f, Mathf.DeltaAngle(0f, _root.localEulerAngles.x), 0.01f);
            Assert.AreEqual(Quaternion.identity, _pivot.localRotation);
        }

        [Test]
        public void MovesThroughTheBeat_ThenSettlesOnIt()
        {
            _player.Play(_sequence, pitch: 0f);

            _player.Tick(0.5f);
            Assert.AreEqual(0.975f, _root.localPosition.y, 0.001f, "halfway between 1.65 and 0.3");
            Assert.IsFalse(_player.IsSettled);

            _player.Tick(0.6f);
            Assert.IsTrue(_player.IsSettled);
            Assert.AreEqual(0.5f, _root.localPosition.x, 0.001f);
            Assert.AreEqual(0.3f, _root.localPosition.y, 0.001f, "no ground found: the feet's level");
            Assert.AreEqual(-1f, _root.localPosition.z, 0.001f);
            Assert.AreEqual(-20f, Mathf.DeltaAngle(0f, _root.localEulerAngles.x), 0.01f);
            Assert.AreEqual(1.2f, _player.FovScale, 0.001f);
        }

        [Test]
        public void FeetBeats_LandOnTheGroundUnderThem()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -2.5f, 0f);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            Physics.SyncTransforms();
            try
            {
                _player.Play(_sequence, 0f);
                _player.Tick(2f);
                Assert.AreEqual(-2f + 0.3f, _root.localPosition.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void FeetBeats_StopShortOfWalls()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, -0.8f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();
            try
            {
                _player.Play(_sequence, 0f);
                _player.Tick(2f);
                Assert.Greater(_root.localPosition.z, -0.7f, "the eye stays in front of the wall");
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void Ended_RaisedOnce_AfterEndAfter()
        {
            Set("_endAfter", 2f);
            int ended = 0;
            _player.Ended += _ => ended++;
            _player.Play(_sequence, 0f);

            _player.Tick(1f);
            _player.Tick(1.5f);
            Assert.AreEqual(0, ended);
            _player.Tick(1f);
            _player.Tick(5f);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void Ended_NeverRaised_WhenEndAfterIsNegative()
        {
            int ended = 0;
            _player.Ended += _ => ended++;
            _player.Play(_sequence, 0f);
            _player.Tick(100f);
            Assert.AreEqual(0, ended);
        }

        [Test]
        public void Continuous_PassesThroughEachBeatOnTime()
        {
            Set("_continuous", true);
            Set("_beats", new[] { Beat(1f, 1f, Vector2.zero, 30f, 0f), Beat(1f, 0.4f, new Vector2(0f, -1f), -10f, 0f) });
            _player.Play(_sequence, 0f);

            _player.Tick(1f);
            Assert.AreEqual(1f, _root.localPosition.y, 0.001f);
            Assert.AreEqual(30f, Mathf.DeltaAngle(0f, _root.localEulerAngles.x), 0.01f);

            _player.Tick(1f);
            Assert.AreEqual(0.4f, _root.localPosition.y, 0.001f);
            Assert.AreEqual(-1f, _root.localPosition.z, 0.001f);
        }

        [Test]
        public void Continuous_KeepsMovingThroughABeatAndItsHold()
        {
            Set("_continuous", true);
            Set("_beats", new[] { Beat(1f, 1f, Vector2.zero, 0f, 1f), Beat(1f, 0.4f, Vector2.zero, 0f, 0f) });
            _player.Play(_sequence, 0f);

            _player.Tick(1f);
            float atBeat = _root.localPosition.y;
            _player.Tick(0.5f);
            float duringHold = _root.localPosition.y;
            _player.Tick(0.5f);

            Assert.Less(duringHold, atBeat, "still sinking while 'held'");
            Assert.Greater(duringHold, _root.localPosition.y);
        }

        [Test]
        public void Continuous_NeverOvershootsABeat()
        {
            Set("_continuous", true);
            Set("_beats", new[] { Beat(0.5f, 0.3f, Vector2.zero, 0f, 0f), Beat(2f, 0.35f, Vector2.zero, 0f, 0f) });
            _player.Play(_sequence, 0f);

            float lowest = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                _player.Tick(0.05f);
                lowest = Mathf.Min(lowest, _root.localPosition.y);
            }
            Assert.GreaterOrEqual(lowest, 0.3f - 0.0001f, "the eye doesn't dip below the low beat");
        }

        [Test]
        public void Handheld_KeepsTheSettledViewMoving()
        {
            Set("_handheldAngles", new Vector3(1f, 1f, 1f));
            _player.Play(_sequence, 0f);
            _player.Tick(2f);

            Vector3 before = _root.localEulerAngles;
            _player.Tick(1f);
            Vector3 after = _root.localEulerAngles;
            float moved = Mathf.Abs(Mathf.DeltaAngle(before.x, after.x)) + Mathf.Abs(Mathf.DeltaAngle(before.y, after.y)) +
                Mathf.Abs(Mathf.DeltaAngle(before.z, after.z));
            Assert.Greater(moved, 0.001f);
        }

        [Test]
        public void Drift_NeverComesToRest()
        {
            Set("_driftAmplitude", new Vector2(3f, 5f));
            Set("_driftPeriod", 4f);
            _player.Play(_sequence, 0f);
            _player.Tick(1f);

            float slowest = float.MaxValue;
            Vector3 last = _root.localEulerAngles;
            for (int i = 0; i < 100; i++)
            {
                _player.Tick(0.1f);
                Vector3 now = _root.localEulerAngles;
                slowest = Mathf.Min(slowest, Mathf.Abs(Mathf.DeltaAngle(last.x, now.x)) + Mathf.Abs(Mathf.DeltaAngle(last.y, now.y)));
                last = now;
            }
            Assert.Greater(slowest, 0.1f, "some of pitch or yaw is always turning");
        }

        [Test]
        public void NoHandheldOrDrift_SettledViewIsStill()
        {
            _player.Play(_sequence, 0f);
            _player.Tick(2f);

            Quaternion before = _root.localRotation;
            _player.Tick(0.2f);
            Assert.AreEqual(0f, Quaternion.Angle(before, _root.localRotation), 0.0001f);
        }

        [Test]
        public void Stop_ReleasesTheCamera()
        {
            _player.Play(_sequence, 0f);
            _player.Tick(2f);
            _player.Stop();

            Assert.IsFalse(_player.IsPlaying);
            Assert.IsNull(_player.Current);
            Assert.AreEqual(1f, _player.FovScale);
        }
    }
}
