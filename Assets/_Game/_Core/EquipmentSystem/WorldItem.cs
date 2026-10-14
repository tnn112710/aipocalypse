using Aipocalypse.Core.Interaction;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // An item lying in the world. Interacting stores it in the actor's hotbar, or swaps it with the selected item,
    // which is dropped in its place. Root of an item's world prefab (ItemDefinition.WorldPrefab) with a trigger collider.
    [DisallowMultipleComponent]
    public sealed class WorldItem : MonoBehaviour, IInteractable
    {
        const string PickUpVerb = "Pick up";
        const string SwapVerb = "Swap for";

        [SerializeField, Tooltip("What a scene-placed copy holds. Dropped items carry their own instance instead")]
        ItemDefinition _definition;

        ItemInstance _item;
        bool _taken;

        // Created on first use for scene-placed items, so per-copy state (ammo later) starts fresh.
        public ItemInstance Item => _item ??= _definition != null ? _definition.CreateInstance() : null;

        public static WorldItem Spawn(ItemInstance item, Vector3 position, Quaternion rotation, Transform parent)
        {
            WorldItem prefab = item.Definition.WorldPrefab;
            if (prefab == null)
            {
                Debug.LogError($"{item.Definition.name} has no WorldPrefab; it can't be dropped.", item.Definition);
                return null;
            }

            WorldItem worldItem = Instantiate(prefab, position, rotation, parent);
            worldItem.name = prefab.name;
            worldItem._item = item;
            return worldItem;
        }

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_taken || Item == null || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null)
                return false;

            switch (owner.Equipment.GetPickUpAction(Item))
            {
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
