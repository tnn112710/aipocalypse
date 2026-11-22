using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // One slot view per slot of the active player's hotbar, refreshed on events (items, selection, badges and the ammo
    // reserve), never polled. Each item says what its badge shows (ItemInstance.Badge).
    // While the inventory screen is open, the screen drags items in and out of these slots (SetInteractive).
    public sealed class HotbarHud : PlayerHudView
    {
        [SerializeField] HotbarSlotView _slotPrefab;
        [SerializeField, Tooltip("Layout group the slot views are spawned into")] RectTransform _slotParent;
        [SerializeField, Tooltip("Off except while the inventory screen is open: then the slots take drags and drops")]
        GraphicRaycaster _raycaster;
        [SerializeField, Tooltip("Clear image over the whole row: a drop between two tiles lands here instead of on the inventory's backdrop (which throws it away)")]
        Graphic _hitArea;

        InputService _input;
        Equipment _equipment;
        AmmoReserve _ammo;
        HotbarSlotView[] _slots;
        ItemInstance[] _items;
        bool _interactive;

        // A new array whenever the player is bound again.
        public IReadOnlyList<ISlotView> Slots => _slots;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Bind(IPlayer player)
        {
            _equipment = player.Equipment;
            if (_equipment == null)
                return;

            _ammo = player.Ammo;
            ItemContainer hotbar = _equipment.Hotbar;
            _slots = new HotbarSlotView[hotbar.Count];
            _items = new ItemInstance[hotbar.Count];
            for (int i = 0; i < _slots.Length; i++)
            {
                HotbarSlotView slot = Instantiate(_slotPrefab, _slotParent, false);
                slot.name = $"Slot{i + 1}";
                slot.SetKey(_input.GetKeyLabel(_equipment.KeyOf(i)));
                slot.SetSelected(i == _equipment.SelectedIndex);
                slot.SetInteractive(_interactive);
                _slots[i] = slot;
                ShowItem(i, hotbar[i]);
            }

            hotbar.SlotChanged += OnSlotChanged;
            _equipment.SelectionChanged += OnSelectionChanged;
            if (_ammo != null)
                _ammo.Changed += OnReserveChanged;
        }

        protected override void Unbind(IPlayer player)
        {
            if (_equipment == null)
                return;

            _equipment.Hotbar.SlotChanged -= OnSlotChanged;
            _equipment.SelectionChanged -= OnSelectionChanged;
            if (_ammo != null)
                _ammo.Changed -= OnReserveChanged;
            foreach (ItemInstance item in _items)
            {
                if (item != null)
                    item.Changed -= RefreshBadges;
            }
            _equipment = null;
            _ammo = null;
            _items = null;

            foreach (HotbarSlotView slot in _slots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _slots = null;
        }

        // While the inventory screen is open: drawn and hit-tested above it (its backdrop covers the screen), and its tiles
        // take drags. `sortingOrder` puts the row above the screen's canvas.
        public void SetInteractive(bool interactive, int sortingOrder)
        {
            _interactive = interactive;
            if (TryGetComponent(out Canvas canvas))
            {
                canvas.overrideSorting = interactive;
                if (interactive)
                    canvas.sortingOrder = sortingOrder;
            }
            if (_raycaster != null)
                _raycaster.enabled = interactive;
            if (_hitArea != null)
                _hitArea.raycastTarget = interactive;
            if (_slots == null)
                return;
            foreach (HotbarSlotView slot in _slots)
                slot.SetInteractive(interactive);
        }

        void OnSlotChanged(int index, ItemInstance previous, ItemInstance current) => ShowItem(index, current);

        void OnSelectionChanged(int previous, int current)
        {
            if (previous >= 0)
                _slots[previous].SetSelected(false);
            if (current >= 0)
                _slots[current].SetSelected(true);
        }

        void OnReserveChanged(AmmoType _) => RefreshBadges();

        void ShowItem(int index, ItemInstance item)
        {
            _slots[index].SetIcon(item?.Definition.Icon);

            if (_items[index] != null)
                _items[index].Changed -= RefreshBadges;
            _items[index] = item;
            if (item != null)
                item.Changed += RefreshBadges;
            RefreshBadge(index);
        }

        // Few slots and rare changes (a shot, a reload): refresh them all.
        void RefreshBadges()
        {
            for (int i = 0; i < _slots.Length; i++)
                RefreshBadge(i);
        }

        void RefreshBadge(int index)
        {
            ItemBadge badge = _items[index]?.Badge ?? ItemBadge.None;
            if (!badge.Visible)
            {
                _slots[index].HideBadge();
                return;
            }

            // A reserve is shown only for ammo the player can run out of.
            bool showReserve = badge.Reserve is AmmoType && _ammo != null && !_ammo.Infinite;
            int reserve = showReserve ? _ammo.Count((AmmoType)badge.Reserve) : 0;
            _slots[index].SetBadge(badge.Count, reserve, showReserve, badge.Warning);
        }
    }
}
