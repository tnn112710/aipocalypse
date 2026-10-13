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
HUD                          Canvas · CanvasScaler
└─ SafeFrame                 stretch, inset 48/40
   ├─ TopLeft                corner anchors (zero size): widgets anchor to these
   ├─ TopRight
   ├─ BottomLeft
   │  └─ Vitals              nested Canvas · VerticalLayoutGroup · ContentSizeFitter · PlayerVitalsHud
   │     ├─ HealthBar        StatBar (HealthBarStyle)
   │     └─ StaminaBar       StatBar (StaminaBarStyle)
   ├─ BottomCenter
   │  └─ Hotbar              nested Canvas · HorizontalLayoutGroup · ContentSizeFitter · HotbarHud
   │     └─ Slot1…SlotN      HotbarSlot.prefab, spawned at Start, one per hotbar slot
   ├─ BottomRight
   └─ Center
```

- **Region per corner.** New HUD info goes under the matching region: quest tracker in TopRight, currency in TopLeft, crosshair and interact prompt in Center, hotbar in BottomCenter, ammo in BottomRight.
- **A nested Canvas around anything that changes often.** `Vitals` has its own Canvas, so the bars changing every frame (stamina regen) only rebuild that group, not the whole HUD. Give each frequently updated group its own nested Canvas. Static decoration can share the root.

## Where the HUD lives

The HUD is nested inside `Player.prefab`, so any scene with a player has a working HUD and nothing has to be wired up in the scene. `PlayerScope` registers its `Player` (`RegisterInstance`, no second injection), and every `[Inject]` view in the HUD receives that player. HUD views can also inject anything from the parent scopes (root or `GameLifetimeScope`), so non-player info such as objectives or currency fits in the same canvas.

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

The trail (health only) holds for `TrailDelay`, then slides to the new value at `TrailSpeed`. The component's `Update` runs only while the trail is moving.

## Hotbar

`_Core/UI/Prefabs/Widgets/HotbarSlot.prefab`, 84×84 tile + 30×30 key cap at 1080p:

```text
HotbarSlot                   HotbarSlotView component
├─ Tile                      scaled by SelectedScale while selected
│  ├─ Background             9-sliced box; dimmer when empty
│  ├─ Icon                   item icon (ItemDefinition.Icon), preserve aspect; hidden when empty
│  └─ Frame                  selection outline, only on the selected slot
└─ KeyCap                    key sprite + TMP label ("1", "2"… from the input binding)
```

- `HotbarSlotView` API: `SetKey(string)`, `SetIcon(Sprite)` (null = empty), `SetSelected(bool)`, `ApplyStyle()`.
- `HotbarHud` (presenter on `Hotbar`) spawns one view per hotbar slot, so the HUD follows `EquipmentConfig` (up to 9). It's **event-driven**: `Hotbar.SlotChanged` → icon, `Equipment.SelectionChanged` → highlight. Nothing is polled.
- The highlight moves as soon as a slot is selected, not when the switch animation finishes.
- Behavior and switching rules: [EQUIPMENT.md](EQUIPMENT.md).

## Changing the look

| To change | Edit |
| --- | --- |
| Sprites, colors, font, font size, trail, label on/off | `Data/UI/HealthBarStyle.asset` / `StaminaBarStyle.asset`. Bars in the open scene repaint right away; everything else picks the change up in `Awake` |
| A new look for one bar | Duplicate a style asset and assign it to that bar's `StatBar._style` |
| Bar size, icon size, padding | `StatBar.prefab` RectTransforms. Every bar updates |
| Hotbar sprites, colors, key font, selected scale | `Data/UI/HotbarSlotStyle.asset` |
| Hotbar slot size, key cap size | `HotbarSlot.prefab` RectTransforms. Outline / box thickness: the images' Pixels Per Unit Multiplier |
| Spacing between hotbar slots | `Hotbar` → HorizontalLayoutGroup spacing |
| Item icons | the item's `Data/Items/*.asset` → `Icon` |
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
3. **New data to show:** add a presenter under `_Core/UI/Hud/` that injects its source (a service, or `Player`) and pushes values into widgets. Use polling for values that change continuously. For rare changes (currency, objectives), subscribe to MessagePipe events.
4. Add the scripts to [CODE_STRUCTURE.md](CODE_STRUCTURE.md).
