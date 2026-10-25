using System;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Which hotbar slot is selected, and keeping the switcher's target in sync with it. Plus the belt: one worn item.
    // What a slot key does comes from the item (ItemDefinition.Selecting): hold it, or ask for a quick use.
    public sealed class Equipment : IDisposable
    {
        readonly Func<ItemInstance, bool> _canSelect = CanHold;
        readonly ItemCategory _beltAccepts;

        // previous index, new index; -1 = nothing selected
        public event Action<int, int> SelectionChanged;
        // A quick-use item's key was pressed; whoever uses it calls Consume. The hands don't change.
        public event Action<ItemInstance> QuickUseRequested;

        // `beltAccepts`: categories worn on the belt instead of the hotbar (EquipmentConfig.BeltAccepts).
        public Equipment(Hotbar hotbar, ItemCategory beltAccepts = ItemCategory.Lantern)
        {
            Hotbar = hotbar;
            _beltAccepts = beltAccepts;
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

        // Merge onto a carried stack if it all fits, else Store in the first free accepting slot (switching to it), else Swap
        // with the selected item. Belt items go on an empty belt or swap with the worn one, without changing the hands.
        public PickUpAction GetPickUpAction(ItemInstance item)
        {
            if (item == null || item.IsEmpty || item == Belt || Hotbar.IndexOf(item) >= 0)
                return PickUpAction.None;
            if (GoesOnBelt(item))
                return Belt == null ? PickUpAction.Store : PickUpAction.Swap;
            if (FindStack(item) >= 0)
                return PickUpAction.Merge;
            if (Hotbar.FindFreeSlot(item) >= 0)
                return PickUpAction.Store;
            if (SelectedIndex >= 0 && Hotbar.CanPlace(SelectedIndex, item))
                return PickUpAction.Swap;
            return PickUpAction.None;
        }

        // Takes the item in the hands out of the hotbar: mid-switch that's the one being put away, not the newly selected one.
        // The caller puts it in the world.
        public ItemInstance DropHeld()
        {
            int index = Switcher.Held != null ? Hotbar.IndexOf(Switcher.Held) : -1;
            return index >= 0 ? Hotbar.Remove(index) : null;
        }

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
                case PickUpAction.Merge:
                    Hotbar[FindStack(item)].Merge(item);
                    return true;

                case PickUpAction.Store:
                    int slot = Hotbar.FindFreeSlot(item);
                    Hotbar.TryPlace(slot, item);
                    // Only a held item is switched to; selecting a quick-use item would use it on pickup.
                    if (CanHold(item))
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

        // Uses up `amount` of a carried item; a stack that runs out leaves the hotbar (or the belt).
        public bool Consume(ItemInstance item, int amount = 1)
        {
            if (item == null || !item.Consume(amount))
                return false;
            if (!item.IsEmpty)
                return true;
            int index = Hotbar.IndexOf(item);
            if (index >= 0)
                Hotbar.Remove(index);
            else if (item == Belt)
                Belt = null;
            return true;
        }

        static bool CanHold(ItemInstance item) => item.Definition.Selecting == SelectBehaviour.Hold;

        bool GoesOnBelt(ItemInstance item) => (item.Definition.Category & _beltAccepts) != 0;

        int FindStack(ItemInstance item)
        {
            for (int i = 0; i < Hotbar.Count; i++)
            {
                if (Hotbar[i] != null && Hotbar[i].CanMerge(item))
                    return i;
            }
            return -1;
        }

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
            // The selected slot's item was swapped or removed: hold the new one, or nothing.
            if (index == SelectedIndex)
            {
                if (current != null && CanHold(current))
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
