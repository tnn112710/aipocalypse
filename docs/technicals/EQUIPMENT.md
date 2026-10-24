# Equipment

The hotbar, items and switching between what's in the player's hands, plus the belt (one worn lantern). Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). The hotbar HUD: [UI.md](UI.md#hotbar).

Modelled on Escape from Duckov's quick bar: a row of numbered slots along the bottom, guns and a melee weapon first, then room for consumables and tools. Today there are 3 weapon slots that take any weapon (gun or melee) in any order, filled by picking weapons up. Guns shoot and the fire axe swings ([WEAPONS.md](WEAPONS.md)).

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
Data (ScriptableObjects: items in Content/<Kind>/<Item>/, Data/Equipment/)
  ItemDefinition                 name, icon, Category, Prefab, MaxStack    abstract; one prefab per item (world + hand)
                                 Selecting: what its slot key does (None, Hold, QuickUse)
  └─ EquippableDefinition        Selecting = Hold; EquipTime, HolsterTime; HoldFeel; HeldPrefab = the HeldItem on Prefab's root
     ├─ GunDefinition            Category Gun; all gun numbers (WeaponSystem, see WEAPONS.md)
     └─ MeleeDefinition          Category Melee        (WeaponSystem)
  └─ LanternDefinition           Category Lantern; worn on the belt: radius, colour, brightness, flicker (PlayerVision)
  EquipmentConfig                SwitchSpeed, BeltAccepts, slots (name, accepted categories, starting item) → CreateEquipment

Rules (plain C#, EditMode-tested)
  ItemInstance                   one owned item or stack (Count, Merge, Consume), Changed, Badge (what its slot shows);
                                 per-copy state goes in subclasses (GunInstance: rounds, jam)
  Hotbar                         ≤ 9 slots, category check, SlotChanged event, FindNext for cycling
  EquipmentSwitcher              Held / Target / Raise 0..1 / Phase; HeldChanged, BecameReady events
  Equipment                      selected slot ↔ switcher target; Select, Cycle; TryPickUp, DropSelected, Consume; SelectionChanged,
                                 QuickUseRequested events; Belt: one worn item (a BeltAccepts category), outside the hotbar

Views
  HeldItem                       on the item prefab's root, for its first-person use; subclass for fire / swing / use (HeldGun)
                                 Tick(HeldItemInput) every frame it's in hand; LateTick after the view kick is applied
                                 (spawn things that must line up with the model); ViewKick (camera offset), LookTurn (kept recoil),
                                 IsAiming, AimWeight, Zoom, Spread, StopsSprint, KeepsClear
  PoseBlender                    a held model's pose pivot easing between pose markers (HeldGun, HeldMelee), editor preview
  EquipmentRig                   spawns one HeldItem per hotbar item (world parts stripped, injected), shows the held one,
                                 poses the hand from Raise, TickHeld passes the input on with IsReady
  HotbarHud → HotbarSlotView     HUD, event-driven (see UI.md)
  WorldItem                      on the same root: the item lying in the world (IInteractable): pick up / swap; Spawn for drops
```

`Player` owns the `Equipment` (`EquipmentConfig.CreateEquipment()` in `Construct`); its `HeldItemDriver` runs it each frame: it feeds it `PlayerIntent.SelectSlot` / `CycleSlot` / `Drop`, ticks it and the rig, builds a `HeldItemInput` (fire / aim / reload + sprinting, moving, airborne) and calls `EquipmentRig.TickHeld`, applies the item's `LookTurn` and `ViewKick` to the look, its `Zoom` to the lens and its `IsAiming` to the move speed (× its `HoldFeel.AimMoveSpeedScale`), then calls the item's `LateTick` (guns spawn their shots there, so tracers start at the muzzle as drawn). Nothing else ticks.

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
- **Usable only when ready.** The rig sets `HeldItemInput.IsReady` from `Switcher.IsReady`; a gun can't fire or aim and cancels a reload while it's false.
- **Hands lowered** (`Equipment.LowerHands`, used by pose sequences such as dying): the held item is holstered but its slot stays selected; selecting another slot changes the selection only. `RaiseHands` raises the selected item again.
- **An item that leaves the hotbar while held** (dropped, swapped away) leaves the hands at once, with no holster animation. Removing the selected item clears the selection.

## Pickup, swap and drop

Slots don't have fixed roles. Each slot's `Accepts` decides what it takes, and the default config lets all three take `Gun | Melee`, so the order is the order you pick things up: an axe picked up first sits in slot 1.

| You press | When | Result |
| --- | --- | --- |
| **E** on a world item | All of it fits on a stack of the same item you carry (`MaxStack` > 1) | **Merge**: it joins that stack; the hands don't change |
| **E** on a world item | A free slot accepts it | **Store**: it goes into the first free slot that accepts it and you switch to it (the current weapon is holstered, the new one raised) |
| **E** on a world item | No free slot accepts it, and the selected slot does | **Swap**: it replaces the selected item. The old item drops where the new one lay and leaves your hands at once; the new one is raised (its EquipTime) |
| **E** on a world item | Neither | Nothing: no prompt is shown |
| **G** | Something is selected | **Drop**: the held item lands on the ground about `DropDistance` in front of you (short of walls). Hands are empty until you select another slot |

- The prompt ("[E] Pick up Rifle" / "[E] Swap for Rifle"; a merge reads "Pick up") comes from `WorldItem.TryGetPrompt`, using the same `Equipment.GetPickUpAction` the pickup uses, so the prompt always matches what E will do.
- **Looking at items:** `PlayerInteractor` sphere-casts from the eye each frame (`PlayerInteractionTuning` → `InteractRange` 2.5 m, `InteractRadius` 0.12 m, `InteractMask`). Walls block it; trigger colliders are hit. Pickups have a trigger box, so you walk through them.
- **The same item object** goes world → hotbar → world. Per-copy state (a gun's rounds and jam) survives drops and swaps. A scene-placed `WorldItem` creates its instance from `_definition` the first time it's used.
- Rules live in `Equipment` (`GetPickUpAction`, `TryPickUp`, `DropSelected`) and are covered by EditMode tests. `WorldItem` only does the world side: it destroys itself and spawns the swapped-out item.

### Belt

A lantern is worn, not held: it works whatever is in your hands. `Equipment.Belt` holds one item whose category is in `EquipmentConfig.BeltAccepts` (`Lantern`), outside the hotbar (no slot, no key, not shown on the HUD yet).

- **E** on a lantern: **Store** on an empty belt, **Swap** with the worn one otherwise (the old lantern drops where the new one lay). A full hotbar doesn't matter, and the hands don't change.
- It can't be dropped with G (that drops the held item).
- What it does: `PlayerVision` reads `Equipment.Belt` and widens the player's light in the dark ([DAYNIGHT.md → Player light](DAYNIGHT.md#player-light-in-the-dark)).
- The pickup is `Content/Items/Lantern/Lantern.prefab` (item `Content/Items/Lantern/Lantern.asset`). It's a world model only: lanterns have no HeldItem.

### One prefab per item

Each item has a single prefab (`Content/Weapons/AK47/AK47.prefab`, `Content/Weapons/FireAxe/FireAxe.prefab`), assigned to its asset's `Prefab`. The root carries both sides:

```text
AK47                 HeldGun, ItemHands                first-person side
│                    WorldItem, trigger BoxCollider    world side (_definition = the asset, _worldModel = World)
├─ Poses, PosePivot  the first-person model, grips, muzzle, animator
└─ World             inactive in the asset: the model lying on the ground
```

- **In the world** (scene-placed or dropped), `WorldItem.Awake` shows only `World` and hides every other child. The root's held components stay idle: nothing binds or ticks them.
- **In hand**, `EquipmentRig` instantiates the prefab under an inactive staging object, injects it, calls `WorldItem.StripForHand` (destroys `World`, the root colliders and the `WorldItem`), then moves it to the hand, where it wakes up. A held copy has no collider and no world model.
- The prefab is a new copy on each change of side. The item's state lives in its `ItemInstance`, never in the prefab.
- Edit the prefab in its first-person layout as before; toggle `World` on to adjust the ground pose or pickup box. Scene instances placed by the migration have `World` on and the rest off, so the scene view shows what play will. A freshly dropped-in copy shows the first-person layout in edit mode, and play fixes it.

### Tuning

| To change | Edit |
| --- | --- |
| Switch speed for everything | `Data/Equipment/EquipmentConfig.asset` → `SwitchSpeed` (live in Play mode) |
| One item's equip / holster time | `Content/<Kind>/<Item>/<Item>.asset` → `EquipTime` / `HolsterTime` |
| How heavy one item feels (walk speed while aiming, sway) | `Content/<Kind>/<Item>/<Item>.asset` → Hold feel (`AimMoveSpeedScale`, `SwayScale`) |
| How many fit in one slot | the item's asset → `MaxStack` |
| What goes on the belt | `EquipmentConfig` → `BeltAccepts` |
| Which slots exist, what they accept, starting items | `EquipmentConfig` → Slots list in the inspector (private serialized `_slots`; order = keys 1…9). Fixed roles (gun-only slot 1) = narrow that slot's `Accepts` |
| Reach and thickness of the look ray, drop distance | `Data/Player/PlayerInteractionTuning.asset` |
| How a pickup lies on the ground, its pickup box | the item prefab → `World/Model` transform, root `BoxCollider` |
| How far a lantern lights, its colour and flicker | `Content/Items/Lantern/Lantern.asset` (where its light sits: `PlayerVisionTuning` → Lantern Position) |
| The temporary lower/raise motion | `Player.prefab` → `CameraRoot/CameraPitch/HeldItems` → `EquipmentRig` (lowered offset, tilt, curve) |
| Where the hand sits on screen | `HeldItems/Hand` local position |
| One model's grip position | that item's prefab → first-person `Model` child transform |

## First-person rendering and obstacle pose

Held models use the `HeldWeapon` layer, drawn by `FirstPersonRenderFeature` (a renderer feature on `PC_Renderer`) inside the world camera's own frame, after transparents and before post-processing, so one grade covers world and gun. `PC_Renderer`'s opaque, transparent and depth-prepass layer masks leave that layer out; the world camera still culls it. World pickups remain on their original layers. `EquipmentRig` assigns the hand's layer recursively when it creates held models. Muzzle flashes use the held gun's layer; tracers, impacts and decals stay in the world. Dropped magazines use the `Debris` layer (6): guns leave it out of `HitLayers`, the player's CharacterController excludes it, and the obstacle-pose and interaction masks don't include it.

For a camera with a `FirstPersonView` (on `PlayerCamera`), the pass clears depth, then draws the layer with that view's lens: the unzoomed field of view (`PlayerZoom` sets it), a 0.01 m near plane and a 10 m far plane. Held models therefore stay in front of walls and enemies and keep their size while aiming zooms the world. Other cameras (Scene view, previews) draw the layer in world space. Held renderers don't cast shadows. This replaced a HeldWeapon-only Overlay camera, which cost about 1.1 ms CPU per frame for a second cull and render-graph pass.

`PlayerSystem/HeldItemClearance`, ticked by `Player`, adds a smooth backward offset and tilt to the hand near obstacles. It never moves the player:

- The rig first restores its normal switch pose and the item applies its normal aim/sprint/reload pose. `HeldItem.TryGetTip` measures the model's forward extent from its mesh bounds before the obstacle offset is applied, avoiding a retract/extend feedback loop.
- A sphere-cast from the eye to that tip (plus margin) finds the nearest wall, floor or entity. The player and triggers are ignored.
- Penetration determines the obstacle pose weight. The hand blends back and tilts, then smoothly returns when clear. Switching to another item resets the weight.
- Melee opts out during swings so the obstacle pose does not change the swing arc. Damage checks remain in world space.

The hand also carries the held item's sway and bob (`HeldItemSway`, applied by `EquipmentRig.ApplyHandMotion` before the obstacle pose): breathing, footstep bob, lag behind look / movement / jumps, a landing dip, all calmed by `HeldItem.AimWeight`. Rules and settings: [WEAPONS.md → Features](WEAPONS.md#features).

Settings: `PlayerHeldItemTuning` → Obstacle pose (on/off, margin, radius, retract distance, tilt, blend speed, layers). The overlay guarantees visibility even when an item is longer than the maximum retraction. Gun shots check the eye-to-muzzle segment before using a muzzle origin so a displayed barrel beyond cover cannot fire through it.

## Temporary animation

The visible hands are separate from this: `HandSystem/FirstPersonHands` puts floating hands on the held item's `HandGrip`s, so they follow this motion with no extra work ([HANDS.md](HANDS.md)).

`EquipmentRig` moves the `Hand` transform between its rest pose and a lowered, tilted pose using `Raise` through an `AnimationCurve`. It's a placeholder for equip / holster animations. To replace it, drive an Animator from the switcher instead: a `Raise` float parameter, or equip / holster clips whose lengths match `EquipTime` / `HolsterTime`. Do it in the rig or in a `HeldItem` subclass. `EquipmentSwitcher` stays as the single source of timing either way.

## Extending

- **Another gun:** follow [WEAPONS.md → Adding a gun](WEAPONS.md#adding-a-gun).
- **A new item of an existing kind** (another gun or melee weapon): follow [WEAPONS.md → Adding a gun](WEAPONS.md#adding-a-gun) or [Melee](WEAPONS.md#melee). In short: an item prefab copied from an existing one with both models swapped (first-person model: shadows off, no colliders; `World/Model`: lying flat), the root `WorldItem._definition` set to a new item asset, both in a new `Content/<Kind>/<Item>/` folder, and the prefab assigned to the asset's `Prefab`. Drop copies into scenes to place pickups.
- **More slots** (up to 9): add entries to the Slots list of `EquipmentConfig` in the inspector (private serialized `_slots`). The HUD spawns one slot view per slot and keys 1–9 are already bound.
- **A new item kind** (bandage, drink, tool):
  1. Add a flag to `ItemCategory`.
  2. Derive a definition: from `EquippableDefinition` if it's held in hand (tools, a fishing rod), or from `ItemDefinition` if not.
  3. Give it slots in the config that accept the new category.
  4. If it doesn't sit in your hand, override `Selecting` on its definition. `QuickUse` (a bandage, a drink): its key raises `Equipment.QuickUseRequested(item)` and the hands don't change; the system that uses it subscribes there and calls `Equipment.Consume(item)`, which removes an emptied stack. `None`: it's only carried.
  5. Stacking is built in: set `MaxStack` above 1 and picking up more merges onto the carried stack (its count shows on the slot). Put other per-copy state (charges, durability) in an `ItemInstance` subclass returned by the definition's `CreateInstance()`; raise `Changed` and override `Badge` to show it on the hotbar.
- **A new kind of held item** (a tool, a consumable): subclass `HeldItem` (like `HeldGun` / `HeldMelee`) and act in `Tick` only while `input.IsReady`; `UseHeld` / `UsePressed` are the use inputs. Do things that must line up with the drawn model or camera in `LateTick`. Override `ViewKick`, `LookTurn`, `IsAiming`, `AimWeight` and `Zoom` (field of view multiplier, e.g. a scope) for camera effects, `Spread` for the crosshair. Pose the model between markers with a `PoseBlender`. For hands, add `ItemHands` + `HandGrip`s to its prefab; moving hands come from the item's clips ([HANDS.md](HANDS.md)). Play sounds through `AudioService` with BroAudio `SoundID`s.
- **Inventory:** fill the hotbar with `Equipment.TryPickUp` or `Hotbar.TryPlace` / `Remove`. `EquipmentRig` spawns and despawns models, and `HotbarHud` refreshes from `SlotChanged`. `StartingItem` in the config can still hand out items at spawn.
- **Other interactables** (doors, loot, workbench): implement `IInteractable` (`_Common/Interaction`) on the object with the collider. `PlayerInteractor` and the HUD prompt pick it up with no other changes.

## Known gaps

- Dropped items are placed on the ground, not thrown: no physics. Picking up has no animation or sound.

- The overlay can make held models appear over nearby surfaces; the obstacle pose softens this, but does not simulate physical weapon collision.

- No sound or HUD feedback for the switch itself. The slot highlight moves immediately when the selection changes.
