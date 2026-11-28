# UI

How the in-game UI is built and how to extend or restyle it. Script locations and frame order are in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

## Rules

- **uGUI + TextMeshPro.** No UI Toolkit for the game HUD.
- **Look: Layer Lab GUI Pro – Survival Clean** (user, 2026-11-21): navy panels with a cut top-right corner, cyan accent, slanted bars. Its sprites only (`_StoreAssets/UI/Layer Lab/GUI Pro-SurvivalClean/ResourcesData/Sprites/Components/`, the tintable `*_Custom` white ones), never its prefabs or its Oxanium fonts (no Vietnamese, two of three weights lack French accents, and no TTF to rebuild them). `AgentScripts/UI/LayerLabSetup.All` writes the sprites and palette into the style assets and re-skins the panels, slots, buttons, frames and key caps the setup scripts baked into `HUD.prefab`, the widget prefabs and `MainMenu.unity` (safe to re-run; change the palette there). Roles: `Frame_ItemFrame04_n_White3` for panels, slots, buttons and the clock plate; `Frame_ItemFrame04_s_White2` for the selected / highlight / rarity frame; `Slider05_White7_1` for key caps and the bars' icon frames; `Slider05_White2_Frame2` for the bar shape; `Label_Tag01_White1` for the PROTOTYPE tag; `Common_4pixel_white` for bar fills. Corner size is `pixelsPerUnitMultiplier` on each image (the script scales it down until the corners fit the rect). Synty Interface Apocalypse HUD still provides the icons, the minimap pieces, the QTE rings and bar, the menu shade gradient and the fonts' source.
- **Fonts: Saira Condensed**, from Synty Interface Apocalypse HUD. Its Saira Condensed fonts are used through our own **static** copies in `Data/UI/Fonts/` (`SairaHeader`, `SairaBold`, `SairaBody` + `_Underlay` material presets, built by `AgentScripts/UI/FontSetup.cs` from Synty's TTFs): a dynamic atlas grows as new text appears and the editor saves it, so Play kept rewriting font assets. The atlases hold ASCII, the Vietnamese and French alphabets and every character in the string tables: after adding text with a new character, re-run `FontSetup.All` (`LocalizationTests` fails until then). `DeathScreenFont` and `RepairQteFont` are static too.
- **No text in code or prefabs.** Every word the player reads comes from the string tables ([LOCALIZATION.md](LOCALIZATION.md)): `LocalizedString` fields on styles and data, `Loc.X("key")` for text owned by code, `LocalizeStringEvent` on fixed labels (listed in `LocalizationKit.Labels`). Leave room for longer words and for Vietnamese accents above capitals. Never use or nest its prefabs: they carry Synty's sample scripts and animators. Our widgets are our own prefabs under `_Core/UI/Prefabs/`.
- **Views are passive.** A widget (`StatBar`, `HotbarSlotView`) only displays the values it's given. A presenter (`PlayerVitalsHud`, `HotbarHud`) reads game state and feeds the widgets. Widgets never know about the player, items, stats or events.
- **Looks live in style assets, layout lives in prefabs.** Sprites, colors and fonts go in a `*Style` ScriptableObject (`Data/UI/`). Size, position and anchors stay on the prefab's RectTransforms.
- **HUD images are not raycast targets** and the HUD canvas has no `GraphicRaycaster`, because nothing on it is clickable. One exception: while the inventory screen is open, the hotbar row turns on its own (normally disabled) raycaster and tile hits (`HotbarHud.SetInteractive`). Menus that need clicks get their own canvas with a raycaster and an EventSystem that's active only while they're open, its input module pointed at InputService's UI map (the inventory screen is the first: see below).
- **Zero allocations per frame.** Widgets skip values that didn't change. Labels use `TMP_Text.SetText("{0}", value)`, never string concatenation.

## Canvas and scaling

`_Core/UI/Prefabs/HUD.prefab`:

| Setting | Value | Why |
| --- | --- | --- |
| Render mode | Screen Space – Overlay, sort order 10 | Drawn over everything, needs no camera reference |
| Canvas Scaler | Scale With Screen Size, **1920×1080**, Match Width/Height **0.5** | 1080p is the most common Steam resolution, so art is authored at 1:1. 0.5 balances 16:10 (Steam Deck), 16:9 and 21:9 ultrawide without the HUD growing too much on wide screens |
| SafeFrame | Stretch, inset 48 px left/right, 40 px top/bottom | Keeps widgets off the screen edge. Change the inset in one place |

```text
HUD                          Canvas · CanvasScaler · CanvasGroup + HudVisibilityHud (fades the HUD out while the player is dead)
├─ HurtFeedback              first child (draws under the widgets), stretched full screen past the SafeFrame inset · nested Canvas · HurtFeedbackHud (HurtFeedbackStyle), see Hurt feedback below
├─ ScopeView                 full screen, under the widgets · nested Canvas (off while nothing shows) · CanvasGroup · ScopeHud: View (one screen height,
│                            on the crosshair) > Dim, Shadow (ScopeShadow), Pupil > Mask (ScopeMask) + Left / Right / Top / Bottom black bars, Reticle; see Scope view below; built by
│                            AgentScripts/Weapons/InspectorSetup
│  └─ Top / Bottom / Left / Right   RawImage edges, off until a hit; gradient made in code
├─ BreachAlert               band under the raid banner (anchors 0.56–0.62), CanvasGroup (alpha 0 at rest) · BreachAlertHud (Data/UI/BreachAlertStyle.asset): one TMP line when a fence segment or gate breaks, "The fence is down behind you" (side from the player's view); built by AgentScripts/Defense/DefenseSetup.Hud
├─ RaidBanner                upper middle band (anchors 0.62–0.82 of the height), CanvasGroup (alpha 0 at rest) · RaidBannerHud (Data/UI/RaidBannerStyle.asset): Title + Subtitle TMP texts, shown when a raid starts, is repelled or ends at dawn; built by AgentScripts/Hud/RaidBannerSetup.Build
└─ SafeFrame                 stretch, inset 48/40
   ├─ TopLeft                corner anchors (zero size): widgets anchor to these
   ├─ TopCenter              top-middle anchor (zero size)
   │  └─ Clock               nested Canvas · ClockView · ClockHud (world clock, see Clock below)
   ├─ TopRight
   │  └─ Minimap             220×220, 10 px in from the corner · nested Canvas · MinimapView (MinimapStyle) · MinimapHud (see Minimap below)
   ├─ BottomLeft
   │  ├─ Needs               nested Canvas · PlayerNeedsHud: Hunger, Thirst, Sanity (NeedRing, 56 px, 10 px apart), 96 px up, above Vitals; built by AgentScripts/Hud/NeedsHudSetup (see Need rings below)
   │  ├─ Effects             nested Canvas · HorizontalLayoutGroup · EffectsHud: Effect template (icon + seconds), 162 px up
   │  └─ Vitals              nested Canvas · VerticalLayoutGroup · ContentSizeFitter · PlayerVitalsHud
   │     ├─ HealthBar        StatBar (HealthBarStyle)
   │     └─ StaminaBar       StatBar (StaminaBarStyle)
   ├─ BottomCenter
   │  └─ Hotbar              nested Canvas · HorizontalLayoutGroup · ContentSizeFitter · HotbarHud
   │     └─ Slot1…SlotN      HotbarSlot.prefab, spawned at Start, one per hotbar slot
   ├─ BottomRight
   │  ├─ PickupFeed          nested Canvas · PickupFeedHud: Line0…Line4 ("+30 Rifle Ammo", newest at the bottom) + Warning ("Backpack full" / "Too heavy"), 150 px up
   │  └─ Bag                 nested Canvas · PlayerLoadHud > Gauge (CanvasGroup · BagGauge): the carried weight, 40 px up, under the feed; built by AgentScripts/Hud/LoadHudSetup (see Bag gauge below)
   └─ Center
      ├─ Crosshair           nested Canvas · CrosshairHud; moved to where the barrel points
      │  ├─ (reticle)        spawned at runtime from CrosshairHud's reticle prefab (Prefabs/Widgets/Reticles, see Crosshair below)
      │  └─ HitMarker        HitFeedbackHud (HitFeedbackStyle)
      ├─ RepairQte           nested Canvas · QteHud (QteStyle): the running QTE beside the crosshair, see QTE below
   ├─ (BottomLeft) DogBar    nested Canvas · CanvasGroup · DogHud: StatBar (DogToughness style, paw icon) 222 px up; the dog's toughness (DOGS.md), built by DogSetup.DogBar
   ├─ (BottomLeft) FireBar   nested Canvas · CanvasGroup · CampfireFuelHud: StatBar (CampfireFuel style, fire icon) 262 px up; the home campfire's fuel in tenths of an hour, shown when it's low or out at night near home (CAMPFIRE.md), built by CampfireSetup.Hud
      ├─ CommandWheel        nested Canvas · CanvasGroup (alpha 0 until open) · CommandWheel (CommandWheelStyle): Backdrop + Slot0…7 (Label); the dog's orders, driven by CommandWheelPresenter (DOGS.md), built by DogSetup.Hud
      ├─ Interact            nested Canvas · InteractPromptHud, 120 px below the centre (hidden while a menu holds the UI)
      │  ├─ InteractPrompt   InteractPrompt.prefab (hidden while nothing is focused)
      │  ├─ ToolPrompt       InteractPrompt.prefab, 46 px above: the held repair tool's key ("[LMB] Repair Fence 63%", held)
      │  ├─ CollectPrompt    InteractPrompt.prefab, 46 px below: the Collect key ("[C] Collect Tokens" on a farm animal)
      │  └─ WhistlePrompt    InteractPrompt.prefab, 92 px below: the whistle ("[Q] Call Bolt" when aiming at a robot E doesn't reach)
      └─ ItemUse           nested Canvas · ItemUseHud: Root (Ring, radial Fill, Label) around the crosshair while a consumable is used
NameTag                      nested Canvas · CanvasGroup · NameTagHud: IconDisc + Icon (the job icon) over a Band and Name, placed over the
                             robot the player aims at (RobotSetup.Hud)
LootMarkers                  full screen, nested Canvas · LootMarkerHud: Marker (inactive 56 px bag icon + Shadow, cloned 8 times), placed
                             over crates and bodies holding loot (see Loot markers and pickup feed below)
Inventory                    the inventory screen, its own Canvas (sorting 50, off while closed) · GraphicRaycaster · CanvasGroup ·
                             InventoryScreen; built by AgentScripts/Inventory/InventorySetup (see Inventory screen below)
SaveToast                    full screen, nested Canvas · SaveToastHud: one TMP line bottom right, 230 px up ("Game saved · Save 2")
PauseMenu                    last child: Esc menu, its own Canvas (sorting 200, off while closed) · GraphicRaycaster · CanvasGroup
                             (ignores the HUD's death fade) · PauseMenu: Backdrop, Page (Title, Buttons, Hint), Slots (SaveSlotsPanel),
                             Confirm (ConfirmDialog), EventSystem (off while closed); built by AgentScripts/Save/SaveSetup.Hud (see Menus below)
```

- **Other screen shapes.** The CanvasScaler alone keeps 16:9 (1080p, 1440p, 4K) identical, only sharper; checked at 1920×1080, 2560×1440, 3840×2160, 3440×1440 (21:9), 2560×1600 (16:10) and 1280×800 (Steam Deck). Corner widgets anchor to the SafeFrame corners, so they follow any shape. The fixed-size screens can't: at 21:9 the canvas is 2226×932 and at 16:10 1821×1138, so the inventory's panels overlapped and the pause menu's last line sat on the health bars. `ShrinkToFit` (on `Inventory`, `PauseMenu/Page`, `PauseMenu/Slots` and the main menu's `Page` / `Slots`; added by InventorySetup and SaveSetup) scales such a stretched layout down uniformly when the canvas is shorter or narrower than 1920×1080 and widens its rect to match, so the panels keep their spacing; it never grows it and runs in Play only. Tooltips, the inspector's leader line and dragging work in world space, so they follow the scale. A new full-screen layout of fixed-size panels gets one too. At 1280×800 the smallest text (15 pt stats) ends up ~10 px tall: a text-size setting is the fix if the Deck matters.
- **Region per corner.** New HUD info goes under the matching region: the clock (and later weather) in TopCenter, the minimap in TopRight, quest tracker in TopRight below it, currency in TopLeft, crosshair and interact prompt in Center, hotbar in BottomCenter, ammo in BottomRight.
- **A nested Canvas around anything that changes often.** `Vitals` has its own Canvas, so the bars changing every frame (stamina regen) only rebuild that group, not the whole HUD. Give each frequently updated group its own nested Canvas. Static decoration can share the root.

## Where the HUD lives

The HUD is **standalone**, not part of the player. It holds info that isn't the player's (time, objectives, currency), and it keeps working through death and respawn, or in a scene with no player.

- **Put `HUD.prefab` in the scene** next to the Player. In World the scene scope will spawn it.
- **`HudScope`** on the HUD root injects every view in it from the scene's `GameLifetimeScope`, or from the root scope if the scene has none. It works the same way as an entity's scope, so the HUD works dropped into any scene.
- **Player views derive from `PlayerHudView`.** Players register with the root `PlayerRegistry`, which publishes `ActivePlayerChanged` (MessagePipe) when the active player changes. The view's `Bind(player)` runs when a player appears and `Unbind(player)` when it leaves or is replaced. Build per-player state in `Bind` (the hotbar spawns its slot views there) and tear it down in `Unbind`.
- **Views about anything else** are plain MonoBehaviours that `[Inject]` their service (time, objectives) or subscribe to its MessagePipe events.

```text
PlayerRegistry (root) ── ActivePlayerChanged ──> PlayerHudView.Rebind → Unbind(old) / Bind(new)
   ▲ Register / Unregister                         ├─ PlayerVitalsHud   polls the bound player in LateUpdate
Player (Construct / OnDestroy)                    ├─ CrosshairHud      polls Player.CrosshairPosition / CrosshairSpread in LateUpdate
                                                  └─ HotbarHud         spawns slot views, listens to Hotbar / Equipment
```

`PlayerHudView.Player` is an `IPlayer`: the read-only state and respawn steps the HUD may use, never the `Player` MonoBehaviour itself.

## Need rings

Hunger, thirst and sanity ([NEEDS.md](NEEDS.md)) show as icons inside rings above the health and stamina bars (user, 2026-11-28: bars for the fast meters, rings for the slow ones). Built by `AgentScripts/Hud/NeedsHudSetup.Build` (row, styles, the thirst cough).

```text
Needs                        Canvas · PlayerNeedsHud (_hunger, _thirst, _sanity)
└─ Hunger / Thirst / Sanity  56×56 · CanvasGroup · NeedRing (NeedRingStyle)
   └─ Body                   scaled by the pulse
      ├─ Track               the ring sprite, dim (the empty part)
      ├─ Fill                Filled, Radial 360 from the top, clockwise: the share of the current max
      └─ Icon                52% of the ring
```

- **Look** (`Data/UI/HungerRingStyle`, `ThirstRingStyle`, `SanityRingStyle`): Synty Apocalypse HUD `SPR_HUD_Apocalypse_Ring_Medium_01_Clean` for the ring and the track (white at 22%), its `ICON_Apocalpyse_Status_Hunger_01` / `Thirst_01` (the bars' icon set) and Layer Lab `Pictoicon_brain` for sanity (the Synty pack has none).
- **Colour by how full** (`NeedRingStyle.Bands`, highest first, user 2026-11-28): green above 60%, yellow above 30% (warning), orange above 10% (near empty), red below (critical); the same on every ring. The bands show the value; the tiers change on the same edges (NEEDS.md), and the pulse and the sound follow the tiers.
- **Dim while green:** at `DimAlpha` (0.45) while in the top band (`NeedRingStyle.Dims`); full in any warning colour. A tier change or a refill brightens it for `BrightTime` (2.5 s). Entering a tier punches its scale once (`PulseScale` 1.18); at empty it pulses slowly (`EmptyPulseRate` 1.2 Hz).
- **Body sound:** `TierSound` plays on entering Low or worse and every `RepeatSoundEvery` (40 s) while VeryLow or Empty. Only thirst has one (`Thirst_Cough`, 15 random Gamemaster coughs, Player library); no pack has a stomach growl; sanity's sounds come with its own pass.
- `NeedRing.SetFlicker(alpha)` multiplies its alpha: sanity's flicker (`SanityGlitch.Flicker`, also on the `Vitals` CanvasGroup through `PlayerVitalsHud._group`); it never changes the value shown.
- **Low sanity lies** ([NEEDS.md → What low sanity shows](NEEDS.md#what-low-sanity-shows)): `ClockHud` shows a wrong time for at most 0.6 s now and then, `MinimapHud` adds fake enemy dots and swaps the compass letters. Bars, rings, ammo and the bag never show a wrong value.
- Hidden parts: real time (`unscaledDeltaTime`), so a paused game doesn't freeze a fade.

## Bag gauge

The carried weight ([NEEDS.md → Carried weight](NEEDS.md#carried-weight)), bottom right under the pickup feed (user, 2026-11-28), so a pickup and the weight it adds show in one place. Built by `AgentScripts/Hud/LoadHudSetup.Build`.

```text
Bag                          130×82 · Canvas · PlayerLoadHud (_gauge)
└─ Gauge                     CanvasGroup · BagGauge (BagGaugeStyle)
   ├─ Icon                   56×56, top centre
   │  ├─ Empty               the bag, dim (EmptyColor)
   │  ├─ Fill                the bag again, Filled vertically from the bottom: carried / Max on the bag's body
   │  └─ Notch               2 px line at Comfortable / Max, same scale
   └─ Label                  "22.9 / 35 kg" (HUD table hud.weight; carried, one decimal / Max, whole)
```

- **Colours** (`Data/UI/BagGaugeStyle.Colors`, fill and label, every `LoadState`): white light, yellow from 80% of Comfortable, orange past it (slower), red at Max (a crawl). Dim (`DimAlpha` 0.5) while light.
- **Body range** (`BagGaugeStyle.FillRange`, 0.009..0.885 of the Synty backpack's height): empty to Max maps onto the bag's body, leaving out the thin carry handle on top, so a fill doesn't read low; the notch uses the same scale. `LoadHudSetup.MeasureFill` measures it from the sprite (rows at least half as wide as the widest); it assumes a sprite no wider than tall in the square icon area.
- The label is rebuilt when a shown number or the language changes.
- **Icon:** Synty Apocalypse HUD `ICON_Apocalpyse_Inventory_Backpack_01`.
- The label is rebuilt only when the carried tenth of a kilo or the whole Max changes (`SetText` with the cached format).
- The inventory screen's weight bar uses the same states (`InventoryStyle` `WeightBarColor`, `NearWeightColor`, `HeavyWeightColor`, `OverweightColor`) and shows carried / Max.

## StatBar widget

`_Core/UI/Prefabs/Widgets/StatBar.prefab`, 331×41 at 1080p (label 28 pt; 368 until 2026-11-22):

```text
StatBar                      StatBar component
├─ IconFrame                 41×41, left; hidden when the style has no icon
│  └─ Icon                   preserve aspect
└─ Bar                       rest of the width; Image + Mask (StatBar._shape) clip the children to the style's ShapeSprite (off when empty)
   ├─ Background
   ├─ Trail                  Filled, horizontal; lags behind drops (optional)
   ├─ Fill                   Filled, horizontal
   └─ Label                  TMP, current value rounded up (to the style's Decimals: 0 whole numbers, 1 tenths)
```

API, called by a presenter:

- `SetValue(current, max)`: updates the fill and label, and is safe to call every frame.
- `SetWarning(bool)`: swaps the fill to the style's `WarningColor`. Stamina uses it while exhausted.
- `ApplyStyle()`: repaints from the style. Runs in `Awake` and on edit (also an Odin button).

The trail (health and stamina) holds for `TrailDelay`, then slides to the new value at `TrailSpeed`. Only drops of at least `TrailHoldMinDrop` (bar fraction) restart the hold, so a steady drain is followed instead of frozen: stamina uses 0.02, so a jump, shot or swing shows its chunk while sprint drain slides along; health uses 0 (every hit holds). The component's `Update` runs only while the trail is moving.

## Hotbar

`_Core/UI/Prefabs/Widgets/HotbarSlot.prefab`, 80×80 tile + 30×30 key cap at 1080p (84 until 2026-11-22); the `Hotbar` row sits 16 px below the SafeFrame's bottom edge:

```text
HotbarSlot                   HotbarSlotView component
├─ Tile                      scaled by SelectedScale while selected
│  ├─ Background             9-sliced box; dimmer when empty
│  ├─ Icon                   item icon (ItemDefinition.Icon), preserve aspect; hidden when empty
│  ├─ Frame                  selection outline, only on the selected slot
│  ├─ Ammo                   TMP badge, bottom-right: "30/90" (magazine / reserve) for guns, a stack's count; hidden when the item shows no badge
│  └─ Lock                   18 px lock (Layer Lab picto Icon_Lock), top-right, while the item is locked
└─ KeyCap                    key sprite + TMP label ("1", "2"… from the input binding)
```

- `HotbarSlotView` API: `SetKey(string)`, `SetIcon(Sprite)` (null = empty), `SetSelected(bool)`, `SetBadge(count, reserve, showReserve, warning)`, `HideBadge()`, `SetLocked(bool)`, `ApplyStyle()`.
- `HotbarHud` (presenter on `Equipment.Hotbar`) spawns one view per hotbar slot, so the HUD follows `EquipmentConfig`: today 10 (1, 2, V, 3–9). Each key cap is the slot's `HotbarKey` binding (`Equipment.KeyOf` → `InputService.GetKeyLabel`). It's **event-driven**: `Hotbar.SlotChanged` → icon, `Equipment.SelectionChanged` → highlight, each slot item's `ItemInstance.Changed` / `AmmoReserve.Changed` → badges. Nothing is polled.
- **With the inventory open**, this row is where the quick slots are set up (`HotbarHud.SetInteractive`): the `Hotbar` node's canvas overrides its sorting to the inventory's + 1 (above the screen's dimming backdrop), its `GraphicRaycaster` turns on, and each `HotbarSlotView` takes pointer hits as an `ISlotView`: a hover frame (`HotbarSlotStyle.HoverFrameColor`) and a faded icon while its item is dragged (`DraggedIconAlpha`). Closing turns it all off again.
- **Badge:** each item says what its slot shows (`ItemInstance.Badge`, an `ItemBadge`): a stack shows its count, a gun its rounds with its `AmmoType` as the reserve to look up. The reserve is shown only for an `AmmoType` when the reserve isn't infinite, so a gun reads as in Escape from Duckov: magazine / reserve, magazine only with infinite ammo, `AmmoWarningColor` when the magazine is empty or the gun is jammed. HotbarHud knows no item kinds.
- The highlight moves as soon as a slot is selected, not when the switch animation finishes.
- Behavior and switching rules: [EQUIPMENT.md](EQUIPMENT.md).

## Inventory screen

The first clickable UI: Tab or searching a crate or body (E) opens it, Tab / Esc close it. Laid out after Escape from Duckov at 1920×1080: the left panel (540×800, fixed) holds the Tokens carried, weight ("Weight 12.5 / 40.0 kg" + bar, red past the limit), EQUIPMENT (the two gun slots and the melee slot with their keys, then Head, Body, Face, Backpack, Light, each captioned, a silhouette when empty) and BACKPACK (n/N) with Sort and a scrolling grid of 5 columns; the loot panel (top right, under the clock) shows the crate or body being searched ("CRATE (5/8)") with Take all, its unrevealed slots as an "unknown" mark. The quick slots are the HUD hotbar's own row along the bottom: the screen drags in and out of all its slots (1, 2, V, 3–9); a drop on a slot that doesn't take the item shows a refusal above the row ("Slot 3 takes usable items and tools", "Slot 1 takes a gun", "Slot V takes a melee weapon").

```text
Inventory                    Canvas (override sorting 50) · GraphicRaycaster · CanvasGroup (ignoreParentGroups: the HUD root's group
│                            isn't interactable, so its buttons would take no click) · InventoryScreen
├─ Backdrop                  full screen, BackdropColor; raycast target (a drag let go here goes back to its slot)
├─ Inspector                 centre panel 720×740 · ItemInspectorView: Content (Title, Info, StageFrame > Stage (RawImage · ItemPreviewStage ·
│                            ItemPreviewInput) + TurnHint, Slot_Optic … Slot_Magazine (ItemSlot), Stat0–13 / Value0–13), Hint, Leader; built by
│                            AgentScripts/Weapons/InspectorSetup, not by InventorySetup.Interface
├─ Left                      panel · VerticalLayoutGroup: Header (Tokens, TokensLabel, Weight, WeightBar > Fill), EquipmentTitle,
│                            EquipmentGrid (GridLayoutGroup 88 px, 5 columns), BackpackRow (BackpackTitle, Sort), BackpackScroll
│                            (ScrollRect > Viewport (RectMask2D) > Content: GridLayoutGroup + ContentSizeFitter)
├─ Loot                      panel: LootRow (LootTitle, Take all), LootGrid; shown only with a container
├─ Message                   refusals ("Empty the backpack's extra slots first", "Backpack full", a hotbar slot that doesn't take the item), 2.5 s, 32 pt, bottom centre just above the hotbar row (150 px up)
├─ Tooltip                   ItemTooltipView: name (rarity colour, opaque) + rarity, "30 x 0.016 kg = 0.48 kg", uses, value, "Locked" (InventoryStyle.LockedColor), then Hint (smaller, dimmer: "Right-click: options · Double-click: move" and "G: drop · X: lock · 1-9: to the hotbar"; a crate's item: "… Double-click: take"; none on a part slot), beside the hovered slot (bottom-aligned with it near the screen's bottom); own Canvas at sorting 60, above the raised hotbar
├─ DragIcon                  follows the pointer while dragging; own Canvas at sorting 60, above the raised hotbar
├─ ItemMenu                  ItemContextMenu, inactive until a right-click: a clear full-screen blocker (a click off the buttons closes it)
│                            with its own Canvas at sorting 70 + GraphicRaycaster, and Panel (180 px column: Pickup, Use, Drop, Lock;
│                            labels from the string tables when it opens). Panel has no Image (it would size the column to its sprite,
│                            204 px): Background (lighter than the panels behind) and Border (HighlightSprite in the accent colour) are
│                            children the column ignores
└─ EventSystem               EventSystem + InputSystemUIInputModule, active only while open
```

- `ItemSlot.prefab` (`Prefabs/Widgets/`, 88×88): Background (raycast target), EmptyIcon (silhouette), Icon, Name (bottom; 22 px tall: the 13 pt font's line is ~20.5 px and a shorter box truncates the whole name), Count (top right), Key (top left), RarityBorder (the item's rarity colour, `InventoryStyle` → Rarity; none for Common), Hidden (an unrevealed item: Mark, the Synty map "unknown" icon, since the font has no `?`, and Ring, a radial fill shown on the slot being revealed), Highlight (hover), Lock (16 px, bottom right above the name, while the item is locked), Caption (under the tile). `ItemSlotView` API: `Show(icon, name, count, warning, rarity, locked)`, `ShowEmpty(silhouette)`, `ShowHidden(progress)`, `SetSearchProgress`, `SetKey`, `SetCaption`, `SetDragged`; it raises Clicked / DragStarted / Dragging / DragEnded / DroppedOn / HoverChanged and knows no items. Labels are rebuilt only when their value changes.
- `InventoryScreen` (a `PlayerHudView`) maps each slot view to a slot of a container (hotbar, gear, backpack, loot). Each grid keeps its own views, hooked once, and a rebuild only adds or hides the difference (0.4 ms; re-parenting pooled views on every open cost 5 ms). It refreshes them all when anything changes (`Inventory.Changed`, the loot container's `Changed` / `RevealChanged`; a flag read in `Update`, ~50 µs), and rebuilds when it opens or the backpack's size changes. **Drag** moves / merges / swaps (`Inventory.TryMove`), **Shift-drag** moves half a stack, a drag let go anywhere but a slot changes nothing, **double-click** sends an item across (loot → inventory, backpack → loot, backpack → a free active or gear slot, active / gear → backpack; a gun part onto the inspected gun, a fitted part off it). **Right-click** opens the item's menu (`ItemContextMenu`, at the pointer, flipped to stay on screen; Esc, a click elsewhere or a drag closes it). On a carried item (backpack, equipment, hotbar row): **Use** (consumables only: `IConsumer.CannotUse` / `Use` at once, no use time, then `HeldConsumable.SpendUse`), **Drop** (the whole stack, `IPlayer.ThrowAway`; greyed out while locked), **Lock / Unlock** (`ItemInstance.Locked`). On a crate's item: **Pick up** first (`Inventory.TakeFrom`, as much as fits; "No room" / "Backpack full"), then Use. Right-click on a fitted part does the double-click's move. **Keys on the hovered item** (not while the menu is open or dragging): the Drop key (G) drops a carried item ("Locked: unlock it to drop it" if locked), X locks / unlocks it, a hotbar key (1-9, V) moves the stack to that slot as a drag would (from the crate too; a slot that doesn't take it shows its refusal). G and the hotbar keys are the gameplay bindings, read while the Player map is off (`InputService.UiDropPressed`, `UiHotbarKeyPressed`); Lock is the UI map's own action (X, since L is the Debug map's revive). Sort: `Backpack.Sort()`; Take all: `Inventory.TakeFrom` per revealed loot slot. A loot slot still hidden by the search refuses every action. While a container is open, the screen ticks `PlayerLooting` on unscaled time and fills the ring of the slot being revealed each frame (one view).
- **Pause and input:** opening takes `TimeService.RequestPause(this)` (not during a raid: `RaidStarted` → `RaidEnded`; then a `DamageDealt` on the player closes the screen) and `InputService.RequestUI(this)` (UI map on, gameplay map off, cursor free) and activates its EventSystem; closing releases both. The input module is pointed at InputService's own UI map (`InputService.ConfigureUiModule`), so nothing but InputService enables actions. Shift comes from `InputService.UiShiftHeld`.
- **Closed, it costs nothing:** its Canvas is disabled (a canvas hidden only by alpha still batches every element each frame), its EventSystem is off, and `Update` only checks the Tab key.
- **No q in titles:** the Synty font's atlas has 74 characters and no lowercase `q`; TMP would add it to the vendor font asset. Titles are written in capitals ("EQUIPMENT") and use the UpperCase style.
- Built by `AgentScripts/Inventory/InventorySetup.Interface`; `InventorySetup.HotbarDrops` applies just the hotbar-row wiring (raycaster, Message, DragIcon / Tooltip canvases; removes the old Quick panel and the row's old drop catcher) to the HUD prefab as it is; `InventorySetup.ItemMenuAndLocks` adds the item menu to it and the lock icon to both slot prefabs; `InventorySetup.MenuHintsAndMessage` rebuilds the item menu, adds the tooltip's Hint line and places the refusal text. After changing layout, run `InventorySetup.Bake` (or `BakeCurrent` right after leaving Play) so the values layout groups and TMP compute in the scene are stored in HUD.prefab, and WorldMap doesn't get dirty with HUD overrides.

- **Sounds** (`InventoryStyle` → Sounds, 2D entities in the Player library): open, close, place (a successful move, double-click send, Take all, Lock / Unlock), refused (with every message), reveal (each revealed stack), a gun part fitted or taken off (`Part_Attach` / `Part_Detach`, Gamemaster magazine latches; the 3D view gives a small size punch with it).
- **Inspector** (the centre, after Duckov's, with the item in 3D): a left click on any item shows it there; opening the screen shows the item in hand. Name in its rarity colour, rarity / weight / value, then the 3D view (`ItemPreviewStage`: the item's world model, a gun upright with its parts, rendered by its own camera into a texture far below the world on the `ItemPreview` layer with `Preview_Renderer`, a copy of PC_Renderer without its features; it renders only while something is shown; drag to turn, wheel to zoom). A gun shows its part slots in Duckov's order with their parts' icons; hovering one draws a leader line to its mount on the model. Stats sit in two columns (damage, fire rate, magazine, reload, range, bullet speed, recoil up / sideways, aim time, spread for shotguns, move speed, hip spread, weight, zoom with a scope). While a part is dragged, the slot it fits lights up (`ItemSlotView.SetMarked`, `InventoryStyle.FitColor`) and the stats preview it in `BetterColor` / `WorseColor`. A part shows its changes instead; its tooltip lists them too. Parts are moved by `GunModding` ([WEAPONS.md → Gun parts and optics](WEAPONS.md#gun-parts-and-optics)).

## Menus

The pause menu, the main menu and the save slot list. What they do: [SAVE.md → Menus](SAVE.md#menus).

- **Pause menu** (`PauseMenu`, in `HUD.prefab`): Esc / gamepad Start (the Player map's `Pause`, read through `InputService.PausePressed`) opens it only while gameplay input is on and no other menu holds the UI. Open: `TimeService.RequestPause(this)`, `InputService.RequestUI(this)`, its EventSystem on (input module pointed at InputService's UI map). UI `Cancel` (Esc) steps back: dialog → slot list → menu → closed. Closing (or the HUD being destroyed by a scene load) releases both.
- **Main menu** (`MainMenuScreen`, `Assets/_Game/Scenes/MainMenu.unity`): a Screen Space Overlay canvas with a `HudScope` (root services, no `GameLifetimeScope` there), its EventSystem always on, `RequestUI` while the scene is open. A title screen: `Background` (the world shot, `AspectRatioFitter` envelope), `Shade` (left 62 %), the title, and text buttons.
- **Language** (`LanguageButton`, under Settings in both menus): each press switches to the next language (`LanguageService.Step`), and the label names the current one in itself ("LANGUAGE: TIẾNG VIỆT"). Everything on screen updates at once; the menus refresh their built text (hint, Continue info, version) on `LanguageChanged`. The pause menu's eight buttons are 62 px tall, so the column stays clear of the health bars. [LOCALIZATION.md](LOCALIZATION.md).
- **Shared widgets** (`UI/Menus/`): `SaveSlotsPanel` (one `SaveSlotRow` cloned per slot from an inactive template), `ConfirmDialog` (No selected first; closing gives the focus back to the button that asked). Both menus hand the panel the session with `Bind`; the pause menu forwards `GameSaved` to it (`ShowSaved`), because a panel never activated never gets `OnDestroy` to drop a subscription.
- **Feel:** every menu button has a `MenuButton`: the mouse over it selects it (one highlight for mouse and gamepad), getting the focus plays the hover tick, a click the click (`MenuButton.SelectSilently` gives focus without the tick, for a menu opening); its labels dim while it can't be pressed (the Button's tint only reaches the strip behind them). `MenuSounds` on each menu's root plays `MenuStyle`'s sounds through `AudioService` (Esc / B: the back sound). `MenuSelectionKeeper` next to each EventSystem gives the focus back to the last button when a mouse click clears it, so a gamepad is never left with nothing selected. Gamepad: d-pad / stick moves, South presses, East / Esc steps back.
- **Look:** `Data/UI/MenuStyle.asset`, one look for all menus (after Synty Interface Apocalypse's sample title scene): `SairaBold` buttons, `SairaHeader` titles, `SairaBody` small text; Layer Lab's chamfered `Frame_ItemFrame04_n_White3` (`ButtonSprite`, sliced at `CornerScale` 3) behind text buttons (clear until hovered or selected, `TextColors`), slot rows and dialog buttons (`Colors`: brown `Button`, the current game's slot in `Accent`, delete / overwrite in `Danger`); the left `Shade` gradient; the UI sounds. **Theme:** the highlight (hover, selected, pressed, the current slot) is orange and comes from one colour, `MenuStyle.Highlight`; `Button` and `Panel` are warm brown. Every menu button (and the inventory's item-menu buttons) carries `MenuButtonColors`, which applies the style's colours when it wakes, so changing `Highlight` / `Button` in the asset recolours every menu on the next Play. The colours stored in the prefabs are only a copy: `AgentScripts/UI/MenuTheme.cs` (`All` once, `Bake` after changing the style) refreshes them for the editor view and recolours the navy panel images of the pause menu, sleep chooser and main menu. The in-game HUD (bars, hotbar, inventory) stays navy / cyan. Values and layout live in `AgentScripts/Save/SaveSetup` (`Assets` sets every MenuStyle value; `Hud`, `MainMenu`, `SlotsPanel`, `Row`, `Confirm`): change them there and re-run. `MainMenu()` builds the scene on its own and opens the previous scene again (it refuses while that one has unsaved changes).

## Item use and effects

- `ItemUseHud` (Center): while the held item is a `HeldConsumable` being used, the green ring fills with `UseProgress` and the item's name sits above; a refusal ("Health full") shows in red for 1.5 s instead (LateUpdate poll; the label is rebuilt only when its text changes).
- `EffectsHud` (BottomLeft, above the bars): one entry per `PlayerEffects.Active` (up to 6): the item's icon and the seconds left, rebuilt only when the whole seconds change; healing entries dim while a hit pauses healing.
- **Icons**: items use icons from the UI icon packs in `Assets/_StoreAssets/UI`, one set and style for a kind of item (ASSETS.md → UI).

## Loot markers and pickup feed

- `LootMarkerHud` (`LootMarkers`, under the inventory screen): every 0.2 s (unscaled) it picks the nearest 8 `LootRegistry` containers holding loot within 15 m (sorted insert into fixed arrays, no allocation); each frame it projects only those (`Camera.main.WorldToScreenPoint` of `LootContainer.MarkerPoint`) and places the cloned markers: `UnsearchedColor` while something is hidden, `SearchedColor` (dim) after, scaled down to `FarScale` at the range's edge. Hidden behind the camera, for the container the player looks at, while a menu holds the UI and while dead. Values on the component.
- `PickupFeedHud` (BottomRight): `Equipment.PickedUp` adds "+count Name" at the bottom (the same item while still showing adds up on its line), `PickUpBlocked` shows "Backpack full", entering Overloaded shows "Too heavy" with `InventoryStyle.OverloadedSound`, and a sprint or jump refused for the load (`IPlayer.LoadRefusals` changed) shows it again with the refused sound unless the line is still up; lines hold 2.5 s and fade over 0.6 s on unscaled time, then the component does nothing until the next pickup. Colours and times on the component. It plays the pickup sound (Tokens have their own) and the refused sound from `InventoryStyle`, at most one per 0.06 s, so the magnet collecting several piles sounds once.

## Interact prompt

`_Core/UI/Prefabs/Widgets/InteractPrompt.prefab`: "[E] Pick up Rifle".

```text
InteractPrompt               InteractPromptView component
└─ Content                   HorizontalLayoutGroup + ContentSizeFitter: grows with the text; shown / hidden as a whole
   ├─ KeyCap                 36×36 key sprite + bold TMP label: the Interact key only ("E", not "Hold E")
   └─ Text                   TMP: verb in VerbColor, target name in TargetColor
```

- `InteractPromptView` API: `SetKey(string)`, `Show(verb, target, holdVerb, holdTarget)` (rebuilds the text only when it changes; a hold use adds a smaller second line, `HoldPrefix` + verb + target: "Hold: Repair All Fences (3)"), `Hide()`, `ApplyStyle()`.
- `InteractPromptHud` polls `Player.Interactor` in `LateUpdate`, because the focus changes as you look around. Its optional `_toolView` (ToolPrompt) shows `Player.Repair`'s prompt on the Reload key, above the E line, while the hammer is in hand. Its optional `_collectView` (CollectPrompt) shows `PlayerInteractor.CollectPrompt` on the Collect key (C), below the E line, while the focus offers something to collect (an `ICollectInteractable`: a farm animal's Tokens).
- The text comes from the focused `IInteractable` ([EQUIPMENT.md → Pickup, swap and drop](EQUIPMENT.md#pickup-swap-and-drop)).

## Changing the look

| To change | Edit |
| --- | --- |
| Sprites, colors, font, font size, trail, label on/off | `Data/UI/HealthBarStyle.asset` / `StaminaBarStyle.asset`. Bars in the open scene repaint right away; everything else picks the change up in `Awake` |
| A new look for one bar | Duplicate a style asset and assign it to that bar's `StatBar._style` |
| Bar size, icon size, padding | `StatBar.prefab` RectTransforms. Every bar updates |
| Hotbar sprites, colors, key font, selected scale, ammo font / size / colors | `Data/UI/HotbarSlotStyle.asset` |
| Hotbar slot size, key cap size | `HotbarSlot.prefab` RectTransforms. Outline / box thickness: the images' Pixels Per Unit Multiplier |
| Spacing between hotbar slots | `Hotbar` → HorizontalLayoutGroup spacing |
| Item icons | the item's `Content/<Kind>/<Item>/<Item>.asset` → `Icon` |
| Interact prompt sprite, fonts, colors, font size | `Data/UI/InteractPromptStyle.asset` |
| Interact prompt position | `HUD` → `Center/Interact` anchored position |
| Clock fonts, sizes, colors, backing panel, phase labels / colors, day format | `Data/UI/ClockStyle.asset` |
| Clock position, size, text placement | `HUD` → `TopCenter/Clock` RectTransforms (Clock size, Time and Labels offsets) |
| Minimap sprites, colours, letters, range, enemy dot size | `Data/UI/MinimapStyle.asset` |
| Minimap size, position | `HUD` → `TopRight/Minimap` RectTransform (the radius is half the `Disc` width) |
| A marker kind's icon, colours, size, pin to the rim | `Data/Map/<Kind>.asset` (`MapMarkerKind`) |
| Crosshair look (dot / line size, color, outline) | the reticle prefab, `Prefabs/Widgets/Reticles/HipReticle_Cross.prefab` |
| Which reticle the HUD uses | `HUD` → `Center/Crosshair` → `CrosshairHud` → Reticle Prefab |
| Cross minimum gap, open / close speed, sprint fade speed | `Data/UI/CrosshairStyle.asset` |
| Hit marker colors, durations, pops; how far above the hit a number starts | `Data/UI/HitFeedbackStyle.asset` (sounds and the number prefabs: `HitFeedbackHud`) |
| Hurt flash color, max alpha, edge thickness, strength by damage, fade-out | `Data/UI/HurtFeedbackStyle.asset` (the pain grunt: `Data/Player/PlayerSoundTuning.asset` → Hurt; the view flinch: `PlayerCameraTuning.asset` → Hurt flinch) |
| Damage number look (normal white, critical red) | `Prefabs/Widgets/DamageNumber.prefab`, `DamageNumber_Critical.prefab` (a variant) |
| QTE sprites, colors, key cap, success / miss animation | `Data/UI/RepairQteStyle.asset` (`QteStyle`) |
| How the crosshair moves | `Data/Player/PlayerHeldItemTuning.asset` → Aim sway ([WEAPONS.md](WEAPONS.md#aim-sway)) |
| Spacing between bars | `Vitals` → VerticalLayoutGroup spacing |
| Distance from the screen edge | `HUD` → `SafeFrame` offsets |
| Overall HUD scale | `HUD` → CanvasScaler reference resolution / match |

- Bar instances in the HUD must not override their RectTransform size. If one does, right-click Size → Revert, or it stops following `StatBar.prefab`.
- A style sprite left empty draws a flat color rectangle.
- Fill and Trail are always `Filled` images. A 9-sliced sprite will show stretched on them, so use sprites with a similar aspect ratio, or switch the widget to an anchor-based fill if sliced art is ever needed.
- A sprite set to `None` on the icon hides the icon frame, and the bar keeps its width.

## Adding HUD info

1. **A new meter** (hunger, thirst, oxygen): make a style asset in `Data/UI/`, drop a `StatBar.prefab` instance under `Vitals` (the layout group stacks it), assign the style, and feed it from a presenter.
2. **A new widget type** (counter, objective line, prompt): add a passive view under `_Core/UI/Widgets/` with a `*Style` asset if it has art, make its prefab in `Prefabs/Widgets/`, and place it in the right region.
3. **New data to show:** add a presenter under `_Core/UI/Hud/` that injects its source (a service; for player data derive from `PlayerHudView`) and pushes values into widgets. Use polling for values that change continuously. For rare changes (currency, objectives), subscribe to MessagePipe events.
4. Add the scripts to [CODE_STRUCTURE.md](CODE_STRUCTURE.md).


## Death screen

`HUD.prefab` → `DeathScreen` (built by `AgentScripts/DeathScreen/DeathScreenSetup.All()`): its own nested Canvas, sort order 100, over the rest of the HUD. Look and timing: `Data/UI/DeathScreenStyle.asset`; art, font and title material in `Data/UI/DeathScreen/`.

```text
DeathScreen                  nested Canvas (override sorting 100) · CanvasGroup (ignores the HUD's) · DeathScreenView (DeathScreenStyle) · DeathScreenHud
├─ Eyes                      stretched; shown while the eyes close
│  ├─ Darkness               black Image, alpha = the step's darkness
│  ├─ UpperLid / LowerLid    RawImage of Eyelid.png (lower one flipped by uvRect), each as tall as the screen, anchored at its middle
└─ Menu                      stretched · CanvasGroup (fades) · "Walk it off!": a sunset with the dog dragging the player home
   ├─ Sky                    RawImage: the style's Sky Gradient as a 1×64 texture built at runtime (the prefab keeps a flat colour)
   ├─ Sun                    Image (sun.png, with its own clouds)
   ├─ Cloud1…6               Images (cloud_1…3), top-centre anchored; each sways sideways (CloudDrift / CloudDriftPeriod)
   ├─ Mountains1…3           Images (mountains.png, middle one mirrored, overlapping), sitting on the ground line
   ├─ TreesBack / Trees      Images (trees.png), bottom-left
   ├─ Ground                 dark band, 162 px, bottom-stretched (GroundColor)
   ├─ Dog                    Image (dog_player.png), standing on the ground line
   ├─ Title / Subtitle       TMP, top-anchored: "WALK IT OFF!" (shown upper case) and the dog line; Varela Round (DeathScreenFont), underlay shadow on the title (DeathScreenFont_Title.mat); both shrink to fit long translations
   ├─ Status                 TMP "Recovering to Base..." / "Click to continue" (pulses), above the bar
   └─ ProgressBar            Image (progress_bar_background.png, 9-sliced) › FillArea › Track (cream) + Clip (RectMask2D, right anchor = progress) › Fill (progress_bar_fill.png at full width, so the stripes don't stretch)
```

The scene layers are laid out in canvas units (1920 × 1080): the scene sits on the ground line and the text hangs from the top, so other aspects only gain sky or sideways room. Art: `Assets/_Placeholder/UI/DeathScreen/` (AI-generated, see its `SOURCES.txt`; the title, subtitle and loading-text images of the mockup are not used, the text is TMP).

- **HUD hidden while dying:** `HudVisibilityHud` on the HUD root fades the HUD's CanvasGroup out on `PlayerDied` (0.35 s) and back in on `PlayerRevived` (0.5 s). The death screen's own CanvasGroup has `ignoreParentGroups`, so it stays visible. Fade times are on the component.
- **Flow:** `PlayerRespawnFlow` (root, `PlayerSystem`) runs the respawn; `DeathScreenHud` (a `PlayerHudView`) only presents it and switches no input. `PlayerDeathSequenceEnded` (the death sequence's `EndAfter`, 6 s after the camera settles) → the flow publishes `PlayerRespawnFading` and waits for its subscribers: the HUD's `CloseEyes` plays the style's eye steps (by default: close to 40%, open to 30%, close to 80%, open to 70%, shut and black), then the menu fades in with "Recovering to Base..." and the filling bar → the flow takes the UI input (`InputService.RequestUI`, cursor free), moves the body to its spawn point and awaits `PlayerRespawning` (the HUD holds it for at least `MinLoadingTime`) → stage Ready: "Click to continue" → a click or Submit (`InputService.UiConfirmPressed`) → `Player.Revive` → `PlayerRevived` → the flow releases the UI input and its stage goes to None → the menu fades out over the respawned player. Any revive (the L cheat) ends it the same way. Without a HUD the flow still runs (the click revives).
- **Eyelids:** `Eyelid.png` is black above an arched soft edge, so a half-closed pair leaves a lens-shaped gap. A step's `Closed` is the share of the view the lids cover in the middle (0.4 = 40%), measured where the lid turns solid; the lids first slide in from off-screen over the first 10%. `EyelidSolidAt` (arch + soft edge, 0.3) is where the texture turns solid and how far past the middle a shut lid goes, so a shut pair leaves no seam. To change the shape, edit and rerun `AgentScripts/DeathScreen/gen_textures.py` (numpy).
- **Not raycast:** nothing on it is clickable; the click is read from the UI input map, so the canvas has no `GraphicRaycaster`.
- **Progress bar:** `DeathScreenHud` fills it to `BarBeforeReady` (0.9) over `MinLoadingTime` when the menu shows, then to full over `BarFinishTime` when the stage is Ready (the status turns to "Click to continue"). It is a look, not a real loading measure.
- **To change:** the sky gradient, layer tints (sun, clouds, mountains, trees, dog, ground, bar), cloud drift, fonts, sizes, colours, texts, eye steps, fades, minimum loading time, continue pulse → `DeathScreenStyle.asset`; the layers' placement → `HUD` → `DeathScreen/Menu` RectTransforms, or `AgentScripts/DeathScreen/DeathScreenSetup.cs` and re-run `All` (it rebuilds the canvas). `Glow.png` is no longer used. Text rows: `death.title`, `death.subtitle`, `death.loading`, `death.continue` in `HUD.csv`.

## Clock

A digital clock as in Escape from Duckov, on `SafeFrame/TopCenter/Clock` (built by `AgentScripts/DayNight/DayNightSetup.Hud()`). The clock itself: [DAYNIGHT.md](DAYNIGHT.md).

```text
Clock                        296×76, hanging from TopCenter · nested Canvas · ClockView (ClockStyle) · ClockHud
└─ Content                   stretched; shown / hidden as a whole
   ├─ Backing                stretched Image: translucent rounded panel (built-in UISprite, 9-sliced)
   ├─ Time                   left 16–172 px: TMP "07:00", monospaced digits, header font + underlay
   └─ Labels                 right of the time
      ├─ Phase               top half: "Dawn" / "Morning" / "Afternoon" / "Dusk" / "Evening" / "Midnight", coloured per phase
      └─ Day                 bottom half: "Day 1" (ClockStyle.DayFormat)
```

- Fixed rects, no layout groups: the digits are monospaced, so the clock never changes size, and nothing layout-driven is written as an override onto the HUD instance in a scene.

- `ClockView` API: `SetTime(hour, minute)`, `SetPhase(label, color)`, `SetDay(day)`, `Show(bool)`, `ApplyStyle()`. Each setter skips repeats; the time is written from a cached char buffer (`SetCharArray`), so updates allocate nothing.
- `ClockHud` resolves the `WorldClock` if the scene has one (else the clock hides) and polls it in `LateUpdate`: 24-hour time, one-minute steps (every 0.75 s at 45 s per game hour).
- A weather line goes under `Labels` when weather exists.

## Minimap

A radar disc on `SafeFrame/TopRight/Minimap` (built by `AgentScripts/Hud/MinimapSetup.Build`, looks after Synty's `HUD_Apocalypse_Minimap_Dial_01`, its sprites only). No terrain: only what is around the player, heading up (what the player faces is always up), with N / E / S / W turning around the rim (N in `NorthColor`). Always on for now; the GDD's "HUD earned back" unlock is not built.

```text
Minimap                      220×220 · nested Canvas · MinimapView · MinimapHud
└─ Content                   stretched; shown / hidden as a whole (no player, or MinimapStyle.Enabled off)
   ├─ Disc                   stretched; half its width is the radius
   │  ├─ Background          dark translucent disc
   │  ├─ ViewCone            fixed, pointing up, centre to rim
   │  ├─ Markers             MarkerTemplate (background + Icon child), cloned _maxMarkers (12) times
   │  ├─ Enemies             EnemyTemplate (red dot), cloned _maxEnemies (48) times
   │  ├─ Player              fixed arrow in the middle
   │  └─ Ring                rim with ticks
   └─ Directions             N, E, S, W TMP letters placed on the rim each frame (upright)
```

- **What shows:** every living enemy in `EnemyRegistry` (TryResolve: none in scenes without an EnemyInstaller) within `MinimapStyle.Range` (69 m to the rim; 60 until 2026-11-24, zoomed out 15% by request), hidden beyond; and every `MapMarker` in `MapMarkerRegistry` (root scope). A marker beyond range sits on the rim as a direction pointer when its `MapMarkerKind.PinToEdge` is on, else hides. Height is ignored.
- **Adding a marker:** put a `MapMarker` with a kind (`Data/Map/Home`, `Data/Map/Objective`, `Data/Map/Dog` (the dog's paw, DOGS.md), or a new `MapMarkerKind` asset) on the object. It registers when injected: a scene-placed one goes in the scene scope's **Auto Inject Game Objects** (`MinimapSetup.HomeMarker` does it for `Home Interior/Home Marker` in WorldMap); a spawned one needs injecting by whatever spawns it.
- `MinimapView` API: `SetHeading(degrees)`, `SetEnemies(positions, count)`, `SetMarker(index, position, kind)`, `HideMarkersFrom(index)`, `Show(bool)`, `ApplyStyle()`. Positions are pixels from the centre; the maths is `MapSystem/MinimapMath` (tested in `MinimapTests`).
- `MinimapHud` rebuilds every frame in `LateUpdate` (the disc turns with the view, so every icon moves anyway): one pass over the enemies and markers, no allocations; the view only toggles pooled objects whose state changed.

## Scope view

A full scope (the 4× scope) covers the screen when aimed through: `ScopeHud` reads `HeldItem.Scope` (weight 0..1 and the reticle sprite) each LateUpdate, fades the `ScopeView` node in over the last 20 % of raising the sights, and keeps it on `Player.CrosshairPosition` so the aim sway moves it like the sights. The mask is `Content/Weapons/_Shared/Optics/ScopeMask.png` (clear inside, a narrow dark edge from 92 % of the radius, a thin grey tube ring lit a little from above from 97.5 %, black past it; the greys are in the texture, so the `Mask` image is white) with black bars past each side; the reticle is the optic's `ScopeReticle`. At full weight the held layer stops drawing (`FirstPersonView.HideHeld`). Its canvas is off while no scope view shows. Raising the sights, the circle opens from 70 % of its size and from dark (`Dim`, 85 % black) as the eye finds the scope. While aimed, the circle and its bars (`Pupil`) slide with `ScopeView.EyeOffset` (the scope off the eye's line from sway and kick, in eye boxes) by 6 % of half the screen height per eye box, and a soft dark rim (`Shadow`, `ScopeShadow.png`, twice the circle's size, under the mask) slides 22 %, so the edge the scope moves away from darkens. Tune on `ScopeHud` (`_pupilShift`, `_shadowShift`, `_openFrom`, `_dimFrom`) and per optic (`EyeBox`).

## Crosshair

`CrosshairHud` (presenter on `SafeFrame/Center/Crosshair`) polls the active player in `LateUpdate`:

- **Reticle:** one design at a time, a prefab with a `HipReticle` (a dot plus spread parts, each a graphic and the direction it opens in, pivot on its inner end). `CrosshairHud` spawns its Reticle Prefab under `Crosshair` on Awake; `SetReticle(prefab)` swaps it at runtime (for unlockable reticles or a settings choice). A new design is a new prefab in `Prefabs/Widgets/Reticles/` (duplicate `HipReticle_Cross`, change its graphics, sizes and part directions); the parts can be any number at any angle, or none.
- **Position:** `Player.CrosshairPosition` (the aim sway with the gun's hip wobble, in half-screen-heights) × half the root canvas height moves the whole `Crosshair` node, so the reticle wobbles with the barrel. Shots go exactly to its centre with `Camera` / `MuzzleToCrosshair` aim. Moves under half a pixel are skipped; its own nested Canvas keeps the per-frame move from rebuilding the HUD. The hit marker is a sibling of the reticle, so it pops on the current aim.
- **Cross size:** the spread parts sit at `Player.CrosshairSpread` (the held item's `Spread`: the hip wobble envelope plus pellet spread) × half the canvas height, never under `MinGap` px (10), eased at `OpenSpeed` (40) while growing and `CloseSpeed` (8) while shrinking (`Data/UI/CrosshairStyle.asset`): a shot or a step opens it at once, standing still lets it close. Standing with the AK it's about 40 px at 1080p.
- **Fade:** the reticle (`CanvasRenderer` alpha), not the hit marker. Hidden in proportion to the held item's `AimWeight` (the gun's sights take over) and faded out at `FadeSpeed` while sprinting. Items with no spread (axe, consumables, empty hands) show only the dot.
- On unbind it snaps back to the centre, fully visible.

## Hit feedback

`HitFeedbackHud` sits on `SafeFrame/Center/Crosshair/HitMarker` (an X of four 2.5×9 px ticks round the reticle's centre, in a CanvasGroup). It subscribes to `DamageDealt` and reacts only to hits whose `Info.Source` is the active player (not the player hurting itself):

- **Marker:** pops to 1.3× and fades over 0.18 s; on a kill it turns red, pops to 1.8× and fades over 0.4 s. Unscaled time, so a hit stop doesn't freeze it. Several hits in one frame (shotgun pellets) mark once; a kill among them wins.
- **Sound:** `Combat/HitMarker` or `Combat/KillConfirm` BroAudio entities, once per frame.
- **Damage number:** `Prefabs/Widgets/DamageNumber.prefab` (Damage Numbers Pro mesh number, 3D, constant screen size, font size 3, 0.8 s, one number per hit (combination off: merged totals read as one big hit), pooled 40, prewarmed on Start) `HitFeedbackStyle.NumberLift` (0.5 m) above the hit point, white; it only rises straight up (1.6, no sideways drift, slide or pop) so the target stays visible. Critical hits (headshots, `DamageInfo.Critical`) spawn `DamageNumber_Critical.prefab` instead: a variant with red text (and its own spam group, `Critical`, in case combination is turned back on). Colors live on the prefabs, never set at spawn: a tinted pooled number keeps the tint. Its font is the first-party copy `Data/UI/DamageNumberFont.asset` (DNP's setup rewrites its font's material, so the vendor asset stays untouched).

Colors, durations and pop sizes are in `Data/UI/HitFeedbackStyle.asset` (`HitFeedbackStyle`); the sounds and the number prefab stay on the component. With no style assigned it warns and uses the defaults (so does `CrosshairHud` without its `CrosshairStyle`).

## Hurt feedback

`HurtFeedbackHud` (a `PlayerHudView` on `HUD/HurtFeedback`, built by `AgentScripts/Hud/HurtFeedbackSetup.Build`) flashes the screen edge red toward whatever hurt the active player. It subscribes to `DamageDealt` and reacts only to hits whose target is the active player:

- **Direction:** from the hit's `Info.Source` (its `Info.Point` when it has none) against the yaw of `IPlayer.View`, on the ground plane: in front lights the top edge, behind the bottom, left and right their sides, shared by angle (`EdgeWeights`). A hit with no direction (the player as its own source, a fall; a source under 0.3 m away) lights every edge.
- **Strength:** `max(MinStrength, applied / max health / FullStrengthShare)` × `MaxAlpha` × the edge's share: a quarter of max health or more flashes fully, a light hit at 35 %. An edge keeps the brighter of its current and the new flash, so pellets in one frame count once and a light hit never cuts a strong one short.
- **Fade:** eases out over `FadeOut` (0.7 s) in real time, so a hit stop doesn't hold it; frozen while `TimeService.IsPaused`. The component turns itself off once every edge has faded.
- **Look:** the four `RawImage` edges use two white gradient textures made in code (no art asset), tinted `FlashColor`, each `Thickness` (0.2) of the HUD height deep. Sizes are recomputed at runtime from the live HUD height; the prefab's are placeholders for 1080 px. Style edits show on the next hit in Play mode.
- It draws under the widgets, so the bars stay readable, and hides with the rest of the HUD while the player is dead (`HudVisibilityHud`). Damage that applies nothing (`cheat.god`) publishes no `DamageDealt`, so nothing flashes.
- The pain grunt and the view flinch belong to the player: `PlayerFeedback` reacts to `PlayerLife.Hurt` (a hit that doesn't kill).

## Low health

`LowHealthHud` (a `PlayerHudView` on `HUD/LowHealth`, child 1 with its own canvas, built by `AgentScripts/Feel/FeelSetup.Hud`) warns that the active player is badly hurt. Under `Below` (35 % health) a red vignette pulses, growing from `StartStrength` to full at `FullAt` (10 %), faster as it grows (`PulseRate` → `PulseRateFull`), and `Heartbeat` plays at the top of every pulse, louder with the strength. Real time (a hit stop doesn't hold it), frozen while paused, gone while dead; it fades with the HUD. The vignette texture is made in code (clear inside `ClearRadius` of the half-diagonal). Everything is in `Data/UI/LowHealthStyle.asset`; `Enabled` off removes it.

## Sleep screen

`SleepScreen` (`HUD/SleepScreen`, own canvas at sorting 150 with a raycaster, built by `AgentScripts/Sleep/SleepSetup.Hud` in the menus' style) shows a sleep from `SleepService`: the wake-up clock, the darkness, the running clock while asleep and a refusal line ([SLEEP.md](SLEEP.md)). Its EventSystem is active only while the clock is open.

## QTE

`SafeFrame/Center/RepairQte`: a nested Canvas whose centre anchor is offset (-80, -60) at 1080p, so a compact widget sits beside the gun near the screen centre. The circle is 112×112, the bar 210×20, with a small key cap below the selected view. No panel, title, countdown or result popup.

```text
RepairQte                    nested Canvas · QteHud (QteStyle)
└─ Content                   CanvasGroup: shown while a QTE runs and during its result
   ├─ Circle / Bar           TimingQteView (a QteView each), one chosen per QTE
   ├─ SuccessBurst           pre-placed ring image for the success burst
   ├─ KeyCap                 key sprite + TMP label
   └─ Panel, Title, Hint, Remaining   disabled
```

- **`QteHud`** (a plain MonoBehaviour, not a `PlayerHudView`) shows whatever QTE is running, whoever started it (a gun's jam repair today), from the QTE's own events: `QteStarted` picks the view whose ID matches the presentation (the challenge's `Presentations`; one ID forces that view, a missing one falls back to a compatible view) and labels the key with `InputService.GetKeyLabel(started.Button)`; `QteAttempted` Miss flashes and shakes it; `QteEnded` Success plays the burst, any other end fades it out over `EndFadeDuration`. It never reads the gun. It hides while `TimeService.IsPaused` or while gameplay input is off (a menu) and shows the same session again afterwards; idle frames skip all work.
- **Views:** `TimingQteView` draws a timing session as a circle or a bar behind the `QteView` contract: the fill / marker show the cycle, the green segment the success interval. Pointer holds and green-zone moves after a miss belong to `TimingQteChallenge`, shared by both layouts. A new widget is a `QteView` with its ID in the HUD's views and the challenge's `Presentations`.
- **Results** (defaults in `QteStyle`): a miss flashes red, shakes the widget 22 reference pixels and squashes it 18% over 0.4 s; the fill keeps its no-attempt color until the next pass. Success fills the widget green, hides the pointer and key cap, pops to 150% and sends the ring out to 260%, then holds 0.32 s and fades: one second in all. Any other end fades over 0.18 s. DOTween sequences run on unscaled time (a success hit stop doesn't freeze them) and reset on disable or when the QTE goes away.
- **Look:** `Data/UI/RepairQteStyle.asset` (`QteStyle`): Synty Apocalypse ring / bar art, the Layer Lab key cap, colors, the key font (the first-party `RepairQteFont.asset` copy, which owns its material, atlas and glyph data) and the success / miss animation. Layout stays in the HUD prefab. Graphics are not raycast targets. How a gun's repair uses the QTE: [WEAPONS.md → Gun jam clearing and QTE](WEAPONS.md#gun-jam-clearing-and-qte).
