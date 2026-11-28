using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.SaveSystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Saves the active player: where they stand and look, their health and needs, everything they carry (hotbar, backpack,
    // gear, ammo included) and the slot in their hands. Timed effects (buffs, healing) and stamina start fresh on load.
    public sealed class PlayerSaveSection : SaveSection
    {
        const string Key = "player";

        readonly PlayerRegistry _players;
        readonly ItemCatalog _catalog;

        public PlayerSaveSection(SaveRegistry registry, PlayerRegistry players, IObjectResolver resolver) : base(registry)
        {
            _players = players;
            resolver.TryResolve(out _catalog);
        }

        public override int Order => SaveOrder.Player;

        public override void Capture(SaveData save)
        {
            if (_players.Current is not Player player)
                return;
            var saved = new PlayerSave
            {
                Position = player.transform.position,
                Yaw = player.transform.eulerAngles.y,
                Pitch = player.Pitch,
                Health = player.Health != null ? player.Health.Current : 0f,
            };
            if (player.Needs != null)
                CaptureNeeds(saved, player.Needs);
            Equipment equipment = player.Equipment;
            if (equipment != null && _catalog != null)
            {
                saved.Hotbar = ItemSaves.WriteSlots(equipment.Inventory.Hotbar, _catalog);
                saved.Backpack = ItemSaves.WriteSlots(equipment.Inventory.Backpack, _catalog);
                saved.Gear = ItemSaves.WriteSlots(equipment.Inventory.Gear, _catalog);
                saved.Selected = equipment.SelectedIndex;
                saved.HasItems = true;
            }
            save.Set(Key, saved);
        }

        public override void Restore(SaveData save)
        {
            if (_players.Current is not Player player || !save.TryGet(Key, out PlayerSave saved) || saved == null)
                return;
            player.RestoreSaved(saved.Position, saved.Yaw, saved.Pitch, saved.Health);
            if (player.Needs != null)
                RestoreNeeds(saved, player.Needs);
            Equipment equipment = player.Equipment;
            if (!saved.HasItems || equipment == null || _catalog == null)
                return;

            Inventory inventory = equipment.Inventory;
            // Emptied first, backpack before gear: taking the worn bag off shrinks the backpack.
            Clear(inventory.Hotbar);
            Clear(inventory.Backpack);
            Clear(inventory.Gear);
            var leftOver = new List<ItemInstance>();
            leftOver.AddRange(ItemSaves.ReadSlots(inventory.Gear, saved.Gear, _catalog));
            leftOver.AddRange(ItemSaves.ReadSlots(inventory.Backpack, saved.Backpack, _catalog));
            leftOver.AddRange(ItemSaves.ReadSlots(inventory.Hotbar, saved.Hotbar, _catalog));
            foreach (ItemInstance item in leftOver)
                player.ThrowAway(item);
            equipment.SelectImmediately(saved.Selected);
        }

        public static void CaptureNeeds(PlayerSave saved, PlayerNeeds needs)
        {
            saved.HasNeeds = true;
            saved.Hunger = needs.Hunger.Current;
            saved.Thirst = needs.Thirst.Current;
            saved.Sanity = needs.Sanity.Current;
            saved.HoursAwake = needs.HoursAwake;
        }

        // A save from before the needs has HasNeeds false: the fresh, full meters stay. Saved stat modifiers load first
        // (SaveOrder.Stats), so a value above the base max isn't clamped.
        public static void RestoreNeeds(PlayerSave saved, PlayerNeeds needs)
        {
            if (!saved.HasNeeds)
                return;
            needs.Hunger.Set(saved.Hunger);
            needs.Thirst.Set(saved.Thirst);
            needs.Sanity.Set(saved.Sanity);
            needs.HoursAwake = saved.HoursAwake;
        }

        static void Clear(ItemContainer container)
        {
            for (int i = 0; i < container.Count; i++)
                container.Remove(i);
        }

        [Serializable]
        public sealed class PlayerSave
        {
            public Vector3 Position;
            public float Yaw;
            public float Pitch;
            public float Health;
            public bool HasNeeds;
            public float Hunger;
            public float Thirst;
            public float Sanity;
            public float HoursAwake;
            public bool HasItems;
            public List<SlotSave> Hotbar = new();
            public List<SlotSave> Backpack = new();
            public List<SlotSave> Gear = new();
            public int Selected = -1;
        }
    }
}
