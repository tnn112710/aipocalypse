# Equipment

The hotbar, items and switching between what's in the player's hands. Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). The hotbar HUD: [UI.md](UI.md#hotbar).

Modelled on Escape from Duckov's quick bar: a row of numbered slots along the bottom, guns and a melee weapon first, then room for consumables and tools. Today there are 3 slots (primary gun, secondary gun, melee) with dummy models and no shooting.

## Controls

| Input | Action | Binding (`InputSystem_Actions`, Player map) |
| --- | --- | --- |
| `Slot1`…`Slot9` | Select that slot | Keys 1–9 |
| `Next` / `Previous` | Next / previous slot that has an item, wrapping | Mouse wheel down / up, d-pad right / left |

Key labels on the HUD come from the bindings (`InputService.GetSlotKeyLabel`), so they follow rebinding. Selecting an empty slot or the current slot does nothing.

## Layers

```text
Data (ScriptableObjects, Data/Items/, Data/Equipment/)
  ItemDefinition                 name, icon, Category                      abstract
  └─ EquippableDefinition        HeldPrefab, EquipTime, HolsterTime        abstract: held in hand when selected
     ├─ GunDefinition            Category Gun          (WeaponSystem)
     └─ MeleeDefinition          Category Melee        (WeaponSystem)
  EquipmentConfig                SwitchSpeed, slots (name, accepted categories, starting item)

Rules (plain C#, EditMode-tested)
  ItemInstance                   one owned item; per-copy state (ammo, durability, count) goes in subclasses
  Hotbar                         ≤ 9 slots, category check, SlotChanged event, FindNext for cycling
  EquipmentSwitcher              Held / Target / Raise 0..1 / Phase; HeldChanged, BecameReady events
  Equipment                      selected slot ↔ switcher target; Select, Cycle; SelectionChanged event

Views
  HeldItem                       root of an item's first-person prefab; subclass for fire / swing / use
  EquipmentRig                   spawns one HeldItem per hotbar item, shows the held one, poses the hand from Raise
  HotbarHud → HotbarSlotView     HUD, event-driven (see UI.md)
```

`Player` owns the `Equipment` (built from `EquipmentConfig` in `Construct`), feeds it from `PlayerIntent.SelectSlot` / `CycleSlot`, and ticks it and the rig last in its `Update`. Nothing else ticks.

## Switching

`Raise` is how far the held item is up: 1 = ready, 0 = out of view.

```text
select another slot ─> Holstering: Raise 1 → 0 over the old item's HolsterTime
                       at 0: Held = Target (HeldChanged: rig hides old model, shows new)
                       Equipping: Raise 0 → 1 over the new item's EquipTime
                       at 1: Ready (BecameReady)
```

- **Change of mind mid-switch** turns around instead of queueing. Picking the held item again while holstering raises it back from where it is. Picking a third item while equipping holsters the half-raised one first.
- **Durations** = item time ÷ `EquipmentConfig.SwitchSpeed`. A time of 0 is instant.
- **At spawn** the first slot with an item is held and ready, with no animation (`EquipFirstImmediately`).
- **Usable only when `Switcher.IsReady`.** Shooting / swinging must check it.
- **Removing the selected item** (later: dropping, inventory) holsters to empty hands and clears the selection.

### Tuning

| To change | Edit |
| --- | --- |
| Switch speed for everything | `Data/Equipment/EquipmentConfig.asset` → `SwitchSpeed` (live in Play mode) |
| One item's equip / holster time | `Data/Items/<Item>.asset` → `EquipTime` / `HolsterTime` |
| Which slots exist, what they accept, starting items | `EquipmentConfig` → `Slots` (order = keys 1…9) |
| The temporary lower/raise motion | `Player.prefab` → `CameraRoot/CameraPitch/HeldItems` → `EquipmentRig` (lowered offset, tilt, curve) |
| Where the hand sits on screen | `HeldItems/Hand` local position |
| One model's grip position | that item's held prefab → `Model` child transform |

## Temporary animation

`EquipmentRig` moves the `Hand` transform between its rest pose and a lowered, tilted pose using `Raise` through an `AnimationCurve`. It's a placeholder until there are first-person arms. To replace it, drive an Animator from the switcher instead: a `Raise` float parameter, or equip / holster clips whose lengths match `EquipTime` / `HolsterTime`. Do it in the rig or in a `HeldItem` subclass. `EquipmentSwitcher` stays as the single source of timing either way.

## Extending

- **A new item of an existing kind** (another gun): create a `Gun` / `Melee` asset (Create → Aipocalypse → Items) in `Data/Items/`, plus a held prefab: an empty root with `HeldItem` and the model as a child named `Model`. Turn off the model's shadows and remove its colliders.
- **More slots** (up to 9): add entries to `EquipmentConfig.Slots`. The HUD spawns one slot view per slot and keys 1–9 are already bound.
- **A new item kind** (bandage, drink, tool):
  1. Add a flag to `ItemCategory`.
  2. Derive a definition: from `EquippableDefinition` if it's held in hand (tools, a fishing rod), or from `ItemDefinition` if not.
  3. Give it slots in the config that accept the new category.
  4. If it doesn't sit in your hand, change what selecting it does in `Equipment.CanSelect`. For example, a bandage would play a use action instead of being held: add a `QuickUse` path there rather than branching in `Player`.
  5. Put per-copy state (stack count, charges) in an `ItemInstance` subclass returned by the definition's `CreateInstance()`.
- **Shooting / melee** (`Proto_Shooting`): subclass `HeldItem` (`GunView`, `MeleeView`) in `WeaponSystem`, with gun data on `GunDefinition` and ammo on a `GunInstance : ItemInstance`. Act only while `Switcher.IsReady`.
- **Inventory / pickups:** fill the hotbar with `Hotbar.TryPlace` / `Remove`. `EquipmentRig` spawns and despawns models, and `HotbarHud` refreshes from `SlotChanged`. `StartingItem` in the config is a stand-in until inventory or `DevStartConfig` hands out items.

## Known gaps

- The held model renders in the world camera, so it can clip into walls when you stand close. Fix it later with a separate view-model render pass or layer.
- No sound or HUD feedback for the switch itself. The slot highlight moves immediately when the selection changes.
