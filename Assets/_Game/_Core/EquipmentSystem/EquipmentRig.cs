using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // First-person hands: one HeldItem per hotbar item (spawned when it enters the hotbar), only the held one visible,
    // posed from the switcher's Raise. Ticked by Player after movement.
    public sealed class EquipmentRig : MonoBehaviour
    {
        [SerializeField, Tooltip("Parent of the held models, at hand position under the camera pitch")] Transform _hand;

        // Temporary switch animation: the hand drops and tilts out of view, then comes back up.
        // Replace with arm/item Animator clips later; the switcher's timing and Raise stay the same.
        [Header("Temporary switch animation")]
        [SerializeField, Tooltip("Hand offset when fully holstered")] Vector3 _loweredOffset = new(0.05f, -0.4f, -0.1f);
        [SerializeField, Tooltip("Hand tilt when fully holstered")] Vector3 _loweredEuler = new(45f, -15f, 20f);
        [SerializeField, Tooltip("Raise 0..1 → pose 0..1")] AnimationCurve _raiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        readonly Dictionary<ItemInstance, HeldItem> _views = new();
        Equipment _equipment;
        IEquipmentOwner _owner;
        IObjectResolver _resolver;
        HeldItem _current;
        Transform _staging;
        Vector3 _restPosition;
        Quaternion _restRotation;

        // The item model in hand (raising, ready or holstering), or null.
        public HeldItem Current => _current;

        // The resolver injects the held models (guns need the bullet and effect services).
        public void Bind(Equipment equipment, IEquipmentOwner owner, IObjectResolver resolver)
        {
            _equipment = equipment;
            _owner = owner;
            _resolver = resolver;
            _restPosition = _hand.localPosition;
            _restRotation = _hand.localRotation;

            ItemContainer hotbar = equipment.Hotbar;
            for (int i = 0; i < hotbar.Count; i++)
                Spawn(hotbar[i]);

            hotbar.SlotChanged += OnSlotChanged;
            equipment.Switcher.HeldChanged += OnHeldChanged;
            equipment.Switcher.BecameReady += OnBecameReady;

            OnHeldChanged(null, equipment.Switcher.Held);
            if (equipment.Switcher.IsReady)
                _current?.OnReady();
            Tick();
        }

        public void Tick()
        {
            if (_equipment == null)
                return;

            float pose = _raiseCurve.Evaluate(_equipment.Switcher.Raise);
            _hand.localPosition = _restPosition + Vector3.LerpUnclamped(_loweredOffset, Vector3.zero, pose);
            _hand.localRotation = _restRotation * Quaternion.SlerpUnclamped(Quaternion.Euler(_loweredEuler), Quaternion.identity, pose);
        }

        // Lets the item in hand act (fire, aim, reload). After Tick, so it sees this frame's switch state.
        public void TickHeld(HeldItemInput input, float deltaTime)
        {
            if (_current == null)
                return;

            EquipmentSwitcher switcher = _equipment.Switcher;
            input.IsReady = switcher.IsReady && switcher.Held == _current.Item;
            _current.Tick(input, deltaTime);
        }

        // Sway and bob on top of this frame's hand pose: offset in the camera's space, rotation around the hand.
        public void ApplyHandMotion(Vector3 offset, Quaternion rotation)
        {
            _hand.localPosition += offset;
            _hand.localRotation *= rotation;
        }

        // Turns the whole rig (it sits at the eye) so the held model points where the aim sway does.
        public void SetAimRotation(Quaternion rotation) => transform.localRotation = rotation;

        public void ApplyObstaclePose(float weight, float distance, Vector3 euler)
        {
            _hand.localPosition += Vector3.back * (weight * distance);
            _hand.localRotation *= Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(euler), weight);
        }

        void OnDestroy()
        {
            if (_equipment == null)
                return;
            _equipment.Hotbar.SlotChanged -= OnSlotChanged;
            _equipment.Switcher.HeldChanged -= OnHeldChanged;
            _equipment.Switcher.BecameReady -= OnBecameReady;
        }

        void OnHeldChanged(ItemInstance previous, ItemInstance current)
        {
            if (_current != null)
                _current.Hide();
            if (previous != null && _equipment.Hotbar.IndexOf(previous) < 0)
                Despawn(previous);

            _current = current != null ? Spawn(current) : null;
            if (_current != null)
                _current.Show();
        }

        void OnBecameReady(ItemInstance item)
        {
            if (_current != null && _current.Item == item)
                _current.OnReady();
        }

        // Models are spawned when items enter the hotbar, never at switch time.
        void OnSlotChanged(int index, ItemInstance previous, ItemInstance current)
        {
            Spawn(current);
            if (previous != null && previous != _equipment.Switcher.Held && _equipment.Hotbar.IndexOf(previous) < 0)
                Despawn(previous);
        }

        HeldItem Spawn(ItemInstance item)
        {
            if (item == null || !item.IsEquippable)
                return null;
            if (_views.TryGetValue(item, out HeldItem view))
                return view;

            HeldItem prefab = item.Equippable.HeldPrefab;
            if (prefab == null)
            {
                Debug.LogError($"{item.Definition.name}: its Prefab has no HeldItem on the root.", item.Definition);
                return null;
            }

            // Built under an inactive parent so the shared prefab's world parts are stripped before anything wakes up.
            view = Instantiate(prefab, Staging(), false);
            view.name = prefab.name;
            _resolver?.InjectGameObject(view.gameObject);
            if (view.TryGetComponent(out WorldItem worldItem))
                worldItem.StripForHand();
            view.transform.SetParent(_hand, false);
            view.transform.SetLayerRecursively(_hand.gameObject.layer);
            view.Bind(item, _owner);
            view.gameObject.SetActive(false);
            _views.Add(item, view);
            return view;
        }

        Transform Staging()
        {
            if (_staging != null)
                return _staging;
            var staging = new GameObject("Staging");
            staging.SetActive(false);
            _staging = staging.transform;
            _staging.SetParent(transform, false);
            return _staging;
        }

        void Despawn(ItemInstance item)
        {
            if (!_views.Remove(item, out HeldItem view))
                return;
            if (view == _current)
                _current = null;
            Destroy(view.gameObject);
        }
    }
}
