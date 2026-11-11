# Equipment

Everything the player carries and holds: the inventory (hotbar, backpack, gear), items and their world pickups, switching what's in the hands, looting crates and bodies, the Token magnet and throwing items away. Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). The hotbar HUD, inventory screen, loot markers and pickup feed: [UI.md](UI.md#hotbar).

Modelled on Escape from Duckov: slots (one item or stack each, no grid), two active guns, a melee slot and quick slots on number keys, a backpack whose size comes from the worn bag, item weights, cash (Tokens) as an item, loot containers and bodies searched item by item, an inventory screen with drag and drop (paused, except during a raid). Guns shoot and the fire axe swings ([WEAPONS.md](WEAPONS.md)); the hammer repairs fences ([DEFENSE.md → Repair](DEFENSE.md#repair)).

## Controls

| Input | Action | Binding (`InputSystem_Actions`) |
| --- | --- | --- |
| `Slot1`, `Slot2` | Select the two gun slots | Keys 1, 2 |
| `Melee` | Select the melee slot | V |
| `Slot3`…`Slot9` | Select a quick slot (consumables, tools) | Keys 3–9 |
| `Next` / `Previous` | Next / previous slot that has a holdable item, wrapping | Mouse wheel down / up, d-pad right / left |
| `Interact` | Pick up the item you look at; search / open a crate or body | E, gamepad north |
| `Drop` | Throw the held item (the whole stack) forward | G |
| `Inventory` (Player and UI maps) | Open / close the inventory screen | Tab, gamepad select |
| UI `Cancel` | Close the inventory screen | Esc |

Which slot a key selects is data: each hotbar slot in `EquipmentConfig` has a `HotbarKey` (Key1…Key9, Melee). A third gun slot later (keys 1, 2, 3) is a config change: add a Gun slot on Key3 and move the quick slots to 4–9. Key labels on the HUD come from the bindings (`InputService.GetKeyLabel(HotbarKey)`), so they follow rebinding. Selecting an empty slot or the current slot does nothing.

## Layers

```text
Data (ScriptableObjects: items in Content/<Kind>/<Item>/, Data/Equipment/, Data/Loot/)
  ItemDefinition                 name, icon, Category, Prefab, MaxStack (or unlimited), Weight (kg / unit), AutoCollect,
                                 Value (Tokens / unit), Rarity (Common … Legendary)
                                 Selecting: what its slot key does (None, Hold, QuickUse); abstract; one prefab per item
  ├─ EquippableDefinition        Selecting = Hold; EquipTime, HolsterTime; HoldFeel; HeldPrefab = the HeldItem on Prefab's root
  │  ├─ GunDefinition            Category Gun (WeaponSystem, WEAPONS.md)
  │  ├─ MeleeDefinition          Category Melee (WeaponSystem)
  │  ├─ RepairToolDefinition     Category Tool: the hammer (PlayerSystem/Repair)
  │  └─ ConsumableDefinition     Category Consumable: first aid kit, bandage, shots; UseSeconds, Uses (above 1: one item
  │                              with uses left, a ConsumableInstance), use move speed; Heal, HealOverTime, buff scales
  ├─ AmmoType                    Category Ammo (WeaponSystem): rounds, carried as stacks; Prefab = the ammo box
  ├─ CurrencyDefinition          Category Currency: Tokens (unlimited stack, weight 0, auto-collected)
  ├─ BackpackDefinition          Category Backpack: ExtraSlots, ExtraWeight while worn
  └─ LanternDefinition           Category Lantern: worn in the Light gear slot (PlayerVision)
  EquipmentConfig                SwitchSpeed; hotbar slots (name, accepts, key, starting item); gear slots (name, accepts,
                                 starting item); backpack slots; max weight; starting items → CreateInventory, CreateEquipment
  LootTable                      weighted drops (item + amount range), rolled into a body's LootContainer

Rules (plain C#, EditMode-tested)
  ItemInstance                   one owned item or stack: Count, SpaceLeft, Weight, Merge / MergeFrom / Split / Consume, Changed,
                                 Badge; per-copy state in subclasses (GunInstance: rounds, jam)
  ItemContainer                  a row of slots: per-slot accepted categories (hotbar, gear) or anything (backpack, loot) with
                                 Resize; TryAdd (stacks first, then free slots, partial), Take / CountOf / SpaceFor, Weight, Sort,
                                 Move (move / split / merge / swap); an emptied stack leaves its slot
  Inventory                      Hotbar + Backpack + Gear; TryAdd routing, AddNew, CountOf / Take, Weight / MaxWeight; the worn
                                 backpack sizes the backpack; TryMove / TryRemove / SendTo never strand an item; TakeFrom (loot)
  EquipmentSwitcher              Held / Target / Raise 0..1 / Phase; HeldChanged, BecameReady events
  Equipment                      Inventory + the hands: selected hotbar slot ↔ switcher target, keys, slot names; Select, Cycle;
                                 TryPickUp (PickedUp / PickUpBlocked events), DropHeld, Consume; Belt (the worn lantern)
  PlayerLooting                  opens a crate or body and reveals what's inside one stack at a time (PlayerSystem)
  ItemMagnet                     auto-collected items fly to the player (PlayerSystem)

Views
  HeldItem                       on the item prefab's root, for its first-person use; subclass for fire / swing / use (HeldGun);
                                 a plain HeldItem just shows the model; HeldConsumable uses a consumable (hold attack)
  PoseBlender                    a held model's pose pivot easing between pose markers (HeldGun, HeldMelee), editor preview
  EquipmentRig                   spawns one HeldItem per hotbar item (world parts stripped, injected), shows the held one,
                                 poses the hand from Raise, TickHeld passes the input on with IsReady
  HotbarHud, InventoryScreen,    HUD and the inventory screen, event-driven (see UI.md)
  LootMarkerHud, PickupFeedHud
  WorldItem                      on the item prefab's root: the item lying in the world (IInteractable); Spawn, Throw
  LootContainer                  something to search: a crate, an enemy body (IInteractable); hidden slots until revealed
  LootRegistry                   enabled, injected loot containers (root scope, EquipmentInstaller): for the loot markers
```

## Inventory

`Player` builds it from `Data/Equipment/EquipmentConfig.asset` (`CreateEquipment` → `Equipment` over an `Inventory`), then the `AmmoReserve` over that inventory ([WEAPONS.md](WEAPONS.md)), before the rig binds.

| Container | Slots | Takes |
| --- | --- | --- |
| Hotbar (active slots) | Firearm (key 1), Firearm (key 2), Melee (V), Quick 3–9 | Gun, Gun, Melee, then Consumable or Tool. Only two guns are active; others ride in the backpack |
| Backpack | 10 without a bag, + the worn bag's `ExtraSlots` (the test backpack: +15) | Anything |
| Gear | Head, Body, Face, Backpack, Light | One of its category each. No head, body or face items exist yet: the slots show a silhouette |

- **Where a new item goes** (`Inventory.TryAdd`, used by pickups, loot and `AddNew`): onto its own stacks (hotbar, then backpack); a gear item into its free gear slot (a backpack is worn at once, a lantern goes on the Light slot); a gun or melee weapon into a free active slot; everything else into the backpack, then any free hotbar slot that takes it. Tools and consumables (the hammer, medic boxes) go to the backpack: drag them to a quick slot. What doesn't fit stays on the item (a partial pickup leaves the rest on the ground).
- **Moving** (the inventory screen): `Inventory.TryMove` = `ItemContainer.Move` (into an empty slot, onto the same stackable item as far as it fits, or a whole-stack swap when both slots take the other item), plus one rule: the worn backpack can't come off, or be swapped for a smaller one, while a slot it adds holds an item, and it can't go into one of its own slots. The refusal is shown on the screen (`Inventory.BackpackNotEmpty`).
- **The hands follow the item**: moving the held gun to the other gun slot keeps it in hand (the selection follows); moving it to the backpack empties the hands at once.
- **Weight**: every item has `Weight` per unit (rounds 0.012–0.04 kg, Tokens 0, first aid kit 0.5, bandage 0.05, shots 0.2, guns 0.9–3.6, axe 1.8, hammer 0.6, lantern 0.8, backpack 1). `Inventory.Weight` / `MaxWeight` (25 kg + the worn bag's `ExtraWeight`) are shown on the inventory screen. Nothing slows the player yet.
- **Tokens** are an item (`CurrencyDefinition`): one unlimited stack in the backpack, weight 0, picked up by the magnet. Dropped, the coin floats about 0.35 m above the ground, glowing (`Token_Coin.mat`, emissive) inside a soft pulsing gold halo (one additive particle billboard, paused off screen), so it reads on grass and at night; its pickup box reaches down to the ground (`InventorySetup.Tokens`). No bank balance yet; the screen shows the Tokens carried.
- **Starting items**: `EquipmentConfig` slot / gear `StartingItem`s and its starting items, plus `AmmoConfig`'s starting rounds (put in the backpack). Death keeps the inventory for now.

## Using consumables

Consumables sit in quick slots (keys 3–9). Selecting one holds it; a fresh press of the attack button starts using it and holding it for `UseSeconds` finishes (`HeldConsumable`). Meanwhile the walk is scaled by `UseMoveSpeedScale` (`HeldItem.MoveSpeedScale`, applied in `HeldItemDriver`) and sprint is off. Letting go, switching away or a hit that hurts (`IConsumer.LastHurtTime`) cancels: nothing is spent. A ring under the crosshair shows the progress (`ItemUseHud`, UI.md).

- **Effects** go to the holder through `IConsumer` (the player): `PlayerEffects` heals at once, heals over time and holds stat buffs. It's the player's `IStatProvider` over the base one, so the motor, stamina and every other reader get buffed values; a buff that starts or ends raises `Changed`. The same item again restarts its buff, or adds its healing to what's left over a fresh duration. A hit pauses healing over time for `PlayerVitalsTuning.HealPauseOnHit` (3 s); death clears everything. `EffectsHud` shows what's running.
- **Refusals**: an item whose only effect is health can't be used at full health ("Health full"), nor while dead or in a pose sequence.
- **Multi-use items** (`Uses` > 1, Max Stack 1) are a `ConsumableInstance`: the slot badge shows the uses left, each use spends one, the last removes the item, and its value falls with the uses.

| Item | Use | Effect | Stack, weight | Value, rarity |
| --- | --- | --- | --- | --- |
| First Aid Kit (`MedicBox`) | 3 s, walk ×0.5 | +20 HP, 5 uses | 1, 0.5 kg | 120, Uncommon |
| Bandage | 1.5 s, ×0.6 | +15 HP over 10 s | 5, 0.05 kg | 15, Common |
| Adrenaline | 1 s, ×0.7 | Walk and sprint speed ×1.25 for 60 s | 3, 0.2 kg | 80, Rare |
| Stamina Shot | 1 s, ×0.7 | Stamina regen ×2, sprint stamina cost ×0.5 for 60 s | 3, 0.2 kg | 60, Uncommon |

All four are placeholders built by `InventorySetup.Consumables`: models from the Animpic Medical Kit, icons from the Kat Grabowska flat set (ASSETS.md), held poses not tuned.

## Value and rarity

Every item has a `Value` in Tokens per unit and a `Rarity` (`InventorySetup.Values`; placeholder numbers): Tokens 1, rounds 2 / 3 / 5, bandage 15, stamina shot 60, adrenaline 80, hammer 80, lantern 100, first aid kit 120, fire axe 150, backpack 150, pistol 300, double barrel 400, pump shotgun 500, MP5 600, AK47 900. `ItemInstance.Value` is value × count (a multi-use item: × uses left / uses). The inventory screen draws a border in the rarity's colour (none for Common) and the tooltip shows rarity, weight, uses and value (UI.md). Nothing buys or sells yet: selling waits for the trading robot.

## Pickup and drop

| You press | When | Result |
| --- | --- | --- |
| **E** on a world item | There's room for all of it | It goes where `Inventory.TryAdd` puts it; a gun or melee weapon stored in an active slot is raised (switch). The pickup feed shows "+30 Rifle Ammo" |
| **E** on a world item | Room for part of a stack | That part is taken, the rest stays on the ground; the feed shows what was taken and "Backpack full" |
| **E** on a world item | No room | Nothing is taken; "Backpack full" |
| **G** | Something is in your hands | **Throw**: the held item (the whole stack) flies forward and lands. Mid-switch that's the item being put away |
| Drag out of the inventory screen | A slot holds something | The stack (or half with Shift) is thrown the same way |

- The prompt is "[E] Pick up Rifle Ammo (30)" (`WorldItem.TryGetPrompt`; the count only for stacks, rebuilt only when it changes). It shows whenever the player could carry the item; a full inventory is reported on the key press.
- **Looking at items:** `PlayerInteractor` sphere-casts from the eye each frame (`PlayerInteractionTuning` → `InteractRange` 2.5 m, `InteractRadius` 0.12 m, `InteractMask`). Walls block it; trigger colliders are hit. Pickups have a trigger box (at least 30 cm a side), so you walk through them.
- **The same item object** goes world → inventory → world. Per-copy state (a gun's rounds and jam) survives drops. A scene-placed `WorldItem` creates its instance from `_definition` and `_count` the first time it's used.
- **Throwing** (`PlayerInteractor.Throw`): spawned 0.5 m in front of the eye (short of a wall), velocity forward × `ThrowSpeed` + up × `ThrowLift`. `WorldItem.Throw` adds a Rigidbody (rotation frozen) and a solid box collider on the **Item** layer (Item collides with Default, where the terrain is; Debris and Ignore Raycast don't, so items there fall through the ground), ignoring the thrower's capsule. It settles (rigidbody and collider removed, `Update` off again) once asleep or after 6 s.

### Pickup magnet

`ItemMagnet` (ticked by `Player`): every 0.1 s an `OverlapSphereNonAlloc` around the player's feet (`MagnetRadius` 1 m, `PickupMask` = Default; non-trigger hits skipped before any lookup) finds pickup boxes of `AutoCollect` items (Tokens) with room in the inventory; they fly to the chest at `MagnetSpeed` and are collected (`WorldItem.Collect`). The radius reaches the edge of the pickup box. A **thrown** item (the interactor's `Thrown` event) waits until it has landed and the player has been more than radius + 0.5 m from it once, so dropping tokens doesn't pull them straight back; walking up to tokens that landed out of reach collects them. A bigger radius later is a tuning value.

## Looting

`LootContainer` (on a crate root, or an enemy root) holds an `ItemContainer` of `_capacity` slots: scene contents (`_contents`) or a body's roll (`Fill`). Everything inside starts **hidden**. Prompt "[E] Search Crate" while something is hidden, "[E] Open Crate" once all of it has been seen.

- **E** (`PlayerLooting.Begin`) opens the inventory screen at once with the container on the right, after Duckov. Hidden slots show an "unknown" mark; one stack is revealed every `SearchSecondsPerItem` (1 s), top to bottom, with a ring filling on the slot being revealed and a rustle (`Loot_Reveal`). The screen ticks the search in real time, since it may pause the game.
- A hidden item can't be dragged, right-clicked, merged into, swapped, hovered or taken by Take all ("Still searching"). Revealed items can be taken (drag, right-click, Take all) or put in.
- Closing the screen stops the search: reopening carries on with what's still hidden, and the stack that was half revealed starts over.
- **Pause:** the screen pauses the game at home. **During a raid** (between `RaidStarted` and `RaidEnded`) it doesn't: the world goes on, and a hit that lands on the player closes the screen. Looting a body mid-fight is a risk, not a safe pause. Dying closes it too.
- **Markers:** `LootMarkerHud` floats a bag icon over the nearest 8 containers within 15 m that hold loot (`LootContainer.HoldsLoot`): bright while something inside is hidden, dim once all of it has been seen, none once empty. It also hides for the container the player is looking at, since the prompt is showing. A container needs injection to register in `LootRegistry` (root scope). Enemy bodies get it through their `EntityScope`. A scene-placed crate must be listed in the scene scope's **Auto Inject Game Objects** (`InventorySetup.InjectCrates` does it for the yard). The marker floats `_markerHeight` (1 m) above the search collider's centre, or above the pivot.
- **Bodies**: an enemy kind's `EnemyDefinition.Loot` (`Data/Loot/GoblinLoot.asset`: 1–2 picks; nothing 30, Tokens 5–20 (40), pistol rounds 6–12 (20), rifle 10–20 (15), shells 2–4 (10), medic box (5)) is rolled into its `LootContainer` on death (`RandomService` "loot" stream). A body with loot lies until emptied (its `LootSearch` trigger on, prompt "Search Goblin"), then sinks `LootedBodySinkDelay` (3 s) later; a body that rolled nothing sinks as before. At most `MaxLootBodies` (20) lie with loot: past that the oldest sinks with its loot; all of them sink at Dawn. Numbers in `Data/Enemies/EnemySystemSettings.asset`.
- **Performance**: a body lying with loot is out of the enemy registry (no thinking, no animation tick) and its Animator is switched off once it has settled (CorpseTime), so lying bodies cost no animation time. Each still holds its pool instance, so raids prewarm `AliveCap` + `CorpseHeadroom` (20) goblins (`Data/Raids/RaidSettings.asset`) and don't instantiate mid-fight. The loot markers rescan the registry 5 times a second (no allocation) and only move the shown markers each frame, on their own canvas.

### Performance pass (2026-11-10)

Measured with `AgentScripts/Inventory/InventoryPerf.cs` (a development build of WindowsDevelopment with the Pipeline server) and editor Play mode:

- **Build** (IL2CPP development, 1920×1080 windowed, idle in the yard): 133–179 fps, CPU 6–8 ms, GPU 1.4–1.7 ms. The scenarios couldn't be driven inside the build: `script eval` in an IL2CPP player only sees the types the game references (no `QualitySettings`, `SystemInfo`, `ProfilerRecorder`) and then stopped compiling at all, and simulated keys don't type into the dev console.
- **Editor**: 20 bodies lying with loot (all 20 Animators off) and their markers cost nothing measurable against the empty yard. Animation stays at ~0.05 ms, and script Update + LateUpdate stay under 1.1 ms in total.
- **Per call** (editor, Mono): `LootMarkerHud.LateUpdate` 8.6 µs with 5 markers shown out of 21 containers; `Scan` 14 µs, 5 times a second; `InventoryScreen.Refresh` ~50 µs; `Rebuild` 0.43 ms (was 5 ms before each grid kept its own views); the whole open call ~3 ms, including enabling the EventSystem and switching input maps.
- **Garbage**: a Profiler capture of 60 frames shows no allocation from the inventory, loot or marker code. The player loop's ~3 KB/frame comes from URP's render graph and an IMGUI repaint. The build reported ~55 KB/frame at idle (`runtime status`). That doesn't come from the player loop in the editor capture, so it's probably the Pipeline server or development tooling. Check it in a distribution build.

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

## One prefab per item

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

## Tuning

| To change | Edit |
| --- | --- |
| Switch speed for everything | `Data/Equipment/EquipmentConfig.asset` → `SwitchSpeed` (live in Play mode) |
| One item's equip / holster time | `Content/<Kind>/<Item>/<Item>.asset` → `EquipTime` / `HolsterTime` |
| How heavy one item feels (walk speed while aiming, sway) | `Content/<Kind>/<Item>/<Item>.asset` → Hold feel (`AimMoveSpeedScale`, `SwayScale`) |
| How many fit in one slot, unlimited stacks | the item's asset → `MaxStack`, `UnlimitedStack` |
| An item's weight, whether the magnet takes it | the item's asset → `Weight` (kg per unit), `AutoCollect` |
| Which hotbar slots exist, what they take, their keys, starting items | `EquipmentConfig` → Slots (private serialized `_slots`, in display order; each with a `HotbarKey`) |
| Gear slots, backpack size without a bag, carry weight | `EquipmentConfig` → Gear, Backpack Slots, Max Weight |
| What a backpack adds | its asset (`Content/Items/Backpack/Backpack.asset`) → `ExtraSlots`, `ExtraWeight` |
| Reach and thickness of the look ray; throw speed and lift; magnet radius, speed and layer; search seconds per stack | `Data/Player/PlayerInteractionTuning.asset` |
| Inventory sounds: open, close, place, refused, reveal, pickup, Tokens (2D, Player library) | `Data/UI/InventoryStyle.asset` → Sounds |
| Consumables: use time, uses, walk speed while using, heal, heal over time, buff seconds and scales | each `ConsumableDefinition` (`Content/Items/<Item>/`) |
| Seconds a hit pauses healing over time | `Data/Player/PlayerVitalsTuning.asset` → Health |
| Item value and rarity; rarity colours and border alpha | each item's definition; `Data/UI/InventoryStyle.asset` → Rarity |
| Loot markers: range, count, colours, size far away | HUD.prefab → LootMarkers (`LootMarkerHud`) |
| What goblins drop, how many picks, the chance of nothing | `Data/Loot/GoblinLoot.asset` (any kind: its `EnemyDefinition.Loot`) |
| How many bodies lie with loot, how long an emptied one lies | `Data/Enemies/EnemySystemSettings.asset` → Max Loot Bodies, Looted Body Sink Delay |
| A crate's name, size and contents | the crate instance → `LootContainer` (`Content/Props/LootCrate/LootCrate.prefab` for the defaults) |
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
- **More slots**: add entries to the Slots (hotbar) or Gear list of `EquipmentConfig`, each hotbar slot with its own `HotbarKey` (keys 1–9 and V are bound). The HUD and the inventory screen spawn one view per slot.
- **A third active gun** (an upgrade later): a Gun slot on Key3 after the first two, the quick slots moved to 4–9. A runtime upgrade would rebuild the hotbar from a second config or add the slot; nothing else assumes two.
- **A new item** of an existing kind (Tokens, ammo, a consumable, a backpack): an asset of that definition type (Create → Aipocalypse → Items / Weapons) with name, icon, stack, weight, value, rarity, and a pickup prefab (root `WorldItem` + trigger box, `World/Model`), as `AgentScripts/Inventory/InventorySetup.Items` / `Consumables` build them. Held ones also need a `HeldItem` (a consumable: `HeldConsumable`) and a first-person model on the root.
- **Item icons** come from the UI icon packs in `Assets/_StoreAssets/UI`, one set and style for items of a kind (user's preference, 2026-11-10; ASSETS.md → UI). The consumables use the Kat Grabowska flat set.
- **A new item kind** (bandage, drink, tool, armour):
  1. Add a flag to `ItemCategory`.
  2. Derive a definition: from `EquippableDefinition` if it's held in hand (tools, a fishing rod), or from `ItemDefinition` if not.
  3. Give it slots in the config that accept the new category (a quick slot, a gear slot), or none: it then lives in the backpack.
  4. If it doesn't sit in your hand, override `Selecting` on its definition. `QuickUse` (a bandage, a drink): its key raises `Equipment.QuickUseRequested(item)` and the hands don't change; the system that uses it subscribes there and calls `Equipment.Consume(item)`, which removes an emptied stack. `None`: it's only carried.
  5. Stacking is built in: set `MaxStack` above 1 and picking up more merges onto the carried stack (its count shows on the slot). Put other per-copy state (charges, durability) in an `ItemInstance` subclass returned by the definition's `CreateInstance()`; raise `Changed` and override `Badge` to show it on the hotbar.
- **A new kind of held item** (a tool, a consumable): subclass `HeldItem` (like `HeldGun` / `HeldMelee`) and act in `Tick` only while `input.IsReady`; `UseHeld` / `UsePressed` are the use inputs. Do things that must line up with the drawn model or camera in `LateTick`. Override `ViewKick`, `LookTurn`, `IsAiming`, `AimWeight` and `Zoom` (field of view multiplier, e.g. a scope) for camera effects, `Spread` for the crosshair. Pose the model between markers with a `PoseBlender`. For hands, add `ItemHands` + `HandGrip`s to its prefab; moving hands come from the item's clips ([HANDS.md](HANDS.md)). Play sounds through `AudioService` with BroAudio `SoundID`s.
- **Giving items from code:** `Equipment.TryPickUp` (with the pickup feed) or `Inventory.TryAdd` / `AddNew` (silent; cheats, loot, crates). Reading: `Inventory.CountOf` / `Take` / `Weight`, each container's `SlotChanged` / `Changed`, or `Inventory.Changed` for anything. `EquipmentRig` spawns and despawns models from the hotbar's `SlotChanged`. Console: `cheat.give <item> [count]`.
- **Something new to loot:** a `LootContainer` on the object (label, capacity, contents), its collider for the look ray. An enemy kind drops loot with its `EnemyDefinition.Loot` (a `LootTable`) and a `LootContainer` + disabled `LootSearch` trigger on its prefab (`InventorySetup.LootBody`). A crate placed in a scene: list it in the scene scope's Auto Inject Game Objects, or it gets no marker.
- **Other interactables** (doors, loot, workbench): implement `IInteractable` (`_Common/Interaction`) on the object with the collider. `PlayerInteractor` and the HUD prompt pick it up with no other changes.

## Known gaps

- Picking up, throwing and searching have no animation. Sounds are placeholders from PlaceHolder Inc (ASSETS.md); throwing has none.
- No weight slowdown yet, no head / body / face items, no buying or selling, no bank for Tokens, no death loss or grave: the inventory survives death. No save / load.
- The hammer and consumables go to the backpack when picked up: they need dragging to a quick slot once.
- Using a consumable has no animation or sound; the held poses of the bandage and shots are rough.
- Item icons mix two styles: Synty's colour renders (guns, ammo, Tokens, backpack, tools) and the Kat Grabowska white flat set (consumables).

- The overlay can make held models appear over nearby surfaces; the obstacle pose softens this, but does not simulate physical weapon collision.

- No sound or HUD feedback for the switch itself. The slot highlight moves immediately when the selection changes.
