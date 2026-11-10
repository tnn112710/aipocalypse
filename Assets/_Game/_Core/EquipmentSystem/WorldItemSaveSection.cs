using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Saves every item lying in the world, scene-placed or dropped. Loading removes the scene's own copies and puts the saved
    // ones back, so a picked-up item stays picked up. WorldItems aren't injected or registered, so this finds them in the
    // scene, only when saving or loading.
    public sealed class WorldItemSaveSection : SaveSection
    {
        const string Key = "worldItems";

        readonly ItemCatalog _catalog;

        public WorldItemSaveSection(SaveRegistry registry, ItemCatalog catalog) : base(registry) => _catalog = catalog;

        public override int Order => SaveOrder.WorldItems;

        public override void Capture(SaveData save)
        {
            var saved = new List<WorldItemSave>();
            foreach (WorldItem worldItem in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
            {
                if (worldItem.IsTaken)
                    continue;
                ItemSave item = ItemSaves.Write(worldItem.Item, _catalog);
                if (item != null)
                    saved.Add(new WorldItemSave { Item = item, Position = worldItem.transform.position, Rotation = worldItem.transform.rotation });
            }
            save.Set(Key, saved);
        }

        public override void Restore(SaveData save)
        {
            if (!save.TryGet(Key, out List<WorldItemSave> saved) || saved == null)
                return;
            foreach (WorldItem worldItem in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
                Object.Destroy(worldItem.gameObject);
            foreach (WorldItemSave entry in saved)
            {
                ItemInstance item = ItemSaves.Read(entry?.Item, _catalog);
                if (item != null)
                    WorldItem.Spawn(item, entry.Position, entry.Rotation, null);
            }
        }

        [Serializable]
        public sealed class WorldItemSave
        {
            public ItemSave Item;
            public Vector3 Position;
            public Quaternion Rotation;
        }
    }
}
