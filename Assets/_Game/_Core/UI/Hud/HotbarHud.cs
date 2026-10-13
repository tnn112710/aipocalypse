using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Input;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // One slot view per slot of the active player's hotbar, refreshed on events (items and selection change rarely), never polled.
    public sealed class HotbarHud : PlayerHudView
    {
        [SerializeField] HotbarSlotView _slotPrefab;
        [SerializeField, Tooltip("Layout group the slot views are spawned into")] RectTransform _slotParent;

        InputService _input;
        Equipment _equipment;
        HotbarSlotView[] _slots;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Bind(Player player)
        {
            _equipment = player.Equipment;
            if (_equipment == null)
                return;

            Hotbar hotbar = _equipment.Hotbar;
            _slots = new HotbarSlotView[hotbar.Count];
            for (int i = 0; i < _slots.Length; i++)
            {
                HotbarSlotView slot = Instantiate(_slotPrefab, _slotParent, false);
                slot.name = $"Slot{i + 1}";
                slot.SetKey(_input.GetSlotKeyLabel(i));
                slot.SetIcon(hotbar[i]?.Definition.Icon);
                slot.SetSelected(i == _equipment.SelectedIndex);
                _slots[i] = slot;
            }

            hotbar.SlotChanged += OnSlotChanged;
            _equipment.SelectionChanged += OnSelectionChanged;
        }

        protected override void Unbind(Player player)
        {
            if (_equipment == null)
                return;

            _equipment.Hotbar.SlotChanged -= OnSlotChanged;
            _equipment.SelectionChanged -= OnSelectionChanged;
            _equipment = null;

            foreach (HotbarSlotView slot in _slots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _slots = null;
        }

        void OnSlotChanged(int index, ItemInstance previous, ItemInstance current) =>
            _slots[index].SetIcon(current?.Definition.Icon);

        void OnSelectionChanged(int previous, int current)
        {
            if (previous >= 0)
                _slots[previous].SetSelected(false);
            if (current >= 0)
                _slots[current].SetSelected(true);
        }
    }
}
