using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
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
        HeldItem _current;
        Vector3 _restPosition;
        Quaternion _restRotation;

        public void Bind(Equipment equipment)
        {
            _equipment = equipment;
            _restPosition = _hand.localPosition;
            _restRotation = _hand.localRotation;

            Hotbar hotbar = equipment.Hotbar;
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
                Debug.LogError($"{item.Definition.name} has no HeldPrefab.", item.Definition);
                return null;
            }

            view = Instantiate(prefab, _hand, false);
            view.name = prefab.name;
            view.Bind(item);
            view.gameObject.SetActive(false);
            _views.Add(item, view);
            return view;
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
