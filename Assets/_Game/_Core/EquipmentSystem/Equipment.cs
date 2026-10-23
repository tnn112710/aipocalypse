using System;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Which hotbar slot is selected, and keeping the switcher's target in sync with it. Plus the belt: one worn item.
    public sealed class Equipment : IDisposable
    {
        const ItemCategory BeltAccepts = ItemCategory.Lantern;

        readonly Func<ItemInstance, bool> _canSelect = CanSelect;

        // previous index, new index; -1 = nothing selected
        public event Action<int, int> SelectionChanged;

        public Equipment(Hotbar hotbar)
        {
            Hotbar = hotbar;
            Switcher = new EquipmentSwitcher();
            Hotbar.SlotChanged += OnSlotChanged;
        }

        public Hotbar Hotbar { get; }
        public EquipmentSwitcher Switcher { get; }
        public int SelectedIndex { get; private set; } = -1;
        public ItemInstance Selected => SelectedIndex >= 0 ? Hotbar[SelectedIndex] : null;
        // Worn, not held: works whatever is in the hands (a lantern).
        public ItemInstance Belt { get; private set; }
        // Selection changes while lowered wait for RaiseHands.
        public bool HandsLowered { get; private set; }

        public void Dispose() => Hotbar.SlotChanged -= OnSlotChanged;

        public bool Select(int index)
        {
            if (index < 0 || index >= Hotbar.Count || index == SelectedIndex)
                return false;

            ItemInstance item = Hotbar[index];
            if (item == null || !CanSelect(item))
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
            Switcher.SetTarget(item != null && CanSelect(item) ? item : null);
        }

        // Store: goes into the first free slot that accepts it, and is switched to. Swap: no free slot, it replaces the selected item.
        // Belt items are stored on an empty belt or swapped with the worn one; the hands don't change.
        public PickUpAction GetPickUpAction(ItemInstance item)
        {
            if (item == null || item == Belt || Hotbar.IndexOf(item) >= 0)
                return PickUpAction.None;
            if (GoesOnBelt(item))
                return Belt == null ? PickUpAction.Store : PickUpAction.Swap;
            if (Hotbar.FindFreeSlot(item) >= 0)
                return PickUpAction.Store;
            if (SelectedIndex >= 0 && Hotbar.CanPlace(SelectedIndex, item))
                return PickUpAction.Swap;
            return PickUpAction.None;
        }

        // Takes the selected item out of the hotbar and empties the hands. The caller puts it in the world.
        public ItemInstance DropSelected() => SelectedIndex >= 0 ? Hotbar.Remove(SelectedIndex) : null;

        // `dropped` is the item a swap pushed out of the hotbar; the caller puts it in the world.
        public bool TryPickUp(ItemInstance item, out ItemInstance dropped)
        {
            dropped = null;
            PickUpAction action = GetPickUpAction(item);
            if (action != PickUpAction.None && GoesOnBelt(item))
            {
                dropped = Belt;
                Belt = item;
                return true;
            }

            switch (action)
            {
                case PickUpAction.Store:
                    int slot = Hotbar.FindFreeSlot(item);
                    Hotbar.TryPlace(slot, item);
                    Select(slot);
                    return true;

                case PickUpAction.Swap:
                    dropped = Hotbar[SelectedIndex];
                    Hotbar.TryPlace(SelectedIndex, item);
                    return true;

                default:
                    return false;
            }
        }

        // Selecting a slot holds its item. Items that do something else on select (quick-use consumables) go here.
        static bool CanSelect(ItemInstance item) => item.IsEquippable;

        static bool GoesOnBelt(ItemInstance item) => (item.Definition.Category & BeltAccepts) != 0;

        void SetSelection(int index)
        {
            int previous = SelectedIndex;
            SelectedIndex = index;
            ItemInstance item = Selected;
            Switcher.SetTarget(!HandsLowered && item != null && CanSelect(item) ? item : null);
            if (previous != index)
                SelectionChanged?.Invoke(previous, index);
        }

        void OnSlotChanged(int index, ItemInstance previous, ItemInstance current)
        {
            // The selected slot's item was swapped or removed: hold the new one, or nothing.
            if (index == SelectedIndex)
            {
                if (current != null && CanSelect(current))
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
