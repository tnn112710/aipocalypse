using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // One item in a save: its catalog id, the count, and per-copy state written by its ItemInstance (WriteSave / ReadSave).
    [Serializable]
    public sealed class ItemSave
    {
        public string Id;
        public int Count = 1;
        public int Rounds;
        public bool Jammed;
        public int Uses;
        public bool Locked;
        // Catalog ids of the items fitted to it (a gun's parts).
        public List<string> Attachments;
    }

    // An item in a container slot.
    [Serializable]
    public sealed class SlotSave
    {
        public int Slot;
        public ItemSave Item;
    }

    // Items and containers to and from their saved form.
    public static class ItemSaves
    {
        // Null (with a warning) for an item the catalog doesn't know: it can't be saved.
        public static ItemSave Write(ItemInstance item, ItemCatalog catalog)
        {
            if (item == null || item.IsEmpty)
                return null;
            string id = catalog.IdOf(item.Definition);
            if (id == null)
            {
                Debug.LogError($"{item.Definition.name} is not in the Item Catalog and was not saved: select Data/Equipment/ItemCatalog and press Collect.", item.Definition);
                return null;
            }
            var save = new ItemSave { Id = id, Count = item.Count, Locked = item.Locked };
            item.WriteSave(save);
            if (item.Attachments != null)
            {
                foreach (ItemInstance attachment in item.Attachments)
                {
                    string attachmentId = attachment != null ? catalog.IdOf(attachment.Definition) : null;
                    if (attachmentId != null)
                        (save.Attachments ??= new List<string>()).Add(attachmentId);
                }
            }
            return save;
        }

        // Null (with a warning) for an id the catalog doesn't know (an item removed since the save).
        public static ItemInstance Read(ItemSave save, ItemCatalog catalog)
        {
            if (save == null)
                return null;
            ItemDefinition definition = catalog.Find(save.Id);
            if (definition == null)
            {
                Debug.LogWarning($"A saved item (id {save.Id}) no longer exists; it was dropped from the save.");
                return null;
            }
            ItemInstance item = definition.CreateInstance(Math.Max(1, save.Count));
            if (save.Attachments != null)
            {
                foreach (string attachmentId in save.Attachments)
                {
                    ItemDefinition attachment = catalog.Find(attachmentId);
                    if (attachment != null)
                        item.RestoreAttachment(attachment.CreateInstance());
                }
            }
            item.ReadSave(save);
            item.Locked = save.Locked;
            return item;
        }

        public static List<SlotSave> WriteSlots(ItemContainer container, ItemCatalog catalog)
        {
            var slots = new List<SlotSave>();
            for (int i = 0; i < container.Count; i++)
            {
                ItemSave item = Write(container[i], catalog);
                if (item != null)
                    slots.Add(new SlotSave { Slot = i, Item = item });
            }
            return slots;
        }

        // Empties the container and puts the saved items back in their slots; one that doesn't fit its slot (the container
        // shrank) goes in any free one. Returns the items that found no room.
        public static List<ItemInstance> ReadSlots(ItemContainer container, IReadOnlyList<SlotSave> slots, ItemCatalog catalog)
        {
            var leftOver = new List<ItemInstance>();
            for (int i = 0; i < container.Count; i++)
                container.Remove(i);
            if (slots == null)
                return leftOver;
            foreach (SlotSave slot in slots)
            {
                ItemInstance item = Read(slot?.Item, catalog);
                if (item == null)
                    continue;
                if (container.CanPlace(slot.Slot, item) && container[slot.Slot] == null)
                    container.TryPlace(slot.Slot, item);
                else if (!container.TryAdd(item, out _))
                    leftOver.Add(item);
            }
            return leftOver;
        }
    }
}
