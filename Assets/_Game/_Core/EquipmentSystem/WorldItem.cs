using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // An item lying in the world, root of its prefab (ItemDefinition.Prefab) with a trigger collider; interacting picks up
    // as much as fits (Equipment.TryPickUp). Equippables share the prefab with their HeldItem; EquipmentRig strips the world
    // parts off the held copy. Thrown items fly with a temporary rigidbody until they come to rest.
    [DisallowMultipleComponent]
    public sealed class WorldItem : MonoBehaviour, IInteractable
    {
        const string PickUpVerb = "Pick up";
        // The layer a thrown item's solid collider flies on: Item collides with Default (terrain, buildings); Debris and
        // Ignore Raycast don't, so items there fall through the ground.
        const string SolidLayerName = "Item";
        // Seconds after which a thrown item stops simulating even if it's still rolling.
        const float MaxFlightTime = 6f;

        [SerializeField, Tooltip("What a scene-placed copy holds. Dropped items carry their own instance instead")]
        ItemDefinition _definition;

        [SerializeField, Min(1), Tooltip("How many a scene-placed copy holds (a pile of tokens, a box of rounds); capped at the item's stack size")]
        int _count = 1;

        [SerializeField, Tooltip("Child shown only in the world (the model lying on the ground). Every other child is " +
            "the first-person model and is hidden in the world. Empty = all children show")]
        GameObject _worldModel;

        ItemInstance _item;
        bool _taken;
        string _promptTarget;
        int _promptCount = -1;
        Rigidbody _body;
        GameObject _solid;
        float _flightEnd;

        // Created on first use for scene-placed items, so per-copy state (a gun's rounds) starts fresh.
        public ItemInstance Item => _item ??= _definition != null ? _definition.CreateInstance(_count) : null;
        public bool IsTaken => _taken;
        // Thrown and still in the air.
        public bool IsFlying => _body != null;
        // Set on items the player dropped: the pickup magnet ignores them until the player has walked away once.
        public bool WaitsForPlayerToLeave { get; set; }

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
            // Update runs only while thrown.
            enabled = false;
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

        // Flies off at `velocity`, upright, until it lands, on a solid collider that ignores `thrower` (the player's capsule).
        public void Throw(Vector3 velocity, Collider thrower = null)
        {
            if (_body == null)
            {
                _solid = new GameObject("Solid") { layer = LayerMask.NameToLayer(SolidLayerName) };
                _solid.transform.SetParent(transform, false);
                var solid = _solid.AddComponent<BoxCollider>();
                if (TryGetComponent(out BoxCollider pickupBox))
                {
                    solid.center = pickupBox.center;
                    solid.size = pickupBox.size * 0.8f;
                }
                if (thrower != null)
                    Physics.IgnoreCollision(solid, thrower);
                _body = gameObject.AddComponent<Rigidbody>();
                _body.constraints = RigidbodyConstraints.FreezeRotation;
                _body.interpolation = RigidbodyInterpolation.Interpolate;
                _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            _body.linearVelocity = velocity;
            _flightEnd = Time.time + MaxFlightTime;
            enabled = true;
        }

        // While thrown, it also passes through `other` (the animals a burst of Tokens flies over).
        public void PassThrough(Collider other)
        {
            if (_solid != null && other != null)
                Physics.IgnoreCollision(_solid.GetComponent<Collider>(), other);
        }

        // Stops simulating (it landed, or the magnet takes it over).
        public void Settle()
        {
            if (_body != null)
                Destroy(_body);
            if (_solid != null)
                Destroy(_solid);
            _body = null;
            _solid = null;
            enabled = false;
        }

        void Update()
        {
            if (_body == null || (Time.time < _flightEnd && !_body.IsSleeping()))
                return;
            Settle();
        }

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_taken || Item == null || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null
                || !owner.Equipment.CanPickUp(Item))
                return false;
            prompt = new InteractionPrompt(PickUpVerb, PromptTarget());
            return true;
        }

        public void Interact(GameObject actor) => Collect(actor);

        // Picks up as much as fits; the rest stays here. True when all of it was taken (this object is destroyed).
        public bool Collect(GameObject actor)
        {
            if (_taken || Item == null || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null)
                return false;
            if (!owner.Equipment.TryPickUp(Item, out _))
                return false;

            _taken = true;
            Destroy(gameObject);
            return true;
        }

        // "Rifle Ammo (30)": rebuilt only when the count changes, not every frame.
        string PromptTarget()
        {
            int count = Item.Definition.Stacks ? Item.Count : 1;
            if (count == _promptCount)
                return _promptTarget;
            _promptCount = count;
            _promptTarget = count > 1 ? $"{Item.Definition.DisplayName} ({count})" : Item.Definition.DisplayName;
            return _promptTarget;
        }
    }
}
