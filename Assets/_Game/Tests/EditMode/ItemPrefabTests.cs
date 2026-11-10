using System.Reflection;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class ItemPrefabTests
    {
        const string PrefabField = "_prefab";

        GameObject _prefab;
        GameObject _worldModel;
        GameObject _heldModel;
        GunDefinition _definition;
        GameObject _rigObject;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Item");
            _prefab.AddComponent<HeldItem>();
            _prefab.AddComponent<BoxCollider>().isTrigger = true;
            _heldModel = new GameObject("PosePivot");
            _heldModel.transform.SetParent(_prefab.transform);
            _worldModel = new GameObject("World");
            _worldModel.transform.SetParent(_prefab.transform);
            _worldModel.SetActive(false);
            var worldItem = _prefab.AddComponent<WorldItem>();
            var so = new SerializedObject(worldItem);
            so.FindProperty("_worldModel").objectReferenceValue = _worldModel;
            so.ApplyModifiedPropertiesWithoutUndo();

            _definition = ScriptableObject.CreateInstance<GunDefinition>();
            var definitionSo = new SerializedObject(_definition);
            definitionSo.FindProperty(PrefabField).objectReferenceValue = worldItem;
            definitionSo.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rigObject);
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void EveryItem_HasOnePrefabForWorldAndHand()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game/Content" });
            Assert.That(guids, Is.Not.Empty);
            foreach (string guid in guids)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                WorldItem prefab = item.Prefab;
                Assert.IsNotNull(prefab, $"{item.name}: no Prefab");
                Assert.AreSame(item, new SerializedObject(prefab).FindProperty("_definition").objectReferenceValue,
                    $"{item.name}: its prefab's WorldItem holds another item");
                Assert.IsTrue(prefab.TryGetComponent(out Collider pickupBox) && pickupBox.isTrigger,
                    $"{item.name}: needs a trigger collider on the prefab root");

                if (item is not EquippableDefinition equippable)
                    continue;
                Assert.IsNotNull(equippable.HeldPrefab, $"{item.name}: no HeldItem on the prefab root");
                if (item is ConsumableDefinition)
                    Assert.IsInstanceOf<HeldConsumable>(equippable.HeldPrefab, $"{item.name}: a consumable is used through a HeldConsumable");
                var worldModel = (GameObject)new SerializedObject(prefab).FindProperty("_worldModel").objectReferenceValue;
                Assert.IsNotNull(worldModel, $"{item.name}: no world model");
                Assert.AreSame(prefab.transform, worldModel.transform.parent, $"{item.name}: world model must be a root child");
            }
        }

        [Test]
        public void InTheWorld_OnlyTheWorldModelShows()
        {
            WorldItem worldItem = Object.Instantiate(_prefab).GetComponent<WorldItem>();
            _rigObject = worldItem.gameObject;
            // EditMode doesn't call Awake; play does, for spawned and scene-placed items.
            typeof(WorldItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(worldItem, null);

            Assert.IsTrue(worldItem.transform.Find("World").gameObject.activeSelf);
            Assert.IsFalse(worldItem.transform.Find("PosePivot").gameObject.activeSelf);
            Assert.IsNotNull(worldItem.GetComponent<Collider>());
        }

        [Test]
        public void InHand_TheCopyLosesItsWorldParts()
        {
            _rigObject = new GameObject("Rig");
            var hand = new GameObject("Hand").transform;
            hand.SetParent(_rigObject.transform);
            var rig = _rigObject.AddComponent<EquipmentRig>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("_hand").objectReferenceValue = hand;
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            var inventory = new Inventory(new ItemContainer(new[] { ItemCategory.Gun }), new ItemContainer(0), 0, 0f);
            ItemInstance gun = _definition.CreateInstance();
            inventory.Hotbar.TryPlace(0, gun);
            var equipment = new Equipment(inventory);
            equipment.EquipFirstImmediately();
            rig.Bind(equipment, null, null);

            HeldItem view = rig.Current;
            Assert.IsNotNull(view);
            Assert.AreSame(hand, view.transform.parent);
            Assert.AreSame(gun, view.Item);
            Assert.IsNull(view.GetComponent<WorldItem>());
            Assert.IsEmpty(view.GetComponentsInChildren<Collider>(true));
            Assert.IsNull(view.transform.Find("World"));
            Assert.IsNotNull(view.transform.Find("PosePivot"));
        }
    }
}
