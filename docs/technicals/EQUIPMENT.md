# Equipment

The hotbar, items and switching between what's in the player's hands. Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). The hotbar HUD: [UI.md](UI.md#hotbar).

Modelled on Escape from Duckov's quick bar: a row of numbered slots along the bottom, guns and a melee weapon first, then room for consumables and tools. Today there are 3 weapon slots that take any weapon (gun or melee) in any order, filled by picking weapons up. Dummy models, no shooting.

## Controls

| Input | Action | Binding (`InputSystem_Actions`, Player map) |
| --- | --- | --- |
| `Slot1`…`Slot9` | Select that slot | Keys 1–9 |
| `Next` / `Previous` | Next / previous slot that has an item, wrapping | Mouse wheel down / up, d-pad right / left |
| `Interact` | Pick up / swap for the item you're looking at | E, gamepad north |
| `Drop` | Drop the held item in front of you | G |

Key labels on the HUD come from the bindings (`InputService.GetSlotKeyLabel`), so they follow rebinding. Selecting an empty slot or the current slot does nothing.

## Layers

```text
Data (ScriptableObjects, Data/Items/, Data/Equipment/)
  ItemDefinition                 name, icon, Category, WorldPrefab         abstract
  └─ EquippableDefinition        HeldPrefab, EquipTime, HolsterTime        abstract: held in hand when selected
     ├─ GunDefinition            Category Gun          (WeaponSystem)
     └─ MeleeDefinition          Category Melee        (WeaponSystem)
  EquipmentConfig                SwitchSpeed, slots (name, accepted categories, starting item)

Rules (plain C#, EditMode-tested)
  ItemInstance                   one owned item; per-copy state (ammo, durability, count) goes in subclasses
  Hotbar                         ≤ 9 slots, category check, SlotChanged event, FindNext for cycling
  EquipmentSwitcher              Held / Target / Raise 0..1 / Phase; HeldChanged, BecameReady events
  Equipment                      selected slot ↔ switcher target; Select, Cycle; TryPickUp, DropSelected; SelectionChanged event

Views
  HeldItem                       root of an item's first-person prefab; subclass for fire / swing / use
  EquipmentRig                   spawns one HeldItem per hotbar item, shows the held one, poses the hand from Raise
  HotbarHud → HotbarSlotView     HUD, event-driven (see UI.md)
  WorldItem                      an item lying in the world (IInteractable): pick up / swap; Spawn for drops
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
- **An item that leaves the hotbar while held** (dropped, swapped away) leaves the hands at once, with no holster animation. Removing the selected item clears the selection.

## Pickup, swap and drop

Slots don't have fixed roles. Each slot's `Accepts` decides what it takes, and the default config lets all three take `Gun | Melee`, so the order is the order you pick things up: an axe picked up first sits in slot 1.

| You press | When | Result |
| --- | --- | --- |
| **E** on a world item | A free slot accepts it | **Store**: it goes into the first free slot that accepts it and you switch to it (the current weapon is holstered, the new one raised) |
| **E** on a world item | No free slot accepts it, and the selected slot does | **Swap**: it replaces the selected item. The old item drops where the new one lay and leaves your hands at once; the new one is raised (its EquipTime) |
| **E** on a world item | Neither | Nothing: no prompt is shown |
| **G** | Something is selected | **Drop**: the held item lands on the ground about `DropDistance` in front of you (short of walls). Hands are empty until you select another slot |

- The prompt ("[E] Pick up Rifle" / "[E] Swap for Rifle") comes from `WorldItem.TryGetPrompt`, using the same `Equipment.GetPickUpAction` the pickup uses, so the prompt always matches what E will do.
- **Looking at items:** `PlayerInteractor` sphere-casts from the eye each frame (`PlayerTuning` → `InteractRange` 2.5 m, `InteractRadius` 0.12 m, `InteractMask`). Walls block it; trigger colliders are hit. Pickups have a trigger box, so you walk through them.
- **The same item object** goes world → hotbar → world. Per-copy state (ammo later) survives drops and swaps. A scene-placed `WorldItem` creates its instance from `_definition` the first time it's used.
- Rules live in `Equipment` (`GetPickUpAction`, `TryPickUp`, `DropSelected`) and are covered by EditMode tests. `WorldItem` only does the world side: it destroys itself and spawns the swapped-out item.

### Tuning

| To change | Edit |
| --- | --- |
| Switch speed for everything | `Data/Equipment/EquipmentConfig.asset` → `SwitchSpeed` (live in Play mode) |
| One item's equip / holster time | `Data/Items/<Item>.asset` → `EquipTime` / `HolsterTime` |
| Which slots exist, what they accept, starting items | `EquipmentConfig` → `Slots` (order = keys 1…9). Fixed roles (gun-only slot 1) = narrow that slot's `Accepts` |
| Reach and thickness of the look ray, drop distance | `Data/Player/PlayerTuning.asset` → Interaction |
| How a pickup lies on the ground, its pickup box | `Dummy*Pickup.prefab` → `Model` transform, root `BoxCollider` |
| The temporary lower/raise motion | `Player.prefab` → `CameraRoot/CameraPitch/HeldItems` → `EquipmentRig` (lowered offset, tilt, curve) |
| Where the hand sits on screen | `HeldItems/Hand` local position |
| One model's grip position | that item's held prefab → `Model` child transform |

## Temporary animation

`EquipmentRig` moves the `Hand` transform between its rest pose and a lowered, tilted pose using `Raise` through an `AnimationCurve`. It's a placeholder until there are first-person arms. To replace it, drive an Animator from the switcher instead: a `Raise` float parameter, or equip / holster clips whose lengths match `EquipTime` / `HolsterTime`. Do it in the rig or in a `HeldItem` subclass. `EquipmentSwitcher` stays as the single source of timing either way.

## Extending

- **A new item of an existing kind** (another gun): create a `Gun` / `Melee` asset (Create → Aipocalypse → Items) in `Data/Items/`, plus a held prefab: an empty root with `HeldItem` and the model as a child named `Model`. Turn off the model's shadows and remove its colliders. Also make a world prefab: a root with `WorldItem` (`_definition` = the asset) and a trigger `BoxCollider`, with the model lying flat as a child. Assign it to the asset's `WorldPrefab`, and drop copies into scenes to place pickups.
- **More slots** (up to 9): add entries to `EquipmentConfig.Slots`. The HUD spawns one slot view per slot and keys 1–9 are already bound.
- **A new item kind** (bandage, drink, tool):
  1. Add a flag to `ItemCategory`.
  2. Derive a definition: from `EquippableDefinition` if it's held in hand (tools, a fishing rod), or from `ItemDefinition` if not.
  3. Give it slots in the config that accept the new category.
  4. If it doesn't sit in your hand, change what selecting it does in `Equipment.CanSelect`. For example, a bandage would play a use action instead of being held: add a `QuickUse` path there rather than branching in `Player`.
  5. Put per-copy state (stack count, charges) in an `ItemInstance` subclass returned by the definition's `CreateInstance()`.
- **Shooting / melee** (`Proto_Shooting`): subclass `HeldItem` (`GunView`, `MeleeView`) in `WeaponSystem`, with gun data on `GunDefinition` and ammo on a `GunInstance : ItemInstance`. Act only while `Switcher.IsReady`.
- **Inventory:** fill the hotbar with `Equipment.TryPickUp` or `Hotbar.TryPlace` / `Remove`. `EquipmentRig` spawns and despawns models, and `HotbarHud` refreshes from `SlotChanged`. `StartingItem` in the config can still hand out items at spawn.
- **Other interactables** (doors, loot, workbench): implement `IInteractable` (`_Common/Interaction`) on the object with the collider. `PlayerInteractor` and the HUD prompt pick it up with no other changes.

## Known gaps

- Dropped items are placed on the ground, not thrown: no physics. Picking up has no animation or sound.

- The held model renders in the world camera, so it can clip into walls when you stand close. Fix it later with a separate view-model render pass or layer.
- No sound or HUD feedback for the switch itself. The slot highlight moves immediately when the selection changes.
