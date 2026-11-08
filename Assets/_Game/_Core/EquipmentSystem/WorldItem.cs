using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // An item lying in the world, root of its prefab (ItemDefinition.Prefab) with a trigger collider; interacting stores or
    // swaps it. Equippables share the prefab with their HeldItem; EquipmentRig strips the world parts off the held copy.
    [DisallowMultipleComponent]
    public sealed class WorldItem : MonoBehaviour, IInteractable
    {
        const string PickUpVerb = "Pick up";
        const string SwapVerb = "Swap for";

        [SerializeField, Tooltip("What a scene-placed copy holds. Dropped items carry their own instance instead")]
        ItemDefinition _definition;

        [SerializeField, Tooltip("Child shown only in the world (the model lying on the ground). Every other child is " +
            "the first-person model and is hidden in the world. Empty = all children show")]
        GameObject _worldModel;

        ItemInstance _item;
        bool _taken;

        // Created on first use for scene-placed items, so per-copy state (ammo later) starts fresh.
        public ItemInstance Item => _item ??= _definition != null ? _definition.CreateInstance() : null;

        public static WorldItem Spawn(ItemInstance item, Vector3 position, Quaternion rotation, Transform parent)
        {
            WorldItem prefab = item.Definition.Prefab;
            if (prefab == null)
            {
                Debug.LogError($"{item.Definition.name} has no Prefab; it can't be dropped.", item.Definition);
                return null;
            }

            WorldItem worldItem = Instantiate(prefab, position, rotation, parent);
            worldItem.name = prefab.name;
            worldItem._item = item;
            return worldItem;
        }

        void Awake()
        {
            if (_worldModel == null)
                return;
            foreach (Transform child in transform)
                child.gameObject.SetActive(child.gameObject == _worldModel);
        }

        // Turns a fresh copy that hasn't woken up yet into a first-person model: removes the world model,
        // the pickup colliders and this component.
        internal void StripForHand()
        {
            if (_worldModel != null)
                DestroyImmediate(_worldModel);
            foreach (Collider pickupCollider in GetComponents<Collider>())
                DestroyImmediate(pickupCollider);
            DestroyImmediate(this);
        }

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_taken || Item == null || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null)
                return false;

            switch (owner.Equipment.GetPickUpAction(Item))
            {
                case PickUpAction.Merge:
                case PickUpAction.Store:
                    prompt = new InteractionPrompt(PickUpVerb, Item.Definition.DisplayName);
                    return true;
                case PickUpAction.Swap:
                    prompt = new InteractionPrompt(SwapVerb, Item.Definition.DisplayName);
                    return true;
                default:
                    return false;
            }
        }

        public void Interact(GameObject actor)
        {
            if (_taken || Item == null || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null)
                return;
            if (!owner.Equipment.TryPickUp(Item, out ItemInstance dropped))
                return;

            _taken = true;
            if (dropped != null)
                Spawn(dropped, transform.position, transform.rotation, transform.parent);
            Destroy(gameObject);
        }
    }
}
