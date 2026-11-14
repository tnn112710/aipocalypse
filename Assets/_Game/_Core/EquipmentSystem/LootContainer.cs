using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Something to search: a crate, a dead enemy. Its key press hands it to the looter, and the inventory screen shows its
    // slots at once: what's inside starts hidden and is revealed one stack at a time (PlayerLooting), after Duckov.
    // Revealed items can be taken out, and items put in.
    [DisallowMultipleComponent]
    public sealed class LootContainer : MonoBehaviour, IInteractable
    {
        static readonly LocalizedString SearchVerb = Loc.Interaction("verb.search");
        static readonly LocalizedString OpenVerb = Loc.Interaction("verb.open");
        // The same stream as enemy bodies' loot.
        const string LootStream = "loot";

        [SerializeField, Tooltip("Name in the prompt and the loot panel (Interaction or Creatures table)")] LocalizedString _label;
        [SerializeField, Min(1)] int _capacity = 8;
        [SerializeField, Tooltip("What a scene-placed container holds at start")] List<ItemStack> _contents = new();
        [SerializeField, Tooltip("Optional: rolled once on top of Contents when it's first filled (a crate's random loot, the " +
            "\"loot\" random stream; needs injection). A loaded save replaces both")]
        LootTable _table;
        [SerializeField, Tooltip("Offered only while it holds something (a body): no prompt once empty")] bool _hideWhenEmpty;
        [SerializeField, Tooltip("Optional: the collider the look ray finds it by, turned off while it can't be searched (a body's trigger)")]
        Collider _searchCollider;
        [SerializeField, Tooltip("Metres above the search collider's centre (or the pivot) where the HUD's loot marker floats")]
        float _markerHeight = 1f;

        ItemContainer _items;
        bool[] _hidden;
        int _hiddenCount;
        bool _searchable = true;
        bool _wasEmpty;
        LootRegistry _registry;
        RandomStream _random;
        bool _tableRolled;

        // It went from holding something to empty.
        public event Action<LootContainer> Emptied;
        // A slot was revealed, or the contents were hidden again (a new fill).
        public event Action<LootContainer> RevealChanged;

        public ItemContainer Items => _items ??= Build();
        public string Label => _label.Text();
        // Everything inside has been revealed: opening it again needs no wait.
        public bool Searched => _hiddenCount == 0;
        public int HiddenCount => _hiddenCount;
        public bool Searchable => _searchable && !(_hideWhenEmpty && Items.IsEmpty);
        // Searchable and holding something: what the HUD marks.
        public bool HoldsLoot => Searchable && !Items.IsEmpty;
        public Vector3 MarkerPoint =>
            (_searchCollider != null && _searchCollider.enabled ? _searchCollider.bounds.center : transform.position) + Vector3.up * _markerHeight;

        [Inject]
        void Construct(IObjectResolver resolver)
        {
            resolver.TryResolve(out _registry);
            if (_table != null && resolver.TryResolve(out RandomService random))
            {
                _random = random.Stream(LootStream);
                // Filled before injection (something looked inside first): its table rolls now.
                if (_items != null && RollTable())
                    HideContents();
            }
            // Injected after OnEnable when the scope builds late: register now (the list ignores repeats).
            if (isActiveAndEnabled)
                OnEnable();
        }

        void OnEnable() => _registry?.Add(this);
        void OnDisable() => _registry?.Remove(this);

        public bool IsHidden(int slot) => _hidden != null && slot >= 0 && slot < _hidden.Length && _hidden[slot];

        // The slot the search reveals next (top to bottom), or -1.
        public int NextHidden
        {
            get
            {
                if (_hiddenCount == 0)
                    return -1;
                for (int i = 0; i < _hidden.Length; i++)
                    if (_hidden[i])
                        return i;
                return -1;
            }
        }

        // Reveals the next hidden slot; returns it, or -1.
        public int RevealNext()
        {
            int slot = NextHidden;
            if (slot >= 0)
                SetHidden(slot, false);
            return slot;
        }

        public void RevealAll()
        {
            if (_hiddenCount == 0)
                return;
            for (int i = 0; i < _hidden.Length; i++)
                _hidden[i] = false;
            _hiddenCount = 0;
            RevealChanged?.Invoke(this);
        }

        public void SetLabel(LocalizedString label) => _label = label;

        // The slots still hidden (saving).
        public List<int> HiddenSlots()
        {
            var slots = new List<int>();
            _ = Items;
            for (int i = 0; i < _hidden.Length; i++)
                if (_hidden[i])
                    slots.Add(i);
            return slots;
        }

        // Loading a save, after its items are back: hides exactly these slots (an empty slot is never hidden).
        public void RestoreHidden(IReadOnlyList<int> slots)
        {
            _ = Items;
            _hiddenCount = 0;
            for (int i = 0; i < _hidden.Length; i++)
                _hidden[i] = false;
            if (slots != null)
                foreach (int slot in slots)
                    if (slot >= 0 && slot < _hidden.Length && _items[slot] != null && !_hidden[slot])
                    {
                        _hidden[slot] = true;
                        _hiddenCount++;
                    }
            _wasEmpty = _items.IsEmpty;
            RevealChanged?.Invoke(this);
        }

        // Replaces what's inside (a body's loot roll); all of it hidden until searched again.
        public void Fill(IReadOnlyList<ItemInstance> items)
        {
            Clear();
            foreach (ItemInstance item in items)
                Items.TryAdd(item, out _);
            HideContents();
        }

        public void Clear()
        {
            for (int i = 0; i < Items.Count; i++)
                Items.Remove(i);
        }

        // Turns searching (and the search collider) on or off.
        public void SetSearchable(bool searchable)
        {
            _searchable = searchable;
            if (_searchCollider != null)
                _searchCollider.enabled = searchable;
        }

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Searchable || !actor.TryGetComponent(out ILooter _))
                return false;
            prompt = new InteractionPrompt((Searched ? OpenVerb : SearchVerb).Text(), _label.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (Searchable && actor.TryGetComponent(out ILooter looter))
                looter.BeginLoot(this);
        }

        ItemContainer Build()
        {
            var items = new ItemContainer(_capacity);
            _hidden = new bool[items.Count];
            foreach (ItemStack stack in _contents)
            {
                if (stack.Item == null)
                    continue;
                for (int left = stack.Count; left > 0;)
                {
                    int size = Math.Min(left, stack.Item.MaxStack);
                    if (!items.TryAdd(stack.Item.CreateInstance(size), out _))
                    {
                        Debug.LogWarning($"{name}: no room for all of {stack.Item.name}.", this);
                        break;
                    }
                    left -= size;
                }
            }
            items.Changed += OnItemsChanged;
            items.SlotChanged += OnSlotChanged;
            _items = items;
            RollTable();
            _wasEmpty = items.IsEmpty;
            HideContents();
            return items;
        }

        // Adds the table's roll once (needs the random stream from injection); true if it rolled.
        bool RollTable()
        {
            if (_tableRolled || _table == null || _random == null)
                return false;
            _tableRolled = true;
            var rolled = new List<ItemInstance>();
            _table.Roll(_random, rolled);
            foreach (ItemInstance item in rolled)
                if (!_items.TryAdd(item, out _))
                    Debug.LogWarning($"{name}: no room for rolled {item.Definition.name}.", this);
            _wasEmpty = _items.IsEmpty;
            return true;
        }

        void HideContents()
        {
            _hiddenCount = 0;
            for (int i = 0; i < _hidden.Length; i++)
            {
                _hidden[i] = _items[i] != null;
                if (_hidden[i])
                    _hiddenCount++;
            }
            RevealChanged?.Invoke(this);
        }

        void SetHidden(int slot, bool hidden)
        {
            if (_hidden[slot] == hidden)
                return;
            _hidden[slot] = hidden;
            _hiddenCount += hidden ? 1 : -1;
            RevealChanged?.Invoke(this);
        }

        // An emptied slot (cleared, or its stack used up) has nothing left to hide.
        void OnSlotChanged(int slot, ItemInstance previous, ItemInstance item)
        {
            if (item == null && slot < _hidden.Length && _hidden[slot])
                SetHidden(slot, false);
        }

        void OnItemsChanged()
        {
            bool empty = _items.IsEmpty;
            if (empty && !_wasEmpty)
                Emptied?.Invoke(this);
            _wasEmpty = empty;
        }
    }
}
