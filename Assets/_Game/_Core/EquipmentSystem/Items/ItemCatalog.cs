using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Every item definition by a stable id (its asset GUID, which survives renames and moves), so saves name items by id
    // instead of by reference. One asset: Data/Equipment/ItemCatalog. Collect after adding an item (a test checks it).
    [CreateAssetMenu(menuName = "Aipocalypse/Equipment/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("Asset GUID → item. Filled by Collect")] Dictionary<string, ItemDefinition> _items = new();

        Dictionary<ItemDefinition, string> _ids;

        public IReadOnlyDictionary<string, ItemDefinition> Items => _items;

        public ItemDefinition Find(string id) => id != null && _items.TryGetValue(id, out ItemDefinition item) ? item : null;

        // The item's id, or null when it isn't in the catalog (it can't be saved).
        public string IdOf(ItemDefinition item)
        {
            if (item == null)
                return null;
            if (_ids == null)
            {
                _ids = new Dictionary<ItemDefinition, string>(_items.Count);
                foreach (KeyValuePair<string, ItemDefinition> pair in _items)
                    if (pair.Value != null)
                        _ids[pair.Value] = pair.Key;
            }
            return _ids.TryGetValue(item, out string id) ? id : null;
        }

#if UNITY_EDITOR
        // Lists every ItemDefinition asset in the project. Returns how many.
        [Button("Collect every item"), PropertyOrder(-1)]
        public int Collect()
        {
            _items.Clear();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
            {
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                    _items[guid] = item;
            }
            _ids = null;
            UnityEditor.EditorUtility.SetDirty(this);
            return _items.Count;
        }
#endif
    }
}
