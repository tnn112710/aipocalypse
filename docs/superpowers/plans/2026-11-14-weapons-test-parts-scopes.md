# Weapons Test scene, gun parts and scopes: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Weapons Test scene where guns and gun parts lie on tables, the player picks them up and fits parts to a gun from the inventory screen's centre inspector, and optics really aim and zoom (red dot, holo, 2× and a 4× scope with a scope view).

**Architecture:** Parts are items (`AttachmentDefinition`, new `Attachment` category). A `GunInstance` holds one part per slot and a `GunStats` sheet rebuilt when parts change; `Gun`, `GunRecoil` and `HeldGun` read the sheet instead of the asset. A `GunMounts` view on each gun model (first-person `Weapon` and `World/Model`) spawns part models at preset-placed mount points. Optics shift the aim pose so their sight line sits where the iron sights did, narrow the zoom, and full scopes fade in a HUD scope view and hide the held model.

**Tech Stack:** Unity 6000.6, URP 17.6 (render graph), VContainer, uGUI + TMP, Unity Localization CSVs, Shader Graph via `ShaderGraphKit`, Unity CLI `script run`.

**Spec:** `docs/superpowers/specs/2026-11-14-gun-system-design.md` (sections 1, 3, 6–8), narrowed by the user's request of 2026-11-14: test scene with tables, pick up and fit parts, scopes, ready for the gun animation rework, built to be used in real gameplay.

## Global Constraints

- AGENTS.md code style: `_camelCase` private fields, `[SerializeField] T _field` + read-only property, sealed leaf classes, no `#region`, 2–3 line comments max, no XML docs.
- No player-facing text in code or prefabs: every string in the CSVs (en, vi, fr), `Loc.UI` / `Loc.Items` in code; re-run `FontSetup.All` if a new character appears.
- Save only what we wrote (`SaveAssetIfDirty`), never `AssetDatabase.SaveAssets()`. WorldMap is open and dirty: never save, discard or reopen it; build the new scene additively and close it.
- Vendor packs untouched: part models are unpacked copies of Castle Bravo meshes and materials under `_Game`.
- Stats stack Duckov-style: final = base × (1 + Σ%/100), the sum clamped to −90 … +300.
- Not in this step (spec decisions still open): rarity per copy, ammo tiers and armour, hip spread, new guns beyond the five built ones, stock parts, suppressed fire sounds (need new BroAudio entities: ask first).

## Review Focus

1. Removing an extended magazine from a loaded gun: the rounds over the new capacity go back to the backpack as ammo; refused with a message if they don't fit, never lost. (Task 2 test.)
2. Fitting a part while that gun is in hand: the held model, muzzle, aim pose and zoom update at once, mid-reload included. (Task 4, play check.)
3. A gun dropped and picked up again keeps its parts, in the world model too. (Task 2 save test + Task 4 world refresh.)
4. Dragging a part that doesn't fit the inspected gun: no slot lights up and the drop is refused with "Doesn't fit this gun". (Task 6.)
5. Fully scoped with a 4× scope then switching weapons or opening the inventory: the scope view and the hidden held layer both reset. (Task 5, play check.)

---

### Task 1: Part data and the stat sheet

**Files:**
- Create: `_Core/WeaponSystem/Attachments/GunSlot.cs`, `GunFamily.cs`, `GunStat.cs`, `AttachmentDefinition.cs`, `GunStats.cs`
- Modify: `_Core/WeaponSystem/GunDefinition.cs` (family, slots), `_Core/EquipmentSystem/Items/ItemCategory.cs` (`Attachment = 1 << 11`)
- Test: `Tests/EditMode/GunPartsTests.cs`

**Interfaces (produces):**
- `enum GunSlot { Optic, Muzzle, Grip, Stock, Tactical, Magazine }`; `GunSlots.Count = 6`.
- `[Flags] enum GunFamily { None, Pistol, Smg, Rifle, Marksman, Sniper, MachineGun, Shotgun }`.
- `enum GunStat { Damage, FireRate, MagazineSize, ReloadTime, Range, BulletSpeed, VerticalRecoil, HorizontalRecoil, AimTime, Spread, MoveSpeed }`; `[Serializable] struct GunStatModifier { GunStat Stat; float Percent; }`.
- `AttachmentDefinition : ItemDefinition`: `Slot`, `Families`, `Guns` (exact fits), `Ammo` (calibre lock, magazines), `Modifiers`, `Model` (`AttachmentModel` prefab), `Optic` (`OpticSettings { Magnification = 1, EyeRelief, FullScope, ScopeReticle }`), `MagazineScale` (Vector3, magazines), `HidesFlash` (suppressors); `bool Fits(GunDefinition gun)` = gun has the slot and (`Guns` contains it, or `Families` overlaps and `Ammo` is null or matches the gun's ammo).
- `GunDefinition.Family`, `GunDefinition.Slots` (`IReadOnlyList<GunSlot>`), `HasSlot(GunSlot)`.
- `GunStats(GunDefinition gun, IReadOnlyList<ItemInstance> parts)`: `Percent(stat)`, `Get(stat)`, `GetBase(stat)`, `static bool HigherIsBetter(stat)`, and the typed values `Damage, RoundsPerMinute, MagazineSize (int), ReloadTime, Range, Falloff, BulletSpeed, VerticalRecoilScale, HorizontalRecoilScale, AimSpeed, AimTime, SpreadAngle, MoveSpeedScale, AimZoom, Optic (AttachmentDefinition or null), Weight`.

- [ ] **Step 1: Write failing tests** `Stats_AddsPercentagesBeforeMultiplying` (two −20 % AimTime parts → `AimSpeed == base / 0.6`), `Stats_MagazineRoundsToWholeRounds` (+33 % on 30 → 40), `Stats_OpticZoomNeverZoomsOutPastTheGun` (1× keeps 0.87, 2× → 0.5), `Stats_PercentSumIsClamped` (−200 % → ×0.1), `Fits_ByFamilyOrExactGunAndAmmoLock`, `Fits_NeedsTheGunToHaveTheSlot`.
- [ ] **Step 2: Run them, expect compile failure.**
- [ ] **Step 3: Implement the types above.** `AimZoom = min(gun.Handling.AimZoom, 1 / Magnification)`; `AimTime = 3 / AimSpeed` (seconds to ~95 % aim); range scales `Bullet.Range` and both falloff distances (one cached `DamageFalloff`).
- [ ] **Step 4: Run, expect PASS.**

### Task 2: Parts on a gun copy, moving parts with the inventory, saves

**Files:**
- Modify: `GunInstance.cs`, `_Core/EquipmentSystem/Items/ItemInstance.cs` (virtual `Weight`, virtual `Attachments`, `RestoreAttachment`), `ItemSave.cs` (`List<string> Attachments`, written / read in `ItemSaves`)
- Create: `_Core/WeaponSystem/Attachments/GunModding.cs`
- Test: `GunPartsTests.cs`

**Interfaces (produces):**
- `GunInstance`: `Stats`, `ItemInstance AttachmentIn(GunSlot)`, `bool Fits(ItemInstance part)`, `bool TryAttach(ItemInstance part, out ItemInstance previous)`, `ItemInstance Detach(GunSlot)`, `int TakeExcessRounds()` (rounds over the current capacity, removed), `event Action AttachmentsChanged`; `Weight` and `Value` include parts; capacity, `IsFull`, `IsLow` use `Stats.MagazineSize`.
- `GunModding` (static): `bool TryAttach(Inventory inventory, ItemContainer from, int index, GunInstance gun, out string refusal)` (the old part takes the new part's place, else the backpack); `bool TryDetach(Inventory inventory, GunInstance gun, GunSlot slot, ItemContainer to, int index, out string refusal)` (`to == null`: anywhere in the backpack). Both return excess rounds to the inventory as the gun's `AmmoType`, and refuse (changing nothing) when the part or the rounds have no room. Refusals: `inventory.part_no_fit`, `inventory.backpack_full`.

- [ ] **Step 1: Failing tests** `Attach_ReplacesThePartInItsSlotAndReturnsIt`, `Attach_RefusesAPartThatDoesntFit`, `Weight_IncludesFittedParts`, `Modding_SwapPutsTheOldPartWhereTheNewOneCameFrom`, `Modding_DetachingAnExtendedMagazineReturnsTheExtraRounds` (40 rounds, mag +33 % off → 30 in the gun, 10 ammo in the backpack), `Modding_RefusesDetachWhenTheBackpackIsFull` (nothing changes), `Save_RoundTripsAttachments` (`ItemSaves.Write` / `Read` with a catalog of the defs).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Attachments are saved as catalog ids (parts carry no per-copy state).
- [ ] **Step 4: Run all EditMode tests touching items (`GunTests`, `InventoryTests`, `EquipmentTests`, `GunPartsTests`), expect PASS.**

### Task 3: Rules read the sheet

**Files:** Modify `Gun.cs` (magazine size, reload time), `GunRecoil.cs` (`Kick(float aimWeight, float stateMultiplier, float verticalScale = 1f, float horizontalScale = 1f)`), `HeldGun.cs` (damage, rate via trigger is unchanged in this step, range, falloff, bullet speed, spread, aim speed, zoom, move speed from `item.Stats`).
- [ ] **Step 1: Failing test** `Gun_ReloadFillsTheExtendedMagazine` (mag part +33 % → a reload loads 40) and `Recoil_ScalesVerticalAndHorizontalKick` (`ViewEuler` after one kick scales with the factors).
- [ ] **Step 2–4: Implement, run `GunTests` + `GunPartsTests`, expect PASS.** Fire rate stays the asset's (no part changes it yet; `GunTrigger` keeps its settings).

### Task 4: Part models on guns (mounts) and the editor content

**Files:**
- Create: `_Core/WeaponSystem/Attachments/GunMounts.cs`, `AttachmentModel.cs`, `_Core/EquipmentSystem/IItemView.cs`
- Modify: `WorldItem.cs` (`Spawn` and first `Item` use call `IItemView.ShowItem` on the world model), `HeldItem.cs` (`RefreshRenderers()` for parts spawned later), `HeldGun.cs` (mounts, muzzle tip, flash hiding, optic aim pose)
- Create: `AgentScripts/Weapons/AttachmentSetup.cs` (`Parts()`: part model prefabs, pickups, definitions, icons, catalog; `Mounts()`: `GunMounts` on the five guns from the Castle Bravo presets, adapters, iron sight markers, families and slots)
- Content: `Content/Weapons/_Shared/Attachments/<Part>/` (`<Part>.asset`, `<Part>.prefab` pickup, `<Part>_Model.prefab`, `<Part>_Icon.png`)

**Interfaces:**
- `GunMounts`: `void Show(GunInstance gun, bool held)`, `Transform Sight`, `Transform MuzzleTip`, `Transform MountOf(GunSlot)`, `AttachmentDefinition Optic`; serialized `List<GunMount>` (`Slot`, `Point`, `ShowWhenFitted[]`, `HideWhenFitted[]`), `_ironSight`, `_magazines[]`, `_forendContact`. Implements `IItemView.ShowItem(ItemInstance)` (world side, held = false).
- `AttachmentModel`: `Tip`, `Sight`, `HandGrip` (where the support hand holds; prep for the animation rework), `Lights` (on only when held).
- Held optic aim: the aim pose target becomes a runtime `AimOptic` marker = `Aim` pose + `Aim.rotation * (iron point on the sight line at the optic's depth − optic sight point)`, plus `EyeRelief` on z.
- Parts (15): red dot (Optic_H), holo (Optic_I), 2× scope (Optic_C), 4× scope (Optic_Recon_D, full scope), pistol red dot (Optic_Pistol_A), compensator (Muzzle_Brake_C), suppressor (Muzzle_Suppressor_A), pistol suppressor (Muzzle_Pistol_Suppressor_A), choke (Muzzle_ShotGun_Shoke_A), vertical grip (ForwardGrip_A), angled grip (ForwardGrip_F), laser (Laser_A_Right), flashlight (Light_A_Right), pistol light (Light_Pistol_A), extended rifle mag, extended pistol mag. Numbers from spec section 3.
- Slots: AK47 Optic Muzzle Grip Tactical Magazine; MP5 the same; Pistol Optic Muzzle Tactical Magazine; Pump Optic Muzzle Grip Tactical; DoubleBarrel Tactical.

- [ ] **Step 1: Failing prefab tests** `GunPrefabs_HaveAMountForEverySlotTheyDeclare` (held and world `GunMounts`), `Attachments_HaveAModelPickupIconAndName`, `ItemCatalog_HoldsEveryAttachment`.
- [ ] **Step 2: Implement scripts, recompile, run `AttachmentSetup.Parts` then `.Mounts`.**
- [ ] **Step 3: Run tests, expect PASS; render each gun with every fitting part (`capture`/render script) and look at it.**

### Task 5: Optics: lens reticles, scope view, hiding the held model

**Files:**
- Create: `Content/Weapons/_Shared/Optics/OpticReticle.shadergraph` + `OpticReticle.hlsl` (unlit transparent; reticle projected at infinity along the lens's +Z from the object-space view direction), reticle textures and materials, `ScopeMask.png`
- Create: `_Core/UI/Hud/ScopeHud.cs`; Modify: `HeldItem.cs` (`virtual ScopeView Scope`), `_Core/EquipmentSystem/ScopeView.cs`, `FirstPersonView.cs` (`HideHeld`), `FirstPersonRenderFeature.cs` (skip the draw), `PlayerZoom.cs` (`SetHeldHidden`), `HeldItemDriver.cs`
- Modify: `AgentScripts/Weapons/AttachmentSetup.cs` (`Optics()`), HUD.prefab (ScopeView node, via `AgentScripts/Weapons/InspectorSetup.cs`)
- [ ] **Step 1: Failing test** `Scope_FadesInOnlyNearFullAimForFullScopes` (HeldGun scope weight from aim weight is a static rule on `OpticSettings.ScopeWeight(aimWeight)`).
- [ ] **Step 2: Implement; Shader Graph through `ShaderGraphKit` (`Check` must compile).**
- [ ] **Step 3: Play check in the test scene: each optic aimed, screenshot; the red dot sits on the crosshair point.**

### Task 6: Inventory inspector (centre panel)

**Files:**
- Create: `_Core/UI/Inventory/ItemInspectorView.cs`, `ItemPreviewStage.cs` (runtime camera + lights + render texture, far below the world, `ItemPreview` layer, its own feature-free URP renderer), `ItemPreviewInput.cs` (drag to turn, scroll to zoom, drop)
- Modify: `InventoryScreen.cs` (selection, part slot refs, drag / drop / right-click routing to `GunModding`, preview while dragging), `ItemSlotView.cs` (`SetMarked`), `InventoryStyle.cs` (better / worse colours)
- Create: `AgentScripts/Weapons/InspectorSetup.cs` (adds `Inspector` under HUD.prefab's `Inventory` node without rebuilding the rest; binds its fixed labels)
- Modify: `ProjectSettings/TagManager.asset` (layer `ItemPreview`), `Settings/PC_RPAsset` (+ `Preview_Renderer`)
- Text: UI.csv (`inspector.*`, `stat.*`, refusals), Items.csv (`slot.*`, `item.part_*`)
- [ ] **Step 1: Failing test** `Inspector_StatRowsCoverEveryGunStat` (every `GunStat` has a name entry) and the existing `LocalizationTests`.
- [ ] **Step 2: Implement, run `InspectorSetup.Build`, then `InventorySetup.Bake`-style override bake for the new nodes.**
- [ ] **Step 3: Play check: open Tab with the AK in hand, drag each part on and off, screenshot.**

### Task 7: The Weapons Test scene

**Files:** Create `AgentScripts/Weapons/WeaponsTestSetup.cs`, `Scenes/Prototypes/WeaponsTest.unity`, `Scenes/Prototypes/WeaponsTest/` (grid texture, material).
- Layout: grid ground, sun, the Player and HUD prefabs, a guns table (5 guns, a backpack, the ammo crate), two parts tables (15 parts), a range down +Z with target dummies at 10 / 25 / 50 / 75 / 100 m on posts with distance numbers, a backstop.
- [ ] **Step 1: Build additively, save, close; WorldMap stays open and untouched.**
- [ ] **Step 2: Play it (virtual input where needed): pick up, fit, aim, shoot each optic; screenshots.**

### Task 8: Docs and finish

- WEAPONS.md (parts, optics, adding a part, the animation-rework contract: mounts under the parts they move with, `AttachmentModel.HandGrip`, `GunInstance.AttachmentsChanged`), EQUIPMENT.md (Attachment category, inspector), UI.md (inspector, scope view), CODE_STRUCTURE.md, ASSETS.md (Castle Bravo attachments, Animpic table), the spec (what this step decided), ARCHITECTURE prototypes line.
- [ ] Recompile clean, console clean, EditMode tests pass (by reflection if a scene is dirty), report what was and wasn't checked. No commit unless asked.
