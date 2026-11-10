using System;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Inventories and item definitions for tests.
    static class TestItems
    {
        // Backpack only: no hotbar or gear slots.
        public static Inventory Inventory(int backpackSlots = 16, float maxWeight = 25f) =>
            new(new ItemContainer(Array.Empty<ItemCategory>()), new ItemContainer(Array.Empty<ItemCategory>()), backpackSlots, maxWeight);

        public static AmmoReserve Reserve(int backpackSlots = 16) => new(Inventory(backpackSlots));

        // Sets an item's serialized stack size, weight or other ItemDefinition fields.
        public static T With<T>(this T definition, int maxStack = 1, float weight = 0f, bool unlimited = false, bool autoCollect = false)
            where T : ItemDefinition
        {
            var so = new SerializedObject(definition);
            so.FindProperty("_maxStack").intValue = maxStack;
            so.FindProperty("_weight").floatValue = weight;
            so.FindProperty("_unlimitedStack").boolValue = unlimited;
            so.FindProperty("_autoCollect").boolValue = autoCollect;
            so.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        public static T Create<T>(int maxStack = 1, float weight = 0f, bool unlimited = false) where T : ItemDefinition =>
            ScriptableObject.CreateInstance<T>().With(maxStack, weight, unlimited);
    }
}
