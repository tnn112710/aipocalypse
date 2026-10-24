using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Input;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // One slot view per slot of the active player's hotbar, refreshed on events (items, selection, badges and the ammo
    // reserve), never polled. Each item says what its badge shows (ItemInstance.Badge).
    public sealed class HotbarHud : PlayerHudView
    {
        [SerializeField] HotbarSlotView _slotPrefab;
        [SerializeField, Tooltip("Layout group the slot views are spawned into")] RectTransform _slotParent;

        InputService _input;
        Equipment _equipment;
        AmmoReserve _ammo;
        HotbarSlotView[] _slots;
        ItemInstance[] _items;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Bind(IPlayer player)
        {
            _equipment = player.Equipment;
            if (_equipment == null)
                return;

            _ammo = player.Ammo;
            Hotbar hotbar = _equipment.Hotbar;
            _slots = new HotbarSlotView[hotbar.Count];
            _items = new ItemInstance[hotbar.Count];
            for (int i = 0; i < _slots.Length; i++)
            {
                HotbarSlotView slot = Instantiate(_slotPrefab, _slotParent, false);
                slot.name = $"Slot{i + 1}";
                slot.SetKey(_input.GetSlotKeyLabel(i));
                slot.SetSelected(i == _equipment.SelectedIndex);
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
