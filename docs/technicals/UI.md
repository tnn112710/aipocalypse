# UI

How the in-game UI is built and how to extend or restyle it. Script locations and frame order are in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

## Rules

- **uGUI + TextMeshPro.** No UI Toolkit for the game HUD.
- **Art comes from Synty Interface Apocalypse HUD as sprites only** (`Assets/Synty/InterfaceApocalypseHUD/Sprites/`, font `Fonts/SairaCondensed/Font_Apocalypse_Bold`). Never use or nest its prefabs: they carry Synty's sample scripts and animators. Our widgets are our own prefabs under `_Core/UI/Prefabs/`.
- **Views are passive.** A widget (`StatBar`, `HotbarSlotView`) only displays the values it's given. A presenter (`PlayerVitalsHud`, `HotbarHud`) reads game state and feeds the widgets. Widgets never know about the player, items, stats or events.
- **Looks live in style assets, layout lives in prefabs.** Sprites, colors and fonts go in a `*Style` ScriptableObject (`Data/UI/`). Size, position and anchors stay on the prefab's RectTransforms.
- **HUD images are not raycast targets** and the HUD canvas has no `GraphicRaycaster`, because nothing on it is clickable. Menus that need clicks get their own canvas with a raycaster.
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
└─ SafeFrame                 stretch, inset 48/40
   ├─ TopLeft                corner anchors (zero size): widgets anchor to these
   ├─ TopRight
   │  └─ Clock               nested Canvas · ClockView · ClockHud (world clock, see Clock below)
   ├─ BottomLeft
   │  └─ Vitals              nested Canvas · VerticalLayoutGroup · ContentSizeFitter · PlayerVitalsHud
   │     ├─ HealthBar        StatBar (HealthBarStyle)
   │     └─ StaminaBar       StatBar (StaminaBarStyle)
   ├─ BottomCenter
   │  └─ Hotbar              nested Canvas · HorizontalLayoutGroup · ContentSizeFitter · HotbarHud
   │     └─ Slot1…SlotN      HotbarSlot.prefab, spawned at Start, one per hotbar slot
   ├─ BottomRight
   └─ Center
      ├─ Crosshair           nested Canvas · CrosshairHud; 6×6 dot (built-in Knob sprite, white 90 %, thin dark outline)
      │  ├─ SpreadUp/Right/Down/Left   2×7 px ticks pushed out to the spread cone (pivot on the inner end)
      │  └─ HitMarker        HitFeedbackHud (HitFeedbackStyle)
      ├─ RepairQte           nested Canvas · QteHud (QteStyle): the running QTE beside the crosshair, see QTE below
      └─ Interact            nested Canvas · InteractPromptHud, 120 px below the centre
         └─ InteractPrompt   InteractPrompt.prefab (hidden while nothing is focused)
```

- **Region per corner.** New HUD info goes under the matching region: the clock (and later weather) in TopRight, quest tracker in TopRight below it, currency in TopLeft, crosshair and interact prompt in Center, hotbar in BottomCenter, ammo in BottomRight.
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
- `HotbarHud` (presenter on `Hotbar`) spawns one view per hotbar slot, so the HUD follows `EquipmentConfig` (up to 9). It's **event-driven**: `Hotbar.SlotChanged` → icon, `Equipment.SelectionChanged` → highlight, each slot item's `ItemInstance.Changed` / `AmmoReserve.Changed` → badges. Nothing is polled.
- **Badge:** each item says what its slot shows (`ItemInstance.Badge`, an `ItemBadge`): a stack shows its count, a gun its rounds with its `AmmoType` as the reserve to look up. The reserve is shown only for an `AmmoType` when the reserve isn't infinite, so a gun reads as in Escape from Duckov: magazine / reserve, magazine only with infinite ammo, `AmmoWarningColor` when the magazine is empty or the gun is jammed. HotbarHud knows no item kinds.
- The highlight moves as soon as a slot is selected, not when the switch animation finishes.
- Behavior and switching rules: [EQUIPMENT.md](EQUIPMENT.md).

## Interact prompt

`_Core/UI/Prefabs/Widgets/InteractPrompt.prefab`: "[E] Pick up Rifle".

```text
InteractPrompt               InteractPromptView component
└─ Content                   HorizontalLayoutGroup + ContentSizeFitter: grows with the text; shown / hidden as a whole
   ├─ KeyCap                 36×36 key sprite + TMP label (from the Interact binding)
   └─ Text                   TMP: verb in VerbColor, target name in TargetColor
```

- `InteractPromptView` API: `SetKey(string)`, `Show(verb, target)` (rebuilds the text only when it changes), `Hide()`, `ApplyStyle()`.
- `InteractPromptHud` polls `Player.Interactor` in `LateUpdate`, because the focus changes as you look around.
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
| Clock position, size, text placement | `HUD` → `TopRight/Clock` RectTransforms (Clock size, Time and Labels offsets) |
| Crosshair size, color | `HUD` → `Center/Crosshair` (RectTransform size, Image color, Outline) |
| Crosshair sprint fade speed, spread tick threshold / speed | `Data/UI/CrosshairStyle.asset` |
| Hit marker colors, durations, pops | `Data/UI/HitFeedbackStyle.asset` (sounds and the number prefabs: `HitFeedbackHud`) |
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

A digital clock as in Escape from Duckov, on `SafeFrame/TopRight/Clock` (built by `AgentScripts/DayNight/DayNightSetup.Hud()`). The clock itself: [DAYNIGHT.md](DAYNIGHT.md).

```text
Clock                        296×76, anchored to TopRight · nested Canvas · ClockView (ClockStyle) · ClockHud
└─ Content                   stretched; shown / hidden as a whole
   ├─ Backing                stretched Image: translucent rounded panel (built-in UISprite, 9-sliced)
   ├─ Time                   left 16–172 px: TMP "07:00", monospaced digits, header font + underlay
   └─ Labels                 right of the time
      ├─ Phase               top half: "Dawn" / "Morning" / "Afternoon" / "Dusk" / "Evening" / "Midnight", coloured per phase
      └─ Day                 bottom half: "Day 1" (ClockStyle.DayFormat)
```

- Fixed rects, no layout groups: the digits are monospaced, so the clock never changes size, and nothing layout-driven is written as an override onto the HUD instance in a scene.

- `ClockView` API: `SetTime(hour, minute)`, `SetPhase(label, color)`, `SetDay(day)`, `Show(bool)`, `ApplyStyle()`. Each setter skips repeats; the time is written from a cached char buffer (`SetCharArray`), so updates allocate nothing.
- `ClockHud` resolves the `WorldClock` if the scene has one (else the clock hides) and polls it in `LateUpdate`: 24-hour time, one-minute steps (every 0.5 s at 30 s per game hour).
- A weather line goes under `Labels` when weather exists.

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
- **Damage number:** `Prefabs/Widgets/DamageNumber.prefab` (Damage Numbers Pro mesh number, 3D, constant screen size, 0.8 s, combines rapid hits, pooled 40, prewarmed on Start) at the hit point, white. Critical hits (headshots, `DamageInfo.Critical`) spawn `DamageNumber_Critical.prefab` instead: a variant with red text in its own spam group (`Critical`), so crits combine only with crits. Colors live on the prefabs, never set at spawn: a tinted pooled number keeps the tint, and a combined number keeps the absorber's color. Its font is the first-party copy `Data/UI/DamageNumberFont.asset` (DNP's setup rewrites its font's material, so the vendor asset stays untouched).

Colors, durations and pop sizes are in `Data/UI/HitFeedbackStyle.asset` (`HitFeedbackStyle`); the sounds and the number prefab stay on the component. With no style assigned it warns and uses the defaults (so does `CrosshairHud` without its `CrosshairStyle`).

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
