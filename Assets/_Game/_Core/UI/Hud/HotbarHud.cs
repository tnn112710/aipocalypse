using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Input;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // One slot view per slot of the active player's hotbar, refreshed on events (items, selection and ammo), never polled.
    public sealed class HotbarHud : PlayerHudView
    {
        [SerializeField] HotbarSlotView _slotPrefab;
        [SerializeField, Tooltip("Layout group the slot views are spawned into")] RectTransform _slotParent;

        InputService _input;
        Equipment _equipment;
        AmmoReserve _ammo;
        HotbarSlotView[] _slots;
        GunInstance[] _guns;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Bind(Player player)
        {
            _equipment = player.Equipment;
            if (_equipment == null)
                return;

            _ammo = player.Ammo;
            Hotbar hotbar = _equipment.Hotbar;
            _slots = new HotbarSlotView[hotbar.Count];
            _guns = new GunInstance[hotbar.Count];
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

        protected override void Unbind(Player player)
        {
            if (_equipment == null)
                return;

            _equipment.Hotbar.SlotChanged -= OnSlotChanged;
            _equipment.SelectionChanged -= OnSelectionChanged;
            if (_ammo != null)
                _ammo.Changed -= OnReserveChanged;
            foreach (GunInstance gun in _guns)
            {
                if (gun != null)
                    gun.Changed -= RefreshAmmo;
            }
            _equipment = null;
            _ammo = null;
            _guns = null;

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

        void OnReserveChanged(AmmoType _) => RefreshAmmo();

        void ShowItem(int index, ItemInstance item)
        {
            _slots[index].SetIcon(item?.Definition.Icon);

            if (_guns[index] != null)
                _guns[index].Changed -= RefreshAmmo;
            _guns[index] = item as GunInstance;
            if (_guns[index] != null)
                _guns[index].Changed += RefreshAmmo;
            RefreshAmmo(index);
        }

        // Few slots and rare changes (a shot, a reload): refresh them all.
        void RefreshAmmo()
        {
            for (int i = 0; i < _slots.Length; i++)
                RefreshAmmo(i);
        }

        void RefreshAmmo(int index)
        {
            GunInstance gun = _guns[index];
            if (gun == null)
            {
                _slots[index].HideAmmo();
                return;
            }

            AmmoType type = gun.Gun.Magazine.AmmoType;
            bool infinite = _ammo == null || _ammo.Infinite;
            int reserve = _ammo != null ? _ammo.Count(type) : 0;
            _slots[index].SetAmmo(gun.Rounds, reserve, infinite, gun.Rounds == 0 || gun.IsJammed);
        }
    }
}
