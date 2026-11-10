using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Saves what's in each scene-placed container (a crate with a SaveId) and which of its slots are still unsearched.
    // Bodies have no SaveId and aren't saved; a crate added to the scene after the save keeps its authored contents.
    public sealed class LootSaveSection : SaveSection
    {
        const string Key = "loot";

        readonly LootRegistry _registry;
        readonly ItemCatalog _catalog;

        public LootSaveSection(SaveRegistry registry, LootRegistry loot, ItemCatalog catalog) : base(registry)
        {
            _registry = loot;
            _catalog = catalog;
        }

        public override int Order => SaveOrder.World;

        public override void Capture(SaveData save)
        {
            var saved = new List<LootSave>();
            foreach (LootContainer container in _registry.Containers)
                if (container != null && container.TryGetComponent(out SaveId id) && id.HasId)
                    saved.Add(new LootSave
                    {
                        Id = id.Id,
                        Items = ItemSaves.WriteSlots(container.Items, _catalog),
                        Hidden = container.HiddenSlots(),
                    });
            save.Set(Key, saved);
        }

        public override void Restore(SaveData save)
        {
            if (!save.TryGet(Key, out List<LootSave> saved) || saved == null)
                return;
            var byId = new Dictionary<string, LootContainer>();
            foreach (LootContainer container in _registry.Containers)
                if (container != null && container.TryGetComponent(out SaveId id) && id.HasId)
                    byId[id.Id] = container;
            foreach (LootSave entry in saved)
            {
                if (entry == null || !byId.TryGetValue(entry.Id, out LootContainer container))
                    continue;
                List<ItemInstance> leftOver = ItemSaves.ReadSlots(container.Items, entry.Items, _catalog);
                if (leftOver.Count > 0)
                    Debug.LogWarning($"{container.name}: {leftOver.Count} saved item(s) no longer fit and were dropped.", container);
                container.RestoreHidden(entry.Hidden);
            }
        }

        [Serializable]
        public sealed class LootSave
        {
            public string Id;
            public List<SlotSave> Items = new();
            public List<int> Hidden = new();
        }
    }
}
