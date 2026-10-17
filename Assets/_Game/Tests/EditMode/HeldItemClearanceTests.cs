using System.Reflection;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public sealed class HeldItemClearanceTests
    {
        GameObject _player;
        GameObject _wall;
        Transform _hand;
        HeldItem _held;
        PlayerHeldItemTuning _tuning;
        HeldItemClearance _clearance;
        readonly Vector3 _handRest = new(0f, 0f, 0.5f);

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("ClearanceTestPlayer");
            _player.transform.position = new Vector3(1000f, 1000f, 1000f);
            var rig = _player.AddComponent<EquipmentRig>();
            _hand = new GameObject("Hand").transform;
            _hand.SetParent(_player.transform, false);
            typeof(EquipmentRig).GetField("_hand", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, _hand);
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(_hand, false);
            model.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
            _held = model.AddComponent<HeldItem>();
            _held.Bind(null, null);
            _tuning = ScriptableObject.CreateInstance<PlayerHeldItemTuning>();
            _clearance = new HeldItemClearance(_player.transform, _player.transform, _tuning, rig);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _player.transform.position + Vector3.forward * 0.75f;
            _wall.transform.localScale = new Vector3(3f, 3f, 0.1f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wall);
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_tuning);
        }

        [Test]
        public void NearbyObstacleRetractsWeaponWithoutMovingPlayer()
        {
            Vector3 position = _player.transform.position;
            Step(60);
            Assert.That(_hand.localPosition.z, Is.LessThan(_handRest.z - 0.1f));
            Assert.That(Quaternion.Angle(_hand.localRotation, Quaternion.identity), Is.GreaterThan(1f));
            Assert.That(_player.transform.position, Is.EqualTo(position));
            float retracted = _hand.localPosition.z;
            Step(60);
            Assert.That(_hand.localPosition.z, Is.EqualTo(retracted).Within(0.001f));
        }

        [Test]
        public void WeaponReturnsToRestWhenObstacleLeaves()
        {
            Step(60);
            _wall.SetActive(false);
            Physics.SyncTransforms();
            Step(60);
            Assert.That(Vector3.Distance(_hand.localPosition, _handRest), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(_hand.localRotation, Quaternion.identity), Is.LessThan(0.1f));
        }

        [Test]
        public void PickupsAndPlayerCollidersDoNotRetractWeapon()
        {
            _wall.GetComponent<BoxCollider>().isTrigger = true;
            var body = _player.AddComponent<BoxCollider>();
            body.center = Vector3.forward * 0.5f;
            body.size = new Vector3(0.2f, 0.2f, 0.2f);
            Physics.SyncTransforms();
            Step(60);
            Assert.That(_hand.localPosition, Is.EqualTo(_handRest));
        }

        void Step(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                _hand.SetLocalPositionAndRotation(_handRest, Quaternion.identity);
                _clearance.Tick(_held, 1f / 60f);
            }
        }
    }
}
