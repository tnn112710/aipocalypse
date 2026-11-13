# UI

How the in-game UI is built and how to extend or restyle it. Script locations and frame order are in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

## Rules

- **uGUI + TextMeshPro.** No UI Toolkit for the game HUD.
- **Art comes from Synty Interface Apocalypse HUD as sprites only** (`Assets/Synty/InterfaceApocalypseHUD/Sprites/`). Its Saira Condensed fonts are used through our own **static** copies in `Data/UI/Fonts/` (`SairaHeader`, `SairaBold`, `SairaBody` + `_Underlay` material presets, built by `AgentScripts/UI/FontSetup.cs` from Synty's TTFs): a dynamic atlas grows as new text appears and the editor saves it, so Play kept rewriting font assets. The atlases hold ASCII, the Vietnamese and French alphabets and every character in the string tables: after adding text with a new character, re-run `FontSetup.All` (`LocalizationTests` fails until then). `DeathScreenFont` and `RepairQteFont` are static too.
- **No text in code or prefabs.** Every word the player reads comes from the string tables ([LOCALIZATION.md](LOCALIZATION.md)): `LocalizedString` fields on styles and data, `Loc.X("key")` for text owned by code, `LocalizeStringEvent` on fixed labels (listed in `LocalizationKit.Labels`). Leave room for longer words and for Vietnamese accents above capitals. Never use or nest its prefabs: they carry Synty's sample scripts and animators. Our widgets are our own prefabs under `_Core/UI/Prefabs/`.
- **Views are passive.** A widget (`StatBar`, `HotbarSlotView`) only displays the values it's given. A presenter (`PlayerVitalsHud`, `HotbarHud`) reads game state and feeds the widgets. Widgets never know about the player, items, stats or events.
- **Looks live in style assets, layout lives in prefabs.** Sprites, colors and fonts go in a `*Style` ScriptableObject (`Data/UI/`). Size, position and anchors stay on the prefab's RectTransforms.
- **HUD images are not raycast targets** and the HUD canvas has no `GraphicRaycaster`, because nothing on it is clickable. Menus that need clicks get their own canvas with a raycaster and an EventSystem that's active only while they're open, its input module pointed at InputService's UI map (the inventory screen is the first: see below).
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
│                            on the crosshair) > Mask (ScopeMask), Left / Right / Top / Bottom black bars, Reticle; see Scope view below; built by
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
   │  ├─ Effects             nested Canvas · HorizontalLayoutGroup · EffectsHud: Effect template (icon + seconds), 130 px up
   │  └─ Vitals              nested Canvas · VerticalLayoutGroup · ContentSizeFitter · PlayerVitalsHud
   │     ├─ HealthBar        StatBar (HealthBarStyle)
   │     └─ StaminaBar       StatBar (StaminaBarStyle)
   ├─ BottomCenter
   │  └─ Hotbar              nested Canvas · HorizontalLayoutGroup · ContentSizeFitter · HotbarHud
   │     └─ Slot1…SlotN      HotbarSlot.prefab, spawned at Start, one per hotbar slot
   ├─ BottomRight
   │  └─ PickupFeed          nested Canvas · PickupFeedHud: Line0…Line4 ("+30 Rifle Ammo", newest at the bottom) + Warning ("Backpack full"), 150 px up
   └─ Center
      ├─ Crosshair           nested Canvas · CrosshairHud; 6×6 dot (built-in Knob sprite, white 90 %, thin dark outline)
      │  ├─ SpreadUp/Right/Down/Left   2×7 px ticks pushed out to the spread cone (pivot on the inner end)
      │  └─ HitMarker        HitFeedbackHud (HitFeedbackStyle)
      ├─ RepairQte           nested Canvas · QteHud (QteStyle): the running QTE beside the crosshair, see QTE below
      ├─ Interact            nested Canvas · InteractPromptHud, 120 px below the centre (hidden while a menu holds the UI)
      │  ├─ InteractPrompt   InteractPrompt.prefab (hidden while nothing is focused)
      │  ├─ ToolPrompt       InteractPrompt.prefab, 46 px above: the held repair tool's key ("[R] Repair Fence 63%")
      │  └─ CollectPrompt    InteractPrompt.prefab, 46 px below: the Collect key ("[C] Collect Tokens" on a farm animal)
      └─ ItemUse           nested Canvas · ItemUseHud: Root (Ring, radial Fill, Label) around the crosshair while a consumable is used
LootMarkers                  full screen, nested Canvas · LootMarkerHud: Marker (inactive 56 px bag icon + Shadow, cloned 8 times), placed
                             over crates and bodies holding loot (see Loot markers and pickup feed below)
Inventory                    the inventory screen, its own Canvas (sorting 50, off while closed) · GraphicRaycaster · CanvasGroup ·
                             InventoryScreen; built by AgentScripts/Inventory/InventorySetup (see Inventory screen below)
SaveToast                    full screen, nested Canvas · SaveToastHud: one TMP line bottom right, 230 px up ("Game saved · Save 2")
PauseMenu                    last child: Esc menu, its own Canvas (sorting 200, off while closed) · GraphicRaycaster · CanvasGroup
                             (ignores the HUD's death fade) · PauseMenu: Backdrop, Page (Title, Buttons, Hint), Slots (SaveSlotsPanel),
                             Confirm (ConfirmDialog), EventSystem (off while closed); built by AgentScripts/Save/SaveSetup.Hud (see Menus below)
```

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
Player (Construct / OnDestroy)                    ├─ CrosshairHud      polls Player.CrosshairPosition in LateUpdate
                                                  └─ HotbarHud         spawns slot views, listens to Hotbar / Equipment
```

`PlayerHudView.Player` is an `IPlayer`: the read-only state and respawn steps the HUD may use, never the `Player` MonoBehaviour itself.

## StatBar widget

`_Core/UI/Prefabs/Widgets/StatBar.prefab`, 368×41 at 1080p (label 28 pt):

```text
StatBar                      StatBar component
├─ IconFrame                 41×41, left; hidden when the style has no icon
│  └─ Icon                   preserve aspect
└─ Bar                       rest of the width
   ├─ Background
   ├─ Trail                  Filled, horizontal; lags behind drops (optional)
   ├─ Fill                   Filled, horizontal
   └─ Label                  TMP, current value rounded up
```

API, called by a presenter:

- `SetValue(current, max)`: updates the fill and label, and is safe to call every frame.
- `SetWarning(bool)`: swaps the fill to the style's `WarningColor`. Stamina uses it while exhausted.
- `ApplyStyle()`: repaints from the style. Runs in `Awake` and on edit (also an Odin button).

The trail (health and stamina) holds for `TrailDelay`, then slides to the new value at `TrailSpeed`. Only drops of at least `TrailHoldMinDrop` (bar fraction) restart the hold, so a steady drain is followed instead of frozen: stamina uses 0.02, so a jump, shot or swing shows its chunk while sprint drain slides along; health uses 0 (every hit holds). The component's `Update` runs only while the trail is moving.

## Hotbar

`_Core/UI/Prefabs/Widgets/HotbarSlot.prefab`, 84×84 tile + 30×30 key cap at 1080p:

```text
HotbarSlot                   HotbarSlotView component
├─ Tile                      scaled by SelectedScale while selected
│  ├─ Background             9-sliced box; dimmer when empty
│  ├─ Icon                   item icon (ItemDefinition.Icon), preserve aspect; hidden when empty
│  ├─ Frame                  selection outline, only on the selected slot
│  └─ Ammo                   TMP badge, bottom-right: "30/90" (magazine / reserve) for guns, a stack's count; hidden when the item shows no badge
└─ KeyCap                    key sprite + TMP label ("1", "2"… from the input binding)
```

- `HotbarSlotView` API: `SetKey(string)`, `SetIcon(Sprite)` (null = empty), `SetSelected(bool)`, `SetBadge(count, reserve, showReserve, warning)`, `HideBadge()`, `ApplyStyle()`.
- `HotbarHud` (presenter on `Equipment.Hotbar`) spawns one view per hotbar slot, so the HUD follows `EquipmentConfig`: today 10 (1, 2, V, 3–9). Each key cap is the slot's `HotbarKey` binding (`Equipment.KeyOf` → `InputService.GetKeyLabel`). It's **event-driven**: `Hotbar.SlotChanged` → icon, `Equipment.SelectionChanged` → highlight, each slot item's `ItemInstance.Changed` / `AmmoReserve.Changed` → badges. Nothing is polled.
- **Badge:** each item says what its slot shows (`ItemInstance.Badge`, an `ItemBadge`): a stack shows its count, a gun its rounds with its `AmmoType` as the reserve to look up. The reserve is shown only for an `AmmoType` when the reserve isn't infinite, so a gun reads as in Escape from Duckov: magazine / reserve, magazine only with infinite ammo, `AmmoWarningColor` when the magazine is empty or the gun is jammed. HotbarHud knows no item kinds.
- The highlight moves as soon as a slot is selected, not when the switch animation finishes.
- Behavior and switching rules: [EQUIPMENT.md](EQUIPMENT.md).

## Inventory screen

The first clickable UI: Tab or searching a crate or body (E) opens it, Tab / Esc close it. Laid out after Escape from Duckov at 1920×1080: the left panel (540×800, fixed) holds the Tokens carried, weight ("Weight 12.5 / 40.0 kg" + bar, red past the limit), EQUIPMENT (the two gun slots and the melee slot with their keys, then Head, Body, Face, Backpack, Light, each captioned, a silhouette when empty) and BACKPACK (n/N) with Sort and a scrolling grid of 5 columns; the loot panel (top right, under the clock) shows the crate or body being searched ("CRATE (5/8)") with Take all, its unrevealed slots as an "unknown" mark; the quick slots 3–9 sit along the bottom ("Drag items into slots 3-9 to set up your hotbar").

```text
Inventory                    Canvas (override sorting 50) · GraphicRaycaster · CanvasGroup · InventoryScreen
├─ Backdrop                  full screen, BackdropColor; raycast target: an item dropped here is thrown away
├─ Inspector                 centre panel 720×740 · ItemInspectorView: Content (Title, Info, StageFrame > Stage (RawImage · ItemPreviewStage ·
│                            ItemPreviewInput) + TurnHint, Slot_Optic … Slot_Magazine (ItemSlot), Stat0–13 / Value0–13), Hint, Leader; built by
│                            AgentScripts/Weapons/InspectorSetup, not by InventorySetup.Interface
├─ Left                      panel · VerticalLayoutGroup: Header (Tokens, TokensLabel, Weight, WeightBar > Fill), EquipmentTitle,
│                            EquipmentGrid (GridLayoutGroup 88 px, 5 columns), BackpackRow (BackpackTitle, Sort), BackpackScroll
│                            (ScrollRect > Viewport (RectMask2D) > Content: GridLayoutGroup + ContentSizeFitter)
├─ Loot                      panel: LootRow (LootTitle, Take all), LootGrid; shown only with a container
├─ Quick                     panel: Hint, QuickRow (HorizontalLayoutGroup)
├─ Message                   refusals ("Empty the backpack's extra slots first", "Backpack full"), 2.5 s
├─ Tooltip                   ItemTooltipView: name (rarity colour) + rarity, "30 x 0.016 kg = 0.48 kg", uses, value, beside the hovered slot
├─ DragIcon                  follows the pointer while dragging
└─ EventSystem               EventSystem + InputSystemUIInputModule, active only while open
```

- `ItemSlot.prefab` (`Prefabs/Widgets/`, 88×88): Background (raycast target), EmptyIcon (silhouette), Icon, Name (bottom; 22 px tall: the 13 pt font's line is ~20.5 px and a shorter box truncates the whole name), Count (top right), Key (top left), RarityBorder (the item's rarity colour, `InventoryStyle` → Rarity; none for Common), Hidden (an unrevealed item: Mark, the Synty map "unknown" icon, since the font has no `?`, and Ring, a radial fill shown on the slot being revealed), Highlight (hover), Caption (under the tile). `ItemSlotView` API: `Show(icon, name, count, warning, rarity)`, `ShowEmpty(silhouette)`, `ShowHidden(progress)`, `SetSearchProgress`, `SetKey`, `SetCaption`, `SetDragged`; it raises Clicked / DragStarted / Dragging / DragEnded / DroppedOn / HoverChanged and knows no items. Labels are rebuilt only when their value changes.
- `InventoryScreen` (a `PlayerHudView`) maps each slot view to a slot of a container (hotbar, gear, backpack, loot). Each grid keeps its own views, hooked once, and a rebuild only adds or hides the difference (0.4 ms; re-parenting pooled views on every open cost 5 ms). It refreshes them all when anything changes (`Inventory.Changed`, the loot container's `Changed` / `RevealChanged`; a flag read in `Update`, ~50 µs), and rebuilds when it opens or the backpack's size changes. **Drag** moves / merges / swaps (`Inventory.TryMove`), **Shift-drag** moves half a stack, **right-click** sends an item across (loot → inventory, backpack → loot, backpack → a free active or gear slot, active / gear → backpack), dropping on the backdrop throws it (`IPlayer.ThrowAway`). Sort: `Backpack.Sort()`; Take all: `Inventory.TakeFrom` per revealed loot slot. A loot slot still hidden by the search refuses every action. While a container is open, the screen ticks `PlayerLooting` on unscaled time and fills the ring of the slot being revealed each frame (one view).
- **Pause and input:** opening takes `TimeService.RequestPause(this)` (not during a raid: `RaidStarted` → `RaidEnded`; then a `DamageDealt` on the player closes the screen) and `InputService.RequestUI(this)` (UI map on, gameplay map off, cursor free) and activates its EventSystem; closing releases both. The input module is pointed at InputService's own UI map (`InputService.ConfigureUiModule`), so nothing but InputService enables actions. Shift comes from `InputService.UiShiftHeld`.
- **Closed, it costs nothing:** its Canvas is disabled (a canvas hidden only by alpha still batches every element each frame), its EventSystem is off, and `Update` only checks the Tab key.
- **No q in titles:** the Synty font's atlas has 74 characters and no lowercase `q`; TMP would add it to the vendor font asset. Titles are written in capitals ("EQUIPMENT") and use the UpperCase style.
- Built by `AgentScripts/Inventory/InventorySetup.Interface`; after changing layout, run `InventorySetup.Bake` (or `BakeCurrent` right after leaving Play) so the values layout groups and TMP compute in the scene are stored in HUD.prefab, and WorldMap doesn't get dirty with HUD overrides.

- **Sounds** (`InventoryStyle` → Sounds, 2D entities in the Player library): open, close, place (a successful move, right-click send or Take all), refused (with every message), reveal (each revealed stack).
- **Inspector** (the centre, after Duckov's, with the item in 3D): a left click on any item shows it there; opening the screen shows the item in hand. Name in its rarity colour, rarity / weight / value, then the 3D view (`ItemPreviewStage`: the item's world model, a gun upright with its parts, rendered by its own camera into a texture far below the world on the `ItemPreview` layer with `Preview_Renderer`, a copy of PC_Renderer without its features; it renders only while something is shown; drag to turn, wheel to zoom). A gun shows its part slots in Duckov's order with their parts' icons; hovering one draws a leader line to its mount on the model. Stats sit in two columns (damage, fire rate, magazine, reload, range, bullet speed, recoil up / sideways, aim time, spread for shotguns, move speed, hip spread, weight, zoom with a scope). While a part is dragged, the slot it fits lights up (`ItemSlotView.SetMarked`, `InventoryStyle.FitColor`) and the stats preview it in `BetterColor` / `WorseColor`. A part shows its changes instead; its tooltip lists them too. Parts are moved by `GunModding` ([WEAPONS.md → Gun parts and optics](WEAPONS.md#gun-parts-and-optics)).

## Menus

The pause menu, the main menu and the save slot list. What they do: [SAVE.md → Menus](SAVE.md#menus).

- **Pause menu** (`PauseMenu`, in `HUD.prefab`): Esc / gamepad Start (the Player map's `Pause`, read through `InputService.PausePressed`) opens it only while gameplay input is on and no other menu holds the UI. Open: `TimeService.RequestPause(this)`, `InputService.RequestUI(this)`, its EventSystem on (input module pointed at InputService's UI map). UI `Cancel` (Esc) steps back: dialog → slot list → menu → closed. Closing (or the HUD being destroyed by a scene load) releases both.
- **Main menu** (`MainMenuScreen`, `Assets/_Game/Scenes/MainMenu.unity`): a Screen Space Overlay canvas with a `HudScope` (root services, no `GameLifetimeScope` there), its EventSystem always on, `RequestUI` while the scene is open. A title screen: `Background` (the world shot, `AspectRatioFitter` envelope), `Shade` (left 62 %), the title, and text buttons.
- **Language** (`LanguageButton`, under Settings in both menus): each press switches to the next language (`LanguageService.Step`), and the label names the current one in itself ("LANGUAGE: TIẾNG VIỆT"). Everything on screen updates at once; the menus refresh their built text (hint, Continue info, version) on `LanguageChanged`. The pause menu's eight buttons are 62 px tall, so the column stays clear of the health bars. [LOCALIZATION.md](LOCALIZATION.md).
- **Shared widgets** (`UI/Menus/`): `SaveSlotsPanel` (one `SaveSlotRow` cloned per slot from an inactive template), `ConfirmDialog` (No selected first; closing gives the focus back to the button that asked). Both menus hand the panel the session with `Bind`; the pause menu forwards `GameSaved` to it (`ShowSaved`), because a panel never activated never gets `OnDestroy` to drop a subscription.
- **Feel:** every menu button has a `MenuButton`: the mouse over it selects it (one highlight for mouse and gamepad), getting the focus plays the hover tick, a click the click (`MenuButton.SelectSilently` gives focus without the tick, for a menu opening); its labels dim while it can't be pressed (the Button's tint only reaches the strip behind them). `MenuSounds` on each menu's root plays `MenuStyle`'s sounds through `AudioService` (Esc / B: the back sound). `MenuSelectionKeeper` next to each EventSystem gives the focus back to the last button when a mouse click clears it, so a gamepad is never left with nothing selected. Gamepad: d-pad / stick moves, South presses, East / Esc steps back.
- **Look:** `Data/UI/MenuStyle.asset`, one look for all menus (after Synty Interface Apocalypse's sample title scene): `SairaBold` buttons, `SairaHeader` titles, `SairaBody` small text; Synty's `Box_Small_02` tape strip (`ButtonSprite`, sliced at `CornerScale` 1.6) behind text buttons (clear until hovered or selected, `TextColors`), slot rows and dialog buttons (`Colors`: dark, the current game's slot in `Accent`, delete / overwrite in `Danger`); the left `Shade` gradient; the UI sounds. Values and layout live in `AgentScripts/Save/SaveSetup` (`Assets` sets every MenuStyle value; `Hud`, `MainMenu`, `SlotsPanel`, `Row`, `Confirm`): change them there and re-run. `MainMenu()` builds the scene on its own and opens the previous scene again (it refuses while that one has unsaved changes).

## Item use and effects

- `ItemUseHud` (Center): while the held item is a `HeldConsumable` being used, the green ring fills with `UseProgress` and the item's name sits above; a refusal ("Health full") shows in red for 1.5 s instead (LateUpdate poll; the label is rebuilt only when its text changes).
- `EffectsHud` (BottomLeft, above the bars): one entry per `PlayerEffects.Active` (up to 6): the item's icon and the seconds left, rebuilt only when the whole seconds change; healing entries dim while a hit pauses healing.
- **Icons**: items use icons from the UI icon packs in `Assets/_StoreAssets/UI`, one set and style for a kind of item (ASSETS.md → UI).

## Loot markers and pickup feed

- `LootMarkerHud` (`LootMarkers`, under the inventory screen): every 0.2 s (unscaled) it picks the nearest 8 `LootRegistry` containers holding loot within 15 m (sorted insert into fixed arrays, no allocation); each frame it projects only those (`Camera.main.WorldToScreenPoint` of `LootContainer.MarkerPoint`) and places the cloned markers: `UnsearchedColor` while something is hidden, `SearchedColor` (dim) after, scaled down to `FarScale` at the range's edge. Hidden behind the camera, for the container the player looks at, while a menu holds the UI and while dead. Values on the component.
- `PickupFeedHud` (BottomRight): `Equipment.PickedUp` adds "+count Name" at the bottom (the same item while still showing adds up on its line), `PickUpBlocked` shows "Backpack full"; lines hold 2.5 s and fade over 0.6 s on unscaled time, then the component does nothing until the next pickup. Colours and times on the component. It plays the pickup sound (Tokens have their own) and the refused sound from `InventoryStyle`, at most one per 0.06 s, so the magnet collecting several piles sounds once.

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
| Crosshair size, color | `HUD` → `Center/Crosshair` (RectTransform size, Image color, Outline) |
| Crosshair sprint fade speed, spread tick threshold / speed | `Data/UI/CrosshairStyle.asset` |
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
└─ Menu                      stretched · CanvasGroup (fades)
   ├─ Background             flat cyan
   ├─ Glow                   RawImage of Glow.png: soft light in the middle (vignette)
   ├─ Title                  TMP "You're dead", Varela Round (DeathScreenFont), underlay shadow (DeathScreenFont_Title.mat)
   ├─ Illustration           empty 640×230 rect between the title and the footer: the slot for an image / animation later
   └─ Footer                 dark band, 228 px, with a 3 px light Line on top
      └─ Status              TMP "Loading..." / "Click to continue" (pulses)
```

- **HUD hidden while dying:** `HudVisibilityHud` on the HUD root fades the HUD's CanvasGroup out on `PlayerDied` (0.35 s) and back in on `PlayerRevived` (0.5 s). The death screen's own CanvasGroup has `ignoreParentGroups`, so it stays visible. Fade times are on the component.
- **Flow:** `PlayerRespawnFlow` (root, `PlayerSystem`) runs the respawn; `DeathScreenHud` (a `PlayerHudView`) only presents it and switches no input. `PlayerDeathSequenceEnded` (the death sequence's `EndAfter`, 6 s after the camera settles) → the flow publishes `PlayerRespawnFading` and waits for its subscribers: the HUD's `CloseEyes` plays the style's eye steps (by default: close to 40%, open to 30%, close to 80%, open to 70%, shut and black), then the menu fades in with "Loading..." → the flow takes the UI input (`InputService.RequestUI`, cursor free), moves the body to its spawn point and awaits `PlayerRespawning` (the HUD holds it for at least `MinLoadingTime`) → stage Ready: "Click to continue" → a click or Submit (`InputService.UiConfirmPressed`) → `Player.Revive` → `PlayerRevived` → the flow releases the UI input and its stage goes to None → the menu fades out over the respawned player. Any revive (the L cheat) ends it the same way. Without a HUD the flow still runs (the click revives).
- **Eyelids:** `Eyelid.png` is black above an arched soft edge, so a half-closed pair leaves a lens-shaped gap. A step's `Closed` is the share of the view the lids cover in the middle (0.4 = 40%), measured where the lid turns solid; the lids first slide in from off-screen over the first 10%. `EyelidSolidAt` (arch + soft edge, 0.3) is where the texture turns solid and how far past the middle a shut lid goes, so a shut pair leaves no seam. To change the shape, edit and rerun `AgentScripts/DeathScreen/gen_textures.py` (numpy).
- **Not raycast:** nothing on it is clickable; the click is read from the UI input map, so the canvas has no `GraphicRaycaster`.
- **To change:** colors, font, sizes, texts, eye steps, fades, minimum loading time, continue pulse → `DeathScreenStyle.asset`; title / illustration / footer placement → `HUD` → `DeathScreen/Menu` RectTransforms. A tip line under the status (as in the reference) is not built yet.

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

- **What shows:** every living enemy in `EnemyRegistry` (TryResolve: none in scenes without an EnemyInstaller) within `MinimapStyle.Range` (60 m to the rim), hidden beyond; and every `MapMarker` in `MapMarkerRegistry` (root scope). A marker beyond range sits on the rim as a direction pointer when its `MapMarkerKind.PinToEdge` is on, else hides. Height is ignored.
- **Adding a marker:** put a `MapMarker` with a kind (`Data/Map/Home`, `Data/Map/Objective`, or a new `MapMarkerKind` asset) on the object. It registers when injected: a scene-placed one goes in the scene scope's **Auto Inject Game Objects** (`MinimapSetup.HomeMarker` does it for `Home Interior/Home Marker` in WorldMap); a spawned one needs injecting by whatever spawns it.
- `MinimapView` API: `SetHeading(degrees)`, `SetEnemies(positions, count)`, `SetMarker(index, position, kind)`, `HideMarkersFrom(index)`, `Show(bool)`, `ApplyStyle()`. Positions are pixels from the centre; the maths is `MapSystem/MinimapMath` (tested in `MinimapTests`).
- `MinimapHud` rebuilds every frame in `LateUpdate` (the disc turns with the view, so every icon moves anyway): one pass over the enemies and markers, no allocations; the view only toggles pooled objects whose state changed.

## Scope view

A full scope (the 4× scope) covers the screen when aimed through: `ScopeHud` reads `HeldItem.Scope` (weight 0..1 and the reticle sprite) each LateUpdate, fades the `ScopeView` node in over the last 20 % of raising the sights, and keeps it on `Player.CrosshairPosition` so the aim sway moves it like the sights. The mask is `Content/Weapons/_Shared/Optics/ScopeMask.png` (black outside a circle, darkening to its rim) with black bars past each side; the reticle is the optic's `ScopeReticle`. At full weight the held layer stops drawing (`FirstPersonView.HideHeld`). Its canvas is off while no scope view shows.

## Crosshair

`CrosshairHud` (presenter on `SafeFrame/Center/Crosshair`) polls the active player in `LateUpdate`:

- **Position:** `Player.CrosshairPosition` (the aim sway, in half-screen-heights) × half the root canvas height. Shots go exactly there with `Camera` / `MuzzleToCrosshair` aim. Moves under half a pixel are skipped; its own nested Canvas keeps the per-frame move from rebuilding the HUD.
- **Fade:** only the dot (its `CanvasRenderer` alpha), so the hit marker child still shows. Hidden in proportion to the held item's `AimWeight` (the gun's sights take over) and faded out at `FadeSpeed` while sprinting.
- **Spread ticks:** `Player.CrosshairSpread` (the held item's `Spread` cone, in half-screen-heights) × half the canvas height, eased at `SpreadSpeed`. They hide under `MinSpread` px (8) and fade in over `SpreadFade` px (`Data/UI/CrosshairStyle.asset`), so a rifle shows them only while airborne and a shotgun always; they share the dot's fade.
- On unbind it snaps back to the centre, fully visible.

## Hit feedback

`HitFeedbackHud` sits on `SafeFrame/Center/Crosshair/HitMarker` (an X of four 2.5×9 px ticks round the dot, in a CanvasGroup). It subscribes to `DamageDealt` and reacts only to hits whose `Info.Source` is the active player (not the player hurting itself):

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
- **Look:** `Data/UI/RepairQteStyle.asset` (`QteStyle`): Synty Apocalypse ring / bar art, the Interface Core key cap, colors, the key font (the first-party `RepairQteFont.asset` copy, which owns its material, atlas and glyph data) and the success / miss animation. Layout stays in the HUD prefab. Graphics are not raycast targets. How a gun's repair uses the QTE: [WEAPONS.md → Gun jam clearing and QTE](WEAPONS.md#gun-jam-clearing-and-qte).
