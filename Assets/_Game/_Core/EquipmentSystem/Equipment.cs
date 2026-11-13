using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Input;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // The player's Inventory plus what's in the hands: which hotbar slot is selected (each slot has a key) and keeping the
    // switcher's target in sync with it. What a key does comes from the item (ItemDefinition.Selecting): hold it, or ask for
    // a quick use. Picking up from the world goes through here so the HUD hears about it.
    public sealed class Equipment : IDisposable
    {
        readonly Func<ItemInstance, bool> _canSelect = CanHold;
        readonly HotbarKey[] _keys;
        readonly LocalizedString[] _slotNames;
        readonly LocalizedString[] _gearNames;

        // previous index, new index; -1 = nothing selected
        public event Action<int, int> SelectionChanged;
        // A quick-use item's key was pressed; whoever uses it calls Consume. The hands don't change.
        public event Action<ItemInstance> QuickUseRequested;
        // Taken from the world: which item and how many.
        public event Action<ItemDefinition, int> PickedUp;
        // Some or all of a world item didn't fit.
        public event Action<ItemDefinition> PickUpBlocked;

        // `keys`: the key of each hotbar slot (EquipmentConfig); missing ones default to keys 1, 2, 3… in order. Names label the
        // hotbar and gear slots on the inventory screen.
        public Equipment(Inventory inventory, IReadOnlyList<HotbarKey> keys = null, IReadOnlyList<LocalizedString> slotNames = null,
            IReadOnlyList<LocalizedString> gearNames = null)
        {
            Inventory = inventory;
            _keys = new HotbarKey[Hotbar.Count];
            for (int i = 0; i < _keys.Length; i++)
                _keys[i] = keys != null && i < keys.Count ? keys[i] : (HotbarKey)Math.Min(i, (int)HotbarKey.Key9);
            _slotNames = Names(slotNames, Hotbar.Count);
            _gearNames = Names(gearNames, inventory.Gear.Count);
            Switcher = new EquipmentSwitcher();
            Hotbar.SlotChanged += OnSlotChanged;
        }

        public Inventory Inventory { get; }
        public ItemContainer Hotbar => Inventory.Hotbar;
        public EquipmentSwitcher Switcher { get; }
        public int SelectedIndex { get; private set; } = -1;
        public ItemInstance Selected => SelectedIndex >= 0 ? Hotbar[SelectedIndex] : null;
        // Worn on the belt, not held: works whatever is in the hands (a lantern).
        public ItemInstance Belt => Inventory.FindGear(ItemCategory.Lantern);
        // Selection changes while lowered wait for RaiseHands.
        public bool HandsLowered { get; private set; }

        public void Dispose()
        {
            Hotbar.SlotChanged -= OnSlotChanged;
            Inventory.Dispose();
        }

        public HotbarKey KeyOf(int slot) => _keys[slot];
        public string SlotName(int slot) => _slotNames[slot].Text();
        public string GearName(int slot) => _gearNames[slot].Text();

        static LocalizedString[] Names(IReadOnlyList<LocalizedString> names, int count)
        {
            var result = new LocalizedString[count];
            for (int i = 0; i < count; i++)
                result[i] = names != null && i < names.Count ? names[i] : null;
            return result;
        }

        // The hotbar slot a key selects, or -1.
        public int SlotOf(HotbarKey key) => Array.IndexOf(_keys, key);

        public bool Select(int index)
        {
            if (index < 0 || index >= Hotbar.Count || index == SelectedIndex)
                return false;

            ItemInstance item = Hotbar[index];
            if (item == null)
                return false;
            if (item.Definition.Selecting == SelectBehaviour.QuickUse)
            {
                QuickUseRequested?.Invoke(item);
                return true;
            }
            if (!CanHold(item))
                return false;

            SetSelection(index);
            return true;
        }

        public bool Cycle(int direction)
        {
            int next = Hotbar.FindNext(SelectedIndex, direction, _canSelect);
            return next >= 0 && Select(next);
        }

        // Selects and holds the first selectable slot without the switch animation.
        public void EquipFirstImmediately()
        {
            int first = Hotbar.FindNext(-1, 1, _canSelect);
            if (first < 0)
                return;
            SetSelection(first);
            Switcher.EquipImmediately(Hotbar[first]);
        }

        // Loading a save: holds the slot at once, without the switch; the first holdable slot when that one can't be held.
        public void SelectImmediately(int index)
        {
            if (index < 0 || index >= Hotbar.Count || Hotbar[index] == null || !CanHold(Hotbar[index]))
            {
                EquipFirstImmediately();
                return;
            }
            SetSelection(index);
            Switcher.EquipImmediately(Hotbar[index]);
        }

        public void Tick(float deltaTime, float switchSpeed) => Switcher.Tick(deltaTime, switchSpeed);

        // Puts the held item away but keeps the slot selected (the player went down); RaiseHands brings it back.
        public void LowerHands()
        {
            HandsLowered = true;
            Switcher.SetTarget(null);
        }

        public void RaiseHands()
        {
            HandsLowered = false;
            ItemInstance item = Selected;
            Switcher.SetTarget(item != null && CanHold(item) ? item : null);
        }

        // Whether a world item can be offered for pickup (room or not: no room shows a warning on pickup).
        public bool CanPickUp(ItemInstance item) => item != null && !item.IsEmpty && !Inventory.Contains(item);

        // Takes as much of a world item as fits (Inventory.TryAdd); a weapon stored in a hotbar slot is switched to. `added`
        // = units taken. True when all of it was taken (the world copy goes); otherwise the item keeps what's left.
        public bool TryPickUp(ItemInstance item, out int added)
        {
            added = 0;
            if (!CanPickUp(item))
                return false;

            ItemDefinition definition = item.Definition;
            bool all = Inventory.TryAdd(item, out added);
            if (added > 0)
                PickedUp?.Invoke(definition, added);
            if (!all)
                PickUpBlocked?.Invoke(definition);

            int slot = Hotbar.IndexOf(item);
            if (slot >= 0 && CanHold(item))
                Select(slot);
            return all;
        }

        // Takes the item in the hands out of the hotbar: mid-switch that's the one being put away, not the newly selected one.
        // The caller puts it in the world.
        public ItemInstance DropHeld()
        {
            int index = Switcher.Held != null ? Hotbar.IndexOf(Switcher.Held) : -1;
            return index >= 0 ? Hotbar.Remove(index) : null;
        }

        // Uses up `amount` of a carried item; a stack that runs out leaves its slot.
        public bool Consume(ItemInstance item, int amount = 1) => item != null && item.Consume(amount);

        static bool CanHold(ItemInstance item) => item.Definition.Selecting == SelectBehaviour.Hold;

        void SetSelection(int index)
        {
            int previous = SelectedIndex;
            SelectedIndex = index;
            ItemInstance item = Selected;
            Switcher.SetTarget(!HandsLowered && item != null && CanHold(item) ? item : null);
            if (previous != index)
                SelectionChanged?.Invoke(previous, index);
        }

        void OnSlotChanged(int index, ItemInstance previous, ItemInstance current)
        {
            if (index == SelectedIndex)
            {
                // The selected item was moved to another hotbar slot: the selection follows it, the hands keep it.
                int moved = previous != null ? Hotbar.IndexOf(previous) : -1;
                if (moved >= 0 && moved != index)
                    SetSelection(moved);
                else if (current != null && CanHold(current))
                    Switcher.SetTarget(HandsLowered ? null : current);
                else
                    SetSelection(-1);
            }

            // An item that left the hotbar can't be holstered, so it leaves the hands at once.
            if (previous != null && previous == Switcher.Held && Hotbar.IndexOf(previous) < 0)
                Switcher.DropHeld();
        }
    }
}
