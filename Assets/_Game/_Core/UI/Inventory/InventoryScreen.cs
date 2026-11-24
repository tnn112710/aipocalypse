using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // The inventory screen, after Escape from Duckov's: equipment (active gun and melee slots, gear) and the backpack on the
    // left, a searched crate or body on the right; the HUD hotbar along the bottom takes drags too. Opens with the inventory key
    // or a crate's / body's key press, whose hidden items it then reveals one by one (PlayerLooting). Pauses the game while
    // open, except during a raid: then the world goes on and a hit closes it.
    // Drag to move, merge or swap; Shift-drag moves half a stack; double-click sends an item across (loot <-> backpack,
    // backpack <-> a free active or gear slot). Right-click opens an item's menu (carried: Use, Drop, Lock; a crate's: Pick
    // up, Use); on a fitted part it does the double-click's move. Keys on the hovered item: Drop, Lock, and a hotbar key
    // moves it to that slot.
    // The centre inspects the item last clicked (the held one when the screen opens): in 3D, with a gun's part slots.
    // Parts are fitted by dragging them onto a part slot or the 3D view, or by double-clicking them; taken off the same way.
    public sealed class InventoryScreen : PlayerHudView
    {
        const ItemCategory WeaponSlots = ItemCategory.Gun | ItemCategory.Melee;
        const float MessageSeconds = 2.5f;
        static readonly LocalizedString BackpackTitle = Loc.UI("inventory.backpack_title");
        static readonly LocalizedString WeightFormat = Loc.UI("inventory.weight");
        static readonly LocalizedString Kilograms = Loc.UI("unit.weight");
        static readonly LocalizedString StackWeight = Loc.UI("unit.weight_stack");
        static readonly LocalizedString NoRoom = Loc.UI("inventory.no_room");
        static readonly LocalizedString NowhereToWear = Loc.UI("inventory.nowhere");
        static readonly LocalizedString BackpackFull = Loc.UI("inventory.backpack_full");
        static readonly LocalizedString StillSearching = Loc.UI("inventory.still_searching");
        static readonly LocalizedString PickGun = Loc.UI("inventory.part_pick_gun");
        static readonly LocalizedString SlotGun = Loc.UI("inventory.slot_gun");
        static readonly LocalizedString SlotMelee = Loc.UI("inventory.slot_melee");
        static readonly LocalizedString SlotItems = Loc.UI("inventory.slot_items");
        static readonly LocalizedString UsesFormat = Loc.UI("tooltip.uses");
        static readonly LocalizedString ValueFormat = Loc.UI("tooltip.value");
        static readonly LocalizedString LockedText = Loc.UI("tooltip.locked");
        static readonly LocalizedString HintCarried = Loc.UI("tooltip.hint_carried");
        static readonly LocalizedString HintLoot = Loc.UI("tooltip.hint_loot");
        static readonly LocalizedString HintKeys = Loc.UI("tooltip.hint_keys");
        static readonly LocalizedString DropLocked = Loc.UI("inventory.drop_locked");
        // Indexed by ItemRarity.
        static readonly LocalizedString[] RarityNames =
        {
            Loc.Items("rarity.common"), Loc.Items("rarity.uncommon"), Loc.Items("rarity.rare"), Loc.Items("rarity.epic"),
            Loc.Items("rarity.legendary"),
        };

        [SerializeField] InventoryStyle _style;
        [SerializeField, Tooltip("Shown and raycast only while open")] CanvasGroup _group;
        [SerializeField, Tooltip("EventSystem + InputSystemUIInputModule: active only while open")] GameObject _eventSystem;
        [SerializeField] ItemSlotView _slotPrefab;
        [SerializeField] RectTransform _equipmentGrid;
        [SerializeField] RectTransform _backpackGrid;
        [SerializeField] RectTransform _lootGrid;
        [SerializeField, Tooltip("The HUD hotbar: while the screen is open its row is drawn above it and its slots take drags (the quick slots live there)")]
        HotbarHud _hotbar;
        [SerializeField] GameObject _lootPanel;
        [SerializeField] TMP_Text _backpackTitle;
        [SerializeField] TMP_Text _lootTitle;
        [SerializeField] TMP_Text _tokens;
        [SerializeField] TMP_Text _weight;
        [SerializeField] Image _weightFill;
        [SerializeField] TMP_Text _message;
        [SerializeField] Button _sortButton;
        [SerializeField] Button _takeAllButton;
        [SerializeField, Tooltip("Follows the pointer while dragging")] Image _dragIcon;
        [SerializeField] ItemTooltipView _tooltip;
        [SerializeField, Tooltip("An item's right-click menu")] ItemContextMenu _menu;
        [SerializeField, Tooltip("Optional: the centre panel inspecting the selected item, with a gun's part slots")]
        ItemInspectorView _inspector;

        // What each slot view shows: a slot of one of the containers, or a part slot of the inspected gun.
        struct SlotRef
        {
            public ISlotView View;
            public ItemContainer Container;
            public int Index;
            public bool IsPart;
            public GunSlot Part;
        }

        readonly List<SlotRef> _slots = new();
        // Each grid keeps its own views, hooked once: a rebuild only adds or hides the difference. Re-parenting ~70 pooled
        // views on every open cost ~5 ms, a hitch now that the screen doesn't pause during raids.
        readonly List<ItemSlotView> _equipmentViews = new();
        readonly List<ItemSlotView> _backpackViews = new();
        readonly List<ItemSlotView> _lootViews = new();
        // The hotbar row last hooked: the HUD makes new slot views when it binds another player.
        IReadOnlyList<ISlotView> _hookedHotbar;
        Canvas _canvas;
        InputService _input;
        TimeService _time;
        AudioService _audio;
        IDisposable _subscriptions;
        bool _raid;
        bool _pausing;
        int _lootStart;
        Equipment _equipment;
        Inventory _inventory;
        PlayerLooting _looting;
        LootContainer _loot;
        bool _open;
        bool _dirty;
        bool _rebuild;
        int _dragFrom = -1;
        int _dragAmount;
        // The slot and item the open menu is for: the item may have moved or gone by the time an option is picked.
        SlotRef _menuSlot;
        ItemInstance _menuItem;
        float _messageUntil;
        // The tooltip's control hints, made when the screen opens (key names allocate).
        string _hintCarried;
        string _hintLoot;

        public bool IsOpen => _open;
        public LootContainer Loot => _loot;

        [Inject]
        void Construct(InputService input, TimeService time, AudioService audio, ISubscriber<RaidStarted> raidStarted,
            ISubscriber<RaidEnded> raidEnded, ISubscriber<DamageDealt> dealt)
        {
            _input = input;
            _time = time;
            _audio = audio;
            _subscriptions = DisposableBag.Create(
                raidStarted.Subscribe(_ => _raid = true),
                raidEnded.Subscribe(_ => _raid = false),
                dealt.Subscribe(OnDamageDealt));
            if (_eventSystem != null && _eventSystem.TryGetComponent(out InputSystemUIInputModule module))
                _input.ConfigureUiModule(module);
        }

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            SetVisible(false);
            if (_sortButton != null)
                _sortButton.onClick.AddListener(SortBackpack);
            if (_takeAllButton != null)
                _takeAllButton.onClick.AddListener(TakeAll);
            if (_inspector != null)
            {
                foreach (ItemSlotView view in _inspector.SlotViews)
                    Hook(view);
                _inspector.StageDropped += OnStageDropped;
            }
            if (_menu != null)
            {
                _menu.Hide();
                _menu.Chosen += OnMenuChosen;
            }
        }

        protected override void Bind(IPlayer player)
        {
            _equipment = player.Equipment;
            _inventory = _equipment?.Inventory;
            _looting = player.Looting;
            if (_inventory == null)
                return;
            _inventory.Changed += MarkDirty;
            _inventory.Backpack.Resized += MarkRebuild;
            if (_looting != null)
            {
                _looting.Opened += OnLootOpened;
                _looting.Revealed += OnRevealed;
            }
        }

        protected override void Unbind(IPlayer player)
        {
            Close();
            if (_inventory != null)
            {
                _inventory.Changed -= MarkDirty;
                _inventory.Backpack.Resized -= MarkRebuild;
            }
            if (_looting != null)
            {
                _looting.Opened -= OnLootOpened;
                _looting.Revealed -= OnRevealed;
            }
            _equipment = null;
            _inventory = null;
            _looting = null;
        }

        protected override void OnDestroy()
        {
            Close();
            _subscriptions?.Dispose();
            base.OnDestroy();
        }

        void Update()
        {
            if (_inventory == null || _input == null)
                return;
            if (!_open)
            {
                if (_input.InventoryPressed && CanOpen())
                    Open(null);
                return;
            }
            if (_menu != null && _menu.IsOpen && _input.UiCancelPressed && !_input.InventoryPressed)
            {
                _menu.Hide();
                return;
            }
            if (_input.InventoryPressed || _input.UiCancelPressed || Player == null || Player.Health == null || Player.Health.IsDead)
            {
                Close();
                return;
            }
            if (_menu == null || !_menu.IsOpen)
                HandleShortcuts();
            TickSearch();
            if (_rebuild)
                Rebuild();
            if (_dirty)
                Refresh();
            if (_message != null && _message.enabled && Time.unscaledTime > _messageUntil)
                _message.enabled = false;
        }

        // Opens the screen; with `loot`, its panel shows that container.
        public void Open(LootContainer loot)
        {
            if (_inventory == null)
                return;
            SetLoot(loot);
            if (!_open)
            {
                _open = true;
                // During a raid the world goes on: looting a body mid-fight is a risk, not a safe pause.
                _pausing = !_raid;
                if (_pausing)
                    _time?.RequestPause(this);
                Play(_style != null ? _style.OpenSound : default);
                _hintCarried = HintCarried.Text() + "\n" + HintKeys.Format(_input.GetDropKeyLabel(), _input.GetLockKeyLabel(), HotbarKeyRange());
                _hintLoot = HintLoot.Text();
                _input.RequestUI(this);
                if (_eventSystem != null)
                    _eventSystem.SetActive(true);
                SetVisible(true);
                if (_hotbar != null)
                    _hotbar.SetInteractive(true, (_canvas != null ? _canvas.sortingOrder : 0) + 1);
                Rebuild();
                // Opening shows the item in hand, as Duckov does.
                HeldItem held = Player?.HeldItem;
                Inspect(held != null && _inventory.Contains(held.Item) ? held.Item : null);
                return;
            }
            Rebuild();
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            _looting?.Stop();
            CancelDrag();
            if (_menu != null)
                _menu.Hide();
            if (_tooltip != null)
                _tooltip.Hide();
            _inspector?.Hide();
            SetLoot(null);
            SetVisible(false);
            if (_hotbar != null)
                _hotbar.SetInteractive(false, 0);
            if (_eventSystem != null)
                _eventSystem.SetActive(false);
            _input?.ReleaseUI(this);
            if (_pausing)
                _time?.ReleasePause(this);
            _pausing = false;
            Play(_style != null ? _style.CloseSound : default);
        }

        bool CanOpen() => Player != null && Player.Health != null && !Player.Health.IsDead && !_input.IsUIRequested;

        void OnLootOpened(LootContainer container)
        {
            if (CanOpen() || _open)
                Open(container);
            else
                _looting.Stop();
        }

        // A hit while the world goes on (a raid) closes the screen, so the player can fight back.
        void OnDamageDealt(DamageDealt hit)
        {
            if (_open && !_pausing && hit.Result.Applied > 0f && Player != null && ReferenceEquals(hit.Target, Player))
                Close();
        }

        void OnRevealed(LootContainer container, int slot)
        {
            if (container == _loot)
                Play(_style != null ? _style.RevealSound : default);
        }

        // Reveals the open container's hidden items in real time (the game may be paused), and fills the circle on the one
        // being revealed.
        void TickSearch()
        {
            if (_looting == null || _loot == null || _looting.Searching != _loot)
                return;
            _looting.Tick(Time.unscaledDeltaTime);
            int slot = _looting.Searching == _loot ? _looting.RevealingSlot : -1;
            if (slot >= 0 && _lootStart + slot < _slots.Count && _slots[_lootStart + slot].View is ItemSlotView view)
                view.SetSearchProgress(_looting.Progress);
        }

        void SetLoot(LootContainer loot)
        {
            if (_loot != null)
            {
                _loot.Items.Changed -= MarkDirty;
                _loot.RevealChanged -= OnRevealChanged;
            }
            _loot = loot;
            if (_loot != null)
            {
                _loot.Items.Changed += MarkDirty;
                _loot.RevealChanged += OnRevealChanged;
            }
            if (_lootPanel != null)
                _lootPanel.SetActive(_loot != null);
        }

        // Closed, its canvas is off: a canvas hidden only by alpha still batches and draws its ~70 slots every frame.
        void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
            if (_group == null)
                return;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        void MarkDirty() => _dirty = true;
        void OnRevealChanged(LootContainer _) => _dirty = true;
        void MarkRebuild() => _rebuild = true;

        // Maps every slot of every container to a view of its grid.
        void Rebuild()
        {
            _rebuild = false;
            CancelDrag();
            if (_menu != null)
                _menu.Hide();
            _slots.Clear();

            ItemContainer hotbar = _inventory.Hotbar;
            int used = 0;
            for (int i = 0; i < hotbar.Count; i++)
                if (IsWeaponSlot(hotbar, i))
                    AddSlot(_equipmentViews, _equipmentGrid, used++, hotbar, i, _equipment.SlotName(i), Key(i));
            ItemContainer gear = _inventory.Gear;
            for (int i = 0; i < gear.Count; i++)
                AddSlot(_equipmentViews, _equipmentGrid, used++, gear, i, _equipment.GearName(i), null);
            HideFrom(_equipmentViews, used);

            for (int i = 0; i < _inventory.Backpack.Count; i++)
                AddSlot(_backpackViews, _backpackGrid, i, _inventory.Backpack, i, null, null);
            HideFrom(_backpackViews, _inventory.Backpack.Count);

            IReadOnlyList<ISlotView> row = _hotbar != null ? _hotbar.Slots : null;
            if (row != null)
            {
                if (!ReferenceEquals(row, _hookedHotbar))
                {
                    foreach (ISlotView view in row)
                        Hook(view);
                    _hookedHotbar = row;
                }
                for (int i = 0; i < row.Count && i < hotbar.Count; i++)
                {
                    row[i].Index = _slots.Count;
                    row[i].SetDragged(false);
                    _slots.Add(new SlotRef { View = row[i], Container = hotbar, Index = i });
                }
            }

            _lootStart = _slots.Count;
            int loot = _loot != null ? _loot.Items.Count : 0;
            for (int i = 0; i < loot; i++)
                AddSlot(_lootViews, _lootGrid, i, _loot.Items, i, null, null);
            HideFrom(_lootViews, loot);

            if (_inspector != null)
            {
                for (int i = 0; i < _inspector.SlotViews.Count; i++)
                {
                    ItemSlotView view = _inspector.SlotViews[i];
                    view.Index = _slots.Count;
                    view.SetDragged(false);
                    _slots.Add(new SlotRef { View = view, IsPart = true, Part = (GunSlot)i });
                }
            }
            Refresh();
        }

        // Shows `item` in the centre; the part slots follow on the next refresh.
        void Inspect(ItemInstance item)
        {
            if (_inspector == null)
                return;
            _inspector.Show(item);
            _dirty = true;
        }

        // What a slot view shows: the container's item, or the part in the inspected gun's slot.
        ItemInstance ItemAt(SlotRef slot) => slot.IsPart ? _inspector?.Gun?.AttachmentIn(slot.Part) : slot.Container[slot.Index];

        string Key(int hotbarSlot) => _input.GetKeyLabel(_equipment.KeyOf(hotbarSlot));

        // "1-5": the number keys of the hotbar's slots.
        string HotbarKeyRange()
        {
            int first = int.MaxValue;
            int last = -1;
            for (int i = 0; i < _inventory.Hotbar.Count; i++)
            {
                var key = (int)_equipment.KeyOf(i);
                if (key > (int)HotbarKey.Key9)
                    continue;
                first = Math.Min(first, key);
                last = Math.Max(last, key);
            }
            if (last < 0)
                return string.Empty;
            string from = _input.GetKeyLabel((HotbarKey)first);
            return first == last ? from : from + "-" + _input.GetKeyLabel((HotbarKey)last);
        }

        static bool IsWeaponSlot(ItemContainer hotbar, int index) =>
            (hotbar.AcceptsAt(index) & WeaponSlots) != 0 && (hotbar.AcceptsAt(index) & ~WeaponSlots) == 0;

        // The `at`-th view of a grid (made the first time) now shows `container[index]`.
        void AddSlot(List<ItemSlotView> views, RectTransform parent, int at, ItemContainer container, int index, string caption, string key)
        {
            if (at == views.Count)
            {
                ItemSlotView created = Instantiate(_slotPrefab, parent, false);
                Hook(created);
                views.Add(created);
            }
            ItemSlotView view = views[at];
            if (!view.gameObject.activeSelf)
                view.gameObject.SetActive(true);
            view.Index = _slots.Count;
            view.SetCaption(caption);
            view.SetKey(key);
            view.SetDragged(false);
            _slots.Add(new SlotRef { View = view, Container = container, Index = index });
        }

        static void HideFrom(List<ItemSlotView> views, int count)
        {
            for (int i = count; i < views.Count; i++)
                if (views[i].gameObject.activeSelf)
                    views[i].gameObject.SetActive(false);
        }

        void Hook(ISlotView view)
        {
            view.Clicked += OnSlotClicked;
            view.DragStarted += OnDragStarted;
            view.Dragging += OnDragging;
            view.DragEnded += OnDragEnded;
            view.DroppedOn += OnDroppedOn;
            view.HoverChanged += OnHoverChanged;
        }

        void Refresh()
        {
            _dirty = false;
            // The inspected item left (thrown away, taken by the crate's search going away): nothing is inspected.
            ItemInstance inspected = _inspector != null ? _inspector.Item : null;
            if (inspected != null && !_inventory.Contains(inspected) && (_loot == null || _loot.Items.IndexOf(inspected) < 0))
                Inspect(null);
            _inspector?.Refresh();

            foreach (SlotRef slot in _slots)
            {
                // The HUD hotbar shows its own items.
                if (slot.View is not ItemSlotView view)
                    continue;
                ItemInstance item = ItemAt(slot);
                if (IsHidden(slot))
                {
                    bool revealing = _looting != null && _looting.Searching == _loot && _looting.RevealingSlot == slot.Index;
                    view.ShowHidden(revealing ? _looting.Progress : -1f);
                    continue;
                }
                if (item == null)
                {
                    view.ShowEmpty(slot.IsPart || slot.Container.AcceptsAnything ? null
                        : _style != null ? _style.EmptyIcon(slot.Container.AcceptsAt(slot.Index)) : null);
                    continue;
                }
                ItemBadge badge = item.Badge;
                view.Show(item.Definition.Icon, item.Definition.DisplayName, badge.Visible ? badge.Count : -1, badge.Warning,
                    _style != null ? _style.RarityColor(item.Definition.Rarity) : default, item.Locked);
            }

            ItemContainer backpack = _inventory.Backpack;
            if (_backpackTitle != null)
                _backpackTitle.SetText(BackpackTitle.Text(), (float)backpack.UsedSlots, (float)backpack.Count);
            if (_loot != null && _lootTitle != null)
                _lootTitle.SetText($"{_loot.Label} ({_loot.Items.UsedSlots}/{_loot.Items.Count})");
            if (_tokens != null)
                _tokens.SetText(CarriedTokens().ToString("N0"));
            float weight = _inventory.Weight;
            float max = _inventory.MaxWeight;
            if (_weight != null)
                _weight.SetText(WeightFormat.Text(), weight, max);
            if (_weightFill != null)
            {
                _weightFill.fillAmount = max > 0f ? Mathf.Clamp01(weight / max) : 0f;
                if (_style != null)
                    _weightFill.color = weight > max ? _style.OverweightColor : _style.WeightBarColor;
            }
            RefreshTooltip();
        }

        long CarriedTokens()
        {
            long tokens = 0;
            Count(_inventory.Backpack, ref tokens);
            Count(_inventory.Hotbar, ref tokens);
            return tokens;

            static void Count(ItemContainer container, ref long total)
            {
                for (int i = 0; i < container.Count; i++)
                    if (container[i] != null && container[i].Definition.Category == ItemCategory.Currency)
                        total += container[i].Count;
            }
        }

        // Not revealed yet by the search: it can't be moved, swapped, merged into or inspected.
        bool IsHidden(SlotRef slot) => IsLoot(slot) && _loot.IsHidden(slot.Index);

        // In the open crate or body: not carried yet.
        bool IsLoot(SlotRef slot) => _loot != null && !slot.IsPart && slot.Container == _loot.Items;

        void Play(SoundID sound) => _audio?.Play(sound);

        void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            Play(_style != null ? _style.RefusedSound : default);
            if (_message == null)
                return;
            _message.SetText(text);
            if (_style != null)
                _message.color = _style.WarningColor;
            _message.enabled = true;
            _messageUntil = Time.unscaledTime + MessageSeconds;
        }

        // Left-click: inspect the item; double-click: send it across, or fit / take off a gun part. Right-click: the item's
        // menu; on a fitted part, the double-click's move.
        void OnSlotClicked(ISlotView view, PointerEventData eventData)
        {
            if (_dragFrom >= 0)
                return;
            SlotRef slot = _slots[view.Index];
            ItemInstance item = ItemAt(slot);
            if (item == null || IsHidden(slot))
                return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (!slot.IsPart)
                    Inspect(item);
                if (eventData.clickCount == 2)
                    SendAcross(slot, item);
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Right)
                return;
            if (slot.IsPart || _menu == null)
            {
                SendAcross(slot, item);
                return;
            }
            _menuSlot = slot;
            _menuItem = item;
            if (_tooltip != null)
                _tooltip.Hide();
            _menu.Show(eventData.position, IsLoot(slot), item.Definition is ConsumableDefinition && Player is IConsumer, item.Locked);
        }

        void SendAcross(SlotRef slot, ItemInstance item)
        {
            string refusal = null;
            GunInstance gun = _inspector != null ? _inspector.Gun : null;
            if (slot.IsPart)
            {
                Modded(GunModding.TryDetach(_inventory, gun, slot.Part, null, 0, out refusal), refusal, attached: false);
                return;
            }
            if (item.Definition is AttachmentDefinition && (gun != null || _loot == null))
            {
                if (gun == null)
                    ShowMessage(PickGun.Text());
                else
                    Modded(GunModding.TryAttach(_inventory, slot.Container, slot.Index, gun, out refusal), refusal, attached: true);
                return;
            }
            if (_loot != null && slot.Container == _loot.Items)
            {
                if (_inventory.TakeFrom(slot.Container, slot.Index) == 0)
                    refusal = NoRoom.Text();
            }
            else if (_loot != null && slot.Container == _inventory.Backpack)
            {
                if (_inventory.SendTo(slot.Container, slot.Index, _loot.Items, out refusal) == 0 && refusal == null)
                    refusal = NoRoom.Text();
            }
            else if (slot.Container == _inventory.Backpack)
            {
                if (!Equip(slot, out refusal))
                    refusal = NowhereToWear.Text();
            }
            else if (_inventory.SendTo(slot.Container, slot.Index, _inventory.Backpack, out refusal) == 0 && refusal == null)
            {
                refusal = BackpackFull.Text();
            }
            if (refusal == null)
                Play(_style != null ? _style.PlaceSound : default);
            ShowMessage(refusal);
        }

        // From the backpack into a free slot that takes it (active or gear), else swapped with the first one that does. True
        // with a `refusal` when a swap was refused (e.g. the worn backpack isn't empty): the caller shows it, once.
        bool Equip(SlotRef from, out string refusal)
        {
            refusal = null;
            ItemInstance item = from.Container[from.Index];
            ItemContainer[] targets = { _inventory.Hotbar, _inventory.Gear };
            foreach (ItemContainer target in targets)
            {
                int free = target.FindFreeSlot(item);
                if (free >= 0)
                    return _inventory.TryMove(from.Container, from.Index, target, free, out _);
            }
            foreach (ItemContainer target in targets)
            {
                for (int i = 0; i < target.Count; i++)
                {
                    if (!target.CanPlace(i, item))
                        continue;
                    if (_inventory.TryMove(from.Container, from.Index, target, i, out refusal))
                        return true;
                    if (refusal == null)
                        continue;
                    return true;
                }
            }
            return false;
        }

        void OnDragStarted(ISlotView view, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            SlotRef slot = _slots[view.Index];
            ItemInstance item = ItemAt(slot);
            if (item == null || IsHidden(slot))
                return;
            _dragFrom = view.Index;
            if (_menu != null)
                _menu.Hide();
            if (item.Definition is AttachmentDefinition)
                _inspector?.SetPreview(item);
            _dragAmount = _input.UiShiftHeld && item.Count > 1 ? item.Count / 2 : item.Count;
            view.SetDragged(true);
            if (_tooltip != null)
                _tooltip.Hide();
            if (_dragIcon != null)
            {
                _dragIcon.sprite = item.Definition.Icon;
                _dragIcon.enabled = true;
                _dragIcon.rectTransform.position = eventData.position;
            }
        }

        void OnDragging(ISlotView view, PointerEventData eventData)
        {
            if (_dragFrom >= 0 && _dragIcon != null)
                _dragIcon.rectTransform.position = eventData.position;
        }

        void OnDroppedOn(ISlotView target, PointerEventData eventData)
        {
            if (_dragFrom < 0 || target.Index == _dragFrom)
                return;
            SlotRef from = _slots[_dragFrom];
            SlotRef to = _slots[target.Index];
            GunInstance gun = _inspector != null ? _inspector.Gun : null;
            if (from.IsPart && to.IsPart)
                return;
            if (IsHidden(to))
                ShowMessage(StillSearching.Text());
            else if (from.IsPart)
                Modded(GunModding.TryDetach(_inventory, gun, from.Part, to.Container, to.Index, out string detachRefusal), detachRefusal, attached: false);
            else if (to.IsPart)
                Modded(GunModding.TryAttach(_inventory, from.Container, from.Index, gun, out string attachRefusal), attachRefusal, attached: true);
            else if (_inventory.TryMove(from.Container, from.Index, to.Container, to.Index, out string refusal, _dragAmount))
                Play(_style != null ? _style.PlaceSound : default);
            else
                ShowMessage(refusal ?? WrongSlot(from, to));
        }

        // A move the containers refused without a reason: a hotbar slot that doesn't take that kind of item, either the one
        // dropped on or, for a swap, the one it came from.
        string WrongSlot(SlotRef from, SlotRef to)
        {
            ItemContainer hotbar = _inventory.Hotbar;
            ItemInstance moved = ItemAt(from);
            ItemInstance swapped = ItemAt(to);
            if (to.Container == hotbar && moved != null && !hotbar.CanPlace(to.Index, moved))
                return SlotTakes(to.Index);
            if (from.Container == hotbar && swapped != null && !hotbar.CanPlace(from.Index, swapped))
                return SlotTakes(from.Index);
            return null;
        }

        string SlotTakes(int hotbarSlot)
        {
            ItemCategory accepts = _inventory.Hotbar.AcceptsAt(hotbarSlot);
            LocalizedString text = accepts == ItemCategory.Gun ? SlotGun : accepts == ItemCategory.Melee ? SlotMelee : SlotItems;
            return text.Format(Key(hotbarSlot));
        }

        // A part dropped on the inspected gun's 3D view goes to its own slot.
        void OnStageDropped()
        {
            if (_dragFrom < 0 || _slots[_dragFrom].IsPart || _inspector == null || _inspector.Gun == null)
                return;
            SlotRef from = _slots[_dragFrom];
            Modded(GunModding.TryAttach(_inventory, from.Container, from.Index, _inspector.Gun, out string refusal), refusal, attached: true);
        }

        // A part went on or came off the inspected gun: its own sound and a small nudge of the 3D view.
        void Modded(bool done, string refusal, bool attached)
        {
            if (!done)
            {
                ShowMessage(refusal);
                return;
            }
            SoundID sound = _style == null ? default : attached ? _style.AttachSound : _style.DetachSound;
            Play(sound.IsValid() ? sound : _style != null ? _style.PlaceSound : default);
            if (_inspector != null)
                _inspector.Nudge(attached);
        }

        // Let go anywhere but a slot: the item stays where it was (dropping is the menu's).
        void OnDragEnded(ISlotView view, PointerEventData eventData)
        {
            if (_dragFrom < 0)
                return;
            CancelDrag();
            _dirty = true;
        }

        void OnMenuChosen(ItemContextMenu.Choice choice)
        {
            SlotRef slot = _menuSlot;
            ItemInstance item = _menuItem;
            _menuItem = null;
            if (item == null || _inventory == null || ItemAt(slot) != item)
                return;
            switch (choice)
            {
                case ItemContextMenu.Choice.Use:
                    Use(item);
                    break;
                case ItemContextMenu.Choice.Drop:
                    Drop(slot, item);
                    break;
                case ItemContextMenu.Choice.Lock:
                    ToggleLock(item);
                    break;
                case ItemContextMenu.Choice.Pickup:
                    Pickup(slot, item);
                    break;
            }
        }

        // Keys on the hovered item: Drop and Lock (carried items), a hotbar key (moves it to that slot, from the crate too).
        void HandleShortcuts()
        {
            if (_dragFrom >= 0)
                return;
            bool drop = _input.UiDropPressed;
            bool toggleLock = _input.UiLockPressed;
            HotbarKey? key = _input.UiHotbarKeyPressed;
            if (!drop && !toggleLock && key == null)
                return;
            SlotRef slot = default;
            bool hovered = false;
            foreach (SlotRef candidate in _slots)
                if (candidate.View.IsHovered)
                {
                    slot = candidate;
                    hovered = true;
                    break;
                }
            ItemInstance item = hovered ? ItemAt(slot) : null;
            if (item == null || slot.IsPart || IsHidden(slot))
                return;
            if (key != null)
                ToHotbar(slot, key.Value);
            else if (IsLoot(slot))
                return;
            else if (drop)
                Drop(slot, item);
            else
                ToggleLock(item);
        }

        void Drop(SlotRef slot, ItemInstance item)
        {
            if (item.Locked)
            {
                ShowMessage(DropLocked.Text());
                return;
            }
            if (_inventory.TryRemove(slot.Container, slot.Index, out ItemInstance removed, out string refusal))
                Player?.ThrowAway(removed);
            else
                ShowMessage(refusal);
        }

        void ToggleLock(ItemInstance item)
        {
            item.Locked = !item.Locked;
            Play(_style != null ? _style.PlaceSound : default);
        }

        // As much of a crate's stack as fits.
        void Pickup(SlotRef slot, ItemInstance item)
        {
            int count = item.Count;
            int taken = _inventory.TakeFrom(slot.Container, slot.Index);
            if (taken > 0)
                Play(_style != null ? _style.PlaceSound : default);
            if (taken < count)
                ShowMessage(taken == 0 ? NoRoom.Text() : BackpackFull.Text());
        }

        // Moves the whole stack to the hotbar slot `key` selects (swapping with what's there), as a drag onto it would.
        void ToHotbar(SlotRef from, HotbarKey key)
        {
            int index = _equipment.SlotOf(key);
            if (index < 0 || (from.Container == _inventory.Hotbar && from.Index == index))
                return;
            var to = new SlotRef { Container = _inventory.Hotbar, Index = index };
            if (_inventory.TryMove(from.Container, from.Index, to.Container, to.Index, out string refusal))
                Play(_style != null ? _style.PlaceSound : default);
            else
                ShowMessage(refusal ?? WrongSlot(from, to));
        }

        // Uses a consumable straight from the screen, with the same refusals as in hand ("Health full").
        void Use(ItemInstance item)
        {
            if (item.Definition is not ConsumableDefinition consumable || Player is not IConsumer consumer)
                return;
            string refusal = consumer.CannotUse(consumable);
            if (refusal != null)
            {
                ShowMessage(refusal);
                return;
            }
            consumer.Use(consumable);
            HeldConsumable.SpendUse(item);
        }

        void CancelDrag()
        {
            if (_dragFrom >= 0 && _dragFrom < _slots.Count)
                _slots[_dragFrom].View.SetDragged(false);
            _dragFrom = -1;
            if (_dragIcon != null)
                _dragIcon.enabled = false;
            _inspector?.SetPreview(null);
        }

        void OnHoverChanged(ISlotView view) => RefreshTooltip();

        internal static string RarityName(ItemRarity rarity) => (int)rarity < RarityNames.Length ? RarityNames[(int)rarity].Text() : rarity.ToString();

        void RefreshTooltip()
        {
            if (_tooltip == null)
                return;
            foreach (SlotRef slot in _slots)
            {
                if (!slot.View.IsHovered || _dragFrom >= 0)
                    continue;
                ItemInstance item = ItemAt(slot);
                if (item == null || IsHidden(slot))
                    break;
                ItemDefinition definition = item.Definition;
                string weight = definition.Stacks
                    ? StackWeight.Format(item.Count, definition.Weight, item.Weight)
                    : Kilograms.Format(definition.Weight);
                string uses = item is ConsumableInstance multiUse ? "\n" + UsesFormat.Format(multiUse.UsesLeft, multiUse.Consumable.Uses) : null;
                string locked = item.Locked
                    ? $"\n<color=#{ColorUtility.ToHtmlStringRGB(_style != null ? _style.LockedColor : Color.white)}>{LockedText.Text()}</color>"
                    : null;
                string details = $"{RarityName(definition.Rarity)}\n{weight}{uses}\n{ValueFormat.Format(item.Value)}{PartDetails(item)}{locked}";
                Color title = _style != null ? _style.RarityTextColor(definition.Rarity) : Color.white;
                string hint = slot.IsPart ? null : IsLoot(slot) ? _hintLoot : _hintCarried;
                _tooltip.Show(slot.View.Rect, definition.DisplayName, details, title, hint);
                return;
            }
            _tooltip.Hide();
        }

        // A gun part's slot and what it changes ("Aim time -10%"), for the tooltip.
        static string PartDetails(ItemInstance item)
        {
            if (item.Definition is not AttachmentDefinition part)
                return null;
            var text = new System.Text.StringBuilder("\n").Append(ItemInspectorView.SlotName(part.Slot));
            foreach (GunStatModifier modifier in part.Modifiers)
                text.Append('\n').Append(ItemInspectorView.StatNames[(int)modifier.Stat].Text()).Append(' ').Append(modifier.Percent.ToString("+0;-0")).Append('%');
            return text.ToString();
        }

        void SortBackpack() => _inventory?.Backpack.Sort();

        void TakeAll()
        {
            if (_loot == null)
                return;
            bool full = false;
            bool took = false;
            for (int i = 0; i < _loot.Items.Count; i++)
                if (_loot.Items[i] != null && !_loot.IsHidden(i))
                {
                    int count = _loot.Items[i].Count;
                    int taken = _inventory.TakeFrom(_loot.Items, i);
                    full |= taken < count;
                    took |= taken > 0;
                }
            if (took)
                Play(_style != null ? _style.PlaceSound : default);
            if (full)
                ShowMessage(BackpackFull.Text());
        }
    }
}
